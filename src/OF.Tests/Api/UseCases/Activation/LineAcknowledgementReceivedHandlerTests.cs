using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OF.Api.UseCases.Activation;
using OF.Common.Infrastructure.IPG.Orders;
using OF.Common.Infrastructure.IPG.Orders.Models.Orders;
using OF.Common.Infrastructure.OF;
using OF.Tests.Common;
using OF.Tests.Data.Test;
using OF.Tests.Data.Test.BODs.AgreementLines;
using System.Net;
using static OF.Common.Enums;

namespace OF.Tests.Api.UseCases.Activation
{
    [Collection("DatabaseCollection")]
    public class LineAcknowledgementReceivedHandlerTests : CommonDBTest
    {
        public LineAcknowledgementReceivedHandlerTests(DatabaseFixture databaseFixture) : base(databaseFixture)
        {
        }

        [SkippableFact]
        public async Task Pending_Line_Ack_Moves_To_Activated_Matching_Agreement_Line_Number_Id()
        {
            var dbContextFactory = await ArrangeAndAct(
                (dbContext) => TestData.AcknowledgeLine.SetupLineWithSubLineAndActivationAndReservations(dbContext));

            var dbContext = await dbContextFactory.CreateDbContextAsync();
            var mockOmService = new Mock<IOrderManagementIntegration>();
            var mockAppRepository = new Mock<ICoreDataRepository>();
            var mockOmLogger = new Mock<ILogger<OrderManagementService>>();
            var mockLogger = new Mock<ILogger>();

            var oms = new OrderManagementService(mockOmService.Object, mockAppRepository.Object, dbContext, mockOmLogger.Object);
            var sut = new LineAcknowledgementReceivedHandler(dbContext, oms, mockLogger.Object);

            await sut.Handle(AgreementLineBOD.Activation);

            var line = await dbContext.Lines.FirstOrDefaultAsync(x => x.AgreementLineNumber == "T712628-1");

            Assert.Equal("0427f9b0-6489-4ea8-b4fc-445cb9390447", line!.ActivationInstanceId);
            Assert.Equal((int)ActivationStatus.Activated, line!.ActivationStatus);
        }

        [SkippableFact]
        public async Task Pending_Line_Ack_Moves_To_Activated_Matching_Correlation_Id()
        {
            var dbContextFactory = await ArrangeAndAct( 
                (dbContext) => TestData.AcknowledgeLine.SetupLineWithSubLineAndActivationAndReservations(dbContext, agreementLineNumberSuffix: ".1"));

            var dbContext = await dbContextFactory.CreateDbContextAsync();
            var mockOmService = new Mock<IOrderManagementIntegration>();
            var mockAppRepository = new Mock<ICoreDataRepository>();
            var mockOmLogger = new Mock<ILogger<OrderManagementService>>();
            var mockLogger = new Mock<ILogger>();

            var oms = new OrderManagementService(mockOmService.Object, mockAppRepository.Object, dbContext, mockOmLogger.Object);
            var sut = new LineAcknowledgementReceivedHandler(dbContext, oms, mockLogger.Object);

            await sut.Handle(AgreementLineBOD.Activation);

            var line = await dbContext.Lines.FirstOrDefaultAsync(x => x.AgreementLineNumber == "T712628-1");

            Assert.Equal("0427f9b0-6489-4ea8-b4fc-445cb9390447", line!.ActivationInstanceId);
            Assert.Equal((int)ActivationStatus.Activated, line!.ActivationStatus);
        }

        [SkippableFact]
        public async Task Already_Activated_Line_Is_Ignored()
        {
            var dbContextFactory = await ArrangeAndAct(
                (dbContext) => TestData.AcknowledgeLine.SetupLineWithSubLineAndActivationAndReservations(dbContext, initialStatus: ActivationStatus.Activated));

            var dbContext = await dbContextFactory.CreateDbContextAsync();
            var mockOmService = new Mock<IOrderManagementIntegration>();
            var mockAppRepository = new Mock<ICoreDataRepository>();
            var mockOmLogger = new Mock<ILogger<OrderManagementService>>();
            var mockLogger = new Mock<ILogger>();

            var oms = new OrderManagementService(mockOmService.Object, mockAppRepository.Object, dbContext, mockOmLogger.Object);
            var sut = new LineAcknowledgementReceivedHandler(dbContext, oms, mockLogger.Object);
            await sut.Handle(AgreementLineBOD.Activation);

            mockLogger.Verify(
                logger => logger.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.StartsWith("Line has already been activated for agreement") == true),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)!),
                Times.Once);

            var line = await dbContext.Lines.FirstOrDefaultAsync(x => x.AgreementLineNumber == "T712628-1");

            Assert.Equal("0427f9b0-6489-4ea8-b4fc-445cb9390447", line!.ActivationInstanceId);
            Assert.Equal((int)ActivationStatus.Activated, line!.ActivationStatus);
        }

        [SkippableFact]
        public async Task WAD0001_Causes_Retry()
        {
            var dbContextFactory = await ArrangeAndAct(
                (dbContext) => TestData.AcknowledgeLine.SetupLineWithSubLineAndActivationAndReservations(dbContext));

            var dbContext = await dbContextFactory.CreateDbContextAsync();
            var mockOmService = new Mock<IOrderManagementIntegration>();
            var mockAppRepository = new Mock<ICoreDataRepository>();
            var mockOmLogger = new Mock<ILogger<OrderManagementService>>();
            mockOmService.Setup(x => x.OrderLineCreate(It.IsAny<OrderLineCreateRequest>())).ReturnsAsync(new HttpResponseMessage() { StatusCode = HttpStatusCode.OK });
            var mockLogger = new Mock<ILogger>();

            var oms = new OrderManagementService(mockOmService.Object, mockAppRepository.Object, dbContext, mockOmLogger.Object);
            var sut = new LineAcknowledgementReceivedHandler(dbContext, oms, mockLogger.Object);

            await sut.Handle(AgreementLineBOD.ActivationFailed_WAD0001);

            mockOmService.Verify(x => x.OrderLineCreate(It.IsAny<OrderLineCreateRequest>()), Times.Once);

            var line = await dbContext.Lines.FirstOrDefaultAsync(x => x.AgreementLineNumber == "T712628-1");

            Assert.NotEqual("0427f9b0-6489-4ea8-b4fc-445cb9390447", line!.ActivationInstanceId);
            Assert.Equal((int)ActivationStatus.Requested, line!.ActivationStatus);
        }
    }
}
