using OF.Data.Database;
using OF.UI.Models;

namespace OF.WebApp.Features.Assets;

internal static class AssetProfileScheduleBuilder
{
    public static AssetProfileResponse Build(
        Asset asset,
        IEnumerable<AssetScheduleReservation> reservations,
        IEnumerable<Ringfence> ringfences,
        DateTime fromDate,
        DateTime toDate,
        DateTime today)
    {
        var events = new List<AssetScheduleEvent>();

        AddCurrentStatusEvent(events, asset, fromDate, toDate, today);
        AddReservationEvents(events, reservations);
        AddRingfenceEvents(events, ringfences);

        var schedule = events
            .Where(item => IsInRange(item, fromDate, toDate))
            .OrderBy(item => item.StartDate)
            .ThenBy(item => item.EndDate ?? DateTime.MaxValue)
            .ToList();

        MarkConflicts(schedule);

        return new AssetProfileResponse
        {
            Asset = AssetProfileAsset.From(asset),
            Summary = BuildSummary(asset, schedule, today),
            Events = schedule,
        };
    }

    private static void AddCurrentStatusEvent(List<AssetScheduleEvent> events, Asset asset, DateTime fromDate, DateTime toDate, DateTime today)
    {
        var status = NormalizeStatus(asset.Status);

        switch (status)
        {
            case "ONHIRE":
                AddEvent(
                    events,
                    "current-on-hire",
                    string.IsNullOrWhiteSpace(asset.AgreementNumber) ? "On hold" : "On hire",
                    "Current",
                    asset.DeliveryDate ?? asset.AgreementLineValidFromDate ?? fromDate,
                    asset.TerminationDate ?? asset.AgreementLineValidToDate,
                    string.IsNullOrWhiteSpace(asset.AgreementNumber) ? "On hold" : asset.AgreementNumber,
                    asset.AgreementNumber,
                    asset.CustomerName,
                    asset.Warehouse,
                    "Asset status");
                break;

            case "SERVICE":
            case "ASSESS":
            case "INSERVICE":
                AddEvent(
                    events,
                    "current-service",
                    "Service / assessment",
                    "Current",
                    asset.IonlastModified ?? today,
                    asset.EstimatedReadyDate,
                    asset.EstimatedReadyDate.HasValue ? "Service in progress" : "Ready date not recorded",
                    null,
                    null,
                    asset.Warehouse,
                    "Asset status");
                break;

            case "REPAIR":
                AddEvent(
                    events,
                    "current-repair",
                    "Repair",
                    "Current",
                    asset.IonlastModified ?? today,
                    asset.EstimatedReadyDate,
                    asset.EstimatedReadyDate.HasValue ? "Repair in progress" : "Ready date not recorded",
                    null,
                    null,
                    asset.Warehouse,
                    "Asset status");
                break;

            case "COLLECTION":
                AddEvent(
                    events,
                    "current-collection",
                    "Collection",
                    "Current",
                    asset.DeliveryDate ?? asset.AgreementLineValidFromDate ?? today,
                    asset.CollectionDate,
                    asset.AgreementNumber ?? "Collection in progress",
                    asset.AgreementNumber,
                    asset.CustomerName,
                    asset.Warehouse,
                    "Asset status");
                break;

            case "INTRANSIT":
                AddEvent(
                    events,
                    "current-transit",
                    "In transit",
                    "Current",
                    asset.IonlastModified ?? today,
                    (asset.IonlastModified ?? today).AddDays(3),
                    "Transport in progress",
                    null,
                    null,
                    asset.Warehouse,
                    "Asset status");
                break;
        }
    }

    private static void AddReservationEvents(List<AssetScheduleEvent> events, IEnumerable<AssetScheduleReservation> reservations)
    {
        foreach (var reservation in reservations)
        {
            AddEvent(
                events,
                $"reservation-{reservation.ReservationId}",
                "Reservation",
                reservation.IsConfirmed ? "Confirmed" : "Planned",
                reservation.StartDate,
                reservation.EndDate,
                reservation.AgreementNumber ?? "Agreement not recorded",
                reservation.AgreementNumber,
                reservation.CustomerName,
                reservation.Warehouse,
                "Reservation",
                reservation.HeaderId);
        }
    }

    private static void AddRingfenceEvents(List<AssetScheduleEvent> events, IEnumerable<Ringfence> ringfences)
    {
        foreach (var ringfence in ringfences)
        {
            AddEvent(
                events,
                $"ringfence-{ringfence.Id}",
                "Ringfence",
                "Protected",
                ringfence.FromDate,
                ringfence.ToDate,
                ringfence.Title,
                null,
                null,
                ringfence.Warehouse,
                "Ringfence");
        }
    }

    private static void AddEvent(
        ICollection<AssetScheduleEvent> events,
        string id,
        string type,
        string status,
        DateTime startDate,
        DateTime? endDate,
        string title,
        string? agreementNumber,
        string? customerName,
        string? warehouse,
        string source,
        int? agreementId = null)
    {
        events.Add(new AssetScheduleEvent
        {
            Id = id,
            Type = type,
            Status = status,
            StartDate = startDate.Date,
            EndDate = endDate?.Date,
            Title = title,
            AgreementId = agreementId,
            AgreementNumber = agreementNumber,
            CustomerName = customerName,
            Warehouse = warehouse,
            Source = source,
        });
    }

    private static AssetScheduleSummary BuildSummary(Asset asset, IReadOnlyList<AssetScheduleEvent> events, DateTime today)
    {
        var normalizedStatus = NormalizeStatus(asset.Status);
        var readinessUnknown = normalizedStatus is "SERVICE" or "ASSESS" or "INSERVICE" or "REPAIR"
            && !asset.EstimatedReadyDate.HasValue;

        var currentEvents = events
            .Where(item => item.EndDate.HasValue && item.StartDate <= today && item.EndDate.Value >= today)
            .ToList();
        var nextCommitment = events
            .Where(item => item.StartDate >= today)
            .OrderBy(item => item.StartDate)
            .FirstOrDefault();

        DateTime? nextAvailableDate = null;
        string availabilityStatus;

        if (normalizedStatus is "REMOVEDSTOCK" or "SCRAP" or "SOLD")
        {
            availabilityStatus = "Out of fleet";
        }
        else if (readinessUnknown)
        {
            availabilityStatus = "Ready date required";
        }
        else if (currentEvents.Count > 0)
        {
            nextAvailableDate = currentEvents.Max(item => item.EndDate)!.Value.AddDays(1);
            availabilityStatus = "Committed";
        }
        else if (normalizedStatus == "AVAILABLE")
        {
            nextAvailableDate = today;
            availabilityStatus = nextCommitment == null ? "Available" : "Available before next commitment";
        }
        else
        {
            availabilityStatus = "Review current status";
        }

        return new AssetScheduleSummary
        {
            NextAvailableDate = nextAvailableDate,
            AvailabilityStatus = availabilityStatus,
            NextCommitmentDate = nextCommitment?.StartDate,
            NextCommitmentLabel = nextCommitment == null ? null : $"{nextCommitment.Type}: {nextCommitment.Title}",
            ConflictCount = events.Count(item => item.HasConflict),
        };
    }

    private static void MarkConflicts(IReadOnlyList<AssetScheduleEvent> events)
    {
        for (var index = 0; index < events.Count; index++)
        {
            var current = events[index];
            if (!current.EndDate.HasValue)
            {
                continue;
            }

            for (var comparisonIndex = index + 1; comparisonIndex < events.Count; comparisonIndex++)
            {
                var comparison = events[comparisonIndex];
                if (!comparison.EndDate.HasValue || !DatesOverlap(current, comparison))
                {
                    continue;
                }

                current.AddConflict($"Overlaps {comparison.Type.ToLowerInvariant()}: {comparison.Title}");
                comparison.AddConflict($"Overlaps {current.Type.ToLowerInvariant()}: {current.Title}");
            }
        }
    }

    private static bool DatesOverlap(AssetScheduleEvent first, AssetScheduleEvent second)
    {
        return first.StartDate <= second.EndDate!.Value && second.StartDate <= first.EndDate!.Value;
    }

    private static bool IsInRange(AssetScheduleEvent item, DateTime fromDate, DateTime toDate)
    {
        return item.StartDate <= toDate && (!item.EndDate.HasValue || item.EndDate.Value >= fromDate);
    }

    private static string NormalizeStatus(string? status)
    {
        return (status ?? string.Empty).Replace(" ", string.Empty).ToUpperInvariant();
    }
}

internal sealed class AssetProfileResponse
{
    public required AssetProfileAsset Asset { get; init; }
    public required AssetScheduleSummary Summary { get; init; }
    public required IReadOnlyList<AssetScheduleEvent> Events { get; init; }
}

internal sealed class AssetProfileAsset
{
    public required string Id { get; init; }
    public required string IndividualItemNumber { get; init; }
    public string? ItemNumber { get; init; }
    public string? Description { get; init; }
    public string? Status { get; init; }
    public string? Warehouse { get; init; }
    public string? WarehouseLocation { get; init; }
    public string? Facility { get; init; }
    public string? Division { get; init; }
    public string? AgreementNumber { get; init; }
    public string? CustomerName { get; init; }
    public DateTime? DeliveryDate { get; init; }
    public DateTime? AgreementLineValidFromDate { get; init; }
    public DateTime? AgreementLineValidToDate { get; init; }
    public DateTime? TerminationDate { get; init; }
    public DateTime? CollectionDate { get; init; }
    public DateTime? EstimatedReadyDate { get; init; }
    public string? TelemetryStatus { get; init; }
    public string? ManufacturerName { get; init; }
    public string? ProductGroup { get; init; }
    public string? ProductCategory { get; init; }
    public double? RunHours { get; init; }
    public string? ServiceCenter { get; init; }

    public static AssetProfileAsset From(Asset asset)
    {
        return new AssetProfileAsset
        {
            Id = asset.Id,
            IndividualItemNumber = asset.IndividualItemNumber,
            ItemNumber = asset.ItemNumber,
            Description = asset.Description,
            Status = asset.Status,
            Warehouse = asset.Warehouse,
            WarehouseLocation = asset.WarehouseLocation,
            Facility = asset.Facility,
            Division = asset.Division,
            AgreementNumber = asset.AgreementNumber,
            CustomerName = asset.CustomerName,
            DeliveryDate = asset.DeliveryDate,
            AgreementLineValidFromDate = asset.AgreementLineValidFromDate,
            AgreementLineValidToDate = asset.AgreementLineValidToDate,
            TerminationDate = asset.TerminationDate,
            CollectionDate = asset.CollectionDate,
            EstimatedReadyDate = asset.EstimatedReadyDate,
            TelemetryStatus = asset.TelemetryStatus,
            ManufacturerName = asset.ManufacturerName,
            ProductGroup = asset.ProductGroup,
            ProductCategory = asset.ProductCategory,
            RunHours = asset.RunHours,
            ServiceCenter = asset.ServiceCenter,
        };
    }
}

internal sealed class AssetScheduleSummary
{
    public DateTime? NextAvailableDate { get; init; }
    public required string AvailabilityStatus { get; init; }
    public DateTime? NextCommitmentDate { get; init; }
    public string? NextCommitmentLabel { get; init; }
    public int ConflictCount { get; init; }
}

internal sealed class AssetScheduleEvent
{
    private readonly List<string> _conflicts = [];

    public required string Id { get; init; }
    public required string Type { get; init; }
    public required string Status { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public required string Title { get; init; }
    public int? AgreementId { get; init; }
    public string? AgreementNumber { get; init; }
    public string? CustomerName { get; init; }
    public string? Warehouse { get; init; }
    public required string Source { get; init; }
    public bool HasConflict => _conflicts.Count > 0;
    public string? ConflictReason => _conflicts.Count == 0 ? null : string.Join("; ", _conflicts);

    public void AddConflict(string reason)
    {
        _conflicts.Add(reason);
    }
}
