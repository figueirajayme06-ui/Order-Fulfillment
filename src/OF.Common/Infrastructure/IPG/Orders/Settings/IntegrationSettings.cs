namespace OF.Common.Infrastructure.IPG.Orders.Settings
{
    public class IntegrationSettings
    {
        public required IntegrationAuthenticationSettings AuthenticationSettings { get; set; }
        public required OrderManagementClientSettings OrderManagementClientSettings { get; set; }
        public required OrderIntegrationClientSettings OrderIntegrationClientSettings { get; set; }
        public required PricingClientSettings PricingClientSettings { get; set; }
    }
}
