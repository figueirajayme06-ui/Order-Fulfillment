using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OF.Common.Infrastructure.Features;
using System.Text.Json.Serialization;

namespace OF.WebApp.Controllers;

[ApiController]
[Route("api/app-config")]
public sealed class AppConfigurationController : ControllerBase
{
    private readonly FrontendExperienceOptions _options;

    public AppConfigurationController(IOptions<FrontendExperienceOptions> options)
    {
        _options = options.Value;
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public ActionResult<AppConfigurationResponse> Get()
    {
        return Ok(new AppConfigurationResponse
        {
            EnvironmentLabel = _options.EnvironmentLabel.Trim(),
            ShowPreviewBanner = _options.ShowPreviewBanner,
            LegacyFrontendUrl = GetHttpUrl(_options.LegacyFrontendUrl),
            DeploymentInfo = GetDeploymentInfo(),
        });
    }

    private DeploymentInfoResponse? GetDeploymentInfo()
    {
        if (!_options.ShowDeploymentInfo)
        {
            return null;
        }

        return new DeploymentInfoResponse
        {
            Version = NullIfWhiteSpace(_options.AppVersion),
            CommitSha = GetCommitSha(_options.CommitSha),
            SourceRef = NullIfWhiteSpace(_options.SourceRef),
            BuildTimestamp = _options.BuildTimestamp,
        };
    }

    private static string? GetCommitSha(string? value)
    {
        var trimmed = NullIfWhiteSpace(value);
        return trimmed is not null
            && trimmed.Length is >= 7 and <= 64
            && trimmed.All(Uri.IsHexDigit)
                ? trimmed.ToLowerInvariant()
                : null;
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? GetHttpUrl(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var parsed)
            || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        return parsed.AbsoluteUri;
    }
}

public sealed class AppConfigurationResponse
{
    public string EnvironmentLabel { get; init; } = string.Empty;

    public bool ShowPreviewBanner { get; init; }

    public string? LegacyFrontendUrl { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DeploymentInfoResponse? DeploymentInfo { get; init; }
}

public sealed class DeploymentInfoResponse
{
    public string? Version { get; init; }

    public string? CommitSha { get; init; }

    public string? SourceRef { get; init; }

    public DateTimeOffset? BuildTimestamp { get; init; }
}
