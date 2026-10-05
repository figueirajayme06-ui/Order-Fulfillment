using System.Text.Json.Serialization;

namespace OF.Common.Infrastructure.IPG.Orders.Models.ChangeOrder
{
    public class SalesforceChangeOrderQuote
    {
        [JsonPropertyName("Minimum_Rental__c")]
        public double? MinimumRental { get; set; }

        [JsonPropertyName("Price_Market_Premium__c")]
        public string? PriceMarketPremium { get; set; }

        [JsonPropertyName("Price_Adjustment__c")]
        public decimal? PriceAdjustment { get; set; }

        [JsonPropertyName("Total_Net_Price_ST__c")]
        public decimal? TotalNetPrice { get; set; }

        [JsonPropertyName("Total_Equipment_Price__c")]
        public decimal? TotalEquipmentPrice { get; set; }

        [JsonPropertyName("SBQQ__TargetCustomerAmount__c")]
        public decimal? TargetCustomerAmount { get; set; }

        [JsonPropertyName("D_W_M_Rates__c")]
        public string? DWMRates { get; set; }
    }
}
