namespace OF.Common.Infrastructure.IPG.Pricing.Models
{
    public class QuotePrice
    {
        public QuoteDetails Details { get; set; }
        public double? ListPrice { get; set; }
        public double? ListFloorPrice { get; set; }
        public double? ListMarketPrice { get; set; }
        public double? ListPremiumPrice { get; set; }
    }
}
