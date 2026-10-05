using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using OF.Api.UseCases.Agreements;
using OF.Common;
using OF.Common.Infrastructure.CloudSuite;
using OF.Common.Infrastructure.IPG.Orders;
using OF.Common.Infrastructure.OF;
using OF.Data;
using System.Diagnostics.CodeAnalysis;

namespace OF.Api
{
    [ExcludeFromCodeCoverage]
    public class ChangeFunctions
    {
        private readonly ICloudSuiteService cloudsuitService;
        private readonly OrderManagementService orderManagementService;
        private readonly ICoreFulfilmentEngine fulfilmentEngine;
        private readonly ApplicationDbContext dbContext;
        private readonly TimeProvider timeProvider;
        private readonly ILogger<ChangeFunctions> logger;

        public ChangeFunctions(
            ICloudSuiteService cloudsuitService, 
            OrderManagementService orderManagementService,
            ICoreFulfilmentEngine fulfilmentEngine,
            ApplicationDbContext dbContext,
            TimeProvider timeProvider,
            ILogger<ChangeFunctions> logger)
        {
            this.cloudsuitService = cloudsuitService;
            this.orderManagementService = orderManagementService;
            this.fulfilmentEngine = fulfilmentEngine;
            this.dbContext = dbContext;
            this.timeProvider = timeProvider;
            this.logger = logger;
        }

        [Function(nameof(ChangeNofity))]
        public async Task ChangeNofity(
         [ServiceBusTrigger(Constants.Queues.ChangeNofity, Connection = "ServiceBusConnection")] string message)
        {
            logger.LogInformation($"ChangeNofity handler invoked: {message}");

            var notify = new ChangeNotifyHandler(dbContext, orderManagementService, fulfilmentEngine, logger);
            await notify.Handle(message);
        }

        [Function(nameof(ChangeApproval))]
        public async Task ChangeApproval(
         [ServiceBusTrigger(Constants.Queues.ChangeApproval, Connection = "ServiceBusConnection")] string message)
        {
            logger.LogInformation($"ChangeApproval handler invoked: {message}");

            var approval = new ChangeApprovalHandler(dbContext, orderManagementService, fulfilmentEngine, logger);
            await approval.Handle(message);
        }

        [Function(nameof(ChangeComplete))]
        public async Task ChangeComplete(
         [ServiceBusTrigger(Constants.Queues.ChangeComplete, Connection = "ServiceBusConnection")] string message)
        {
            logger.LogInformation($"ChangeComplete handler invoked: {message}");

            var complete = new ChangeCompleteHandler(dbContext, orderManagementService, fulfilmentEngine, logger);
            await complete.Handle(message);
        }
    }
}
