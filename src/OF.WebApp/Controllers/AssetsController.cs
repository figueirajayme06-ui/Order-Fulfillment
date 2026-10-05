using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using OF.UI.Database;
using OF.UI.Identity;
using OF.WebApp.Features.Assets;
using OF.WebApp.Features.Divisions;

namespace OF.WebApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AssetsController : ControllerBase
{
    private readonly IDataRepository _repository;
    private readonly IUserIdentity _userIdentity;
    private readonly IAssetEnrichmentService _assetEnrichmentService;
    private readonly ILogger<AssetsController> _logger;

    public AssetsController(
        IDataRepository repository,
        IUserIdentity userIdentity,
        IAssetEnrichmentService assetEnrichmentService,
        ILogger<AssetsController> logger)
    {
        _repository = repository;
        _userIdentity = userIdentity;
        _assetEnrichmentService = assetEnrichmentService;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult GetAssets(
        [FromQuery] string? search = null,
        [FromQuery] string? warehouse = null,
        [FromQuery] string? status = null,
        [FromQuery] string? division = null,
        [FromQuery] string? facility = null,
        [FromQuery] string? itemNumber = null,
        [FromQuery] string? description = null,
        [FromQuery] string? agreementNumber = null,
        [FromQuery] DateTime? deliveryDateFrom = null,
        [FromQuery] DateTime? deliveryDateTo = null,
        [FromQuery] DateTime? validFromDate = null,
        [FromQuery] DateTime? validToDate = null,
        [FromQuery] string? warehouseLocation = null,
        [FromQuery] string? individualItemNumber = null,
        [FromQuery] DateTime? terminationDateFrom = null,
        [FromQuery] DateTime? terminationDateTo = null,
        [FromQuery] DateTime? collectionDateFrom = null,
        [FromQuery] DateTime? collectionDateTo = null,
        [FromQuery] DateTime? estimatedReadyDateFrom = null,
        [FromQuery] DateTime? estimatedReadyDateTo = null,
        [FromQuery] string? excludeStatuses = null,
        [FromQuery] int? take = null,
        [FromQuery] bool exactMatch = false,
        [FromQuery] string? statuses = null)
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null)
        {
            return Unauthorized();
        }

        var assets =
            from asset in _repository.GetAssets()
            join assetItem in _repository.GetAssetItems(includeAllStatuses: true)
                on asset.Id equals assetItem.Id into assetItems
            from assetItem in assetItems.DefaultIfEmpty()
            select new
            {
                Asset = asset,
                assetItem.DaysOffHire,
                WarehouseName = assetItem.WarehouseName,
            };

        // Exclude statuses (e.g. RemovedStock,Scrap,Sold)
        if (!string.IsNullOrWhiteSpace(excludeStatuses))
        {
            var excluded = excludeStatuses.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            assets = assets.Where(a => a.Asset.Status == null || !excluded.Contains(a.Asset.Status));
        }

        // Status filter
        var selectedStatuses = SplitCsv(!string.IsNullOrWhiteSpace(statuses) ? statuses : status);
        if (selectedStatuses.Length > 0)
        {
            assets = assets.Where(a => a.Asset.Status != null && selectedStatuses.Contains(a.Asset.Status));
        }

        // Broad text search
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            assets = assets.Where(a =>
                a.Asset.Id.ToLower().Contains(term) ||
                (a.Asset.IndividualItemNumber != null && a.Asset.IndividualItemNumber.ToLower().Contains(term)) ||
                (a.Asset.ItemNumber != null && a.Asset.ItemNumber.ToLower().Contains(term)) ||
                (a.Asset.Status != null && a.Asset.Status.ToLower().Contains(term)) ||
                (a.Asset.Warehouse != null && a.Asset.Warehouse.ToLower().Contains(term)) ||
                (a.Asset.WarehouseLocation != null && a.Asset.WarehouseLocation.ToLower().Contains(term)) ||
                (a.Asset.Division != null && a.Asset.Division.ToLower().Contains(term)) ||
                (a.Asset.Facility != null && a.Asset.Facility.ToLower().Contains(term)) ||
                (a.Asset.Description != null && a.Asset.Description.ToLower().Contains(term)) ||
                (a.Asset.AgreementNumber != null && a.Asset.AgreementNumber.ToLower().Contains(term)) ||
                (a.Asset.CustomerName != null && a.Asset.CustomerName.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(warehouse))
        {
            var term = warehouse.Trim().ToLower();
            assets = exactMatch
                ? assets.Where(a => a.Asset.Warehouse != null && a.Asset.Warehouse.ToLower() == term)
                : assets.Where(a =>
                    (a.Asset.Warehouse != null && a.Asset.Warehouse.ToLower().Contains(term)) ||
                    (a.Asset.WarehouseLocation != null && a.Asset.WarehouseLocation.ToLower().Contains(term)));
        }

        // Advanced field-level filters
        if (!string.IsNullOrWhiteSpace(facility))
        {
            var term = facility.Trim().ToLower();
            assets = assets.Where(a => a.Asset.Facility != null && a.Asset.Facility.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(itemNumber))
        {
            var term = itemNumber.Trim().ToLower();
            assets = exactMatch
                ? assets.Where(a => a.Asset.ItemNumber != null && a.Asset.ItemNumber.ToLower() == term)
                : assets.Where(a => a.Asset.ItemNumber != null && a.Asset.ItemNumber.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(description))
        {
            var term = description.Trim().ToLower();
            assets = assets.Where(a => a.Asset.Description != null && a.Asset.Description.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(agreementNumber))
        {
            var term = agreementNumber.Trim().ToLower();
            assets = assets.Where(a => a.Asset.AgreementNumber != null && a.Asset.AgreementNumber.ToLower().Contains(term));
        }

        if (deliveryDateFrom.HasValue)
        {
            assets = assets.Where(a => a.Asset.DeliveryDate >= deliveryDateFrom.Value);
        }

        if (deliveryDateTo.HasValue)
        {
            assets = assets.Where(a => a.Asset.DeliveryDate <= deliveryDateTo.Value);
        }

        if (validFromDate.HasValue)
        {
            assets = assets.Where(a => a.Asset.AgreementLineValidFromDate >= validFromDate.Value);
        }

        if (validToDate.HasValue)
        {
            assets = assets.Where(a => a.Asset.AgreementLineValidToDate <= validToDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(warehouseLocation))
        {
            var term = warehouseLocation.Trim().ToLower();
            assets = assets.Where(a => a.Asset.WarehouseLocation != null && a.Asset.WarehouseLocation.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(individualItemNumber))
        {
            var term = individualItemNumber.Trim().ToLower();
            assets = assets.Where(a => a.Asset.IndividualItemNumber != null && a.Asset.IndividualItemNumber.ToLower().Contains(term));
        }

        if (terminationDateFrom.HasValue)
        {
            assets = assets.Where(a => a.Asset.TerminationDate >= terminationDateFrom.Value);
        }

        if (terminationDateTo.HasValue)
        {
            assets = assets.Where(a => a.Asset.TerminationDate <= terminationDateTo.Value);
        }

        if (collectionDateFrom.HasValue)
        {
            assets = assets.Where(a => a.Asset.CollectionDate >= collectionDateFrom.Value);
        }

        if (collectionDateTo.HasValue)
        {
            assets = assets.Where(a => a.Asset.CollectionDate <= collectionDateTo.Value);
        }

        if (estimatedReadyDateFrom.HasValue)
        {
            assets = assets.Where(a => a.Asset.EstimatedReadyDate >= estimatedReadyDateFrom.Value);
        }

        if (estimatedReadyDateTo.HasValue)
        {
            assets = assets.Where(a => a.Asset.EstimatedReadyDate <= estimatedReadyDateTo.Value);
        }

        var filterDivisions = DivisionAccess.Resolve(identity, division, [',']);
        if (!identity.IsSuperAdmin || !string.IsNullOrWhiteSpace(division))
        {
            assets = assets.Where(a =>
                a.Asset.Division != null
                && filterDivisions.Contains(a.Asset.Division.ToUpper()));
        }

        var query = assets.OrderBy(a => a.Asset.Id).AsQueryable();

        if (take.HasValue && take.Value > 0)
        {
            query = query.Take(take.Value);
        }

        var timer = Stopwatch.StartNew();
        // Project before materialisation: this read must not create tracked Asset entities.
        var results = query
            .Select(a => new AssetListItemResponse
            {
                Id = a.Asset.Id,
                IndividualItemNumber = a.Asset.IndividualItemNumber,
                ItemNumber = a.Asset.ItemNumber,
                Status = a.Asset.Status,
                Warehouse = a.Asset.Warehouse,
                Division = a.Asset.Division,
                Facility = a.Asset.Facility,
                EstimatedReadyDate = a.Asset.EstimatedReadyDate,
                AgreementNumber = a.Asset.AgreementNumber,
                CustomerName = a.Asset.CustomerName,
                DeliveryDate = a.Asset.DeliveryDate,
                AgreementLineValidFromDate = a.Asset.AgreementLineValidFromDate,
                AgreementLineValidToDate = a.Asset.AgreementLineValidToDate,
                Description = a.Asset.Description,
                WarehouseLocation = a.Asset.WarehouseLocation,
                CollectionDate = a.Asset.CollectionDate,
                TerminationDate = a.Asset.TerminationDate,
                DaysOffHire = a.DaysOffHire,
                WarehouseName = a.WarehouseName,
                CustomerNumber = a.Asset.CustomerNumber,
                ProductGroup = a.Asset.ProductGroup,
                ProductCategory = a.Asset.ProductCategory,
                RunHours = a.Asset.RunHours,
                Size = a.Asset.UsSizeRating,
                TelemetryStatus = a.Asset.TelemetryStatus,
                Remark = a.Asset.Remark,
            })
            .ToList();

        var assetsMs = timer.Elapsed.TotalMilliseconds;
        var assetIds = results.Select(a => a.Id).ToArray();
        var noteCounts = assetIds.Length == 0
            ? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            : (_repository.GetNotes("asset") ?? Enumerable.Empty<OF.Data.Database.Note>().AsQueryable())
                .Where(note => assetIds.Contains(note.ParentId))
                .GroupBy(note => note.ParentId)
                .Select(group => new { ParentId = group.Key, Count = group.Count() })
                .ToDictionary(entry => entry.ParentId, entry => entry.Count, StringComparer.OrdinalIgnoreCase);
        var notesMs = timer.Elapsed.TotalMilliseconds - assetsMs;
        foreach (var row in results)
        {
            row.NoteCount = noteCounts.GetValueOrDefault(row.Id);
        }
        var preparationMs = timer.Elapsed.TotalMilliseconds - assetsMs - notesMs;

        // These request-correlated timings include reading/materialising all rows, unlike
        // the SQL dependency span. Record only timings/counts, never asset or note data.
        Activity.Current?.SetTag("assets.row_count", results.Count);
        Activity.Current?.SetTag("assets.load_ms", assetsMs);
        Activity.Current?.SetTag("assets.notes_ms", notesMs);
        Activity.Current?.SetTag("assets.prepare_ms", preparationMs);
        _logger.LogInformation(
            "Asset list prepared: {AssetCount} rows; load {AssetLoadMs} ms; notes {NoteCountMs} ms; prepare {PreparationMs} ms",
            results.Count, assetsMs, notesMs, preparationMs);
        if (HttpContext is not null)
        {
            Response.Headers["Server-Timing"] = FormattableString.Invariant(
                $"assets;dur={assetsMs:F2}, notes;dur={notesMs:F2}, prepare;dur={preparationMs:F2}");
            var responseStartedAt = timer.Elapsed.TotalMilliseconds;
            Response.OnCompleted(() =>
            {
                _logger.LogInformation("Asset list response completed: {AssetCount} rows; response phase {ResponseMs} ms",
                    results.Count, timer.Elapsed.TotalMilliseconds - responseStartedAt);
                return Task.CompletedTask;
            });
        }

        return Ok(results);
    }

    private static string[] SplitCsv(string? value) =>
        value?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];

    [HttpGet("{id}")]
    public IActionResult GetAsset(string id)
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null)
        {
            return Unauthorized();
        }

        var asset = _repository.GetAsset(id);
        if (asset == null || !DivisionAccess.CanAccess(identity, asset.Division))
        {
            return NotFound();
        }

        return Ok(AssetDetailResponseMapper.Map(asset));
    }

    [HttpGet("{id}/profile")]
    public IActionResult GetAssetProfile(string id, [FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null)
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null)
        {
            return Unauthorized();
        }

        var asset = _repository.GetAssets().FirstOrDefault(item => item.Id == id);
        if (asset == null || !DivisionAccess.CanAccess(identity, asset.Division))
        {
            return NotFound();
        }

        var today = DateTime.UtcNow.Date;
        var scheduleFrom = (fromDate ?? today).Date;
        var scheduleTo = (toDate ?? scheduleFrom.AddMonths(12).AddDays(-1)).Date;

        if (scheduleTo < scheduleFrom)
        {
            return BadRequest(new { message = "toDate must be on or after fromDate." });
        }

        var reservations = _repository.GetAssetScheduleReservations(asset.Id, scheduleFrom, scheduleTo);
        var ringfences = _repository.GetRingfencesForAsset(asset.Id, scheduleFrom, scheduleTo);

        return Ok(AssetProfileScheduleBuilder.Build(asset, reservations, ringfences, scheduleFrom, scheduleTo, today));
    }

    [HttpGet("{id}/enrichment")]
    public async Task<IActionResult> GetAssetEnrichment(
        string id,
        [FromQuery] int serviceLimit = 20,
        CancellationToken cancellationToken = default)
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null)
        {
            return Unauthorized();
        }

        var asset = _repository.GetAsset(id);
        if (asset == null || !DivisionAccess.CanAccess(identity, asset.Division))
        {
            return NotFound();
        }

        if (serviceLimit is < 1 or > 50)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid service history limit",
                Detail = "serviceLimit must be between 1 and 50.",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        try
        {
            return Ok(await _assetEnrichmentService.GetAsync(
                asset.IndividualItemNumber,
                serviceLimit,
                cancellationToken));
        }
        catch (AssetEnrichmentUnavailableException)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Asset enrichment is unavailable",
                detail: "Location and service history could not be loaded. The core asset profile is still available.");
        }
    }
}
