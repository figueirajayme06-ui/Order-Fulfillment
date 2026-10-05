namespace OF.WebApp.Features.Assets;

public sealed record AssetEnrichmentResponse(
    AssetLocationObservation? Location,
    IReadOnlyList<AssetServiceHistoryItem> ServiceHistory,
    IReadOnlyList<AssetRetrofitItem> Retrofits,
    IReadOnlyList<AssetRentalHistoryItem> RentalHistory,
    AssetTelemetrySummary? Telemetry = null);

public sealed record AssetTelemetrySummary(
    string FitmentStatus,
    bool HasDeviceMapping,
    DateTime? DeviceStatusUpdatedAt,
    DateTime? DeviceStatusIngestedAt);

public sealed record AssetLocationObservation(
    decimal Latitude,
    decimal Longitude,
    DateTime ObservedAt,
    DateTime? IngestedAt,
    int? Validity,
    int? SatelliteCount,
    double? HorizontalAccuracy,
    string? AssignedLocation,
    string TelemetryFitment);

public sealed record AssetServiceHistoryItem(
    string ServiceOrderNumber,
    int ServiceOrderJobNumber,
    string? Status,
    DateTime? CreatedDate,
    DateTime? FinishedDate,
    string? Type,
    decimal? MeterReading,
    DateTime? MeterDate,
    string? ErrorSymptom,
    string? ErrorCause,
    string? Action,
    string? ActionText,
    int DetailCount);

public sealed record AssetRetrofitItem(
    string Document,
    string? Description,
    string? Status,
    DateTime? OpenedAt,
    DateTime? CompletedAt,
    string? ServiceOrder);

public sealed record AssetRentalHistoryItem(
    string AgreementNumber,
    string? CustomerNumber,
    string? CustomerName,
    DateTime? ValidFrom,
    DateTime? ValidTo,
    DateTime? TerminationDate);

public sealed class AssetEnrichmentUnavailableException(string message, Exception innerException)
    : Exception(message, innerException);
