using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using OF.Api.UseCases.Activation;
using OF.Api.UseCases.Agreements;
using OF.Api.UseCases.Quotes;
using OF.Common;
using OF.Common.Extensions;
using OF.Common.Infrastructure.CloudSuite;
using OF.Common.Infrastructure.CloudSuite.Models.Agreements.BODs;
using OF.Common.Infrastructure.IPG.Orders;
using OF.Common.Infrastructure.OF;
using OF.Common.Infrastructure.Storage;
using OF.Data;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace OF.Api
{
    [ExcludeFromCodeCoverage]
    public class AgreementMessageFunctions
    {
        private readonly ICloudSuiteService cloudSuiteService;
        private readonly IAgreementLineFetcher agreementLineFetcher;
        private readonly IOrderManagementIntegration orderIntegration;
        private readonly OrderManagementService orderManagementService;
        private readonly ICoreFulfilmentEngine fulfilmentEngine;
        private readonly ApplicationDbContext dbContext;
        private readonly IActivateHeaderQueueClient headerQueue;
        private readonly IAgreementMessageSynchronizer  agreementSyncClient;
        private readonly TimeProvider timeProvider;
        private readonly ILogger<AgreementMessageFunctions> logger;

        public AgreementMessageFunctions(
            ICloudSuiteService cloudSuiteService, 
            IAgreementLineFetcher agreementLineFetcher,
            IOrderManagementIntegration orderIntegration, 
            OrderManagementService orderManagementService,
            ICoreFulfilmentEngine fulfilmentEngine,
            ApplicationDbContext dbContext,
            IActivateHeaderQueueClient headerQueue,
            IAgreementMessageSynchronizer  agreementSync,
            TimeProvider timeProvider,
            ILogger<AgreementMessageFunctions> logger)
        {
            this.cloudSuiteService = cloudSuiteService;
            this.agreementLineFetcher = agreementLineFetcher;
            this.orderIntegration = orderIntegration;
            this.orderManagementService = orderManagementService;
            this.fulfilmentEngine = fulfilmentEngine;
            this.dbContext = dbContext;
            this.headerQueue = headerQueue;
            this.agreementSyncClient = agreementSync;
            this.timeProvider = timeProvider;
            this.logger = logger;
        }

        // Agreement Queue Processor
        // Handles messages queued by the BOD triggers below
        // and processes them in session order (FIFO) per agreement - to ensure message sequence is maintained.

        [Function(nameof(AgreementSync))]
        public async Task AgreementSync(
         [ServiceBusTrigger(queueName:Constants.Queues.AgreementSync, Connection = "ServiceBusConnection", IsSessionsEnabled = true)] ServiceBusReceivedMessage message)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            string body = message.Body?.ToString() ?? string.Empty;
            IBodHandler? bodHandler;

            string operation = message.ApplicationProperties[Constants.AgreementSync.QueueOperationKey]?.ToString() ?? "unknown";

            using var _ = logger.BeginScope(
                new
                {
                    Action = nameof(AgreementSync),
                    Operation = operation,
                    CorrelationId = message.CorrelationId
                });

            switch (operation)
            {
                case Constants.AgreementSync.Operations.OrderHeaderSync:
                    bodHandler = new AgreementReceivedHandler(orderIntegration, dbContext, fulfilmentEngine, agreementLineFetcher, logger);
                    break;

                case Constants.AgreementSync.Operations.OrderLineSync:
                    bodHandler = new AgreementLineReceivedHandler(orderIntegration, dbContext, fulfilmentEngine, logger);
                    break;

                case Constants.AgreementSync.Operations.OrderLineAcknowledgment:
                    bodHandler = new LineAcknowledgementReceivedHandler(dbContext, orderManagementService, logger);
                    break;

                case Constants.AgreementSync.Operations.OrderActivationAcknowledgment:
                    bodHandler = new ActivationAcknowledgementReceivedHandler(dbContext, logger);
                    break;

                default:
                    logger.LogWarning("AgreementQueue: Unknown operation '{Operation}'", operation);
                    return;
            }

            await bodHandler.Handle(body);

            stopwatch.Stop();

            logger.LogInformation("AgreementQueue: Processed operation '{Operation}' in {ElapsedMilliseconds} ms",
                operation,
                stopwatch.ElapsedMilliseconds);
        }

        // BOD Messages

        [Function(nameof(LineAckBOD))]
        public async Task LineAckBOD(
         [ServiceBusTrigger(Constants.Topics.OrderLineAcknowledgment, "%IPGSubscriptionName%", Connection = "IPGServiceBusConnection")] ServiceBusReceivedMessage message)
        {
            string body = message.GetBodyAsString();
            await agreementSyncClient
                .QueueMessage(
                    LineAcknowledgementReceivedHandler.GetSessionId(body),
                    body,
                    Constants.AgreementSync.Operations.OrderLineAcknowledgment,
                    message.CorrelationId);
        }

        [Function(nameof(ActivationAckBOD))]
        public async Task ActivationAckBOD(
         [ServiceBusTrigger(Constants.Topics.OrderActivationAcknowledgment, "%IPGSubscriptionName%", Connection = "IPGServiceBusConnection")] ServiceBusReceivedMessage message)
        {
            string body = message.GetBodyAsString();
            await agreementSyncClient
                .QueueMessage(
                ActivationAcknowledgementReceivedHandler.GetSessionId(body),
                body,
                Constants.AgreementSync.Operations.OrderActivationAcknowledgment,
                message.CorrelationId);
        }

        [Function(nameof(HeaderSyncBOD))]
        public async Task HeaderSyncBOD(
            [ServiceBusTrigger(Constants.Topics.OrderHeaderSync, "%IPGSubscriptionName%", Connection = "IPGServiceBusConnection")] ServiceBusReceivedMessage message)
        {
            string body = message.GetBodyAsString();
            await agreementSyncClient
                .QueueMessage(
                    AgreementReceivedHandler.GetSessionId(body),
                    body,
                    Constants.AgreementSync.Operations.OrderHeaderSync,
                    message.CorrelationId);
        }

        [Function(nameof(LineSyncBOD))]
        public async Task LineSyncBOD(
            [ServiceBusTrigger(Constants.Topics.OrderLineSync, "%IPGSubscriptionName%", Connection = "IPGServiceBusConnection")] ServiceBusReceivedMessage message)
        {
            string body = message.GetBodyAsString();
            await agreementSyncClient
                .QueueMessage(
                    AgreementLineReceivedHandler.GetSessionId(body),
                    body,
                    Constants.AgreementSync.Operations.OrderLineSync,
                    message.CorrelationId);
        }

        // OF Messages

        [Function(nameof(ActivateHeader))]
        public async Task ActivateHeader(
            [ServiceBusTrigger(Constants.Queues.ActivateHeader, Connection = "ServiceBusConnection")] ServiceBusReceivedMessage message,
            FunctionContext functionContext,
            CancellationToken shutDown)
        {
            using var _ = shutDown.Register(() => logger.LogCritical($"Host is shutting down! Cancelling [{functionContext.FunctionDefinition.Name}]: {message.Body}"));
            var request = new ActivateHeaderRequest(
                message.Body,
                message.ScheduledEnqueueTime > message.EnqueuedTime ? message.ScheduledEnqueueTime : message.EnqueuedTime,
                message.ApplicationProperties);

            logger.LogInformation($"ActivateHeader handler invoked: {request.Content}");

            var assetHandler = new ActivateHeaderHandler(dbContext, orderManagementService, headerQueue, timeProvider, logger);
            await assetHandler.Handle(request, shutDown);
        }

        [Function(nameof(ActivateAgreement))]
        public async Task ActivateAgreement(
         [ServiceBusTrigger(Constants.Queues.ActivateAgreement, Connection = "ServiceBusConnection")] string message)
        {
            logger.LogInformation($"ActivateAgreement handler invoked: {message}");

            var assetHandler = new ActivateAgreementHandler(dbContext, orderManagementService, headerQueue, fulfilmentEngine, logger);
            await assetHandler.Handle(message);
        }

        [Function(nameof(UpdateAgreementByNumber))]
        public async Task UpdateAgreementByNumber(
         [ServiceBusTrigger(Constants.Queues.UpdateByAgreement, Connection = "ServiceBusConnection")] string message)
        {
            logger.LogInformation($"UpdateAgreementByNumber handler invoked: {message}");

            var assetHandler = new UpdateByAgreementNumberHandler(cloudSuiteService, orderIntegration, dbContext, fulfilmentEngine, logger);
            await assetHandler.Handle(message);
        }

        [Function(nameof(UpsertQuoteById))]
        public async Task UpsertQuoteById(
         [ServiceBusTrigger(Constants.Queues.UpsertQuote, Connection = "ServiceBusConnection")] string message)
        {
            logger.LogInformation($"UpsertQuoteById handler invoked: {message}");

            var assetHandler = new ImportPrimaryQuoteById(orderIntegration, dbContext, logger);
            await assetHandler.Handle(message);
        }
    }
}
