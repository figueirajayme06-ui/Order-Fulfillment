namespace OF.Common.Infrastructure.IPG.Pricing.Models
{
    public class CalculationOptions
    {
        public string PricingRuleBook { get; set; }
        public bool OutputSteps { get; set; }
        public bool ContinueOnFailedLine { get; set; }
    }
}
