namespace OF.Common.Infrastructure.IPG.Pricing.Models
{
    public class CalculationRequest
    {
        public string Id { get; set; }
        public PricingQuote Quote { get; set; }
        public CalculationOptions Options { get; set; }
    }
}
