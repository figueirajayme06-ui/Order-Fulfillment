using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OF.Data.Database;
using OF.UI.Database;
using OF.UI.Identity;
using OF.WebApp.Features.SavedViews;
using OF.WebApp.Features.Authorization;

namespace OF.WebApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ViewsController : ControllerBase
{
    private const string FrontendViewApp = "nof-frontend";
    private const int FrontendViewVersion = 2;
    private const int DefaultRecipientCandidateLimit = 10;
    private const int MaximumRecipientCandidateLimit = 25;

    private readonly IDataRepository _repository;
    private readonly IUserIdentity _userIdentity;

    public ViewsController(IDataRepository repository, IUserIdentity userIdentity)
    {
        _repository = repository;
        _userIdentity = userIdentity;
    }

    [HttpGet]
    public IActionResult GetViews([FromQuery] string page = "agreements")
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null)
        {
            return Unauthorized();
        }

        if (!TryParsePage(page, out var assetView, out var normalizedPage))
        {
            return BadRequest(new { message = "Unsupported page value. Use 'agreements' or 'assets'." });
        }

        var loginName = identity.LoginName;
        var userDivisions = SplitCsv(identity.Division);

        var candidates = _repository.GetViewsWithRecipients()
            .Where(v => v.AssetView == assetView)
            .ToList();

        var result = new List<SavedViewDto>();

        foreach (var candidate in candidates)
        {
            if (!CanReadView(candidate, loginName, userDivisions))
            {
                continue;
            }

            if (!TryParseFrontendEnvelope(candidate.ViewJson, normalizedPage, out var envelope))
            {
                continue;
            }

            result.Add(SavedViewResponseMapper.Map(candidate, identity, normalizedPage, envelope.State));
        }

        return Ok(result.OrderBy(v => v.Name).ThenBy(v => v.Id));
    }

    [HttpGet("recipient-candidates")]
    [DenyReadOnly]
    public IActionResult GetRecipientCandidates(
        [FromQuery] string? search = null,
        [FromQuery] int limit = DefaultRecipientCandidateLimit)
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null)
        {
            return Unauthorized();
        }

        if (ReadOnlyAccess.IsReadOnly(identity))
        {
            return Forbid();
        }

        if (limit < 1 || limit > MaximumRecipientCandidateLimit)
        {
            return BadRequest(new
            {
                message = $"Recipient candidate limit must be between 1 and {MaximumRecipientCandidateLimit}.",
            });
        }

        var normalizedSearch = search?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedSearch))
        {
            return Ok(Array.Empty<SavedViewRecipientDto>());
        }

        var candidates = GetEligibleRecipients(identity, identity.LoginName, normalizedSearch)
            .Where(candidate => IsRecipientMatch(candidate, normalizedSearch))
            .OrderBy(candidate => GetRecipientMatchRank(candidate, normalizedSearch))
            .ThenBy(candidate => candidate.FullName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.LoginName, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToArray();

        return Ok(candidates);
    }

    [HttpPost]
    [DenyReadOnly]
    public IActionResult CreateView([FromBody] UpsertSavedViewRequest request)
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null)
        {
            return Unauthorized();
        }

        if (!TryValidateRequest(request, out var normalizedName, out var normalizedPage, out var assetView, out var forEveryone, out var usersScope, out var scopeError))
        {
            return BadRequest(new { message = scopeError });
        }

        if (forEveryone > 0 && !identity.IsAdmin)
        {
            return Forbid();
        }

        string[] recipientLoginNames = [];
        SavedViewRecipientDto[] recipientMetadata = [];
        if (usersScope
            && !TryResolveRecipients(identity, identity.LoginName, request.Recipients, out recipientLoginNames, out recipientMetadata, out var recipientError))
        {
            return BadRequest(new { message = recipientError });
        }

        var divisions = forEveryone == 2 ? string.Join(',', SplitCsv(identity.Division)) : null;

        var newView = new View
        {
            Name = normalizedName,
            Owner = identity.LoginName,
            ForEveryone = forEveryone,
            ForDivisions = divisions,
            AssetView = assetView,
            GanttView = false,
            ViewJson = SerializeFrontendEnvelope(normalizedPage, request.State),
        };

        View saved;
        try
        {
            saved = usersScope
                ? _repository.AddViewWithRecipients(newView, recipientLoginNames)
                : _repository.AddView(newView);
        }
        catch (DbUpdateException) when (usersScope)
        {
            return BadRequest(new
            {
                message = "One or more recipients are no longer available. Refresh the recipient list and try again.",
            });
        }

        return Ok(SavedViewResponseMapper.Map(
            saved,
            identity,
            normalizedPage,
            request.State,
            callerCanManage: true,
            recipientMetadata: recipientMetadata));
    }

    [HttpPut("{id:int}")]
    [DenyReadOnly]
    public IActionResult UpdateView(int id, [FromBody] UpsertSavedViewRequest request)
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null)
        {
            return Unauthorized();
        }

        if (!TryValidateRequest(request, out var normalizedName, out var normalizedPage, out var assetView, out var forEveryone, out var usersScope, out var scopeError))
        {
            return BadRequest(new { message = scopeError });
        }

        var existing = _repository.GetView(id);
        if (existing == null)
        {
            return NotFound();
        }

        if (!string.Equals(existing.Owner, identity.LoginName, StringComparison.OrdinalIgnoreCase) && !identity.IsAdmin)
        {
            return Forbid();
        }

        string[] recipientLoginNames = [];
        SavedViewRecipientDto[] recipientMetadata = [];
        if (usersScope
            && !TryResolveRecipients(identity, existing.Owner, request.Recipients, out recipientLoginNames, out recipientMetadata, out var recipientError))
        {
            return BadRequest(new { message = recipientError });
        }

        if (forEveryone > 0 && !identity.IsAdmin)
        {
            return Forbid();
        }

        existing.Name = normalizedName;
        existing.ForEveryone = forEveryone;
        existing.ForDivisions = forEveryone == 2 ? string.Join(',', SplitCsv(identity.Division)) : null;
        existing.AssetView = assetView;
        existing.GanttView = false;
        existing.ViewJson = SerializeFrontendEnvelope(normalizedPage, request.State);

        View updated;
        try
        {
            updated = _repository.UpdateViewWithRecipients(existing, recipientLoginNames);
        }
        catch (DbUpdateException) when (usersScope)
        {
            return BadRequest(new
            {
                message = "One or more recipients are no longer available. Refresh the recipient list and try again.",
            });
        }

        return Ok(SavedViewResponseMapper.Map(
            updated,
            identity,
            normalizedPage,
            request.State,
            recipientMetadata: recipientMetadata));
    }

    [HttpDelete("{id:int}")]
    [DenyReadOnly]
    public IActionResult DeleteView(int id)
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null)
        {
            return Unauthorized();
        }

        var existing = _repository.GetView(id);
        if (existing == null)
        {
            return NotFound();
        }

        if (!string.Equals(existing.Owner, identity.LoginName, StringComparison.OrdinalIgnoreCase) && !identity.IsAdmin)
        {
            return Forbid();
        }

        _repository.DeleteView(existing);
        return NoContent();
    }

    private static bool TryValidateRequest(
        UpsertSavedViewRequest request,
        out string normalizedName,
        out string normalizedPage,
        out bool assetView,
        out int forEveryone,
        out bool usersScope,
        out string error)
    {
        normalizedName = NormalizeName(request.Name);
        normalizedPage = string.Empty;
        assetView = false;
        forEveryone = 0;
        usersScope = false;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            error = "View name is required.";
            return false;
        }

        if (!TryParsePage(request.Page, out assetView, out normalizedPage))
        {
            error = "Unsupported page value. Use 'agreements' or 'assets'.";
            return false;
        }

        if (!TryParseScope(request.Scope, out forEveryone, out usersScope))
        {
            error = "Unsupported scope value. Use 'personal', 'users', 'division', or 'global'.";
            return false;
        }

        if (request.State.ValueKind != JsonValueKind.Object)
        {
            error = "State must be a JSON object.";
            return false;
        }

        return true;
    }

    private bool TryResolveRecipients(
        User identity,
        string ownerLoginName,
        IReadOnlyCollection<string>? requestedRecipients,
        out string[] recipientLoginNames,
        out SavedViewRecipientDto[] recipientMetadata,
        out string error)
    {
        recipientLoginNames = NormalizeRecipientLoginNames(requestedRecipients, ownerLoginName);
        recipientMetadata = [];
        error = string.Empty;

        if (recipientLoginNames.Length == 0)
        {
            error = "Specific users scope requires at least one recipient other than the owner.";
            return false;
        }

        var eligibleRecipients = GetEligibleRecipients(identity, ownerLoginName);
        var eligibleByLogin = eligibleRecipients.ToDictionary(
            recipient => recipient.LoginName,
            StringComparer.OrdinalIgnoreCase);
        var invalidRecipients = recipientLoginNames
            .Where(loginName => !eligibleByLogin.ContainsKey(loginName))
            .ToArray();

        if (invalidRecipients.Length > 0)
        {
            error = $"One or more recipients are unavailable or outside your authorised divisions: {string.Join(", ", invalidRecipients)}.";
            return false;
        }

        recipientMetadata = recipientLoginNames
            .Select(loginName => eligibleByLogin[loginName])
            .ToArray();
        recipientLoginNames = recipientMetadata
            .Select(recipient => recipient.LoginName)
            .ToArray();
        return true;
    }

    private SavedViewRecipientDto[] GetEligibleRecipients(
        User identity,
        string ownerLoginName,
        string? search = null)
    {
        var callerDivisions = SplitCsv(identity.Division).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var users = _repository.GetUsers();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            users = users.Where(user =>
                (user.FullName != null && user.FullName.ToUpper().Contains(normalizedSearch))
                || (user.LoginName != null && user.LoginName.ToUpper().Contains(normalizedSearch)));
        }

        return users
            .AsEnumerable()
            .Where(user => !string.IsNullOrWhiteSpace(user.LoginName))
            .Where(user => !string.Equals(user.LoginName.Trim(), ownerLoginName.Trim(), StringComparison.OrdinalIgnoreCase))
            .Where(user => identity.IsSuperAdmin
                || SplitCsv(user.Division).Any(callerDivisions.Contains))
            .GroupBy(user => user.LoginName.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Select(user => new SavedViewRecipientDto
            {
                LoginName = user.LoginName.Trim(),
                FullName = string.IsNullOrWhiteSpace(user.FullName)
                    ? user.LoginName.Trim()
                    : user.FullName.Trim(),
            })
            .OrderBy(user => user.FullName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(user => user.LoginName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsRecipientMatch(SavedViewRecipientDto candidate, string search)
    {
        return candidate.FullName.Contains(search, StringComparison.OrdinalIgnoreCase)
            || candidate.LoginName.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private static int GetRecipientMatchRank(SavedViewRecipientDto candidate, string search)
    {
        if (string.Equals(candidate.FullName, search, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.LoginName, search, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (candidate.FullName.StartsWith(search, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (candidate.LoginName.StartsWith(search, StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (candidate.FullName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(part => part.StartsWith(search, StringComparison.OrdinalIgnoreCase)))
        {
            return 3;
        }

        return candidate.FullName.Contains(search, StringComparison.OrdinalIgnoreCase) ? 4 : 5;
    }

    private static bool CanReadView(View view, string loginName, string[] userDivisions)
    {
        if (string.Equals(view.Owner, loginName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (view.ForEveryone == 1)
        {
            return true;
        }

        if (view.ViewRecipients.Any(recipient =>
            string.Equals(recipient.RecipientLoginName, loginName, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (view.ForEveryone != 2)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(view.ForDivisions))
        {
            return true;
        }

        var viewDivisions = SplitCsv(view.ForDivisions);
        return viewDivisions.Intersect(userDivisions, StringComparer.OrdinalIgnoreCase).Any();
    }

    private static string NormalizeName(string? rawName)
    {
        var trimmed = rawName?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        return trimmed.Length <= 80 ? trimmed : trimmed[..80];
    }

    private static string[] SplitCsv(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        return raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string[] NormalizeRecipientLoginNames(
        IReadOnlyCollection<string>? rawRecipients,
        string ownerLoginName)
    {
        return rawRecipients?
            .Where(loginName => !string.IsNullOrWhiteSpace(loginName))
            .Select(loginName => loginName.Trim())
            .Where(loginName => !string.Equals(loginName, ownerLoginName.Trim(), StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? [];
    }

    private static bool TryParsePage(string? page, out bool assetView, out string normalizedPage)
    {
        assetView = false;
        normalizedPage = string.Empty;

        if (string.Equals(page, "agreements", StringComparison.OrdinalIgnoreCase))
        {
            normalizedPage = "agreements";
            assetView = false;
            return true;
        }

        if (string.Equals(page, "orders", StringComparison.OrdinalIgnoreCase))
        {
            normalizedPage = "agreements";
            assetView = false;
            return true;
        }

        if (string.Equals(page, "assets", StringComparison.OrdinalIgnoreCase))
        {
            normalizedPage = "assets";
            assetView = true;
            return true;
        }

        return false;
    }

    private static bool TryParseScope(string? scope, out int forEveryone, out bool usersScope)
    {
        forEveryone = 0;
        usersScope = false;

        if (string.Equals(scope, "personal", StringComparison.OrdinalIgnoreCase))
        {
            forEveryone = 0;
            return true;
        }

        if (string.Equals(scope, "users", StringComparison.OrdinalIgnoreCase))
        {
            usersScope = true;
            return true;
        }

        if (string.Equals(scope, "global", StringComparison.OrdinalIgnoreCase))
        {
            forEveryone = 1;
            return true;
        }

        if (string.Equals(scope, "division", StringComparison.OrdinalIgnoreCase))
        {
            forEveryone = 2;
            return true;
        }

        return false;
    }

    private static bool TryParseFrontendEnvelope(string rawJson, string normalizedPage, out FrontendViewEnvelope envelope)
    {
        envelope = new FrontendViewEnvelope();

        try
        {
            var parsed = JsonSerializer.Deserialize<FrontendViewEnvelope>(rawJson);
            if (parsed == null)
            {
                return false;
            }

            if (!string.Equals(parsed.App, FrontendViewApp, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (parsed.Version < 1)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(parsed.Page)
                && !IsMatchingPage(parsed.Page, normalizedPage))
            {
                return false;
            }

            if (parsed.State.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            envelope = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string SerializeFrontendEnvelope(string page, JsonElement state)
    {
        var envelope = new FrontendViewEnvelope
        {
            App = FrontendViewApp,
            Version = FrontendViewVersion,
            Page = page,
            State = state.Clone(),
        };

        return JsonSerializer.Serialize(envelope);
    }

    private static bool IsMatchingPage(string pageFromEnvelope, string normalizedPage)
    {
        if (string.Equals(pageFromEnvelope, normalizedPage, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(normalizedPage, "agreements", StringComparison.OrdinalIgnoreCase)
            && string.Equals(pageFromEnvelope, "orders", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class UpsertSavedViewRequest
{
    public string Name { get; init; } = string.Empty;
    public string Page { get; init; } = string.Empty;
    public string Scope { get; init; } = "personal";
    public string[] Recipients { get; init; } = [];
    public JsonElement State { get; init; }
}

public sealed class SavedViewDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Page { get; init; } = string.Empty;
    public string Scope { get; init; } = "personal";
    public string Owner { get; init; } = string.Empty;
    public bool IsOwner { get; init; }
    public bool CanEdit { get; init; }
    public bool CanDelete { get; init; }
    public SavedViewRecipientDto[] Recipients { get; init; } = [];
    public bool IsDefault { get; init; }
    public JsonElement State { get; init; }
}

public sealed class SavedViewRecipientDto
{
    public string LoginName { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
}

public sealed class FrontendViewEnvelope
{
    public string App { get; init; } = string.Empty;
    public int Version { get; init; }
    public string Page { get; init; } = string.Empty;
    public JsonElement State { get; init; }
}
