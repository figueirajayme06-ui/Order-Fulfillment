using OF.UI.Models;

namespace OF.WebApp.Features.Events;

public static class AssetTimelineFallbackSynthesizer
{
    public static IList<Event> Synthesize(
        IEnumerable<AssetTimelineFallbackSource> assets,
        DateTime today,
        DateTime rangeStart,
        DateTime rangeEnd)
    {
        var events = new List<Event>();

        foreach (var asset in assets)
        {
            var assetId = asset.Id;
            if (string.IsNullOrWhiteSpace(assetId))
            {
                continue;
            }

            var status = (asset.Status ?? string.Empty).Trim().ToUpperInvariant();

            switch (status)
            {
                case "ONHIRE":
                {
                    if (!string.IsNullOrWhiteSpace(asset.AgreementNumber))
                    {
                        var start = (asset.DeliveryDate ?? rangeStart).Date;
                        var end = (asset.TerminationDate ?? asset.AgreementLineValidToDate ?? today).Date;
                        if (end < today)
                        {
                            end = today;
                        }

                        TryAddEvent(
                            events,
                            assetId,
                            "ONHIRE",
                            BuildAgreementLabel(asset.AgreementNumber, asset.CustomerName),
                            "onhire_event",
                            start,
                            end,
                            rangeStart,
                            rangeEnd);
                    }
                    else
                    {
                        TryAddEvent(
                            events,
                            assetId,
                            "ONHOLD",
                            "ON HOLD",
                            "onhold_event",
                            rangeStart,
                            rangeEnd,
                            rangeStart,
                            rangeEnd);
                    }

                    break;
                }
                case "SERVICE":
                case "ASSESS":
                case "INSERVICE":
                {
                    var start = (asset.IonlastModified ?? today).Date;
                    var end = asset.EstimatedReadyDate.HasValue && asset.EstimatedReadyDate.Value.Date >= start
                        ? asset.EstimatedReadyDate.Value.Date
                        : today.AddDays(3);

                    TryAddEvent(events, assetId, "SERVICE", "SERVICE", "service_event", start, end, rangeStart, rangeEnd);
                    break;
                }
                case "INTRANSIT":
                {
                    var start = (asset.IonlastModified ?? today).Date;
                    var end = start.AddDays(3);

                    TryAddEvent(events, assetId, "TRANSPORT", "TRANSPORT", "transport_event", start, end, rangeStart, rangeEnd);
                    break;
                }
                case "COLLECTION":
                {
                    var start = (asset.DeliveryDate ?? asset.AgreementLineValidFromDate ?? rangeStart).Date;
                    var end = asset.CollectionDate.HasValue && asset.CollectionDate.Value.Date > today
                        ? asset.CollectionDate.Value.Date
                        : today.AddDays(3);

                    var title = string.IsNullOrWhiteSpace(asset.AgreementNumber)
                        ? "COLLECTION"
                        : BuildAgreementLabel(asset.AgreementNumber, asset.CustomerName);

                    TryAddEvent(events, assetId, "COLLECTION", title, "collection_event", start, end, rangeStart, rangeEnd);
                    break;
                }
                case "REPAIR":
                {
                    var start = (asset.IonlastModified ?? today).Date;
                    var end = asset.EstimatedReadyDate.HasValue && asset.EstimatedReadyDate.Value.Date >= start
                        ? asset.EstimatedReadyDate.Value.Date
                        : rangeEnd;

                    TryAddEvent(events, assetId, "REPAIR", "MAJOR REPAIR", "repair_event", start, end, rangeStart, rangeEnd);
                    break;
                }
            }
        }

        return events;
    }

    private static string BuildAgreementLabel(string? agreementNumber, string? customerName)
    {
        var agreement = (agreementNumber ?? string.Empty).Trim();
        var customer = (customerName ?? string.Empty).Trim();

        return string.IsNullOrEmpty(customer)
            ? agreement
            : $"{agreement} ({customer})";
    }

    private static void TryAddEvent(
        ICollection<Event> target,
        string assetId,
        string eventType,
        string title,
        string cssClass,
        DateTime start,
        DateTime end,
        DateTime rangeStart,
        DateTime rangeEnd)
    {
        if (end < start)
        {
            end = start;
        }

        if (start > rangeEnd || end < rangeStart)
        {
            return;
        }

        target.Add(new Event
        {
            AssetId = assetId,
            EventType = eventType,
            Title = title,
            CssClass = cssClass,
            StartDate = start,
            EndDate = end,
        });
    }
}

public sealed record AssetTimelineFallbackSource
{
    public string? Id { get; init; }
    public string? Status { get; init; }
    public string? AgreementNumber { get; init; }
    public string? CustomerName { get; init; }
    public DateTime? DeliveryDate { get; init; }
    public DateTime? TerminationDate { get; init; }
    public DateTime? AgreementLineValidFromDate { get; init; }
    public DateTime? AgreementLineValidToDate { get; init; }
    public DateTime? EstimatedReadyDate { get; init; }
    public DateTime? IonlastModified { get; init; }
    public DateTime? CollectionDate { get; init; }
}
