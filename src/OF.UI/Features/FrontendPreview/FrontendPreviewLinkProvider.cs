using Microsoft.Extensions.Options;
using OF.Common;
using OF.Common.Infrastructure.Features;
using OF.Data.Database;

namespace OF.UI.Features.FrontendPreview;

public sealed class FrontendPreviewLinkProvider
{
    private readonly FrontendExperienceOptions _options;

    public FrontendPreviewLinkProvider(IOptions<FrontendExperienceOptions> options)
    {
        _options = options.Value;
    }

    public string? GetUrl(User? user)
    {
        if (!_options.Enabled || user is null || !TryGetHttpUrl(_options.NewFrontendUrl, out var url))
        {
            return null;
        }

        if (_options.ShowForAll || user.ActiveRoles.Contains(Constants.Roles.NewFrontendPreview, StringComparer.OrdinalIgnoreCase))
        {
            return url;
        }

        return null;
    }

    private static bool TryGetHttpUrl(string? value, out string url)
    {
        url = string.Empty;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var parsed)
            || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        url = parsed.AbsoluteUri;
        return true;
    }
}
