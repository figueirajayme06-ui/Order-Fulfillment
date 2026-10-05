using AutoFixture;
using OF.Tests.AutoFixture.Customizations;

namespace OF.Tests.AutoFixture;

public class AutoDataAttribute : global::AutoFixture.Xunit2.AutoDataAttribute
{
    public AutoDataAttribute()
        : base(() => new Fixture().Customize(new MoqCustomization()))
    {
    }

    public AutoDataAttribute(params Type[] customizations)
        : base(() => Customize(customizations))
    {
    }

    private static IFixture Customize(Type[] customizationTypes)
    {
        var fixture = new Fixture();

        for (int i = 0; i < customizationTypes.Length; i++)
        {
            fixture.Customize((ICustomization)Activator.CreateInstance(customizationTypes[i])!);
        }

        return fixture;
    }
}
