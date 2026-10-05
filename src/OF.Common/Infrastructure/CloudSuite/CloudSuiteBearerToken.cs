using Newtonsoft.Json;

namespace OF.Common.Infrastructure.CloudSuite
{
    public class CloudSuiteBearerToken
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; set; } = ""!;

        [JsonProperty("refresh_token")]
        public string RefreshToken { get; set; } = ""!;
    }
}
