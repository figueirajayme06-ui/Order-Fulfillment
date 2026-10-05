using AutoFixture;
using AutoFixture.AutoMoq;

namespace OF.Tests.AutoFixture.Customizations;

public class MoqCustomization : AutoMoqCustomization
{
    public MoqCustomization()
    {
        ConfigureMembers = false;
        GenerateDelegates = true;
    }
}
