using System.Text.Json.Serialization;

namespace OF.Common.Infrastructure.IPG.Orders.Models.ChangeOrder
{
    public class SalesforceChangeOrderAddress
    {
        [JsonPropertyName("city")]
        public string? City { get; set; }

        [JsonPropertyName("country")]
        public string? Country { get; set; }

        [JsonPropertyName("countryCode")]
        public string? CountryCode { get; set; }

        [JsonPropertyName("postalCode")]
        public string? PostalCode { get; set; }

        [JsonPropertyName("state")]
        public string? State { get; set; }

        [JsonPropertyName("stateCode")]
        public string? StateCode { get; set; }

        [JsonPropertyName("street")]
        public string? Street { get; set; }
    }
}
