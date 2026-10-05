namespace OF.Common.Infrastructure.IPG.Orders.Settings
{
    public abstract class IntegrationClientSettings
    {
        public required string BaseAddress { get; set; }
        public required string ClientId { get; set; }
        public required string ClientSecret { get; set; }
        public required string Audience { get; set; }
        public string IPGSubscriptionHeaderKey { get; set; } = "Ocp-Apim-Subscription-Key";
        public required string IPGSubscriptionHeaderValue { get; set; }
        public int ApiTimeout { get; set; } = 60;
        public TimeSpan Timeout => TimeSpan.FromSeconds(ApiTimeout);
    }

    public class OrderManagementClientSettings : IntegrationClientSettings
    {
    }

    public class OrderIntegrationClientSettings : IntegrationClientSettings
    {
    }

    public class PricingClientSettings : IntegrationClientSettings
    {
    }
}
