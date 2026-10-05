using AutoFixture;
using AutoFixture.Kernel;
using OF.Data;
using OF.Data.Database;

namespace OF.Tests.AutoFixture.Customizations;

public class ApplicationDbContextCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customize<InMemoryDBContext>(x => x
            .FromFactory((string seed) => new InMemoryDBContext(seed))
            .OmitAutoProperties());
        fixture.Customizations.Add(new TypeRelay(typeof(ApplicationDbContext), typeof(InMemoryDBContext)));
    }
}
