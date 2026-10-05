using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OF.Common.Extensions;
using OF.Common.Infrastructure.IPG.Orders.Models.Quotes;
using OF.Common.Utils;
using OF.Data;
using OF.Data.Database;
using System.Data;

namespace OF.Common.UseCases.Quotes
{
    public class PersistOrdersHandler
    {
        private readonly ApplicationDbContext dbContext;
        private readonly ILogger logger;

        public PersistOrdersHandler(ApplicationDbContext dbContext, ILogger logger)
        {
            this.dbContext = dbContext;
            this.logger = logger;
        }

        public async Task Handle(List<QuoteData> quotes, string? processId = null)
        {
            logger.LogInformation("Persisting {Count} quotes to database - ProcessId: {ProcessId}", quotes?.Count ?? 0, processId);

            if (quotes?.Any() == false)
            {
                logger.LogWarning("No quotes to save - ProcessId: {ProcessId}", processId);
                return;
            }

            var groupedQuotes = quotes!.GroupBy(i => i.QuotePublicId).ToList();
            int successCount = 0;
            int errorCount = 0;

            foreach (var groupedQuote in groupedQuotes)
            {
                try
                {
                    await ProcessSingleQuoteGroup(groupedQuote, processId);
                    successCount++;
                }
                catch (Exception ex)
                {
                    errorCount++;
                    var quoteId = groupedQuote.Key ?? "Unknown";
                    logger.LogError(ex, "Failed to persist quote group {QuoteId} - ProcessId: {ProcessId}", quoteId, processId);

                    await ProcessingErrorLogger.LogErrorAsync(
                        dbContext,
                        logger,
                        "QuoteSync",
                        processId,
                        quoteId,
                        "QuoteGroup",
                        ex);
                }
            }

            logger.LogInformation("Quote persistence completed: {SuccessCount} successful, {ErrorCount} failed - ProcessId: {ProcessId}",
                successCount, errorCount, processId);
        }

        private async Task ProcessSingleQuoteGroup(IGrouping<string?, QuoteData> groupedQuote, string? processId)
        {
            using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadUncommitted);

            try
            {
                if (groupedQuote.Count() > 1)
                {
                    logger.LogWarning("Quote {QuoteId} has multiple entries from API, selecting one with most lines - ProcessId: {ProcessId}",
                        groupedQuote.Key, processId);
                }

                var quote = groupedQuote.OrderByDescending(i => i.Lines.Count()).First();
                await PersistSingleQuote(quote, processId);

                await dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task PersistSingleQuote(QuoteData quote, string? processId)
        {
            var header = await dbContext.Headers
                .Include(h => h.Lines)
                .FirstOrDefaultAsync(h => h.QuotePublicId == quote.QuotePublicId);

            if (header == null)
            {
                header = new Header();
                dbContext.Headers.Add(header);
            }

            var changes = new List<string>();

            void SetField<T>(string field, T? oldValue, T? newValue, Action<T?> setter)
            {
                if ((oldValue == null && newValue != null) || (oldValue != null && !oldValue.Equals(newValue)))
                {
                    changes.Add($"{field}: '{oldValue}' -> '{newValue}'");
                    setter(newValue);
                }
            }

            // Track and apply field changes
            SetField("AgreementNumber", header.AgreementNumber, quote.QuotePublicId, v => header.AgreementNumber = v);
            SetField("Division", header.Division, quote.SourceWarehouse?.Division, v => header.Division = v ?? header.Division);
            SetField("Facility", header.Facility, quote.SourceWarehouse?.Facility, v => header.Facility = v ?? header.Facility);
            SetField("Probability", header.Probability, quote.Probability, v => header.Probability = v);
            SetField("OpportunityStage", header.OpportunityStage, quote.StageName, v => header.OpportunityStage = v);
            SetField("OpportunityName", header.OpportunityName, quote.OpportunityName, v => header.OpportunityName = v);
            SetField("OverviewOfService", header.OverviewOfService, quote.OverviewOfServices, v => header.OverviewOfService = v);
            SetField("OpportunityNumber", header.OpportunityNumber, quote.OpportunityRecordId, v => header.OpportunityNumber = v);
            SetField("QuoteNumber", header.QuoteNumber, quote.QuoteRecordId, v => header.QuoteNumber = v);
            SetField("QuotePublicId", header.QuotePublicId, quote.QuotePublicId, v => header.QuotePublicId = v);
            SetField("OffHireDate", header.OffHireDate, quote.OffHireDate, v => header.OffHireDate = v);
            SetField("OnHireDate", header.OnHireDate, quote.OnHireDate, v => header.OnHireDate = v);
            SetField("CustomerNumber", header.CustomerNumber, quote.Customer?.CustomerNumber, v => header.CustomerNumber = v);
            SetField("CustomerName", header.CustomerName, quote.Customer?.Name, v => header.CustomerName = v);
            SetField("CustomerAddress", header.CustomerAddress, quote.Address1, v => header.CustomerAddress = v);
            SetField("QuotePublicIdNumbersOnly", header.QuotePublicIdNumbersOnly, quote.QuotePublicId?.Length >= 2 ? quote.QuotePublicId.Substring(2) : quote.QuotePublicId, v => header.QuotePublicIdNumbersOnly = v);
            SetField("ArmcontactName", header.ArmcontactName, quote.Contact?.Name, v => header.ArmcontactName = v);
            SetField("ArmcontactEmail", header.ArmcontactEmail, quote.Contact?.Email, v => header.ArmcontactEmail = v);
            SetField("ArmcontactPhone", header.ArmcontactPhone, quote.Contact?.Phone, v => header.ArmcontactPhone = v);

            // Set fields that always update regardless of change tracking
            header.ChangeSequence = 0;
            header.OrderSource = "SF";
            header.IsDeleted = false;
            header.LastUpdatedDate = DateTime.UtcNow;
            header.LastUpdatedBy = "SF";

            foreach (var line in header.Lines)
            {
                line.IsDeleted = true;
            }

            // Upsert lines from Salesforce
            foreach (var quoteLine in quote.Lines)
            {
                try
                {
                    var agreementLineNumber = quote.QuotePublicId?.Replace("-", string.Empty) + "-" + quoteLine.QuoteLineIndex;

                    // Match by stable Salesforce line ID first to survive QuoteLineIndex changes,
                    // then fall back to AgreementLineNumber for lines without a QuoteLineNumber set.
                    var existingLine = header.Lines.FirstOrDefault(l => !string.IsNullOrEmpty(l.QuoteLineNumber) && l.QuoteLineNumber == quoteLine.QuoteLineId)
                        ?? header.Lines.FirstOrDefault(l => l.AgreementLineNumber == agreementLineNumber);

                    if (existingLine != null)
                    {
                        // Update fields and unmark as deleted
                        existingLine.IsDeleted = false;

                        // If the QuoteLineIndex changed (line was matched by Salesforce ID),
                        // update AgreementLineNumber to reflect the new index.
                        if (existingLine.AgreementLineNumber != agreementLineNumber)
                        {
                            logger.LogInformation(
                                "Line {LineId} for quote {QuoteId} had its AgreementLineNumber updated from '{OldLineNumber}' to '{NewLineNumber}' due to a QuoteLineIndex change. Reservations preserved. - ProcessId: {ProcessId}",
                                existingLine.Id, quote.QuotePublicId, existingLine.AgreementLineNumber, agreementLineNumber, processId);
                            existingLine.AgreementLineNumber = agreementLineNumber;
                        }

                        existingLine.AgreementLineType = quoteLine.LineTypeId?.ToString();
                        existingLine.Attributes = quoteLine.Attributes;
                        existingLine.LocalizedAttributes = quoteLine.LocalizedAttributes;
                        existingLine.ChangeSequence = 0;
                        existingLine.Division = quote.SourceWarehouse!.Division!;
                        existingLine.Facility = quote.SourceWarehouse!.Facility!;
                        existingLine.OrderSource = header.OrderSource;
                        existingLine.GenericItemNumber = quoteLine.GenericCode;
                        existingLine.LastUpdatedDate = DateTime.UtcNow;
                        existingLine.LastUpdatedBy = "SF";
                        existingLine.ItemNumber = quoteLine.GenericCode;
                        existingLine.PackageGroupNumber = quoteLine.CPQGroupName;
                        existingLine.Quantity = quoteLine.Quantity;
                        existingLine.QuoteLineIndex = quoteLine.QuoteLineIndex;
                        existingLine.QuoteLineNumber = quoteLine.QuoteLineId;
                        existingLine.DeliveryDate = quote.DeliveryDate;
                        existingLine.ValidFromDate = quoteLine.OnHireDate;
                        existingLine.ValidToDate = quoteLine.OffHireDate;
                        existingLine.Warehouse = quote.SourceWarehouse!.Name!;
                        existingLine.QuotePublicId = quote.QuotePublicId;
                        existingLine.QuotePublicIdNumbersOnly = quote.QuotePublicId?.Length >= 2 ? quote.QuotePublicId.Substring(2) : quote.QuotePublicId;
                        existingLine.NumberOfShifts = quoteLine.NumberOfShifts;
                        existingLine.RateType = quote.RateType;
                        existingLine.CollectionDate = quoteLine.CollectionDate;
                        existingLine.DescriptionWithAttributes = quoteLine.DescriptionWithAttributes;
                        existingLine.ItemDescription = quoteLine.ItemDescription;
                        existingLine.RequiresFulfilment = !quoteLine.GenericCode.IsExcludedLine();
                        existingLine.NormalizeItemDescription(logger);
                    }
                    else
                    {
                        // Insert new line
                        var line = new Line();
                        dbContext.Lines.Add(line);
                        header.Lines.Add(line);

                        line.Header = header;
                        line.AgreementLineType = quoteLine.LineTypeId?.ToString();
                        line.AgreementLineNumber = agreementLineNumber;
                        line.Attributes = quoteLine.Attributes;
                        line.LocalizedAttributes = quoteLine.LocalizedAttributes;
                        line.ChangeSequence = 0;
                        line.Division = quote.SourceWarehouse!.Division!;
                        line.Facility = quote.SourceWarehouse!.Facility!;
                        line.OrderSource = header.OrderSource;
                        line.GenericItemNumber = quoteLine.GenericCode;
                        line.IsDeleted = false;
                        line.LastUpdatedDate = DateTime.UtcNow;
                        line.LastUpdatedBy = "SF";
                        line.ItemNumber = quoteLine.GenericCode;
                        line.PackageGroupNumber = quoteLine.CPQGroupName;
                        line.Quantity = quoteLine.Quantity;
                        line.QuoteLineIndex = quoteLine.QuoteLineIndex;
                        line.QuoteLineNumber = quoteLine.QuoteLineId;
                        line.DeliveryDate = quote.DeliveryDate;
                        line.ValidFromDate = quoteLine.OnHireDate;
                        line.ValidToDate = quoteLine.OffHireDate;
                        line.Warehouse = quote.SourceWarehouse!.Name!;
                        line.QuotePublicId = quote.QuotePublicId;
                        line.QuotePublicIdNumbersOnly = quote.QuotePublicId?.Length >= 2 ? quote.QuotePublicId.Substring(2) : quote.QuotePublicId;
                        line.NumberOfShifts = quoteLine.NumberOfShifts;
                        line.RateType = quote.RateType;
                        line.CollectionDate = quoteLine.CollectionDate;
                        line.DescriptionWithAttributes = quoteLine.DescriptionWithAttributes;
                        line.ItemDescription = quoteLine.ItemDescription;
                        line.RequiresFulfilment = !quoteLine.GenericCode.IsExcludedLine();
                        line.NormalizeItemDescription(logger);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to process quote line {QuoteLineId} for quote {QuoteId} - ProcessId: {ProcessId}",
                        quoteLine.QuoteLineId, quote.QuotePublicId, processId);

                    await ProcessingErrorLogger.LogErrorAsync(
                        dbContext,
                        logger,
                        "QuoteSync",
                        processId,
                        quoteLine.QuoteLineId,
                        "QuoteLine",
                        ex);
                }
            }

            var linesToDelete = header.Lines.Where(l => l.IsDeleted).ToList();

            // Prefetch all line IDs with reservations in a single query to avoid N+1 database calls
            var lineIdsToCheck = linesToDelete.Select(l => l.Id).ToList();
            var lineIdsWithReservations = (await dbContext.Reservations
                .Where(r => lineIdsToCheck.Contains(r.LineId))
                .Select(r => r.LineId)
                .Distinct()
                .ToListAsync())
                .ToHashSet();

            // Null out AgreementLineNumber for lines that will be deleted before SaveChanges runs.
            // EF Core processes UPDATEs before DELETEs, so without this a line taking the index number
            // of a deleted line would hit the unique index on AgreementLineNumber mid-transaction.
            var lineIdsBeingDeleted = linesToDelete
                .Where(l => !lineIdsWithReservations.Contains(l.Id))
                .Select(l => l.Id)
                .ToList();

            if (lineIdsBeingDeleted.Count > 0 && dbContext.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
            {
                await dbContext.Lines
                    .Where(l => lineIdsBeingDeleted.Contains(l.Id))
                    .ExecuteUpdateAsync(s => s.SetProperty(l => l.AgreementLineNumber, (string?)null));
            }

            foreach (var line in linesToDelete)
            {
                if (lineIdsWithReservations.Contains(line.Id))
                {
                    logger.LogWarning(
                        "Line {LineId} ({AgreementLineNumber}) for quote {QuoteId} has active reservations and was not matched in the incoming sync. " +
                        "The line will not be deleted to preserve reservations. Review the quote in Salesforce/CPQ for line index changes. - ProcessId: {ProcessId}",
                        line.Id, line.AgreementLineNumber, quote.QuotePublicId, processId);
                    continue;
                }

                dbContext.Lines.Remove(line);
            }

            if (changes.Count > 0)
            {
                logger.LogInformation("Header {QuotePublicId} updated fields: {Changes} - ProcessId: {ProcessId}",
                    quote.QuotePublicId, string.Join(", ", changes), processId);
            }
        }
    }
}
