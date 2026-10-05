using Microsoft.AspNetCore.Mvc;
using OF.Data.Database;
using OF.UI.Database;
using OF.UI.Identity;
using OF.WebApp.Features.Divisions;
using OF.WebApp.Features.Authorization;
using OF.WebApp.Features.Ringfences;

namespace OF.WebApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RingfenceController : ControllerBase
{
    private const int MaximumBatchAssetIds = 250;

    private readonly IDataRepository _repository;
    private readonly IUserIdentity _userIdentity;
    private readonly TimeProvider _timeProvider;

    public RingfenceController(
        IDataRepository repository,
        IUserIdentity userIdentity,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _userIdentity = userIdentity;
        _timeProvider = timeProvider;
    }

    [HttpGet]
    public IActionResult GetRingfences()
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null)
        {
            return Unauthorized();
        }

        var ringfences = _repository.GetRingfences()
            .AsEnumerable()
            .Where(ringfence => CanAccessRingfence(identity, ringfence))
            .OrderByDescending(r => r.FromDate)
            .ToList();

        var assetCounts = ringfences.Count == 0
            ? new Dictionary<int, int>()
            : _repository.GetRingfenceItemCounts(ringfences.Select(ringfence => ringfence.Id).ToArray())
                ?? new Dictionary<int, int>();

        var response = ringfences
            .Select(r => new RingfenceListItemResponse
            {
                Id = r.Id,
                Title = r.Title,
                FromDate = r.FromDate,
                ToDate = r.ToDate,
                Divisions = r.Divisions,
                Warehouse = r.Warehouse,
                Owner = r.Owner,
                CreatedBy = r.CreatedBy,
                CreatedAt = r.CreatedAt,
                AssetCount = assetCounts.GetValueOrDefault(r.Id),
            })
            .ToList();

        return Ok(response);
    }

    [HttpGet("{id:int}")]
    public IActionResult GetRingfence(int id)
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null)
        {
            return Unauthorized();
        }

        var ringfence = _repository.GetRingfence(id);
        if (!CanAccessRingfence(identity, ringfence))
        {
            return NotFound();
        }

        var items = _repository.GetRingfenceItems(id).ToList();
        var assets = _repository.GetAssetsForRingfence(id)
            ?.AsEnumerable()
            ?.Select(RingfenceResponseMapper.MapAsset)
            .OrderBy(asset => asset.Id)
            .ToList() ?? [];

        return Ok(new RingfenceDetailResponse
        {
            Ringfence = RingfenceResponseMapper.MapRingfence(ringfence!),
            Items = items.Select(RingfenceResponseMapper.MapItem).ToList(),
            Assets = assets,
        });
    }

    [HttpPost]
    [DenyReadOnly]
    public IActionResult CreateRingfence([FromBody] CreateRingfenceRequest request)
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null)
        {
            return Unauthorized();
        }

        var owner = ResolveOwner(request.Owner, existingOwner: null, identity);
        var warehouse = ResolveWarehouse(request.Warehouse, existingWarehouse: null);
        var validationErrors = ValidateRingfenceWrite(
            request.Title,
            request.FromDate,
            request.ToDate,
            request.Divisions,
            owner,
            warehouse);
        if (validationErrors != null)
        {
            return RingfenceValidationProblem(validationErrors);
        }

        if (!DivisionAccess.CanAssignAll(identity, request.Divisions, [',']))
        {
            return Forbid();
        }

        var referenceValidationErrors = ValidateRingfenceReferences(identity, request.Divisions, owner!, warehouse!);
        if (referenceValidationErrors != null)
        {
            return RingfenceValidationProblem(referenceValidationErrors);
        }

        var title = request.Title.Trim();
        if (HasDuplicateTitle(title))
        {
            return RingfenceValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["title"] = ["A ringfence with this name already exists."],
                },
                StatusCodes.Status409Conflict,
                "Ringfence name already exists.");
        }

        var ringfence = new Ringfence(identity.LoginName)
        {
            Title = title,
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            Divisions = request.Divisions,
            Warehouse = warehouse!,
            Owner = owner!,
        };

        var created = _repository.CreateRingfence(_userIdentity, ringfence);
        return CreatedAtAction(
            nameof(GetRingfence),
            new { id = created.Id },
            RingfenceResponseMapper.MapRingfence(created));
    }

    [HttpPut("{id:int}")]
    [DenyReadOnly]
    public IActionResult UpdateRingfence(int id, [FromBody] UpdateRingfenceRequest request)
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null)
        {
            return Unauthorized();
        }

        var ringfence = _repository.GetRingfence(id);
        if (!CanAccessRingfence(identity, ringfence))
        {
            return NotFound();
        }

        var owner = ResolveOwner(request.Owner, ringfence.Owner, identity);
        var warehouse = ResolveWarehouse(request.Warehouse, ringfence.Warehouse);
        var validationErrors = ValidateRingfenceWrite(
            request.Title,
            request.FromDate,
            request.ToDate,
            request.Divisions,
            owner,
            warehouse,
            ringfence);
        if (validationErrors != null)
        {
            return RingfenceValidationProblem(validationErrors);
        }

        if (!HasSameDivisionSelection(ringfence.Divisions, request.Divisions) &&
            !DivisionAccess.CanAssignAll(identity, request.Divisions, [',']))
        {
            return Forbid();
        }

        var referenceValidationErrors = ValidateRingfenceReferences(identity, request.Divisions, owner!, warehouse!, ringfence);
        if (referenceValidationErrors != null)
        {
            return RingfenceValidationProblem(referenceValidationErrors);
        }

        var title = request.Title.Trim();
        if (HasDuplicateTitle(title, id))
        {
            return RingfenceValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["title"] = ["A ringfence with this name already exists."],
                },
                StatusCodes.Status409Conflict,
                "Ringfence name already exists.");
        }

        ringfence.Title = title;
        ringfence.FromDate = request.FromDate;
        ringfence.ToDate = request.ToDate;
        ringfence.Divisions = request.Divisions;
        ringfence.Warehouse = warehouse;
        ringfence.Owner = owner!;

        var updated = _repository.UpdateRingfence(_userIdentity, ringfence);
        return Ok(RingfenceResponseMapper.MapRingfence(updated));
    }

    [HttpDelete("{id:int}")]
    [DenyReadOnly]
    public IActionResult DeleteRingfence(int id)
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null)
        {
            return Unauthorized();
        }

        var ringfence = _repository.GetRingfence(id);
        if (!CanAccessRingfence(identity, ringfence))
        {
            return NotFound();
        }

        _repository.DeleteRingfence(ringfence);
        return NoContent();
    }

    [HttpPost("{id:int}/items")]
    [DenyReadOnly]
    public IActionResult AddItemToRingfence(int id, [FromBody] AddRingfenceItemRequest request)
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null)
        {
            return Unauthorized();
        }

        var ringfence = _repository.GetRingfence(id);
        if (!CanAccessRingfence(identity, ringfence))
        {
            return NotFound();
        }

        var assetId = request.AssetId?.Trim();
        if (string.IsNullOrWhiteSpace(assetId))
        {
            return RingfenceValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["assetId"] = ["An asset ID is required."],
                },
                title: "Ringfence asset validation failed.");
        }

        var existing = _repository.GetRingfenceItems(id)
            .AsEnumerable()
            .FirstOrDefault(item => string.Equals(item.AssetId, assetId, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            return Ok(RingfenceResponseMapper.MapItem(existing));
        }

        var asset = _repository.GetAsset(assetId);
        if (asset == null || !DivisionAccess.CanAccess(identity, asset.Division))
        {
            return NotFound();
        }

        var created = _repository.AddAssetToRingfence(_userIdentity, ringfence!, asset.Id);
        return Ok(RingfenceResponseMapper.MapItem(created));
    }

    [HttpPost("{id:int}/items/preflight")]
    [ReadOnlyQuery]
    public async Task<IActionResult> PreflightItemsForRingfence(
        int id,
        [FromBody] RingfenceItemBatchRequest request,
        CancellationToken cancellationToken)
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null)
        {
            return Unauthorized();
        }

        var ringfence = _repository.GetRingfence(id);
        if (!CanAccessRingfence(identity, ringfence))
        {
            return NotFound();
        }

        if (!TryNormaliseAssetIds(request.AssetIds, out var assetIds, out var validationError))
        {
            return RingfenceValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["assetIds"] = [validationError],
                },
                title: "Ringfence asset validation failed.");
        }

        return Ok(await BuildItemBatchResponseAsync(identity, ringfence!, assetIds, cancellationToken));
    }

    [HttpPost("{id:int}/items/batch")]
    [DenyReadOnly]
    public async Task<IActionResult> AddItemsToRingfence(
        int id,
        [FromBody] RingfenceItemBatchRequest request,
        CancellationToken cancellationToken)
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null)
        {
            return Unauthorized();
        }

        var ringfence = _repository.GetRingfence(id);
        if (!CanAccessRingfence(identity, ringfence))
        {
            return NotFound();
        }

        if (!TryNormaliseAssetIds(request.AssetIds, out var assetIds, out var validationError))
        {
            return RingfenceValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["assetIds"] = [validationError],
                },
                title: "Ringfence asset validation failed.");
        }

        var response = await BuildItemBatchResponseAsync(identity, ringfence!, assetIds, cancellationToken);
        if (response.UnavailableAssetIds.Count > 0)
        {
            return BadRequest(response);
        }

        if (response.RequiresOverlapAcknowledgement && !request.AcknowledgeOverlaps)
        {
            return Conflict(response);
        }

        foreach (var assetId in response.ReadyAssetIds)
        {
            var item = _repository.AddAssetToRingfence(_userIdentity, ringfence!, assetId);
            response.AddedAssetIds.Add(item.AssetId);
        }

        return Ok(response);
    }

    [HttpPost("{id:int}/overlaps")]
    [ReadOnlyQuery]
    public async Task<IActionResult> GetItemOverlaps(
        int id,
        [FromBody] RingfenceOverlapRequest request,
        CancellationToken cancellationToken)
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null)
        {
            return Unauthorized();
        }

        var ringfence = _repository.GetRingfence(id);
        if (!CanAccessRingfence(identity, ringfence))
        {
            return NotFound();
        }

        var assetIds = request.AssetIds
            .Where(assetId => !string.IsNullOrWhiteSpace(assetId))
            .Select(assetId => assetId.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (assetIds.Count == 0)
        {
            return Ok(new RingfenceOverlapResponse());
        }

        var overlaps = await _repository.GetOverlappingRingfenceDetailsAsync(
            id,
            assetIds,
            ringfence!.FromDate,
            ringfence.ToDate,
            cancellationToken);

        return Ok(new RingfenceOverlapResponse
        {
            Overlaps = overlaps.Select(RingfenceResponseMapper.MapOverlap).ToList(),
        });
    }

    [HttpDelete("{id:int}/items/{assetId}")]
    [DenyReadOnly]
    public IActionResult RemoveItemFromRingfence(int id, string assetId)
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null)
        {
            return Unauthorized();
        }

        var ringfence = _repository.GetRingfence(id);
        if (!CanAccessRingfence(identity, ringfence))
        {
            return NotFound();
        }

        var items = _repository.GetRingfenceItems(id);
        var item = items.FirstOrDefault(i => i.AssetId == assetId);
        if (item == null)
        {
            return NotFound();
        }

        _repository.DeleteRingfenceItem(item);
        return NoContent();
    }

    private static bool CanAccessRingfence(User identity, Ringfence? ringfence) =>
        ringfence != null && DivisionAccess.CanAccessAny(identity, ringfence.Divisions, [',']);

    private IReadOnlyDictionary<string, string[]>? ValidateRingfenceWrite(
        string? title,
        DateTime fromDate,
        DateTime toDate,
        string? divisions,
        string? owner,
        string? warehouse,
        Ringfence? existingRingfence = null)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(title))
        {
            errors["title"] = ["Ringfence name cannot be empty."];
        }

        if (fromDate.Date > toDate.Date)
        {
            errors["fromDate"] = ["The ringfence start date cannot be after the end date."];
            errors["toDate"] = ["The ringfence end date cannot be before the start date."];
        }

        if (string.IsNullOrWhiteSpace(owner))
        {
            errors["owner"] = ["Ringfence owner cannot be empty."];
        }

        if (string.IsNullOrWhiteSpace(warehouse) &&
            (existingRingfence == null || !string.IsNullOrWhiteSpace(existingRingfence.Warehouse)))
        {
            errors["warehouse"] = ["Ringfence warehouse cannot be empty."];
        }

        if (!HasAtLeastOneDivision(divisions))
        {
            errors["divisions"] = ["Ringfence division cannot be empty."];
        }

        var today = _timeProvider.GetUtcNow().UtcDateTime.Date;
        if (existingRingfence == null)
        {
            if (fromDate.Date < today)
            {
                errors["fromDate"] = ["Ringfence start date cannot be in the past."];
            }

            if (toDate.Date < today)
            {
                errors["toDate"] = ["Ringfence end date cannot be in the past."];
            }

            return errors.Count == 0 ? null : errors;
        }

        // Historical records remain editable, but their historical period cannot be
        // rewritten. An active record may keep its original past start date while
        // its end date or other metadata is updated.
        var isChangingFromDate = existingRingfence.FromDate.Date != fromDate.Date;
        var isChangingToDate = existingRingfence.ToDate.Date != toDate.Date;
        if (isChangingFromDate && fromDate.Date < today)
        {
            errors["fromDate"] = ["Ringfence start date cannot be changed to a date in the past."];
        }

        if (isChangingToDate && toDate.Date < today)
        {
            errors["toDate"] = ["Ringfence end date cannot be changed to a date in the past."];
        }

        return errors.Count == 0 ? null : errors;
    }

    private IReadOnlyDictionary<string, string[]>? ValidateRingfenceReferences(
        User identity,
        string? divisions,
        string owner,
        string? warehouse,
        Ringfence? existingRingfence = null)
    {
        var selectedDivisions = DivisionAccess.Resolve(identity, divisions, [',']);
        var selectedDivisionLookup = selectedDivisions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var errors = new Dictionary<string, string[]>();

        var retainsExistingDivisions = existingRingfence != null &&
            HasSameDivisionSelection(existingRingfence.Divisions, divisions);
        var preservesExistingWarehouse = retainsExistingDivisions &&
            HasSameText(existingRingfence!.Warehouse, warehouse);
        var preservesExistingOwner = retainsExistingDivisions &&
            HasSameText(existingRingfence!.Owner, owner);

        if (!preservesExistingWarehouse)
        {
            var warehouseIsInSelectedDivision = (_repository.GetWarehouses(selectedDivisions) ?? [])
                .Any(candidate =>
                    !string.IsNullOrWhiteSpace(candidate.WarehouseCode) &&
                    !string.IsNullOrWhiteSpace(candidate.DivisionCode) &&
                    string.Equals(candidate.WarehouseCode.Trim(), warehouse, StringComparison.OrdinalIgnoreCase) &&
                    selectedDivisionLookup.Contains(candidate.DivisionCode.Trim()));
            if (!warehouseIsInSelectedDivision)
            {
                errors["warehouse"] = ["Select a warehouse in one of the chosen ringfence divisions."];
            }
            else if (!HasRingfenceWarehouseSuffix(warehouse))
            {
                errors["warehouse"] = ["Select a warehouse code that ends in 0."];
            }
        }

        if (!preservesExistingOwner)
        {
            var ownerIsCurrentUser = string.Equals(owner, identity.LoginName?.Trim(), StringComparison.OrdinalIgnoreCase);
            var ownerIsAssignedToSelectedDivision = !ownerIsCurrentUser &&
                (_repository.GetUsers()?.AsEnumerable() ?? [])
                    .Any(candidate =>
                        !string.IsNullOrWhiteSpace(candidate.LoginName) &&
                        string.Equals(candidate.LoginName.Trim(), owner, StringComparison.OrdinalIgnoreCase) &&
                        UserHasAnySelectedDivision(candidate, selectedDivisionLookup));
            if (!ownerIsCurrentUser && !ownerIsAssignedToSelectedDivision)
            {
                errors["owner"] = ["Select an owner assigned to one of the chosen ringfence divisions."];
            }
        }

        return errors.Count == 0 ? null : errors;
    }

    private static bool UserHasAnySelectedDivision(User user, ISet<string> selectedDivisionLookup) =>
        user.Division?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(division => selectedDivisionLookup.Contains(division)) == true;

    private static bool HasSameDivisionSelection(string? first, string? second)
    {
        var firstDivisions = ParseDivisionSelection(first);
        return firstDivisions.SetEquals(ParseDivisionSelection(second));
    }

    private static HashSet<string> ParseDivisionSelection(string? divisions) =>
        divisions?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(division => division.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase)
        ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private static bool HasSameText(string? first, string? second) =>
        string.Equals(first?.Trim(), second?.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool HasRingfenceWarehouseSuffix(string? warehouse) =>
        warehouse?.Trim().EndsWith('0') == true;

    private static string? ResolveOwner(string? requestedOwner, string? existingOwner, User identity)
    {
        if (!string.IsNullOrWhiteSpace(requestedOwner))
        {
            return requestedOwner.Trim();
        }

        // Older clients did not send Owner. Keep an existing value on update;
        // creation and legacy blank records default to the authenticated user.
        return !string.IsNullOrWhiteSpace(existingOwner) ? existingOwner.Trim() : identity.LoginName?.Trim();
    }

    private static string? ResolveWarehouse(string? requestedWarehouse, string? existingWarehouse)
    {
        if (!string.IsNullOrWhiteSpace(requestedWarehouse))
        {
            return requestedWarehouse.Trim();
        }

        // Older clients may omit Warehouse on an update. Preserve even a legacy
        // blank value so an otherwise valid metadata edit remains possible.
        return existingWarehouse?.Trim();
    }

    private ObjectResult RingfenceValidationProblem(
        IReadOnlyDictionary<string, string[]> errors,
        int statusCode = StatusCodes.Status400BadRequest,
        string? title = null)
    {
        var message = errors.Values.SelectMany(messages => messages).First();
        var problem = new ValidationProblemDetails(new Dictionary<string, string[]>(errors))
        {
            Status = statusCode,
            Title = title ?? "Ringfence validation failed.",
            Detail = message,
        };
        problem.Extensions["message"] = message;

        var result = new ObjectResult(problem)
        {
            StatusCode = statusCode,
        };
        result.ContentTypes.Add("application/problem+json");
        return result;
    }

    private bool HasDuplicateTitle(string title, int? excludedRingfenceId = null) =>
        _repository.GetRingfences()?
            .AsEnumerable()
            .Any(ringfence =>
                ringfence.Id != excludedRingfenceId &&
                string.Equals(ringfence.Title?.Trim(), title, StringComparison.OrdinalIgnoreCase))
        == true;

    private static bool HasAtLeastOneDivision(string? divisions) =>
        divisions?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(division => !string.IsNullOrWhiteSpace(division))
        == true;

    private static bool TryNormaliseAssetIds(
        IEnumerable<string>? requestedAssetIds,
        out List<string> assetIds,
        out string validationError)
    {
        assetIds = requestedAssetIds?
            .Where(assetId => !string.IsNullOrWhiteSpace(assetId))
            .Select(assetId => assetId.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
        ?? [];

        if (assetIds.Count == 0)
        {
            validationError = "At least one asset ID is required.";
            return false;
        }

        if (assetIds.Count > MaximumBatchAssetIds)
        {
            validationError = $"A maximum of {MaximumBatchAssetIds} asset IDs can be processed at once.";
            return false;
        }

        validationError = string.Empty;
        return true;
    }

    private async Task<RingfenceItemBatchResponse> BuildItemBatchResponseAsync(
        User identity,
        Ringfence ringfence,
        IReadOnlyCollection<string> requestedAssetIds,
        CancellationToken cancellationToken)
    {
        var requestedAssetIdKeys = requestedAssetIds
            .Select(assetId => assetId.ToUpperInvariant())
            .ToList();
        var matchingAssets = _repository.GetAssets()?
            .Where(asset => asset.Id != null && requestedAssetIdKeys.Contains(asset.Id.ToUpper()))
            .AsEnumerable()
            .Where(asset => !string.IsNullOrWhiteSpace(asset.Id))
            .GroupBy(asset => asset.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, Asset>(StringComparer.OrdinalIgnoreCase);

        var existingAssetIds = _repository.GetRingfenceItems(ringfence.Id)
            .AsEnumerable()
            .Where(item => !string.IsNullOrWhiteSpace(item.AssetId))
            .Select(item => item.AssetId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var response = new RingfenceItemBatchResponse();
        foreach (var assetId in requestedAssetIds)
        {
            if (!matchingAssets.TryGetValue(assetId, out var asset) ||
                !DivisionAccess.CanAccess(identity, asset.Division))
            {
                response.UnavailableAssetIds.Add(assetId);
            }
            else if (existingAssetIds.Contains(asset.Id))
            {
                response.AlreadyAssignedAssetIds.Add(asset.Id);
            }
            else
            {
                response.ReadyAssetIds.Add(asset.Id);
            }
        }

        if (response.ReadyAssetIds.Count == 0)
        {
            return response;
        }

        var overlaps = await _repository.GetOverlappingRingfenceDetailsAsync(
            ringfence.Id,
            response.ReadyAssetIds,
            ringfence.FromDate,
            ringfence.ToDate,
            cancellationToken);

        response.Overlaps = overlaps
            .Select(RingfenceResponseMapper.MapOverlap)
            .ToList();
        return response;
    }
}

public record AddRingfenceItemRequest
{
    public required string AssetId { get; init; }
}

public record RingfenceItemBatchRequest
{
    public required List<string> AssetIds { get; init; }
    public bool AcknowledgeOverlaps { get; init; }
}

public record CreateRingfenceRequest
{
    public required string Title { get; init; }
    public required DateTime FromDate { get; init; }
    public required DateTime ToDate { get; init; }
    public required string Divisions { get; init; }
    public string? Warehouse { get; init; }
    public string? Owner { get; init; }
}

public record UpdateRingfenceRequest
{
    public required string Title { get; init; }
    public required DateTime FromDate { get; init; }
    public required DateTime ToDate { get; init; }
    public required string Divisions { get; init; }
    public string? Warehouse { get; init; }
    public string? Owner { get; init; }
}

public record RingfenceOverlapRequest
{
    public required List<string> AssetIds { get; init; }
}
