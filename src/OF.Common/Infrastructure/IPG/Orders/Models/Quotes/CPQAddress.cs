using Newtonsoft.Json;

namespace OF.Common.Infrastructure.IPG.Orders.Models.Quotes
{
    public class CPQAddress : SfObjectBase
    {
        [JsonProperty("CAP_AG_Street__c")]
        public string? Street { get; set; }
    }
}
