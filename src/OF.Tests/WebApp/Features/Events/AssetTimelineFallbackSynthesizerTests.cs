using FluentAssertions;
using OF.UI.Models;
using OF.WebApp.Features.Events;

namespace OF.Tests.WebApp.Features.Events;

public class AssetTimelineFallbackSynthesizerTests
{
    private static readonly DateTime Today = new(2026, 1, 15);
    private static readonly DateTime RangeStart = new(2026, 1, 1);
    private static readonly DateTime RangeEnd = new(2026, 3, 31);

    [Theory]
    [InlineData("SERVICE")]
    [InlineData(" assess ")]
    [InlineData("inservice")]
    public void Synthesize_MapsServiceStatusesToTheSameCurrentEvent(string status)
    {
        var source = CreateSource("ASSET-1", status) with
        {
            IonlastModified = new DateTime(2026, 1, 10, 14, 30, 0),
            EstimatedReadyDate = new DateTime(2026, 1, 20, 18, 0, 0),
        };

        var item = GetSingleEvent(source);

        AssertEvent(item, "ASSET-1", "SERVICE", "SERVICE", "service_event", new DateTime(2026, 1, 10), new DateTime(2026, 1, 20));
    }

    [Theory]
    [InlineData(" Customer Ltd ", "AG-1 (Customer Ltd)")]
    [InlineData(" ", "AG-1")]
    [InlineData(null, "AG-1")]
    public void Synthesize_TrimsOnHireAgreementLabelAndFloorsPastEndAtToday(
        string? customerName,
        string expectedTitle)
    {
        var source = CreateSource("ASSET-1", " onhire ") with
        {
            AgreementNumber = " AG-1 ",
            CustomerName = customerName,
            DeliveryDate = new DateTime(2026, 1, 2, 10, 30, 0),
            TerminationDate = new DateTime(2026, 1, 10, 20, 0, 0),
            AgreementLineValidToDate = new DateTime(2026, 2, 20),
        };

        var item = GetSingleEvent(source);

        AssertEvent(item, "ASSET-1", "ONHIRE", expectedTitle, "onhire_event", new DateTime(2026, 1, 2), Today);
    }

    [Fact]
    public void Synthesize_UsesOnHireDatePrecedenceAndDefaultsInInputOrder()
    {
        var sources = new[]
        {
            CreateSource("TERMINATION", "ONHIRE") with
            {
                AgreementNumber = "AG-1",
                DeliveryDate = new DateTime(2026, 1, 2),
                TerminationDate = new DateTime(2026, 2, 1),
                AgreementLineValidToDate = new DateTime(2026, 2, 10),
            },
            CreateSource("VALID-TO", "ONHIRE") with
            {
                AgreementNumber = "AG-2",
                AgreementLineValidToDate = new DateTime(2026, 2, 10),
            },
            CreateSource("TODAY", "ONHIRE") with
            {
                AgreementNumber = "AG-3",
            },
        };

        var result = Synthesize(sources);

        result.Select(item => item.AssetId).Should().Equal("TERMINATION", "VALID-TO", "TODAY");
        result.Select(item => item.StartDate).Should().Equal(new DateTime(2026, 1, 2), RangeStart, RangeStart);
        result.Select(item => item.EndDate).Should().Equal(new DateTime(2026, 2, 1), new DateTime(2026, 2, 10), Today);
    }

    [Fact]
    public void Synthesize_MapsOnHireWithoutAgreementToOnHoldAcrossTheRequestedRange()
    {
        var source = CreateSource("ASSET-1", "ONHIRE") with
        {
            AgreementNumber = "  ",
            DeliveryDate = new DateTime(2026, 2, 1),
            TerminationDate = new DateTime(2026, 2, 2),
        };

        var item = GetSingleEvent(source);

        AssertEvent(item, "ASSET-1", "ONHOLD", "ON HOLD", "onhold_event", RangeStart, RangeEnd);
    }

    [Fact]
    public void Synthesize_DefaultsServiceDatesFromTodayWhenRecordedDatesAreNotUsable()
    {
        var source = CreateSource("ASSET-1", "SERVICE") with
        {
            EstimatedReadyDate = Today.AddDays(-1),
        };

        var item = GetSingleEvent(source);

        AssertEvent(item, "ASSET-1", "SERVICE", "SERVICE", "service_event", Today, Today.AddDays(3));
    }

    [Fact]
    public void Synthesize_MapsTransportDatesFromLastModifiedOrToday()
    {
        var sources = new[]
        {
            CreateSource("RECORDED", "INTRANSIT") with
            {
                IonlastModified = new DateTime(2026, 1, 8, 13, 0, 0),
            },
            CreateSource("DEFAULT", "INTRANSIT"),
        };

        var result = Synthesize(sources);

        AssertEvent(result[0], "RECORDED", "TRANSPORT", "TRANSPORT", "transport_event", new DateTime(2026, 1, 8), new DateTime(2026, 1, 11));
        AssertEvent(result[1], "DEFAULT", "TRANSPORT", "TRANSPORT", "transport_event", Today, Today.AddDays(3));
    }

    [Fact]
    public void Synthesize_UsesCollectionDatePrecedenceLabelsAndStrictFutureEnd()
    {
        var sources = new[]
        {
            CreateSource("DELIVERY", "COLLECTION") with
            {
                AgreementNumber = " AG-1 ",
                CustomerName = " Customer Ltd ",
                DeliveryDate = new DateTime(2026, 1, 3, 12, 0, 0),
                AgreementLineValidFromDate = new DateTime(2026, 1, 2),
                CollectionDate = new DateTime(2026, 1, 25, 18, 0, 0),
            },
            CreateSource("VALID-FROM", "COLLECTION") with
            {
                AgreementLineValidFromDate = new DateTime(2026, 1, 4, 11, 0, 0),
                CollectionDate = Today,
            },
            CreateSource("RANGE", "COLLECTION"),
        };

        var result = Synthesize(sources);

        AssertEvent(result[0], "DELIVERY", "COLLECTION", "AG-1 (Customer Ltd)", "collection_event", new DateTime(2026, 1, 3), new DateTime(2026, 1, 25));
        AssertEvent(result[1], "VALID-FROM", "COLLECTION", "COLLECTION", "collection_event", new DateTime(2026, 1, 4), Today.AddDays(3));
        AssertEvent(result[2], "RANGE", "COLLECTION", "COLLECTION", "collection_event", RangeStart, Today.AddDays(3));
    }

    [Fact]
    public void Synthesize_UsesRepairReadyDateOrRangeEnd()
    {
        var sources = new[]
        {
            CreateSource("READY", "REPAIR") with
            {
                IonlastModified = new DateTime(2026, 1, 5, 10, 0, 0),
                EstimatedReadyDate = new DateTime(2026, 1, 12, 11, 0, 0),
            },
            CreateSource("DEFAULT", "REPAIR") with
            {
                EstimatedReadyDate = Today.AddDays(-1),
            },
        };

        var result = Synthesize(sources);

        AssertEvent(result[0], "READY", "REPAIR", "MAJOR REPAIR", "repair_event", new DateTime(2026, 1, 5), new DateTime(2026, 1, 12));
        AssertEvent(result[1], "DEFAULT", "REPAIR", "MAJOR REPAIR", "repair_event", Today, RangeEnd);
    }

    [Fact]
    public void Synthesize_ClampsAnEndBeforeStartWithoutClippingTheEventToTheRange()
    {
        var source = CreateSource("ASSET-1", "SERVICE") with
        {
            IonlastModified = new DateTime(2026, 2, 10, 12, 0, 0),
        };

        var item = GetSingleEvent(source);

        AssertEvent(item, "ASSET-1", "SERVICE", "SERVICE", "service_event", new DateTime(2026, 2, 10), new DateTime(2026, 2, 10));
    }

    [Fact]
    public void Synthesize_KeepsInclusiveRangeBoundariesAndSourceOrderButDropsOutsideAndBlankAssets()
    {
        var sources = new[]
        {
            CreateSource(null, "INTRANSIT"),
            CreateSource(" ", "INTRANSIT"),
            CreateSource("END-BOUNDARY", "INTRANSIT") with { IonlastModified = RangeEnd },
            CreateSource("AFTER", "INTRANSIT") with { IonlastModified = RangeEnd.AddDays(1) },
            CreateSource("START-BOUNDARY", "REPAIR") with
            {
                IonlastModified = RangeStart.AddDays(-10),
                EstimatedReadyDate = RangeStart,
            },
            CreateSource("BEFORE", "INTRANSIT") with { IonlastModified = RangeStart.AddDays(-10) },
        };

        var result = Synthesize(sources);

        result.Select(item => item.AssetId).Should().Equal("END-BOUNDARY", "START-BOUNDARY");
        result[0].StartDate.Should().Be(RangeEnd);
        result[0].EndDate.Should().Be(RangeEnd.AddDays(3));
        result[1].StartDate.Should().Be(RangeStart.AddDays(-10));
        result[1].EndDate.Should().Be(RangeStart);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("AVAILABLE")]
    [InlineData("RESERVED")]
    [InlineData("RINGFENCE")]
    public void Synthesize_DoesNotInventEventsForUnsupportedOrBlankStatuses(string? status)
    {
        Synthesize([CreateSource("ASSET-1", status)]).Should().BeEmpty();
    }

    private static IList<Event> Synthesize(IEnumerable<AssetTimelineFallbackSource> sources) =>
        AssetTimelineFallbackSynthesizer.Synthesize(sources, Today, RangeStart, RangeEnd);

    private static Event GetSingleEvent(AssetTimelineFallbackSource source) =>
        Synthesize([source]).Should().ContainSingle().Subject;

    private static AssetTimelineFallbackSource CreateSource(string? id, string? status) => new()
    {
        Id = id,
        Status = status,
    };

    private static void AssertEvent(
        Event item,
        string assetId,
        string eventType,
        string title,
        string cssClass,
        DateTime startDate,
        DateTime endDate)
    {
        item.AssetId.Should().Be(assetId);
        item.EventType.Should().Be(eventType);
        item.Title.Should().Be(title);
        item.CssClass.Should().Be(cssClass);
        item.StartDate.Should().Be(startDate);
        item.EndDate.Should().Be(endDate);
    }
}
