using OF.Data.Database;
using OF.UI.Models;

namespace OF.WebApp.Features.Ringfences;

internal static class RingfenceResponseMapper
{
    internal static RingfenceResponse MapRingfence(Ringfence ringfence) => new()
    {
        Id = ringfence.Id,
        FromDate = ringfence.FromDate,
        ToDate = ringfence.ToDate,
        Title = ringfence.Title,
        LastUpdatedBy = ringfence.LastUpdatedBy,
        LastUpdatedDate = ringfence.LastUpdatedDate,
        Divisions = ringfence.Divisions,
        Owner = ringfence.Owner,
        Warehouse = ringfence.Warehouse,
        CreatedAt = ringfence.CreatedAt,
        CreatedBy = ringfence.CreatedBy,
    };

    internal static RingfenceItemResponse MapItem(RingfenceItem item) => new()
    {
        Id = item.Id,
        RingfenceId = item.RingfenceId,
        AssetId = item.AssetId,
        LastUpdatedBy = item.LastUpdatedBy,
        LastUpdatedDate = item.LastUpdatedDate,
        CreatedAt = item.CreatedAt,
        CreatedBy = item.CreatedBy,
    };

    internal static RingfenceAssetResponse MapAsset(VwAssetItem asset) => new()
    {
        Id = asset.Id,
        Division = asset.Division,
        Warehouse = asset.Warehouse,
        Description = asset.Description,
        ItemNumber = asset.ItemNumber,
        Status = asset.Status,
        WarehouseLocation = asset.WarehouseLocation,
    };

    internal static RingfenceOverlapDetailResponse MapOverlap(RingfenceAssestDetails overlap) => new()
    {
        RingfenceId = overlap.RingfenceId,
        AssetIds = overlap.AssetIds?.ToList() ?? [],
        Title = overlap.Title,
        FromDate = overlap.FromDate,
        ToDate = overlap.ToDate,
        Owner = overlap.Owner,
    };
}
