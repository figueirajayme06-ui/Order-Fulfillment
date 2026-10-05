namespace OF.Tests.AutoFixture;

public class InlineAutoDataAttribute : global::AutoFixture.Xunit2.InlineAutoDataAttribute
{
    public InlineAutoDataAttribute(params object[] parameters)
        : base(new AutoDataAttribute(), parameters)
    {
    }

    public InlineAutoDataAttribute(Type[] customizations, params object[] parameters)
        : base(new AutoDataAttribute(customizations), parameters)
    {
    }
}
