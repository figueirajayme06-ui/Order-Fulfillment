using OF.Data.Database;

namespace OF.WebApp.Features.Divisions;

public static class DivisionAccess
{
    public static string[] Resolve(User identity, string? requestedDivisions, char[] separators)
    {
        var hasRequestedDivisions = !string.IsNullOrWhiteSpace(requestedDivisions);
        var requested = Parse(requestedDivisions, separators);
        if (identity.IsSuperAdmin)
        {
            return requested;
        }

        var allowed = Parse(identity.Division, [',']);
        if (!hasRequestedDivisions)
        {
            return allowed;
        }

        if (requested.Length == 0)
        {
            return [];
        }

        var allowedLookup = allowed.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return requested.Where(allowedLookup.Contains).ToArray();
    }

    public static bool CanAccess(User identity, string? resourceDivision)
    {
        if (identity.IsSuperAdmin)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(resourceDivision))
        {
            return false;
        }

        return Parse(identity.Division, [',']).Contains(resourceDivision.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    public static bool CanAccessAny(User identity, string? resourceDivisions, char[] separators)
    {
        if (identity.IsSuperAdmin)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(resourceDivisions))
        {
            return false;
        }

        return Resolve(identity, resourceDivisions, separators).Length > 0;
    }

    public static bool CanAssignAll(User identity, string? resourceDivisions, char[] separators)
    {
        if (identity.IsSuperAdmin)
        {
            return true;
        }

        var requested = Parse(resourceDivisions, separators);
        if (requested.Length == 0)
        {
            return false;
        }

        var allowed = Parse(identity.Division, [',']).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return requested.All(allowed.Contains);
    }

    private static string[] Parse(string? divisions, char[] separators) =>
        divisions?
            .Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(division => !string.IsNullOrWhiteSpace(division))
            .Select(division => division.ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
        ?? [];
}
