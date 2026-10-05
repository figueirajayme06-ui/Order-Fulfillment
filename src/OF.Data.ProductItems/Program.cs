using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.ApplicationInsights;
using Microsoft.Extensions.Logging;
using OF.Common;
using OF.Common.Infrastructure.CloudSuite.DependencyInjection;
using OF.Data.ProductItems.UseCases;

await using var app = AppBootstrap.Init<Program>(
    logLevel: LogLevel.Information,
    bootstrap: (services, config) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.AddLogging(logs => logs
            .AddApplicationInsights()
            .AddFilter<ApplicationInsightsLoggerProvider>(level => level >= LogLevel.Information)
            .AddFilter("Azure.Core", LogLevel.Warning)
            .AddFilter("Azure.Messaging.ServiceBus", LogLevel.Warning));
    }, configure: (services, configuration) =>
    {
        services.ConfigureCloudSuiteIntegrations(configuration);
        services.AddScoped<GetProductItemsHandler>();
    });

await app.Run<GetProductItemsHandler>((handler, ct) => handler.Handle());
return 0;