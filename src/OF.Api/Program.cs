using Azure.Identity;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.OpenApi.Extensions;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Abstractions;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Configurations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using OF.Api.UseCases.Reservations;
using OF.Common;
using OF.Common.Infrastructure.CloudSuite.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(worker => worker.UseNewtonsoftJson())
    .ConfigureOpenApi()
    .ConfigureHostConfiguration(configHost =>
    {
        var keyVaultUri = Environment.GetEnvironmentVariable("KeyVaultUri", EnvironmentVariableTarget.Process) ?? throw new ArgumentException("KeyVaultUri must have a value");

        configHost.AddAzureKeyVault(new Uri(keyVaultUri), new DefaultAzureCredential()).AddEnvironmentVariables();
    })
    .ConfigureServices((hostBuilderContext, services) =>
    {
        _ = services
            .AddApplicationInsightsTelemetryWorkerService(options =>
            {
                options.EnableAdaptiveSampling = false;
            })
            .ConfigureFunctionsApplicationInsights()

            // Configure logging to use host.json settings
            .Configure<LoggerFilterOptions>(options =>
            {
                // Remove the default Application Insights rule that limits logging
                var defaultRule = options.Rules.FirstOrDefault(rule =>
                    rule.ProviderName == "Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider");
                if (defaultRule != null)
                {
                    options.Rules.Remove(defaultRule);
                }
            })
            .AddSingleton<IOpenApiConfigurationOptions>(_ =>
             {
                 var version = typeof(Program).Assembly.GetName().Version!.ToString().TrimEnd('0').TrimEnd('.');

                 var options = new OpenApiConfigurationOptions()
                 {
                     Info = new OpenApiInfo()
                     {
                         Version = version,
                         Title = $"Order Fulfillment Api",
                         Description = "Api calls and message processing functions for Order Fulfillment",
                     },
                     Servers = DefaultOpenApiConfigurationOptions.GetHostNames(),
                     OpenApiVersion = DefaultOpenApiConfigurationOptions.GetOpenApiVersion(),
                     IncludeRequestingHostName = DefaultOpenApiConfigurationOptions.IsFunctionsRuntimeEnvironmentDevelopment(),
                     ForceHttps = DefaultOpenApiConfigurationOptions.IsHttpsForced(),
                     ForceHttp = DefaultOpenApiConfigurationOptions.IsHttpForced()
                 };

                 return options;
             })
            .RegisterCommonDependencies(hostBuilderContext.Configuration)
            .AddTransient<ReservationRequestHandler>()
            .ConfigureCloudSuiteIntegrations(hostBuilderContext.Configuration);
    })
    .Build();

host.Run();

[ExcludeFromCodeCoverage]
public partial class Program
{
}