using System.Text.Json.Serialization;

namespace OF.Common.Infrastructure.IPG.Orders.Models.ChangeOrder
{
    public class SalesforceChangeOrderContact
    {
        [JsonPropertyName("Email")]
        public string? Email { get; set; }

        [JsonPropertyName("Salutation")]
        public string? Salutation { get; set; }

        [JsonPropertyName("Name")]
        public string? Name { get; set; }

        [JsonPropertyName("Phone")]
        public string? Phone { get; set; }

        [JsonPropertyName("MobilePhone")]
        public string? MobilePhone { get; set; }

        [JsonPropertyName("M3_Customer_Number__c")]
        public string? M3CustomerNumber { get; set; }

        [JsonPropertyName("M3_Contact_Code__c")]
        public string? M3ContactCode { get; set; }
    }
}
