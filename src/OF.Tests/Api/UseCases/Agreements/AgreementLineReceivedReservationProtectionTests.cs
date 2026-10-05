using AutoFixture;
using AutoFixture.AutoMoq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OF.Api.UseCases.Agreements;
using OF.Common;
using OF.Common.Infrastructure.IPG.Orders;
using OF.Common.Infrastructure.OF;
using OF.Data;
using OF.Data.Database;
using OF.Tests.Common;
using OF.Tests.Data.Test.BODs.AgreementLines;
using static OF.Common.Enums;

namespace OF.Tests.Api.UseCases.Agreements
{
    [Collection("DatabaseCollection")]
    public class AgreementLineReceivedReservationProtectionTests : CommonDBTest
    {
        private readonly IFixture _fixture;

        public AgreementLineReceivedReservationProtectionTests(DatabaseFixture databaseFixture) : base(databaseFixture)
        {
            _fixture = new Fixture().Customize(new AutoMoqCustomization());
            _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
                .ForEach(b => _fixture.Behaviors.Remove(b));
            _fixture.Behaviors.Add(new OmitOnRecursionBehavior());
        }

        [SkippableFact]
        public async Task Delete_Preserves_Reservations_When_Line_Has_Confirmed_Reservation()
        {
            // Arrange - Header is NOT in a historical status but line has a confirmed reservation (on-hire/delivered)
            var dbContextFactory = await SeedData(nameof(Delete_Preserves_Reservations_When_Line_Has_Confirmed_Reservation));
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var targetHeader = new Header
            {
                AgreementNumber = "A712794",
                AgreementNumbersOnly = "712794",
                CustomerNumber = "US00103535",
                CustomerAddressCode = "900000",
                Division = "200",
                Facility = "USG",
                OrderSource = "CPQ",
                Status = Constants.HeaderStatus.OnHire,
                FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled,
                ActivationStatus = (int)ActivationStatus.Activated
            };

            var lineToDelete = new Line
            {
                Header = targetHeader,
                AgreementLineNumber = "A712794-1",
                AgreementNumbersOnly = "712794",
                AgreementLineIndex = 1,
                ItemNumber = "GN0125GHPCAN",
                GenericItemNumber = "XGGN0125",
                Warehouse = "BD0",
                Quantity = 1,
                Division = "200",
                Facility = "USG",
                OrderSource = "CPQ",
                ValidFromDate = DateTime.UtcNow.AddDays(-7),
                ValidToDate = DateTime.UtcNow.AddDays(30),
                FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled,
                ActivationStatus = (int)ActivationStatus.Activated,
                RequiresFulfilment = true,
                Status = Constants.M3LineStatus.OnHire.ToString()
            };

            dbContext.Headers.Add(targetHeader);
            dbContext.Lines.Add(lineToDelete);
            await dbContext.SaveChangesAsync();

            var confirmedReservation = new Reservation
            {
                AssetId = "XAPP004",
                ItemNumber = "GN0125GHPCAN",
                Quantity = 1,
                Warehouse = "BD0",
                LineId = lineToDelete.Id,
                IsConfirmed = true,
                ActualAssetId = "XAPP004",
                ActualItemNumber = "GN0125GHPCAN",
                ActualQuantity = 1
            };

            dbContext.Reservations.Add(confirmedReservation);
            await dbContext.SaveChangesAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            var fulfilmentEngine = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var mockLogger = new Mock<ILogger>();

            var sut = new AgreementLineReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngine, mockLogger.Object);

            // Act
            await sut.Handle(AgreementLineBOD.SyncDeleted);

            // Assert
            var line = await dbContext.Lines.FirstAsync(l => l.Id == lineToDelete.Id);
            var reservations = await dbContext.Reservations.Where(r => r.LineId == lineToDelete.Id).ToListAsync();

            Assert.True(line.IsDeleted);
            Assert.Single(reservations);
            Assert.True(reservations[0].IsConfirmed);
            Assert.Equal("XAPP004", reservations[0].AssetId);
        }

        [SkippableFact]
        public async Task Delete_Removes_Reservations_When_Line_Has_No_Confirmed_Reservation()
        {
            // Arrange - Line has an unconfirmed reservation that should be deleted
            var dbContextFactory = await SeedData(nameof(Delete_Removes_Reservations_When_Line_Has_No_Confirmed_Reservation));
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var targetHeader = new Header
            {
                AgreementNumber = "A712794",
                AgreementNumbersOnly = "712794",
                CustomerNumber = "US00103535",
                CustomerAddressCode = "900000",
                Division = "200",
                Facility = "USG",
                OrderSource = "CPQ",
                Status = Constants.HeaderStatus.LineCreated,
                FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled,
                ActivationStatus = (int)ActivationStatus.TODO
            };

            var lineToDelete = new Line
            {
                Header = targetHeader,
                AgreementLineNumber = "A712794-1",
                AgreementNumbersOnly = "712794",
                AgreementLineIndex = 1,
                ItemNumber = "XGCE1500",
                GenericItemNumber = "XGCE1500",
                Warehouse = "ED0",
                Quantity = 1,
                Division = "200",
                Facility = "USG",
                OrderSource = "CPQ",
                ValidFromDate = DateTime.UtcNow.AddDays(-1),
                ValidToDate = DateTime.UtcNow.AddDays(1),
                FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled,
                ActivationStatus = (int)ActivationStatus.TODO,
                RequiresFulfilment = true
            };

            dbContext.Headers.Add(targetHeader);
            dbContext.Lines.Add(lineToDelete);
            await dbContext.SaveChangesAsync();

            var unconfirmedReservation = new Reservation
            {
                AssetId = "XAPP007",
                ItemNumber = "GN0155GHPCAN",
                Quantity = 1,
                Warehouse = "BD1",
                LineId = lineToDelete.Id,
                IsConfirmed = false
            };

            dbContext.Reservations.Add(unconfirmedReservation);
            await dbContext.SaveChangesAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            var fulfilmentEngine = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var mockLogger = new Mock<ILogger>();

            var sut = new AgreementLineReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngine, mockLogger.Object);

            // Act
            await sut.Handle(AgreementLineBOD.SyncDeleted);

            // Assert
            var line = await dbContext.Lines.FirstAsync(l => l.Id == lineToDelete.Id);
            var reservations = await dbContext.Reservations.Where(r => r.LineId == lineToDelete.Id).ToListAsync();

            Assert.True(line.IsDeleted);
            Assert.Empty(reservations);
        }

        [SkippableFact]
        public async Task Delete_Preserves_Reservations_For_Historical_Header_Status()
        {
            // Arrange - Header is in a historical status (Invoiced), reservation should be preserved
            var dbContextFactory = await SeedData(nameof(Delete_Preserves_Reservations_For_Historical_Header_Status));
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var targetHeader = new Header
            {
                AgreementNumber = "A712794",
                AgreementNumbersOnly = "712794",
                CustomerNumber = "US00103535",
                CustomerAddressCode = "900000",
                Division = "200",
                Facility = "USG",
                OrderSource = "CPQ",
                Status = Constants.HeaderStatus.Invoiced,
                FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled,
                ActivationStatus = (int)ActivationStatus.Activated
            };

            var lineToDelete = new Line
            {
                Header = targetHeader,
                AgreementLineNumber = "A712794-1",
                AgreementNumbersOnly = "712794",
                AgreementLineIndex = 1,
                ItemNumber = "GN0125GHPCAN",
                GenericItemNumber = "XGGN0125",
                Warehouse = "BD0",
                Quantity = 1,
                Division = "200",
                Facility = "USG",
                OrderSource = "CPQ",
                ValidFromDate = DateTime.UtcNow.AddDays(-7),
                ValidToDate = DateTime.UtcNow.AddDays(30),
                FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled,
                ActivationStatus = (int)ActivationStatus.Activated,
                RequiresFulfilment = true
            };

            dbContext.Headers.Add(targetHeader);
            dbContext.Lines.Add(lineToDelete);
            await dbContext.SaveChangesAsync();

            var reservation = new Reservation
            {
                AssetId = "XAPP004",
                ItemNumber = "GN0125GHPCAN",
                Quantity = 1,
                Warehouse = "BD0",
                LineId = lineToDelete.Id,
                IsConfirmed = true,
                ActualAssetId = "XAPP004",
                ActualItemNumber = "GN0125GHPCAN",
                ActualQuantity = 1
            };

            dbContext.Reservations.Add(reservation);
            await dbContext.SaveChangesAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            var fulfilmentEngine = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var mockLogger = new Mock<ILogger>();

            var sut = new AgreementLineReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngine, mockLogger.Object);

            // Act
            await sut.Handle(AgreementLineBOD.SyncDeleted);

            // Assert
            var line = await dbContext.Lines.FirstAsync(l => l.Id == lineToDelete.Id);
            var reservations = await dbContext.Reservations.Where(r => r.LineId == lineToDelete.Id).ToListAsync();

            Assert.True(line.IsDeleted);
            Assert.Single(reservations);
            Assert.True(reservations[0].IsConfirmed);
        }

        [SkippableFact]
        public async Task Line_Lookup_Matches_Correct_Agreement_Only()
        {
            // Arrange - Two agreements with lines that share the same line suffix (A712565-3)
            // The lookup must only match lines belonging to the correct agreement
            var dbContextFactory = await SeedData(nameof(Line_Lookup_Matches_Correct_Agreement_Only));
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var targetHeader = new Header
            {
                AgreementNumber = "A712565",
                AgreementNumbersOnly = "712565",
                CustomerNumber = "US00103535",
                CustomerAddressCode = "800000",
                Division = "200",
                Facility = "USG",
                OrderSource = "CPQ",
                FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled,
                ActivationStatus = (int)ActivationStatus.TODO
            };

            var targetLine = new Line
            {
                Header = targetHeader,
                AgreementLineNumber = "A712565-3",
                AgreementNumbersOnly = "712565",
                AgreementLineIndex = 3,
                ItemNumber = "OLD_ITEM",
                GenericItemNumber = "OLD_GENERIC",
                Warehouse = "OLD_WH",
                Quantity = 5,
                Division = "200",
                Facility = "USG",
                OrderSource = "CPQ",
                ValidFromDate = DateTime.UtcNow.AddDays(-1),
                ValidToDate = DateTime.UtcNow.AddDays(1),
                FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled,
                ActivationStatus = (int)ActivationStatus.TODO,
                RequiresFulfilment = true
            };

            // Different agreement with same line number pattern
            var otherHeader = new Header
            {
                AgreementNumber = "A999999",
                AgreementNumbersOnly = "999999",
                CustomerNumber = "US00999999",
                CustomerAddressCode = "800001",
                Division = "200",
                Facility = "USG",
                OrderSource = "CPQ",
                FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled,
                ActivationStatus = (int)ActivationStatus.Activated
            };

            var otherLine = new Line
            {
                Header = otherHeader,
                AgreementLineNumber = "A999999-3",
                AgreementNumbersOnly = "999999",
                AgreementLineIndex = 3,
                ItemNumber = "OTHER_ITEM",
                GenericItemNumber = "OTHER_GENERIC",
                Warehouse = "OTHER_WH",
                Quantity = 10,
                Division = "200",
                Facility = "USG",
                OrderSource = "CPQ",
                ValidFromDate = DateTime.UtcNow.AddDays(-1),
                ValidToDate = DateTime.UtcNow.AddDays(1),
                FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled,
                ActivationStatus = (int)ActivationStatus.Activated,
                RequiresFulfilment = true
            };

            dbContext.Headers.AddRange(targetHeader, otherHeader);
            dbContext.Lines.AddRange(targetLine, otherLine);
            await dbContext.SaveChangesAsync();

            var otherReservation = new Reservation
            {
                AssetId = "XOTHER001",
                ItemNumber = "OTHER_ITEM",
                Quantity = 10,
                Warehouse = "OTHER_WH",
                LineId = otherLine.Id,
                IsConfirmed = true,
                ActualAssetId = "XOTHER001",
                ActualItemNumber = "OTHER_ITEM",
                ActualQuantity = 10
            };

            dbContext.Reservations.Add(otherReservation);
            await dbContext.SaveChangesAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            var fulfilmentEngine = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var mockLogger = new Mock<ILogger>();

            var sut = new AgreementLineReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngine, mockLogger.Object);

            // Act - Send a sync BOD for agreement A712565 line 3 (Replace action)
            await sut.Handle(AgreementLineBOD.Sync);

            // Assert - The other agreement's line and reservation must be unaffected
            var otherLineRefreshed = await dbContext.Lines.FirstAsync(l => l.Id == otherLine.Id);
            var otherReservations = await dbContext.Reservations.Where(r => r.LineId == otherLine.Id).ToListAsync();

            Assert.Equal("OTHER_ITEM", otherLineRefreshed.ItemNumber);
            Assert.Equal(10, otherLineRefreshed.Quantity);
            Assert.False(otherLineRefreshed.IsDeleted);
            Assert.Single(otherReservations);
            Assert.Equal("XOTHER001", otherReservations[0].AssetId);

            // Verify the target line was updated
            var targetLineRefreshed = await dbContext.Lines.FirstAsync(l => l.Id == targetLine.Id);
            Assert.Equal("XDH51005R", targetLineRefreshed.ItemNumber);
            Assert.Equal(1, targetLineRefreshed.Quantity);
        }

        [SkippableFact]
        public async Task Line_Lookup_Does_Not_Match_Deleted_Lines()
        {
            // Arrange - A deleted line exists with matching agreement number pattern
            // A new BOD should create a new line rather than matching the deleted one
            var dbContextFactory = await SeedData(nameof(Line_Lookup_Does_Not_Match_Deleted_Lines));
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var targetHeader = new Header
            {
                AgreementNumber = "A712565",
                AgreementNumbersOnly = "712565",
                CustomerNumber = "US00103535",
                CustomerAddressCode = "800000",
                Division = "200",
                Facility = "USG",
                OrderSource = "CPQ",
                FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled,
                ActivationStatus = (int)ActivationStatus.TODO
            };

            var deletedLine = new Line
            {
                Header = targetHeader,
                AgreementLineNumber = "A712565-3",
                AgreementNumbersOnly = "712565",
                AgreementLineIndex = 3,
                ItemNumber = "DELETED_ITEM",
                GenericItemNumber = "DELETED_GENERIC",
                Warehouse = "DEL_WH",
                Quantity = 99,
                Division = "200",
                Facility = "USG",
                OrderSource = "CPQ",
                ValidFromDate = DateTime.UtcNow.AddDays(-10),
                ValidToDate = DateTime.UtcNow.AddDays(-5),
                FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled,
                ActivationStatus = (int)ActivationStatus.TODO,
                RequiresFulfilment = true,
                IsDeleted = true
            };

            dbContext.Headers.Add(targetHeader);
            dbContext.Lines.Add(deletedLine);
            await dbContext.SaveChangesAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            var fulfilmentEngine = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var mockLogger = new Mock<ILogger>();

            var sut = new AgreementLineReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngine, mockLogger.Object);

            // Act - Send a sync BOD for A712565 line 3
            await sut.Handle(AgreementLineBOD.Sync);

            // Assert - A new line should be created since the existing one is deleted
            var lines = await dbContext.Lines
                .Where(l => l.AgreementNumbersOnly == "712565")
                .ToListAsync();

            var activeLines = lines.Where(l => !l.IsDeleted && l.AgreementLineNumber == "A712565-3").ToList();
            var deletedLines = lines.Where(l => l.IsDeleted).ToList();

            Assert.Single(activeLines);
            Assert.Equal("XDH51005R", activeLines[0].ItemNumber);
            Assert.Single(deletedLines);
            Assert.Equal("DELETED_ITEM", deletedLines[0].ItemNumber);
        }

        [SkippableFact]
        public async Task Sync_Delivered_Preserves_Reservation_On_Already_Fulfilled_Historical_Line()
        {
            // Arrange - Agreement A608408-style scenario: line is fully allocated and invoiced
            // A subsequent sync should preserve (or recreate) the reservation, not leave it missing
            var dbContextFactory = await SeedData(nameof(Sync_Delivered_Preserves_Reservation_On_Already_Fulfilled_Historical_Line));
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var targetHeader = new Header
            {
                AgreementNumber = "A712844",
                AgreementNumbersOnly = "712844",
                CustomerNumber = "US00103535",
                CustomerAddressCode = "900000",
                Division = "200",
                Facility = "USG",
                OrderSource = "CPQ",
                Status = Constants.HeaderStatus.OnHire,
                FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled,
                ActivationStatus = (int)ActivationStatus.Activated
            };

            var existingLine = new Line
            {
                Header = targetHeader,
                AgreementLineNumber = "A712844-1",
                AgreementNumbersOnly = "712844",
                AgreementLineIndex = 1,
                ItemNumber = "GN0125GHPCAN",
                GenericItemNumber = "XGGN0125",
                Warehouse = "BD0",
                Quantity = 1,
                Division = "200",
                Facility = "USG",
                OrderSource = "CPQ",
                ValidFromDate = DateTime.UtcNow.AddDays(-7),
                ValidToDate = DateTime.UtcNow.AddDays(30),
                FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled,
                ActivationStatus = (int)ActivationStatus.Activated,
                RequiresFulfilment = true,
                Status = Constants.M3LineStatus.OnHire.ToString()
            };

            dbContext.Headers.Add(targetHeader);
            dbContext.Lines.Add(existingLine);
            await dbContext.SaveChangesAsync();

            var confirmedReservation = new Reservation
            {
                AssetId = "XAPP004",
                ItemNumber = "GN0125GHPCAN",
                Quantity = 1,
                Warehouse = "BD0",
                LineId = existingLine.Id,
                IsConfirmed = true,
                ActualAssetId = "XAPP004",
                ActualItemNumber = "GN0125GHPCAN",
                ActualQuantity = 1
            };

            dbContext.Reservations.Add(confirmedReservation);
            await dbContext.SaveChangesAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            var fulfilmentEngine = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var mockLogger = new Mock<ILogger>();

            var sut = new AgreementLineReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngine, mockLogger.Object);

            // Act - Process a delivered line sync (status 50, delivery status 99)
            await sut.Handle(AgreementLineBOD.SyncDeliveredMatching);

            // Assert - Reservation should still exist and be confirmed
            var line = await dbContext.Lines.FirstAsync(l => l.Id == existingLine.Id);
            var reservations = await dbContext.Reservations.Where(r => r.LineId == existingLine.Id).ToListAsync();

            Assert.False(line.IsDeleted);
            Assert.Equal((int)FulfilmentStatus.FullyFulfiled, line.FulfilmentStatus);
            Assert.Single(reservations);
            Assert.True(reservations[0].IsConfirmed);
            Assert.Equal("XAPP004", reservations[0].ActualAssetId);
        }

        private async Task<IDbContextFactory<ApplicationDbContext>> SeedData(string testName)
        {
            var dbContextFactory = databaseFixture.SetupDbContext(testName);
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var headers = _fixture.Build<Header>()
                .Without(h => h.Id)
                .With(h => h.Division, "200")
                .With(h => h.Facility, () => $"F{_fixture.Create<int>() % 10}")
                .With(h => h.OrderSource, "CPQ")
                .With(h => h.RentalDepot, () => $"RD{_fixture.Create<int>() % 100:D2}")
                .With(h => h.FulfilmentStatus, () => _fixture.Create<int>() % 4)
                .With(h => h.ActivationStatus, () => _fixture.Create<int>() % 4)
                .With(h => h.IsDeleted, false)
                .With(h => h.IsSkeleton, false)
                .Without(h => h.Lines)
                .Without(h => h.ChangeOrderHeaders)
                .Without(h => h.ChangeOrders)
                .CreateMany(50)
                .ToList();

            for (int i = 0; i < headers.Count; i++)
            {
                var header = headers[i];
                header.AgreementNumber = $"A{800000 + i}";
                header.AgreementNumbersOnly = $"{800000 + i}";

                dbContext.Headers.Add(header);

                var lineCount = 2 + (i % 4);
                var lines = _fixture.Build<Line>()
                    .Without(l => l.Id)
                    .With(l => l.Header, header)
                    .With(l => l.HeaderId, (int?)null)
                    .With(l => l.AgreementNumbersOnly, $"{800000 + i}")
                    .With(l => l.Division, "200")
                    .With(l => l.Facility, header.Facility)
                    .With(l => l.OrderSource, "CPQ")
                    .With(l => l.ValidFromDate, DateTime.UtcNow.AddDays(-7))
                    .With(l => l.ValidToDate, DateTime.UtcNow.AddDays(30))
                    .With(l => l.FulfilmentStatus, () => _fixture.Create<int>() % 4)
                    .With(l => l.ActivationStatus, () => _fixture.Create<int>() % 4)
                    .With(l => l.RequiresFulfilment, true)
                    .With(l => l.IsDeleted, false)
                    .With(l => l.Status, (string?)null)
                    .With(l => l.Warehouse, () => $"WH{_fixture.Create<int>() % 10}")
                    .Without(l => l.ChangeOrderLines)
                    .CreateMany(lineCount)
                    .ToList();

                for (int j = 0; j < lines.Count; j++)
                {
                    lines[j].AgreementLineNumber = $"A{800000 + i}-{j + 1}";
                    lines[j].AgreementLineIndex = j + 1;
                    dbContext.Lines.Add(lines[j]);
                }
            }

            await dbContext.SaveChangesAsync();
            return dbContextFactory;
        }
    }
}
