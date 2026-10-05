namespace OF.Common.Infrastructure.IPG.Pricing.Models
{
    public class QuoteDetails
    {
        public string Id { get; set; }
        public string Division { get; set; }
        public string Warehouse { get; set; }
        public string JobAICCode { get; set; }
        public string OnHire { get; set; }
        public string OffHire { get; set; }
        public string ContractNumber { get; set; }
        public string CurrencyISOCode { get; set; }
        public double? CurrencyExchangeRate { get; set; }
        public double? DaysInWeek { get; set; }
        public double? DaysInMonth { get; set; }
        public double? WeeksInMonth { get; set; }
        public string RateType { get; set; }
        public string PricingTier { get; set; }
    }
}
