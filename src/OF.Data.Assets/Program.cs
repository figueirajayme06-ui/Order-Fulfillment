using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.ApplicationInsights;
using OF.Common;
using OF.Common.Infrastructure.CloudSuite.DependencyInjection;
using OF.Data.Assets.UseCases;

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
    },
    configure: (services, configuration) =>
    {
        services.ConfigureCloudSuiteIntegrations(configuration);
        services.AddScoped<GetAssetsHandler>();
    });

await app.Run<GetAssetsHandler>((handler, ct) => handler.Handle());
return 0;
