using Newtonsoft.Json;

namespace OF.Common.Infrastructure.IPG.Orders.Models.Quotes
{
    public class SfAuthResult
    {
        [JsonProperty("access_token")]
        public required string AccessToken { get; set; }
    }
}
