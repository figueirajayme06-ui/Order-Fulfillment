using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OF.Api.UseCases.Agreements;
using OF.Common.Infrastructure.IPG.Orders;
using OF.Common.Infrastructure.OF;
using OF.Data.Database;
using OF.Tests.Common;
using OF.Tests.Data.Test;
using OF.Tests.Data.Test.BODs.AgreementLines;
namespace OF.Tests.Api.UseCases.Agreements
{
    [Collection("DatabaseCollection")]
    public class AgreementLineReceivedAsExcludedHandlerTests : CommonDBTest
    {
        public AgreementLineReceivedAsExcludedHandlerTests(DatabaseFixture databaseFixture) : base(databaseFixture)
        {
        }

        [SkippableTheory]
        [InlineData("FSLLABOUR")]
        [InlineData("FUEL OUT/IN")]
        [InlineData("MTR123")]
        [InlineData("XX123")]
        [InlineData("XH123")]
        [InlineData("TX123")]
        [InlineData("BD123")]
        [InlineData("BF123")]
        [InlineData("PF123")]
        [InlineData("SERV123")]
        [InlineData("YDEF OUT/IN")]
        public async Task Header_Still_Fulfilled_Though_Excluded_Line_Received_And_Added(string itemNumber)
        {
            var dbContextFactory = await ArrangeAndAct(nameof(Header_Still_Fulfilled_Though_Excluded_Line_Received_And_Added), TestData.AgreementLines.FulfilledOrderAndLine);
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            var fulfilmentEngione = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var mockLogger = new Mock<ILogger>();

            var sut = new AgreementLineReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngione, mockLogger.Object);

            var bod = string.Format(AgreementLineBOD.SyncDeliveredExcludedLine, itemNumber);
            await sut.Handle(bod);

            var header = await dbContext.Headers.SingleAsync();
            var lines = await dbContext.Lines.ToListAsync();
            var line = lines[0];
            var excludedLine = lines[1];

            Assert.Equal((int)FulfilmentStatus.FullyFulfiled, header.FulfilmentStatus);
            Assert.Equal((int)FulfilmentStatus.FullyFulfiled, line.FulfilmentStatus);
            Assert.Equal((int)FulfilmentStatus.FullyFulfiled, excludedLine.FulfilmentStatus);

            Assert.Equal("XGCE1500", line.ItemNumber);
            Assert.Equal("XGCE1500", line.GenericItemNumber);
            Assert.Equal("ED0", line.Warehouse);
            Assert.Equal(2, line.Quantity);
            Assert.True(line.RequiresFulfilment);

            Assert.Equal(itemNumber, excludedLine.ItemNumber);
            Assert.Equal(itemNumber, excludedLine.GenericItemNumber);
            Assert.Equal("BD0", excludedLine.Warehouse);
            Assert.Equal(1, excludedLine.Quantity);
            Assert.False(excludedLine.RequiresFulfilment);
        }

        [SkippableTheory]
        [InlineData("XXMISC123")]
        public async Task Header_Needs_Fulfilled_When_XX_Line_Received_And_Added(string itemNumber)
        {
            var dbContextFactory = await ArrangeAndAct(nameof(Header_Needs_Fulfilled_When_XX_Line_Received_And_Added), TestData.AgreementLines.FulfilledOrderAndLine);
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            var fulfilmentEngione = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var mockLogger = new Mock<ILogger>();

            var sut = new AgreementLineReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngione, mockLogger.Object);

            var bod = string.Format(AgreementLineBOD.SyncDeliveredExcludedLine, itemNumber);
            await sut.Handle(bod);

            var header = await dbContext.Headers.SingleAsync();
            var lines = await dbContext.Lines.ToListAsync();
            var line = lines[0];
            var excludedLine = lines[1];

            Assert.Equal((int)FulfilmentStatus.PartiallyFulfilled, header.FulfilmentStatus);
            Assert.Equal((int)FulfilmentStatus.FullyFulfiled, line.FulfilmentStatus);
            Assert.Equal((int)FulfilmentStatus.Unfulfilled, excludedLine.FulfilmentStatus);

            Assert.Equal("XGCE1500", line.ItemNumber);
            Assert.Equal("XGCE1500", line.GenericItemNumber);
            Assert.Equal("ED0", line.Warehouse);
            Assert.Equal(2, line.Quantity);
            Assert.True(line.RequiresFulfilment);

            Assert.Equal(itemNumber, excludedLine.ItemNumber);
            Assert.Equal(itemNumber, excludedLine.GenericItemNumber);
            Assert.Equal("BD0", excludedLine.Warehouse);
            Assert.Equal(1, excludedLine.Quantity);
            Assert.True(excludedLine.RequiresFulfilment);
        }
    }
}
