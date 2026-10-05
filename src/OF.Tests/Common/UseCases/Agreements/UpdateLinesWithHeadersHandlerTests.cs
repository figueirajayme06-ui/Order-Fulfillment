using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OF.Common.UseCases.Agreements;
using OF.Data;
using OF.Data.Database;
using OF.Tests.Common;
using static OF.Common.Enums;

namespace OF.Tests.Common.UseCases.Agreements
{
    [Collection("DatabaseCollection")]
    public class UpdateLinesWithHeadersHandlerTests : CommonDBTest
    {
        public UpdateLinesWithHeadersHandlerTests(DatabaseFixture databaseFixture)
            : base(databaseFixture)
        {
        }

        [SkippableFact]
        public async Task Should_Update_OnHire_OffHire_Dates_Using_Inner_Join()
        {
            var dbContextFactory = await ArrangeAndAct((ApplicationDbContext dbContext) =>
            {
                // Create header without OnHire/OffHire dates
                var header = new Header
                {
                    AgreementNumber = "A712565",
                    AgreementNumbersOnly = "712565",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000",
                    OnHireDate = null,
                    OffHireDate = null,
                    FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled
                };
                dbContext.Headers.Add(header);
                dbContext.SaveChanges();

                // Create lines with ValidFromDate and ValidToDate
                var line1 = new Line
                {
                    HeaderId = header.Id,
                    AgreementLineNumber = "A712565-1",
                    AgreementNumbersOnly = "712565",
                    AgreementLineIndex = 1,
                    ItemNumber = "ITEM001",
                    ValidFromDate = new DateTime(2026, 1, 15),
                    ValidToDate = new DateTime(2026, 3, 15),
                    Division = "200",
                    Facility = "USG",
                    RequiresFulfilment = true
                };

                var line2 = new Line
                {
                    HeaderId = header.Id,
                    AgreementLineNumber = "A712565-2",
                    AgreementNumbersOnly = "712565",
                    AgreementLineIndex = 2,
                    ItemNumber = "ITEM002",
                    ValidFromDate = new DateTime(2026, 1, 10), // Earlier date
                    ValidToDate = new DateTime(2026, 4, 20),   // Later date
                    Division = "200",
                    Facility = "USG",
                    RequiresFulfilment = true
                };

                dbContext.Lines.AddRange(line1, line2);
                dbContext.SaveChanges();

                return header;
            });

            var dbContext = await dbContextFactory.CreateDbContextAsync();
            var mockLogger = new Mock<ILogger>();

            var sut = new UpdateLinesWithHeadersHandler(dbContext, mockLogger.Object);

            var header = await dbContext.Headers.FirstAsync();

            // Act
            await sut.Handle(header.Id);

            // Assert - need to reload from database to see SQL updates
            await dbContext.Entry(await dbContext.Headers.FirstAsync(h => h.Id == header.Id)).ReloadAsync();
            var updatedHeader = await dbContext.Headers.FirstAsync(h => h.Id == header.Id);

            // Should have Min(ValidFromDate) as OnHireDate
            Assert.NotNull(updatedHeader.OnHireDate);
            Assert.Equal(new DateTime(2026, 1, 10), updatedHeader.OnHireDate);

            // Should have Max(ValidToDate) as OffHireDate
            Assert.NotNull(updatedHeader.OffHireDate);
            Assert.Equal(new DateTime(2026, 4, 20), updatedHeader.OffHireDate);
        }

        [SkippableFact]
        public async Task Should_Not_Update_If_Header_Already_Has_OnHire_And_OffHire_Dates()
        {
            var dbContextFactory = await ArrangeAndAct((ApplicationDbContext dbContext) =>
            {
                var existingOnHire = new DateTime(2025, 12, 1);
                var existingOffHire = new DateTime(2026, 2, 1);

                // Create header WITH OnHire/OffHire dates already set
                var header = new Header
                {
                    AgreementNumber = "A712565",
                    AgreementNumbersOnly = "712565",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000",
                    OnHireDate = existingOnHire,
                    OffHireDate = existingOffHire,
                    FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled
                };
                dbContext.Headers.Add(header);
                dbContext.SaveChanges();

                // Create lines with different dates
                var line = new Line
                {
                    HeaderId = header.Id,
                    AgreementLineNumber = "A712565-1",
                    AgreementNumbersOnly = "712565",
                    AgreementLineIndex = 1,
                    ItemNumber = "ITEM001",
                    ValidFromDate = new DateTime(2026, 1, 15),
                    ValidToDate = new DateTime(2026, 3, 15),
                    Division = "200",
                    Facility = "USG",
                    RequiresFulfilment = true
                };

                dbContext.Lines.Add(line);
                dbContext.SaveChanges();

                return header;
            });

            var dbContext = await dbContextFactory.CreateDbContextAsync();
            var mockLogger = new Mock<ILogger>();

            var sut = new UpdateLinesWithHeadersHandler(dbContext, mockLogger.Object);

            var header = await dbContext.Headers.FirstAsync();
            var originalOnHire = header.OnHireDate;
            var originalOffHire = header.OffHireDate;

            // Act
            await sut.Handle(header.Id);

            // Assert - dates should remain unchanged
            var updatedHeader = await dbContext.Headers.FirstAsync(h => h.Id == header.Id);

            Assert.Equal(originalOnHire, updatedHeader.OnHireDate);
            Assert.Equal(originalOffHire, updatedHeader.OffHireDate);
        }

        [SkippableFact]
        public async Task Should_Update_ActivationStatus_For_A_Numbers_With_Requested_Status()
        {
            var dbContextFactory = await ArrangeAndAct((ApplicationDbContext dbContext) =>
            {
                // Create header with A number and Requested status
                var header = new Header
                {
                    AgreementNumber = "A712565",
                    AgreementNumbersOnly = "712565",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000",
                    ActivationStatus = (int)ActivationStatus.Requested,
                    FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled
                };
                dbContext.Headers.Add(header);
                dbContext.SaveChanges();

                return header;
            });

            var dbContext = await dbContextFactory.CreateDbContextAsync();
            var mockLogger = new Mock<ILogger>();

            var sut = new UpdateLinesWithHeadersHandler(dbContext, mockLogger.Object);

            var header = await dbContext.Headers.FirstAsync();

            // Act
            await sut.Handle(header.Id);

            // Assert - need to reload from database to see SQL updates
            await dbContext.Entry(await dbContext.Headers.FirstAsync(h => h.Id == header.Id)).ReloadAsync();
            var updatedHeader = await dbContext.Headers.FirstAsync(h => h.Id == header.Id);

            Assert.Equal((int)ActivationStatus.Activated, updatedHeader.ActivationStatus);
            Assert.NotNull(updatedHeader.LastUpdatedDate);
        }

        [SkippableFact]
        public async Task Should_Not_Update_ActivationStatus_For_T_Numbers()
        {
            var dbContextFactory = await ArrangeAndAct((ApplicationDbContext dbContext) =>
            {
                // Create header with T number and Requested status
                var header = new Header
                {
                    AgreementNumber = "T712565",
                    AgreementNumbersOnly = "712565",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000",
                    ActivationStatus = (int)ActivationStatus.Requested,
                    FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled
                };
                dbContext.Headers.Add(header);
                dbContext.SaveChanges();

                return header;
            });

            var dbContext = await dbContextFactory.CreateDbContextAsync();
            var mockLogger = new Mock<ILogger>();

            var sut = new UpdateLinesWithHeadersHandler(dbContext, mockLogger.Object);

            var header = await dbContext.Headers.FirstAsync();

            // Act
            await sut.Handle(header.Id);

            // Assert
            var updatedHeader = await dbContext.Headers.FirstAsync(h => h.Id == header.Id);

            // Should remain Requested since it's a T number
            Assert.Equal((int)ActivationStatus.Requested, updatedHeader.ActivationStatus);
        }

        [SkippableFact]
        public async Task Should_Throw_Exception_For_Invalid_HeaderId()
        {
            var dbContextFactory = await ArrangeAndAct();
            var dbContext = await dbContextFactory.CreateDbContextAsync();
            var mockLogger = new Mock<ILogger>();

            var sut = new UpdateLinesWithHeadersHandler(dbContext, mockLogger.Object);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await sut.Handle(0));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await sut.Handle(-1));
        }
    }
}
