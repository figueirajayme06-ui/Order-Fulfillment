using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OF.Common.Infrastructure.IPG.Orders;
using OF.Common.Infrastructure.OF;
using OF.Data;

namespace OF.Api.UseCases.Agreements
{
    public class ChangeCompleteRequest
    {
        public int HeaderId { get; set; }
    }

    public class ChangeCompleteHandler
    {
        private readonly ApplicationDbContext dbContext;
        private readonly OrderManagementService orderIntegration;
        private readonly ICoreFulfilmentEngine engine;
        private readonly ILogger logger;

        public ChangeCompleteHandler(
            ApplicationDbContext dbContext, 
            OrderManagementService orderIntegration, 
            ICoreFulfilmentEngine engine,
            ILogger logger)
        {
            this.dbContext = dbContext;
            this.orderIntegration = orderIntegration;
            this.engine = engine;
            this.logger = logger;
        }

        public Task Handle(string body)
        {
            ChangeCompleteRequest request = JsonConvert.DeserializeObject<ChangeCompleteRequest>(body)!;

            return Task.CompletedTask;
        }

    }
}
