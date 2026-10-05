using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OF.Api.UseCases.Activation;
using OF.Tests.Common;
using OF.Tests.Data.Test;
using OF.Tests.Data.Test.BODs.Agreements;
using static OF.Common.Enums;

namespace OF.Tests.Api.UseCases.Activation
{
    [Collection("DatabaseCollection")]
    public class ActivationAcknowledgementReceivedHandlerTests : CommonDBTest
    {
        public ActivationAcknowledgementReceivedHandlerTests(DatabaseFixture databaseFixture)
            : base(databaseFixture)
        {
        }

        [SkippableFact]
        public async Task Pending_Header_Ack_Moves_To_Activated_Matching_Agreement_Number_Id()
        {
            var dbContextFactory = await ArrangeAndAct(
                nameof(Pending_Header_Ack_Moves_To_Activated_Matching_Agreement_Number_Id), 
                (dbContext) => TestData.AcknowledgeHeader.SetupHeaderWithLineWithSubLineAndActivation(dbContext));

            var dbContext = await dbContextFactory.CreateDbContextAsync();
            var mockLogger = new Mock<ILogger>();

            var sut = new ActivationAcknowledgementReceivedHandler(dbContext, mockLogger.Object);

            await sut.Handle(AgreementBOD.Activation);

            var header = await dbContext.Headers.FirstOrDefaultAsync(x => x.AgreementNumbersOnly == "712565");

            Assert.Equal("0427f9b0-6489-4ea8-b4fc-445cb9390447", header!.ActivationInstanceId);
            Assert.Equal((int)ActivationStatus.Activated, header!.ActivationStatus);
        }

        [SkippableFact]
        public async Task Pending_Header_Ack_Moves_To_Activated_Matching_Correlation_Id()
        {
            var dbContextFactory = await ArrangeAndAct(
                nameof(Pending_Header_Ack_Moves_To_Activated_Matching_Correlation_Id), 
                (dbContext) => TestData.AcknowledgeHeader.SetupHeaderWithLineWithSubLineAndActivation(dbContext, agreementLineNumberSuffix: ".1"));

            var dbContext = await dbContextFactory.CreateDbContextAsync();
            var mockLogger = new Mock<ILogger>();

            var sut = new ActivationAcknowledgementReceivedHandler(dbContext, mockLogger.Object);

            await sut.Handle(AgreementBOD.Activation);

            var header = await dbContext.Headers.FirstOrDefaultAsync(x => x.AgreementNumbersOnly == "712565");

            Assert.Equal("0427f9b0-6489-4ea8-b4fc-445cb9390447", header!.ActivationInstanceId);
            Assert.Equal((int)ActivationStatus.Activated, header!.ActivationStatus);
        }

        [SkippableFact]
        public async Task Already_Activated_Header_Is_Ignored()
        {
            var dbContextFactory = await ArrangeAndAct(
                nameof(Already_Activated_Header_Is_Ignored),
                (dbContext) => TestData.AcknowledgeHeader.SetupHeaderWithLineWithSubLineAndActivation(dbContext, initialStatus: ActivationStatus.Activated));

            var dbContext = await dbContextFactory.CreateDbContextAsync();
            var mockLogger = new Mock<ILogger>();
            var sut = new ActivationAcknowledgementReceivedHandler(dbContext, mockLogger.Object);
            await sut.Handle(AgreementBOD.Activation);

            mockLogger.Verify(
                logger => logger.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.StartsWith("Header has already been activated for agreement") == true),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)!),
                Times.Once);

            var header = await dbContext.Headers.FirstOrDefaultAsync(x => x.AgreementNumbersOnly == "712565");

            Assert.Equal("0427f9b0-6489-4ea8-b4fc-445cb9390447", header!.ActivationInstanceId);
            Assert.Equal((int)ActivationStatus.Activated, header!.ActivationStatus);
        }
    }
}
