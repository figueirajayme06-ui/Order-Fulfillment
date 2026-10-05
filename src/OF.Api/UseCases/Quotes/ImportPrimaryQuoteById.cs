using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OF.Common.Infrastructure.IPG.Orders;
using OF.Common.Infrastructure.IPG.Orders.Models.Quotes;
using OF.Common.Infrastructure.IPG.Orders.SOQL;
using OF.Common.UseCases.Quotes;
using OF.Data;

namespace OF.Api.UseCases.Quotes
{
    public class ImportPrimaryQuoteByIdRequest
    {
        public required string QuoteNumber { get; set; }
    }

    public class ImportPrimaryQuoteById
    {
        private const double probability = 0;

        private readonly ILogger logger;
        private readonly IMemoryCache? quoteIdCache;
        private readonly GetOpportunitiesHandler getOpportunities;
        private readonly MapOrderFromOpportunityHandler mapOpportunitiesToOrders;
        private readonly PersistOrdersHandler persistOrders;

        public ImportPrimaryQuoteById(IOrderManagementIntegration salesforceService, ApplicationDbContext dbContext, ILogger logger, IMemoryCache? quoteIdCache = null)
        {
            this.logger = logger;
            this.quoteIdCache = quoteIdCache;
            this.getOpportunities = new GetOpportunitiesHandler(salesforceService, dbContext, logger, quoteIdCache);
            this.mapOpportunitiesToOrders = new MapOrderFromOpportunityHandler(salesforceService, logger);
            this.persistOrders = new PersistOrdersHandler(dbContext, logger);
        }

        public async Task Handle(string body)
        {
            ImportPrimaryQuoteByIdRequest request = JsonConvert.DeserializeObject<ImportPrimaryQuoteByIdRequest>(body)!;

            logger.LogInformation($"Importing Quote By Id {request.QuoteNumber} with Probability >= ({probability}%) Opportunities");

            var soql = string.Format(OFSOQL.GetHighProbabilityQuotesById, probability, request.QuoteNumber);

            var opportunities = await getOpportunities.Handle(soql, probability);

            if (opportunities == null)
            {
                return;
            }

            List<QuoteData> orderHeaders = new List<QuoteData>();

            foreach (var opportunity in opportunities)
            {
                var order = await mapOpportunitiesToOrders.Handle(opportunity);

                if (order == null)
                {
                    continue;
                }

                orderHeaders.Add(order);
            }

            await persistOrders.Handle(orderHeaders);
        }
    }
}
