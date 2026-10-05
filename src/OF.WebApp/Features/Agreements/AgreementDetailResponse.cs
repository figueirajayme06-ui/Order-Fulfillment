using OF.Data.Database;

namespace OF.WebApp.Features.Agreements;

public sealed class AgreementDetailResponse
{
    public AgreementHeaderDetailResponse Header { get; init; } = null!;
    public List<AgreementLineDetailResponse> Lines { get; init; } = [];
}

public sealed class AgreementHeaderDetailResponse
{
    public bool IsActivated { get; init; }
    public int Id { get; init; }
    public string? QuotePublicId { get; init; }
    public string? AgreementNumber { get; init; }
    public DateTime? OnHireDate { get; init; }
    public DateTime? OffHireDate { get; init; }
    public string? Status { get; init; }
    public string? CustomerName { get; init; }
    public string? CustomerAddress { get; init; }
    public string? CustomerNumber { get; init; }
    public string Division { get; init; } = null!;
    public string? CustomerAddressCode { get; init; }
    public string OrderSource { get; init; } = null!;
    public long ChangeSequence { get; init; }
    public bool IsDeleted { get; init; }
    public int FulfilmentStatus { get; init; }
    public string? LastUpdatedBy { get; init; }
    public DateTime? LastUpdatedDate { get; init; }
    public string Facility { get; init; } = null!;
    public string? OpportunityNumber { get; init; }
    public string? OrderNumber { get; init; }
    public string? QuoteNumber { get; init; }
    public string? AgreementNumbersOnly { get; init; }
    public string? QuotePublicIdNumbersOnly { get; init; }
    public string? OverviewOfService { get; init; }
    public double? Probability { get; init; }
    public string? ArmcontactName { get; init; }
    public string? ArmcontactEmail { get; init; }
    public string? ArmcontactPhone { get; init; }
    public int ActivationStatus { get; init; }
    public string? ActivationErrors { get; init; }
    public string? ActivationInstanceId { get; init; }
    public string? RentalDepot { get; init; }
    public string? OpportunityName { get; init; }
    public string? OpportunityStage { get; init; }
    public bool? IsSkeleton { get; init; }
}

public sealed class AgreementLineDetailResponse
{
    public bool IsSubline { get; init; }
    public int Id { get; init; }
    public int? HeaderId { get; init; }
    public string? OrderLineNumber { get; init; }
    public string? AgreementLineNumber { get; init; }
    public string? ItemNumber { get; init; }
    public DateTime? DeliveryDate { get; init; }
    public DateTime ValidToDate { get; init; }
    public DateTime? TerminationDate { get; init; }
    public float Quantity { get; init; }
    public string? AgreementLineType { get; init; }
    public string Warehouse { get; init; } = null!;
    public string? Status { get; init; }
    public string Division { get; init; } = null!;
    public string? PackageGroupNumber { get; init; }
    public string? Attributes { get; init; }
    public string? LocalizedAttributes { get; init; }
    public DateTime ValidFromDate { get; init; }
    public long ChangeSequence { get; init; }
    public string? GenericItemNumber { get; init; }
    public bool IsDeleted { get; init; }
    public int FulfilmentStatus { get; init; }
    public double QuantityFulfilled { get; init; }
    public string? LastUpdatedBy { get; init; }
    public DateTime? LastUpdatedDate { get; init; }
    public int? AgreementLineIndex { get; init; }
    public string Facility { get; init; } = null!;
    public int? OrderLineIndex { get; init; }
    public int? QuoteLineIndex { get; init; }
    public string? QuoteLineNumber { get; init; }
    public string OrderSource { get; init; } = null!;
    public string? AgreementNumbersOnly { get; init; }
    public string? QuotePublicId { get; init; }
    public string? QuotePublicIdNumbersOnly { get; init; }
    public string? NumberOfShifts { get; init; }
    public string? ActivationErrors { get; init; }
    public string? RateType { get; init; }
    public DateTime? CollectionDate { get; init; }
    public string? DescriptionWithAttributes { get; init; }
    public int ActivationStatus { get; init; }
    public string? ActivationInstanceId { get; init; }
    public string? ItemDescription { get; init; }
    public bool RequiresFulfilment { get; init; }
}

internal static class AgreementDetailResponseMapper
{
    internal static AgreementDetailResponse Map(Header header, IEnumerable<Line> lines) => new()
    {
        Header = MapHeader(header),
        Lines = lines.Select(MapLine).ToList(),
    };

    private static AgreementHeaderDetailResponse MapHeader(Header header) => new()
    {
        IsActivated = header.IsActivated,
        Id = header.Id,
        QuotePublicId = header.QuotePublicId,
        AgreementNumber = header.AgreementNumber,
        OnHireDate = header.OnHireDate,
        OffHireDate = header.OffHireDate,
        Status = header.Status,
        CustomerName = header.CustomerName,
        CustomerAddress = header.CustomerAddress,
        CustomerNumber = header.CustomerNumber,
        Division = header.Division,
        CustomerAddressCode = header.CustomerAddressCode,
        OrderSource = header.OrderSource,
        ChangeSequence = header.ChangeSequence,
        IsDeleted = header.IsDeleted,
        FulfilmentStatus = header.FulfilmentStatus,
        LastUpdatedBy = header.LastUpdatedBy,
        LastUpdatedDate = header.LastUpdatedDate,
        Facility = header.Facility,
        OpportunityNumber = header.OpportunityNumber,
        OrderNumber = header.OrderNumber,
        QuoteNumber = header.QuoteNumber,
        AgreementNumbersOnly = header.AgreementNumbersOnly,
        QuotePublicIdNumbersOnly = header.QuotePublicIdNumbersOnly,
        OverviewOfService = header.OverviewOfService,
        Probability = header.Probability,
        ArmcontactName = header.ArmcontactName,
        ArmcontactEmail = header.ArmcontactEmail,
        ArmcontactPhone = header.ArmcontactPhone,
        ActivationStatus = header.ActivationStatus,
        ActivationErrors = header.ActivationErrors,
        ActivationInstanceId = header.ActivationInstanceId,
        RentalDepot = header.RentalDepot,
        OpportunityName = header.OpportunityName,
        OpportunityStage = header.OpportunityStage,
        IsSkeleton = header.IsSkeleton,
    };

    private static AgreementLineDetailResponse MapLine(Line line) => new()
    {
        IsSubline = line.IsSubline,
        Id = line.Id,
        HeaderId = line.HeaderId,
        OrderLineNumber = line.OrderLineNumber,
        AgreementLineNumber = line.AgreementLineNumber,
        ItemNumber = line.ItemNumber,
        DeliveryDate = line.DeliveryDate,
        ValidToDate = line.ValidToDate,
        TerminationDate = line.TerminationDate,
        Quantity = line.Quantity,
        AgreementLineType = line.AgreementLineType,
        Warehouse = line.Warehouse,
        Status = line.Status,
        Division = line.Division,
        PackageGroupNumber = line.PackageGroupNumber,
        Attributes = line.Attributes,
        LocalizedAttributes = line.LocalizedAttributes,
        ValidFromDate = line.ValidFromDate,
        ChangeSequence = line.ChangeSequence,
        GenericItemNumber = line.GenericItemNumber,
        IsDeleted = line.IsDeleted,
        FulfilmentStatus = line.FulfilmentStatus,
        QuantityFulfilled = line.QuantityFulfilled,
        LastUpdatedBy = line.LastUpdatedBy,
        LastUpdatedDate = line.LastUpdatedDate,
        AgreementLineIndex = line.AgreementLineIndex,
        Facility = line.Facility,
        OrderLineIndex = line.OrderLineIndex,
        QuoteLineIndex = line.QuoteLineIndex,
        QuoteLineNumber = line.QuoteLineNumber,
        OrderSource = line.OrderSource,
        AgreementNumbersOnly = line.AgreementNumbersOnly,
        QuotePublicId = line.QuotePublicId,
        QuotePublicIdNumbersOnly = line.QuotePublicIdNumbersOnly,
        NumberOfShifts = line.NumberOfShifts,
        ActivationErrors = line.ActivationErrors,
        RateType = line.RateType,
        CollectionDate = line.CollectionDate,
        DescriptionWithAttributes = line.DescriptionWithAttributes,
        ActivationStatus = line.ActivationStatus,
        ActivationInstanceId = line.ActivationInstanceId,
        ItemDescription = line.ItemDescription,
        RequiresFulfilment = line.RequiresFulfilment,
    };
}
