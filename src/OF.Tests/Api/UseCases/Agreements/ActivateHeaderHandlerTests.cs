using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OF.Api.UseCases.Agreements;
using OF.Common;
using OF.Common.Infrastructure.IPG.Orders;
using OF.Common.Infrastructure.IPG.Orders.Models.Orders;
using OF.Common.Infrastructure.OF;
using OF.Common.Infrastructure.Storage;
using OF.Data.Database;
using OF.Tests.Common;
using OF.Tests.Data.Test;
using System.Linq.Expressions;
using System.Net;
using Xunit.Abstractions;
using static OF.Common.Enums;

namespace OF.Tests.Api.UseCases.Agreements
{
    [Collection("DatabaseCollection")]
    public class ActivateHeaderHandlerTests : CommonDBTest
    {
        public ActivateHeaderHandlerTests(ITestOutputHelper testOutput, DatabaseFixture databaseFixture) : base(databaseFixture)
        {
            TestOutput = testOutput;
        }

        private ITestOutputHelper TestOutput { get; }

        private static Expression<Func<IActivateHeaderQueueClient, Task>> QueueActivationExpression { get; }
            = x => x.QueueActivation(It.IsAny<int>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<CancellationToken>());

        [SkippableFact]
        public async Task Do_Not_Activate_Header_If_Already_At_A()
        {
            var dbContextFactory = await ArrangeAndAct(
                TestData.ActivateHeader.IgnoreIfAlreadyActivatedByANumber);

            using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var mockOmService = new Mock<IOrderManagementIntegration>();
            var mockAppRepository = new Mock<ICoreDataRepository>();
            var mockActivateHeaderQueue = new Mock<IActivateHeaderQueueClient>();
            mockActivateHeaderQueue.Setup(QueueActivationExpression);
            mockOmService.Setup(x => x.OrderActivation(It.IsAny<OrderActivationRequest>())).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

            var oms = new OrderManagementService(mockOmService.Object, mockAppRepository.Object, dbContext, TestOutput.ToLogger<OrderManagementService>().Object);
            var sut = new ActivateHeaderHandler(dbContext, oms, mockActivateHeaderQueue.Object, new SystemTimeProvider(), TestOutput.ToLogger<ActivateHeaderHandler>().Object);

            await sut.Handle(new(BinaryData.FromString("{\"HeaderId\":1}"), DateTimeOffset.UtcNow, null));

            mockOmService.Verify(x => x.OrderActivation(It.IsAny<OrderActivationRequest>()), Times.Exactly(0));
            mockActivateHeaderQueue.Verify(QueueActivationExpression, Times.Exactly(0));

            var header = await dbContext.Headers.Include(i => i.Lines).SingleAsync(x => x.AgreementNumbersOnly == "712628");
            Assert.Equal((int)ActivationStatus.Activated, header!.ActivationStatus);
            Assert.Null(header!.ActivationErrors);
            Assert.Null(header!.ActivationInstanceId);
        }

        [SkippableFact]
        public async Task Do_Not_Activate_Header_If_Already_Activated()
        {
            var dbContextFactory = await ArrangeAndAct(
                TestData.ActivateHeader.IgnoreIfAlreadyActivatedByStatus);

            using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var mockOmService = new Mock<IOrderManagementIntegration>();
            var mockAppRepository = new Mock<ICoreDataRepository>();
            var mockActivateHeaderQueue = new Mock<IActivateHeaderQueueClient>();
            mockActivateHeaderQueue.Setup(QueueActivationExpression);
            mockOmService.Setup(x => x.OrderActivation(It.IsAny<OrderActivationRequest>())).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

            var oms = new OrderManagementService(mockOmService.Object, mockAppRepository.Object, dbContext, TestOutput.ToLogger<OrderManagementService>().Object);
            var sut = new ActivateHeaderHandler(dbContext, oms, mockActivateHeaderQueue.Object, new SystemTimeProvider(), TestOutput.ToLogger<ActivateHeaderHandler>().Object);

            await sut.Handle(new(BinaryData.FromString("{\"HeaderId\":1}"), DateTimeOffset.UtcNow, null));

            mockOmService.Verify(x => x.OrderActivation(It.IsAny<OrderActivationRequest>()), Times.Exactly(0));
            mockActivateHeaderQueue.Verify(QueueActivationExpression, Times.Exactly(0));

            var header = await dbContext.Headers.Include(i => i.Lines).SingleAsync(x => x.AgreementNumbersOnly == "712628");
            Assert.Equal((int)ActivationStatus.Activated, header!.ActivationStatus);
            Assert.Null(header!.ActivationErrors);
            Assert.Null(header!.ActivationInstanceId);
        }

        [SkippableFact]
        public async Task Do_Not_Activate_Header_If_Partially_Fulfilled()
        {
            var dbContextFactory = await ArrangeAndAct(
                TestData.ActivateHeader.IgnoreIfPartiallyFulfilleds);

            using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var mockOmService = new Mock<IOrderManagementIntegration>();
            var mockAppRepository = new Mock<ICoreDataRepository>();
            var mockActivateHeaderQueue = new Mock<IActivateHeaderQueueClient>();
            mockActivateHeaderQueue.Setup(QueueActivationExpression);
            mockOmService.Setup(x => x.OrderActivation(It.IsAny<OrderActivationRequest>())).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

            var oms = new OrderManagementService(mockOmService.Object, mockAppRepository.Object, dbContext, TestOutput.ToLogger<OrderManagementService>().Object);
            var sut = new ActivateHeaderHandler(dbContext, oms, mockActivateHeaderQueue.Object, new SystemTimeProvider(), TestOutput.ToLogger<ActivateHeaderHandler>().Object);

            await sut.Handle(new(BinaryData.FromString("{\"HeaderId\":1}"), DateTimeOffset.UtcNow, null));

            mockOmService.Verify(x => x.OrderActivation(It.IsAny<OrderActivationRequest>()), Times.Exactly(0));
            mockActivateHeaderQueue.Verify(QueueActivationExpression, Times.Exactly(0));

            var header = await dbContext.Headers.Include(i => i.Lines).SingleAsync(x => x.AgreementNumbersOnly == "712628");
            Assert.Equal((int)ActivationStatus.Failed, header!.ActivationStatus);
            Assert.Contains((FulfilmentStatus.PartiallyFulfilled).ToString(), header!.ActivationErrors);
            Assert.Null(header!.ActivationInstanceId);
        }

        [SkippableFact]
        public async Task Do_Not_Activate_Header_If_Not_Fulfilled()
        {
            var dbContextFactory = await ArrangeAndAct(
                TestData.ActivateHeader.IgnoreIfNotFulfilleds);

            using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var mockOmService = new Mock<IOrderManagementIntegration>();
            var mockAppRepository = new Mock<ICoreDataRepository>();
            var mockActivateHeaderQueue = new Mock<IActivateHeaderQueueClient>();
            mockActivateHeaderQueue.Setup(QueueActivationExpression);
            mockOmService.Setup(x => x.OrderActivation(It.IsAny<OrderActivationRequest>())).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

            var oms = new OrderManagementService(mockOmService.Object, mockAppRepository.Object, dbContext, TestOutput.ToLogger<OrderManagementService>().Object);
            var sut = new ActivateHeaderHandler(dbContext, oms, mockActivateHeaderQueue.Object, new SystemTimeProvider(), TestOutput.ToLogger<ActivateHeaderHandler>().Object);

            await sut.Handle(new(BinaryData.FromString("{\"HeaderId\":1}"), DateTimeOffset.UtcNow, null));

            mockOmService.Verify(x => x.OrderActivation(It.IsAny<OrderActivationRequest>()), Times.Exactly(0));
            mockActivateHeaderQueue.Verify(QueueActivationExpression, Times.Exactly(0));

            var header = await dbContext.Headers.Include(i => i.Lines).SingleAsync(x => x.AgreementNumbersOnly == "712628");
            Assert.Equal((int)ActivationStatus.Failed, header!.ActivationStatus);
            Assert.Contains((FulfilmentStatus.Unfulfilled).ToString(), header!.ActivationErrors);
            Assert.Null(header!.ActivationInstanceId);
        }

        [SkippableFact]
        public async Task Do_Not_Activate_Header_If_Any_Lines_Failed()
        {
            var dbContextFactory = await ArrangeAndAct(
                TestData.ActivateHeader.IgnoreIfWithFailedLines);

            using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var mockOmService = new Mock<IOrderManagementIntegration>();
            var mockAppRepository = new Mock<ICoreDataRepository>();
            var mockActivateHeaderQueue = new Mock<IActivateHeaderQueueClient>();
            mockActivateHeaderQueue.Setup(QueueActivationExpression);
            mockOmService.Setup(x => x.OrderActivation(It.IsAny<OrderActivationRequest>())).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

            var oms = new OrderManagementService(mockOmService.Object, mockAppRepository.Object, dbContext, TestOutput.ToLogger<OrderManagementService>().Object);
            var sut = new ActivateHeaderHandler(dbContext, oms, mockActivateHeaderQueue.Object, new SystemTimeProvider(), TestOutput.ToLogger<ActivateHeaderHandler>().Object);

            await sut.Handle(new(BinaryData.FromString("{\"HeaderId\":1}"), DateTimeOffset.UtcNow, null));

            mockOmService.Verify(x => x.OrderActivation(It.IsAny<OrderActivationRequest>()), Times.Exactly(0));
            mockActivateHeaderQueue.Verify(QueueActivationExpression, Times.Exactly(0));

            var header = await dbContext.Headers.Include(i => i.Lines).SingleAsync(x => x.AgreementNumbersOnly == "712628");
            Assert.Equal((int)ActivationStatus.Failed, header!.ActivationStatus);
            Assert.Contains("lines have failed", header!.ActivationErrors);
            Assert.Null(header!.ActivationInstanceId);
        }

        [SkippableFact]
        public async Task Do_Not_Activate_Header_If_Any_Lines_Still_To_Be_Initially_Activated()
        {
            var dbContextFactory = await ArrangeAndAct(
                TestData.ActivateHeader.IgnoreIfWithTodoLines);

            using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var mockOmService = new Mock<IOrderManagementIntegration>();
            var mockAppRepository = new Mock<ICoreDataRepository>();
            var mockActivateHeaderQueue = new Mock<IActivateHeaderQueueClient>();
            mockActivateHeaderQueue.Setup(QueueActivationExpression);
            mockOmService.Setup(x => x.OrderActivation(It.IsAny<OrderActivationRequest>())).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

            var oms = new OrderManagementService(mockOmService.Object, mockAppRepository.Object, dbContext, TestOutput.ToLogger<OrderManagementService>().Object);
            var sut = new ActivateHeaderHandler(dbContext, oms, mockActivateHeaderQueue.Object, new SystemTimeProvider(), TestOutput.ToLogger<ActivateHeaderHandler>().Object);

            await sut.Handle(new(BinaryData.FromString("{\"HeaderId\":1}"), DateTimeOffset.UtcNow, null));

            mockOmService.Verify(x => x.OrderActivation(It.IsAny<OrderActivationRequest>()), Times.Exactly(0));
            mockActivateHeaderQueue.Verify(QueueActivationExpression, Times.Exactly(0));

            var header = await dbContext.Headers.Include(i => i.Lines).SingleAsync(x => x.AgreementNumbersOnly == "712628");
            Assert.Equal((int)ActivationStatus.Failed, header!.ActivationStatus);
            Assert.Contains("not yet requested", header!.ActivationErrors);
            Assert.Null(header!.ActivationInstanceId);
        }

        [SkippableTheory]
        [InlineData(0)]
        [InlineData(60)]
        [InlineData(359)]
        public async Task Retry_Under_Grace_Period_Activate_Header_If_Any_Lines_Still_Being_Requested(int delay_sec)
        {
            var dbContextFactory = await ArrangeAndAct(
                TestData.ActivateHeader.RetryIfWithLinesStillGoing);

            using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var mockOmService = new Mock<IOrderManagementIntegration>();
            var mockAppRepository = new Mock<ICoreDataRepository>();
            var mockActivateHeaderQueue = new Mock<IActivateHeaderQueueClient>();
            IDictionary<string, object>? properties = null;
            mockActivateHeaderQueue.Setup(QueueActivationExpression).Callback<int, IDictionary<string, object>, CancellationToken>((headerId, props, ct) => properties = props);
            var mockOmLogger = new Mock<ILogger<OrderManagementService>>();
            mockOmService.Setup(x => x.OrderActivation(It.IsAny<OrderActivationRequest>())).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });
            var oms = new OrderManagementService(mockOmService.Object, mockAppRepository.Object, dbContext, mockOmLogger.Object);
            var sut = new ActivateHeaderHandler(dbContext, oms, mockActivateHeaderQueue.Object, new SystemTimeProvider(), TestOutput.ToLogger<ActivateHeaderHandler>().Object);
            var sent = DateTimeOffset.UtcNow - TimeSpan.FromSeconds(delay_sec);

            await sut.Handle(new(BinaryData.FromString("{\"HeaderId\":1}"), sent, null));

            mockOmService.Verify(x => x.OrderActivation(It.IsAny<OrderActivationRequest>()), Times.Exactly(0));
            mockActivateHeaderQueue.Verify(QueueActivationExpression, Times.Exactly(1));

            var header = await dbContext.Headers.Include(i => i.Lines).SingleAsync(x => x.AgreementNumbersOnly == "712628");
            Assert.Equal((int)ActivationStatus.Requested, header!.ActivationStatus);
            Assert.Null(header!.ActivationErrors);
            Assert.Null(header!.ActivationInstanceId);
            Assert.NotNull(properties);
            Assert.Contains(KeyValuePair.Create(Constants.Message.Properties.ActivationTimestampKey, (object)sent), properties);
        }

        [SkippableTheory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public async Task Retry_After_Grace_Period_Activate_Header_If_Any_Lines_Still_Being_Requested(int attemptTimes)
        {
            var dbContextFactory = await ArrangeAndAct(
                TestData.ActivateHeader.RetryIfWithLinesStillGoing);

            using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var mockOmService = new Mock<IOrderManagementIntegration>();
            var mockAppRepository = new Mock<ICoreDataRepository>();
            var mockActivateHeaderQueue = new Mock<IActivateHeaderQueueClient>();
            IDictionary<string, object>? properties = null;
            mockActivateHeaderQueue.Setup(QueueActivationExpression).Callback<int, IDictionary<string, object>, CancellationToken>((headerId, props, ct) => properties = props);
            var mockOmLogger = new Mock<ILogger<OrderManagementService>>();
            mockOmService.Setup(x => x.OrderActivation(It.IsAny<OrderActivationRequest>())).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });
            var oms = new OrderManagementService(mockOmService.Object, mockAppRepository.Object, dbContext, mockOmLogger.Object);
            var sut = new ActivateHeaderHandler(dbContext, oms, mockActivateHeaderQueue.Object, new SystemTimeProvider(), TestOutput.ToLogger<ActivateHeaderHandler>().Object);
            var sent = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(5.01);

            await sut.Handle(new(
                BinaryData.FromString("{\"HeaderId\":1}"),
                DateTimeOffset.UtcNow,
                new Dictionary<string, object>
                {
                    { Constants.Message.Properties.ActivationTimestampKey, sent },
                    { Constants.Message.Properties.ActivationRetryCounterKey, attemptTimes },
                }));

            mockOmService.Verify(x => x.OrderActivation(It.IsAny<OrderActivationRequest>()), Times.Exactly(0));
            mockActivateHeaderQueue.Verify(QueueActivationExpression, Times.Exactly(1));

            var header = await dbContext.Headers.Include(i => i.Lines).SingleAsync(x => x.AgreementNumbersOnly == "712628");
            Assert.Equal((int)ActivationStatus.Requested, header!.ActivationStatus);
            Assert.Null(header!.ActivationErrors);
            Assert.Null(header!.ActivationInstanceId);
            Assert.NotNull(properties);
            Assert.Contains(KeyValuePair.Create(Constants.Message.Properties.ActivationTimestampKey, (object)sent), properties);
            Assert.Contains(KeyValuePair.Create(Constants.Message.Properties.ActivationRetryCounterKey, (object)(attemptTimes + 1)), properties);
        }

        [SkippableFact]
        public async Task Error_After_Grace_Period_And_Max_Retries_If_Any_Lines_Still_Being_Requested()
        {
            var dbContextFactory = await ArrangeAndAct(
                TestData.ActivateHeader.RetryIfWithLinesStillGoing);

            using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var mockOmService = new Mock<IOrderManagementIntegration>();
            var mockAppRepository = new Mock<ICoreDataRepository>();
            var mockActivateHeaderQueue = new Mock<IActivateHeaderQueueClient>();
            mockActivateHeaderQueue.Setup(QueueActivationExpression);
            var mockOmLogger = new Mock<ILogger<OrderManagementService>>();
            mockOmService.Setup(x => x.OrderActivation(It.IsAny<OrderActivationRequest>())).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });
            var requestedLines = await dbContext
                .Lines
                .Where(x => x.ActivationStatus == (int)ActivationStatus.Requested && x.AgreementNumbersOnly == "712628")
                .ToListAsync();
            var oms = new OrderManagementService(mockOmService.Object, mockAppRepository.Object, dbContext, mockOmLogger.Object);
            var sut = new ActivateHeaderHandler(dbContext, oms, mockActivateHeaderQueue.Object, new SystemTimeProvider(), TestOutput.ToLogger<ActivateHeaderHandler>().Object);
            var sent = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(5.01);

            await sut.Handle(new(
                BinaryData.FromString("{\"HeaderId\":1}"),
                DateTimeOffset.UtcNow,
                new Dictionary<string, object>
                {
                    { Constants.Message.Properties.ActivationTimestampKey, sent },
                    { Constants.Message.Properties.ActivationRetryCounterKey, 3 },
                }));

            mockOmService.Verify(x => x.OrderActivation(It.IsAny<OrderActivationRequest>()), Times.Exactly(0));
            mockActivateHeaderQueue.Verify(QueueActivationExpression, Times.Exactly(0));

            var header = await dbContext.Headers.Include(i => i.Lines).SingleAsync(x => x.AgreementNumbersOnly == "712628");
            Assert.Equal((int)ActivationStatus.Failed, header!.ActivationStatus);
            Assert.Contains("Activation failed: unable to activate line", header!.ActivationErrors);
            Assert.All(requestedLines, line =>
            {
                Assert.Equal((int)ActivationStatus.Failed, line!.ActivationStatus);
                Assert.Contains("Activation failed: activation wasn't done in time!", line!.ActivationErrors);
            });
            Assert.Null(header!.ActivationInstanceId);
        }

        [SkippableFact]
        public async Task Activate_Header_If_All_Criteria_Met()
        {
            var dbContextFactory = await ArrangeAndAct(
                TestData.ActivateHeader.ActivateWhenAllGood);

            using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var mockOmService = new Mock<IOrderManagementIntegration>();
            var mockAppRepository = new Mock<ICoreDataRepository>();
            var mockActivateHeaderQueue = new Mock<IActivateHeaderQueueClient>();
            mockActivateHeaderQueue.Setup(QueueActivationExpression);
            mockOmService.Setup(x => x.OrderActivation(It.IsAny<OrderActivationRequest>())).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

            var oms = new OrderManagementService(mockOmService.Object, mockAppRepository.Object, dbContext, TestOutput.ToLogger<OrderManagementService>().Object);
            var sut = new ActivateHeaderHandler(dbContext, oms, mockActivateHeaderQueue.Object, new SystemTimeProvider(), TestOutput.ToLogger<ActivateHeaderHandler>().Object);

            await sut.Handle(new(BinaryData.FromString("{\"HeaderId\":1}"), DateTimeOffset.UtcNow, null));

            mockOmService.Verify(x => x.OrderActivation(It.IsAny<OrderActivationRequest>()), Times.Exactly(1));
            mockActivateHeaderQueue.Verify(QueueActivationExpression, Times.Exactly(0));

            var header = await dbContext.Headers.Include(i => i.Lines).SingleAsync(x => x.AgreementNumbersOnly == "712628");
            Assert.Equal((int)ActivationStatus.Requested, header!.ActivationStatus);
            Assert.Null(header!.ActivationErrors);
            Assert.NotNull(header!.ActivationInstanceId);
        }

        [SkippableFact]
        public async Task Activate_Header_With_Deleted_Line_If_All_Criteria_Met()
        {
            var dbContextFactory = await ArrangeAndAct(
                TestData.ActivateHeader.ActivateWhenAllGoodWithDeletedItem);

            using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var mockOmService = new Mock<IOrderManagementIntegration>();
            var mockAppRepository = new Mock<ICoreDataRepository>();
            var mockActivateHeaderQueue = new Mock<IActivateHeaderQueueClient>();
            mockActivateHeaderQueue.Setup(QueueActivationExpression);
            mockOmService.Setup(x => x.OrderActivation(It.IsAny<OrderActivationRequest>())).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

            var oms = new OrderManagementService(mockOmService.Object, mockAppRepository.Object, dbContext, TestOutput.ToLogger<OrderManagementService>().Object);
            var sut = new ActivateHeaderHandler(dbContext, oms, mockActivateHeaderQueue.Object, new SystemTimeProvider(), TestOutput.ToLogger<ActivateHeaderHandler>().Object);

            await sut.Handle(new(BinaryData.FromString("{\"HeaderId\":1}"), DateTimeOffset.UtcNow, null));

            mockOmService.Verify(x => x.OrderActivation(It.IsAny<OrderActivationRequest>()), Times.Exactly(1));
            mockActivateHeaderQueue.Verify(QueueActivationExpression, Times.Exactly(0));

            var header = await dbContext.Headers.Include(i => i.Lines).SingleAsync(x => x.AgreementNumbersOnly == "712628");
            Assert.Equal((int)ActivationStatus.Requested, header!.ActivationStatus);
            Assert.Null(header!.ActivationErrors);
            Assert.NotNull(header!.ActivationInstanceId);
        }

        [SkippableFact]
        public async Task Activate_Header_With_Excluded_Line()
        {
            var dbContextFactory = await ArrangeAndAct(
                TestData.ActivateHeader.ActivateWhenAllGoodWithExcludedItem);

            using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var mockOmService = new Mock<IOrderManagementIntegration>();
            var mockAppRepository = new Mock<ICoreDataRepository>();
            var mockActivateHeaderQueue = new Mock<IActivateHeaderQueueClient>();
            mockActivateHeaderQueue.Setup(QueueActivationExpression);
            mockOmService.Setup(x => x.OrderActivation(It.IsAny<OrderActivationRequest>())).ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

            var oms = new OrderManagementService(mockOmService.Object, mockAppRepository.Object, dbContext, TestOutput.ToLogger<OrderManagementService>().Object);
            var sut = new ActivateHeaderHandler(dbContext, oms, mockActivateHeaderQueue.Object, new SystemTimeProvider(), TestOutput.ToLogger<ActivateHeaderHandler>().Object);

            await sut.Handle(new(BinaryData.FromString("{\"HeaderId\":1}"), DateTimeOffset.UtcNow, null));

            mockOmService.Verify(x => x.OrderActivation(It.IsAny<OrderActivationRequest>()), Times.Exactly(1));
            mockActivateHeaderQueue.Verify(QueueActivationExpression, Times.Exactly(0));

            var header = await dbContext.Headers.Include(i => i.Lines).SingleAsync(x => x.AgreementNumbersOnly == "712628");
            Assert.Equal((int)ActivationStatus.Requested, header!.ActivationStatus);
            Assert.Null(header!.ActivationErrors);
            Assert.NotNull(header!.ActivationInstanceId);
        }
    }
}
