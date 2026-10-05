using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using OF.Common.Infrastructure.IPG.Orders.Auth.Models;
using System.Collections.Concurrent;

namespace OF.Common.Infrastructure.IPG.Orders.Auth
{
    public class OAuthTokenProvider
    {
        private const string HttpClientName = "IPGAuth";
        private static readonly TimeSpan TokenExpiryBuffer = TimeSpan.FromSeconds(30);

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<OAuthTokenProvider> _logger;
        private readonly string _authenticationUrl;
        private readonly ConcurrentDictionary<string, IReadOnlyList<KeyValuePair<string, string>>> _credentialsByAudience = new();
        private readonly ConcurrentDictionary<string, OAuthToken> _tokenCache = new();

        public OAuthTokenProvider(IHttpClientFactory httpClientFactory, IOptions<OAuthTokenProviderOptions> options, ILogger<OAuthTokenProvider> logger)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger;
            var settings = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _authenticationUrl = new Uri(settings.AuthenticationUrl).PathAndQuery;

            foreach (var client in settings.Clients)
            {
                _credentialsByAudience.TryAdd(
                    client.Audience,
                    new[]
                    {
                        new KeyValuePair<string, string>("grant_type", "client_credentials"),
                        new KeyValuePair<string, string>("client_id", client.ClientId),
                        new KeyValuePair<string, string>("client_secret", client.ClientSecret),
                        new KeyValuePair<string, string>("resource", client.Audience)
                    });
            }
        }

        public async Task<string?> GetBearerTokenAsync(string audience, CancellationToken cancellationToken)
        {
            if (!_credentialsByAudience.TryGetValue(audience, out var credentials))
            {
                _logger.LogError("No credentials registered for audience '{Audience}'", audience);
                return null;
            }

            if (_tokenCache.TryGetValue(audience, out var cached) &&
                cached.ExpiresAt - DateTimeOffset.UtcNow > TokenExpiryBuffer)
            {
                _logger.LogDebug("Using cached token for audience '{Audience}'", audience);
                return cached.AccessToken;
            }

            _logger.LogDebug("Getting token from Azure AD for audience '{Audience}'", audience);
            using var client = _httpClientFactory.CreateClient(HttpClientName);
            using var content = new FormUrlEncodedContent(credentials);
            var response = await client.PostAsync(_authenticationUrl, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Unable to get bearer token for audience '{Audience}'. HTTP {StatusCode}, Response: {Body}",
                    audience, (int)response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));
                return null;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var token = JsonConvert.DeserializeObject<OAuthToken>(body)!;
            _tokenCache[audience] = token;
            _logger.LogDebug("Token acquired for audience '{Audience}'", audience);
            return token.AccessToken;
        }
    }
}
