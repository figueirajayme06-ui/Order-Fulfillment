using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OF.Common.Infrastructure.IPG.Orders;
using OF.Common.Infrastructure.IPG.Orders.Models.Quotes;
using OF.Common.Utils;
using OF.Data;
using OF.Data.Database;

namespace OF.Common.UseCases.Quotes;
public class HireDateCorrectionHandler
{
    private readonly ILogger logger;
    private readonly ApplicationDbContext dbContext;
    private readonly GetQuoteLinesHandler getQuoteLinesHandler;

    public HireDateCorrectionHandler(ILogger logger, ApplicationDbContext dbContext, IOrderManagementIntegration salesforceService)
    {
        this.logger = logger;
        this.dbContext = dbContext;
        this.getQuoteLinesHandler = new GetQuoteLinesHandler(salesforceService, logger);
    }

    public async Task Handle(Opportunity opportunity)
    {
        var header = await FindHeaderByOpportunityNumberAsync(opportunity.Id);
        if (header == default)
        {
            logger.LogWarning("Failed to find Header with OpportunityNumber {Id}!", opportunity.Id);
            return;
        }

        if (IsQuoteUpdated(header, opportunity))
        {
            UpdateHeader(header, opportunity, opportunity.Quote);
            await CorrectHeaderLineDates(header, opportunity);
            try
            {
                await dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to save changes for Header {HeaderId} (Opportunity {OpportunityId})", header.Id, opportunity.Id);
            }
        }
    }

    private Task<Header?> FindHeaderByOpportunityNumberAsync(string? OpportunityNumber)
         => dbContext.Headers
                .Include(x => x.Lines)
                .Where(x => x.OpportunityNumber == OpportunityNumber)
                .SingleOrDefaultAsync();

    public async Task<bool> CorrectHeaderLineDates(Header header, Opportunity opportunity)
    {
        var opportunityQuoteLines = await getQuoteLinesHandler.Handle(opportunity);
        var quote = opportunity.Quote;
        var updated = false;

        foreach (Line line in header.Lines)
        {
            try
            {
                OpportunityQuoteLine? oppQuoteLine = opportunityQuoteLines.SingleOrDefault(x => x.Id == line.QuoteLineNumber);
                if (oppQuoteLine == null)
                {
                    logger.LogWarning("{Class} failed to find QuoteLine for QuoteLineNumber {Id}", nameof(HireDateCorrectionHandler), line.Id);
                    continue;
                }

                line.ValidFromDate = oppQuoteLine.OnHireDate ?? header.OnHireDate!.Value;
                line.ValidToDate = oppQuoteLine.OffHireDate ?? header.OffHireDate!.Value;
                line.CollectionDate = quote.CollectionDate;
                line.DeliveryDate = quote.DeliveryDate;
                line.Quantity = float.TryParse(oppQuoteLine.Quantity, out float quantity) ? quantity : 0;
                line.ItemDescription = oppQuoteLine.ItemDescription;
                line.DescriptionWithAttributes = oppQuoteLine.DescriptionWithAttributes;
                line.LastUpdatedDate = DateTime.UtcNow;
                line.AgreementLineNumber = BuildAgreementLineNumber(line.QuotePublicId ?? quote.Name, oppQuoteLine.LineId);
                line.NormalizeItemDescription(logger);

                updated = true;

                logger.LogInformation("Updated line for Id {Id} based on LastModifiedDate", line.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error updating line {LineId} in header {HeaderId}", line.Id, header.Id);
            }
        }

        return updated;
    }

    private static string? BuildAgreementLineNumber(string? quotePublicId, double? quoteLineIndex)
    {
        if (string.IsNullOrEmpty(quotePublicId) || !quoteLineIndex.HasValue)
            return null;
        return $"{quotePublicId.Replace("-", string.Empty)}-{quoteLineIndex.Value}";
    }

    private bool IsQuoteUpdated(Header header, Opportunity opportunity)
    {
        bool hireDatesChanged = header.OnHireDate != opportunity.Quote.OnHireDate
            || header.OffHireDate != opportunity.Quote.OffHireDate;

        if (hireDatesChanged)
        {
            logger.LogInformation("Quote {QuoteId} hire dates differ from Salesforce. OF OnHire: {OFOnHire}, SF OnHire: {SFOnHire}, OF OffHire: {OFOffHire}, SF OffHire: {SFOffHire}",
                opportunity.Quote.Name, header.OnHireDate, opportunity.Quote.OnHireDate, header.OffHireDate, opportunity.Quote.OffHireDate);
            return true;
        }

        return false;
    }

    private void UpdateHeader(Header header, Opportunity opportunity, Quote quote)
    {
        var wasDeleted = header.IsDeleted;

        header.AgreementNumber = header.QuotePublicId;
        header.OpportunityName = opportunity.OpportunityName;
        header.OpportunityStage = opportunity.StageName;
        header.OverviewOfService = opportunity.Quote.OverviewOfServices;
        header.Probability = opportunity.EffectiveProbability != null
            ? (float)opportunity.EffectiveProbability.Value
            : float.TryParse(opportunity.Probability, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0;
        header.OnHireDate = quote.OnHireDate;
        header.OffHireDate = quote.OffHireDate;
        header.LastUpdatedDate = DateTime.UtcNow;

        logger.LogInformation("Updated all fields for Header Id {Id} from Salesforce data", header.Id);

        if (wasDeleted)
        {
            logger.LogWarning(
                "Updated Salesforce fields for already-deleted header {HeaderId} (Quote {QuotePublicId}). Header remains deleted unless a separate persistence path resets IsDeleted.",
                header.Id,
                header.QuotePublicId);
        }
    }
}
