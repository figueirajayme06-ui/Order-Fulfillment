using Newtonsoft.Json;

namespace OF.Common.Infrastructure.IPG.Orders.Models.Quotes
{
    public class Account : SfObjectBase
    {
        public required string Name { get; set; }

        [JsonProperty("M3_Customer_Number__c")]
        public required string M3CustomerNumber { get; set; }
    }
}
