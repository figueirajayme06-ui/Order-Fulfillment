using AutoFixture;
using OF.Data.Database;

namespace OF.Tests.AutoFixture.Customizations;

public class OrderCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList().ForEach(b => fixture.Behaviors.Remove(b));
        fixture.Behaviors.Add(new OmitOnRecursionBehavior());

        fixture.Customize<Header>(x => x.Without(xx => xx.Lines));
        fixture.Customize<Header>(x => x.Without(xx => xx.ChangeOrderHeaders));
        fixture.Customize<Header>(x => x.Without(xx => xx.ChangeOrders));
        fixture.Customize<Line>(x => x.Without(xx => xx.Header));
        fixture.Customize<Line>(x => x.Without(xx => xx.ChangeOrderLines));
    }
}
