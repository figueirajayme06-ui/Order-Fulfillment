using Azure.Data.Tables;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OF.Common.Infrastructure.CloudSuite.DependencyInjection;
using OF.Common.Infrastructure.CPQ;
using OF.Common.Infrastructure.IPG;
using OF.Common.Infrastructure.IPG.Orders;
using OF.Common.Infrastructure.IPG.Orders.Settings;
using OF.Common.Infrastructure.MDP;
using OF.Common.Infrastructure.MDP.Services;
using OF.Common.Infrastructure.OF;
using OF.Common.Infrastructure.Storage;
using OF.Data;
using System.Diagnostics.CodeAnalysis;

namespace OF.Common
{
    [ExcludeFromCodeCoverage]
    public static class DependecyRegistration
    {
        public static IServiceCollection RegisterCommonDependencies(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSystemTimeProvider();

            var storageAccountName = configuration["StorageAccountName"];
            var connection = configuration["StorageConnection"];
            var serviceBusNamespace = configuration["ServiceBusNamespace"];
            var serviceBusConnection = configuration["ServiceBusConnection"];

            ServiceBusClient client;
            if (!string.IsNullOrEmpty(serviceBusNamespace))
            {
                var credential = new DefaultAzureCredential();
                client = new ServiceBusClient(serviceBusNamespace, credential, new ServiceBusClientOptions
                {
                    TransportType = ServiceBusTransportType.AmqpWebSockets
                });
            }
            else
            {
                client = new ServiceBusClient(serviceBusConnection, new ServiceBusClientOptions
                {
                    TransportType = ServiceBusTransportType.AmqpWebSockets
                });
            }
            services.AddSingleton(client);

            services.AddSingleton<UpdateAgreementQueueClient>();
            services.AddSingleton<UpsertQuoteQueueClient>();
            services.AddSingleton<ActivateAgreementQueueClient>();
            services.AddSingleton<IActivateHeaderQueueClient, ActivateHeaderQueueClient>();
            services.AddSingleton<IAgreementMessageSynchronizer , AgreementQueueClient>();

            BlobServiceClient blobClient;
            TableServiceClient tableClient;

            if (!string.IsNullOrEmpty(storageAccountName))
            {
                var credential = new DefaultAzureCredential();
                blobClient = new BlobServiceClient(new Uri($"https://{storageAccountName}.blob.core.windows.net"), credential);
                tableClient = new TableServiceClient(new Uri($"https://{storageAccountName}.table.core.windows.net"), credential);
            }
            else
            {
                blobClient = new BlobServiceClient(connection);
                tableClient = new TableServiceClient(connection);
            }

            var container = blobClient.GetBlobContainerClient(HierarchyBlobClient.ContainerName);
            if (!container.Exists())
            {
                blobClient.CreateBlobContainer(HierarchyBlobClient.ContainerName);
            }
            services.AddSingleton(blobClient);
            services.AddSingleton<HierarchyBlobClient>();

            var table = tableClient.GetTableClient(WatermarkTableClient.TableName);
            table.CreateIfNotExists();

            services.AddSingleton(tableClient);
            services.AddSingleton<IWatermarkTableClient, WatermarkTableClient>();

            services.AddDataDependencies(configuration);

            var cpqConnectionString = configuration["CPQConnectionString"] ?? throw new ArgumentException("CPQConnectionString must have a value");
            services.AddDbContext<CPQDbContext>(options => options.UseSqlServer(cpqConnectionString));

            var mdpConnectionString = configuration["MDPConnectionString"] ?? throw new ArgumentException("MDPConnectionString must have a value");
            services.AddDbContext<MDPDbContext>(options => options.UseSqlServer(mdpConnectionString));

            var fdpConnectionString = configuration["FDPConnectionString"] ?? throw new ArgumentException("FDPConnectionString must have a value");

            services.AddDbContext<FDPDbContext>(options => options.UseNpgsql(fdpConnectionString));

            services.AddDbContextFactory<FDPDbContext>(options =>
            {
                options.UseNpgsql(fdpConnectionString);
            }, ServiceLifetime.Scoped);

            services.AddIPGIntegrations(opt => opt.Configure<IConfiguration>((settings, config) =>
            {
                var ipgRootAddress = (config["IPGBaseAddress"] ?? throw new ArgumentNullException("IPGBaseAddress must have a value")).TrimEnd('/');

                settings.AuthenticationSettings = new IntegrationAuthenticationSettings
                {
                    AuthenticationUrl = config["IPGAuthAuthenticationUrl"] ?? throw new ArgumentNullException("IPGAuthAuthenticationUrl must have a value")
                };
                settings.OrderManagementClientSettings = new OrderManagementClientSettings
                {
                    BaseAddress = $"{ipgRootAddress}/om/v1",
                    ClientId = config["IPGAuthClientId"] ?? throw new ArgumentNullException("IPGAuthClientId must have a value"),
                    ClientSecret = config["IPGAuthClientSecret"] ?? throw new ArgumentNullException("IPGAuthClientSecret must have a value"),
                    Audience = config["IPGAuthAudience"] ?? throw new ArgumentNullException("IPGAuthAudience must have a value"),
                    IPGSubscriptionHeaderValue = config["IPGAuthSubscriptionKey"] ?? throw new ArgumentNullException("IPGAuthSubscriptionKey must have a value")
                };
                settings.OrderIntegrationClientSettings = new OrderIntegrationClientSettings
                {
                    BaseAddress = $"{ipgRootAddress}/oi/v1",
                    ClientId = config["IPGOrderIntegrationClientId"] ?? throw new ArgumentNullException("IPGOrderIntegrationClientId must have a value"),
                    ClientSecret = config["IPGOrderIntegrationClientSecret"] ?? throw new ArgumentNullException("IPGOrderIntegrationClientSecret must have a value"),
                    Audience = config["IPGOrderIntegrationAudience"] ?? throw new ArgumentNullException("IPGOrderIntegrationAudience must have a value"),
                    IPGSubscriptionHeaderValue = config["IPGOrderIntegrationSubscriptionKey"] ?? throw new ArgumentNullException("IPGOrderIntegrationSubscriptionKey must have a value")
                };
                settings.PricingClientSettings = new PricingClientSettings
                {
                    BaseAddress = config["IPGBaseAddressPricing"] ?? throw new ArgumentNullException("IPGBaseAddressPricing must have a value"),
                    ClientId = config["IPGAuthClientId"] ?? throw new ArgumentNullException("IPGAuthClientId must have a value"),
                    ClientSecret = config["IPGAuthClientSecret"] ?? throw new ArgumentNullException("IPGAuthClientSecret must have a value"),
                    Audience = config["IPGAuthAudience"] ?? throw new ArgumentNullException("IPGAuthAudience must have a value"),
                    IPGSubscriptionHeaderValue = config["IPGSubscriptionHeaderPricingValue"] ?? throw new ArgumentNullException("IPGSubscriptionHeaderPricingValue must have a value")
                };
            }));

            services.ConfigureCloudSuiteIntegrations(configuration);

            services.AddScoped<ICoreFulfilmentEngine, CoreFulfilmentEngine>();
            services.AddScoped<ICoreDataRepository, CoreDataRepository>();
            services.AddScoped<OrderManagementService>();
            services.AddScoped<IAgreementLineFetcher, AgreementLineFetcher>();

            services.AddScoped<IRulesExtractor, RulesExtractor>();
            services.AddScoped<IRulesEvaluator, RulesEvaluator>();
            services.AddTransient<ISalesforceUserLanguageService, SalesforceUserLanguageService>();

            return services;
        }

        /// <summary>
        /// Adds the system <see cref="TimeProvider"/> that provides date and time services from the system.
        /// </summary>
        /// <param name="services"></param>
        public static void AddSystemTimeProvider(this IServiceCollection services)
            => services.TryAdd(ServiceDescriptor.Singleton(typeof(TimeProvider), typeof(SystemTimeProvider)));
    }
}