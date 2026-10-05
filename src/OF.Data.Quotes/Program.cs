using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.ApplicationInsights;
using Microsoft.Extensions.Logging;
using OF.Common;
using OF.Data.Quotes.UseCases;

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
        services.AddScoped<ImportPrimaryQuotes>();
    });

await app.Run<ImportPrimaryQuotes>((handler, ct) => handler.Handle());
return 0;