using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using OF.Data.Database;
using OF.UI.Models;
using OF.WebApp.Controllers;

namespace OF.Tests.WebApp.Features.Assets;

public class AssetProfileScheduleBuilderTests
{
    private static readonly DateTime FromDate = new(2026, 1, 1);
    private static readonly DateTime ToDate = new(2026, 2, 28);
    private static readonly DateTime Today = new(2026, 1, 15);
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private static readonly MethodInfo BuildMethod = typeof(AssetsController).Assembly
        .GetType("OF.WebApp.Features.Assets.AssetProfileScheduleBuilder", throwOnError: true)!
        .GetMethod("Build", BindingFlags.Public | BindingFlags.Static)
        ?? throw new InvalidOperationException("AssetProfileScheduleBuilder.Build was not found.");

    [Fact]
    public void Build_SynthesizesOnHireFromThePreferredDatesAndAgreementContext()
    {
        var asset = CreateAsset(" on hire ");
        asset.DeliveryDate = new DateTime(2026, 1, 3, 14, 30, 0);
        asset.AgreementLineValidFromDate = new DateTime(2026, 1, 2, 9, 0, 0);
        asset.TerminationDate = new DateTime(2026, 1, 20, 18, 0, 0);
        asset.AgreementLineValidToDate = new DateTime(2026, 1, 25, 10, 0, 0);
        asset.AgreementNumber = "AG-42";
        asset.CustomerName = "Customer Ltd";

        var profile = Build(asset);

        profile.Events.Should().ContainSingle().Which.Should().BeEquivalentTo(new EventSnapshot
        {
            Id = "current-on-hire",
            Type = "On hire",
            Status = "Current",
            StartDate = new DateTime(2026, 1, 3),
            EndDate = new DateTime(2026, 1, 20),
            Title = "AG-42",
            AgreementNumber = "AG-42",
            CustomerName = "Customer Ltd",
            Warehouse = "ED1",
            Source = "Asset status",
        });
        profile.Summary.Should().BeEquivalentTo(new SummarySnapshot
        {
            NextAvailableDate = new DateTime(2026, 1, 21),
            AvailabilityStatus = "Committed",
        });
    }

    [Fact]
    public void Build_SynthesizesOnHoldAndFallsBackToAgreementThenRangeDates()
    {
        var agreementDates = CreateAsset("ONHIRE");
        agreementDates.AgreementNumber = "   ";
        agreementDates.AgreementLineValidFromDate = new DateTime(2026, 1, 5, 16, 0, 0);
        agreementDates.AgreementLineValidToDate = new DateTime(2026, 2, 10, 12, 0, 0);

        var agreementProfile = Build(agreementDates);

        agreementProfile.Events.Should().ContainSingle().Which.Should().BeEquivalentTo(new EventSnapshot
        {
            Id = "current-on-hire",
            Type = "On hold",
            Status = "Current",
            StartDate = new DateTime(2026, 1, 5),
            EndDate = new DateTime(2026, 2, 10),
            Title = "On hold",
            AgreementNumber = "   ",
            Warehouse = "ED1",
            Source = "Asset status",
        });

        var rangeDates = CreateAsset("ONHIRE");
        rangeDates.AgreementNumber = "AG-DEFAULT";

        var rangeProfile = Build(rangeDates);

        rangeProfile.Events.Should().ContainSingle().Which.Should().BeEquivalentTo(new EventSnapshot
        {
            Id = "current-on-hire",
            Type = "On hire",
            Status = "Current",
            StartDate = FromDate,
            Title = "AG-DEFAULT",
            AgreementNumber = "AG-DEFAULT",
            Warehouse = "ED1",
            Source = "Asset status",
        });
    }

    [Theory]
    [InlineData("SERVICE")]
    [InlineData(" assess ")]
    [InlineData("in service")]
    public void Build_NormalizesServiceAliasesToTheSameCurrentEvent(string status)
    {
        var asset = CreateAsset(status);
        asset.IonlastModified = new DateTime(2026, 1, 10, 13, 0, 0);
        asset.EstimatedReadyDate = new DateTime(2026, 1, 20, 19, 0, 0);

        var profile = Build(asset);

        profile.Events.Should().ContainSingle().Which.Should().BeEquivalentTo(new EventSnapshot
        {
            Id = "current-service",
            Type = "Service / assessment",
            Status = "Current",
            StartDate = new DateTime(2026, 1, 10),
            EndDate = new DateTime(2026, 1, 20),
            Title = "Service in progress",
            Warehouse = "ED1",
            Source = "Asset status",
        });
        profile.Summary.Should().BeEquivalentTo(new SummarySnapshot
        {
            NextAvailableDate = new DateTime(2026, 1, 21),
            AvailabilityStatus = "Committed",
        });
    }

    [Fact]
    public void Build_UsesRecordedAndFallbackDatesForRepairCollectionAndTransit()
    {
        var repair = CreateAsset("REPAIR");
        repair.IonlastModified = new DateTime(2026, 1, 8, 11, 0, 0);
        repair.EstimatedReadyDate = new DateTime(2026, 1, 18, 17, 0, 0);

        var repairEvent = Build(repair).Events.Should().ContainSingle().Which;

        repairEvent.Should().BeEquivalentTo(new EventSnapshot
        {
            Id = "current-repair",
            Type = "Repair",
            Status = "Current",
            StartDate = new DateTime(2026, 1, 8),
            EndDate = new DateTime(2026, 1, 18),
            Title = "Repair in progress",
            Warehouse = "ED1",
            Source = "Asset status",
        });

        var collection = CreateAsset("COLLECTION");
        collection.DeliveryDate = new DateTime(2026, 1, 4, 14, 0, 0);
        collection.AgreementLineValidFromDate = new DateTime(2026, 1, 2);
        collection.CollectionDate = new DateTime(2026, 1, 25, 18, 0, 0);
        collection.AgreementNumber = null;
        collection.CustomerName = "Customer Ltd";

        var collectionEvent = Build(collection).Events.Should().ContainSingle().Which;

        collectionEvent.Should().BeEquivalentTo(new EventSnapshot
        {
            Id = "current-collection",
            Type = "Collection",
            Status = "Current",
            StartDate = new DateTime(2026, 1, 4),
            EndDate = new DateTime(2026, 1, 25),
            Title = "Collection in progress",
            CustomerName = "Customer Ltd",
            Warehouse = "ED1",
            Source = "Asset status",
        });

        collection.DeliveryDate = null;
        var agreementDateEvent = Build(collection).Events.Should().ContainSingle().Which;
        agreementDateEvent.StartDate.Should().Be(new DateTime(2026, 1, 2));

        collection.AgreementLineValidFromDate = null;
        var todayEvent = Build(collection).Events.Should().ContainSingle().Which;
        todayEvent.StartDate.Should().Be(Today);

        var transit = CreateAsset("in transit");

        var defaultTransitEvent = Build(transit).Events.Should().ContainSingle().Which;

        defaultTransitEvent.Should().BeEquivalentTo(new EventSnapshot
        {
            Id = "current-transit",
            Type = "In transit",
            Status = "Current",
            StartDate = Today,
            EndDate = Today.AddDays(3),
            Title = "Transport in progress",
            Warehouse = "ED1",
            Source = "Asset status",
        });

        transit.IonlastModified = new DateTime(2026, 1, 9, 15, 0, 0);
        var recordedTransitEvent = Build(transit).Events.Should().ContainSingle().Which;
        recordedTransitEvent.StartDate.Should().Be(new DateTime(2026, 1, 9));
        recordedTransitEvent.EndDate.Should().Be(new DateTime(2026, 1, 12));
    }

    [Fact]
    public void Build_MergesFiltersAndOrdersReservationsAndRingfencesWithoutClippingDates()
    {
        var reservations = new[]
        {
            CreateReservation(1, new DateTime(2025, 12, 20, 12, 0, 0), new DateTime(2026, 1, 1, 18, 0, 0), true),
            CreateReservation(2, new DateTime(2026, 1, 10, 16, 0, 0), new DateTime(2026, 1, 15, 17, 0, 0)),
            CreateReservation(4, new DateTime(2026, 1, 20), new DateTime(2026, 1, 22)),
            CreateReservation(6, new DateTime(2025, 12, 1), new DateTime(2025, 12, 31)),
        };
        var ringfences = new[]
        {
            CreateRingfence(3, new DateTime(2026, 1, 10, 16, 0, 0), new DateTime(2026, 1, 15, 17, 0, 0), "Priority fleet"),
            CreateRingfence(5, new DateTime(2026, 2, 28, 23, 0, 0), new DateTime(2026, 3, 4), "Boundary fence"),
            CreateRingfence(7, new DateTime(2026, 3, 1), new DateTime(2026, 3, 2), "Outside fence"),
        };

        var asset = CreateAsset("INTRANSIT");
        asset.IonlastModified = new DateTime(2026, 1, 5, 12, 0, 0);

        var profile = Build(asset, reservations, ringfences);

        profile.Events.Select(item => item.Id).Should().Equal(
            "reservation-1",
            "current-transit",
            "reservation-2",
            "ringfence-3",
            "reservation-4",
            "ringfence-5");
        profile.Events[0].StartDate.Should().Be(new DateTime(2025, 12, 20));
        profile.Events[0].EndDate.Should().Be(FromDate);
        profile.Events[0].Status.Should().Be("Confirmed");
        profile.Events[5].StartDate.Should().Be(ToDate);
        profile.Events[5].EndDate.Should().Be(new DateTime(2026, 3, 4));
        profile.Events[2].Should().BeEquivalentTo(new EventSnapshot
        {
            Id = "reservation-2",
            Type = "Reservation",
            Status = "Planned",
            StartDate = new DateTime(2026, 1, 10),
            EndDate = Today,
            Title = "AG-2",
            AgreementId = 102,
            AgreementNumber = "AG-2",
            CustomerName = "Customer 2",
            Warehouse = "WH-2",
            Source = "Reservation",
            HasConflict = true,
            ConflictReason = "Overlaps ringfence: Priority fleet",
        });
        profile.Events[3].Should().BeEquivalentTo(new EventSnapshot
        {
            Id = "ringfence-3",
            Type = "Ringfence",
            Status = "Protected",
            StartDate = new DateTime(2026, 1, 10),
            EndDate = Today,
            Title = "Priority fleet",
            Warehouse = "RF-WH-3",
            Source = "Ringfence",
            HasConflict = true,
            ConflictReason = "Overlaps reservation: AG-2",
        });
    }

    [Fact]
    public void Build_MarksInclusiveOverlapsAndCountsConflictingEventsRatherThanPairs()
    {
        var reservations = new[]
        {
            CreateReservation(1, new DateTime(2026, 1, 10), Today, agreementNumber: "First"),
            CreateReservation(2, Today, new DateTime(2026, 1, 20), agreementNumber: "Second"),
        };
        var ringfences = new[]
        {
            CreateRingfence(3, new DateTime(2026, 1, 14), new DateTime(2026, 1, 16), "Fence"),
        };

        var profile = Build(CreateAsset(), reservations, ringfences);

        profile.Events.Select(item => item.Id).Should().Equal("reservation-1", "ringfence-3", "reservation-2");
        profile.Events[0].ConflictReason.Should().Be(
            "Overlaps ringfence: Fence; Overlaps reservation: Second");
        profile.Events[1].ConflictReason.Should().Be(
            "Overlaps reservation: First; Overlaps reservation: Second");
        profile.Events[2].ConflictReason.Should().Be(
            "Overlaps reservation: First; Overlaps ringfence: Fence");
        profile.Events.Should().OnlyContain(item => item.HasConflict);
        profile.Summary.Should().BeEquivalentTo(new SummarySnapshot
        {
            NextAvailableDate = new DateTime(2026, 1, 21),
            AvailabilityStatus = "Committed",
            NextCommitmentDate = Today,
            NextCommitmentLabel = "Reservation: Second",
            ConflictCount = 3,
        });
    }

    [Fact]
    public void Build_DoesNotConflictOpenEndedStatusEventsAndPrioritizesMissingReadiness()
    {
        var asset = CreateAsset(" repair ");
        asset.IonlastModified = new DateTime(2025, 12, 1, 14, 0, 0);
        var reservation = CreateReservation(
            1,
            new DateTime(2026, 1, 10),
            new DateTime(2026, 1, 20),
            omitAgreementNumber: true);

        var profile = Build(asset, [reservation]);

        profile.Events.Select(item => item.Id).Should().Equal("current-repair", "reservation-1");
        profile.Events[0].EndDate.Should().BeNull();
        profile.Events[0].Title.Should().Be("Ready date not recorded");
        profile.Events[1].Title.Should().Be("Agreement not recorded");
        profile.Events.Should().OnlyContain(item => !item.HasConflict && item.ConflictReason == null);
        profile.Summary.Should().BeEquivalentTo(new SummarySnapshot
        {
            AvailabilityStatus = "Ready date required",
        });
    }

    [Fact]
    public void Build_AppliesOutOfFleetAndAvailableSummaryRulesWithoutDroppingFutureCommitments()
    {
        var futureReservation = CreateReservation(
            1,
            new DateTime(2026, 2, 1),
            new DateTime(2026, 2, 4),
            agreementNumber: "FUTURE");

        var outOfFleet = Build(CreateAsset(" removed stock "), [futureReservation]);

        outOfFleet.Summary.Should().BeEquivalentTo(new SummarySnapshot
        {
            AvailabilityStatus = "Out of fleet",
            NextCommitmentDate = new DateTime(2026, 2, 1),
            NextCommitmentLabel = "Reservation: FUTURE",
        });

        var availableBeforeCommitment = Build(CreateAsset(), [futureReservation]);

        availableBeforeCommitment.Summary.Should().BeEquivalentTo(new SummarySnapshot
        {
            NextAvailableDate = Today,
            AvailabilityStatus = "Available before next commitment",
            NextCommitmentDate = new DateTime(2026, 2, 1),
            NextCommitmentLabel = "Reservation: FUTURE",
        });

        var available = Build(CreateAsset());

        available.Summary.Should().BeEquivalentTo(new SummarySnapshot
        {
            NextAvailableDate = Today,
            AvailabilityStatus = "Available",
        });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("reserved")]
    [InlineData("unknown status")]
    public void Build_DoesNotInventCurrentEventsForBlankOrUnsupportedStatuses(string? status)
    {
        var profile = Build(CreateAsset(status));

        profile.Events.Should().BeEmpty();
        profile.Summary.Should().BeEquivalentTo(new SummarySnapshot
        {
            AvailabilityStatus = "Review current status",
        });
    }

    [Fact]
    public void Build_PreservesNullDefaultsAndDoesNotMutateAnyInput()
    {
        var asset = new Asset
        {
            Id = "ASSET-FULL",
            IndividualItemNumber = "SERIAL-FULL",
            ItemNumber = "ITEM-1",
            Description = "Generator",
            Status = null,
            Warehouse = "ED1",
            WarehouseLocation = "Yard A",
            Facility = "UKN",
            Division = "UK",
            AgreementNumber = "AG-99",
            CustomerName = "Customer Ltd",
            DeliveryDate = new DateTime(2026, 1, 2, 14, 0, 0),
            AgreementLineValidFromDate = new DateTime(2026, 1, 1, 8, 0, 0),
            AgreementLineValidToDate = new DateTime(2026, 2, 1, 18, 0, 0),
            TerminationDate = new DateTime(2026, 1, 31, 17, 0, 0),
            CollectionDate = new DateTime(2026, 2, 2, 12, 0, 0),
            EstimatedReadyDate = new DateTime(2026, 1, 20, 10, 0, 0),
            TelemetryStatus = "Connected",
            ManufacturerName = "Aggreko",
            ProductGroup = "Power",
            ProductCategory = "Generator",
            RunHours = 123.5,
            ServiceCenter = "Edinburgh",
            IonlastModified = new DateTime(2026, 1, 14, 9, 0, 0),
        };
        var reservations = new[]
        {
            CreateReservation(2, new DateTime(2026, 1, 20), new DateTime(2026, 1, 25)),
            CreateReservation(1, new DateTime(2026, 1, 10), new DateTime(2026, 1, 15)),
        };
        var ringfences = new[]
        {
            CreateRingfence(1, new DateTime(2026, 1, 12), new DateTime(2026, 1, 18), "Protected"),
        };
        var before = JsonSerializer.Serialize(new { asset, reservations, ringfences }, WebJson);

        var profile = Build(asset, reservations, ringfences);

        JsonSerializer.Serialize(new { asset, reservations, ringfences }, WebJson).Should().Be(before);
        profile.Asset.Should().BeEquivalentTo(new AssetSnapshot
        {
            Id = "ASSET-FULL",
            IndividualItemNumber = "SERIAL-FULL",
            ItemNumber = "ITEM-1",
            Description = "Generator",
            Status = null,
            Warehouse = "ED1",
            WarehouseLocation = "Yard A",
            Facility = "UKN",
            Division = "UK",
            AgreementNumber = "AG-99",
            CustomerName = "Customer Ltd",
            DeliveryDate = new DateTime(2026, 1, 2, 14, 0, 0),
            AgreementLineValidFromDate = new DateTime(2026, 1, 1, 8, 0, 0),
            AgreementLineValidToDate = new DateTime(2026, 2, 1, 18, 0, 0),
            TerminationDate = new DateTime(2026, 1, 31, 17, 0, 0),
            CollectionDate = new DateTime(2026, 2, 2, 12, 0, 0),
            EstimatedReadyDate = new DateTime(2026, 1, 20, 10, 0, 0),
            TelemetryStatus = "Connected",
            ManufacturerName = "Aggreko",
            ProductGroup = "Power",
            ProductCategory = "Generator",
            RunHours = 123.5,
            ServiceCenter = "Edinburgh",
        });

        var minimal = Build(new Asset
        {
            Id = "ASSET-MIN",
            IndividualItemNumber = "SERIAL-MIN",
        });

        minimal.Asset.Should().BeEquivalentTo(new AssetSnapshot
        {
            Id = "ASSET-MIN",
            IndividualItemNumber = "SERIAL-MIN",
        });
        minimal.Events.Should().BeEmpty();
        minimal.Summary.Should().BeEquivalentTo(new SummarySnapshot
        {
            AvailabilityStatus = "Review current status",
        });
    }

    private static ProfileSnapshot Build(
        Asset asset,
        IEnumerable<AssetScheduleReservation>? reservations = null,
        IEnumerable<Ringfence>? ringfences = null)
    {
        var response = BuildMethod.Invoke(null, new object?[]
        {
            asset,
            reservations ?? Array.Empty<AssetScheduleReservation>(),
            ringfences ?? Array.Empty<Ringfence>(),
            FromDate,
            ToDate,
            Today,
        }) ?? throw new InvalidOperationException("Asset profile builder returned null.");

        var json = JsonSerializer.Serialize(response, response.GetType(), WebJson);
        return JsonSerializer.Deserialize<ProfileSnapshot>(json, WebJson)
            ?? throw new InvalidOperationException("Asset profile response could not be read.");
    }

    private static Asset CreateAsset(string? status = "AVAILABLE") => new()
    {
        Id = "ASSET-1",
        IndividualItemNumber = "SERIAL-1",
        Status = status,
        Warehouse = "ED1",
    };

    private static AssetScheduleReservation CreateReservation(
        int id,
        DateTime startDate,
        DateTime endDate,
        bool isConfirmed = false,
        string? agreementNumber = null,
        bool omitAgreementNumber = false)
    {
        return new AssetScheduleReservation
        {
            ReservationId = id,
            LineId = id + 10,
            HeaderId = id + 100,
            AgreementNumber = omitAgreementNumber ? null : agreementNumber ?? $"AG-{id}",
            CustomerName = $"Customer {id}",
            Warehouse = $"WH-{id}",
            StartDate = startDate,
            EndDate = endDate,
            IsConfirmed = isConfirmed,
        };
    }

    private static Ringfence CreateRingfence(
        int id,
        DateTime fromDate,
        DateTime toDate,
        string title)
    {
        return new Ringfence("planner@example.com")
        {
            Id = id,
            FromDate = fromDate,
            ToDate = toDate,
            Title = title,
            Divisions = "UK",
            Warehouse = $"RF-WH-{id}",
        };
    }

    private sealed class ProfileSnapshot
    {
        public ProfileSnapshot()
        {
        }

        public AssetSnapshot Asset { get; init; } = new();
        public SummarySnapshot Summary { get; init; } = new();
        public List<EventSnapshot> Events { get; init; } = [];
    }

    private sealed class AssetSnapshot
    {
        public AssetSnapshot()
        {
        }

        public string Id { get; init; } = string.Empty;
        public string IndividualItemNumber { get; init; } = string.Empty;
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
    }

    private sealed class SummarySnapshot
    {
        public SummarySnapshot()
        {
        }

        public DateTime? NextAvailableDate { get; init; }
        public string AvailabilityStatus { get; init; } = string.Empty;
        public DateTime? NextCommitmentDate { get; init; }
        public string? NextCommitmentLabel { get; init; }
        public int ConflictCount { get; init; }
    }

    private sealed class EventSnapshot
    {
        public EventSnapshot()
        {
        }

        public string Id { get; init; } = string.Empty;
        public string Type { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public DateTime StartDate { get; init; }
        public DateTime? EndDate { get; init; }
        public string Title { get; init; } = string.Empty;
        public int? AgreementId { get; init; }
        public string? AgreementNumber { get; init; }
        public string? CustomerName { get; init; }
        public string? Warehouse { get; init; }
        public string Source { get; init; } = string.Empty;
        public bool HasConflict { get; init; }
        public string? ConflictReason { get; init; }
    }
}
