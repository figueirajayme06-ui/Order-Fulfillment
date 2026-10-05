using FluentAssertions;
using Microsoft.Extensions.Options;
using OF.Common.Infrastructure.Features;
using OF.Data.Database;
using OF.UI.Features.FrontendPreview;

namespace OF.Tests.UI.Features.FrontendPreview;

public class FrontendPreviewLinkProviderTests
{
    [Fact]
    public void GetUrl_ReturnsUrl_ForRoleMatchIgnoringCase()
    {
        var subject = CreateSubject(enabled: true, url: "https://preview.example.test", showForAll: false);
        var user = new User { Roles = "changeorder, newfrontendpreview" };

        subject.GetUrl(user).Should().Be("https://preview.example.test/");
    }

    [Fact]
    public void GetUrl_ReturnsUrl_ForAnyUserWhenShowForAll()
    {
        var subject = CreateSubject(enabled: true, url: "https://preview.example.test/new", showForAll: true);

        subject.GetUrl(new User()).Should().Be("https://preview.example.test/new");
    }

    [Theory]
    [InlineData(false, "https://preview.example.test")]
    [InlineData(true, "")]
    [InlineData(true, "not-a-url")]
    [InlineData(true, "file:///preview")]
    public void GetUrl_ReturnsNull_WhenConfigurationIsUnavailable(bool enabled, string url)
    {
        var subject = CreateSubject(enabled, url, showForAll: true);

        subject.GetUrl(new User()).Should().BeNull();
    }

    [Fact]
    public void GetUrl_ReturnsNull_ForUserWithoutPreviewRole()
    {
        var subject = CreateSubject(enabled: true, url: "https://preview.example.test", showForAll: false);

        subject.GetUrl(new User { Roles = "ChangeOrder" }).Should().BeNull();
    }

    private static FrontendPreviewLinkProvider CreateSubject(bool enabled, string url, bool showForAll)
    {
        return new FrontendPreviewLinkProvider(Options.Create(new FrontendExperienceOptions
        {
            Enabled = enabled,
            NewFrontendUrl = url,
            ShowForAll = showForAll,
        }));
    }
}
