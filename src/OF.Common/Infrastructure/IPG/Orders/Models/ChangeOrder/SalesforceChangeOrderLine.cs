using OF.Common.Infrastructure.IPG.Orders.Models.Quotes;
using System.Text.Json.Serialization;

namespace OF.Common.Infrastructure.IPG.Orders.Models.ChangeOrder
{
    public class SalesforceChangeOrderLine : SfObjectBase
    {
        [JsonPropertyName("On_Hire_Date__c")]
        public string? OnHireDate { get; set; }

        [JsonPropertyName("Off_Hire_Date__c")]
        public string? OffHireDate { get; set; }

        [JsonPropertyName("UnitPrice")]
        public decimal? UnitPrice { get; set; }

        [JsonPropertyName("TotalPrice")]
        public decimal? TotalPrice { get; set; }

        [JsonPropertyName("Description__c")]
        public string? Description { get; set; }

        [JsonPropertyName("M3_Line_Number_Id__c")]
        public string? M3LineNumberId { get; set; }

        [JsonPropertyName("Group_Number__c")]
        public string? GroupNumber { get; set; }

        [JsonPropertyName("SBQQ__QuoteLine__r")]
        public SalesforceChangeOrderQuoteLine? QuoteLine { get; set; }

        [JsonPropertyName("Quantity")]
        public decimal? Quantity { get; set; }

        [JsonPropertyName("SBQQ__OrderedQuantity__c")]
        public decimal? OrderedQuantity { get; set; }

        [JsonPropertyName("SBQQ__QuotedQuantity__c")]
        public decimal? QuotedQuantity { get; set; }
    }
}
