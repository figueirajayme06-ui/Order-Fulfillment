using Azure.Core;
using Azure.Identity;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OF.WebApp.Features.Administration;

public interface IAdminDirectoryService
{
    Task<IReadOnlyList<DirectoryPerson>> SearchAsync(string search, CancellationToken cancellationToken);
}

public sealed record DirectoryPerson(string LoginName, string FullName, string? Mail);

public sealed class MicrosoftGraphAdminDirectoryService : IAdminDirectoryService
{
    private const string GraphClientName = "MicrosoftGraph";
    private static readonly string[] GraphScopes = ["https://graph.microsoft.com/.default"];

    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private ClientSecretCredential? _credential;

    public MicrosoftGraphAdminDirectoryService(IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IReadOnlyList<DirectoryPerson>> SearchAsync(
        string search,
        CancellationToken cancellationToken)
    {
        var normalizedSearch = search.Trim();
        if (normalizedSearch.Length < 2)
        {
            return [];
        }

        var credential = _credential ??= CreateCredential();
        var token = await credential.GetTokenAsync(
            new TokenRequestContext(GraphScopes),
            cancellationToken);

        var client = _httpClientFactory.CreateClient(GraphClientName);
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildSearchUri(normalizedSearch));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        request.Headers.Add("ConsistencyLevel", "eventual");

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        var graphResponse = await JsonSerializer.DeserializeAsync<GraphUserCollectionResponse>(
            body,
            cancellationToken: cancellationToken);

        return graphResponse?.Value
            .Where(user => !string.IsNullOrWhiteSpace(user.UserPrincipalName))
            .Select(user => new DirectoryPerson(
                user.UserPrincipalName!.Trim().ToLowerInvariant(),
                string.IsNullOrWhiteSpace(user.DisplayName)
                    ? user.UserPrincipalName.Trim()
                    : user.DisplayName.Trim(),
                string.IsNullOrWhiteSpace(user.Mail) ? null : user.Mail.Trim()))
            .DistinctBy(person => person.LoginName, StringComparer.OrdinalIgnoreCase)
            .OrderBy(person => person.FullName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(person => person.LoginName, StringComparer.OrdinalIgnoreCase)
            .ToList()
            ?? [];
    }

    internal static string BuildSearchUri(string search)
    {
        var escapedSearch = search[..Math.Min(search.Length, 100)]
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
        var expression = $"\"displayName:{escapedSearch}\" OR \"userPrincipalName:{escapedSearch}\"";

        return "users"
            + $"?$search={Uri.EscapeDataString(expression)}"
            + "&$orderby=displayName"
            + "&$select=displayName,mail,userPrincipalName"
            + "&$top=25";
    }

    private ClientSecretCredential CreateCredential()
    {
        var tenantId = _configuration["GraphClientTenantId"];
        var clientId = _configuration["GraphClientId"];
        var clientSecret = _configuration["GraphClientSecret"];

        if (string.IsNullOrWhiteSpace(tenantId)
            || string.IsNullOrWhiteSpace(clientId)
            || string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException("Microsoft Graph directory search is not configured.");
        }

        return new ClientSecretCredential(tenantId, clientId, clientSecret);
    }

    private sealed class GraphUserCollectionResponse
    {
        [JsonPropertyName("value")]
        public List<GraphUser> Value { get; init; } = [];
    }

    private sealed class GraphUser
    {
        [JsonPropertyName("displayName")]
        public string? DisplayName { get; init; }

        [JsonPropertyName("mail")]
        public string? Mail { get; init; }

        [JsonPropertyName("userPrincipalName")]
        public string? UserPrincipalName { get; init; }
    }
}
