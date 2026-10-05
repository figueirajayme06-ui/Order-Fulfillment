using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using OF.Common.Infrastructure.IPG.Orders;
using OF.Common.Infrastructure.IPG.Orders.Models.Quotes;
using OF.Common.Infrastructure.IPG.Orders.SOQL;
using OF.Common.UseCases.Quotes;
using OF.Common.Utils;
using OF.Common;
using Refresh = OF.Common.Constants.DataRefresh.Quotes;

namespace OF.Data.Quotes.UseCases
{
    public class ImportPrimaryQuotes
    {
        private const double probability = 90;
        private readonly ApplicationDbContext dbContext;
        private readonly ILogger<ImportPrimaryQuotes> logger;
        private readonly IMemoryCache quoteIdCache;

        private readonly GetOpportunitiesHandler getOpportunities;
        private readonly MapOrderFromOpportunityHandler mapOpportunitiesToOrders;
        private readonly PersistOrdersHandler persistOrders;
        private readonly OpportunitiesUpdateHandler opportunitiesUpdateHandler;

        public ImportPrimaryQuotes(IOrderManagementIntegration salesforceService, ApplicationDbContext dbContext, ILogger<ImportPrimaryQuotes> logger, IMemoryCache? quoteIdCache = null)
        {
            this.dbContext = dbContext;
            this.logger = logger;
            this.quoteIdCache = quoteIdCache ?? new MemoryCache(new MemoryCacheOptions());
            this.getOpportunities = new GetOpportunitiesHandler(salesforceService, dbContext, logger, quoteIdCache);
            this.mapOpportunitiesToOrders = new MapOrderFromOpportunityHandler(salesforceService, logger);
            this.persistOrders = new PersistOrdersHandler(dbContext, logger);
            this.opportunitiesUpdateHandler = new OpportunitiesUpdateHandler(dbContext, logger);
        }

        public async Task Handle()
        {
            var processId = Guid.NewGuid().ToString();

            // Single query to Salesforce that returns high probability, closed lost, and not accepted opportunities
            logger.LogInformation("Starting combined Opportunities import - ProcessId: {ProcessId}", processId);

            var soql = string.Format(OFSOQL.GetHighProbabilityQuotes, probability);
            var opportunities = await getOpportunities.Handle(soql, probability, processId);

            if (opportunities == null || opportunities.Count == 0)
            {
                logger.LogInformation("No opportunities retrieved - ProcessId: {ProcessId}", processId);
                return;
            }

            logger.LogInformation("Retrieved {Count} opportunities from Salesforce - ProcessId: {ProcessId}", opportunities.Count, processId);

            // Mark Closed Lost opportunities as deleted
            await opportunitiesUpdateHandler.MarkClosedLostOpportunitiesAsync(opportunities, processId);

            // Mark Not Accepted opportunities as deleted
            await opportunitiesUpdateHandler.MarkNotAcceptedOpportunitiesAsync(opportunities, processId);

            logger.LogInformation("Processing {Count} opportunities for import - ProcessId: {ProcessId}", opportunities.Count, processId);

            List<QuoteData> orderHeaders = new List<QuoteData>();
            int successCount = 0;
            int errorCount = 0;

            foreach (var opportunity in opportunities)
            {
                // Skip Not Accepted (already marked as deleted above)
                if (OpportunitiesUpdateHandler.IsNotAccepted(opportunity))
                {
                    continue;
                }

                // Skip Closed Lost (already marked as deleted above)
                if (OpportunitiesUpdateHandler.IsClosedLost(opportunity))
                {
                    continue;
                }

                try
                {
                    var order = await mapOpportunitiesToOrders.Handle(opportunity);

                    if (order == null)
                    {
                        logger.LogWarning("No order mapped for Opportunity {OpportunityId} - ProcessId: {ProcessId}",
                            opportunity.Id, processId);
                        continue;
                    }

                    orderHeaders.Add(order);
                    successCount++;
                }
                catch (Exception ex)
                {
                    errorCount++;
                    logger.LogError(ex, "Failed to map order for Opportunity {OpportunityId} - ProcessId: {ProcessId}",
                        opportunity.Id, processId);

                    await ProcessingErrorLogger.LogErrorAsync(
                        dbContext,
                        logger,
                        "QuoteSync",
                        processId,
                        opportunity.Id,
                        "Opportunity",
                        ex);
                }
            }

            logger.LogInformation("Mapping completed: {SuccessCount} successful, {ErrorCount} failed - ProcessId: {ProcessId}",
                successCount, errorCount, processId);

            try
            {
                await persistOrders.Handle(orderHeaders, processId);
                await dbContext.DataRefreshStamp(Refresh.Key, Refresh.Description);
                await dbContext.SaveChangesAsync();

                logger.LogInformation("Quote sync completed successfully - ProcessId: {ProcessId}, Final stats: {SuccessCount} successful, {ErrorCount} failed",
                    processId, successCount, errorCount);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to persist orders or update refresh stamp - ProcessId: {ProcessId}", processId);

                await ProcessingErrorLogger.LogErrorAsync(
                    dbContext,
                    logger,
                    "QuoteSync",
                    processId,
                    "Batch",
                    "OrderPersistence",
                    ex);

                throw;
            }
        }
    }
}
