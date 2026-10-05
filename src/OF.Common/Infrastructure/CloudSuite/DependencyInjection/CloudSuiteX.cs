using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OF.Common.Infrastructure.CloudSuite.DependencyInjection
{
    public static class CloudSuiteX
    {
        public static IServiceCollection ConfigureCloudSuiteIntegrations(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddHttpClient();

            services.AddSingleton(new CloudSuiteSettings
            {
                AuthBaseUrl = configuration["CloudSuite-TokenBaseUrl"] ?? throw new ArgumentException("CloudSuite-TokenBaseUrl must have a value"),
                CSBaseUrl = configuration["CloudSuite-BaseUrl"] ?? throw new ArgumentException("CloudSuite-BaseUrl must have a value"),
                EnvUrl = configuration["CloudSuite-EnvUrl"] ?? throw new ArgumentException("CloudSuite-EnvUrl must have a value"),
                ClientID = configuration["CloudSuite-ClientID"] ?? throw new ArgumentException("CloudSuite-ClientID must have a value"),
                ClientSecret = configuration["CloudSuite-ClientSecret"] ?? throw new ArgumentException("CloudSuite-ClientSecret must have a value"),
                Username = configuration["CloudSuite-Username"] ?? throw new ArgumentException("CloudSuite-Username must have a value"),
                Password = configuration["CloudSuite-Password"] ?? throw new ArgumentException("CloudSuite-Password must have a value"),
                CSAuthUrl = configuration["CloudSuite-TokenUrl"] ?? throw new ArgumentException("CloudSuite-TokenUrl must have a value")
            });

            services.AddScoped<ICloudSuiteService, CloudSuiteService>();

            return services;
        }
    }
}
