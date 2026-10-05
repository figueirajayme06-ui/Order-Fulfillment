using Newtonsoft.Json;

namespace OF.Common.Infrastructure.IPG.Orders.Models.Quotes
{
    public class Opportunity : SfObjectBase
    {
        public required string Id { get; set; }

        [JsonProperty("Name")]
        public required string OpportunityName { get; set; }

        [JsonProperty("StageName")]
        public required string StageName { get; set; }

        [JsonProperty("CAP_AG_Agreement_Number__c")]
        public string? AgreementNumber { get; set; }

        public required Account Account { get; set; }

        [JsonProperty("SBQQ__PrimaryQuote__c")]
        public required string QuoteNumber { get; set; }

        [JsonProperty("SBQQ__PrimaryQuote__r")]
        public required Quote Quote { get; set; }

        [JsonProperty("Shipping_Address__r")]
        public CPQAddress? Address { get; set; }

        public string? Probability { get; set; }

        [JsonProperty("PS_AG_Effective_Probability__c")]
        public double? EffectiveProbability { get; set; }

        [JsonProperty("LastModifiedDate")]
        public DateTime? LastModifiedDate { get; set; }
    }
}
