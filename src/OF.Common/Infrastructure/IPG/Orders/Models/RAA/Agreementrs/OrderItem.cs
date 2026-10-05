using Newtonsoft.Json;
using OF.Common.Infrastructure.IPG.Orders.Models.Quotes;

namespace OF.Common.Infrastructure.IPG.Orders.Models.RAA.Agreementrs
{
    public class OrderItem : SfObjectBase
    {
        public required string Id { get; set; }

        [JsonProperty("M3_Line_Number_Id__c")]
        public required double? OrderLineIndex { get; set; }

        [JsonProperty("SBQQ__QuoteLine__c")]
        public required string QuoteLineId { get; set; }

        [JsonProperty("SBQQ__QuoteLine__r")]
        public required QuoteLine QuoteLine { get; set; }
    }
}
