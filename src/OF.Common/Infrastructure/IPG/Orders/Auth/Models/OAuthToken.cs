using Newtonsoft.Json;

namespace OF.Common.Infrastructure.IPG.Orders.Auth.Models
{
    public sealed class OAuthToken
    {
        [JsonProperty("access_token")]
        public string? AccessToken { get; set; }

        [JsonProperty("token_type")]
        public string? TokenType { get; set; }

        [JsonProperty("expires_in")]
        public int ExpiresInSeconds { get; set; }

        [JsonIgnore]
        public DateTimeOffset Created { get; } = DateTimeOffset.UtcNow;

        [JsonIgnore]
        public DateTimeOffset ExpiresAt => Created + TimeSpan.FromSeconds(ExpiresInSeconds);
    }
}
