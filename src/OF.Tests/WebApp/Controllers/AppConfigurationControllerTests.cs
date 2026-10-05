using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OF.Common.Infrastructure.Features;
using OF.WebApp.Controllers;

namespace OF.Tests.WebApp.Controllers;

public class AppConfigurationControllerTests
{
    [Fact]
    public void Get_ReturnsRuntimeConfiguration()
    {
        var subject = CreateSubject(new FrontendExperienceOptions
        {
            EnvironmentLabel = " OF Dev ",
            ShowPreviewBanner = true,
            LegacyFrontendUrl = "https://asofdev.azurewebsites.net",
            ShowDeploymentInfo = true,
            AppVersion = " 1.12.0 ",
            CommitSha = " ABCDEF1234567890 ",
            SourceRef = " refs/heads/main ",
            BuildTimestamp = new DateTimeOffset(2026, 9, 3, 10, 30, 0, TimeSpan.Zero),
        });

        var response = GetResponse(subject.Get());

        response.Should().BeEquivalentTo(new AppConfigurationResponse
        {
            EnvironmentLabel = "OF Dev",
            ShowPreviewBanner = true,
            LegacyFrontendUrl = "https://asofdev.azurewebsites.net/",
            DeploymentInfo = new DeploymentInfoResponse
            {
                Version = "1.12.0",
                CommitSha = "abcdef1234567890",
                SourceRef = "refs/heads/main",
                BuildTimestamp = new DateTimeOffset(2026, 9, 3, 10, 30, 0, TimeSpan.Zero),
            },
        });
    }

    [Fact]
    public void Get_OmitsDeploymentInformationWhenDisabled()
    {
        var response = GetResponse(CreateSubject(new FrontendExperienceOptions
        {
            ShowDeploymentInfo = false,
            AppVersion = "1.12.0",
            CommitSha = "abcdef1234567890",
        }).Get());

        response.DeploymentInfo.Should().BeNull();
    }

    [Fact]
    public void Get_DoesNotExposeMalformedCommitLinkTarget()
    {
        var response = GetResponse(CreateSubject(new FrontendExperienceOptions
        {
            ShowDeploymentInfo = true,
            CommitSha = "not-a-commit",
        }).Get());

        response.DeploymentInfo.Should().NotBeNull();
        response.DeploymentInfo!.CommitSha.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-url")]
    [InlineData("file:///legacy")]
    public void Get_OmitsInvalidLegacyUrl(string value)
    {
        var response = GetResponse(CreateSubject(new FrontendExperienceOptions
        {
            LegacyFrontendUrl = value,
        }).Get());

        response.LegacyFrontendUrl.Should().BeNull();
    }

    private static AppConfigurationController CreateSubject(FrontendExperienceOptions options) =>
        new(Options.Create(options));

    private static AppConfigurationResponse GetResponse(ActionResult<AppConfigurationResponse> result) =>
        result.Result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<AppConfigurationResponse>().Subject;
}
