using Microsoft.Extensions.Logging;
using OF.Common.Infrastructure.IPG.Orders.Models.Orders;
using OF.Common.Infrastructure.OF;
using OF.Common.Utils;
using OF.Data;
using OF.Data.Database;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using static OF.Common.Enums;

namespace OF.Common.Infrastructure.IPG.Orders;

public class OrderManagementService
{
    private readonly IOrderManagementIntegration orderIntegration;
    private readonly ICoreDataRepository applicationRepository;
    private readonly ApplicationDbContext dbContext;
    private readonly ILogger<OrderManagementService> logger;

    private static readonly string[] _fulfilmentAttributesPrefix = new string[]
    {
        RemoveWhiteSpaces(Constants.M3LineText.ItemToPickText),
        RemoveWhiteSpaces(Constants.M3LineText.DepotFulfillesFrom)
    };

    public OrderManagementService(
        IOrderManagementIntegration orderIntegration,
        ICoreDataRepository applicationRepository,
        ApplicationDbContext dbContext,
        ILogger<OrderManagementService> logger)
    {
        this.orderIntegration = orderIntegration;
        this.applicationRepository = applicationRepository;
        this.dbContext = dbContext;
        this.logger = logger;
    }

    public async Task DeleteLine(Header header, Line line)
    {
        if (line.IsSubline)
        {
            using var __ = EnterScope(header, line);
            logger.LogInformation($"Sub-line {line.AgreementLineNumber} is deleted locally only.");
            line.ActivationStatus = (int)ActivationStatus.Activated;
            line.ActivationErrors = null;
            line.ActivationInstanceId = null;
            return;
        }

        var request = new OrderLineDeleteRequest()
        {
            AgreementLineNumber = line.AgreementLineIndex!.ToString()!,
            AgreementNumber = header.AgreementNumber!,
            Division = line.Division
        };

        using var _ = EnterScope(request, header, line);
        logger.LogInformation($"Deleting line {line.AgreementLineNumber}...");
        var response = await orderIntegration.OrderLineDelete(request);

        if (await HandleCallback(line, "Delete", response, request.CorrelationId))
        {
            try
            {
                // Need tests around this status stuff 

                var statusRequest = new OrderLineProcessStatusRequest()
                {
                    AgreementNumber = header.AgreementNumber!,
                    AgreementLineNumber = line.AgreementLineIndex.ToString()!,
                    Division = line.Division,
                    Facility = line.Facility,
                    OrderLineProcessStatus = Constants.IPG.ProcessStatus.LineInProgress
                };

                var headerStatusRequest = new OrderProcessStatusRequest()
                {
                    AgreementNumber = header.AgreementNumber!,
                    Division = header.Division,
                    Facility = header.Facility,
                    OrderProcessStatus = Constants.IPG.ProcessStatus.HeaderHasLinesInProgress
                };

                await orderIntegration.OrderLineProcessStatus(statusRequest);
                await orderIntegration.OrderProcessStatus(headerStatusRequest);
                logger.LogInformation($"Line {line.AgreementLineNumber} deleted!");
            }
            catch (Exception ex)
            {
                logger.LogWarning($"OrderLineProcessStatus[{Constants.IPG.ProcessStatus.LineInProgress}]: Delete Failed: {ex.FullMessage()}.");
            }
        }
    }

    public async Task CreateLine(Header header, Line line, Reservation reservation)
    {
        var request = new OrderLineCreateRequest()
        {
            AgreementLineType = line.AgreementLineType!,
            AgreementNumber = header.AgreementNumber!,
            CustomerSiteAccount = header.CustomerNumber!,
            CustomerSiteAddress = header.CustomerAddressCode!,
            DeliveryDate = line.DeliveryDate!.Value.ToString("yyyy-MM-dd"),
            RateType = line.RateType!,
            DeliveryStartTime = line.DeliveryDate!.Value.ToString("HH:mm:ss.fffZ"),
            Division = line.Division,
            Facility = line.Facility,
            FromWarehouse = reservation.Warehouse.EnsureWarehouseIsZero(),
            ItemNumber = reservation.IsDepotFulfilled ? line.ItemNumber! : reservation.ItemNumber,
            NumberOfShifts = line.NumberOfShifts!,
            OrderedQuantity = reservation.Quantity.ToString(),
            ValidFromDate = line.ValidFromDate!.ToString("yyyy-MM-dd"),
            ValidToDate = line.ValidToDate!.ToString("yyyy-MM-dd"),
            ItemAttributesAsText = await ComposeLineAttributes(line, reservation, default),
            LinkedAgreementLine = reservation.Id.ToString(),
            OrderItemRecordId = line.OrderLineNumber,
            OrderItemLine = line.OrderLineIndex?.ToString(),
            QuoteLineRecordId = line.QuoteLineNumber
        };

        using var _ = EnterScope(request, header, line, reservation);
        logger.LogInformation($"Creating line {line.AgreementLineNumber}");

        var response = await orderIntegration.OrderLineCreate(request);

        if (await HandleCallback(line, "Create", response, request.CorrelationId))
        {
            logger.LogInformation($"Line {line.AgreementLineNumber} created!");
        }
    }

    public async Task UpdateLine(Header header, Line line, Reservation reservation)
    {
        var request = new OrderLineUpdateRequest()
        {
            AgreementNumber = header.AgreementNumber!,
            AgreementLineNumber = line.AgreementLineIndex!.ToString()!,
            OrderedQuantity = reservation.Quantity.ToString(),
            FromWarehouse = reservation.Warehouse.EnsureWarehouseIsZero(),
            Division = line.Division,
            Facility = line.Facility,
            ItemAttributesAsText = await ComposeLineAttributes(line, reservation, default),
            LinkedAgreementLine = reservation.Id.ToString(),
            OrderItemRecordId = line.OrderLineNumber,
            OrderItemLine = line.OrderLineIndex?.ToString(),
            QuoteLineRecordId = line.QuoteLineNumber
        };
        using var _ = EnterScope(request, header, line, reservation);
        logger.LogInformation($"Updating line {line.AgreementLineNumber}");

        var response = await orderIntegration.OrderLineUpdate(request);

        if (await HandleCallback(line, "Update", response, request.CorrelationId))
        {
            try
            {
                // Need tests around this status stuff 

                var statusRequest = new OrderLineProcessStatusRequest()
                {
                    AgreementNumber = header.AgreementNumber!,
                    AgreementLineNumber = line.AgreementLineIndex.ToString()!,
                    Division = line.Division,
                    Facility = line.Facility,
                    OrderLineProcessStatus = Constants.IPG.ProcessStatus.LineInProgress
                };

                var headerStatusRequest = new OrderProcessStatusRequest()
                {
                    AgreementNumber = header.AgreementNumber!,
                    Division = header.Division,
                    Facility = header.Facility,
                    OrderProcessStatus = Constants.IPG.ProcessStatus.HeaderHasLinesInProgress
                };

                await orderIntegration.OrderLineProcessStatus(statusRequest);
                await orderIntegration.OrderProcessStatus(headerStatusRequest);
                logger.LogInformation($"Line {line.AgreementLineNumber} updated!");
            }
            catch (Exception ex)
            {
                logger.LogWarning($"OrderLineProcessStatus[{Constants.IPG.ProcessStatus.LineInProgress}]: Failed: {ex.FullMessage()}.");
                logger.LogWarning($"OrderProcessStatus[{Constants.IPG.ProcessStatus.HeaderHasLinesInProgress}]: Failed: {ex.FullMessage()}.");
            }
        }
    }

    public async Task ActivateOrder(Header header)
    {
        if (header.ActivationStatus == (int)ActivationStatus.Activated)
        {
            logger.LogInformation($"Order {header.AgreementNumber} already activated!");
            return;
        }

        if (header.AgreementNumber?.ToLower()?.StartsWith("a") == true)
        {
            logger.LogInformation($"Order {header.AgreementNumber} is already an agreement order!");
            header.ActivationStatus = (int)ActivationStatus.Activated;
            return;
        }

        var request = new OrderActivationRequest()
        {
            AgreementNumber = header.AgreementNumber!,
            Division = header.Division
        };
        using var _ = EnterScope(request, header);
        logger.LogInformation($"Activating header {header.AgreementNumber}...");

        var response = await orderIntegration.OrderActivation(request);

        if (await HandleCallback(header, "Activate", response, request.CorrelationId))
        {
            try
            {
                // Need tests around this status stuff 

                var statusRequest = new OrderProcessStatusRequest()
                {
                    AgreementNumber = header.AgreementNumber!,
                    Division = header.Division,
                    Facility = header.Facility,
                    OrderProcessStatus = Constants.IPG.ProcessStatus.HeaderHasLinesInProgress
                };

                await orderIntegration.OrderProcessStatus(statusRequest);
                logger.LogInformation($"Header {header.AgreementNumber} activated!");
            }
            catch (Exception ex)
            {
                logger.LogWarning($"OrderProcessStatus[{Constants.IPG.ProcessStatus.HeaderHasLinesInProgress}]: Failed: {ex.FullMessage()}.");
            }
        }
    }

    public async ValueTask<string> ComposeLineAttributes(Line line, Reservation reservation, CancellationToken cancellationToken)
    {
        var fulfillementAttribute = await CreateHandheldText(reservation, cancellationToken);
        var parts = line.Attributes?.Split(Constants.M3LineText.AttributeSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? Array.Empty<string>();
        var updatedAttributes = string.Join(Constants.M3LineText.AttributeSeparator + " ", parts
            .Where(x => !_fulfilmentAttributesPrefix
                .Any(prefix => RemoveWhiteSpaces(x).StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase)))
            .Append(fulfillementAttribute));
        return updatedAttributes;
    }

    private ValueTask<string> CreateHandheldText(Reservation reservation, CancellationToken cancellationToken)
    {
        return reservation.IsDepotFulfilled
            ? new(CreateDepotFulfilledHandheldText(reservation, cancellationToken))
            : new(reservation.GetItemToPickAttributeMessage());
    }

    private async Task<string> CreateDepotFulfilledHandheldText(Reservation reservation, CancellationToken cancellationToken)
    {
        var warehouse = await applicationRepository.GetWarehouseByCode(reservation.Warehouse, cancellationToken);
        return reservation.GetDepotFulfilledAttributeMessage(warehouse?.Warehouse);
    }

    private async Task<bool> HandleCallback(IActivatable entity, string requestType, HttpResponseMessage response, string correlationId)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            var issue = $"{DateTime.UtcNow.ToString("yyyyMMddHHmmss")}: Fail[{requestType}]: ({entity.GetType().Name}: {entity.Id}) -> {response.StatusCode}:{body};";
            logger.LogError(issue);
            entity.ActivationErrors = issue;
            entity.ActivationStatus = (int)ActivationStatus.Failed;
            entity.ActivationInstanceId = null;
        }
        else
        {
            entity.ActivationErrors = null;
            entity.ActivationInstanceId = correlationId;
        }

        return response.IsSuccessStatusCode;
    }

    private IDisposable EnterScope(Header header, Line line, [CallerMemberName] string action = "")
    {
        return logger.BeginScope(new { Action = action, Header = GetInfo(header), Line = GetInfo(line) })!;
    }

    private IDisposable EnterScope(object request, Header header, Line line, [CallerMemberName] string action = "")
    {
        return logger.BeginScope(new { Action = action, Request = request, Header = GetInfo(header), Line = GetInfo(line) })!;
    }

    private IDisposable EnterScope(object request, Header header, Line line, Reservation reservation, [CallerMemberName] string action = "")
    {
        return logger.BeginScope(new { Action = action, Header = GetInfo(header), Line = GetInfo(line), Reservation = GetInfo(reservation) })!;
    }

    private IDisposable EnterScope(object request, Header header, [CallerMemberName] string action = "")
    {
        return logger.BeginScope(new { Action = action, Header = GetInfo(header) })!;
    }

    [return: NotNullIfNotNull(nameof(str))]
    private static string? RemoveWhiteSpaces(string? str)
        => string.IsNullOrEmpty(str)
            ? str
            : new(str.Where(c => !char.IsWhiteSpace(c)).ToArray());

    private static object GetInfo(Line line)
        => new
        {
            line.Id,
            line.AgreementLineNumber,
            line.AgreementLineIndex,
            line.AgreementLineType,
            line.Quantity,
            line.QuantityFulfilled,
            line.Division,
            line.Facility,
            line.Warehouse,
            line.ValidFromDate,
            line.ValidToDate,
            line.CollectionDate,
            line.DeliveryDate,
            FulfilmentStatus = (FulfilmentStatus)line.FulfilmentStatus,
            ActivationStatus = (ActivationStatus)line.ActivationStatus,
            line.OrderSource
        };

    private static object GetInfo(Reservation reservation)
        => new
        {
            reservation.Id,
            reservation.LineId,
            reservation.AssetId,
            reservation.ItemNumber,
            reservation.Warehouse,
            reservation.Quantity,
            reservation.ActualItemNumber,
            reservation.ActualQuantity,
            reservation.EffectiveQuantity,
            reservation.IsConfirmed,
            reservation.IsDepotFulfilled,
            reservation.IsIndividualItem,
            reservation.IsRehire,
            reservation.LastUpdatedBy,
            reservation.LastUpdatedDate,
        };

    private static object GetInfo(Header header)
        => new
        {
            header.Id,
            header.AgreementNumber,
            header.Division,
            header.Facility,
            header.OnHireDate,
            header.OffHireDate,
            header.LastUpdatedBy,
            header.LastUpdatedDate,
            header.CustomerNumber,
            header.QuoteNumber,
            header.QuotePublicId,
            FulfilmentStatus = (FulfilmentStatus)header.FulfilmentStatus,
            ActivationStatus = (ActivationStatus)header.ActivationStatus,
            header.OrderSource
        };
}
