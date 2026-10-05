using Microsoft.AspNetCore.Mvc;
using OF.Common.Infrastructure.OF;
using OF.Data.Database;
using OF.UI.Database;
using OF.UI.Identity;
using OF.WebApp.Features.Agreements;
using OF.WebApp.Features.Authorization;
using OF.WebApp.Features.Divisions;

namespace OF.WebApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReservationsController : ControllerBase
{
    private readonly IDataRepository _repository;
    private readonly IUserIdentity _userIdentity;
    private readonly ICoreFulfilmentEngine _coreEngine;

    public ReservationsController(
        IDataRepository repository,
        IUserIdentity userIdentity,
        ICoreFulfilmentEngine coreEngine)
    {
        _repository = repository;
        _userIdentity = userIdentity;
        _coreEngine = coreEngine;
    }

    [HttpGet("header/{headerId:int}")]
    public IActionResult GetReservationsForHeader(int headerId)
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null)
        {
            return Unauthorized();
        }

        var header = _repository.GetHeader(headerId);
        if (header == null || !AgreementDivisionAccess.CanAccess(identity, header.Division))
        {
            return NotFound();
        }

        var reservations = _repository.GetReservationsForHeader(headerId)
            .ToList()
            .Select(ToResponse)
            .ToList();
        return Ok(reservations);
    }

    [HttpGet("{id:int}")]
    public IActionResult GetReservation(int id)
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null)
        {
            return Unauthorized();
        }

        var reservation = _repository.GetReservation(id);
        if (reservation == null)
        {
            return NotFound();
        }

        if (!CanAccessAgreementForLine(identity, reservation.LineId))
        {
            return NotFound();
        }

        return Ok(ToResponse(reservation));
    }

    [HttpPost]
    [DenyReadOnly]
    public IActionResult CreateReservation([FromBody] CreateReservationRequest request)
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null)
        {
            return Unauthorized();
        }

        if (!CanAccessAgreementForLine(identity, request.LineId))
        {
            return NotFound();
        }

        var reservation = new Reservation
        {
            AssetId = request.AssetId,
            LineId = request.LineId,
            ItemNumber = request.ItemNumber,
            Quantity = request.Quantity,
            EffectiveQuantity = request.Quantity,
            Warehouse = request.Warehouse,
            Notes = request.Notes,
            IsConfirmed = request.IsConfirmed,
            IsDepotFulfilled = request.IsDepotFulfilled,
            IsRehire = request.IsRehire,
        };

        var asset = _repository.GetAsset(reservation.AssetId);
        if (asset != null)
        {
            if (!DivisionAccess.CanAccess(identity, asset.Division))
            {
                return NotFound();
            }
        }
        else if (reservation.IsIndividualItem)
        {
            return NotFound();
        }

        var line = _repository.GetLine(request.LineId);
        if (line == null)
        {
            return NotFound();
        }

        var created = _repository.CreateReservation(_userIdentity, reservation);
        _coreEngine.RecalculateStatusForLineAndHeader(line, identity.LoginName);
        return CreatedAtAction(nameof(GetReservation), new { id = created.Id }, ToResponse(created));
    }

    [HttpDelete("{id:int}")]
    [DenyReadOnly]
    public IActionResult DeleteReservation(int id)
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null)
        {
            return Unauthorized();
        }

        var reservation = _repository.GetReservation(id);
        if (reservation == null || !CanAccessAgreementForLine(identity, reservation.LineId))
        {
            return NotFound();
        }

        var line = _repository.GetLine(reservation.LineId);
        if (line == null)
        {
            return NotFound();
        }

        var deleted = _repository.DeleteReservation(id);
        if (deleted == null)
        {
            return NotFound();
        }

        _coreEngine.RecalculateStatusForLineAndHeader(line, identity.LoginName);
        return NoContent();
    }

    private bool CanAccessAgreementForLine(User identity, int lineId)
    {
        var header = _repository.GetHeaderForLineId(lineId);
        return header != null && AgreementDivisionAccess.CanAccess(identity, header.Division);
    }

    private static ReservationResponse ToResponse(Reservation reservation) => new()
    {
        Id = reservation.Id,
        AssetId = reservation.AssetId,
        Notes = reservation.Notes,
        ItemNumber = reservation.ItemNumber,
        Quantity = reservation.Quantity,
        Warehouse = reservation.Warehouse,
        LineId = reservation.LineId,
        LastUpdatedBy = reservation.LastUpdatedBy,
        LastUpdatedDate = reservation.LastUpdatedDate,
        IsDepotFulfilled = reservation.IsDepotFulfilled,
        IsRehire = reservation.IsRehire,
        EffectiveQuantity = reservation.EffectiveQuantity,
        IsConfirmed = reservation.IsConfirmed,
        ActualAssetId = reservation.ActualAssetId,
        ActualItemNumber = reservation.ActualItemNumber,
        ActualQuantity = reservation.ActualQuantity,
        IsIndividualItem = reservation.IsIndividualItem,
    };
}

public sealed class ReservationResponse
{
    public bool IsIndividualItem { get; init; }
    public int Id { get; init; }
    public string AssetId { get; init; } = string.Empty;
    public string? Notes { get; init; }
    public string ItemNumber { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public string Warehouse { get; init; } = string.Empty;
    public int LineId { get; init; }
    public string? LastUpdatedBy { get; init; }
    public DateTime? LastUpdatedDate { get; init; }
    public bool IsDepotFulfilled { get; init; }
    public bool IsRehire { get; init; }
    public double EffectiveQuantity { get; init; }
    public bool IsConfirmed { get; init; }
    public string? ActualAssetId { get; init; }
    public string? ActualItemNumber { get; init; }
    public double? ActualQuantity { get; init; }
}

public record CreateReservationRequest
{
    public required string AssetId { get; init; }
    public required int LineId { get; init; }
    public required string ItemNumber { get; init; }
    public required int Quantity { get; init; }
    public required string Warehouse { get; init; }
    public string? Notes { get; init; }
    public bool IsConfirmed { get; init; }
    public bool IsDepotFulfilled { get; init; }
    public bool IsRehire { get; init; }
}
