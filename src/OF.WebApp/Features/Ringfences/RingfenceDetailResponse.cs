namespace OF.WebApp.Features.Ringfences;

public sealed class RingfenceResponse
{
    public int Id { get; init; }
    public DateTime FromDate { get; init; }
    public DateTime ToDate { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? LastUpdatedBy { get; init; }
    public DateTime? LastUpdatedDate { get; init; }
    public string Divisions { get; init; } = string.Empty;
    public string? Owner { get; init; }
    public string? Warehouse { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
}

public sealed class RingfenceItemResponse
{
    public int Id { get; init; }
    public int? RingfenceId { get; init; }
    public string AssetId { get; init; } = string.Empty;
    public string? LastUpdatedBy { get; init; }
    public DateTime? LastUpdatedDate { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
}

public sealed class RingfenceDetailResponse
{
    public RingfenceResponse Ringfence { get; init; } = null!;
    public List<RingfenceItemResponse> Items { get; init; } = [];
    public List<RingfenceAssetResponse> Assets { get; init; } = [];
}

public sealed class RingfenceAssetResponse
{
    public string Id { get; init; } = string.Empty;
    public string? Division { get; init; }
    public string? Warehouse { get; init; }
    public string? Description { get; init; }
    public string? ItemNumber { get; init; }
    public string? Status { get; init; }
    public string? WarehouseLocation { get; init; }
}

public sealed class RingfenceOverlapResponse
{
    public List<RingfenceOverlapDetailResponse> Overlaps { get; init; } = [];
}

public sealed class RingfenceOverlapDetailResponse
{
    public int RingfenceId { get; init; }
    public List<string> AssetIds { get; init; } = [];
    public string Title { get; init; } = string.Empty;
    public DateTime FromDate { get; init; }
    public DateTime ToDate { get; init; }
    public string? Owner { get; init; }
}

public sealed class RingfenceItemBatchResponse
{
    public List<string> ReadyAssetIds { get; init; } = [];
    public List<string> AlreadyAssignedAssetIds { get; init; } = [];
    public List<string> UnavailableAssetIds { get; init; } = [];
    public List<string> AddedAssetIds { get; init; } = [];
    public List<RingfenceOverlapDetailResponse> Overlaps { get; set; } = [];
    public bool RequiresOverlapAcknowledgement => Overlaps.Count > 0;
}
