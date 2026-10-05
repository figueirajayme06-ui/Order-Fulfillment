using Newtonsoft.Json;

namespace OF.Common.Infrastructure.IPG.Orders.Models.Quotes
{

    public class OpportunityQuoteLine : QuoteLine
    {
        public required string Id { get; set; }

        public DateTime? LastModified { get; set; }

        [JsonProperty("M3_Line_Type__c")]
        public string? LineTypeId { get; set; }

        [JsonProperty("SBQQ__ProductCode__c")]
        public string? ProductCode { get; set; }

        [JsonProperty("SBQQ__Quantity__c")]
        public required string Quantity { get; set; }

        [JsonProperty("SBQQ__Group__r")]
        public CPQOrderLineGroup? QuoteGroup { get; set; }

        [JsonProperty("On_Hire_Date__c")]
        public DateTime? OnHireDate { get; set; }

        [JsonProperty("Off_Hire_Date__c")]
        public DateTime? OffHireDate { get; set; }

        [JsonProperty("NUMBER_OF_SHIFTS__C")]
        public string? NumberOfShifts { get; set; }

        [JsonProperty("Proposal_Section__c")]
        public string? ProposalSection { get; set; }
    }

    public class QuoteLine : SfObjectBase
    {
        [JsonProperty("SBQQ__Number__c")]
        public double? LineId { get; set; }

        [JsonProperty("SBQQ__Group__r")]
        public QuoteGroup? Group { get; set; }

        [JsonProperty("SBQQ__Quote__c")]
        public string? QuoteId { get; set; }

        [JsonProperty("Rich_Displayed_In_Line_Items_With_Attrib__c")]
        public string? DescriptionWithAttributes { get; set; }

        [JsonProperty("PS_AG_Selected_Attributes_As_Text_EN__c")]
        public string? SelectedAttributesAsText { get; set; }

        [JsonProperty("Selected_Attributes_As_Text__c")]
        public string? LocalizedAttributesAsText { get; set; }

        [JsonProperty("Generic_Code__c")]
        public string? GenericItemNumber { get; set; }

        [JsonProperty("SBQQ__Quote__r")]
        public OrderItemQuote? Quote { get; set; }

        [JsonProperty("SBQQ__Description__c")]
        public string? ItemDescription { get; set; }
    }

    public class QuoteGroup : SfObjectBase
    {
        public string? Name { get; set; }
    }

    public class OrderItemQuote : SfObjectBase
    {
        public required string? Name { get; set; }
    }
}
