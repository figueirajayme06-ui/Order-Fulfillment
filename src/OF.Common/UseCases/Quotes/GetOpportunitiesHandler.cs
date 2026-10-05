using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OF.Common.Extensions;
using OF.Common.Infrastructure.IPG.Orders;
using OF.Common.Infrastructure.IPG.Orders.Models.Quotes;
using OF.Common.Utils;
using OF.Data;
using OF.Data.Database;
using Microsoft.Extensions.Caching.Memory;

namespace OF.Common.UseCases.Quotes
{
    public class GetOpportunitiesHandler
    {
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);
        private readonly IMemoryCache quoteIdCache;
        private readonly IOrderManagementIntegration salesforceService;
        private readonly ApplicationDbContext dbContext;
        private readonly ILogger logger;
        private readonly HireDateCorrectionHandler dateCorrectionHandler;
        private readonly GetQuoteLinesHandler getQuoteLinesHandler;
        private readonly MapOrderFromOpportunityHandler mapOpportunitiesToOrders;

        public GetOpportunitiesHandler(IOrderManagementIntegration salesforceService, ApplicationDbContext dbContext, ILogger logger, IMemoryCache? memoryCache = null)
        {
            this.salesforceService = salesforceService;
            this.dbContext = dbContext;
            this.logger = logger;
            this.quoteIdCache = memoryCache ?? new MemoryCache(new MemoryCacheOptions());
            this.dateCorrectionHandler = new HireDateCorrectionHandler(logger, dbContext, salesforceService);
            this.getQuoteLinesHandler = new GetQuoteLinesHandler(salesforceService, logger);
            this.mapOpportunitiesToOrders = new MapOrderFromOpportunityHandler(salesforceService, logger);
        }

        public async Task<IList<Opportunity>?> Handle(string soql, double probability = 90, string? processId = null)
        {
            logger.LogInformation("Importing Quotes Probability - ProcessId: {ProcessId}, SOQL: {SOQL}", processId, soql);

            var response = await salesforceService.Query<Opportunity>(soql);
            if (response == null || response.TotalSize <= 0)
            {
                logger.LogWarning("Response contains 0 results - ProcessId: {ProcessId}", processId);
                return null;
            }

            logger.LogInformation("Retrieved {TotalSize} potential High Probability Opportunities from Salesforce - ProcessId: {ProcessId}",
                response.TotalSize, processId);

            return await FilterOpportunities(response.Records?.ToList() ?? new List<Opportunity>(), probability, processId);
        }

        private async Task<IList<Opportunity>> FilterOpportunities(List<Opportunity> opps, double probability, string? processId = null)
        {
            var newOpps = await FilterOutExistingOpportunities(opps, processId);

            logger.LogInformation("Filtered {OppsCount} down to {NewOppsCount} new opportunities - ProcessId: {ProcessId}",
                opps.Count, newOpps.Count, processId);

            // Keep opportunities that are eligible for import OR should be marked for deletion (Closed Lost, Not Accepted)
            var relevantOpps = newOpps.Where(q =>
                IsEligibleForHighProbabilityImport(q, probability)
                || OpportunitiesUpdateHandler.IsClosedLost(q)
                || OpportunitiesUpdateHandler.IsNotAccepted(q)).ToList();

            logger.LogInformation("Filtered {NewOppsCount} down to {RelevantOppsCount} relevant Opportunities (eligible + deletions) - ProcessId: {ProcessId}",
                newOpps.Count, relevantOpps.Count, processId);

            return relevantOpps;
        }

        private async Task<IList<Opportunity>> FilterOutExistingOpportunities(IList<Opportunity> opps, string? processId = null)
        {

            // Filter out opportunities that are in the cache (recently processed)
            var oppIds = opps.Select(x => x.Id).Distinct().ToList();
            var cachedOppIds = oppIds.Where(id => quoteIdCache.TryGetValue(id, out _)).ToList();
            if (cachedOppIds.Any())
            {
                logger.LogInformation("Skipping {Count} opportunities from cache - ProcessId: {ProcessId}", cachedOppIds.Count, processId);
            }
            var uncachedOppIds = oppIds.Except(cachedOppIds).ToList();
            var matchedInDb = await dbContext.Headers
                .Include(i => i.Lines)
                .Where(order => order.OpportunityNumber != null && uncachedOppIds.Contains(order.OpportunityNumber))
                .ToListAsync();

            var oppsToIgnore = new List<Opportunity>();
            int successCount = 0;
            int errorCount = 0;

            foreach (var header in matchedInDb)
            {
                using (var transaction = await dbContext.Database.BeginTransactionAsync())
                {
                    try
                    {
                        logger.LogInformation("FilterOutExistingOpportunities Processing header: {Id}, {QuotePublicId} - ProcessId: {ProcessId}",
                            header.Id, header.QuotePublicId, processId);
                        logger.LogInformation(
                            "FilterOutExistingOpportunities Header state before processing: HeaderId={HeaderId}, Quote={QuotePublicId}, IsDeleted={IsDeleted}, Stage={OpportunityStage}, LastUpdatedBy={LastUpdatedBy}, LastUpdatedDate={LastUpdatedDate} - ProcessId: {ProcessId}",
                            header.Id,
                            header.QuotePublicId,
                            header.IsDeleted,
                            header.OpportunityStage,
                            header.LastUpdatedBy,
                            header.LastUpdatedDate,
                            processId);

                        // Find the opportunity matching this header
                        var matchingOpp = opps.FirstOrDefault(i => i.Id == header.OpportunityNumber);

                        // Skip processing for Closed Lost and Not Accepted - they will be handled by the caller for deletion
                        if (matchingOpp != null && (OpportunitiesUpdateHandler.IsClosedLost(matchingOpp) || OpportunitiesUpdateHandler.IsNotAccepted(matchingOpp)))
                        {
                            logger.LogInformation("FilterOutExistingOpportunities Skipping {StageName} opportunity {OpportunityId} for header {HeaderId} (Quote {QuotePublicId}, HeaderIsDeleted={HeaderIsDeleted}) - will be handled for deletion - ProcessId: {ProcessId}",
                                matchingOpp.StageName, matchingOpp.Id, header.Id, header.QuotePublicId, header.IsDeleted, processId);
                            await transaction.CommitAsync();
                            continue;
                        }

                        // If this opp matches in the db and the public quote matches the agreement or quotepublicid
                        // (quote has not changed)
                        var opportunityWhichMatchQuote = opps.FirstOrDefault(i => i.Id == header.OpportunityNumber && (!header.AgreementNumber.IsTOrAAgreement() && header.QuotePublicId == i.Quote.Name));

                        if (opportunityWhichMatchQuote != null)
                        {
                            await ProcessMatchedOpportunity(opportunityWhichMatchQuote, header, processId);
                            logger.LogInformation(
                                "FilterOutExistingOpportunities Header state after matched processing: HeaderId={HeaderId}, Quote={QuotePublicId}, IsDeleted={IsDeleted}, Stage={OpportunityStage}, LastUpdatedBy={LastUpdatedBy}, LastUpdatedDate={LastUpdatedDate} - ProcessId: {ProcessId}",
                                header.Id,
                                header.QuotePublicId,
                                header.IsDeleted,
                                header.OpportunityStage,
                                header.LastUpdatedBy,
                                header.LastUpdatedDate,
                                processId);
                            await dbContext.SaveChangesAsync();
                            await transaction.CommitAsync();
                            oppsToIgnore.Add(opportunityWhichMatchQuote);
                            successCount++;
                            continue;
                        }

                        // Find the opportunity with the same ID where the header has a T or A agreement or
                        // the quote in Salesforce has been updated (i.e. quote name doesn't match)
                        var opportunityWithUpdatedQuote = opps.FirstOrDefault(i => i.Id == header.OpportunityNumber &&
                            (header.AgreementNumber.IsTOrAAgreement() ||
                            (header.QuotePublicId != null && header.QuotePublicId != i.Quote.Name)));

                        if (opportunityWithUpdatedQuote == null)
                        {
                            logger.LogInformation("FilterOutExistingOpportunities Adding opportunity with no match: {Id}, {QuotePublicId} - ProcessId: {ProcessId}",
                                header.Id, header.QuotePublicId, processId);
                            await transaction.CommitAsync();
                            continue;
                        }

                        await ProcessUpdatedOpportunity(opportunityWithUpdatedQuote, header, processId);
                        logger.LogInformation(
                            "FilterOutExistingOpportunities Header state after updated-quote processing: HeaderId={HeaderId}, Quote={QuotePublicId}, IsDeleted={IsDeleted}, Stage={OpportunityStage}, LastUpdatedBy={LastUpdatedBy}, LastUpdatedDate={LastUpdatedDate} - ProcessId: {ProcessId}",
                            header.Id,
                            header.QuotePublicId,
                            header.IsDeleted,
                            header.OpportunityStage,
                            header.LastUpdatedBy,
                            header.LastUpdatedDate,
                            processId);
                        await dbContext.SaveChangesAsync();
                        await transaction.CommitAsync();
                        oppsToIgnore.Add(opportunityWithUpdatedQuote);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        await transaction.RollbackAsync();
                        logger.LogError(ex, "Error processing header {HeaderId} - ProcessId: {ProcessId}", header.Id, processId);

                        await ProcessingErrorLogger.LogErrorAsync(
                            dbContext,
                            logger,
                            "QuoteSync",
                            processId,
                            header.Id.ToString(),
                            "Header",
                            ex);
                    }
                }
            }

            // Cache all successfully processed opportunities at once
            foreach (var oppToIgnore in oppsToIgnore)
            {
                quoteIdCache.Set(oppToIgnore.Id, true, CacheDuration);
            }

            logger.LogInformation("FilterOutExistingOpportunities completed: {SuccessCount} successful, {ErrorCount} failed - ProcessId: {ProcessId}",
                successCount, errorCount, processId);

            return opps.Except(oppsToIgnore).ToList();
        }

        private async Task ProcessMatchedOpportunity(Opportunity opportunity, Header header, string? processId)
        {
            try
            {
                if (header.IsDeleted)
                {
                    logger.LogWarning(
                        "FilterOutExistingOpportunities matched an already-deleted header: HeaderId={HeaderId}, Quote={QuotePublicId}, OpportunityId={OpportunityId}. This path updates fields/lines but does not reset Header.IsDeleted. - ProcessId: {ProcessId}",
                        header.Id,
                        header.QuotePublicId,
                        opportunity.Id,
                        processId);
                }

                await this.dateCorrectionHandler.Handle(opportunity);

                var sfQuoteLines = await getQuoteLinesHandler.Handle(opportunity);

                var quoteData = await mapOpportunitiesToOrders.Handle(opportunity);
                if (quoteData != null && sfQuoteLines?.Any() == true)
                {
                    await MarkDeletedQuoteLines(opportunity, sfQuoteLines, quoteData);
                    AddNewQuoteLines(header, opportunity, sfQuoteLines, quoteData);
                }

                logger.LogInformation("FilterOutExistingOpportunities Filtered (OpportunityWhichMatchQuote): {Id}, {QuotePublicId} - ProcessId: {ProcessId}",
                    header.Id, header.QuotePublicId, processId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing quote lines for matched Opportunity {OpportunityId} - ProcessId: {ProcessId}",
                    opportunity.Id, processId);
                throw;
            }
        }

        private async Task ProcessUpdatedOpportunity(Opportunity opportunity, Header header, string? processId)
        {
            try
            {
                var sfUpdatedQuoteLines = await getQuoteLinesHandler.Handle(opportunity);

                var updatedQuoteData = await mapOpportunitiesToOrders.Handle(opportunity);
                if (updatedQuoteData != null && sfUpdatedQuoteLines?.Any() == true)
                {
                    AddNewQuoteLines(header, opportunity, sfUpdatedQuoteLines, updatedQuoteData);
                    await MarkDeletedQuoteLines(opportunity, sfUpdatedQuoteLines, updatedQuoteData);
                }

                foreach (var line in header.Lines)
                {
                    line.QuotePublicId = opportunity.Quote?.Name;
                    line.QuotePublicIdNumbersOnly = opportunity.Quote?.Name?.Substring(2);
                    logger.LogInformation("FilterOutExistingOpportunities Updating Line: {Id}, {QuotePublicId}, {LineId} - ProcessId: {ProcessId}",
                        header.Id, header.QuotePublicId, line.Id, processId);
                }

                header.QuotePublicId = opportunity.Quote?.Name;
                header.QuotePublicIdNumbersOnly = (opportunity.Quote?.Name != null && opportunity.Quote.Name.Length >= 2) ? opportunity.Quote.Name.Substring(2) : null;

                logger.LogInformation("FilterOutExistingOpportunities Updating Header and Ignored: {Id}, {QuotePublicId} - ProcessId: {ProcessId}",
                    header.Id, header.QuotePublicId, processId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing updated quote for Opportunity {OpportunityId} - ProcessId: {ProcessId}",
                    opportunity.Id, processId);
                throw;
            }
        }

        private void AddNewQuoteLines(Header header, Opportunity opportunity, IEnumerable<OpportunityQuoteLine> sfQuoteLines, QuoteData quoteData)
        {
            var existingLineIds = header.Lines
                .Where(l => !l.IsDeleted && l.QuoteLineNumber != null)
                .Select(l => l.QuoteLineNumber)
                .ToHashSet();

            var newQuoteLines = quoteData.Lines.Where(ql => !existingLineIds.Contains(ql.QuoteLineId)).ToList();

            if (newQuoteLines.Any())
            {
                logger.LogInformation("Found {Count} new quote lines to add for header {HeaderId} from opportunity {OpportunityId}",
                    newQuoteLines.Count, header.Id, opportunity.Id);

                foreach (var quoteLine in newQuoteLines)
                {
                    try
                    {
                        var line = CreateLineFromQuoteLine(header, opportunity!, quoteData, quoteLine);

                        line.NormalizeItemDescription(logger);
                        header.Lines.Add(line);

                        logger.LogInformation("Added new line {LineNumber} to header {HeaderId} for quote {QuotePublicId}",
                            line.AgreementLineNumber, header.Id, opportunity?.Quote?.Name);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error adding new quote line {QuoteLineId} to header {HeaderId}", quoteLine.QuoteLineId, header.Id);
                    }
                }
            }
        }
        private static Line CreateLineFromQuoteLine(Header header, Opportunity opportunity, QuoteData quoteData, QuoteLineData quoteLine)
        {
            var agreementLineNumber = quoteData.QuotePublicId?.Replace("-", string.Empty) + "-" + quoteLine.QuoteLineIndex;

            return new Line
            {
                Header = header,
                AgreementLineNumber = agreementLineNumber,
                AgreementLineType = quoteLine.LineTypeId?.ToString(),
                Attributes = quoteLine.Attributes,
                LocalizedAttributes = quoteLine.LocalizedAttributes,
                ChangeSequence = 0,
                CollectionDate = quoteLine.CollectionDate,
                DeliveryDate = quoteData.DeliveryDate,
                DescriptionWithAttributes = quoteLine.DescriptionWithAttributes,
                Division = quoteData.SourceWarehouse!.Division!,
                Facility = quoteData.SourceWarehouse!.Facility!,
                GenericItemNumber = quoteLine.GenericCode,
                IsDeleted = false,
                ItemDescription = quoteLine.ItemDescription,
                ItemNumber = quoteLine.GenericCode,
                LastUpdatedDate = DateTime.UtcNow,
                NumberOfShifts = quoteLine.NumberOfShifts,
                OrderSource = header.OrderSource,
                PackageGroupNumber = quoteLine.CPQGroupName,
                Quantity = quoteLine.Quantity,
                QuoteLineIndex = quoteLine.QuoteLineIndex,
                QuoteLineNumber = quoteLine.QuoteLineId,
                QuotePublicId = quoteData.QuotePublicId,
                QuotePublicIdNumbersOnly = quoteData.QuotePublicId?.Length >= 2 ? quoteData.QuotePublicId.Substring(2) : quoteData.QuotePublicId,
                RequiresFulfilment = !quoteLine.GenericCode.IsExcludedLine(),
                ValidFromDate = quoteLine.OnHireDate,
                ValidToDate = quoteLine.OffHireDate,
                Warehouse = quoteData.SourceWarehouse!.Name!
            };
        }

        private async Task MarkDeletedQuoteLines(Opportunity opportunity, IEnumerable<OpportunityQuoteLine> sfQuoteLines, QuoteData quoteData)
        {
            try
            {
                logger.LogInformation($"Checking for deleted quote lines for quote {opportunity.QuoteNumber}");

                var existingQuoteLines = await dbContext.Lines
                    .Where(ql => ql.QuotePublicId == quoteData.QuotePublicId)
                    .ToListAsync();

                var salesforceLineIds = sfQuoteLines.Select(line => line.Id).ToHashSet();
                var deletedLines = existingQuoteLines.Where(line => (string.IsNullOrEmpty(line.QuoteLineNumber) || !salesforceLineIds.Contains(line.QuoteLineNumber)) && !line.IsDeleted).ToList();

                if (deletedLines.Any())
                {
                    int deletedCount = deletedLines.Count;
                    logger.LogInformation($"Found {deletedCount} deleted quote lines for quote {opportunity.QuoteNumber}");

                    foreach (var line in deletedLines)
                    {
                        await line.MarkAsDeletedAndRemoveReservationsAsync(dbContext, logger);
                    }

                    await dbContext.SaveChangesAsync();
                }
                else
                {
                    logger.LogInformation($"No deleted quote lines found for quote {opportunity.QuoteNumber}");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error marking deleted quote lines for quote {opportunity.QuoteNumber}");
                throw;
            }
        }

        private bool IsEligibleForHighProbabilityImport(Opportunity opportunity, double probability)
        {
            if (probability <= 0)
            {
                return opportunity?.Quote?.Warehouse != null;
            }

            return opportunity?.Quote?.Warehouse != null && !OpportunityIsClosedLost(opportunity) && !OpportunityIsClosedWon(opportunity) && opportunity.EffectiveProbability >= probability;
        }

        private bool OpportunityIsClosedLost(Opportunity opportunity)
        {
            return opportunity?.StageName == Constants.OpportunityStage.ClosedLost;
        }

        private bool OpportunityIsClosedWon(Opportunity opportunity)
        {
            return opportunity?.StageName == Constants.OpportunityStage.ClosedWon;
        }
    }
}
