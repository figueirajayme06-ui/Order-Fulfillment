namespace OF.WebApp.Features.Ringfences;

public sealed class RingfenceListItemResponse
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public DateTime FromDate { get; init; }
    public DateTime ToDate { get; init; }
    public string Divisions { get; init; } = string.Empty;
    public string? Warehouse { get; init; }
    public string? Owner { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public int AssetCount { get; init; }
}
