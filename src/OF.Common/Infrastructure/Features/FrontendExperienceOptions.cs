namespace OF.Common.Infrastructure.Features;

public sealed class FrontendExperienceOptions
{
    public const string SectionName = "FrontendExperience";

    public bool Enabled { get; init; }

    public bool ShowForAll { get; init; }

    public string? NewFrontendUrl { get; init; }

    public string? LegacyFrontendUrl { get; init; }

    public string EnvironmentLabel { get; init; } = string.Empty;

    public bool ShowPreviewBanner { get; init; }

    public bool ShowDeploymentInfo { get; init; }

    public string? AppVersion { get; init; }

    public string? CommitSha { get; init; }

    public string? SourceRef { get; init; }

    public DateTimeOffset? BuildTimestamp { get; init; }
}
