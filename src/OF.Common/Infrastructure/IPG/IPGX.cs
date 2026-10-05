using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using OF.Common.Infrastructure.Http;
using OF.Common.Infrastructure.IPG.Orders;
using OF.Common.Infrastructure.IPG.Orders.Auth;
using OF.Common.Infrastructure.IPG.Orders.Handlers;
using OF.Common.Infrastructure.IPG.Orders.Settings;
using OF.Common.Infrastructure.IPG.Pricing;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Refit;

namespace OF.Common.Infrastructure.IPG
{
    public static class IPGX
    {
        public static IServiceCollection AddIPGIntegrations(this IServiceCollection services, Action<OptionsBuilder<IntegrationSettings>> configure)
        {
            configure(services.AddOptions<IntegrationSettings>());

            services.AddSingleton(sp => sp.GetRequiredService<IOptions<IntegrationSettings>>().Value);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<IntegrationSettings>>().Value.OrderManagementClientSettings);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<IntegrationSettings>>().Value.OrderIntegrationClientSettings);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<IntegrationSettings>>().Value.PricingClientSettings);

            var transientBackoff = Backoff.DecorrelatedJitterBackoffV2(
               medianFirstRetryDelay: TimeSpan.FromSeconds(1),
               retryCount: 3);

            services
                .AddHttpClient("IPGAuth", (provider, client) =>
                {
                    client.BaseAddress = new Uri(provider.GetRequiredService<IntegrationSettings>().AuthenticationSettings.AuthenticationUrl);
                    client.DefaultRequestHeaders.Add("Accept", "application/json");
                })
                .AddTransientHttpErrorPolicy(p => p.WaitAndRetryAsync(transientBackoff));

            services.AddOptions<OAuthTokenProviderOptions>()
                .Configure<IOptions<IntegrationSettings>>((auth, integration) =>
                {
                    var settings = integration.Value;
                    auth.AuthenticationUrl = settings.AuthenticationSettings.AuthenticationUrl;
                    auth.Clients = new[]
                    {
                        ToCredentials(settings.OrderManagementClientSettings),
                        ToCredentials(settings.OrderIntegrationClientSettings),
                        ToCredentials(settings.PricingClientSettings)
                    };
                });

            services.AddSingleton<OAuthTokenProvider>();

            services.TryAddTransient<OAuthHttpHandler<OrderManagementClientSettings>>();
            services.TryAddTransient<OAuthHttpHandler<OrderIntegrationClientSettings>>();
            services.TryAddTransient<OAuthHttpHandler<PricingClientSettings>>();

            AddRefitClient<IOrderManagementIntegration, OrderManagementClientSettings>(services);
            AddRefitClient<IOrderIntegration, OrderIntegrationClientSettings>(services);
            AddRefitClient<IPricingApi, PricingClientSettings>(services);

            return services;
        }

        private static void AddRefitClient<T, TSettings>(IServiceCollection services)
            where T : class
            where TSettings : IntegrationClientSettings
        {
            var refitSettings = new RefitSettings
            {
                ContentSerializer = new NewtonsoftJsonContentSerializer(new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                })
            };

            var transientBackoff = Backoff.DecorrelatedJitterBackoffV2(
                medianFirstRetryDelay: TimeSpan.FromSeconds(1),
                retryCount: 3);

            services.TryAddTransient<LoggingHttpHandler>();
            services.TryAddTransient<CorrelationHttpHandler>();

            services.AddRefitClient<T>(refitSettings)
                .AddHttpMessageHandler<CorrelationHttpHandler>()
                .AddHttpMessageHandler<LoggingHttpHandler>()
                .AddTransientHttpErrorPolicy(p => p.WaitAndRetryAsync(transientBackoff))
                .AddPolicyHandler((provider, _) =>
                {
                    var logger = provider.GetRequiredService<ILogger<OAuthHttpHandler<TSettings>>>();
                    return Policy
                        .HandleResult<HttpResponseMessage>(r => r.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        .RetryAsync(
                            retryCount: 1,
                            onRetry: (x, _) => logger.LogInformation("HTTP {Method} {Uri}: Unauthorized, retrying...",
                                x.Result.RequestMessage!.Method, x.Result.RequestMessage.RequestUri));
                })
                .AddHttpMessageHandler<OAuthHttpHandler<TSettings>>()
                .ConfigureHttpClient((provider, client) =>
                {
                    var clientSettings = provider.GetRequiredService<TSettings>();
                    client.BaseAddress = new Uri(clientSettings.BaseAddress);
                    client.Timeout = clientSettings.Timeout;
                    client.DefaultRequestHeaders.Add("Accept", "application/json");
                    client.DefaultRequestHeaders.Add(clientSettings.IPGSubscriptionHeaderKey, clientSettings.IPGSubscriptionHeaderValue);
                });
        }

        private static OAuthClientCredentials ToCredentials(IntegrationClientSettings client) => new()
        {
            ClientId = client.ClientId,
            ClientSecret = client.ClientSecret,
            Audience = client.Audience
        };

    }
}
