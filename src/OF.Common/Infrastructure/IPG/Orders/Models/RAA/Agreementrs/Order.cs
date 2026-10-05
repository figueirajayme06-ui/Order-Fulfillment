using Newtonsoft.Json;
using OF.Common.Infrastructure.IPG.Orders.Models.Quotes;

namespace OF.Common.Infrastructure.IPG.Orders.Models.RAA.Agreementrs
{
    public class OrderQuote : Quote
    {
        [JsonProperty("SBQQ__Opportunity2__c")]
        public string? OpportunityId { get; set; }

        [JsonProperty("SBQQ__Opportunity2__r")]
        public OrderQuoteOpportunity? Opportunity { get; set; }
    }

    public class OrderQuoteOpportunity : SfObjectBase
    {
        public string? Probability { get; set; }

        [JsonProperty("PS_AG_Effective_Probability__c")]
        public double? EffectiveProbability { get; set; }
        public string? Name { get; set; }
        [JsonProperty("StageName")]
        public string? OpportunityStageName { get; set; }

    }

    public class Order : SfObjectBase
    {
        public required string Id { get; set; }

        [JsonProperty("SBQQ__Quote__c")]
        public required string QuoteId { get; set; }

        [JsonProperty("SBQQ__Quote__r")]
        public required OrderQuote Quote { get; set; }

        [JsonProperty("On_Hire_Date__c")]
        public DateTime? OnHireDate { get; set; }

        [JsonProperty("Off_Hire_Date__c")]
        public DateTime? OffHireDate { get; set; }

        [JsonProperty("PS_AG_Shipping_Address_Details__c")]
        public required string ShippingAddress { get; set; }
    }
}
