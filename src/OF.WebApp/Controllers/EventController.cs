using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using OF.UI.Database;
using OF.UI.Identity;
using OF.UI.Models;
using OF.WebApp.Features.Divisions;
using OF.WebApp.Features.Events;
using OF.WebApp.Features.Authorization;

namespace OF.WebApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EventController : ControllerBase
{
    private readonly IDataRepository _repository;
    private readonly IUserIdentity _userIdentity;
    private readonly ILogger<EventController> _logger;

    public EventController(IDataRepository repository, IUserIdentity userIdentity, ILogger<EventController> logger)
    {
        _repository = repository;
        _userIdentity = userIdentity;
        _logger = logger;
    }

    [HttpPost("events")]
    [ReadOnlyQuery]
    public IActionResult Events([FromBody] AssetTimelineEventsRequest request)
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null)
        {
            return Unauthorized();
        }

        var startDate = request.StartDate == default
            ? DateTime.UtcNow.Date
            : request.StartDate.Date;
        var endDate = request.EndDate?.Date ?? startDate.AddMonths(3).AddDays(-1);

        var requestedAssetIds = (request.AssetIds ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var requestedAssetLookup = requestedAssetIds.Length > 0
            ? new HashSet<string>(requestedAssetIds, StringComparer.OrdinalIgnoreCase)
            : null;

        var divisionsRaw = string.IsNullOrWhiteSpace(request.Divisions) ? string.Empty : request.Divisions;

        if (string.IsNullOrWhiteSpace(divisionsRaw) && requestedAssetIds.Length > 0)
        {
            divisionsRaw = string.Join(';', _repository.GetAssets()
                .Where(a => a.Id != null && requestedAssetIds.Contains(a.Id))
                .Select(a => a.Division)
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Distinct());
        }

        if (identity.IsSuperAdmin && string.IsNullOrWhiteSpace(divisionsRaw))
        {
            divisionsRaw = string.Join(';', _repository.GetAssets()
                .Select(a => a.Division)
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Distinct());
        }

        var filterDivisions = DivisionAccess.Resolve(identity, divisionsRaw, [',', ';']);

        if (filterDivisions.Length == 0)
        {
            return Ok(new AssetTimelineEventsResponse());
        }

        IList<Event> events;
        try
        {
            events = _repository.GetEventsForDivisionsAndRange(startDate, endDate, filterDivisions);
        }
        catch (SqlException ex) when (ex.Number == 229)
        {
            _logger.LogWarning(ex, "EXECUTE permission denied on GenerateEvents. Falling back to synthesized asset events.");
            events = BuildFallbackEvents(startDate, endDate, filterDivisions, requestedAssetIds);
        }

        var returnedAssetIds = events
            .Select(item => item.AssetId?.Trim())
            .Where(assetId => !string.IsNullOrWhiteSpace(assetId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var allowedAssetIds = _repository.GetAssets()
            .Where(asset =>
                asset.Id != null
                && returnedAssetIds.Contains(asset.Id)
                && asset.Division != null
                && filterDivisions.Contains(asset.Division.ToUpper()))
            .Select(asset => asset.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var response = new AssetTimelineEventsResponse();
        foreach (var item in events)
        {
            if (!item.StartDate.HasValue || !item.EndDate.HasValue)
            {
                continue;
            }

            var assetId = item.AssetId?.Trim();
            if (string.IsNullOrWhiteSpace(assetId))
            {
                continue;
            }

            if (!allowedAssetIds.Contains(assetId))
            {
                continue;
            }

            if (requestedAssetLookup != null && !requestedAssetLookup.Contains(assetId))
            {
                continue;
            }

            if (!response.Events.TryGetValue(assetId, out var eventItems))
            {
                eventItems = [];
                response.Events[assetId] = eventItems;
            }

            item.AssetId = assetId;
            eventItems.Add(item);
        }

        return Ok(response);
    }

    private IList<Event> BuildFallbackEvents(DateTime startDate, DateTime endDate, string[] divisions, string[] requestedAssetIds)
    {
        var query = _repository.GetAssets()
            .Where(a => a.Division != null && divisions.Contains(a.Division.ToUpper()));

        if (requestedAssetIds.Length > 0)
        {
            query = query.Where(a => requestedAssetIds.Contains(a.Id));
        }

        var today = DateTime.UtcNow.Date;

        var assets = query.Select(a => new AssetTimelineFallbackSource
        {
            Id = a.Id,
            Status = a.Status,
            AgreementNumber = a.AgreementNumber,
            CustomerName = a.CustomerName,
            DeliveryDate = a.DeliveryDate,
            TerminationDate = a.TerminationDate,
            AgreementLineValidFromDate = a.AgreementLineValidFromDate,
            AgreementLineValidToDate = a.AgreementLineValidToDate,
            EstimatedReadyDate = a.EstimatedReadyDate,
            IonlastModified = a.IonlastModified,
            CollectionDate = a.CollectionDate,
        }).ToList();

        return AssetTimelineFallbackSynthesizer.Synthesize(assets, today, startDate, endDate);
    }
}

public sealed class AssetTimelineEventsRequest
{
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Divisions { get; set; }
    public string[]? AssetIds { get; set; }
}

public sealed class AssetTimelineEventsResponse
{
    public Dictionary<string, List<Event>> Events { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
