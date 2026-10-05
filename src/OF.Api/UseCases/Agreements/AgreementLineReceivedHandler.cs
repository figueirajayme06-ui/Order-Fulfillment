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
using OF.Common.Models;
using OF.Common.UseCases.Agreements;
using OF.Common.Utils;
using OF.Data;
using OF.Data.Database;
using Refit;
using System.Data;

namespace OF.Api.UseCases.Agreements
{
    public class AgreementLineReceivedHandler : IBodHandler
    {
        private readonly IOrderManagementIntegration salesforceService;
        private readonly ApplicationDbContext dbContext;
        private readonly ICoreFulfilmentEngine fulfilmentEngine;
        private readonly ILogger logger;

        public AgreementLineReceivedHandler(
            IOrderManagementIntegration salesforceService,
            ApplicationDbContext dbContext,
            ICoreFulfilmentEngine fulfilmentEngine,
            ILogger logger)
        {
            this.salesforceService = salesforceService;
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
                return bod?.DataArea.AGKRentalOrderLine.AgreementNumber.Substring(1) ?? string.Empty;
            }
            catch
            {
                // Swallow exception for logging purposes
                return string.Empty;
            }
        }

        public async Task Handle(string body)
        {
            using var _ = logger.BeginScope(new { Action = nameof(AgreementLineReceivedHandler), SessionId = GetSessionId(body) });
            logger.LogInformation($"AgreementLineReceivedHandler fired for message.");
            logger.LogDebug(body);

            using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

            var isNew = false;

            try
            {
                (string operation, Line entity, SyncAGKRentalOrderLine bod, Header? header) = await GetEntityFromMessage(body);

                if (operation!.ToLower() == Constants.Operations.Ignore)
                {
                    logger.LogWarning($"Item {bod.DataArea.AGKRentalOrderLine.AgreementLines.ItemNumber} is in the exclusion list of codes and is not processed.");

                    await transaction.RollbackAsync();
                    return;
                }
                else if (operation!.ToLower() == Constants.Operations.Delete)
                {
                    logger.LogInformation($"Deleting line for agreement {entity.AgreementLineNumber!}.");

                    entity.IsDeleted = true;

                    // Preserve reservations for historical orders (Terminated, Invoiced, Completed) to maintain a record of what was allocated
                    if (header != null && Constants.HeaderStatus.HistoricalStatuses.Contains(header.Status ?? string.Empty))
                    {
                        logger.LogInformation(
                            "Skipping reservation deletion for historical order {AgreementLineNumber} (Header Status: {Status}).",
                            entity.AgreementLineNumber,
                            header.Status);
                    }
                    else if (dbContext.Reservations.Any(r => r.LineId == entity.Id && r.IsConfirmed))
                    {
                        logger.LogWarning(
                            "Skipping reservation deletion for line {AgreementLineNumber} because it has confirmed reservations (line already on-hire/delivered).",
                            entity.AgreementLineNumber);
                    }
                    else
                    {
                        // We have to delete reservations if we delete a line to free up serialized assets
                        DeleteReservationsForLine(entity.Id);
                    }
                }
                else
                {
                    logger.LogInformation($"Set IsDeleted to false for non-delete operations {entity.AgreementLineNumber!}.");

                    // This ensures that the entity is marked as active.
                    entity.IsDeleted = false;


                    if (entity.Id == 0)
                    {
                        dbContext.Lines.Add(entity);
                        isNew = true;
                        await dbContext.SaveChangesAsync();
                    }

                    // These two paths are mutually exclusive to prevent data loss:
                    // - If the BOD contains allocation/delivery data, the upsert knows exactly which asset to assign.
                    //   No need to speculatively move sibling reservations (which the upsert would then delete).
                    // - If the BOD has no allocation data, move matching reservations from sibling lines.
                    var allocationText = bod.DataArea.AGKRentalOrderLine?.AgreementLines?.TextIdentityDeliveryText?.Text;
                    var hasAllocationData = allocationText?.GetItemsFromAttributeText(bod)?.ContainsAllocation == true;

                    if (hasAllocationData || bod.DataArea.ShouldConfirmReservation)
                    {
                        await UpsertReservationForLineIfAlreadyAllocatedOrDelivered(entity, bod);
                    }
                    else
                    {
                        await ReassignReservationsFromSiblingQuoteLine(entity);
                    }

                    if (bod.DataArea.ShouldConfirmReservation)
                    {
                        await UpdateOnHireAssetWithDates(bod, entity);
                    }
                }

                await dbContext.SaveChangesAsync();

                // Update the status of any associated line and header

                if (header != null)
                {
                    fulfilmentEngine.RecalculateStatusForLineAndHeader(entity);

                    var tidy = new UpdateLinesWithHeadersHandler(dbContext, logger);
                    await tidy.Handle(header.Id);
                }

                await transaction.CommitAsync();

                if (isNew)
                {
                    try
                    {
                        // Need tests around this status stuff 

                        var request = new OrderLineProcessStatusRequest()
                        {
                            AgreementNumber = bod.DataArea.AGKRentalOrderLine.AgreementNumber,
                            AgreementLineNumber = bod.DataArea.AGKRentalOrderLine.GetAgreementLineNumber().ToString(),
                            Division = entity.Division,
                            Facility = entity.Facility,
                            OrderLineProcessStatus = Constants.IPG.ProcessStatus.LineReady
                        };

                        await salesforceService.OrderLineProcessStatus(request);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning($"OrderLineProcessStatus[{Constants.IPG.ProcessStatus.LineReady}]: Failed: {ex.GetBaseException().Message}.");
                    }
                }
            }
            catch (BodValidationException exception)
            {
                logger.LogError($"Bod validation error for Line exception. {exception.GetBaseException().Message}", exception);
                await transaction.RollbackAsync();
            }
            catch (Exception exception)
            {
                logger.LogError($"Error while deserializing BOD for Line message. {exception.GetBaseException().Message}", exception);
                logger.LogInformation("Body Error: " + body);
                await transaction.RollbackAsync();
            }
        }

        private async Task<(string operation, Line entity, SyncAGKRentalOrderLine bod, Header? header)> GetEntityFromMessage(string body)
        {
            SyncAGKRentalOrderLine bod = GetBodFromMessage(body);

            string operation = bod.DataArea.Sync.ActionCriteria.ActionExpression.ActionCode;

            // Get the existing line record from the database by agreement number if it exists to update

            (Header? header, Line? line) = await GetHeaderAndLineByAgreementDetails(bod);
            (header, line, OrderItem? orderItem) = await GetHeaderAndLineBySalesforceDetailsIfNotFound(header, line, bod);
            line = GetLineFromHeaderExistingGenericItemsIfNotFound(header, line, bod);

            if (operation.ToLower() == Constants.Operations.Delete)
            {
                if (line == null)
                {
                    logger.LogWarning($"No line found for Agreement Line ${bod.DataArea.AGKRentalOrderLine.GetAgreementLineId()}, ignoring 'Delete'");
                    return (Constants.Operations.Ignore, new Line(), bod, null);
                }

                return (Constants.Operations.Delete, line, bod, header);
            }


            if (header == null)
            {
                header = await CreateSkeletonHeader(bod);
            }

            // Map the BOD to the entity
            var entity = bod.DataArea.ToLineEntity(line, logger);

            if (orderItem != null)
            {
                entity.QuoteLineNumber = orderItem.QuoteLineId;
                entity.QuoteLineIndex = Convert.ToInt32(orderItem.QuoteLine?.LineId ?? 0);
                entity.OrderLineNumber = orderItem.Id;
                entity.OrderLineIndex = Convert.ToInt32(orderItem.OrderLineIndex ?? 0);
                entity.PackageGroupNumber = orderItem.QuoteLine?.Group?.Name ?? entity.PackageGroupNumber;
                entity.QuotePublicId ??= orderItem.QuoteLine?.Quote?.Name;
                entity.QuotePublicIdNumbersOnly ??= !string.IsNullOrWhiteSpace(orderItem.QuoteLine?.Quote?.Name) ? orderItem.QuoteLine.Quote?.Name?.Substring(2) : null;
                entity.GenericItemNumber = !string.IsNullOrWhiteSpace(orderItem.QuoteLine?.GenericItemNumber) ? orderItem.QuoteLine.GenericItemNumber : bod.DataArea.AGKRentalOrderLine.AgreementLines.GenericOrItemNumber;
                entity.Attributes = !string.IsNullOrWhiteSpace(orderItem.QuoteLine?.SelectedAttributesAsText) ? orderItem.QuoteLine?.SelectedAttributesAsText : entity.Attributes;
                entity.DescriptionWithAttributes = !string.IsNullOrWhiteSpace(orderItem.QuoteLine?.DescriptionWithAttributes) ? orderItem.QuoteLine?.DescriptionWithAttributes : entity.Attributes;
                entity.ItemDescription = !string.IsNullOrWhiteSpace(orderItem.QuoteLine?.ItemDescription) ? orderItem.QuoteLine.ItemDescription : entity.ItemDescription;
            }

            entity.NormalizeItemDescription(logger);

            if (header != null)
            {
                // UPdate header address with Line address if need be

                header.CustomerAddressCode = bod.DataArea.AGKRentalOrderLine.AgreementLines.CustomerSiteAddress;
                header.CustomerAddress = bod.DataArea.AGKRentalOrderLine.GetAgreementAddress();
            }

            // If this doesnt have a header but we found one which matches, get it added to the line

            if (entity.Header == null && header != null)
            {
                entity.Header = header;
            }

            return (operation, entity, bod, header);
        }

        private static SyncAGKRentalOrderLine GetBodFromMessage(string messageBody)
            => messageBody.ParseToBODResponse<SyncAGKRentalOrderLine>();

        private async Task<(Header? header, Line? line)> GetHeaderAndLineByAgreementDetails(SyncAGKRentalOrderLine bod)
        {
            var lineNumbersOnly = bod.DataArea.AGKRentalOrderLine.GetAgreementLineId().Substring(1);

            Header? header = null;
            Line? line = await dbContext.Lines
                .Include(i => i.Header)
                .Where(i => !i.IsDeleted)
                .FirstOrDefaultAsync(i =>
                    i.AgreementNumbersOnly == bod.DataArea.AGKRentalOrderLine.AgreementNumberId &&
                    (i.AgreementLineNumber == $"T{lineNumbersOnly}" || i.AgreementLineNumber == $"A{lineNumbersOnly}"));

            if (line?.Header == null)
            {
                header = await dbContext.Headers
                    .Include(i => i.Lines)
                    .FirstOrDefaultAsync(i => i.AgreementNumbersOnly == bod.DataArea.AGKRentalOrderLine.AgreementNumberId);
            }

            return (header, line);
        }

        private async Task<(Header? header, Line? line, OrderItem? orderItem)> GetHeaderAndLineBySalesforceDetailsIfNotFound(Header? existingHeader, Line? existingLine, SyncAGKRentalOrderLine bod)
        {
            var lineNumbersOnly = bod.DataArea.AGKRentalOrderLine.GetAgreementLineId().Substring(1);

            if (existingLine == null && !string.IsNullOrEmpty(bod.DataArea.AGKRentalOrderLine.AgreementLines.OrderLineRecordId))
            {
                existingLine = await dbContext.Lines.Include(i => i.Header).FirstOrDefaultAsync(i =>
                    (i.AgreementLineNumber == $"T{lineNumbersOnly}" || i.AgreementLineNumber == $"A{lineNumbersOnly}") &&
                    i.OrderLineNumber == bod.DataArea.AGKRentalOrderLine.AgreementLines.OrderLineRecordId);
            }

            if (existingLine?.Header != null && existingHeader == null)
            {
                existingHeader = existingLine.Header;
            }

            // If we haven't found the header and line by the order line record id, try to find it by the order quote line in salesforce

            OrderItem? orderItem = null;

            if ((existingHeader == null || existingLine == null) && !string.IsNullOrEmpty(bod.DataArea.AGKRentalOrderLine.AgreementLines.OrderLineRecordId))
            {
                try
                {
                    // Get the ids etc for the lines and quote from SF order id

                    string soql = string.Format(OFSOQL.GetOrderLine, bod.DataArea.AGKRentalOrderLine.AgreementLines.OrderLineRecordId);
                    SOQLResponse<OrderItem> soqlResponse = await salesforceService.Query<OrderItem>(soql);
                    orderItem = soqlResponse?.Records?.FirstOrDefault();
                }
                catch (ApiException apiException)
                {
                    logger.LogWarning(apiException.GetBaseException().Message);
                }

                // Get the lines by the quote line id if this line is a quote at the time

                if (existingLine == null && !string.IsNullOrWhiteSpace(orderItem?.QuoteLineId))
                {
                    existingLine = await dbContext.Lines.Include(i => i.Header).FirstOrDefaultAsync(i =>
                        (i.AgreementLineNumber != null && i.AgreementLineNumber.ToUpper().StartsWith("Q") == true) &&
                         i.QuoteLineNumber == orderItem.QuoteLineId);

                    if (existingLine?.Header != null && existingHeader == null)
                    {
                        existingHeader = existingLine.Header;
                    }
                }

                // Get the header by the quote id

                if (existingHeader == null && !string.IsNullOrWhiteSpace(orderItem?.QuoteLine?.QuoteId))
                {
                    existingHeader = await dbContext.Headers.Include(i => i.Lines).FirstOrDefaultAsync(i => i.QuoteNumber == orderItem.QuoteLine.QuoteId);
                }

                // Get the header by the quote public id

                if (existingHeader == null && !string.IsNullOrWhiteSpace(orderItem?.QuoteLine?.Quote?.Name))
                {
                    existingHeader = await dbContext.Headers.Include(i => i.Lines).FirstOrDefaultAsync(i => i.QuotePublicId == orderItem.QuoteLine.Quote.Name);
                }
            }

            return (existingHeader, existingLine, orderItem);
        }

        /// <summary>
        /// Update the reservation if there is item attributes as text item allocated
        /// </summary>
        /// <param name="lineId"></param>
        private async Task UpsertReservationForLineIfAlreadyAllocatedOrDelivered(Line line, SyncAGKRentalOrderLine bod)
        {
            var lineId = line.Id;

            if (lineId <= 0)
            {
                return;
            }

            ItemsFromAttributes? itemsFromAttributesAsText = bod.DataArea.AGKRentalOrderLine?.AgreementLines?.TextIdentityDeliveryText?.Text?.GetItemsFromAttributeText(bod);

            // If we have a allocation which already exists in the attributes as text or we have a confirmed or terminated and delivered line then create a reservation

            if (itemsFromAttributesAsText?.ContainsAllocation == true || bod.DataArea.ShouldConfirmReservation)
            {
                if (itemsFromAttributesAsText == null)
                {
                    itemsFromAttributesAsText = bod.GetDefaultItemsFromBOD();
                }

                // Remove the reservations
                var reservations = await dbContext.Reservations.Where(r => r.LineId == lineId).ToListAsync();
                var primaryReservation = reservations.FirstOrDefault(r => r.LineId == lineId);
                var isNewReservation = primaryReservation == null;

                foreach (var reservation in reservations)
                {
                    if (primaryReservation == reservation)
                    {
                        continue;
                    }

                    dbContext.Reservations.Remove(reservation);
                }

                if (primaryReservation == null)
                {
                    primaryReservation = new Reservation();
                    primaryReservation.LineId = lineId;
                    primaryReservation.Quantity = (int)itemsFromAttributesAsText.Quantity;
                    primaryReservation.Warehouse = itemsFromAttributesAsText.Warehouse;

                    if (itemsFromAttributesAsText.IsDepotFulfil)
                    {
                        primaryReservation.ItemNumber = Constants.IPG.DepotFulfil;
                        primaryReservation.AssetId = Constants.IPG.DepotFulfil;
                    }
                    else
                    {
                        primaryReservation.ItemNumber = itemsFromAttributesAsText.ItemNumber;
                        primaryReservation.AssetId = itemsFromAttributesAsText.LotNumber;
                    }

                    dbContext.Reservations.Add(primaryReservation);
                }

                primaryReservation.ActualAssetId = itemsFromAttributesAsText.LotNumber;
                primaryReservation.ActualItemNumber = itemsFromAttributesAsText.ItemNumber;
                primaryReservation.ActualQuantity = itemsFromAttributesAsText.Quantity;
                primaryReservation.LastUpdatedDate = DateTime.UtcNow;
                primaryReservation.IsConfirmed = bod.DataArea.ShouldConfirmReservation;
                // We do not update rehire here, just leave reservations as they are as we won't recieve a rehire as a create
                bool wasDepotFulfilled = primaryReservation.IsDepotFulfilled;
                primaryReservation.IsDepotFulfilled = itemsFromAttributesAsText.IsDepotFulfil;

                var bodLotNumber = bod.DataArea.AGKRentalOrderLine?.AgreementLines?.LotNumber;
                var bodItemNumber = bod.DataArea.AGKRentalOrderLine?.AgreementLines?.ItemNumber;
                // Only applies when updating an existing reservation: M3 has allocated a different asset than the one stored in attributes
                var m3OverriddenSuggestion = !isNewReservation && !string.IsNullOrWhiteSpace(bodLotNumber) && !string.IsNullOrWhiteSpace(itemsFromAttributesAsText.LotNumber) && !string.Equals(bodLotNumber, itemsFromAttributesAsText.LotNumber, StringComparison.OrdinalIgnoreCase);

                // When an actual asset is assigned in M3/Spartan, update the reservation and clear depot fulfil flag
                if (!string.IsNullOrWhiteSpace(bodLotNumber) && (primaryReservation.IsDepotFulfilled || wasDepotFulfilled || m3OverriddenSuggestion))
                {
                    primaryReservation.AssetId = bodLotNumber;
                    if (!string.IsNullOrWhiteSpace(bodItemNumber))
                    {
                        primaryReservation.ItemNumber = bodItemNumber;
                    }
                    primaryReservation.IsDepotFulfilled = false; // Clear depot fulfil when actual asset is assigned
                }

                line.FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled;

                await dbContext.SaveChangesAsync();
            }
        }

        private async Task UpdateOnHireAssetWithDates(SyncAGKRentalOrderLine bod, Line line)
        {
            if (string.IsNullOrWhiteSpace(bod.DataArea.AGKRentalOrderLine?.AgreementLines?.LotNumber))
            {
                logger.LogInformation($"No asset lotnumber in BOD for line: {bod.DataArea.AGKRentalOrderLine?.GetAgreementLineNumber()}");
                return;
            }

            var asset = await dbContext.Assets.FirstOrDefaultAsync(i => i.Id == bod.DataArea.AGKRentalOrderLine.AgreementLines.LotNumber);

            if (asset == null)
            {
                logger.LogInformation($"No asset with the id {bod.DataArea.AGKRentalOrderLine.AgreementLines.LotNumber} found for BOD line: {bod.DataArea.AGKRentalOrderLine?.GetAgreementLineNumber()}");
                return;
            }

            asset.DeliveryDate = line.DeliveryDate;
            asset.TerminationDate = line.TerminationDate;
            asset.AgreementLineValidFromDate = line.ValidFromDate;
            asset.AgreementLineValidToDate = line.ValidToDate;
            asset.CollectionDate = line.CollectionDate;
            asset.IonlastModified = bod.DataArea.AGKRentalOrderLine.GetDateFromChangeSequence();

            await dbContext.SaveChangesAsync();
        }

        private void DeleteReservationsForLine(int lineId)
        {
            if (lineId <= 0)
            {
                return;
            }

            var reservations = dbContext.Reservations.Where(r => r.LineId == lineId).ToArray();

            foreach (var ringfence in reservations)
            {
                dbContext.Reservations.Remove(ringfence);
            }
        }

        private Line? GetLineFromHeaderExistingGenericItemsIfNotFound(Header? existingHeader, Line? existingLine, SyncAGKRentalOrderLine bod)
        {
            if (existingHeader != null && existingLine == null)
            {
                string genericItemNumber = bod.DataArea.AGKRentalOrderLine.AgreementLines.GenericOrItemNumber;
                existingLine = existingHeader.Lines.FirstOrDefault(i => i.GenericItemNumber == genericItemNumber && i.ItemNumber == genericItemNumber && string.IsNullOrWhiteSpace(i.AgreementNumbersOnly));
            }

            return existingLine;
        }

        private async Task<Header> CreateSkeletonHeader(SyncAGKRentalOrderLine bod)
        {
            ArgumentNullException.ThrowIfNull(bod);

            var header = new Header
            {
                AgreementNumber = bod.DataArea.AGKRentalOrderLine.AgreementNumber,
                AgreementNumbersOnly = bod.DataArea.AGKRentalOrderLine.AgreementNumberId,
                IsSkeleton = true,

                Facility = bod.DataArea.AGKRentalOrderLine.AgreementLines.Facility,
                Division = bod.DataArea.AGKRentalOrderLine.AgreementLines.Division,
                OrderSource = bod.DataArea.AGKRentalOrderLine.AgreementLines.OrderSource ?? "SF",
                OnHireDate = bod.DataArea.AGKRentalOrderLine.AgreementLines.ValidFromDate,
                OffHireDate = bod.DataArea.AGKRentalOrderLine.AgreementLines.ValidToDate
            };

            dbContext.Headers.Add(header);

            logger.LogInformation("Created skeleton header for Agreement Number {AgreementNumber}.", bod.DataArea.AGKRentalOrderLine.AgreementNumber);

            await dbContext.SaveChangesAsync();
            return header;
        }

        private async Task ReassignReservationsFromSiblingQuoteLine(Line newLine)
        {
            if (string.IsNullOrWhiteSpace(newLine.QuoteLineNumber) || string.IsNullOrWhiteSpace(newLine.Warehouse) || string.IsNullOrWhiteSpace(newLine.AgreementNumbersOnly))
            {
                return;
            }

            var siblingLineIds = await dbContext.Lines
                .Where(l => l.Id != newLine.Id &&
                            l.AgreementNumbersOnly == newLine.AgreementNumbersOnly &&
                            l.QuoteLineNumber == newLine.QuoteLineNumber &&
                            !l.IsDeleted)
                .Select(l => l.Id)
                .ToListAsync();

            if (!siblingLineIds.Any())
            {
                return;
            }

            // Get existing reservation asset IDs on this line to avoid duplicates
            var existingAssetIds = await dbContext.Reservations
                .Where(r => r.LineId == newLine.Id)
                .Select(r => r.AssetId)
                .ToListAsync();

            // Only move unconfirmed reservations - don't touch on-hire assets
            var reservationsToMove = await dbContext.Reservations
                .Where(r => siblingLineIds.Contains(r.LineId) && 
                            r.Warehouse == newLine.Warehouse &&
                            !r.IsConfirmed)
                .ToListAsync();

            // Log skipped confirmed reservations for visibility
            var confirmedCount = await dbContext.Reservations
                .CountAsync(r => siblingLineIds.Contains(r.LineId) && 
                                 r.Warehouse == newLine.Warehouse && 
                                 r.IsConfirmed);

            if (confirmedCount > 0)
            {
                logger.LogInformation(
                    "Skipped {Count} confirmed reservation(s) on sibling lines for {AgreementLineNumber} - assets already on-hire.",
                    confirmedCount, newLine.AgreementLineNumber);
            }

            if (!reservationsToMove.Any())
            {
                return;
            }

            // Filter out reservations for assets already on this line, and limit to line's needed quantity
            var neededQuantity = Math.Max((int)newLine.Quantity - existingAssetIds.Count, 0);
            var reservationsToActuallyMove = reservationsToMove
                .Where(r => !existingAssetIds.Contains(r.AssetId))
                .Take(neededQuantity)
                .ToList();

            var skippedDuplicates = reservationsToMove.Count - reservationsToActuallyMove.Count;
            if (skippedDuplicates > 0)
            {
                logger.LogInformation(
                    "Skipped {Count} reservation(s) - asset already reserved or line quantity satisfied for {AgreementLineNumber}.",
                    skippedDuplicates, newLine.AgreementLineNumber);
            }

            foreach (var reservation in reservationsToActuallyMove)
            {
                logger.LogInformation(
                    "Moving reservation {AssetId} from line {OldLineId} to line {NewLineId} ({AgreementLineNumber}).",
                    reservation.AssetId, reservation.LineId, newLine.Id, newLine.AgreementLineNumber);

                reservation.LineId = newLine.Id;
                reservation.LastUpdatedDate = DateTime.UtcNow;
            }

            if (reservationsToActuallyMove.Any())
            {
                logger.LogInformation(
                    "Reassigned {Count} reservation(s) from sibling line(s) to line {AgreementLineNumber} (warehouse: {Warehouse}).",
                    reservationsToActuallyMove.Count, newLine.AgreementLineNumber, newLine.Warehouse);
            }

            await dbContext.SaveChangesAsync();
        }
    }
}