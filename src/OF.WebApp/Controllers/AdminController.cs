using Azure.Identity;
using Microsoft.AspNetCore.Mvc;
using OF.Common;
using OF.Common.Infrastructure.Features;
using OF.Data.Enums;
using OF.UI.Database;
using OF.UI.Identity;
using OF.WebApp.Features.Administration;
using OF.WebApp.Features.Authorization;
using OF.WebApp.Features.Divisions;

namespace OF.WebApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly IDataRepository _repository;
    private readonly IUserIdentity _userIdentity;
    private readonly IAdminDirectoryService _directoryService;
    private readonly FeatureProvider _featureProvider;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IDataRepository repository,
        IUserIdentity userIdentity,
        IAdminDirectoryService directoryService,
        FeatureProvider featureProvider,
        ILogger<AdminController> logger)
    {
        _repository = repository;
        _userIdentity = userIdentity;
        _directoryService = directoryService;
        _featureProvider = featureProvider;
        _logger = logger;
    }

    [HttpGet("users")]
    [DenyReadOnly]
    public IActionResult GetUsers()
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null || (!identity.IsAdmin && !identity.IsSuperAdmin))
        {
            return Forbid();
        }

        var users = _repository.GetUsers()
            .Select(u => new AdminUserListResponse
            {
                LoginName = u.LoginName,
                FullName = u.FullName,
                Division = u.Division,
                IsAdmin = u.IsAdmin,
                IsSuperAdmin = u.IsSuperAdmin,
                Language = u.Language,
                DateFormat = u.DateFormat,
                Roles = u.Roles,
                LastLoginAtUtc = AsUtc(u.LastLoginAtUtc),
            })
            .ToList();

        return Ok(users);
    }

    [HttpGet("options")]
    [DenyReadOnly]
    public IActionResult GetOptions()
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null || (!identity.IsAdmin && !identity.IsSuperAdmin))
        {
            return Forbid();
        }

        var divisionCodes = _repository.GetWarehouseDivisionCodes()
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var divisionNames = _repository.GetWarehouses(divisionCodes)
            .Where(warehouse => !string.IsNullOrWhiteSpace(warehouse.DivisionCode))
            .GroupBy(warehouse => warehouse.DivisionCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(warehouse => warehouse.Country?.Trim())
                    .FirstOrDefault(country => !string.IsNullOrWhiteSpace(country)) ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);
        var divisions = divisionCodes
            .Select(code => new AdminDivisionOptionResponse
            {
                Code = code,
                Name = DivisionDisplayName.Resolve(code, divisionNames.GetValueOrDefault(code, string.Empty)),
            })
            .ToArray();
        var languages = Enum.GetValues<Languages>()
            .Select(language => new AdminValueOptionResponse<int>
            {
                Value = (int)language,
                Label = language.ToString(),
            })
            .ToArray();
        string[] roles = _featureProvider.RolesEnabled
            ? [
                Constants.Roles.ReadOnly,
                Constants.Roles.ChangeOrder,
                Constants.Roles.ChangeApproval,
                Constants.Roles.NewFrontendPreview,
            ]
            : [Constants.Roles.ReadOnly];

        return Ok(new AdminOptionsResponse
        {
            Divisions = divisions,
            Languages = languages,
            DateFormats = ["dd/MM/yyyy", "MM/dd/yyyy"],
            RolesEnabled = _featureProvider.RolesEnabled,
            Roles = roles,
        });
    }

    [HttpGet("people")]
    [DenyReadOnly]
    public async Task<IActionResult> SearchPeople(
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null || (!identity.IsAdmin && !identity.IsSuperAdmin))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(search) || search.Trim().Length < 2)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Enter at least two characters to search the directory.",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        try
        {
            var people = await _directoryService.SearchAsync(search, cancellationToken);
            return Ok(people);
        }
        catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException or AuthenticationFailedException)
        {
            _logger.LogError(exception, "Unable to search Microsoft Graph for admin user lookup.");
            return Problem(
                title: "Directory search is unavailable.",
                detail: "Try again later or contact support if the problem continues.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpPost("users")]
    [DenyReadOnly]
    public IActionResult CreateUser([FromBody] CreateUserRequest request)
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null || (!identity.IsAdmin && !identity.IsSuperAdmin))
        {
            return Forbid();
        }

        if (request.IsSuperAdmin && !identity.IsSuperAdmin)
        {
            return Forbid();
        }

        if (HasConflictingAccess(request.IsAdmin, request.IsSuperAdmin, request.Roles))
        {
            return ConflictingAccessProblem();
        }

        var loginName = request.LoginName.Trim().ToLowerInvariant();
        if (_repository.GetUser(loginName) is not null)
        {
            return Conflict(new ProblemDetails
            {
                Title = "User already exists.",
                Detail = "Edit the existing user instead of adding a duplicate.",
                Status = StatusCodes.Status409Conflict,
            });
        }

        var user = new OF.Data.Database.User
        {
            LoginName = loginName,
            FullName = request.FullName.Trim(),
            Division = NormalizeCsv(request.Division) ?? string.Empty,
            IsAdmin = request.IsAdmin,
            IsSuperAdmin = request.IsSuperAdmin,
            DateFormat = NormalizeDateFormat(request.DateFormat),
            Language = request.Language,
            Roles = NormalizeRolesForPersistence(request.Roles),
        };

        var created = _repository.AddUser(user);
        return CreatedAtAction(
            nameof(GetUsers),
            new { loginName = created.LoginName },
            ToMutationResponse(created));
    }

    [HttpPut("users/{loginName}")]
    [DenyReadOnly]
    public IActionResult UpdateUser(string loginName, [FromBody] UpdateUserRequest request)
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null || (!identity.IsAdmin && !identity.IsSuperAdmin))
        {
            return Forbid();
        }

        var user = _repository.GetUser(loginName);
        if (user == null)
        {
            return NotFound();
        }

        if (request.IsSuperAdmin != user.IsSuperAdmin && !identity.IsSuperAdmin)
        {
            return Forbid();
        }

        var requestedRoles = request.Roles ?? user.Roles;
        if (HasConflictingAccess(request.IsAdmin, request.IsSuperAdmin, requestedRoles))
        {
            return ConflictingAccessProblem();
        }

        user.FullName = request.FullName.Trim();
        user.Division = NormalizeCsv(request.Division) ?? string.Empty;
        user.IsAdmin = request.IsAdmin;
        user.IsSuperAdmin = request.IsSuperAdmin;
        user.Language = request.Language;
        if (request.DateFormat is not null)
        {
            user.DateFormat = NormalizeDateFormat(request.DateFormat);
        }
        if (request.Roles is not null)
        {
            user.Roles = NormalizeRolesForPersistence(request.Roles, user.Roles);
        }

        var updated = _repository.UpdateUser(user);
        return Ok(ToMutationResponse(updated));
    }

    [HttpDelete("users/{loginName}")]
    [DenyReadOnly]
    public IActionResult DeleteUser(string loginName)
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null || !identity.IsSuperAdmin)
        {
            return Forbid();
        }

        var user = _repository.GetUser(loginName);
        if (user == null)
        {
            return NotFound();
        }

        _repository.DeleteUser(user);
        return NoContent();
    }

    private static AdminUserMutationResponse ToMutationResponse(OF.Data.Database.User user) => new()
    {
        LanguageName = user.LanguageName,
        ActiveRoles = user.ActiveRoles,
        LoginName = user.LoginName,
        FullName = user.FullName,
        Division = user.Division,
        IsAdmin = user.IsAdmin,
        IsSuperAdmin = user.IsSuperAdmin,
        DateFormat = user.DateFormat,
        Language = user.Language,
        Roles = user.Roles,
        LastLoginAtUtc = AsUtc(user.LastLoginAtUtc),
    };

    private static DateTime? AsUtc(DateTime? value) =>
        value is null ? null : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);

    private static string NormalizeDateFormat(string? dateFormat) =>
        string.Equals(dateFormat?.Trim(), "MM/dd/yyyy", StringComparison.Ordinal)
            ? "MM/dd/yyyy"
            : "dd/MM/yyyy";

    private string? NormalizeRolesForPersistence(string? requestedRoles, string? existingRoles = null)
    {
        if (_featureProvider.RolesEnabled)
        {
            return NormalizeCsv(requestedRoles);
        }

        var roles = existingRoles?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(role => !string.Equals(role, Constants.Roles.ReadOnly, StringComparison.OrdinalIgnoreCase))
            .ToList()
            ?? [];

        if (ReadOnlyAccess.HasReadOnlyRole(requestedRoles))
        {
            roles.Add(Constants.Roles.ReadOnly);
        }

        return NormalizeCsv(string.Join(',', roles));
    }

    private static bool HasConflictingAccess(bool isAdmin, bool isSuperAdmin, string? roles) =>
        (isAdmin || isSuperAdmin) && ReadOnlyAccess.HasReadOnlyRole(roles);

    private static BadRequestObjectResult ConflictingAccessProblem() => new(new ProblemDetails
    {
        Title = "Read only cannot be combined with Admin or Super Admin.",
        Status = StatusCodes.Status400BadRequest,
    });

    private static string? NormalizeCsv(string? value)
    {
        var normalized = value?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? [];
        return normalized.Length == 0 ? null : string.Join(',', normalized);
    }
}

public sealed class AdminUserListResponse
{
    public string LoginName { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Division { get; init; } = string.Empty;
    public bool IsAdmin { get; init; }
    public bool IsSuperAdmin { get; init; }
    public int Language { get; init; }
    public string DateFormat { get; init; } = string.Empty;
    public string? Roles { get; init; }
    public DateTime? LastLoginAtUtc { get; init; }
}

public sealed class AdminOptionsResponse
{
    public AdminDivisionOptionResponse[] Divisions { get; init; } = [];
    public AdminValueOptionResponse<int>[] Languages { get; init; } = [];
    public string[] DateFormats { get; init; } = [];
    public bool RolesEnabled { get; init; }
    public string[] Roles { get; init; } = [];
}

public sealed class AdminDivisionOptionResponse
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}

public sealed class AdminValueOptionResponse<T>
{
    public required T Value { get; init; }
    public string Label { get; init; } = string.Empty;
}

public sealed class AdminUserMutationResponse
{
    public string LanguageName { get; init; } = string.Empty;
    public string[] ActiveRoles { get; init; } = [];
    public string LoginName { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Division { get; init; } = string.Empty;
    public bool IsAdmin { get; init; }
    public bool IsSuperAdmin { get; init; }
    public string DateFormat { get; init; } = string.Empty;
    public int Language { get; init; }
    public string? Roles { get; init; }
    public DateTime? LastLoginAtUtc { get; init; }
}

public record CreateUserRequest
{
    public required string LoginName { get; init; }
    public required string FullName { get; init; }
    public required string Division { get; init; }
    public bool IsAdmin { get; init; }
    public bool IsSuperAdmin { get; init; }
    public int Language { get; init; }
    public string? DateFormat { get; init; }
    public string? Roles { get; init; }
}

public record UpdateUserRequest
{
    public required string FullName { get; init; }
    public required string Division { get; init; }
    public bool IsAdmin { get; init; }
    public bool IsSuperAdmin { get; init; }
    public int Language { get; init; }
    public string? DateFormat { get; init; }
    public string? Roles { get; init; }
}
