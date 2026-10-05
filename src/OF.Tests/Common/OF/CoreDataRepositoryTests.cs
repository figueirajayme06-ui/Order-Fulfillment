using OF.Common.Infrastructure.OF;
using OF.Tests.Data.Test;

namespace OF.Tests.Common.OF
{
    [Collection("DatabaseCollection")]
    public class CoreDataRepositoryTests : CommonDBTest
    {
        public CoreDataRepositoryTests(DatabaseFixture databaseFixture) : base(databaseFixture)
        {
        }

        [Fact]
        public async Task GetNonServiceLines_Returns_Only_RequiresFulfilment_True_Lines()
        {
            // Arrange
            var dbContextFactory = await ArrangeAndAct(TestData.AgreementLines.UnFulfilledHeaderAndQuoteLineWithExcludedLines);
            var dbContext = await dbContextFactory.CreateDbContextAsync();
            var repository = new CoreDataRepository(dbContext);

            // Act
            var result = repository.GetNonServiceLines(1).ToList();

            // Assert
            Assert.Single(result);
            Assert.Equal(1, result[0].Id);
            Assert.True(result[0].RequiresFulfilment);
        }
    }
}
