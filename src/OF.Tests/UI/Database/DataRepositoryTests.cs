using Microsoft.Extensions.Time.Testing;
using Microsoft.EntityFrameworkCore;
using OF.Tests.Data.Test;
using OF.Tests.Common;
using OF.UI.Database;
using Moq;
using Microsoft.Extensions.Caching.Memory;
using OF.Data.Database;
using OF.UI.Identity;
using static OF.Common.Enums;

namespace OF.Tests.UI.Database
{
    [Collection("DatabaseCollection")]
    public class DataRepositoryTests : CommonDBTest
    {
        public DataRepositoryTests(DatabaseFixture databaseFixture) : base(databaseFixture)
        {
        }

        [Fact]
        public async Task UpdateLastLoginAtUtc_OnlyMovesTimestampForward()
        {
            var dbContextFactory = await ArrangeAndAct();
            await using var context = await dbContextFactory.CreateDbContextAsync();
            var user = new User
            {
                LoginName = "audit.user@example.com",
                FullName = "Audit User",
                Division = "110",
                DateFormat = "dd/MM/yyyy",
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();
            var repository = new DataRepository(
                context,
                Mock.Of<IMemoryCache>(),
                new FakeTimeProvider());
            var newer = new DateTimeOffset(2026, 9, 2, 9, 30, 0, TimeSpan.Zero);
            var older = newer.AddMinutes(-5);

            repository.UpdateLastLoginAtUtc(user.LoginName, newer);
            repository.UpdateLastLoginAtUtc(user.LoginName, older);
            context.ChangeTracker.Clear();

            var updated = await context.Users.SingleAsync(item => item.LoginName == user.LoginName);
            Assert.Equal(newer.UtcDateTime, updated.LastLoginAtUtc);
        }

        [Fact]
        public async Task GetAgreements_Line_Count_Excludes_Excluded_Lines()
        {
            // Arrange
            var mockMemoryCache = new Mock<IMemoryCache>();
            var fakeTimeProvider = new FakeTimeProvider();
            var dbContextFactory = await ArrangeAndAct(TestData.AgreementLines.View_Headers_Line_Counts_With_Excluded_Lines);
            var dbContext = await dbContextFactory.CreateDbContextAsync();
            var repository = new DataRepository(dbContext, mockMemoryCache.Object, fakeTimeProvider);

            // Act
            var result = repository.GetAgreements(false).ToList();

            // Assert
            Assert.Single(result);
            Assert.Equal("T712843", result[0].AgreementNumber);
            Assert.Equal(3, result[0].LineCount);
            Assert.Equal((int)FulfilmentStatus.PartiallyFulfilled, result[0].FulfilmentStatus);
        }

        [Fact]
        public async Task GetAgreements_Line_Count_Excludes_Excluded_Lines_When_Showing_Fulfilled()
        {
            // Arrange
            var mockMemoryCache = new Mock<IMemoryCache>();
            var fakeTimeProvider = new FakeTimeProvider();
            var dbContextFactory = await ArrangeAndAct(TestData.AgreementLines.View_Headers_Line_Counts_With_Excluded_Lines);
            var dbContext = await dbContextFactory.CreateDbContextAsync();
            var repository = new DataRepository(dbContext, mockMemoryCache.Object, fakeTimeProvider);

            // Act
            var result = repository.GetAgreements(true).ToList();

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal("A712844", result[0].AgreementNumber);
            Assert.Equal("T712843", result[1].AgreementNumber);
            Assert.Equal(2, result[0].LineCount);
            Assert.Equal(3, result[1].LineCount);
            Assert.Equal((int)FulfilmentStatus.FullyFulfiled, result[0].FulfilmentStatus);
            Assert.Equal((int)FulfilmentStatus.PartiallyFulfilled, result[1].FulfilmentStatus);
        }

        [Fact]
        public async Task GetAllLines_Excludes_Excluded_Lines()
        {
            // Arrange
            var mockMemoryCache = new Mock<IMemoryCache>();
            var fakeTimeProvider = new FakeTimeProvider();
            var dbContextFactory = await ArrangeAndAct(TestData.AgreementLines.View_Headers_Line_Counts_With_Excluded_Lines);
            var dbContext = await dbContextFactory.CreateDbContextAsync();
            var repository = new DataRepository(dbContext, mockMemoryCache.Object, fakeTimeProvider);

            // Act
            var result = repository.GetAllLines(1).ToList();

            // Assert
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task GetLines_Excludes_Excluded_Lines()
        {
            // Arrange
            var mockMemoryCache = new Mock<IMemoryCache>();
            var fakeTimeProvider = new FakeTimeProvider();
            var dbContextFactory = await ArrangeAndAct(TestData.AgreementLines.View_Headers_Line_Counts_With_Excluded_Lines);
            var dbContext = await dbContextFactory.CreateDbContextAsync();
            var repository = new DataRepository(dbContext, mockMemoryCache.Object, fakeTimeProvider);

            // Act
            var result = repository.GetLines(1).ToList();

            // Assert
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task GetAllLinesForChangeOrder_Includes_Excludes_Lines()
        {
            // Arrange
            var mockMemoryCache = new Mock<IMemoryCache>();
            var fakeTimeProvider = new FakeTimeProvider();
            var dbContextFactory = await ArrangeAndAct(TestData.AgreementLines.View_Headers_Line_Counts_With_Excluded_Lines);
            var dbContext = await dbContextFactory.CreateDbContextAsync();
            var repository = new DataRepository(dbContext, mockMemoryCache.Object, fakeTimeProvider);

            // Act
            var result = repository.GetAllLinesForChangeOrder(1).ToList();

            // Assert
            Assert.Equal(3, result.Count);
        }

        [Fact]
        public void GetAssetItems_AlwaysExcludesRemovedStock()
        {
            using (var context = new InMemoryDBContext())
            {
                context.AddRange(TestData.Assets.AssetItemsWithSomeRemovedStock);
                context.SaveChanges();

                var mockMemoryCache = new Mock<IMemoryCache>();
                var fakeTimeProvider = new FakeTimeProvider();
                var repository = new DataRepository(context, mockMemoryCache.Object, fakeTimeProvider);

                // Act
                var result = repository.GetAssetItems();

                // Assert
                Assert.Equal(7, result.Count());
            }
        }

        [Fact]
        public void GetAssetItems_ProjectsDaysOffHire()
        {
            using (var context = new InMemoryDBContext())
            {
                context.AddRange(TestData.Assets.AssetItemsWithDaysOffHire);
                context.SaveChanges();

                var mockMemoryCache = new Mock<IMemoryCache>();
                var fakeTimeProvider = new FakeTimeProvider();
                var repository = new DataRepository(context, mockMemoryCache.Object, fakeTimeProvider);

                // Act
                var result = repository.GetAssetItems().ToList();

                // Assert
                Assert.Equal(33, result.First(a => a.Id == "long-off").DaysOffHire);
                Assert.Equal(5, result.First(a => a.Id == "recent-off").DaysOffHire);
                Assert.Equal(0, result.First(a => a.Id == "on-hire").DaysOffHire);
                Assert.Null(result.First(a => a.Id == "never-on-hire").DaysOffHire);
            }
        }

        [Fact]
        public async Task SetHeaderForActivation_ShouldSetLastUpdatedBy()
        {
            // Arrange
            using var context = new InMemoryDBContext();
            var header = new Header
            {
                AgreementNumber = "A999001",
                AgreementNumbersOnly = "999001",
                CustomerNumber = "GB00013290",
                CustomerAddressCode = "900000",
                Division = "200",
                OrderSource = "NOF",
                Facility = "USG",
                FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled,
                ActivationStatus = (int)ActivationStatus.TODO
            };
            context.Headers.Add(header);

            var line = new Line
            {
                HeaderId = header.Id,
                AgreementLineNumber = "A999001-1",
                ItemNumber = "TEST001",
                Quantity = 1,
                Division = "200",
                OrderSource = "NOF",
                Facility = "USG",
                Warehouse = "ED0",
                RequiresFulfilment = true,
                ActivationStatus = (int)ActivationStatus.TODO
            };
            context.Lines.Add(line);
            header.Lines.Add(line);
            context.SaveChanges();

            var mockMemoryCache = new Mock<IMemoryCache>();
            var fakeTimeProvider = new FakeTimeProvider();
            var repository = new DataRepository(context, mockMemoryCache.Object, fakeTimeProvider);

            var mockIdentity = new Mock<IUserIdentity>();
            mockIdentity.Setup(x => x.GetIdentity()).Returns(new User { LoginName = "amanda.benz@aggreko.com", FullName = "Amanda Benz" });

            // Act
            await repository.SetHeaderForActivation(header.Id, mockIdentity.Object);

            // Assert
            var updatedHeader = context.Headers.First(h => h.Id == header.Id);
            Assert.Equal("amanda.benz@aggreko.com", updatedHeader.LastUpdatedBy);
            Assert.NotNull(updatedHeader.LastUpdatedDate);

            var updatedLine = context.Lines.First(l => l.Id == line.Id);
            Assert.Equal("amanda.benz@aggreko.com", updatedLine.LastUpdatedBy);
        }

        [Fact]
        public void SetLineActivationStatus_ShouldSetLastUpdatedBy()
        {
            // Arrange
            using var context = new InMemoryDBContext();
            var header = new Header
            {
                AgreementNumber = "A999002",
                AgreementNumbersOnly = "999002",
                CustomerNumber = "GB00013290",
                CustomerAddressCode = "900000",
                Division = "200",
                OrderSource = "NOF",
                Facility = "USG",
                FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled
            };
            context.Headers.Add(header);

            var line = new Line
            {
                HeaderId = header.Id,
                AgreementLineNumber = "A999002-1",
                ItemNumber = "TEST001",
                Quantity = 1,
                Division = "200",
                OrderSource = "NOF",
                Facility = "USG",
                Warehouse = "ED0",
                RequiresFulfilment = true,
                ActivationStatus = (int)ActivationStatus.Requested
            };
            context.Lines.Add(line);
            context.SaveChanges();

            var mockMemoryCache = new Mock<IMemoryCache>();
            var fakeTimeProvider = new FakeTimeProvider();
            var repository = new DataRepository(context, mockMemoryCache.Object, fakeTimeProvider);

            var mockIdentity = new Mock<IUserIdentity>();
            mockIdentity.Setup(x => x.GetIdentity()).Returns(new User { LoginName = "suzy.larochelle@aggreko.com", FullName = "Suzy Larochelle" });

            // Act
            repository.SetLineActivationStatus(line, ActivationStatus.TODO, mockIdentity.Object);

            // Assert
            var updatedLine = context.Lines.First(l => l.Id == line.Id);
            Assert.Equal((int)ActivationStatus.TODO, updatedLine.ActivationStatus);
            Assert.Equal("suzy.larochelle@aggreko.com", updatedLine.LastUpdatedBy);
            Assert.NotNull(updatedLine.LastUpdatedDate);
        }

        [Fact]
        public void TimeoutSublineActivation_ShouldSetLastUpdatedBy()
        {
            // Arrange
            using var context = new InMemoryDBContext();
            var header = new Header
            {
                AgreementNumber = "A999003",
                AgreementNumbersOnly = "999003",
                CustomerNumber = "GB00013290",
                CustomerAddressCode = "900000",
                Division = "200",
                OrderSource = "NOF",
                Facility = "USG",
                FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled,
                ActivationStatus = (int)ActivationStatus.Requested
            };
            context.Headers.Add(header);

            var line = new Line
            {
                HeaderId = header.Id,
                AgreementLineNumber = "A999003-1",
                ItemNumber = "TEST001",
                Quantity = 1,
                Division = "200",
                OrderSource = "NOF",
                Facility = "USG",
                Warehouse = "ED0",
                RequiresFulfilment = true,
                ActivationStatus = (int)ActivationStatus.Requested
            };
            context.Lines.Add(line);
            header.Lines.Add(line);
            context.SaveChanges();

            var mockMemoryCache = new Mock<IMemoryCache>();
            var fakeTimeProvider = new FakeTimeProvider();
            var repository = new DataRepository(context, mockMemoryCache.Object, fakeTimeProvider);

            var mockIdentity = new Mock<IUserIdentity>();
            mockIdentity.Setup(x => x.GetIdentity()).Returns(new User { LoginName = "amanda.benz@aggreko.com", FullName = "Amanda Benz" });

            // Act
            repository.TimeoutSublineActivation(header.Id, mockIdentity.Object);

            // Assert
            var updatedHeader = context.Headers.First(h => h.Id == header.Id);
            Assert.Equal("amanda.benz@aggreko.com", updatedHeader.LastUpdatedBy);
            Assert.NotNull(updatedHeader.LastUpdatedDate);

            var updatedLine = context.Lines.First(l => l.Id == line.Id);
            Assert.Equal((int)ActivationStatus.Failed, updatedLine.ActivationStatus);
            Assert.Equal("amanda.benz@aggreko.com", updatedLine.LastUpdatedBy);
        }
    }
}
