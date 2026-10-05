namespace OF.WebApp.Features.Assets;

public sealed class AssetListItemResponse
{
    public string Id { get; init; } = null!;
    public string IndividualItemNumber { get; init; } = null!;
    public string? ItemNumber { get; init; }
    public string? Status { get; init; }
    public string? Warehouse { get; init; }
    public string? Division { get; init; }
    public string? Facility { get; init; }
    public DateTime? EstimatedReadyDate { get; init; }
    public string? AgreementNumber { get; init; }
    public string? CustomerName { get; init; }
    public DateTime? DeliveryDate { get; init; }
    public DateTime? AgreementLineValidFromDate { get; init; }
    public DateTime? AgreementLineValidToDate { get; init; }
    public string? Description { get; init; }
    public string? WarehouseLocation { get; init; }
    public DateTime? CollectionDate { get; init; }
    public DateTime? TerminationDate { get; init; }
    public int? DaysOffHire { get; init; }
    public string? WarehouseName { get; init; }
    public string? CustomerNumber { get; init; }
    public string? ProductGroup { get; init; }
    public string? ProductCategory { get; init; }
    public double? RunHours { get; init; }
    public string? Size { get; init; }
    public string? TelemetryStatus { get; init; }
    public string? Remark { get; init; }
    public int NoteCount { get; set; }
}
