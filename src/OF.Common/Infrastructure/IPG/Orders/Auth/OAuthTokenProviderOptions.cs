namespace OF.Common.Infrastructure.IPG.Orders.Auth;

public class OAuthTokenProviderOptions
{
    public required string AuthenticationUrl { get; set; }
    public required IReadOnlyList<OAuthClientCredentials> Clients { get; set; }
}

public class OAuthClientCredentials
{
    public required string ClientId { get; set; }
    public required string ClientSecret { get; set; }
    public required string Audience { get; set; }
}
