namespace OF.WebApp.Features.Divisions;

public static class DivisionDisplayName
{
    public static string Resolve(string? divisionCode, string? sourceName) =>
        string.Equals(divisionCode?.Trim(), "200", StringComparison.OrdinalIgnoreCase)
            ? "USA"
            : sourceName?.Trim() ?? string.Empty;
}
