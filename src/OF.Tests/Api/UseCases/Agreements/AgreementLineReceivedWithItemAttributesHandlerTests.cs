using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OF.Api.UseCases.Agreements;
using OF.Common;
using OF.Common.Infrastructure.IPG.Orders;
using OF.Common.Infrastructure.OF;
using OF.Data.Database;
using OF.Tests.Common;
using OF.Tests.Data.Test;
using OF.Tests.Data.Test.BODs.AgreementLines;
namespace OF.Tests.Api.UseCases.Agreements
{
    [Collection("DatabaseCollection")]
    public class AgreementLineReceivedWithItemAttributesHandlerTests : CommonDBTest
    {
        public AgreementLineReceivedWithItemAttributesHandlerTests(DatabaseFixture databaseFixture) : base(databaseFixture)
        {
        }

        [SkippableTheory]
        [InlineData("Item to Pick:[DB0063FXC143] [XDSC465]")]
        [InlineData("Qty: x3;Item to Pick:[DB0063FXC143] [XDSC465]")]
        [InlineData("Qty: x3;Item to Pick:[DB0063FXC143] [XDSC465];Voltage Range:Low Voltage")]
        [InlineData("Item to Pick:[DB0063FXC143] [XDSC465];Voltage Range:Low Voltage")]
        public async Task Serialized_Line_Recieved_From_With_Item_50_99_Creates_Confirmed_Reservation(string attributes)
        {
            var dbContextFactory = await ArrangeAndAct(nameof(Serialized_Line_Recieved_From_With_Item_50_99_Creates_Confirmed_Reservation), TestData.AgreementLines.OldOFHeaderNoLinesOrReservations);
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            var fulfilmentEngione = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var mockLogger = new Mock<ILogger>();

            var sut = new AgreementLineReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngione, mockLogger.Object);

            var bod = string.Format(AgreementLineBOD.SyncSerialisedORFDelivered, attributes);
            await sut.Handle(bod);

            var header = await dbContext.Headers.SingleAsync();
            var line = await dbContext.Lines.SingleAsync();
            var reservation = await dbContext.Reservations.SingleAsync();

            Assert.Equal((int)FulfilmentStatus.FullyFulfiled, header.FulfilmentStatus);
            Assert.Equal((int)FulfilmentStatus.FullyFulfiled, line.FulfilmentStatus);

            Assert.Equal("GN0125GHPCAN", line.ItemNumber);
            Assert.Equal("XGDP0100CBL", line.GenericItemNumber);
            Assert.Equal("BD0", line.Warehouse);
            Assert.Equal(1, line.Quantity);

            Assert.Equal("DB0063FXC143", reservation.ItemNumber);
            Assert.Equal("XDSC465", reservation.AssetId);
            Assert.Equal("BD0", reservation.Warehouse);
            Assert.Equal(1, reservation.Quantity);
            Assert.Equal(0, reservation.EffectiveQuantity);
            Assert.Equal("DB0063FXC143", reservation.ActualItemNumber);
            Assert.Equal("XDSC465", reservation.ActualAssetId);
            Assert.Equal(1, reservation.ActualQuantity);
            Assert.True(reservation.IsConfirmed);
            Assert.False(reservation.IsRehire);
            Assert.False(reservation.IsDepotFulfilled);
        }

        [SkippableTheory]
        [InlineData("Item to Pick:[DB0063FXC143] [XDSC465]")]
        [InlineData("Qty: x3;Item to Pick:[DB0063FXC143] [XDSC465]")]
        [InlineData("Qty: x3;Item to Pick:[DB0063FXC143] [XDSC465];Voltage Range:Low Voltage")]
        [InlineData("Item to Pick:[DB0063FXC143] [XDSC465];Voltage Range:Low Voltage")]
        public async Task Serialized_Line_Recieved_From_With_Item_NOT_At_50_99_Creates_UnConfirmed_Reservation(string attributes)
        {
            var dbContextFactory = await ArrangeAndAct(nameof(Serialized_Line_Recieved_From_With_Item_NOT_At_50_99_Creates_UnConfirmed_Reservation), TestData.AgreementLines.OldOFHeaderNoLinesOrReservations);
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            var fulfilmentEngione = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var mockLogger = new Mock<ILogger>();

            var sut = new AgreementLineReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngione, mockLogger.Object);

            var bod = string.Format(AgreementLineBOD.SyncSerialisedORFNotDelivered, attributes);
            await sut.Handle(bod);

            var header = await dbContext.Headers.SingleAsync();
            var line = await dbContext.Lines.SingleAsync();
            var reservation = await dbContext.Reservations.SingleAsync();

            Assert.Equal((int)FulfilmentStatus.FullyFulfiled, header.FulfilmentStatus);
            Assert.Equal((int)FulfilmentStatus.FullyFulfiled, line.FulfilmentStatus);

            Assert.Equal("GN0125GHPCAN", line.ItemNumber);
            Assert.Equal("XGDP0100CBL", line.GenericItemNumber);
            Assert.Equal("BD0", line.Warehouse);
            Assert.Equal(1, line.Quantity);

            Assert.Equal("DB0063FXC143", reservation.ItemNumber);
            Assert.Equal("XDSC465", reservation.AssetId);
            Assert.Equal("BD0", reservation.Warehouse);
            Assert.Equal(1, reservation.Quantity);
            Assert.Equal(0, reservation.EffectiveQuantity);
            Assert.Equal("DB0063FXC143", reservation.ActualItemNumber);
            Assert.Equal("XDSC465", reservation.ActualAssetId);
            Assert.Equal(1, reservation.ActualQuantity);
            Assert.False(reservation.IsConfirmed);
            Assert.False(reservation.IsRehire);
            Assert.False(reservation.IsDepotFulfilled);
        }

        [SkippableTheory]
        [InlineData("Item to Pick:[DB0063FXC143] x13.5", 13.5)]
        [InlineData("Qty: x3;Item to Pick:[DB0063FXC143] x13.5", 13.5)]
        [InlineData("Qty: x3;Item to Pick:[DB0063FXC143] x13.5;Voltage Range:Low Voltage", 13.5)]
        [InlineData("Qty: x3;Item to Pick:[DB0063FXC143] x13;Voltage Range:Low Voltage", 13.0)]
        [InlineData("Item to Pick:[DB0063FXC143] x13.5;Voltage Range:Low Voltage", 13.5)]
        public async Task Non_Serialized_Line_Recieved_From_With_Item_At_50_99_Updates_Confirmed_Rehire(string attributes, float quantity)
        {
            var dbContextFactory = await ArrangeAndAct(nameof(Non_Serialized_Line_Recieved_From_With_Item_At_50_99_Updates_Confirmed_Rehire), TestData.AgreementLines.OldOFHeaderSingleLineAndReservation);
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            var fulfilmentEngione = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var mockLogger = new Mock<ILogger>();

            var sut = new AgreementLineReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngione, mockLogger.Object);

            var bod = string.Format(AgreementLineBOD.SyncNonSerialisedORFDelivered, attributes);
            await sut.Handle(bod);

            var header = await dbContext.Headers.SingleAsync();
            var line = await dbContext.Lines.SingleAsync();
            var reservation = await dbContext.Reservations.SingleAsync();

            Assert.Equal((int)FulfilmentStatus.FullyFulfiled, header.FulfilmentStatus);
            Assert.Equal((int)FulfilmentStatus.FullyFulfiled, line.FulfilmentStatus);

            Assert.Equal("GN0125GHPCAN", line.ItemNumber);
            Assert.Equal("XGDP0100CBL", line.GenericItemNumber);
            Assert.Equal("BD0", line.Warehouse);
            Assert.Equal(1, line.Quantity);

            Assert.Equal("XGCB0535_NA", reservation.ItemNumber);
            Assert.Equal("XGCB0535_NA", reservation.AssetId);
            Assert.Equal("BD0", reservation.Warehouse);
            Assert.Equal(1, reservation.Quantity);
            Assert.Equal(1, reservation.EffectiveQuantity);
            Assert.Equal("DB0063FXC143", reservation.ActualItemNumber);
            Assert.Equal("DB0063FXC143", reservation.ActualAssetId);
            Assert.Equal(quantity, reservation.ActualQuantity);
            Assert.True(reservation.IsConfirmed);
            Assert.True(reservation.IsRehire);
            Assert.False(reservation.IsDepotFulfilled);
        }

        [SkippableTheory]
        [InlineData("Item to Pick:[DB0063FXC143] x13.5", 13.5)]
        [InlineData("Qty: x3;Item to Pick:[DB0063FXC143] x13.5", 13.5)]
        [InlineData("Qty: x3;Item to Pick:[DB0063FXC143] x13.5;Voltage Range:Low Voltage", 13.5)]
        [InlineData("Qty: x3;Item to Pick:[DB0063FXC143] x13;Voltage Range:Low Voltage", 13.0)]
        [InlineData("Item to Pick:[DB0063FXC143] x13.5;Voltage Range:Low Voltage", 13.5)]
        public async Task Non_Serialized_Line_Recieved_From_With_Item_At_50_99_Creates_Confirmed_Reservation(string attributes, float quantity)
        {
            var dbContextFactory = await ArrangeAndAct(nameof(Non_Serialized_Line_Recieved_From_With_Item_At_50_99_Creates_Confirmed_Reservation), TestData.AgreementLines.OldOFHeaderNoLinesOrReservations);
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            var fulfilmentEngione = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var mockLogger = new Mock<ILogger>();

            var sut = new AgreementLineReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngione, mockLogger.Object);

            var bod = string.Format(AgreementLineBOD.SyncNonSerialisedORFDelivered, attributes);
            await sut.Handle(bod);

            var header = await dbContext.Headers.SingleAsync();
            var line = await dbContext.Lines.SingleAsync();
            var reservation = await dbContext.Reservations.SingleAsync();

            Assert.Equal((int)FulfilmentStatus.FullyFulfiled, header.FulfilmentStatus);
            Assert.Equal((int)FulfilmentStatus.FullyFulfiled, line.FulfilmentStatus);

            Assert.Equal("GN0125GHPCAN", line.ItemNumber);
            Assert.Equal("XGDP0100CBL", line.GenericItemNumber);
            Assert.Equal("BD0", line.Warehouse);
            Assert.Equal(1, line.Quantity);

            Assert.Equal("DB0063FXC143", reservation.ItemNumber);
            Assert.Equal("DB0063FXC143", reservation.AssetId);
            Assert.Equal("BD0", reservation.Warehouse);
            Assert.Equal((int)quantity, reservation.Quantity);
            Assert.Equal(0, reservation.EffectiveQuantity);
            Assert.Equal("DB0063FXC143", reservation.ActualItemNumber);
            Assert.Equal("DB0063FXC143", reservation.ActualAssetId);
            Assert.Equal(quantity, reservation.ActualQuantity);
            Assert.True(reservation.IsConfirmed);
            Assert.False(reservation.IsRehire);
            Assert.False(reservation.IsDepotFulfilled);
        }

        [SkippableTheory]
        [InlineData("Item to Pick:[DB0063FXC143] x13.5", 13.5)]
        [InlineData("Qty: x3;Item to Pick:[DB0063FXC143] x13.5", 13.5)]
        [InlineData("Qty: x3;Item to Pick:[DB0063FXC143] x13.5;Voltage Range:Low Voltage", 13.5)]
        [InlineData("Qty: x3;Item to Pick:[DB0063FXC143] x13;Voltage Range:Low Voltage", 13.0)]
        [InlineData("Item to Pick:[DB0063FXC143] x13.5;Voltage Range:Low Voltage", 13.5)]
        public async Task Non_Serialized_Line_Recieved_From_With_Item_NOT_At_50_99_Creates_UnConfirmed_Reservation(string attributes, float quantity)
        {
            var dbContextFactory = await ArrangeAndAct(nameof(Non_Serialized_Line_Recieved_From_With_Item_NOT_At_50_99_Creates_UnConfirmed_Reservation), TestData.AgreementLines.OldOFHeaderNoLinesOrReservations);
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            var fulfilmentEngione = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var mockLogger = new Mock<ILogger>();

            var sut = new AgreementLineReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngione, mockLogger.Object);

            var bod = string.Format(AgreementLineBOD.SyncNonSerialisedORFNotDelivered, attributes);
            await sut.Handle(bod);

            var header = await dbContext.Headers.SingleAsync();
            var line = await dbContext.Lines.SingleAsync();
            var reservation = await dbContext.Reservations.SingleAsync();

            Assert.Equal((int)FulfilmentStatus.FullyFulfiled, header.FulfilmentStatus);
            Assert.Equal((int)FulfilmentStatus.FullyFulfiled, line.FulfilmentStatus);

            Assert.Equal("GN0125GHPCAN", line.ItemNumber);
            Assert.Equal("XGDP0100CBL", line.GenericItemNumber);
            Assert.Equal("BD0", line.Warehouse);
            Assert.Equal(1, line.Quantity);

            Assert.Equal("DB0063FXC143", reservation.ItemNumber);
            Assert.Equal("DB0063FXC143", reservation.AssetId);
            Assert.Equal("BD0", reservation.Warehouse);
            Assert.Equal((int)quantity, reservation.Quantity);
            Assert.Equal(0, reservation.EffectiveQuantity);
            Assert.Equal("DB0063FXC143", reservation.ActualItemNumber);
            Assert.Equal("DB0063FXC143", reservation.ActualAssetId);
            Assert.Equal(quantity, reservation.ActualQuantity);
            Assert.False(reservation.IsConfirmed);
            Assert.False(reservation.IsRehire);
            Assert.False(reservation.IsDepotFulfilled);
        }

        [SkippableTheory]
        [InlineData("Depot fulfills from:[ED0] [Dumbarton] Qty : x12.5", 12.5)]
        [InlineData("Qty: x3;Depot fulfills from:[ED0] [Dumbarton] Qty : x12.5", 12.5)]
        [InlineData("Qty: x3;Depot fulfills from:[ED0] [Dumbarton] Qty : x12", 12.0)]
        [InlineData("Qty: x3;Depot fulfills from:[ED0] [Dumbarton] Qty : x12.5;Voltage Range:Low Voltage", 12.5)]
        [InlineData("Depot fulfills from:[ED0] [Dumbarton] Qty : x12.5;Voltage Range:Low Voltage", 12.5)]
        public async Task DepotFulfil_Line_Recieved_With_Depotfulfil_Not_At_50_99_Creates_UnConfirmed_Reservation(string attributes, float quantity)
        {
            var dbContextFactory = await ArrangeAndAct(nameof(DepotFulfil_Line_Recieved_With_Depotfulfil_Not_At_50_99_Creates_UnConfirmed_Reservation), TestData.AgreementLines.OldOFHeaderNoLinesOrReservations);
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            var fulfilmentEngione = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var mockLogger = new Mock<ILogger>();

            var sut = new AgreementLineReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngione, mockLogger.Object);

            var bod = string.Format(AgreementLineBOD.SyncNonSerialisedORFNotDelivered, attributes);
            await sut.Handle(bod);

            var header = await dbContext.Headers.SingleAsync();
            var line = await dbContext.Lines.SingleAsync();
            var reservation = await dbContext.Reservations.SingleAsync();

            Assert.Equal((int)FulfilmentStatus.FullyFulfiled, header.FulfilmentStatus);
            Assert.Equal((int)FulfilmentStatus.FullyFulfiled, line.FulfilmentStatus);

            Assert.Equal("GN0125GHPCAN", line.ItemNumber);
            Assert.Equal("XGDP0100CBL", line.GenericItemNumber);
            Assert.Equal("BD0", line.Warehouse);
            Assert.Equal(1, line.Quantity);

            Assert.Equal(Constants.IPG.DepotFulfil, reservation.ItemNumber);
            Assert.Equal(Constants.IPG.DepotFulfil, reservation.AssetId);
            Assert.Equal("ED0", reservation.Warehouse);
            Assert.Equal((int)quantity, reservation.Quantity);
            Assert.Equal(0, reservation.EffectiveQuantity);
            Assert.Equal("GN0125GHPCAN", reservation.ActualItemNumber);
            Assert.Equal("GN0125GHPCAN", reservation.ActualAssetId);
            Assert.Equal(quantity, reservation.ActualQuantity);
            Assert.False(reservation.IsConfirmed);
            Assert.False(reservation.IsRehire);
            Assert.True(reservation.IsDepotFulfilled);
        }

        [SkippableTheory]
        [InlineData("Depot fulfills from:[ED0] [Dumbarton] Qty : x12.5", 12.5)]
        [InlineData("Qty: x3;Depot fulfills from:[ED0] [Dumbarton] Qty : x12.5", 12.5)]
        [InlineData("Qty: x3;Depot fulfills from:[ED0] [Dumbarton] Qty : x12", 12.0)]
        [InlineData("Qty: x3;Depot fulfills from:[ED0] [Dumbarton] Qty : x12.5;Voltage Range:Low Voltage", 12.5)]
        [InlineData("Depot fulfills from:[ED0] [Dumbarton] Qty : x12.5;Voltage Range:Low Voltage", 12.5)]
        public async Task DepotFulfil_Line_Recieved_With_Depotfulfil_At_50_99_Creates_Confirmed_Reservation(string attributes, float quantity)
        {
            var dbContextFactory = await ArrangeAndAct(nameof(DepotFulfil_Line_Recieved_With_Depotfulfil_At_50_99_Creates_Confirmed_Reservation), TestData.AgreementLines.OldOFHeaderNoLinesOrReservations);
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            var fulfilmentEngione = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var mockLogger = new Mock<ILogger>();

            var sut = new AgreementLineReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngione, mockLogger.Object);

            var bod = string.Format(AgreementLineBOD.SyncNonSerialisedORFDelivered, attributes);
            await sut.Handle(bod);

            var header = await dbContext.Headers.SingleAsync();
            var line = await dbContext.Lines.SingleAsync();
            var reservation = await dbContext.Reservations.SingleAsync();

            Assert.Equal((int)FulfilmentStatus.FullyFulfiled, header.FulfilmentStatus);
            Assert.Equal((int)FulfilmentStatus.FullyFulfiled, line.FulfilmentStatus);

            Assert.Equal("GN0125GHPCAN", line.ItemNumber);
            Assert.Equal("XGDP0100CBL", line.GenericItemNumber);
            Assert.Equal("BD0", line.Warehouse);
            Assert.Equal(1, line.Quantity);

            Assert.Equal(Constants.IPG.DepotFulfil, reservation.ItemNumber);
            Assert.Equal(Constants.IPG.DepotFulfil, reservation.AssetId);
            Assert.Equal("ED0", reservation.Warehouse);
            Assert.Equal((int)quantity, reservation.Quantity);
            Assert.Equal(0, reservation.EffectiveQuantity);
            Assert.Equal("GN0125GHPCAN", reservation.ActualItemNumber);
            Assert.Equal("GN0125GHPCAN", reservation.ActualAssetId);
            Assert.Equal(quantity, reservation.ActualQuantity);
            Assert.True(reservation.IsConfirmed);
            Assert.False(reservation.IsRehire);
            Assert.True(reservation.IsDepotFulfilled);
        }

        [SkippableTheory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("Some unrelated input text")]
        [InlineData("Another unrelated text")]
        [InlineData("Yet another unrelated text")]
        public async Task No_Matched_Text_In_Attributes_Delivered_Recieved_Create_Reservation(string attributes)
        {
            var dbContextFactory = await ArrangeAndAct(nameof(No_Matched_Text_In_Attributes_Delivered_Recieved_Create_Reservation), TestData.AgreementLines.OldOFHeaderNoLinesOrReservations);
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            var fulfilmentEngione = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var mockLogger = new Mock<ILogger>();

            var sut = new AgreementLineReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngione, mockLogger.Object);

            var bod = string.Format(AgreementLineBOD.SyncNonSerialisedORFDelivered, attributes);
            await sut.Handle(bod);

            var header = await dbContext.Headers.SingleAsync();
            var line = await dbContext.Lines.SingleAsync();
            var reservations = await dbContext.Reservations.ToListAsync();

            Assert.Equal((int)FulfilmentStatus.FullyFulfiled, header.FulfilmentStatus);
            Assert.Equal((int)FulfilmentStatus.FullyFulfiled, line.FulfilmentStatus);

            Assert.Equal("GN0125GHPCAN", line.ItemNumber);
            Assert.Equal("XGDP0100CBL", line.GenericItemNumber);
            Assert.Equal("BD0", line.Warehouse);
            Assert.Equal(1, line.Quantity);

            Assert.NotEmpty(reservations);
        }

        [SkippableTheory]
        [InlineData(Constants.M3LineStatus.Terminated, Constants.M3LineDeliveryStatus.Delivered)]
        [InlineData(Constants.M3LineStatus.OnHire, Constants.M3LineDeliveryStatus.Delivered)]
        [InlineData(Constants.M3LineStatus.Invoiced, Constants.M3LineDeliveryStatus.Delivered)]
        [InlineData(Constants.M3LineStatus.Closed, Constants.M3LineDeliveryStatus.Delivered)]
        public async Task No_Matched_Text_In_Attributes_Recieved_Confirmed_Status_Create_Reservation(int status, int deliveryStatus)
        {
            var dbContextFactory = await ArrangeAndAct(nameof(No_Matched_Text_In_Attributes_Recieved_Confirmed_Status_Create_Reservation), TestData.AgreementLines.OldOFHeaderNoLinesOrReservations);
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            var fulfilmentEngione = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var mockLogger = new Mock<ILogger>();

            var sut = new AgreementLineReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngione, mockLogger.Object);

            var bod = AgreementLineBOD.SyncNonSerialisedORFDelivered;
            bod = bod.Replace("<agreementLineStatus>50</agreementLineStatus>", $"<agreementLineStatus>{status}</agreementLineStatus>");
            bod = bod.Replace("<deliveryOrderLineHighestStatus>99</deliveryOrderLineHighestStatus>", $"<deliveryOrderLineHighestStatus>{deliveryStatus}</deliveryOrderLineHighestStatus>");

            await sut.Handle(bod);

            var header = await dbContext.Headers.SingleAsync();
            var line = await dbContext.Lines.SingleAsync();
            var reservations = await dbContext.Reservations.ToListAsync();

            Assert.Equal((int)FulfilmentStatus.FullyFulfiled, header.FulfilmentStatus);
            Assert.Equal((int)FulfilmentStatus.FullyFulfiled, line.FulfilmentStatus);

            Assert.Equal("GN0125GHPCAN", line.ItemNumber);
            Assert.Equal("XGDP0100CBL", line.GenericItemNumber);
            Assert.Equal("BD0", line.Warehouse);
            Assert.Equal(1, line.Quantity);

            Assert.NotEmpty(reservations);
        }

        [SkippableTheory]
        [InlineData(Constants.M3LineStatus.Terminated, 98)]
        [InlineData(Constants.M3LineStatus.OnHire, 98)]
        [InlineData(Constants.M3LineStatus.Invoiced, 98)]
        [InlineData(Constants.M3LineStatus.Closed, 98)]
        [InlineData(20, Constants.M3LineDeliveryStatus.Delivered)]
        public async Task No_Matched_Text_In_Attributes_Recieved_UnConfirmed_Status_Create_Reservation(int status, int deliveryStatus)
        {
            var dbContextFactory = await ArrangeAndAct(nameof(No_Matched_Text_In_Attributes_Recieved_UnConfirmed_Status_Create_Reservation), TestData.AgreementLines.OldOFHeaderNoLinesOrReservations);
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            var fulfilmentEngione = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var mockLogger = new Mock<ILogger>();

            var sut = new AgreementLineReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngione, mockLogger.Object);

            var bod = AgreementLineBOD.SyncNonSerialisedORFDelivered;
            bod = bod.Replace("<agreementLineStatus>50</agreementLineStatus>", $"<agreementLineStatus>{status}</agreementLineStatus>");
            bod = bod.Replace("<deliveryOrderLineHighestStatus>99</deliveryOrderLineHighestStatus>", $"<deliveryOrderLineHighestStatus>{deliveryStatus}</deliveryOrderLineHighestStatus>");

            await sut.Handle(bod);

            var header = await dbContext.Headers.SingleAsync();
            var line = await dbContext.Lines.SingleAsync();
            var reservations = await dbContext.Reservations.ToListAsync();

            Assert.Equal((int)FulfilmentStatus.Unfulfilled, header.FulfilmentStatus);
            Assert.Equal((int)FulfilmentStatus.Unfulfilled, line.FulfilmentStatus);

            Assert.Equal("GN0125GHPCAN", line.ItemNumber);
            Assert.Equal("XGDP0100CBL", line.GenericItemNumber);
            Assert.Equal("BD0", line.Warehouse);
            Assert.Equal(1, line.Quantity);

            Assert.Empty(reservations);
        }

        [SkippableTheory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("Some unrelated input text")]
        [InlineData("Another unrelated text")]
        [InlineData("Yet another unrelated text")]
        public async Task No_Matched_Text_In_Attributes_Not_Delivered_Recieved_Not_Create_Reservation(string attributes)
        {
            var dbContextFactory = await ArrangeAndAct(nameof(No_Matched_Text_In_Attributes_Not_Delivered_Recieved_Not_Create_Reservation), TestData.AgreementLines.OldOFHeaderNoLinesOrReservations);
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            var fulfilmentEngione = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var mockLogger = new Mock<ILogger>();

            var sut = new AgreementLineReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngione, mockLogger.Object);

            var bod = string.Format(AgreementLineBOD.SyncNonSerialisedORFNotDelivered, attributes);
            await sut.Handle(bod);

            var header = await dbContext.Headers.SingleAsync();
            var line = await dbContext.Lines.SingleAsync();
            var reservations = await dbContext.Reservations.ToListAsync();

            Assert.Equal((int)FulfilmentStatus.Unfulfilled, header.FulfilmentStatus);
            Assert.Equal((int)FulfilmentStatus.Unfulfilled, line.FulfilmentStatus);

            Assert.Equal("GN0125GHPCAN", line.ItemNumber);
            Assert.Equal("XGDP0100CBL", line.GenericItemNumber);
            Assert.Equal("BD0", line.Warehouse);
            Assert.Equal(1, line.Quantity);

            Assert.Empty(reservations);
        }

        [Theory]
        [InlineData("Item to Pick:[DB0063FXC143] x13.5", "<lotNumber>TEST123</lotNumber>", "DB0063FXC143", "DB0063FXC143", false)]
        [InlineData("Item to Pick:[DB0063FXC143] x13.5", "", "DB0063FXC143", "DB0063FXC143", false)]
        [InlineData("Depot fulfills from:[ED0] [Dumbarton] Qty : x12.5", "<lotNumber>TEST123</lotNumber>", "TEST123", "GN0060GHPCAN", false)]
        [InlineData("Depot fulfills from:[ED0] [Dumbarton] Qty : x12.5", "", "DEPOTFULFIL", "DEPOTFULFIL", true)]
        public async Task Reservation_Created_With_LotNumber_If_Present_And_Is_DepotFulfil(string attributes, string lotNumberXml, string expectedAssetId, string expectedItemNumber, bool expectedIsDepotFulfilled)
        {
            var dbContextFactory = await ArrangeAndAct(nameof(Reservation_Created_With_LotNumber_If_Present_And_Is_DepotFulfil), TestData.AgreementLines.OldOFHeaderNoLinesOrReservations);
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            var fulfilmentEngione = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var mockLogger = new Mock<ILogger>();

            var sut = new AgreementLineReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngione, mockLogger.Object);

            var bod = string.Format(AgreementLineBOD.SyncDepotFulfilWithLotNumber, attributes, lotNumberXml);
            await sut.Handle(bod);

            var reservation = await dbContext.Reservations.SingleAsync();

            // Enhanced assertions to prevent regression of defect 120940
            Assert.Equal(expectedAssetId, reservation.AssetId);
            Assert.Equal(expectedItemNumber, reservation.ItemNumber);
            Assert.Equal(expectedIsDepotFulfilled, reservation.IsDepotFulfilled);
        }

        /// <summary>
        /// Test for Defect 120940: When depot fulfil reservation is updated with actual LotNumber from M3/Spartan,
        /// the IsDepotFulfilled flag should be cleared and ItemNumber should be updated
        /// </summary>
        [SkippableFact]
        public async Task Defect_120940_DepotFulfil_Updated_With_LotNumber_Clears_IsDepotFulfilled_Flag()
        {
            // Arrange - Create initial depot fulfil reservation
            var dbContextFactory = await ArrangeAndAct(nameof(Defect_120940_DepotFulfil_Updated_With_LotNumber_Clears_IsDepotFulfilled_Flag), TestData.AgreementLines.OldOFHeaderNoLinesOrReservations);
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            var fulfilmentEngine = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var mockLogger = new Mock<ILogger>();

            var sut = new AgreementLineReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngine, mockLogger.Object);

            // Step 1: Create initial depot fulfil reservation
            var depotFulfilBod = string.Format(AgreementLineBOD.SyncDepotFulfilWithLotNumber, "Depot fulfills from:[ED0] [Dumbarton] Qty : x1", "");
            await sut.Handle(depotFulfilBod);

            var initialReservation = await dbContext.Reservations.SingleAsync();
            Assert.True(initialReservation.IsDepotFulfilled);
            Assert.Equal(Constants.IPG.DepotFulfil, initialReservation.AssetId);
            Assert.Equal(Constants.IPG.DepotFulfil, initialReservation.ItemNumber);

            // Step 2: Update with actual asset (simulating M3/Spartan assigning asset 460714)
            var updatedBod = string.Format(AgreementLineBOD.SyncDepotFulfilWithLotNumber, "Depot fulfills from:[ED0] [Dumbarton] Qty : x1", "<lotNumber>460714</lotNumber>");
            await sut.Handle(updatedBod);

            // Assert - Verify the reservation was updated correctly
            var updatedReservation = await dbContext.Reservations.SingleAsync();

            // Critical assertions for defect 120940
            Assert.False(updatedReservation.IsDepotFulfilled); // Should be cleared
            Assert.Equal("460714", updatedReservation.AssetId); // Should have actual asset
            Assert.Equal("GN0060GHPCAN", updatedReservation.ItemNumber); // Should have actual item number from BOD

            // Additional verifications
            Assert.Equal("ED0", updatedReservation.Warehouse);
            Assert.Equal(1, updatedReservation.Quantity);
        }

        /// <summary>
        /// Test for Defect 120940: Verify ItemNumber is correctly updated when LotNumber is provided
        /// </summary>
        [SkippableTheory]
        [InlineData("Depot fulfills from:[ED0] [Dumbarton] Qty : x1", "460714", "GN0060GHPCAN")]
        [InlineData("Depot fulfills from:[BD0] [Glasgow] Qty : x2", "TEST123", "GN0060GHPCAN")]
        public async Task Defect_120940_DepotFulfil_With_LotNumber_Updates_ItemNumber(string depotFulfilAttributes, string lotNumber, string expectedItemNumber)
        {
            // Arrange
            var dbContextFactory = await ArrangeAndAct($"{nameof(Defect_120940_DepotFulfil_With_LotNumber_Updates_ItemNumber)}_{lotNumber}", TestData.AgreementLines.OldOFHeaderNoLinesOrReservations);
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            var fulfilmentEngine = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var mockLogger = new Mock<ILogger>();

            var sut = new AgreementLineReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngine, mockLogger.Object);

            // Act - Create depot fulfil with LotNumber in single step
            var bod = string.Format(AgreementLineBOD.SyncDepotFulfilWithLotNumber, depotFulfilAttributes, $"<lotNumber>{lotNumber}</lotNumber>");
            await sut.Handle(bod);

            // Assert
            var reservation = await dbContext.Reservations.SingleAsync();

            Assert.False(reservation.IsDepotFulfilled);
            Assert.Equal(lotNumber, reservation.AssetId);
            Assert.Equal(expectedItemNumber, reservation.ItemNumber);
        }

        /// <summary>
        /// Test for Defect 120940: Verify depot fulfil WITHOUT LotNumber keeps IsDepotFulfilled = true
        /// </summary>
        [SkippableFact]
        public async Task Defect_120940_DepotFulfil_Without_LotNumber_Keeps_IsDepotFulfilled_True()
        {
            // Arrange
            var dbContextFactory = await ArrangeAndAct(nameof(Defect_120940_DepotFulfil_Without_LotNumber_Keeps_IsDepotFulfilled_True), TestData.AgreementLines.OldOFHeaderNoLinesOrReservations);
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            var fulfilmentEngine = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var mockLogger = new Mock<ILogger>();

            var sut = new AgreementLineReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngine, mockLogger.Object);

            // Act - Create depot fulfil WITHOUT LotNumber
            var bod = string.Format(AgreementLineBOD.SyncDepotFulfilWithLotNumber, "Depot fulfills from:[ED0] [Dumbarton] Qty : x1", "");
            await sut.Handle(bod);

            // Assert - Should remain as depot fulfil
            var reservation = await dbContext.Reservations.SingleAsync();

            Assert.True(reservation.IsDepotFulfilled); // Should remain true
            Assert.Equal(Constants.IPG.DepotFulfil, reservation.AssetId);
            Assert.Equal(Constants.IPG.DepotFulfil, reservation.ItemNumber);
        }

        /// <summary>
        /// Test for Defect 120940: Verify multiple updates to same reservation handle LotNumber correctly
        /// </summary>
        [SkippableFact]
        public async Task Defect_120940_Multiple_Updates_To_Depot_Fulfil_Handle_LotNumber_Changes()
        {
            // Arrange
            var dbContextFactory = await ArrangeAndAct(nameof(Defect_120940_Multiple_Updates_To_Depot_Fulfil_Handle_LotNumber_Changes), TestData.AgreementLines.OldOFHeaderNoLinesOrReservations);
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            var fulfilmentEngine = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var mockLogger = new Mock<ILogger>();

            var sut = new AgreementLineReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngine, mockLogger.Object);

            // Step 1: Initial depot fulfil
            var bod1 = string.Format(AgreementLineBOD.SyncDepotFulfilWithLotNumber, "Depot fulfills from:[ED0] [Dumbarton] Qty : x1", "");
            await sut.Handle(bod1);

            var reservation1 = await dbContext.Reservations.SingleAsync();
            Assert.True(reservation1.IsDepotFulfilled);
            Assert.Equal(Constants.IPG.DepotFulfil, reservation1.AssetId);

            // Step 2: Update with first asset
            var bod2 = string.Format(AgreementLineBOD.SyncDepotFulfilWithLotNumber, "Depot fulfills from:[ED0] [Dumbarton] Qty : x1", "<lotNumber>ASSET001</lotNumber>");
            await sut.Handle(bod2);

            var reservation2 = await dbContext.Reservations.SingleAsync();
            Assert.False(reservation2.IsDepotFulfilled);
            Assert.Equal("ASSET001", reservation2.AssetId);
            Assert.Equal("GN0060GHPCAN", reservation2.ItemNumber);

            // Step 3: Update with different asset (asset swap scenario)
            var bod3 = string.Format(AgreementLineBOD.SyncDepotFulfilWithLotNumber, "Depot fulfills from:[ED0] [Dumbarton] Qty : x1", "<lotNumber>ASSET002</lotNumber>");
            await sut.Handle(bod3);

            var reservation3 = await dbContext.Reservations.SingleAsync();
            Assert.False(reservation3.IsDepotFulfilled); // Should still be false
            Assert.Equal("ASSET002", reservation3.AssetId); // Should have new asset
            Assert.Equal("GN0060GHPCAN", reservation3.ItemNumber);
        }


        /// <summary>
        /// Verifies M3 asset allocation updates AssetId/ItemNumber when IsDepotFulfil is false.
        /// </summary>
        [SkippableFact]
        public async Task NonDepotFulfil_With_M3_Allocated_Asset_Updates_AssetId_And_ItemNumber()
        {
            var dbContextFactory = await ArrangeAndAct(nameof(NonDepotFulfil_With_M3_Allocated_Asset_Updates_AssetId_And_ItemNumber), TestData.AgreementLines.OldOFHeaderNoLinesOrReservations);
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            var fulfilmentEngine = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var mockLogger = new Mock<ILogger>();

            var sut = new AgreementLineReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngine, mockLogger.Object);

            var bod = string.Format(AgreementLineBOD.SyncSerialisedORFDelivered, "Voltage:240V 3-phase @ 60 Hz");
            await sut.Handle(bod);

            var reservation = await dbContext.Reservations.SingleAsync();

            Assert.False(reservation.IsDepotFulfilled);
            Assert.Equal("XAPP004", reservation.AssetId);
            Assert.Equal("GN0125GHPCAN", reservation.ItemNumber);
            Assert.Equal("GN0125GHPCAN", reservation.ActualItemNumber);
            Assert.Equal("XAPP004", reservation.ActualAssetId);
            Assert.Equal("BD0", reservation.Warehouse);
            Assert.Equal(1, reservation.Quantity);
            Assert.True(reservation.IsConfirmed);
        }

        /// <summary>
        /// Regression test for UAT scenario: when a user suggests an asset in OF (e.g. YBBG012) but M3/Spartan
        /// subsequently allocates a different asset (e.g. YBBG037), the reservation must be updated to reflect
        /// M3's actual allocation. This is the m3OverriddenSuggestion condition and only applies to existing
        /// reservations - brand new reservations always use the attributes text as the source of truth.
        /// </summary>
        [SkippableFact]
        public async Task M3_Overrides_User_Suggestion_Updates_AssetId_To_M3_Allocated_Asset()
        {
            var dbContextFactory = await ArrangeAndAct(nameof(M3_Overrides_User_Suggestion_Updates_AssetId_To_M3_Allocated_Asset), TestData.AgreementLines.OldOFHeaderNoLinesOrReservations);
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            var fulfilmentEngine = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var mockLogger = new Mock<ILogger>();

            var sut = new AgreementLineReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngine, mockLogger.Object);

            // Step 1: Initial BOD — user suggested YBBG012 in OF, and M3 confirms with YBBG012 as well
            var bod1 = string.Format(AgreementLineBOD.SyncDepotFulfilWithLotNumber, "Item to Pick:[GN0060GHPCAN] [YBBG012]", "<lotNumber>YBBG012</lotNumber>");
            await sut.Handle(bod1);

            var reservation1 = await dbContext.Reservations.SingleAsync();
            Assert.False(reservation1.IsDepotFulfilled);
            Assert.Equal("YBBG012", reservation1.AssetId);
            Assert.Equal("GN0060GHPCAN", reservation1.ItemNumber);

            // Step 2: M3/Spartan re-allocates to a different asset (YBBG037) — OF attributes still show old suggestion (YBBG012)
            var bod2 = string.Format(AgreementLineBOD.SyncDepotFulfilWithLotNumber, "Item to Pick:[GN0060GHPCAN] [YBBG012]", "<lotNumber>YBBG037</lotNumber>");
            await sut.Handle(bod2);

            // Assert — reservation must be updated to M3's actual allocation
            var reservation2 = await dbContext.Reservations.SingleAsync();
            Assert.False(reservation2.IsDepotFulfilled);
            Assert.Equal("YBBG037", reservation2.AssetId);
            Assert.Equal("GN0060GHPCAN", reservation2.ItemNumber);
        }
    }
}

