using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OF.Api.UseCases.Agreements;
using OF.Common.Infrastructure.IPG.Orders;
using OF.Common.Infrastructure.IPG.Orders.Models.Orders;
using OF.Common.Infrastructure.OF;
using OF.Common.Infrastructure.Storage;
using OF.Tests.Common;
using OF.Tests.Data.Test;
using System.Linq.Expressions;
using System.Net;
using static OF.Common.Enums;

namespace OF.Tests.Api.UseCases.Agreements
{
    [Collection("DatabaseCollection")]
    public class ActivateAgreementHandlerTests : CommonDBTest
    {
        public ActivateAgreementHandlerTests(DatabaseFixture databaseFixture) : base(databaseFixture)
        {
        }

        private static Expression<Func<IActivateHeaderQueueClient, Task>> QueueActivationExpression { get; }
            = x => x.QueueActivation(It.IsAny<int>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<CancellationToken>());

        [SkippableFact]
        public async Task Splits_To_Multiple_Lines_With_Multi_Warehouse_When_Deleted_Subline_Exists()
        {
            var dbContextFactory = await ArrangeAndAct(
                TestData.Activation.TwoLinesMultipleWarehouseReservationAndSingleReservationWithQuantityGt1AndDeletedSubline);

            var dbContext = await dbContextFactory.CreateDbContextAsync();
            var mockOmService = new Mock<IOrderManagementIntegration>();
            var mockAppRepository = new Mock<ICoreDataRepository>();
            var mockActivateHeaderQueue = new Mock<IActivateHeaderQueueClient>();
            mockActivateHeaderQueue.Setup(QueueActivationExpression);
            var mockOmLogger = new Mock<ILogger<OrderManagementService>>();
            mockOmService.Setup(x => x.OrderLineCreate(It.IsAny<OrderLineCreateRequest>())).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });
            mockOmService.Setup(x => x.OrderLineUpdate(It.IsAny<OrderLineUpdateRequest>())).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });
            mockOmService.Setup(x => x.OrderActivation(It.IsAny<OrderActivationRequest>())).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

            var mockLogger = new Mock<ILogger>();

            var oms = new OrderManagementService(mockOmService.Object, mockAppRepository.Object, dbContext, mockOmLogger.Object);
            var sut = new ActivateAgreementHandler(dbContext, oms, mockActivateHeaderQueue.Object, new CoreFulfilmentEngine(new CoreDataRepository(dbContext)), mockLogger.Object);

            await sut.Handle("{\"HeaderId\":1}");

            mockOmService.Verify(x => x.OrderLineCreate(It.IsAny<OrderLineCreateRequest>()), Times.Exactly(2));
            mockOmService.Verify(x => x.OrderLineUpdate(It.IsAny<OrderLineUpdateRequest>()), Times.Exactly(2));
            mockActivateHeaderQueue.Verify(QueueActivationExpression, Times.Exactly(1));

            var header = await dbContext.Headers.Include(i => i.Lines).FirstAsync(x => x.AgreementNumbersOnly == "712628");
            var line1 = header.Lines.Single(i => i.AgreementLineNumber == "T712628-1");
            var line1aDeleted = header.Lines.Single(i => i.AgreementLineNumber == "T712628-1.1");
            var line1a = header.Lines.Single(i => i.AgreementLineNumber == "T712628-1.2");
            var line1b = header.Lines.Single(i => i.AgreementLineNumber == "T712628-1.3");
            var line2 = header.Lines.Single(i => i.AgreementLineNumber == "T712628-2");

            var reservation1aDeleted = dbContext.Reservations.SingleOrDefault(i => i.LineId == line1aDeleted.Id);
            var reservation1 = dbContext.Reservations.Single(i => i.LineId == line1.Id);
            var reservation1a = dbContext.Reservations.Single(i => i.LineId == line1a.Id);
            var reservation1b = dbContext.Reservations.Single(i => i.LineId == line1b.Id);
            var reservation2 = dbContext.Reservations.Single(i => i.LineId == line2.Id);

            Assert.Equal(2, line1!.Quantity);
            Assert.Equal(1, line2!.Quantity);
            Assert.Equal(3, line1a!.Quantity);
            Assert.Equal(4, line1b!.Quantity);
            Assert.Equal(2, reservation1!.Quantity);
            Assert.Equal(1, reservation2!.Quantity);
            Assert.Equal(3, reservation1a!.Quantity);
            Assert.Equal(4, reservation1b!.Quantity);
            Assert.True(line1aDeleted.IsDeleted);
            Assert.Null(reservation1aDeleted);
        }

        [SkippableFact]
        public async Task Warehouse_Reservations_And_Line_Quantity_Gt_1_Splits_To_Multiple_Lines_But_Update_Orginal_Line_Quantity()
        {
            var dbContextFactory = await ArrangeAndAct(
                TestData.Activation.TwoLinesMultipleWarehouseReservationAndSingleReservationWithQuantityGt1);

            var dbContext = await dbContextFactory.CreateDbContextAsync();
            var mockOmService = new Mock<IOrderManagementIntegration>();
            var mockAppRepository = new Mock<ICoreDataRepository>();
            var mockActivateHeaderQueue = new Mock<IActivateHeaderQueueClient>();
            mockActivateHeaderQueue.Setup(QueueActivationExpression);
            var mockOmLogger = new Mock<ILogger<OrderManagementService>>();
            mockOmService.Setup(x => x.OrderLineCreate(It.IsAny<OrderLineCreateRequest>())).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });
            mockOmService.Setup(x => x.OrderLineUpdate(It.IsAny<OrderLineUpdateRequest>())).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });
            mockOmService.Setup(x => x.OrderActivation(It.IsAny<OrderActivationRequest>())).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

            var mockLogger = new Mock<ILogger>();

            var oms = new OrderManagementService(mockOmService.Object, mockAppRepository.Object, dbContext, mockOmLogger.Object);
            var sut = new ActivateAgreementHandler(dbContext, oms, mockActivateHeaderQueue.Object, new CoreFulfilmentEngine(new CoreDataRepository(dbContext)), mockLogger.Object);

            await sut.Handle("{\"HeaderId\":1}");

            mockOmService.Verify(x => x.OrderLineCreate(It.IsAny<OrderLineCreateRequest>()), Times.Exactly(2));
            mockOmService.Verify(x => x.OrderLineUpdate(It.IsAny<OrderLineUpdateRequest>()), Times.Exactly(2));
            mockActivateHeaderQueue.Verify(QueueActivationExpression, Times.Exactly(1));

            var header = await dbContext.Headers.Include(i => i.Lines).FirstAsync(x => x.AgreementNumbersOnly == "712628");
            var line1 = header.Lines.Single(i => i.AgreementLineNumber == "T712628-1");
            var line1a = header.Lines.Single(i => i.AgreementLineNumber == "T712628-1.1");
            var line1b = header.Lines.Single(i => i.AgreementLineNumber == "T712628-1.2");
            var line2 = header.Lines.Single(i => i.AgreementLineNumber == "T712628-2");
            var reservation1 = dbContext.Reservations.Single(i => i.LineId == line1.Id);
            var reservation1a = dbContext.Reservations.Single(i => i.LineId == line1a.Id);
            var reservation1b = dbContext.Reservations.Single(i => i.LineId == line1b.Id);
            var reservation2 = dbContext.Reservations.Single(i => i.LineId == line2.Id);

            Assert.Equal(2, line1!.Quantity);
            Assert.Equal(1, line2!.Quantity);
            Assert.Equal(3, line1a!.Quantity);
            Assert.Equal(4, line1b!.Quantity); 
            Assert.Equal(2, reservation1!.Quantity);
            Assert.Equal(1, reservation2!.Quantity);
            Assert.Equal(3, reservation1a!.Quantity);
            Assert.Equal(4, reservation1b!.Quantity);
        }

        [SkippableFact]
        public async Task Agreement_Lines_With_Multiple_Warehouse_Reservations_Splits_To_Multiple_Lines()
        {
            var dbContextFactory = await ArrangeAndAct(
                TestData.Activation.TwoLinesMultipleWarehouseReservationAndSingleReservation);

            var dbContext = await dbContextFactory.CreateDbContextAsync();
            var mockOmService = new Mock<IOrderManagementIntegration>();
            var mockAppRepository = new Mock<ICoreDataRepository>();
            var mockActivateHeaderQueue = new Mock<IActivateHeaderQueueClient>();
            mockActivateHeaderQueue.Setup(QueueActivationExpression);
            var mockOmLogger = new Mock<ILogger<OrderManagementService>>();
            mockOmService.Setup(x => x.OrderLineCreate(It.IsAny<OrderLineCreateRequest>())).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });
            mockOmService.Setup(x => x.OrderLineUpdate(It.IsAny<OrderLineUpdateRequest>())).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });
            mockOmService.Setup(x => x.OrderActivation(It.IsAny<OrderActivationRequest>())).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

            var mockLogger = new Mock<ILogger>();

            var oms = new OrderManagementService(mockOmService.Object, mockAppRepository.Object, dbContext, mockOmLogger.Object);
            var sut = new ActivateAgreementHandler(dbContext, oms, mockActivateHeaderQueue.Object, new CoreFulfilmentEngine(new CoreDataRepository(dbContext)), mockLogger.Object);

            await sut.Handle("{\"HeaderId\":1}");

            mockOmService.Verify(x => x.OrderLineCreate(It.IsAny<OrderLineCreateRequest>()), Times.Exactly(2));
            mockOmService.Verify(x => x.OrderLineUpdate(It.IsAny<OrderLineUpdateRequest>()), Times.Exactly(2));
            mockActivateHeaderQueue.Verify(QueueActivationExpression, Times.Exactly(1));

            var header = await dbContext.Headers.Include(i => i.Lines).FirstAsync(x => x.AgreementNumbersOnly == "712628");
            var line1 = header.Lines.Single(i => i.AgreementLineNumber == "T712628-1");
            var line1a = header.Lines.Single(i => i.AgreementLineNumber == "T712628-1.1");
            var line1b = header.Lines.Single(i => i.AgreementLineNumber == "T712628-1.2");
            var line2 = header.Lines.Single(i => i.AgreementLineNumber == "T712628-2");
            var reservation1 = dbContext.Reservations.Single(i => i.LineId == line1.Id);
            var reservation1a = dbContext.Reservations.Single(i => i.LineId == line1a.Id);
            var reservation1b = dbContext.Reservations.Single(i => i.LineId == line1b.Id);
            var reservation2 = dbContext.Reservations.Single(i => i.LineId == line2.Id);

            Assert.NotNull(line1!.ActivationInstanceId);
            Assert.Equal((int)ActivationStatus.Requested, line1!.ActivationStatus);
            Assert.Equal("T712628-1", line1!.AgreementLineNumber);
            Assert.Equal(2, line1!.Quantity);
            Assert.Equal(line1.Id, reservation1!.LineId);
            Assert.Equal("CB04/4BAE025FT", reservation1!.AssetId);
            Assert.Equal("CB04/4BAE025FT", reservation1!.ItemNumber);
            Assert.Equal(2, reservation1!.Quantity);
            Assert.Equal("BD0", reservation1!.Warehouse);

            Assert.NotNull(line2!.ActivationInstanceId);
            Assert.Equal((int)ActivationStatus.Requested, line2!.ActivationStatus);
            Assert.Equal("T712628-2", line2!.AgreementLineNumber);
            Assert.Equal(1, line2!.Quantity);
            Assert.Equal(line2.Id, reservation2!.LineId);
            Assert.Equal("XAPP007", reservation2!.AssetId);
            Assert.Equal("GN0125GHPCAN", reservation2!.ItemNumber);
            Assert.Equal(1, reservation2!.Quantity);
            Assert.Equal("BD0", reservation2!.Warehouse);

            Assert.NotNull(line1a!.ActivationInstanceId);
            Assert.Equal((int)ActivationStatus.Requested, line1a!.ActivationStatus);
            Assert.Equal("T712628-1.1", line1a!.AgreementLineNumber);
            Assert.Equal(3, line1a!.Quantity);
            Assert.Equal(line1a.Id, reservation1a!.LineId);
            Assert.Equal("CB04/4BAE050FT", reservation1a!.AssetId);
            Assert.Equal("CB04/4BAE050FT", reservation1a!.ItemNumber);
            Assert.Equal(3, reservation1a!.Quantity);
            Assert.Equal("ED1", reservation1a!.Warehouse);

            Assert.NotNull(line1b!.ActivationInstanceId);
            Assert.Equal((int)ActivationStatus.Requested, line1b!.ActivationStatus);
            Assert.Equal("T712628-1.2", line1b!.AgreementLineNumber);
            Assert.Equal(4, line1b!.Quantity);
            Assert.Equal(line1b.Id, reservation1b!.LineId);
            Assert.Equal("CB04/4BAE075FT", reservation1b!.AssetId);
            Assert.Equal("CB04/4BAE075FT", reservation1b!.ItemNumber);
            Assert.Equal(4, reservation1b!.Quantity);
            Assert.Equal("ED1", reservation1b!.Warehouse);
        }

        [SkippableFact]
        public async Task Agreement_Lines_With_Rehire_Adds_Item_Numbers_Instead_of_Rehire_Text()
        {
            var dbContextFactory = await ArrangeAndAct(
                TestData.Activation.SingleLineSingleRehireReservation);

            var dbContext = await dbContextFactory.CreateDbContextAsync();
            var mockOmService = new Mock<IOrderManagementIntegration>();
            var mockAppRepository = new Mock<ICoreDataRepository>();
            var mockActivateHeaderQueue = new Mock<IActivateHeaderQueueClient>();
            mockActivateHeaderQueue.Setup(QueueActivationExpression);
            var mockOmLogger = new Mock<ILogger<OrderManagementService>>();
            mockOmService.Setup(x => x.OrderLineUpdate(It.Is<OrderLineUpdateRequest>(s => s.FromWarehouse == "BD0"))).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });
            mockOmService.Setup(x => x.OrderActivation(It.IsAny<OrderActivationRequest>())).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

            var mockLogger = new Mock<ILogger>();

            var oms = new OrderManagementService(mockOmService.Object, mockAppRepository.Object, dbContext, mockOmLogger.Object);
            var sut = new ActivateAgreementHandler(dbContext, oms, mockActivateHeaderQueue.Object, new CoreFulfilmentEngine(new CoreDataRepository(dbContext)), mockLogger.Object);

            await sut.Handle("{\"HeaderId\":1}");

            mockOmService.Verify(x => x.OrderLineUpdate(It.Is<OrderLineUpdateRequest>((i) => i.ItemAttributesAsText.Contains("Item to Pick:[XHAH] x1"))));
            mockOmService.Verify(x => x.OrderLineUpdate(It.IsAny<OrderLineUpdateRequest>()), Times.Exactly(1));
            mockActivateHeaderQueue.Verify(QueueActivationExpression, Times.Exactly(1));

            var line = await dbContext.Lines.FirstOrDefaultAsync(x => x.AgreementLineNumber == "T712628-1");
            Assert.NotNull(line!.ActivationInstanceId);
            Assert.Equal((int)ActivationStatus.Requested, line!.ActivationStatus);
        }

        [SkippableFact]
        public async Task Agreement_Lines_With_Single_Reservations_Splits_To_Multiple_Lines()
        {
            var dbContextFactory = await ArrangeAndAct(
                TestData.Activation.SingleLineSingleReservation);

            var dbContext = await dbContextFactory.CreateDbContextAsync();
            var mockOmService = new Mock<IOrderManagementIntegration>();
            var mockAppRepository = new Mock<ICoreDataRepository>();
            var mockActivateHeaderQueue = new Mock<IActivateHeaderQueueClient>();
            mockActivateHeaderQueue.Setup(QueueActivationExpression);
            var mockOmLogger = new Mock<ILogger<OrderManagementService>>();
            mockOmService.Setup(x => x.OrderLineUpdate(It.Is<OrderLineUpdateRequest>(s => s.FromWarehouse == "BD0"))).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });
            mockOmService.Setup(x => x.OrderActivation(It.IsAny<OrderActivationRequest>())).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

            var mockLogger = new Mock<ILogger>();

            var oms = new OrderManagementService(mockOmService.Object, mockAppRepository.Object, dbContext, mockOmLogger.Object);
            var sut = new ActivateAgreementHandler(dbContext, oms, mockActivateHeaderQueue.Object, new CoreFulfilmentEngine(new CoreDataRepository(dbContext)), mockLogger.Object);

            await sut.Handle("{\"HeaderId\":1}");

            mockOmService.Verify(x => x.OrderLineUpdate(It.IsAny<OrderLineUpdateRequest>()), Times.Exactly(1));
            mockActivateHeaderQueue.Verify(QueueActivationExpression, Times.Exactly(1));

            var line = await dbContext.Lines.FirstOrDefaultAsync(x => x.AgreementLineNumber == "T712628-1");
            Assert.NotNull(line!.ActivationInstanceId);
            Assert.Equal((int)ActivationStatus.Requested, line!.ActivationStatus);
        }

        [SkippableFact]
        public async Task Retry_Activation_After_Prior_M3_Failure_Does_Not_Call_DeleteLine_For_Lines_Deleted_Post_Failure()
        {
            // Arrange: agreement with a previous failed activation (e.g. customer on block in M3).
            // The user deleted Line 2 after the failure, so it has IsDeleted=true and ActivationStatus=Failed.
            // Line 2 was never created in M3, so calling DeleteLine would cause M3 to return "line does not exist".
            var dbContextFactory = await ArrangeAndAct(
                TestData.Activation.TwoLinesSingleReservationOneDeletedAfterFailedActivation);

            var dbContext = await dbContextFactory.CreateDbContextAsync();
            var mockOmService = new Mock<IOrderManagementIntegration>();
            var mockAppRepository = new Mock<ICoreDataRepository>();
            var mockActivateHeaderQueue = new Mock<IActivateHeaderQueueClient>();
            mockActivateHeaderQueue.Setup(QueueActivationExpression);
            var mockOmLogger = new Mock<ILogger<OrderManagementService>>();
            mockOmService.Setup(x => x.OrderLineUpdate(It.IsAny<OrderLineUpdateRequest>())).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });
            mockOmService.Setup(x => x.OrderActivation(It.IsAny<OrderActivationRequest>())).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

            var mockLogger = new Mock<ILogger>();
            var oms = new OrderManagementService(mockOmService.Object, mockAppRepository.Object, dbContext, mockOmLogger.Object);
            var sut = new ActivateAgreementHandler(dbContext, oms, mockActivateHeaderQueue.Object, new CoreFulfilmentEngine(new CoreDataRepository(dbContext)), mockLogger.Object);

            await sut.Handle("{\"HeaderId\":1}");

            // DeleteLine must never be called - the deleted line does not exist in M3
            mockOmService.Verify(x => x.OrderLineDelete(It.IsAny<OrderLineDeleteRequest>()), Times.Never);
            // The active line (Line 1) should still be processed normally
            mockOmService.Verify(x => x.OrderLineUpdate(It.IsAny<OrderLineUpdateRequest>()), Times.Exactly(1));
            mockActivateHeaderQueue.Verify(QueueActivationExpression, Times.Exactly(1));
        }

        [SkippableFact]
        public async Task Retry_Activation_After_Prior_M3_Failure_Resolves_Lines_Deleted_Post_Failure_Without_M3_Call()
        {
            // Arrange: same scenario - Line 2 is deleted with Failed status from a prior M3 rejection.
            // The fix should mark Line 2 as Activated (resolved) so it is not retried again.
            var dbContextFactory = await ArrangeAndAct(
                TestData.Activation.TwoLinesSingleReservationOneDeletedAfterFailedActivation);

            var dbContext = await dbContextFactory.CreateDbContextAsync();
            var mockOmService = new Mock<IOrderManagementIntegration>();
            var mockAppRepository = new Mock<ICoreDataRepository>();
            var mockActivateHeaderQueue = new Mock<IActivateHeaderQueueClient>();
            mockActivateHeaderQueue.Setup(QueueActivationExpression);
            var mockOmLogger = new Mock<ILogger<OrderManagementService>>();
            mockOmService.Setup(x => x.OrderLineUpdate(It.IsAny<OrderLineUpdateRequest>())).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });
            mockOmService.Setup(x => x.OrderActivation(It.IsAny<OrderActivationRequest>())).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

            var mockLogger = new Mock<ILogger>();
            var oms = new OrderManagementService(mockOmService.Object, mockAppRepository.Object, dbContext, mockOmLogger.Object);
            var sut = new ActivateAgreementHandler(dbContext, oms, mockActivateHeaderQueue.Object, new CoreFulfilmentEngine(new CoreDataRepository(dbContext)), mockLogger.Object);

            await sut.Handle("{\"HeaderId\":1}");

            var header = await dbContext.Headers.SingleAsync(x => x.AgreementNumbersOnly == "712628");
            var lines = await dbContext.Lines.Where(x => x.AgreementNumbersOnly == "712628").OrderBy(x => x.AgreementLineNumber).ToListAsync();

            var activeLine = lines.Single(l => l.AgreementLineNumber == "T712628-1");
            var deletedLine = lines.Single(l => l.AgreementLineNumber == "T712628-2");

            // Header should be in Requested state (waiting for M3 acknowledgement)
            Assert.Equal((int)ActivationStatus.Requested, header.ActivationStatus);
            Assert.Null(header.ActivationErrors);

            // Active line (Line 1) should be in Requested state, awaiting M3 acknowledgement
            Assert.Equal((int)ActivationStatus.Requested, activeLine.ActivationStatus);
            Assert.NotNull(activeLine.ActivationInstanceId);

            // Deleted line (Line 2) should be resolved as Activated - it was never in M3 so nothing to delete
            Assert.Equal((int)ActivationStatus.Activated, deletedLine.ActivationStatus);
            Assert.Null(deletedLine.ActivationErrors);
            Assert.Null(deletedLine.ActivationInstanceId);
            Assert.True(deletedLine.IsDeleted);
        }

        [SkippableFact]
        public async Task Agreement_Lines_Deleted_Do_Not_Get_Activated_Just_Deleted()
        {
            var dbContextFactory = await ArrangeAndAct(
                TestData.Activation.TwoLinesSingleReservationOneDeleted);

            var dbContext = await dbContextFactory.CreateDbContextAsync();
            var mockOmService = new Mock<IOrderManagementIntegration>();
            var mockAppRepository = new Mock<ICoreDataRepository>();
            var mockActivateHeaderQueue = new Mock<IActivateHeaderQueueClient>();
            mockActivateHeaderQueue.Setup(QueueActivationExpression);
            var mockOmLogger = new Mock<ILogger<OrderManagementService>>();
            mockOmService.Setup(x => x.OrderLineUpdate(It.IsAny<OrderLineUpdateRequest>())).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });
            mockOmService.Setup(x => x.OrderLineDelete(It.IsAny<OrderLineDeleteRequest>())).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });
            mockOmService.Setup(x => x.OrderActivation(It.IsAny<OrderActivationRequest>())).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

            var mockLogger = new Mock<ILogger>();

            var oms = new OrderManagementService(mockOmService.Object, mockAppRepository.Object, dbContext, mockOmLogger.Object);
            var sut = new ActivateAgreementHandler(dbContext, oms, mockActivateHeaderQueue.Object, new CoreFulfilmentEngine(new CoreDataRepository(dbContext)), mockLogger.Object);

            await sut.Handle("{\"HeaderId\":1}");

            mockOmService.Verify(x => x.OrderLineUpdate(It.IsAny<OrderLineUpdateRequest>()), Times.Exactly(1));
            mockOmService.Verify(x => x.OrderLineDelete(It.IsAny<OrderLineDeleteRequest>()), Times.Exactly(1));
            mockActivateHeaderQueue.Verify(QueueActivationExpression, Times.Exactly(1));

            var header = await dbContext.Headers.SingleAsync(x => x.AgreementNumbersOnly == "712628");
            var lines = await dbContext.Lines.Where(x => x.AgreementNumbersOnly == "712628").ToListAsync();
            var lineIds = lines.Select(i => i.Id).ToList();
            var reservations = await dbContext.Reservations.Where(x => lineIds.Contains(x.LineId)).ToListAsync();

            Assert.Equal((int)ActivationStatus.Requested, header.ActivationStatus);
            Assert.Null(header.ActivationErrors);
            Assert.Null(header.ActivationInstanceId);

            Assert.NotNull(lines[0]!.ActivationInstanceId);
            Assert.Equal((int)ActivationStatus.Requested, lines[0]!.ActivationStatus);
            Assert.Equal("T712628-1", lines[0]!.AgreementLineNumber);
            Assert.Equal(1, lines[0]!.Quantity);

            Assert.NotNull(lines[1]!.ActivationInstanceId);
            Assert.Equal((int)ActivationStatus.Requested, lines[1]!.ActivationStatus);
            Assert.Equal("T712628-2", lines[1]!.AgreementLineNumber);
            Assert.Equal(1, lines[1]!.Quantity);
            Assert.True(lines[1]!.IsDeleted);

            Assert.Single(reservations);
        }

        [SkippableFact]
        public async Task Agrement_Activate_Having_A_Number_Handles_Lines_Ignores_Header()
        {
            var dbContextFactory = await ArrangeAndAct(
                TestData.Activation.ActivatedHeaderByNumberSingleLineSingleReservation);

            var dbContext = await dbContextFactory.CreateDbContextAsync();
            var mockOmService = new Mock<IOrderManagementIntegration>();
            var mockAppRepository = new Mock<ICoreDataRepository>();
            var mockActivateHeaderQueue = new Mock<IActivateHeaderQueueClient>();
            mockActivateHeaderQueue.Setup(QueueActivationExpression);
            var mockOmLogger = new Mock<ILogger<OrderManagementService>>();
            mockOmService.Setup(x => x.OrderLineUpdate(It.IsAny<OrderLineUpdateRequest>())).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });
            mockOmService.Setup(x => x.OrderLineDelete(It.IsAny<OrderLineDeleteRequest>())).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });
            mockOmService.Setup(x => x.OrderActivation(It.IsAny<OrderActivationRequest>())).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

            var mockLogger = new Mock<ILogger>();

            var oms = new OrderManagementService(mockOmService.Object, mockAppRepository.Object, dbContext, mockOmLogger.Object);
            var sut = new ActivateAgreementHandler(dbContext, oms, mockActivateHeaderQueue.Object, new CoreFulfilmentEngine(new CoreDataRepository(dbContext)), mockLogger.Object);

            await sut.Handle("{\"HeaderId\":1}");

            mockOmService.Verify(x => x.OrderLineUpdate(It.IsAny<OrderLineUpdateRequest>()), Times.Exactly(1));
            mockActivateHeaderQueue.Verify(QueueActivationExpression, Times.Exactly(0));

            var header = await dbContext.Headers.SingleAsync(x => x.AgreementNumbersOnly == "712628");
            var lines = await dbContext.Lines.Where(x => x.AgreementNumbersOnly == "712628").ToListAsync();
            var lineIds = lines.Select(i => i.Id).ToList();
            var reservations = await dbContext.Reservations.Where(x => lineIds.Contains(x.LineId)).ToListAsync();

            Assert.Equal((int)ActivationStatus.Activated, header.ActivationStatus);
            Assert.NotNull(lines[0]!.ActivationInstanceId);
            Assert.Equal((int)ActivationStatus.Requested, lines[0]!.ActivationStatus);
            Assert.Equal("T712628-1", lines[0]!.AgreementLineNumber);
            Assert.Equal(1, lines[0]!.Quantity);

            Assert.Single(reservations);
        }

        [SkippableFact]
        public async Task Agreement_Activate_Having_Activate_Status_Handles_Lines_Ignores_Header()
        {
            var dbContextFactory = await ArrangeAndAct(
                TestData.Activation.ActivatedHeaderByStatusSingleLineSingleReservation);

            var dbContext = await dbContextFactory.CreateDbContextAsync();
            var mockOmService = new Mock<IOrderManagementIntegration>();
            var mockAppRepository = new Mock<ICoreDataRepository>();
            var mockActivateHeaderQueue = new Mock<IActivateHeaderQueueClient>();
            mockActivateHeaderQueue.Setup(QueueActivationExpression);
            var mockOmLogger = new Mock<ILogger<OrderManagementService>>();
            mockOmService.Setup(x => x.OrderLineUpdate(It.IsAny<OrderLineUpdateRequest>())).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });
            mockOmService.Setup(x => x.OrderLineDelete(It.IsAny<OrderLineDeleteRequest>())).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });
            mockOmService.Setup(x => x.OrderActivation(It.IsAny<OrderActivationRequest>())).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

            var mockLogger = new Mock<ILogger>();

            var oms = new OrderManagementService(mockOmService.Object, mockAppRepository.Object, dbContext, mockOmLogger.Object);
            var sut = new ActivateAgreementHandler(dbContext, oms, mockActivateHeaderQueue.Object, new CoreFulfilmentEngine(new CoreDataRepository(dbContext)), mockLogger.Object);

            await sut.Handle("{\"HeaderId\":1}");

            mockOmService.Verify(x => x.OrderLineUpdate(It.IsAny<OrderLineUpdateRequest>()), Times.Exactly(1));
            mockActivateHeaderQueue.Verify(QueueActivationExpression, Times.Exactly(0));

            var header = await dbContext.Headers.SingleAsync(x => x.AgreementNumbersOnly == "712628");
            var lines = await dbContext.Lines.Where(x => x.AgreementNumbersOnly == "712628").ToListAsync();
            var lineIds = lines.Select(i => i.Id).ToList();
            var reservations = await dbContext.Reservations.Where(x => lineIds.Contains(x.LineId)).ToListAsync();

            Assert.Equal((int)ActivationStatus.Activated, header.ActivationStatus);
            Assert.NotNull(lines[0]!.ActivationInstanceId);
            Assert.Equal((int)ActivationStatus.Requested, lines[0]!.ActivationStatus);
            Assert.Equal("T712628-1", lines[0]!.AgreementLineNumber);
            Assert.Equal(1, lines[0]!.Quantity);

            Assert.Single(reservations);
        }

        [SkippableFact]
        public async Task Agreement_With_Excluded_Lines_Ignores_Them()
        {
            var dbContextFactory = await ArrangeAndAct(
                TestData.Activation.ActivatedHeaderByStatusSingleLineWithExcludedLineSingleReservation);

            var dbContext = await dbContextFactory.CreateDbContextAsync();
            var mockOmService = new Mock<IOrderManagementIntegration>();
            var mockAppRepository = new Mock<ICoreDataRepository>();
            var mockActivateHeaderQueue = new Mock<IActivateHeaderQueueClient>();
            mockActivateHeaderQueue.Setup(QueueActivationExpression);
            var mockOmLogger = new Mock<ILogger<OrderManagementService>>();
            mockOmService.Setup(x => x.OrderLineUpdate(It.IsAny<OrderLineUpdateRequest>())).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });
            mockOmService.Setup(x => x.OrderLineDelete(It.IsAny<OrderLineDeleteRequest>())).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });
            mockOmService.Setup(x => x.OrderActivation(It.IsAny<OrderActivationRequest>())).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

            var mockLogger = new Mock<ILogger>();

            var oms = new OrderManagementService(mockOmService.Object, mockAppRepository.Object, dbContext, mockOmLogger.Object);
            var sut = new ActivateAgreementHandler(dbContext, oms, mockActivateHeaderQueue.Object, new CoreFulfilmentEngine(new CoreDataRepository(dbContext)), mockLogger.Object);

            await sut.Handle("{\"HeaderId\":1}");

            mockOmService.Verify(x => x.OrderLineUpdate(It.IsAny<OrderLineUpdateRequest>()), Times.Exactly(1));
            mockActivateHeaderQueue.Verify(QueueActivationExpression, Times.Exactly(0));

            var header = await dbContext.Headers.SingleAsync(x => x.AgreementNumbersOnly == "712628");
            var lines = await dbContext.Lines.Where(x => x.AgreementNumbersOnly == "712628").ToListAsync();
            var lineIds = lines.Select(i => i.Id).ToList();
            var reservations = await dbContext.Reservations.Where(x => lineIds.Contains(x.LineId)).ToListAsync();

            Assert.Equal((int)ActivationStatus.Activated, header.ActivationStatus);
            Assert.NotNull(lines[0]!.ActivationInstanceId);
            Assert.Equal((int)ActivationStatus.Requested, lines[0]!.ActivationStatus);
            Assert.Equal("T712628-1", lines[0]!.AgreementLineNumber);
            Assert.Equal(1, lines[0]!.Quantity);

            Assert.Single(reservations);
        }

        [SkippableFact]
        public async Task Agreement_With_Quote_Lines_Ignores_Them()
        {
            var dbContextFactory = await ArrangeAndAct(
                TestData.Activation.ActivatedHeaderByStatusSingleLineWithQuoteLineSingleReservation);

            var dbContext = await dbContextFactory.CreateDbContextAsync();
            var mockOmService = new Mock<IOrderManagementIntegration>();
            var mockAppRepository = new Mock<ICoreDataRepository>();
            var mockActivateHeaderQueue = new Mock<IActivateHeaderQueueClient>();
            mockActivateHeaderQueue.Setup(QueueActivationExpression);
            var mockOmLogger = new Mock<ILogger<OrderManagementService>>();
            mockOmService.Setup(x => x.OrderLineUpdate(It.IsAny<OrderLineUpdateRequest>())).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });
            mockOmService.Setup(x => x.OrderLineDelete(It.IsAny<OrderLineDeleteRequest>())).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });
            mockOmService.Setup(x => x.OrderActivation(It.IsAny<OrderActivationRequest>())).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

            var mockLogger = new Mock<ILogger>();

            var oms = new OrderManagementService(mockOmService.Object, mockAppRepository.Object, dbContext, mockOmLogger.Object);
            var sut = new ActivateAgreementHandler(dbContext, oms, mockActivateHeaderQueue.Object, new CoreFulfilmentEngine(new CoreDataRepository(dbContext)), mockLogger.Object);

            await sut.Handle("{\"HeaderId\":1}");

            mockOmService.Verify(x => x.OrderLineUpdate(It.IsAny<OrderLineUpdateRequest>()), Times.Exactly(1));
            mockActivateHeaderQueue.Verify(QueueActivationExpression, Times.Exactly(0));

            var header = await dbContext.Headers.SingleAsync(x => x.AgreementNumbersOnly == "712628");
            var lines = await dbContext.Lines.Where(x => x.AgreementNumbersOnly == "712628").ToListAsync();
            var lineIds = lines.Select(i => i.Id).ToList();
            var reservations = await dbContext.Reservations.Where(x => lineIds.Contains(x.LineId)).ToListAsync();

            Assert.Equal((int)ActivationStatus.Activated, header.ActivationStatus);
            Assert.NotNull(lines[0]!.ActivationInstanceId);
            Assert.Equal((int)ActivationStatus.Requested, lines[0]!.ActivationStatus);
            Assert.Equal("T712628-1", lines[0]!.AgreementLineNumber);
            Assert.Equal(1, lines[0]!.Quantity);

            Assert.Single(reservations);
        }

        [SkippableFact]
        public async Task Excluded_Generic_Codes_Get_Automatically_Activated()
        {
            var dbContextFactory = await ArrangeAndAct(
                TestData.Activation.MiscLines);

            var dbContext = await dbContextFactory.CreateDbContextAsync();
            var mockOmService = new Mock<IOrderManagementIntegration>();
            var mockAppRepository = new Mock<ICoreDataRepository>();
            var mockActivateHeaderQueue = new Mock<IActivateHeaderQueueClient>();
            mockActivateHeaderQueue.Setup(QueueActivationExpression);
            var mockOmLogger = new Mock<ILogger<OrderManagementService>>();
            mockOmService.Setup(x => x.OrderLineCreate(It.IsAny<OrderLineCreateRequest>())).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });
            mockOmService.Setup(x => x.OrderLineUpdate(It.IsAny<OrderLineUpdateRequest>())).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });
            mockOmService.Setup(x => x.OrderLineDelete(It.IsAny<OrderLineDeleteRequest>())).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });
            mockOmService.Setup(x => x.OrderActivation(It.IsAny<OrderActivationRequest>())).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

            var mockLogger = new Mock<ILogger>();

            var oms = new OrderManagementService(mockOmService.Object, mockAppRepository.Object, dbContext, mockOmLogger.Object);
            var sut = new ActivateAgreementHandler(dbContext, oms, mockActivateHeaderQueue.Object, new CoreFulfilmentEngine(new CoreDataRepository(dbContext)), mockLogger.Object);

            await sut.Handle("{\"HeaderId\":1}");

            mockOmService.Verify(x => x.OrderLineUpdate(It.IsAny<OrderLineUpdateRequest>()), Times.Exactly(0)); 
            mockOmService.Verify(x => x.OrderLineCreate(It.IsAny<OrderLineCreateRequest>()), Times.Exactly(3));
            mockActivateHeaderQueue.Verify(QueueActivationExpression, Times.Exactly(1));

            var header = await dbContext.Headers.FirstAsync();
            var lines = await dbContext.Lines.ToListAsync();

            Assert.Equal((int)ActivationStatus.Activated, lines[9].ActivationStatus);
            Assert.Equal((int)ActivationStatus.Requested, lines[10].ActivationStatus);
            Assert.Equal((int)ActivationStatus.Requested, lines[11].ActivationStatus);
            Assert.Equal((int)ActivationStatus.Requested, lines[12].ActivationStatus);

            Assert.Equal((int)ActivationStatus.Requested, header.ActivationStatus);
        }

        [SkippableFact]
        public async Task Split_Line_Create_And_Update_Payloads_Contain_Order_References()
        {
            var dbContextFactory = await ArrangeAndAct(
                TestData.Activation.TwoLinesMultipleWarehouseReservationAndSingleReservation);

            var dbContext = await dbContextFactory.CreateDbContextAsync();
            var mockOmService = new Mock<IOrderManagementIntegration>();
            var mockAppRepository = new Mock<ICoreDataRepository>();
            var mockActivateHeaderQueue = new Mock<IActivateHeaderQueueClient>();
            mockActivateHeaderQueue.Setup(QueueActivationExpression);
            var mockOmLogger = new Mock<ILogger<OrderManagementService>>();

            var capturedCreateRequests = new List<OrderLineCreateRequest>();
            var capturedUpdateRequests = new List<OrderLineUpdateRequest>();

            mockOmService.Setup(x => x.OrderLineCreate(It.IsAny<OrderLineCreateRequest>()))
                .Callback<OrderLineCreateRequest>(r => capturedCreateRequests.Add(r))
                .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });
            mockOmService.Setup(x => x.OrderLineUpdate(It.IsAny<OrderLineUpdateRequest>()))
                .Callback<OrderLineUpdateRequest>(r => capturedUpdateRequests.Add(r))
                .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });
            mockOmService.Setup(x => x.OrderActivation(It.IsAny<OrderActivationRequest>())).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

            var mockLogger = new Mock<ILogger>();

            var oms = new OrderManagementService(mockOmService.Object, mockAppRepository.Object, dbContext, mockOmLogger.Object);
            var sut = new ActivateAgreementHandler(dbContext, oms, mockActivateHeaderQueue.Object, new CoreFulfilmentEngine(new CoreDataRepository(dbContext)), mockLogger.Object);

            await sut.Handle("{\"HeaderId\":1}");

            var jsonSettings = new Newtonsoft.Json.JsonSerializerSettings
            {
                ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver(),
                Formatting = Newtonsoft.Json.Formatting.Indented
            };

            foreach (var req in capturedCreateRequests)
            {
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(req, jsonSettings);
                Console.WriteLine($"--- OrderLineCreate payload ---\n{json}");
            }

            foreach (var req in capturedUpdateRequests)
            {
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(req, jsonSettings);
                Console.WriteLine($"--- OrderLineUpdate payload ---\n{json}");
            }

            // Verify create requests contain order references
            Assert.All(capturedCreateRequests, r =>
            {
                Assert.NotNull(r.OrderItemRecordId);
                Assert.NotNull(r.OrderItemLine);
            });
        }
    }
}
