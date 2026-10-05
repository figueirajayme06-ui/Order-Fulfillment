using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OF.Common;
using OF.Common.Infrastructure.CloudSuite.Models.Agreements.BODs;
using OF.Common.Infrastructure.CloudSuite.Models.Agreements.BODs.Exceptions;
using OF.Common.Infrastructure.IPG.Orders;
using OF.Common.Infrastructure.IPG.Orders.Models.Orders;
using OF.Common.Infrastructure.IPG.Orders.Models.Quotes.Response;
using OF.Common.Infrastructure.IPG.Orders.Models.RAA.Agreementrs;
using OF.Common.Infrastructure.IPG.Orders.SOQL;
using OF.Common.Infrastructure.OF;
using OF.Common.UseCases.Agreements;
using OF.Common.Utils;
using OF.Data;
using OF.Data.Database;
using Refit;

namespace OF.Api.UseCases.Agreements
{
    public class AgreementReceivedHandler : IBodHandler
    {
        private readonly IOrderManagementIntegration salesforceService;
        private readonly IAgreementLineFetcher agreementLineFetcher;
        private readonly ApplicationDbContext dbContext;
        private readonly ICoreFulfilmentEngine fulfilmentEngine;
        private readonly ILogger logger;

        public AgreementReceivedHandler(IOrderManagementIntegration salesforceService, ApplicationDbContext dbContext, ICoreFulfilmentEngine fulfilmentEngine, IAgreementLineFetcher agreementLineFetcher, ILogger logger)
        {
            this.salesforceService = salesforceService;
            this.agreementLineFetcher = agreementLineFetcher;
            this.dbContext = dbContext;
            this.fulfilmentEngine = fulfilmentEngine;
            this.logger = logger;
        }

        public static string GetSessionId(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return string.Empty;
            }

            try
            {
                var bod = GetBodFromMessage(body);
                return bod?.DataArea.AGKRentalOrderHeader.AgreementHeaders.AgreementNumber.Substring(1) ?? string.Empty;
            }
            catch
            {
                // Swallow exception for logging purposes
                return string.Empty;
            }
        }

        public async Task Handle(string body)
        {
            using var _ = logger.BeginScope(new { Action = nameof(AgreementReceivedHandler), SessionId = GetSessionId(body) });
            logger.LogInformation($"AgreementReceivedHandler fired for message.");
            logger.LogDebug(body);

            var isNew = false;

            try
            {
                (string operation, Header entity, SyncAGKRentalOrderHeader bod) = await GetEntityFromMessage(body);

                if (operation.ToLower() == Constants.Operations.Delete)
                {
                    logger.LogInformation($"Deleting header for agreement {entity.AgreementNumber!}.");

                    entity.IsDeleted = true;
                }
                else
                {
                    if (entity.Id == 0)
                    {
                        logger.LogInformation($"Adding header for agreement {entity.AgreementNumber!}.");

                        dbContext.Headers.Add(entity);

                        isNew = true;
                    }
                }

                var lines = await dbContext.Lines.Where(i => i.AgreementNumbersOnly == entity.AgreementNumbersOnly && i.HeaderId == null).ToListAsync();

                if (lines.Count > 0)
                {
                    var existingLineIds = entity.Lines.Select(i => i.Id).ToList();

                    foreach (var line in lines.Where(i => !existingLineIds.Contains(i.Id)))
                    {
                        line.Header = entity;
                    }
                }

                await dbContext.SaveChangesAsync();

                if (operation.ToLower() != Constants.Operations.Delete)
                {
                    await PullAndPersistLinesAsync(entity);
                }

                var tidy = new UpdateLinesWithHeadersHandler(dbContext, logger);
                await tidy.Handle(entity.Id);

                // Update the status of any associated line and header

                fulfilmentEngine.RecalculateStatusForHeader(entity);
                await dbContext.SaveChangesAsync();

                if (isNew)
                {
                    try
                    {
                        var request = new OrderProcessStatusRequest()
                        {
                            AgreementNumber = bod.DataArea.AGKRentalOrderHeader.AgreementHeaders.AgreementNumber,
                            Division = entity.Division,
                            Facility = entity.Facility,
                            OrderProcessStatus = Constants.IPG.ProcessStatus.HeaderReady
                        };

                        await salesforceService.OrderProcessStatus(request);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning($"OrderProcessStatus[{Constants.IPG.ProcessStatus.HeaderReady}]: Failed: {ex.GetBaseException().Message}.");
                    }
                }
            }
            catch (BodValidationException exception)
            {
                logger.LogError("Bod validation error for Header.", exception);
            }
            catch (Exception exception)
            {
                logger.LogError("Error while deserializing BOD for Header message.", exception);
                logger.LogInformation("Body Error: " + body);
            }
        }

        private async Task PullAndPersistLinesAsync(Header header)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(header.AgreementNumber))
                {
                    logger.LogWarning($"Cannot pull lines: agreement number is missing for header {header.Id}.");
                    return;
                }

                var m3Lines = await agreementLineFetcher.FetchLinesAsync(header.AgreementNumber, CancellationToken.None);

                var filteredLines = m3Lines
                    .Where(l => !l.ItemNumber.IsExcludedLine())
                    .ToList();

                if (filteredLines.Count == 0)
                {
                    logger.LogInformation($"No lines found via M3 API for agreement {header.AgreementNumbersOnly}.");
                    return;
                }

                logger.LogInformation($"Pulling {filteredLines.Count} lines from M3 API for agreement {header.AgreementNumbersOnly}.");

                IList<OrderItem>? orderLines = null;

                if (!string.IsNullOrWhiteSpace(header.QuotePublicId))
                {
                    try
                    {
                        var soql = string.Format(OFSOQL.GetOrderLinesByQuotePublicId, header.QuotePublicId);
                        var soqlResponse = await salesforceService.Query<OrderItem>(soql);
                        orderLines = soqlResponse?.Records?.ToList();
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning($"Failed to retrieve Salesforce order lines for agreement {header.AgreementNumbersOnly}: {ex.GetBaseException().Message}");
                    }
                }

                foreach (var m3Line in filteredLines)
                {
                    var existing = header.Lines.FirstOrDefault(i =>
                        i.AgreementLineNumber == "A" + m3Line.AgreementLineNumbersOnly ||
                        i.AgreementLineNumber == "T" + m3Line.AgreementLineNumbersOnly);

                    if (existing == null)
                    {
                        existing = await dbContext.Lines.FirstOrDefaultAsync(i =>
                            (i.AgreementLineNumber == "A" + m3Line.AgreementLineNumbersOnly ||
                             i.AgreementLineNumber == "T" + m3Line.AgreementLineNumbersOnly) &&
                            i.HeaderId == null);

                        if (existing != null)
                        {
                            header.Lines.Add(existing);
                        }
                    }

                    if (existing == null)
                    {
                        existing = header.Lines.FirstOrDefault(i =>
                            i.GenericItemNumber == m3Line.GenericItem &&
                            i.ItemNumber == m3Line.GenericItem &&
                            string.IsNullOrWhiteSpace(i.AgreementNumbersOnly));
                    }

                    bool toBeAdded = existing == null;

                    existing = m3Line.ToLineEntity(existing);

                    if (existing.Id == 0)
                    {
                        dbContext.Lines.Add(existing);
                    }

                    var orderItem = orderLines?.FirstOrDefault(i =>
                        i.QuoteLine?.GenericItemNumber == (m3Line.GenericItem ?? m3Line.ItemNumber));

                    if (orderItem != null)
                    {
                        existing.QuoteLineNumber = orderItem.QuoteLineId;
                        existing.QuoteLineIndex = Convert.ToInt32(orderItem.QuoteLine?.LineId ?? 0);
                        existing.OrderLineNumber = orderItem.Id;
                        existing.OrderLineIndex = Convert.ToInt32(orderItem.OrderLineIndex ?? 0);
                        existing.PackageGroupNumber ??= orderItem.QuoteLine?.Group?.Name;
                        existing.QuotePublicId ??= orderItem.QuoteLine?.Quote?.Name;
                        existing.QuotePublicIdNumbersOnly ??= !string.IsNullOrWhiteSpace(orderItem.QuoteLine?.Quote?.Name)
                            ? orderItem.QuoteLine.Quote?.Name?.Substring(2)
                            : null;
                        existing.GenericItemNumber = !string.IsNullOrWhiteSpace(orderItem.QuoteLine?.GenericItemNumber)
                            ? orderItem.QuoteLine.GenericItemNumber
                            : m3Line.ItemNumber;
                        existing.Attributes ??= orderItem.QuoteLine?.SelectedAttributesAsText;
                        existing.DescriptionWithAttributes ??= orderItem.QuoteLine?.DescriptionWithAttributes;
                        existing.ItemDescription ??= orderItem.QuoteLine?.ItemDescription;

                        existing.NormalizeItemDescription(logger);
                    }

                    if (toBeAdded)
                    {
                        header.Lines.Add(existing);
                    }
                }

                await dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning($"Failed to pull lines from M3 API for agreement {header.AgreementNumbersOnly}: {ex.GetBaseException().Message}");
            }
        }

        private async Task<(string operation, Header entity, SyncAGKRentalOrderHeader bod)> GetEntityFromMessage(string body)
        {
            SyncAGKRentalOrderHeader bod = GetBodFromMessage(body);
            string operation = bod.DataArea.Sync.ActionCriteria.ActionExpression.ActionCode;

            // Get the header by the agreement number only originally

            Header? header = await GetHeaderByAgreementDetails(bod);
            (header, Order? order) = await GetHeaderBySalesforceDetailsIfNotFound(header, bod);


            var entity = bod.DataArea.ToHeaderEntity(header);

            entity.IsSkeleton = false; // Force to false as we have received a full agreement

            if (order != null)
            {
                float? probability = order.Quote?.Opportunity?.EffectiveProbability != null
                    ? (float)order.Quote.Opportunity.EffectiveProbability.Value
                    : float.TryParse(order.Quote?.Opportunity?.Probability, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                        ? parsed
                        : null;

                entity.QuoteNumber = order.QuoteId;
                entity.QuotePublicId = order.Quote?.Name;
                entity.QuotePublicIdNumbersOnly = order.Quote?.Name?.Substring(2);
                entity.OrderNumber = order.Id;
                entity.OnHireDate ??= order.OnHireDate;
                entity.OffHireDate ??= order.OffHireDate;
                entity.OpportunityNumber ??= order.Quote?.OpportunityId;
                entity.OpportunityName = order.Quote?.Opportunity?.Name;
                entity.OpportunityStage = order.Quote?.Opportunity?.OpportunityStageName;
                entity.OverviewOfService ??= order.Quote?.OverviewOfServices;
                if (probability.HasValue)
                    entity.Probability = probability.Value;
                entity.ArmcontactName ??= order.Quote?.Contact?.Name;
                entity.ArmcontactEmail ??= order.Quote?.Contact?.Email;
                entity.ArmcontactPhone ??= order.Quote?.Contact?.Phone;
                entity.CustomerAddress ??= order.ShippingAddress;
            }

            return (operation, entity, bod);
        }

        private static SyncAGKRentalOrderHeader GetBodFromMessage(string body)
            => body.ParseToBODResponse<SyncAGKRentalOrderHeader>();

        private async Task<Header?> GetHeaderByAgreementDetails(SyncAGKRentalOrderHeader bod)
        {
            return await dbContext.Headers
                .Include(i => i.Lines)
                .FirstOrDefaultAsync(i => i.AgreementNumbersOnly == bod.DataArea.AGKRentalOrderHeader.AgreementHeaders.AgreementNumberId);
        }

        private async Task<(Header? header, Order? order)> GetHeaderBySalesforceDetailsIfNotFound(Header? existingHeader, SyncAGKRentalOrderHeader bod)
        {
            if (existingHeader == null && !string.IsNullOrEmpty(bod.DataArea.AGKRentalOrderHeader.AgreementHeaders.OrderRecordId))
            {
                existingHeader = await dbContext.Headers.Include(i => i.Lines).FirstOrDefaultAsync(i => i.OrderNumber == bod.DataArea.AGKRentalOrderHeader.AgreementHeaders.OrderRecordId);
            }

            if (existingHeader == null && !string.IsNullOrEmpty(bod.DataArea.AGKRentalOrderHeader.AgreementHeaders.QuotePublicId))
            {
                existingHeader = await dbContext.Headers.Include(i => i.Lines).FirstOrDefaultAsync(i => i.QuotePublicId == bod.DataArea.AGKRentalOrderHeader.AgreementHeaders.QuotePublicId);
            }

            // If we haven't found the header by the order record id, try to find it by the order quote in salesforce

            Order? order = null;

            var headerDoesntExistOrOrderRecordIdIsDifferentOrNoQuote = existingHeader == null || (existingHeader.OrderNumber != bod.DataArea.AGKRentalOrderHeader.AgreementHeaders.OrderRecordId) || string.IsNullOrEmpty(existingHeader.QuotePublicId);
            if (!string.IsNullOrEmpty(bod.DataArea.AGKRentalOrderHeader.AgreementHeaders.OrderRecordId) && headerDoesntExistOrOrderRecordIdIsDifferentOrNoQuote)
            {
                try
                {
                    // Get the ids etc for the lines and quote from SF order id

                    string soql = string.Format(OFSOQL.GetOrder, bod.DataArea.AGKRentalOrderHeader.AgreementHeaders.OrderRecordId);
                    SOQLResponse<Order> soqlResponse = await salesforceService.Query<Order>(soql);
                    order = soqlResponse?.Records?.FirstOrDefault();

                    if (order == null && !string.IsNullOrWhiteSpace(bod.DataArea.AGKRentalOrderHeader.AgreementHeaders.QuotePublicId))
                    {
                        soql = string.Format(OFSOQL.GetOrderByQuotePublicId, bod.DataArea.AGKRentalOrderHeader.AgreementHeaders.QuotePublicId);
                        soqlResponse = await salesforceService.Query<Order>(soql);
                        order = soqlResponse?.Records?.FirstOrDefault();
                    }
                }
                catch (ApiException apiException)
                {
                    logger.LogWarning(apiException.GetBaseException().Message);
                }
            }

            if (existingHeader == null && !string.IsNullOrEmpty(order?.QuoteId))
            {
                existingHeader = await dbContext.Headers.Include(i => i.Lines).FirstOrDefaultAsync(i => i.QuoteNumber == order.QuoteId);
            }

            if (existingHeader == null && !string.IsNullOrEmpty(order?.Quote?.Name))
            {
                existingHeader = await dbContext.Headers.Include(i => i.Lines).FirstOrDefaultAsync(i => i.QuoteNumber == order.Quote.Name);
            }

            if (existingHeader == null && !string.IsNullOrEmpty(order?.Quote?.OpportunityId))
            {
                existingHeader = await dbContext.Headers.Include(i => i.Lines).FirstOrDefaultAsync(i => i.OpportunityNumber == order.Quote.OpportunityId);
            }

            return (existingHeader, order);
        }
    }
}
