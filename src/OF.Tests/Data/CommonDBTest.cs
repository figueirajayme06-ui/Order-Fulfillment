using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OF.Data;
using OF.Data.Database;
using System.Runtime.CompilerServices;

namespace OF.Tests.Common
{
    public class CommonDBTest : IDisposable
    {
        protected DbContextOptions<ApplicationDbContext> dbContextOptions;
        protected ApplicationDbContext dbContext;
        protected readonly DatabaseFixture databaseFixture;

        protected CommonDBTest(DatabaseFixture databaseFixture)
        {
            dbContextOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            dbContext = new ApplicationDbContext(dbContextOptions);
            this.databaseFixture = databaseFixture;
        }

        protected Task<IDbContextFactory<ApplicationDbContext>> ArrangeAndAct(Func<ApplicationDbContext, Header>? setupData = null, [CallerMemberName] string testName = "<test name>")
            => ArrangeAndAct(testName, setupData);

        protected Task<IDbContextFactory<ApplicationDbContext>> ArrangeAndActRingFence(Func<ApplicationDbContext, (Ringfence, Ringfence)>? setupData = null, [CallerMemberName] string testName = "<test name>")
            => ArrangeAndActRingFence(testName, setupData);

        [Obsolete("Use the overload without passing nameof(\"<testName>\")")]
        protected async Task<IDbContextFactory<ApplicationDbContext>> ArrangeAndAct(string testName, Func<ApplicationDbContext, Header>? setupData = null)
        {
            var dbContextFactory = databaseFixture.SetupDbContext(testName);
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            setupData?.Invoke(dbContext);

            return dbContextFactory;
        }

        [Obsolete("Use the overload without passing nameof(\"<testName>\")")]
        protected async Task<IDbContextFactory<ApplicationDbContext>> ArrangeAndActRingFence(string testName, Func<ApplicationDbContext, (Ringfence, Ringfence)>? setupData = null)
        {
            var dbContextFactory = databaseFixture.SetupDbContext(testName);
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            setupData?.Invoke(dbContext);

            return dbContextFactory;
        }

        public void Dispose()
        {
            dbContext.Dispose();
        }
    }
}
