using Microsoft.AspNetCore.Mvc;
using OF.Common.Infrastructure.OF;
using OF.Data.Database;
using OF.UI.Database;
using OF.UI.Identity;
using OF.WebApp.Features.Agreements;
using OF.WebApp.Features.Authorization;

namespace OF.WebApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BulkActionsController : ControllerBase
{
    private readonly IDataRepository _repository;
    private readonly IUserIdentity _userIdentity;
    private readonly ICoreFulfilmentEngine _coreEngine;

    public BulkActionsController(IDataRepository repository, IUserIdentity userIdentity, ICoreFulfilmentEngine coreEngine)
    {
        _repository = repository;
        _userIdentity = userIdentity;
        _coreEngine = coreEngine;
    }

    [HttpPost("depot-fulfil")]
    [DenyReadOnly]
    public IActionResult DepotFulfil([FromBody] BulkActionRequest request)
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null)
        {
            return Unauthorized();
        }

        if (!CanAccessAgreement(identity, request.HeaderId))
        {
            return NotFound();
        }

        var lines = _repository.GetLines(request.HeaderId).ToArray();
        var processed = 0;

        foreach (var line in lines)
        {
            if (!request.LineIds.Contains(line.Id)) continue;

            // Depot fulfilment is only valid for lines that have not begun fulfilment.
            // Keep this server-side so clients cannot bypass the UI eligibility rule.
            if (line.FulfilmentStatus != (int)OF.Data.Database.FulfilmentStatus.Unfulfilled)
            {
                continue;
            }

            var quantityToFulfil = (int)(line.Quantity - _repository.GetReservationSumForLine(line.Id));

            if (quantityToFulfil <= 0) continue;

            var reservation = new Reservation
            {
                AssetId = "DEPOTFULFIL",
                LineId = line.Id,
                ItemNumber = "DEPOTFULFIL",
                Warehouse = request.Warehouse ?? line.Warehouse,
                Quantity = quantityToFulfil,
                EffectiveQuantity = quantityToFulfil,
                IsDepotFulfilled = true,
            };

            _repository.CreateReservation(_userIdentity, reservation);
            _coreEngine.RecalculateStatusForLineAndHeader(line, identity.LoginName);
            processed++;
        }

        return Ok(new BulkActionProcessedResponse
        {
            Processed = processed,
        });
    }

    [HttpPost("rehire")]
    [DenyReadOnly]
    public IActionResult Rehire([FromBody] BulkActionRequest request)
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null)
        {
            return Unauthorized();
        }

        if (!CanAccessAgreement(identity, request.HeaderId))
        {
            return NotFound();
        }

        var lines = _repository.GetLines(request.HeaderId).ToArray();
        var processed = 0;

        foreach (var line in lines)
        {
            if (!request.LineIds.Contains(line.Id)) continue;

            if (!request.IncludeAlreadyFulfilled && line.FulfilmentStatus == (int)OF.Data.Database.FulfilmentStatus.FullyFulfiled)
            {
                continue;
            }

            // Prefer the item's generic; generic-only lines retain their persisted generic code.
            var genericCode = line.GenericItemNumber ?? line.ItemNumber;
            if (string.IsNullOrWhiteSpace(genericCode)) continue;

            var specific = string.IsNullOrWhiteSpace(line.ItemNumber)
                ? null
                : _repository.GetItem(line.ItemNumber);
            var matchedGeneric = specific != null
                ? _repository.GetGeneric(specific.GenericId)
                : _repository.GetGeneric(genericCode);

            if (matchedGeneric == null) continue;

            var alternatives = _repository.GetAlternativeOptions(matchedGeneric.GenericCode);
            if (alternatives.Length == 0) continue;

            if (request.IncludeAlreadyFulfilled)
            {
                _repository.DeleteReservationsForLine(line.Id);
            }

            var reservation = new Reservation
            {
                AssetId = alternatives[0].ItemNumber,
                LineId = line.Id,
                ItemNumber = alternatives[0].ItemNumber,
                Warehouse = request.Warehouse ?? line.Warehouse,
                Quantity = 1,
                EffectiveQuantity = 1,
                IsRehire = true,
            };

            _repository.CreateReservation(_userIdentity, reservation);
            _coreEngine.RecalculateStatusForLineAndHeader(line, identity.LoginName);
            processed++;
        }

        return Ok(new BulkActionProcessedResponse
        {
            Processed = processed,
        });
    }

    private bool CanAccessAgreement(User identity, int headerId)
    {
        var header = _repository.GetHeader(headerId);
        return header != null && AgreementDivisionAccess.CanAccess(identity, header.Division);
    }
}

public record BulkActionRequest
{
    public required int HeaderId { get; init; }
    public required int[] LineIds { get; init; }
    public string? Warehouse { get; init; }
    public bool IncludeAlreadyFulfilled { get; init; }
}

public sealed class BulkActionProcessedResponse
{
    public int Processed { get; init; }
}
