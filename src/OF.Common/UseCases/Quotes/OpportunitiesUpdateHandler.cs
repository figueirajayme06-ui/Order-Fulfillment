using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OF.Common.Extensions;
using OF.Common.Infrastructure.IPG.Orders.Models.Quotes;
using OF.Common.Utils;
using OF.Data;
using OF.Data.Database;

namespace OF.Common.UseCases.Quotes
{
    public class OpportunitiesUpdateHandler
    {
        private readonly ApplicationDbContext dbContext;
        private readonly ILogger logger;

        public OpportunitiesUpdateHandler(ApplicationDbContext dbContext, ILogger logger)
        {
            this.dbContext = dbContext;
            this.logger = logger;
        }

        public static bool IsNotAccepted(Opportunity? opportunity) =>
            string.Equals(opportunity?.StageName, Constants.OpportunityStage.NotAccepted, StringComparison.OrdinalIgnoreCase);

        public static bool IsClosedLost(Opportunity? opportunity) =>
            string.Equals(opportunity?.StageName, Constants.OpportunityStage.ClosedLost, StringComparison.OrdinalIgnoreCase);

        private static IQueryable<Header> QueryMatchingHeader(ApplicationDbContext ctx, Opportunity opp)
        {
            var sfQuoteName = opp.Quote?.Name ?? string.Empty;
            var sfQuoteId = opp.Quote?.Id ?? string.Empty;

            return ctx.Headers
                .Include(h => h.Lines)
                .Where(h =>
                    (!string.IsNullOrEmpty(h.QuotePublicId) && !string.IsNullOrEmpty(sfQuoteName) && h.QuotePublicId == sfQuoteName)
                    || (!string.IsNullOrEmpty(h.QuoteNumber) && !string.IsNullOrEmpty(sfQuoteId) && h.QuoteNumber == sfQuoteId));
        }

        private async Task RemoveReservationsForLineAsync(Line line)
        {
            var historicalHeader = await line.GetHistoricalHeaderAsync(dbContext);
            if (historicalHeader != null)
            {
                logger.LogInformation(
                    "Preserving reservations for historical deleted line {LineId} on header {HeaderId}",
                    line.Id,
                    line.HeaderId);
                return;
            }

            var reservations = await dbContext.Reservations
                .Where(r => r.LineId == line.Id)
                .ToListAsync();

            if (!reservations.Any())
            {
                logger.LogInformation(
                    "No reservations for line {LineId} on header {HeaderId}",
                    line.Id,
                    line.HeaderId);
                return;
            }

            dbContext.Reservations.RemoveRange(reservations);

            logger.LogInformation(
                "Removed {ReservationCount} reservations for deleted line {LineId} on header {HeaderId}",
                reservations.Count,
                line.Id,
                line.HeaderId);
        }

        public async Task<int> MarkNotAcceptedOpportunitiesAsync(IList<Opportunity> opportunities, string processId)
        {
            var notAccepted = opportunities
                .Where(IsNotAccepted)
                .ToList();

            if (!notAccepted.Any())
            {
                logger.LogInformation("Persisted {MarkedCount} headers marked deleted for Not Accepted opportunities - ProcessId: {ProcessId}", 0, processId);
                return 0;
            }

            logger.LogInformation("Found {Count} Not Accepted opportunities to mark deleted - ProcessId: {ProcessId}", notAccepted.Count, processId);

            int markedCount = 0;
            foreach (var opp in notAccepted)
            {
                try
                {
                    var header = await QueryMatchingHeader(dbContext, opp).FirstOrDefaultAsync();

                    if (header == null)
                    {
                        logger.LogDebug("Not Accepted Opportunity {OpportunityId} has no existing header - ProcessId: {ProcessId}", opp.Id, processId);
                        continue;
                    }

                    if (header.IsDeleted)
                    {
                        logger.LogInformation(
                            "Header for Opportunity {OpportunityId} already marked deleted. HeaderId={HeaderId}, Quote={QuotePublicId}, Stage={OpportunityStage}, LastUpdatedBy={LastUpdatedBy}, LastUpdatedDate={LastUpdatedDate} - ProcessId: {ProcessId}",
                            opp.Id,
                            header.Id,
                            header.QuotePublicId,
                            header.OpportunityStage,
                            header.LastUpdatedBy,
                            header.LastUpdatedDate,
                            processId);
                        continue;
                    }

                    var activeLineCount = header.Lines.Count(l => !l.IsDeleted);
                    var alreadyDeletedLineCount = header.Lines.Count - activeLineCount;
                    logger.LogWarning(
                        "Marking header as deleted for Not Accepted opportunity. HeaderId={HeaderId}, Quote={QuotePublicId}, OpportunityId={OpportunityId}, PreviousStage={PreviousStage}, ActiveLines={ActiveLineCount}, AlreadyDeletedLines={AlreadyDeletedLineCount} - ProcessId: {ProcessId}",
                        header.Id,
                        header.QuotePublicId,
                        opp.Id,
                        header.OpportunityStage,
                        activeLineCount,
                        alreadyDeletedLineCount,
                        processId);

                    header.IsDeleted = true;
                    header.LastUpdatedBy = "SF";
                    header.LastUpdatedDate = DateTime.UtcNow;
                    header.OpportunityStage = opp.StageName;

                    foreach (var line in header.Lines)
                    {
                        if (line.IsDeleted)
                        {
                            await RemoveReservationsForLineAsync(line);
                            logger.LogInformation(
                                "Processed reservations for already deleted line {LineId} on header {HeaderId}",
                                line.Id,
                                line.HeaderId);
                            
                            continue;
                        }

                        await line.MarkAsDeletedAndRemoveReservationsAsync(dbContext, logger);
                        line.LastUpdatedBy = "SF";
                        line.LastUpdatedDate = DateTime.UtcNow;
                    }

                    markedCount++;
                    logger.LogInformation("Marked header (Id:{HeaderId}, Quote:{QuotePublicId}) as deleted for Not Accepted Opportunity {OpportunityId} - ProcessId: {ProcessId}", header.Id, header.QuotePublicId, opp.Id, processId);
                }
                catch (Exception ex)
                {
                    try
                    {
                        logger.LogError(ex,
                            "Failed marking Not Accepted Opportunity {OpportunityId} as deleted - ProcessId: {ProcessId}",
                            opp.Id, processId);

                        await ProcessingErrorLogger.LogErrorAsync(
                            dbContext,
                            logger,
                            "QuoteSync",
                            processId,
                            opp.Id,
                            "NotAcceptedMark",
                            ex);
                    }
                    catch (Exception loggingEx)
                    {
                        logger.LogWarning(loggingEx,
                            "Secondary error logging failed for Opportunity {OpportunityId} - ProcessId: {ProcessId}",
                            opp.Id, processId);
                    }
                }
            }

            if (markedCount > 0)
            {
                await dbContext.SaveChangesAsync();
            }

            logger.LogInformation("Persisted {MarkedCount} headers marked deleted for Not Accepted opportunities - ProcessId: {ProcessId}", markedCount, processId);

            return markedCount;
        }

        public async Task<int> MarkClosedLostOpportunitiesAsync(IList<Opportunity> opportunities, string processId)
        {
            var closedLost = opportunities
                .Where(IsClosedLost)
                .ToList();

            if (!closedLost.Any())
            {
                logger.LogInformation("Persisted {MarkedCount} headers marked deleted for Closed Lost opportunities - ProcessId: {ProcessId}", 0, processId);
                return 0;
            }

            logger.LogInformation("Found {Count} Closed Lost opportunities to mark deleted - ProcessId: {ProcessId}", closedLost.Count, processId);

            int markedCount = 0;
            foreach (var opp in closedLost)
            {
                try
                {
                    var header = await QueryMatchingHeader(dbContext, opp).FirstOrDefaultAsync();

                    if (header == null)
                    {
                        logger.LogDebug("Closed Lost Opportunity {OpportunityId} has no existing header - ProcessId: {ProcessId}", opp.Id, processId);
                        continue;
                    }

                    if (header.IsDeleted)
                    {
                        logger.LogInformation(
                            "Header for Opportunity {OpportunityId} already marked deleted. HeaderId={HeaderId}, Quote={QuotePublicId}, Stage={OpportunityStage}, LastUpdatedBy={LastUpdatedBy}, LastUpdatedDate={LastUpdatedDate} - ProcessId: {ProcessId}",
                            opp.Id,
                            header.Id,
                            header.QuotePublicId,
                            header.OpportunityStage,
                            header.LastUpdatedBy,
                            header.LastUpdatedDate,
                            processId);
                        continue;
                    }

                    var activeLineCount = header.Lines.Count(l => !l.IsDeleted);
                    var alreadyDeletedLineCount = header.Lines.Count - activeLineCount;
                    logger.LogWarning(
                        "Marking header as deleted for Closed Lost opportunity. HeaderId={HeaderId}, Quote={QuotePublicId}, OpportunityId={OpportunityId}, PreviousStage={PreviousStage}, ActiveLines={ActiveLineCount}, AlreadyDeletedLines={AlreadyDeletedLineCount} - ProcessId: {ProcessId}",
                        header.Id,
                        header.QuotePublicId,
                        opp.Id,
                        header.OpportunityStage,
                        activeLineCount,
                        alreadyDeletedLineCount,
                        processId);

                    header.IsDeleted = true;
                    header.LastUpdatedBy = "SF";
                    header.LastUpdatedDate = DateTime.UtcNow;
                    header.OpportunityStage = opp.StageName;

                    foreach (var line in header.Lines)
                    {
                        if (line.IsDeleted)
                        {
                            await RemoveReservationsForLineAsync(line);
                            continue;
                        }

                        await line.MarkAsDeletedAndRemoveReservationsAsync(dbContext, logger);
                        line.LastUpdatedBy = "SF";
                        line.LastUpdatedDate = DateTime.UtcNow;
                    }

                    markedCount++;
                    logger.LogInformation("Marked header (Id:{HeaderId}, Quote:{QuotePublicId}) as deleted for Closed Lost Opportunity {OpportunityId} - ProcessId: {ProcessId}", header.Id, header.QuotePublicId, opp.Id, processId);
                }
                catch (Exception ex)
                {
                    try
                    {
                        logger.LogError(ex,
                            "Failed marking Closed Lost Opportunity {OpportunityId} as deleted - ProcessId: {ProcessId}",
                            opp.Id, processId);

                        await ProcessingErrorLogger.LogErrorAsync(
                            dbContext,
                            logger,
                            "QuoteSync",
                            processId,
                            opp.Id,
                            "ClosedLostMark",
                            ex);
                    }
                    catch (Exception loggingEx)
                    {
                        logger.LogWarning(loggingEx,
                            "Secondary error logging failed for Opportunity {OpportunityId} - ProcessId: {ProcessId}",
                            opp.Id, processId);
                    }
                }
            }

            if (markedCount > 0)
            {
                await dbContext.SaveChangesAsync();
            }

            logger.LogInformation("Persisted {MarkedCount} headers marked deleted for Closed Lost opportunities - ProcessId: {ProcessId}", markedCount, processId);

            return markedCount;
        }
    }
}
