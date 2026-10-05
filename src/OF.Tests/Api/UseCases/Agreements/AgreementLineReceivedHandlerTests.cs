using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OF.Api.UseCases.Agreements;
using OF.Common.Infrastructure.IPG.Orders;
using OF.Common.Infrastructure.IPG.Orders.Models.Quotes;
using OF.Common.Infrastructure.IPG.Orders.Models.Quotes.Response;
using OF.Common.Infrastructure.IPG.Orders.Models.RAA.Agreementrs;
using OF.Common.Infrastructure.OF;
using OF.Data.Database;
using OF.Tests.Common;
using OF.Tests.Data.Test;
using OF.Tests.Data.Test.BODs.AgreementLines;
using static OF.Common.Enums;
namespace OF.Tests.Api.UseCases.Agreements
{
    [Collection("DatabaseCollection")]
    public class AgreementLineReceivedHandlerTests : CommonDBTest
    {
        public AgreementLineReceivedHandlerTests(DatabaseFixture databaseFixture) : base(databaseFixture)
        {
        }

        [SkippableFact]
        public async Task Agreement_Line_Gets_Salesforce_Data_If_Populated_On_Incoming_Request()
        {
            var dbContextFactory = await ArrangeAndAct(TestData.AgreementLines.UnFulfilledHeaderAndQuoteLine);
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var orderItem = new OrderItem()
            {
                Id = "Q017a000003GAerrBBG",
                OrderLineIndex = 1,
                QuoteLineId = "8017a000003GAerAAG",
                QuoteLine = new QuoteLine()
                {
                    LineId = 1,
                    SelectedAttributesAsText = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok 1",
                    DescriptionWithAttributes = "Calculated Amperage:300;Length (m):30;Cable Type:Extension;Cable Connection Type:Cam-Lok",
                    GenericItemNumber = "XGCE15001",
                    Group = new QuoteGroup()
                    {
                        Name = "Group 1",
                    },
                    Quote = new OrderItemQuote()
                    {
                        Name = "Q-438254"
                    }
                }
            };

            var mockOmService = new Mock<IOrderManagementIntegration>();
            mockOmService.Setup(i => i.Query<OrderItem>(It.IsAny<string>())).ReturnsAsync(new SOQLResponse<OrderItem>()
            {
                Records = new List<OrderItem>()
                {
                    orderItem 
                },
                TotalSize = 1,
                Done = true
            });
            var fulfilmentEngione = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var mockLogger = new Mock<ILogger>();

            var sut = new AgreementLineReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngione, mockLogger.Object);
            await sut.Handle(AgreementLineBOD.SyncDeliveredMatching);

            var header = await dbContext.Headers.SingleAsync();
            var line = await dbContext.Lines.Where(i => i.RequiresFulfilment == true).SingleAsync();
            var reservation = await dbContext.Reservations.SingleAsync();

            Assert.Equal((int)FulfilmentStatus.FullyFulfiled, header.FulfilmentStatus);
            Assert.Equal((int)FulfilmentStatus.FullyFulfiled, line.FulfilmentStatus);

            Assert.Equal("GN0125GHPCAN", line.ItemNumber);
            Assert.Equal(orderItem.QuoteLine.GenericItemNumber, line.GenericItemNumber);
            Assert.Equal("BD0", line.Warehouse);
            Assert.Equal(1, line.Quantity);
            Assert.Equal(orderItem.QuoteLine.Quote.Name, line.QuotePublicId);
            Assert.Equal(orderItem.QuoteLine.Quote.Name.Substring(2), line.QuotePublicIdNumbersOnly);
            Assert.Equal(orderItem.QuoteLine.SelectedAttributesAsText, line.Attributes);
            Assert.Equal(orderItem.QuoteLine.DescriptionWithAttributes, line.DescriptionWithAttributes);
            Assert.Equal(orderItem.QuoteLine.Group.Name, line.PackageGroupNumber);
            Assert.Equal(orderItem.Id, line.OrderLineNumber);
            Assert.Equal(orderItem.OrderLineIndex, line.OrderLineIndex);
            Assert.Equal(orderItem.QuoteLineId, line.QuoteLineNumber);
            Assert.Equal(orderItem.QuoteLine.LineId, line.QuoteLineIndex);

            Assert.Equal("GN0125GHPCAN", reservation.ItemNumber);
            Assert.Equal("XAPP004", reservation.AssetId);
            Assert.Equal("BD0", reservation.Warehouse);
            Assert.Equal(1, reservation.Quantity);
            Assert.Equal(0, reservation.EffectiveQuantity);
            Assert.Equal("GN0125GHPCAN", reservation.ActualItemNumber);
            Assert.Equal("XAPP004", reservation.ActualAssetId);
            Assert.Equal(1, reservation.ActualQuantity);
            Assert.True(reservation.IsConfirmed);
        }

        [SkippableFact]
        public async Task Agreement_UnFulfilled_With_Single_Line_Fulfills_And_Confirms_When_Serialized_Line_Recieved_At_50_99()
        {
            var dbContextFactory = await ArrangeAndAct(TestData.AgreementLines.UnFulfilledHeaderAndLine);
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            var fulfilmentEngione = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var mockLogger = new Mock<ILogger>();

            var sut = new AgreementLineReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngione, mockLogger.Object);
            await sut.Handle(AgreementLineBOD.SyncDeliveredMatching);

            var header = await dbContext.Headers.SingleAsync();
            var line = await dbContext.Lines.Where(i => i.RequiresFulfilment == true).SingleAsync();
            var reservation = await dbContext.Reservations.SingleAsync();

            Assert.Equal((int)FulfilmentStatus.FullyFulfiled, header.FulfilmentStatus);
            Assert.Equal((int)FulfilmentStatus.FullyFulfiled, line.FulfilmentStatus);

            Assert.Equal("GN0125GHPCAN", line.ItemNumber);
            Assert.Equal("XGGN0125", line.GenericItemNumber);
            Assert.Equal("BD0", line.Warehouse);
            Assert.Equal(1, line.Quantity);
            Assert.Equal("Diesel Generator 125 kW", line.ItemDescription);

            Assert.Equal("GN0125GHPCAN", reservation.ItemNumber);
            Assert.Equal("XAPP004", reservation.AssetId);
            Assert.Equal("BD0", reservation.Warehouse);
            Assert.Equal(1, reservation.Quantity);
            Assert.Equal(0, reservation.EffectiveQuantity);
            Assert.Equal("GN0125GHPCAN", reservation.ActualItemNumber);
            Assert.Equal("XAPP004", reservation.ActualAssetId);
            Assert.Equal(1, reservation.ActualQuantity);
            Assert.True(reservation.IsConfirmed);
        }

        [SkippableFact]
        public async Task Agreement_Address_Updates_From_Lines()
        {
            var dbContextFactory = await ArrangeAndAct(TestData.AgreementLines.UnFulfilledHeaderAndLine);
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            var fulfilmentEngione = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var mockLogger = new Mock<ILogger>();

            var sut = new AgreementLineReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngione, mockLogger.Object);
            await sut.Handle(AgreementLineBOD.SyncDeliveredMatchingNotDelivered);

            var header = await dbContext.Headers.SingleAsync();
            var line = await dbContext.Lines.Where(i => i.RequiresFulfilment == true).SingleAsync();
            var reservations = await dbContext.Reservations.ToListAsync();

            Assert.Equal((int)FulfilmentStatus.Unfulfilled, header.FulfilmentStatus);
            Assert.Equal((int)FulfilmentStatus.Unfulfilled, line.FulfilmentStatus);

            Assert.Equal("900000", header.CustomerAddressCode);
            Assert.Equal("400 North Blvd, Baton Rouge, LA LA 70802", header.CustomerAddress);

            Assert.Equal("GN0125GHPCAN", line.ItemNumber);
            Assert.Equal("XGGN0125", line.GenericItemNumber);
            Assert.Equal("BD0", line.Warehouse);
            Assert.Equal(1, line.Quantity);

            Assert.Empty(reservations);
        }

        [SkippableFact]
        public async Task Agreement_UnFulfilled_With_Single_Line_Remains_Unfulfiled_When_Serialized_Line_Recieved_At_50_22()
        {
            var dbContextFactory = await ArrangeAndAct(TestData.AgreementLines.UnFulfilledHeaderAndLine);
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            var fulfilmentEngione = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var mockLogger = new Mock<ILogger>();

            var sut = new AgreementLineReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngione, mockLogger.Object);
            await sut.Handle(AgreementLineBOD.SyncDeliveredMatchingNotDelivered);

            var header = await dbContext.Headers.SingleAsync();
            var line = await dbContext.Lines.Where(i => i.RequiresFulfilment == true).SingleAsync();
            var reservations = await dbContext.Reservations.ToListAsync();

            Assert.Equal((int)FulfilmentStatus.Unfulfilled, header.FulfilmentStatus);
            Assert.Equal((int)FulfilmentStatus.Unfulfilled, line.FulfilmentStatus);

            Assert.Equal("GN0125GHPCAN", line.ItemNumber);
            Assert.Equal("XGGN0125", line.GenericItemNumber);
            Assert.Equal("BD0", line.Warehouse);
            Assert.Equal(1, line.Quantity);

            Assert.Empty(reservations);
        }

        [SkippableFact]
        public async Task Agreement_Fulfilled_With_Single_Line_Confirms_Reservation_With_Actual_On_Serialized_Line_Recieved_At_50_99()
        {
            var dbContextFactory = await ArrangeAndAct(TestData.AgreementLines.FulfilledHeaderAndLine);
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            var fulfilmentEngione = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var mockLogger = new Mock<ILogger>();

            var sut = new AgreementLineReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngione, mockLogger.Object);
            await sut.Handle(AgreementLineBOD.SyncDeliveredMatching);

            var header = await dbContext.Headers.SingleAsync();
            var line = await dbContext.Lines.Where(i => i.RequiresFulfilment == true).SingleAsync();
            var reservation = await dbContext.Reservations.SingleAsync();

            Assert.Equal((int)FulfilmentStatus.FullyFulfiled, header.FulfilmentStatus);
            Assert.Equal((int)FulfilmentStatus.FullyFulfiled, line.FulfilmentStatus);

            Assert.Equal("GN0125GHPCAN", line.ItemNumber);
            Assert.Equal("XGGN0125", line.GenericItemNumber);
            Assert.Equal("BD0", line.Warehouse);
            Assert.Equal(1, line.Quantity);

            Assert.Equal("GN0155GHPCAN", reservation.ItemNumber);
            Assert.Equal("XAPP007", reservation.AssetId);
            Assert.Equal("BD1", reservation.Warehouse);
            Assert.Equal(1, reservation.Quantity);
            Assert.Equal(1, reservation.EffectiveQuantity);
            Assert.Equal("GN0125GHPCAN", reservation.ActualItemNumber);
            Assert.Equal("XAPP004", reservation.ActualAssetId);
            Assert.Equal(1, reservation.ActualQuantity);
            Assert.True(reservation.IsConfirmed);
        }

        [SkippableFact]
        public async Task Agreement_Fulfilled_With_Single_Line_Moves_PartiallyFulfilled_When_New_Serialized_Line_Recieved_At_50_22()
        {
            var dbContextFactory = await ArrangeAndAct(TestData.AgreementLines.FulfilledHeaderAndLine);
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            var fulfilmentEngione = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var mockLogger = new Mock<ILogger>();

            var sut = new AgreementLineReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngione, mockLogger.Object);
            await sut.Handle(AgreementLineBOD.SyncNotDelivered);

            var header = await dbContext.Headers.SingleAsync();
            var lines = await dbContext.Lines.ToListAsync();
            var reservation = await dbContext.Reservations.SingleAsync();

            Assert.Equal((int)FulfilmentStatus.PartiallyFulfilled, header.FulfilmentStatus);
            Assert.Equal((int)FulfilmentStatus.FullyFulfiled, lines[0].FulfilmentStatus);
            Assert.Equal((int)FulfilmentStatus.Unfulfilled, lines[1].FulfilmentStatus);

            Assert.Equal("GN0155GHPCAN", reservation.ItemNumber);
            Assert.Equal("XAPP007", reservation.AssetId);
            Assert.Equal("BD1", reservation.Warehouse);
            Assert.Equal(1, reservation.Quantity);
            Assert.Equal(1, reservation.EffectiveQuantity);
            Assert.Null(reservation.ActualItemNumber);
            Assert.Null(reservation.ActualAssetId);
            Assert.Null(reservation.ActualQuantity);
        }

        [SkippableFact]
        public async Task Agreement_Fulfilled_With_Single_Line_Stays_Fulfilled_When_New_Serialized_Line_Recieved_At_50_99()
        {
            var dbContextFactory = await ArrangeAndAct(TestData.AgreementLines.FulfilledHeaderAndLine);
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            var fulfilmentEngione = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var mockLogger = new Mock<ILogger>();

            var sut = new AgreementLineReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngione, mockLogger.Object);
            await sut.Handle(AgreementLineBOD.SyncDelivered);

            var header = await dbContext.Headers.SingleAsync();
            var lines = await dbContext.Lines.ToListAsync();
            var reservations = await dbContext.Reservations.ToListAsync();

            Assert.Equal((int)FulfilmentStatus.FullyFulfiled, header.FulfilmentStatus);
            Assert.Equal((int)FulfilmentStatus.FullyFulfiled, lines[0].FulfilmentStatus);
            Assert.Equal((int)FulfilmentStatus.FullyFulfiled, lines[1].FulfilmentStatus);

            Assert.Equal("GN0155GHPCAN", reservations[0].ItemNumber);
            Assert.Equal("XAPP007", reservations[0].AssetId);
            Assert.Equal("BD1", reservations[0].Warehouse);
            Assert.Equal(1, reservations[0].Quantity);
            Assert.Equal(1, reservations[0].EffectiveQuantity);
            Assert.Null(reservations[0].ActualItemNumber);
            Assert.Null(reservations[0].ActualAssetId);
            Assert.Null(reservations[0].ActualQuantity);

            Assert.Equal("GN0125GHPCAN", reservations[1].ItemNumber);
            Assert.Equal("XAPP004", reservations[1].AssetId);
            Assert.Equal("BD0", reservations[1].Warehouse);
            Assert.Equal(1, reservations[1].Quantity);
            Assert.Equal(0, reservations[1].EffectiveQuantity);
            Assert.Equal("GN0125GHPCAN", reservations[1].ActualItemNumber);
            Assert.Equal("XAPP004", reservations[1].ActualAssetId);
            Assert.Equal(1.0, reservations[1].ActualQuantity);
            Assert.True(reservations[1].IsConfirmed);
        }

        [SkippableFact]
        public async Task Agreement_Fulfilled_With_Single_Line_Stays_Fulfilled_When_New_Non_Serialized_Line_Recieved_At_50_99()
        {
            var dbContextFactory = await ArrangeAndAct(TestData.AgreementLines.FulfilledHeaderAndLine);
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            var fulfilmentEngione = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var mockLogger = new Mock<ILogger>();

            var sut = new AgreementLineReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngione, mockLogger.Object);
            await sut.Handle(AgreementLineBOD.SyncDeliveredNonSerialized);

            var header = await dbContext.Headers.SingleAsync();
            var lines = await dbContext.Lines.ToListAsync();
            var reservations = await dbContext.Reservations.ToListAsync();

            Assert.Equal((int)FulfilmentStatus.FullyFulfiled, header.FulfilmentStatus);
            Assert.Equal((int)FulfilmentStatus.FullyFulfiled, lines[0].FulfilmentStatus);
            Assert.Equal((int)FulfilmentStatus.FullyFulfiled, lines[1].FulfilmentStatus);

            Assert.Equal("GN0155GHPCAN", reservations[0].ItemNumber);
            Assert.Equal("XAPP007", reservations[0].AssetId);
            Assert.Equal("BD1", reservations[0].Warehouse);
            Assert.Equal(1, reservations[0].Quantity);
            Assert.Equal(1, reservations[0].EffectiveQuantity);
            Assert.Null(reservations[0].ActualItemNumber);
            Assert.Null(reservations[0].ActualAssetId);
            Assert.Null(reservations[0].ActualQuantity);

            Assert.Equal("GN0125GHPCAN", reservations[1].ItemNumber);
            Assert.Equal("GN0125GHPCAN", reservations[1].AssetId);
            Assert.Equal("BD0", reservations[1].Warehouse);
            Assert.Equal(1, reservations[1].Quantity);
            Assert.Equal(0, reservations[1].EffectiveQuantity);
            Assert.Equal("GN0125GHPCAN", reservations[1].ActualItemNumber);
            Assert.Equal("GN0125GHPCAN", reservations[1].ActualAssetId);
            Assert.Equal(1.0, reservations[1].ActualQuantity);
            Assert.True(reservations[1].IsConfirmed);
        }

        [SkippableFact]
        public async Task Agreement_Fulfilled_With_Single_Line_Goes_Partially_Fulfilled_When_Quantity_Changes()
        {
            var dbContextFactory = await ArrangeAndAct(TestData.AgreementLines.FulfilledHeaderAndLineGoesToPartiallyFulfilled);
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            var fulfilmentEngione = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var mockLogger = new Mock<ILogger>();

            var sut = new AgreementLineReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngione, mockLogger.Object);
            await sut.Handle(AgreementLineBOD.SyncQuantityGoesPartiallyFulfilled);

            var header = await dbContext.Headers.SingleAsync();
            var line = await dbContext.Lines.SingleAsync();
            var reservation = await dbContext.Reservations.SingleAsync();

            Assert.Equal("XGCB0535_NA", line.ItemNumber);
            Assert.Equal("XGCB0535_NA", line.GenericItemNumber);
            Assert.Equal("BD0", line.Warehouse);
            Assert.Equal(2, line.Quantity);

            Assert.Equal("XGCB0535_NA", reservation.ItemNumber);
            Assert.Equal("XGCB0535_NA", reservation.AssetId);
            Assert.Equal("BD0", reservation.Warehouse);
            Assert.Equal(1, reservation.Quantity);
            Assert.Equal(1, reservation.EffectiveQuantity);

            Assert.Equal((int)FulfilmentStatus.PartiallyFulfilled, header.FulfilmentStatus);
            Assert.Equal((int)FulfilmentStatus.PartiallyFulfilled, line.FulfilmentStatus);
        }

        [SkippableFact]
        public async Task Agreement_UnFulfilled_With_Single_Serialized_Line_With_Quantity_Gt_1_Splits_To_Multiple_Lines()
        {
            var dbContextFactory = await ArrangeAndAct(TestData.AgreementLines.UnFulfilledHeaderAndSerializedLineSplitsWhenQuantityisGt1);
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            var fulfilmentEngione = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var mockLogger = new Mock<ILogger>();

            var sut = new AgreementLineReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngione, mockLogger.Object);
            await sut.Handle(AgreementLineBOD.SyncSerialisedQuantityGt1);

            var header = await dbContext.Headers.SingleAsync();
            var lines = await dbContext.Lines.ToListAsync();
            var reservations = await dbContext.Reservations.ToListAsync();

            Assert.Equal("T713033-1", lines[0].AgreementLineNumber);
            Assert.Equal("XGDP0100CBL", lines[0].ItemNumber);
            Assert.Equal("XGDP0100CBL", lines[0].GenericItemNumber);
            Assert.Equal("BD0", lines[0].Warehouse);
            Assert.Equal(1, lines[0].Quantity);
            Assert.Equal("8027a000007OxtQAAS", lines[1].OrderLineNumber);

            Assert.Equal("T713033-2", lines[1].AgreementLineNumber);
            Assert.Equal("XGDP0100CBL", lines[1].ItemNumber);
            Assert.Equal("XGDP0100CBL", lines[1].GenericItemNumber);
            Assert.Equal("BD0", lines[1].Warehouse);
            Assert.Equal(1, lines[1].Quantity);
            Assert.Equal("8027a000007OxtQAAS", lines[1].OrderLineNumber);

            Assert.Empty(reservations);

            Assert.Equal((int)FulfilmentStatus.Unfulfilled, header.FulfilmentStatus);
            Assert.Equal((int)FulfilmentStatus.Unfulfilled, lines[0].FulfilmentStatus);
            Assert.Equal((int)FulfilmentStatus.Unfulfilled, lines[1].FulfilmentStatus);
        }

        [SkippableFact]
        public async Task Agreement_Deletes_Line_When_Rejected_Message()
        {
            var dbContextFactory = await ArrangeAndAct(TestData.AgreementLines.UnFulfilledHeaderAndSerializedLineReadyForDelete);
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            var fulfilmentEngione = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var mockLogger = new Mock<ILogger>();

            var sut = new AgreementLineReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngione, mockLogger.Object);
            await sut.Handle(AgreementLineBOD.SyncDeleted);

            var header = await dbContext.Headers.SingleAsync();
            var lines = await dbContext.Lines.ToListAsync();
            var reservations = await dbContext.Reservations.ToListAsync();

            Assert.Empty(reservations);
            Assert.Single(lines.Where(i => i.IsDeleted).ToList());
            Assert.Equal("A712794-2", lines[1].AgreementLineNumber);

            Assert.Equal((int)FulfilmentStatus.Unfulfilled, header.FulfilmentStatus);
            Assert.Equal((int)FulfilmentStatus.Unfulfilled, lines[1].FulfilmentStatus);
        }

        [SkippableFact]
        public async Task No_Identity_Text_Does_Not_Cause_Error()
        {
            var dbContextFactory = await ArrangeAndAct(TestData.AgreementLines.InvalidExpected);
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            var fulfilmentEngione = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var mockLogger = new Mock<ILogger>();

            var sut = new AgreementLineReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngione, mockLogger.Object);
            await sut.Handle(AgreementLineBOD.Invalid);

            var lines = await dbContext.Lines.Include(x => x.Header).ToListAsync();
            var reservations = await dbContext.Reservations.ToListAsync();

            Assert.Empty(reservations);
            Assert.Single(lines.ToList());
            Assert.True(lines[0]!.Header!.IsSkeleton!.Value);
        }

        [SkippableFact]
        public async Task Agreement_Line_From_Same_Quote_Line_Gets_Reservations_Reassigned_By_Warehouse()
        {
            var dbContextFactory = await ArrangeAndAct(TestData.AgreementLines.QuoteConvertedToTAgreementWithMultipleWarehouseReservations);
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var orderItem = new OrderItem()
            {
                Id = "8027a000007OuQhAAK",
                OrderLineIndex = 2,
                QuoteLineId = "8017a000003GAerAAG",
                QuoteLine = new QuoteLine()
                {
                    LineId = 1,
                    SelectedAttributesAsText = "Shift factor:Single;Telemetry:Yes;Voltage:240V 3-phase @ 60 Hz",
                    DescriptionWithAttributes = "Shift factor:Single;Telemetry:Yes;Voltage:240V 3-phase @ 60 Hz",
                    GenericItemNumber = "XGGN0125",
                    Quote = new OrderItemQuote()
                    {
                        Name = "Q-438254"
                    }
                }
            };

            var mockOmService = new Mock<IOrderManagementIntegration>();
            mockOmService.Setup(i => i.Query<OrderItem>(It.IsAny<string>())).ReturnsAsync(new SOQLResponse<OrderItem>()
            {
                Records = new List<OrderItem>() { orderItem },
                TotalSize = 1,
                Done = true
            });
            var fulfilmentEngione = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var mockLogger = new Mock<ILogger>();

            var sut = new AgreementLineReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngione, mockLogger.Object);

            await sut.Handle(AgreementLineBOD.SyncNotDelivered);

            var lines = await dbContext.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.AgreementLineNumber).ToListAsync();
            var allReservations = await dbContext.Reservations.ToListAsync();

            Assert.Equal(2, lines.Count);

            var originalLine = lines.First(l => l.AgreementLineNumber == "T712844-1");
            var newLine = lines.First(l => l.AgreementLineNumber == "A712844-2");

            Assert.Equal("ED1", originalLine.Warehouse);
            Assert.Equal("BD0", newLine.Warehouse);

            var originalLineReservations = allReservations.Where(r => r.LineId == originalLine.Id).ToList();
            Assert.Single(originalLineReservations);
            Assert.Equal("ED1", originalLineReservations[0].Warehouse);
            Assert.Equal("XBBE234", originalLineReservations[0].AssetId);

            var newLineReservations = allReservations.Where(r => r.LineId == newLine.Id).ToList();
            Assert.Single(newLineReservations);
            Assert.Equal("BD0", newLineReservations[0].Warehouse);
            Assert.Equal("XBBE349", newLineReservations[0].AssetId);
        }

        [SkippableFact]
        public async Task Agreement_Line_Reassigns_Reservations_Even_When_Sibling_On_Different_Header()
        {
            var dbContextFactory = await ArrangeAndAct(TestData.AgreementLines.QuoteConvertedToTAgreementWithSiblingOnDifferentHeader);
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var orderItem = new OrderItem()
            {
                Id = "8027a000007OuQhAAK",
                OrderLineIndex = 2,
                QuoteLineId = "8017a000003GAerAAG",
                QuoteLine = new QuoteLine()
                {
                    LineId = 1,
                    SelectedAttributesAsText = "Shift factor:Single;Telemetry:Yes;Voltage:240V 3-phase @ 60 Hz",
                    DescriptionWithAttributes = "Shift factor:Single;Telemetry:Yes;Voltage:240V 3-phase @ 60 Hz",
                    GenericItemNumber = "XGGN0125",
                    Quote = new OrderItemQuote()
                    {
                        Name = "Q-438254"
                    }
                }
            };

            var mockOmService = new Mock<IOrderManagementIntegration>();
            mockOmService.Setup(i => i.Query<OrderItem>(It.IsAny<string>())).ReturnsAsync(new SOQLResponse<OrderItem>()
            {
                Records = new List<OrderItem>() { orderItem },
                TotalSize = 1,
                Done = true
            });
            var fulfilmentEngine = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var mockLogger = new Mock<ILogger>();

            var sut = new AgreementLineReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngine, mockLogger.Object);

            await sut.Handle(AgreementLineBOD.SyncNotDelivered);

            var lines = await dbContext.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.AgreementLineNumber).ToListAsync();
            var allReservations = await dbContext.Reservations.ToListAsync();

            Assert.Equal(2, lines.Count);

            var originalLine = lines.First(l => l.AgreementLineNumber == "T712844-1");
            var newLine = lines.First(l => l.AgreementLineNumber == "A712844-2");

            Assert.Equal("ED1", originalLine.Warehouse);
            Assert.Equal("BD0", newLine.Warehouse);

            // BD0 reservation should have moved to new line despite sibling being on a different header
            var originalLineReservations = allReservations.Where(r => r.LineId == originalLine.Id).ToList();
            Assert.Single(originalLineReservations);
            Assert.Equal("ED1", originalLineReservations[0].Warehouse);
            Assert.Equal("XBBE234", originalLineReservations[0].AssetId);

            var newLineReservations = allReservations.Where(r => r.LineId == newLine.Id).ToList();
            Assert.Single(newLineReservations);
            Assert.Equal("BD0", newLineReservations[0].Warehouse);
            Assert.Equal("XBBE349", newLineReservations[0].AssetId);
        }

        [SkippableFact]
        public async Task Agreement_Line_Does_Not_Reassign_Confirmed_Reservations_From_Sibling()
        {
            // Arrange: Setup sibling line with one confirmed and one unconfirmed reservation
            var dbContextFactory = await ArrangeAndAct(TestData.AgreementLines.SiblingWithConfirmedReservation);
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var orderItem = new OrderItem()
            {
                Id = "8027a000007OuQhAAK",
                OrderLineIndex = 2,
                QuoteLineId = "8017a000003GAerAAG",
                QuoteLine = new QuoteLine()
                {
                    LineId = 2,
                    SelectedAttributesAsText = "Shift factor:Single;Telemetry:Yes;Voltage:240V 3-phase @ 60 Hz",
                    DescriptionWithAttributes = "Shift factor:Single;Telemetry:Yes;Voltage:240V 3-phase @ 60 Hz",
                    GenericItemNumber = "XGGN0125",
                    Quote = new OrderItemQuote()
                    {
                        Name = "Q-438254"
                    }
                }
            };

            var mockOmService = new Mock<IOrderManagementIntegration>();
            mockOmService.Setup(i => i.Query<OrderItem>(It.IsAny<string>())).ReturnsAsync(new SOQLResponse<OrderItem>()
            {
                Records = new List<OrderItem>() { orderItem },
                TotalSize = 1,
                Done = true
            });
            var fulfilmentEngine = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var mockLogger = new Mock<ILogger>();

            var sut = new AgreementLineReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngine, mockLogger.Object);

            // Act: Process a new line BOD that matches the sibling's QuoteLineNumber
            await sut.Handle(AgreementLineBOD.SyncNotDelivered);

            // Assert
            var lines = await dbContext.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.AgreementLineNumber).ToListAsync();
            var allReservations = await dbContext.Reservations.ToListAsync();

            Assert.Equal(2, lines.Count);

            var siblingLine = lines.First(l => l.AgreementLineNumber == "T712844-1");
            var newLine = lines.First(l => l.AgreementLineNumber == "A712844-2");

            // Confirmed reservation should NOT have moved - stays on sibling
            var siblingReservations = allReservations.Where(r => r.LineId == siblingLine.Id).ToList();
            Assert.Single(siblingReservations);
            Assert.Equal("XBBE234", siblingReservations[0].AssetId);
            Assert.True(siblingReservations[0].IsConfirmed);

            // Only the unconfirmed reservation should have moved to the new line
            var newLineReservations = allReservations.Where(r => r.LineId == newLine.Id).ToList();
            Assert.Single(newLineReservations);
            Assert.Equal("XBBE349", newLineReservations[0].AssetId);
            Assert.False(newLineReservations[0].IsConfirmed);
        }

        [SkippableFact]
        public async Task Agreement_Line_Does_Not_Reassign_Duplicate_Reservations_From_Sibling()
        {
            // Arrange: Setup target line that already has a reservation, sibling has duplicate + unique
            var dbContextFactory = await ArrangeAndAct(TestData.AgreementLines.SiblingWithDuplicateReservation);
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            var fulfilmentEngine = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var mockLogger = new Mock<ILogger>();

            var sut = new AgreementLineReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngine, mockLogger.Object);

            // Get the target line before processing
            var targetLine = await dbContext.Lines.FirstAsync(l => l.AgreementLineNumber == "A712844-2");
            var siblingLine = await dbContext.Lines.FirstAsync(l => l.AgreementLineNumber == "T712844-1");

            // Act: Process an update BOD for the existing target line (Qty 2 so there's room for the unique asset)
            await sut.Handle(AgreementLineBOD.SyncNotDeliveredQty2);

            // Assert
            var allReservations = await dbContext.Reservations.ToListAsync();

            // Target line should have 2 reservations:
            // - Original XBBE234 (was already there)
            // - XBBE999 (moved from sibling because it's unique)
            var targetLineReservations = allReservations.Where(r => r.LineId == targetLine.Id).OrderBy(r => r.AssetId).ToList();
            Assert.Equal(2, targetLineReservations.Count);
            Assert.Contains(targetLineReservations, r => r.AssetId == "XBBE234");
            Assert.Contains(targetLineReservations, r => r.AssetId == "XBBE999");

            // Sibling should still have the duplicate XBBE234 (wasn't moved because target already has it)
            var siblingReservations = allReservations.Where(r => r.LineId == siblingLine.Id).ToList();
            Assert.Single(siblingReservations);
            Assert.Equal("XBBE234", siblingReservations[0].AssetId);
        }

        [SkippableFact]
        public async Task Agreement_Line_With_Allocation_Data_Does_Not_Move_Or_Delete_Sibling_Reservations()
        {
            // Arrange: Sibling has 2 unconfirmed reservations in BD0.
            // New line arrives WITH allocation data (Item to Pick pattern).
            // The upsert should create a reservation for the specific asset WITHOUT moving/deleting siblings.
            var dbContextFactory = await ArrangeAndAct(TestData.AgreementLines.SiblingWithMultipleReservationsAndAllocationOnNewLine);
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var orderItem = new OrderItem()
            {
                Id = "8027a000007OuQhAAK",
                OrderLineIndex = 2,
                QuoteLineId = "8017a000003GAerAAG",
                QuoteLine = new QuoteLine()
                {
                    LineId = 2,
                    SelectedAttributesAsText = "Shift factor:Single;Telemetry:Yes;Voltage:240V 3-phase @ 60 Hz",
                    DescriptionWithAttributes = "Shift factor:Single;Telemetry:Yes;Voltage:240V 3-phase @ 60 Hz",
                    GenericItemNumber = "XGGN0125",
                    Quote = new OrderItemQuote()
                    {
                        Name = "Q-438254"
                    }
                }
            };

            var mockOmService = new Mock<IOrderManagementIntegration>();
            mockOmService.Setup(i => i.Query<OrderItem>(It.IsAny<string>())).ReturnsAsync(new SOQLResponse<OrderItem>()
            {
                Records = new List<OrderItem>() { orderItem },
                TotalSize = 1,
                Done = true
            });
            var fulfilmentEngine = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var mockLogger = new Mock<ILogger>();

            var sut = new AgreementLineReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngine, mockLogger.Object);

            // Act: Process a BOD with allocation data (Item to Pick:[GN0125GHPCAN][XAPP004])
            await sut.Handle(AgreementLineBOD.SyncWithAllocationData);

            // Assert
            var lines = await dbContext.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.AgreementLineNumber).ToListAsync();
            var allReservations = await dbContext.Reservations.ToListAsync();

            var siblingLine = lines.First(l => l.AgreementLineNumber == "T712844-1");
            var newLine = lines.First(l => l.AgreementLineNumber == "A712844-2");

            // Sibling's reservations should be UNTOUCHED - not moved, not deleted
            var siblingReservations = allReservations.Where(r => r.LineId == siblingLine.Id).OrderBy(r => r.AssetId).ToList();
            Assert.Equal(2, siblingReservations.Count);
            Assert.Contains(siblingReservations, r => r.AssetId == "XAPP004");
            Assert.Contains(siblingReservations, r => r.AssetId == "XAPP005");

            // New line should have exactly 1 reservation from the allocation upsert
            var newLineReservations = allReservations.Where(r => r.LineId == newLine.Id).ToList();
            Assert.Single(newLineReservations);
            Assert.Equal("XAPP004", newLineReservations[0].AssetId);
            Assert.Equal("GN0125GHPCAN", newLineReservations[0].ItemNumber);
        }
    }
}