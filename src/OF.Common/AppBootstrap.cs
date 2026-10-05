using Ardalis.GuardClauses;
using Azure.Identity;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Metrics;
using Microsoft.ApplicationInsights.Metrics.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.ExceptionServices;

namespace OF.Common
{
    public static class AppBootstrap
    {
        public static AppBootstrap<T> Init<T>(
            LogLevel logLevel = LogLevel.Warning,
            Action<ServiceCollection, IConfiguration>? bootstrap = null,
            Action<ServiceCollection, IConfiguration>? configure = null) where T : notnull
        {
            var bootstrapConfig = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(bootstrapConfig);
            services.AddLogging((builder) =>
            {
                builder.SetMinimumLevel(logLevel).AddConsole();
            });

            bootstrap?.Invoke(services, bootstrapConfig);

            var provider = services.BuildServiceProvider();

            var logger = provider.GetRequiredService<ILogger<AppBootstrap<T>>>();
            logger.LogInformation("Starting applications.");

            var keyVaultUri = bootstrapConfig["KeyVaultUri"];

            if (string.IsNullOrWhiteSpace(keyVaultUri))
            {
                throw new ArgumentException("KeyVaultUri must have a value");
            }

            logger.LogInformation($"Aquire secrets from {keyVaultUri}.");

            var builder = new ConfigurationBuilder()
                .AddAzureKeyVault(new Uri(keyVaultUri), new DefaultAzureCredential())
                .AddEnvironmentVariables();
            var configuration = builder.Build();

            logger.LogInformation($"secrets from {keyVaultUri} acquired.");

            services.Replace(ServiceDescriptor.Singleton<IConfiguration>(configuration));

            var logLevelConfig = configuration["Data-LogLevel"];

            switch (logLevelConfig)
            {
                case nameof(LogLevel.Trace):
                    logLevel = LogLevel.Trace;
                    break;
                case nameof(LogLevel.Debug):
                    logLevel = LogLevel.Debug;
                    break;
                case nameof(LogLevel.Information):
                    logLevel = LogLevel.Information;
                    break;
                case nameof(LogLevel.Warning):
                    logLevel = LogLevel.Warning;
                    break;
                case nameof(LogLevel.Error):
                    logLevel = LogLevel.Error;
                    break;
            }

            logger.LogInformation($"Configuring logging with {logLevel}.");

            services.AddLogging((builder) =>
            {
                builder.SetMinimumLevel(logLevel).AddConsole();
            });

            logger.LogInformation($"{logLevel} logging configured.");

            logger.LogInformation($"Registering dependencies.");

            configure?.Invoke(services, configuration);

            services.RegisterCommonDependencies(configuration);

            logger.LogInformation($"Configuration complete.");

            var serviceProvider = services.BuildServiceProvider();
            var defaultLogger = serviceProvider.GetRequiredService<ILogger<T>>();
            services.AddSingleton<ILogger>(defaultLogger);

            return new(services.BuildServiceProvider(), provider, serviceProvider);
        }
    }

    public class AppBootstrap<T> : IAsyncDisposable where T : notnull
    {
        internal AppBootstrap(ServiceProvider serviceProvider, params ServiceProvider[] disposables)
        {
            ServiceProvider = Guard.Against.Null(serviceProvider);
            Disposables = Guard.Against.Null(disposables);
            ActivitySource = new(typeof(T).FullName);
            ActivityListener = new()
            {
                ShouldListenTo = source => source == ActivitySource,
                Sample = (ref ActivityCreationOptions<ActivityContext> opts) => ActivitySamplingResult.AllData
            };
            ActivitySource.AddActivityListener(ActivityListener);
        }

        private ActivityListener ActivityListener { get; }
        private ActivitySource ActivitySource { get; }
        private ServiceProvider ServiceProvider { get; }
        private ServiceProvider[] Disposables { get; }

        public async Task Run<THandler>(Func<THandler, CancellationToken, Task> runAsync, CancellationToken cancellationToken = default) where THandler : notnull
        {
            await using var scope = ServiceProvider.CreateAsyncScope();
            using var activity = ActivitySource.CreateActivity(typeof(THandler).FullName, ActivityKind.Producer);
            var telemetry = scope.ServiceProvider.GetRequiredService<TelemetryClient>();
            var success = false;
            var operation = telemetry.StartOperation<DependencyTelemetry>(activity);
            try
            {
                var handler = scope.ServiceProvider.GetRequiredService<THandler>();
                await runAsync(handler, cancellationToken);
                success = true;
            }
            catch (Exception e)
            {
                var exceptionTelemetry = new ExceptionTelemetry();
                exceptionTelemetry.Context.Operation.Id = activity!.RootId;
                exceptionTelemetry.Context.Operation.ParentId = activity.SpanId.ToString();
                exceptionTelemetry.Exception = e;
                telemetry.TrackException(exceptionTelemetry);
                ExceptionDispatchInfo.Capture(e).Throw();
            }
            finally
            {
                operation.Telemetry.Success = success;
                telemetry.StopOperation(operation);
                operation.Dispose();
            }
        }

        private async Task FlushTelemetries(IServiceProvider serviceProvider)
        {
            var telemetry = ServiceProvider.GetService<TelemetryClient>();
            await telemetry.FlushAsync(CancellationToken.None);
            telemetry.GetMetricManager(MetricAggregationScope.TelemetryConfiguration).Flush();
            await Task.Delay(TimeSpan.FromSeconds(3), CancellationToken.None);  //  Allow some time to send telemetries
        }

        public async ValueTask DisposeAsync()
        {
            await FlushTelemetries(ServiceProvider);
            await Task.WhenAll(Disposables.Append(ServiceProvider)
                .Reverse()
                .Select(x => x.DisposeAsync().AsTask()));
            ActivityListener.Dispose();
            ActivitySource.Dispose();
        }
    }
}
