using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using OF.Data.Database;
using OF.UI.Database;
using OF.UI.Identity;
using OF.UI.Models;
using OF.WebApp.Features.Divisions;

namespace OF.WebApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AvailabilityController : ControllerBase
{
    private readonly IDataRepository _repository;
    private readonly IUserIdentity _userIdentity;
    private readonly ILogger<AvailabilityController> _logger;

    public AvailabilityController(
        IDataRepository repository,
        IUserIdentity userIdentity,
        ILogger<AvailabilityController> logger)
    {
        _repository = repository;
        _userIdentity = userIdentity;
        _logger = logger;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetAvailabilitySummary(
        [FromQuery] string genericCode,
        [FromQuery] string? attributes = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? division = null,
        [FromQuery] string? itemNumber = null,
        [FromQuery] int? lineId = null)
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(genericCode))
        {
            return BadRequest("genericCode is required.");
        }

        var hasExplicitDivision = !string.IsNullOrWhiteSpace(division);
        var requestedDivisions = identity.IsSuperAdmin && !hasExplicitDivision
            ? string.Join(',', _repository.GetWarehouseDivisionCodes())
            : division;
        var effectiveDivisions = DivisionAccess.Resolve(identity, requestedDivisions, [',']);

        if (effectiveDivisions.Length == 0)
        {
            return Ok(Array.Empty<AvailabilitySummaryResponse>());
        }

        var effectiveDivision = !hasExplicitDivision && !identity.IsSuperAdmin
            ? effectiveDivisions[0]
            : string.Join(',', effectiveDivisions);

        try
        {
            var results = await _repository.GetAvailabilitySummaryAsync(
                genericCode,
                attributes ?? string.Empty,
                startDate,
                endDate,
                effectiveDivision,
                itemNumber,
                lineId);

            IReadOnlyDictionary<string, WarehouseItem> warehouseLocations =
                new Dictionary<string, WarehouseItem>(StringComparer.OrdinalIgnoreCase);

            if (results.Any(NeedsWarehouseLocationFallback))
            {
                try
                {
                    warehouseLocations = _repository.GetWarehouses(effectiveDivisions)
                        .Where(warehouse => !string.IsNullOrWhiteSpace(warehouse.WarehouseCode))
                        .GroupBy(warehouse => warehouse.WarehouseCode, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Unable to enrich availability hierarchy from warehouse references.");
                }
            }

            return Ok(results.Select(result =>
            {
                WarehouseItem? warehouseLocation = null;
                if (!string.IsNullOrWhiteSpace(result.WarehouseCode))
                {
                    warehouseLocations.TryGetValue(result.WarehouseCode, out warehouseLocation);
                }

                return ToResponse(result, warehouseLocation);
            }).ToList());
        }
        catch (SqlException ex) when (ex.Number == 229)
        {
            _logger.LogWarning("EXECUTE permission denied on GetFulfilmentAvailabilitySummary. Grant required.");
            return Ok(Array.Empty<AvailabilitySummaryResponse>());
        }
    }

    private static bool NeedsWarehouseLocationFallback(AvailabilitySummaryResult result) =>
        !string.IsNullOrWhiteSpace(result.WarehouseCode) &&
        (string.IsNullOrWhiteSpace(result.Facility) ||
         string.IsNullOrWhiteSpace(result.DivisionCode) ||
         string.IsNullOrWhiteSpace(result.DivisionName));

    private static AvailabilitySummaryResponse ToResponse(
        AvailabilitySummaryResult result,
        WarehouseItem? warehouseLocation)
    {
        var divisionCode = FirstPopulated(result.DivisionCode, warehouseLocation?.DivisionCode);
        return new AvailabilitySummaryResponse
        {
            WarehouseCode = result.WarehouseCode,
            Warehouse = result.Warehouse,
            GenericCode = result.GenericCode,
            GenericDescription = result.GenericDescription,
            ItemNumber = result.ItemNumber,
            DescriptionIntl = result.DescriptionIntl,
            Facility = FirstPopulated(
                result.Facility,
                warehouseLocation?.FacilityCode,
                warehouseLocation?.Facility),
            DivisionCode = divisionCode,
            DivisionName = DivisionDisplayName.Resolve(
                divisionCode,
                FirstPopulated(result.DivisionName, warehouseLocation?.Country)),
            Available = result.Available,
            Count = result.Count,
            GenericOnly = result.GenericOnly,
            ReservationMode = result.ReservationMode,
            SubstitutionReason = result.SubstitutionReason,
        };
    }

    private static string FirstPopulated(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}

public sealed class AvailabilitySummaryResponse
{
    public string WarehouseCode { get; init; } = string.Empty;
    public string Warehouse { get; init; } = string.Empty;
    public string GenericCode { get; init; } = string.Empty;
    public string GenericDescription { get; init; } = string.Empty;
    public string ItemNumber { get; init; } = string.Empty;
    public string DescriptionIntl { get; init; } = string.Empty;
    public string Facility { get; init; } = string.Empty;
    public string DivisionCode { get; init; } = string.Empty;
    public string DivisionName { get; init; } = string.Empty;
    public int Available { get; init; }
    public int Count { get; init; }
    public bool GenericOnly { get; init; }
    public string ReservationMode { get; init; } = "asset";
    public string? SubstitutionReason { get; init; }
}
