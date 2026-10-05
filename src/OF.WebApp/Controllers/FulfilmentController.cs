using Microsoft.AspNetCore.Mvc;
using OF.Common.Infrastructure.OF;
using OF.Data.Database;
using OF.UI.Database;
using OF.UI.Identity;
using OF.UI.Models;
using OF.WebApp.Features.Agreements;
using OF.WebApp.Features.Authorization;
using OF.WebApp.Features.Divisions;

namespace OF.WebApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FulfilmentController : ControllerBase
{
    private readonly IDataRepository _repository;
    private readonly IUserIdentity _userIdentity;
    private readonly ICoreFulfilmentEngine _coreEngine;

    public FulfilmentController(
        IDataRepository repository,
        IUserIdentity userIdentity,
        ICoreFulfilmentEngine coreEngine)
    {
        _repository = repository;
        _userIdentity = userIdentity;
        _coreEngine = coreEngine;
    }

    [HttpGet("stock/serialized")]
    public IActionResult GetSerializedStock(
        [FromQuery] int lineId,
        [FromQuery] string? warehouse = null,
        [FromQuery] string? attributes = null)
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null)
        {
            return Unauthorized();
        }

        var divisions = identity.Division?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
        var attributeArray = attributes?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? [];

        var stock = _repository.GetSerializedStock(lineId, divisions, warehouse ?? string.Empty, attributeArray);
        return Ok(stock);
    }

    [HttpGet("stock/nonserialized")]
    public IActionResult GetNonSerializedStock(
        [FromQuery] int lineId,
        [FromQuery] string? warehouse = null,
        [FromQuery] string? attributes = null)
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null)
        {
            return Unauthorized();
        }

        var divisions = identity.Division?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
        var attributeArray = attributes?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? [];

        var stock = _repository.GetNonSerializedStock(lineId, divisions, warehouse ?? string.Empty, attributeArray);
        return Ok(stock);
    }

    [HttpPost("stock/nonserialized/reservations")]
    [DenyReadOnly]
    public IActionResult ReserveNonSerializedStock([FromBody] ReserveNonSerializedStockRequest request)
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null)
        {
            return Unauthorized();
        }

        var header = _repository.GetHeaderForLineId(request.LineId);
        if (header == null || !AgreementDivisionAccess.CanAccess(identity, header.Division))
        {
            return NotFound();
        }

        var line = _repository.GetLine(request.LineId);
        if (line == null)
        {
            return NotFound();
        }

        if (request.Quantity <= 0)
        {
            return BadRequest(new StockReservationErrorResponse("Quantity must be greater than zero."));
        }

        var divisions = identity.IsSuperAdmin
            ? _repository.GetWarehouseDivisionCodes()
            : DivisionAccess.Resolve(identity, identity.Division, [',']);
        var attributes = line.Attributes?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
        var stock = _repository.GetNonSerializedStock(line.Id, divisions, request.Warehouse, attributes)
            .FirstOrDefault(item =>
                string.Equals(item.Asset.ItemNumber, request.ItemNumber, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.Asset.Warehouse, request.Warehouse, StringComparison.OrdinalIgnoreCase));

        if (stock == null)
        {
            return NotFound();
        }

        var startDate = (line.DeliveryDate ?? line.ValidFromDate).Date;
        var endDate = (line.TerminationDate ?? line.ValidToDate).Date;
        var available = GetMinimumAvailable(stock, startDate, endDate);
        if (request.Quantity > available)
        {
            return Conflict(new StockReservationErrorResponse(
                $"Only {available} stock units are available for the selected period.",
                available));
        }

        var reservation = new Reservation
        {
            AssetId = stock.Asset.ItemNumber,
            ItemNumber = stock.Asset.ItemNumber,
            Warehouse = stock.Asset.Warehouse,
            LineId = line.Id,
            Quantity = request.Quantity,
            EffectiveQuantity = request.Quantity,
        };

        var created = _repository.CreateReservation(_userIdentity, reservation);
        _coreEngine.RecalculateStatusForLineAndHeader(line, identity.LoginName);

        return Created($"/api/reservations/{created.Id}", new ReserveNonSerializedStockResponse(
            created.Id,
            created.ItemNumber,
            created.Warehouse,
            created.Quantity,
            created.EffectiveQuantity));
    }

    private static int GetMinimumAvailable(NonSerializedQueryResult stock, DateTime startDate, DateTime endDate)
    {
        if (endDate < startDate)
        {
            endDate = startDate;
        }

        var changes = new SortedDictionary<DateTime, decimal>();
        foreach (var reservation in stock.Reservations.Where(reservation => !reservation.IsConfirmed))
        {
            var reservationStart = (reservation.DeliveryDate ?? reservation.ValidFromDate ?? DateTime.MinValue).Date;
            var reservationEnd = (reservation.TerminationDate ?? reservation.ValidToDate ?? DateTime.MaxValue).Date;
            if (reservationEnd < startDate || reservationStart > endDate)
            {
                continue;
            }

            var effectiveStart = reservationStart < startDate ? startDate : reservationStart;
            var effectiveEnd = reservationEnd > endDate ? endDate : reservationEnd;
            changes[effectiveStart] = changes.GetValueOrDefault(effectiveStart) + reservation.Quantity;
            if (effectiveEnd < DateTime.MaxValue.Date)
            {
                var afterEnd = effectiveEnd.AddDays(1);
                changes[afterEnd] = changes.GetValueOrDefault(afterEnd) - reservation.Quantity;
            }
        }

        decimal reserved = 0;
        decimal maximumReserved = 0;
        foreach (var change in changes)
        {
            reserved += change.Value;
            maximumReserved = Math.Max(maximumReserved, reserved);
        }

        var available = stock.Asset.StockQuantity - stock.Asset.AllocatedQuantity - maximumReserved;
        return Math.Max(0, decimal.ToInt32(decimal.Floor(available)));
    }
}

public sealed record ReserveNonSerializedStockRequest(
    int LineId,
    string ItemNumber,
    string Warehouse,
    int Quantity);

public sealed record ReserveNonSerializedStockResponse(
    int ReservationId,
    string ItemNumber,
    string Warehouse,
    int Quantity,
    double EffectiveQuantity);

public sealed record StockReservationErrorResponse(string Message, int? Available = null);
