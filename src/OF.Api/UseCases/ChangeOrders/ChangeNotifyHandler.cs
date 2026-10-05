using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OF.Common.Infrastructure.IPG.Orders;
using OF.Common.Infrastructure.OF;
using OF.Data;

namespace OF.Api.UseCases.Agreements
{
    public class ChangeNotifyRequest
    {
        public int HeaderId { get; set; }
    }

    public class ChangeNotifyHandler
    {
        private readonly ApplicationDbContext dbContext;
        private readonly OrderManagementService orderIntegration;
        private readonly ICoreFulfilmentEngine engine;
        private readonly ILogger logger;

        public ChangeNotifyHandler(
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
            ChangeNotifyRequest request = JsonConvert.DeserializeObject<ChangeNotifyRequest>(body)!;

            return Task.CompletedTask;
        }

    }
}
