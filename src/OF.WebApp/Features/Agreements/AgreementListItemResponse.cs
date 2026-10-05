namespace OF.WebApp.Features.Agreements;

public sealed class AgreementListItemResponse
{
    public int Id { get; init; }
    public string? AgreementNumber { get; init; }
    public string? CustomerName { get; init; }
    public string? CustomerNumber { get; init; }
    public string Division { get; init; } = null!;
    public string? Warehouse { get; init; }
    public int FulfilmentStatus { get; init; }
    public DateTime? OnHireDate { get; init; }
    public DateTime? OffHireDate { get; init; }
    public bool IsDeleted { get; init; }
    public string OrderSource { get; init; } = null!;
    public int? LineCount { get; init; }
    public DateTime? DeliveryDate { get; init; }
    public DateTime? ValidFromDate { get; init; }
    public DateTime? ValidToDate { get; init; }
    public DateTime? TerminationDate { get; init; }
    public DateTime? CollectionDate { get; init; }
    public string? CustomerAddress { get; init; }
    public string? LastUpdatedByName { get; init; }
    public string? OpportunityName { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public DateTime? LastUpdatedDate { get; init; }
    public string? OpportunityStage { get; init; }
    public float? Probability { get; init; }
    public int? MinFulfilmentStatus { get; init; }
    public int? MaxFulfilmentStatus { get; init; }
    public int NoteCount { get; init; }
}
