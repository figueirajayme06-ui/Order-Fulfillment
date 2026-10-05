using OF.Data.Database;

namespace OF.WebApp.Features.Assets;

public sealed class AssetDetailResponse
{
    public string Id { get; init; } = null!;
    public string IndividualItemNumber { get; init; } = null!;
    public string? StatusCode { get; init; }
    public string? Warehouse { get; init; }
    public string? ShipAddress1 { get; init; }
    public string? AgreementNumber { get; init; }
    public DateTime? DeliveryDate { get; init; }
    public DateTime? AgreementLineValidToDate { get; init; }
    public DateTime? TerminationDate { get; init; }
    public string? CustomerName { get; init; }
    public string? TelemetryStatus { get; init; }
    public string? ServiceCenter { get; init; }
    public string? Description { get; init; }
    public string? Status { get; init; }
    public string? ManufacturerName { get; init; }
    public string? OwnerServiceCenter { get; init; }
    public string? CustomerNumber { get; init; }
    public string? ItemNumber { get; init; }
    public string? ShipAddress3 { get; init; }
    public string? Facility { get; init; }
    public DateTime? IonlastModified { get; init; }
    public string? WarehouseLocation { get; init; }
    public string? Division { get; init; }
    public string? Remark { get; init; }
    public string? ProductGroup { get; init; }
    public string? ProductCategory { get; init; }
    public string? InternationalSizeRating { get; init; }
    public string? UsSizeRating { get; init; }
    public double? RunHours { get; init; }
    public DateTime? EstimatedReadyDate { get; init; }
    public DateTime? AgreementLineValidFromDate { get; init; }
    public DateTime? CollectionDate { get; init; }
}

internal static class AssetDetailResponseMapper
{
    internal static AssetDetailResponse Map(Asset asset) => new()
    {
        Id = asset.Id,
        IndividualItemNumber = asset.IndividualItemNumber,
        StatusCode = asset.StatusCode,
        Warehouse = asset.Warehouse,
        ShipAddress1 = asset.ShipAddress1,
        AgreementNumber = asset.AgreementNumber,
        DeliveryDate = asset.DeliveryDate,
        AgreementLineValidToDate = asset.AgreementLineValidToDate,
        TerminationDate = asset.TerminationDate,
        CustomerName = asset.CustomerName,
        TelemetryStatus = asset.TelemetryStatus,
        ServiceCenter = asset.ServiceCenter,
        Description = asset.Description,
        Status = asset.Status,
        ManufacturerName = asset.ManufacturerName,
        OwnerServiceCenter = asset.OwnerServiceCenter,
        CustomerNumber = asset.CustomerNumber,
        ItemNumber = asset.ItemNumber,
        ShipAddress3 = asset.ShipAddress3,
        Facility = asset.Facility,
        IonlastModified = asset.IonlastModified,
        WarehouseLocation = asset.WarehouseLocation,
        Division = asset.Division,
        Remark = asset.Remark,
        ProductGroup = asset.ProductGroup,
        ProductCategory = asset.ProductCategory,
        InternationalSizeRating = asset.InternationalSizeRating,
        UsSizeRating = asset.UsSizeRating,
        RunHours = asset.RunHours,
        EstimatedReadyDate = asset.EstimatedReadyDate,
        AgreementLineValidFromDate = asset.AgreementLineValidFromDate,
        CollectionDate = asset.CollectionDate,
    };
}
