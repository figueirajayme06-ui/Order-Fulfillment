using Microsoft.AspNetCore.Mvc;
using OF.UI.Database;
using OF.UI.Identity;
using OF.WebApp.Features.Divisions;

namespace OF.WebApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LookupsController : ControllerBase
{
    private readonly IDataRepository _repository;
    private readonly IUserIdentity _userIdentity;

    public LookupsController(IDataRepository repository, IUserIdentity userIdentity)
    {
        _repository = repository;
        _userIdentity = userIdentity;
    }

    [HttpGet("divisions")]
    public IActionResult GetDivisions()
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null)
        {
            return Unauthorized();
        }

        string[] userDivisions = identity.Division?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? Array.Empty<string>();

        // For super admins, get all distinct divisions from warehouse items
        // For normal users, only their assigned divisions
        IList<OF.Data.Database.WarehouseItem> warehouses;
        if (identity.IsSuperAdmin)
        {
            var allDivisionCodes = _repository.GetWarehouseDivisionCodes();
            warehouses = _repository.GetWarehouses(allDivisionCodes);
        }
        else
        {
            warehouses = _repository.GetWarehouses(userDivisions);
        }

        var divisions = warehouses
            .GroupBy(w => w.DivisionCode)
            .Select(g => new DivisionLookupResponse
            {
                Code = g.Key,
                Name = DivisionDisplayName.Resolve(g.Key, g.First().Country),
            })
            .OrderBy(d => d.Code)
            .ToList();

        return Ok(divisions);
    }

    [HttpGet("warehouses")]
    public IActionResult GetWarehouses([FromQuery] string? division)
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(division))
        {
            return BadRequest("At least one division is required.");
        }

        var effectiveDivisions = DivisionAccess.Resolve(identity, division, [',']);
        if (effectiveDivisions.Length == 0)
        {
            return Ok(Array.Empty<WarehouseLookupResponse>());
        }

        var allowedDivisions = effectiveDivisions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var warehouses = _repository.GetWarehouses(effectiveDivisions)
            .Where(warehouse =>
                !string.IsNullOrWhiteSpace(warehouse.WarehouseCode)
                && !string.IsNullOrWhiteSpace(warehouse.DivisionCode)
                && allowedDivisions.Contains(warehouse.DivisionCode.Trim()))
            .Select(warehouse => new WarehouseLookupResponse
            {
                WarehouseCode = warehouse.WarehouseCode.Trim(),
                Warehouse = string.IsNullOrWhiteSpace(warehouse.Warehouse)
                    ? warehouse.WarehouseCode.Trim()
                    : warehouse.Warehouse.Trim(),
                Facility = !string.IsNullOrWhiteSpace(warehouse.FacilityCode)
                    ? warehouse.FacilityCode.Trim()
                    : warehouse.Facility?.Trim() ?? string.Empty,
                FacilityName = warehouse.Facility?.Trim() ?? string.Empty,
                DivisionCode = warehouse.DivisionCode.Trim(),
                DivisionName = DivisionDisplayName.Resolve(warehouse.DivisionCode, warehouse.Country),
            })
            .OrderBy(warehouse => warehouse.DivisionCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(warehouse => warehouse.Facility, StringComparer.OrdinalIgnoreCase)
            .ThenBy(warehouse => warehouse.WarehouseCode, StringComparer.OrdinalIgnoreCase)
            .GroupBy(warehouse => warehouse.WarehouseCode, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        return Ok(warehouses);
    }

    [HttpGet("users")]
    public IActionResult GetUsers([FromQuery] string? division)
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(division))
        {
            return BadRequest("At least one division is required.");
        }

        var effectiveDivisions = DivisionAccess.Resolve(identity, division, [',']);
        if (effectiveDivisions.Length == 0)
        {
            return Ok(Array.Empty<UserLookupResponse>());
        }

        var allowedDivisions = effectiveDivisions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var users = _repository.GetUsers()
            .AsEnumerable()
            .Where(user =>
                !string.IsNullOrWhiteSpace(user.LoginName)
                && SplitDivisions(user.Division).Any(allowedDivisions.Contains))
            .Select(user => new UserLookupResponse
            {
                LoginName = user.LoginName.Trim(),
                FullName = string.IsNullOrWhiteSpace(user.FullName)
                    ? user.LoginName.Trim()
                    : user.FullName.Trim(),
            })
            .OrderBy(user => user.FullName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(user => user.LoginName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Ok(users);
    }

    private static string[] SplitDivisions(string? divisions) =>
        divisions?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(division => !string.IsNullOrWhiteSpace(division))
            .ToArray()
        ?? [];
}

public sealed class DivisionLookupResponse
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}

public sealed class WarehouseLookupResponse
{
    public string WarehouseCode { get; init; } = string.Empty;
    public string Warehouse { get; init; } = string.Empty;
    public string Facility { get; init; } = string.Empty;
    public string FacilityName { get; init; } = string.Empty;
    public string DivisionCode { get; init; } = string.Empty;
    public string DivisionName { get; init; } = string.Empty;
}

public sealed class UserLookupResponse
{
    public string LoginName { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
}
