using Microsoft.Extensions.Configuration;

namespace OF.Common.Infrastructure.Features;


public class FeatureProvider
{
    public static class Features
    {
        public const string ChangeOrders = "ChangeOrders";
    }

    private Dictionary<string, bool> features = new Dictionary<string, bool>();
    private readonly bool newFrontendPreviewEnabled;

    public FeatureProvider(IConfiguration configuration)
    {
        var section = configuration.GetSection("Features");

        if (section != null)
        {
            features = section.Get<Dictionary<string, bool>>() ?? new Dictionary<string, bool>();
        }

        newFrontendPreviewEnabled = configuration.GetValue<bool>($"{FrontendExperienceOptions.SectionName}:Enabled");
    }

    public bool ChangeOrdersEnabled => features.ContainsKey(Features.ChangeOrders) && features[Features.ChangeOrders];

    public bool NewFrontendPreviewEnabled => newFrontendPreviewEnabled;

    public bool RolesEnabled => ChangeOrdersEnabled || NewFrontendPreviewEnabled;

    public bool IsEnabled(string feature) => features.ContainsKey(feature) && features[feature];
}
