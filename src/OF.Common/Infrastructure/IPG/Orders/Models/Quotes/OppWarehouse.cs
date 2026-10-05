using Newtonsoft.Json;

namespace OF.Common.Infrastructure.IPG.Orders.Models.Quotes
{
    public class OppWarehouse : SfObjectBase
    {
        [JsonProperty("CAP_AG_M3_Warehouse_ID__c")]
        public string? M3Id { get; set; }

        [JsonProperty("CAP_AG_M3_Facility_ID__c")]
        public string? M3FacilityId { get; set; }

        [JsonProperty("CAP_AG_M3_Division_ID__c")]
        public string? M3DivisionId { get; set; }
    }
}
