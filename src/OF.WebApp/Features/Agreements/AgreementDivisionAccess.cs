using OF.Data.Database;

namespace OF.WebApp.Features.Agreements;

internal static class AgreementDivisionAccess
{
    public static string[] ParseDivisions(string? divisions) =>
        divisions?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(division => division.ToUpperInvariant())
            .Distinct()
            .ToArray()
        ?? [];

    public static bool CanAccess(User identity, string? agreementDivision) =>
        identity.IsSuperAdmin
        || (!string.IsNullOrWhiteSpace(agreementDivision)
            && ParseDivisions(identity.Division).Contains(agreementDivision.ToUpperInvariant()));
}
