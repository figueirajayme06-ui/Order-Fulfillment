using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Time.Testing;
using Moq;
using OF.Data;
using OF.Data.Database;
using OF.Tests.Common;
using OF.UI.Database;

namespace OF.Tests.UI.Database;

[Collection("DatabaseCollection")]
public class FulfilmentAvailabilitySummaryIntegrationTests : CommonDBTest
{
    private const string GenericCode = "GEN-QTY";
    private const string ItemNumber = "ITEM-QTY";
    private const string WarehouseCode = "WH-QTY";

    public FulfilmentAvailabilitySummaryIntegrationTests(DatabaseFixture databaseFixture)
        : base(databaseFixture)
    {
    }

    [SkippableFact]
    public async Task GetFulfilmentAvailabilitySummary_QuantityStock_SubtractsPeakConcurrentPendingReservations()
    {
        var dbContextFactory = await ArrangeAndAct(SeedQuantityAvailabilityData);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var repository = new DataRepository(
            dbContext,
            new Mock<IMemoryCache>().Object,
            new FakeTimeProvider());

        var results = await repository.GetAvailabilitySummaryAsync(
            GenericCode,
            string.Empty,
            new DateTime(2026, 8, 1),
            new DateTime(2026, 8, 31),
            "DIV1",
            null);

        var result = Assert.Single(results);
        Assert.Equal(WarehouseCode, result.WarehouseCode);
        Assert.Equal(ItemNumber, result.ItemNumber);
        Assert.Equal(11, result.Available);
        Assert.Equal(20, result.Count);
        Assert.False(result.GenericOnly);
        Assert.Equal("quantity", result.ReservationMode);
    }

    [SkippableFact]
    public async Task GetFulfilmentAvailabilitySummary_SerializedAsset_UsesDeliveryDateAsReservationStart()
    {
        var dbContextFactory = await ArrangeAndAct(SeedSerializedAvailabilityData);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var repository = new DataRepository(
            dbContext,
            new Mock<IMemoryCache>().Object,
            new FakeTimeProvider());

        var results = await repository.GetAvailabilitySummaryAsync(
            GenericCode,
            string.Empty,
            new DateTime(2026, 8, 5),
            new DateTime(2026, 8, 7),
            "DIV1",
            null);

        var result = Assert.Single(results);
        Assert.Equal(1, result.Available);
        Assert.Equal(1, result.Count);
        Assert.Equal("asset", result.ReservationMode);
    }

    [SkippableFact]
    public async Task GetFulfilmentAvailabilitySummary_OnHireAsset_UsesKnownReleaseDateForRequestedPeriod()
    {
        var dbContextFactory = await ArrangeAndAct(SeedDateAwareSerializedAvailabilityData);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var repository = new DataRepository(
            dbContext,
            new Mock<IMemoryCache>().Object,
            new FakeTimeProvider());

        async Task<int> GetAvailable(DateTime date)
        {
            var results = await repository.GetAvailabilitySummaryAsync(
                GenericCode,
                string.Empty,
                date,
                date,
                "DIV1",
                null);

            return Assert.Single(results).Available;
        }

        dbContext.Reservations.Remove(await dbContext.Reservations.SingleAsync());
        await dbContext.SaveChangesAsync();

        Assert.Equal(0, await GetAvailable(new DateTime(2026, 2, 1)));
        Assert.Equal(1, await GetAvailable(new DateTime(2026, 2, 2)));

        var asset = await dbContext.Assets.SingleAsync(item => item.Id == "ASSET-1");
        asset.TerminationDate = new DateTime(2026, 2, 3);
        await dbContext.SaveChangesAsync();

        Assert.Equal(0, await GetAvailable(new DateTime(2026, 2, 3)));
        Assert.Equal(1, await GetAvailable(new DateTime(2026, 2, 4)));

        asset.CollectionDate = new DateTime(2026, 2, 5);
        await dbContext.SaveChangesAsync();

        Assert.Equal(0, await GetAvailable(new DateTime(2026, 2, 4)));
        Assert.Equal(1, await GetAvailable(new DateTime(2026, 2, 6)));

        asset.CollectionDate = null;
        asset.TerminationDate = null;
        asset.AgreementLineValidToDate = null;
        await dbContext.SaveChangesAsync();

        Assert.Equal(0, await GetAvailable(new DateTime(2026, 2, 6)));

        asset.AgreementLineValidToDate = new DateTime(2026, 2, 1);
        asset.Status = "Repair";
        await dbContext.SaveChangesAsync();

        Assert.Equal(0, await GetAvailable(new DateTime(2026, 2, 6)));
    }

    [SkippableFact]
    public async Task GetFulfilmentAvailabilitySummary_OnHireAsset_StillHonorsCommitmentsAndOnHoldState()
    {
        var dbContextFactory = await ArrangeAndAct(SeedDateAwareSerializedAvailabilityData);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var repository = new DataRepository(
            dbContext,
            new Mock<IMemoryCache>().Object,
            new FakeTimeProvider());

        async Task<int> GetAvailable(DateTime date)
        {
            var results = await repository.GetAvailabilitySummaryAsync(
                GenericCode,
                string.Empty,
                date,
                date,
                "DIV1",
                null);

            return Assert.Single(results).Available;
        }

        Assert.Equal(1, await GetAvailable(new DateTime(2026, 2, 2)));

        var header = await dbContext.Headers.SingleAsync();
        var futureLine = CreateLine(
            header.Id,
            "FUTURE-RESERVATION",
            new DateTime(2026, 2, 3),
            new DateTime(2026, 2, 4),
            1);
        dbContext.Lines.Add(futureLine);
        await dbContext.SaveChangesAsync();
        dbContext.Reservations.Add(new Reservation
        {
            AssetId = "ASSET-1",
            ItemNumber = ItemNumber,
            Warehouse = WarehouseCode,
            LineId = futureLine.Id,
            Quantity = 1,
            EffectiveQuantity = 1,
            IsConfirmed = false,
        });
        await dbContext.SaveChangesAsync();

        Assert.Equal(0, await GetAvailable(new DateTime(2026, 2, 3)));
        Assert.Equal(1, await GetAvailable(new DateTime(2026, 2, 5)));

        var ringfence = new Ringfence("availability-test")
        {
            Title = "Future Ringfence",
            FromDate = new DateTime(2026, 2, 6),
            ToDate = new DateTime(2026, 2, 7),
            Divisions = "DIV1",
        };
        dbContext.Ringfences.Add(ringfence);
        await dbContext.SaveChangesAsync();
        dbContext.RingfenceItems.Add(new RingfenceItem("availability-test")
        {
            RingfenceId = ringfence.Id,
            AssetId = "ASSET-1",
        });
        await dbContext.SaveChangesAsync();

        Assert.Equal(0, await GetAvailable(new DateTime(2026, 2, 6)));
        Assert.Equal(1, await GetAvailable(new DateTime(2026, 2, 8)));

        var asset = await dbContext.Assets.SingleAsync(item => item.Id == "ASSET-1");
        asset.AgreementNumber = " ";
        await dbContext.SaveChangesAsync();

        Assert.Equal(0, await GetAvailable(new DateTime(2026, 2, 8)));
    }

    private static Header SeedQuantityAvailabilityData(ApplicationDbContext dbContext)
    {
        var family = new CpqFamily { FamilyDescription = "Quantity" };
        dbContext.CpqFamilies.Add(family);
        dbContext.SaveChanges();

        var lineDefinition = new CpqLine
        {
            FamilyId = family.Id,
            LineDescription = "Quantity Line",
        };
        dbContext.CpqLines.Add(lineDefinition);
        dbContext.SaveChanges();

        var generic = new CpqGeneric
        {
            LineId = lineDefinition.Id,
            GenericCode = GenericCode,
            GenericDescription = "Quantity generic",
            Active = true,
            Deleted = false,
            Rehire = "No",
        };
        dbContext.CpqGenerics.Add(generic);
        dbContext.SaveChanges();

        dbContext.CpqItems.Add(new CpqItem
        {
            ItemNumber = ItemNumber,
            DescriptionNam = "Quantity item",
            DescriptionIntl = "Quantity item",
            GenericId = generic.Id,
            Active = true,
            Deleted = false,
        });

        var header = new Header
        {
            AgreementNumber = "T-QTY-SUMMARY",
            AgreementNumbersOnly = "QTY-SUMMARY",
            Division = "DIV1",
            Facility = "FAC1",
            OrderSource = "CPQ",
        };
        dbContext.Headers.Add(header);
        dbContext.SaveChanges();

        var firstPending = CreateLine(header.Id, "PENDING-1", new DateTime(2026, 8, 1), new DateTime(2026, 8, 10), 4);
        var overlappingPending = CreateLine(header.Id, "PENDING-2", new DateTime(2026, 8, 5), new DateTime(2026, 8, 7), 3);
        var nonOverlappingPending = CreateLine(header.Id, "PENDING-3", new DateTime(2026, 8, 15), new DateTime(2026, 8, 20), 6);
        var confirmed = CreateLine(header.Id, "CONFIRMED", new DateTime(2026, 8, 5), new DateTime(2026, 8, 7), 9);

        dbContext.Lines.AddRange(firstPending, overlappingPending, nonOverlappingPending, confirmed);
        dbContext.SaveChanges();

        dbContext.Reservations.AddRange(
            CreateReservation(firstPending.Id, 4, isConfirmed: false),
            CreateReservation(overlappingPending.Id, 3, isConfirmed: false),
            CreateReservation(nonOverlappingPending.Id, 6, isConfirmed: false),
            CreateReservation(confirmed.Id, 9, isConfirmed: true));

        dbContext.Database.ExecuteSqlRaw(
            """
            INSERT INTO WarehouseItems
                (WarehouseCode, Warehouse, DivisionCode, FacilityCode, Facility, CountryCode, Country)
            VALUES
                ('WH-QTY', 'Quantity Warehouse', 'DIV1', 'FAC1', 'Quantity Facility', 'GB', 'United Kingdom')
            """);

        dbContext.ProductItems.Add(new ProductItem
        {
            ItemNumber = ItemNumber,
            Warehouse = WarehouseCode,
            Division = "DIV1",
            Facility = "FAC1",
            Status = "Available",
            StockQuantity = 20,
            AllocatedQuantity = 2,
        });

        dbContext.SaveChanges();
        return header;
    }

    private static Header SeedSerializedAvailabilityData(ApplicationDbContext dbContext)
    {
        var family = new CpqFamily { FamilyDescription = "Serialized" };
        dbContext.CpqFamilies.Add(family);
        dbContext.SaveChanges();

        var lineDefinition = new CpqLine
        {
            FamilyId = family.Id,
            LineDescription = "Serialized Line",
        };
        dbContext.CpqLines.Add(lineDefinition);
        dbContext.SaveChanges();

        var generic = new CpqGeneric
        {
            LineId = lineDefinition.Id,
            GenericCode = GenericCode,
            GenericDescription = "Serialized generic",
            Active = true,
            Deleted = false,
            Rehire = "No",
        };
        dbContext.CpqGenerics.Add(generic);
        dbContext.SaveChanges();

        dbContext.CpqItems.Add(new CpqItem
        {
            ItemNumber = ItemNumber,
            DescriptionNam = "Serialized item",
            DescriptionIntl = "Serialized item",
            GenericId = generic.Id,
            Active = true,
            Deleted = false,
        });

        var header = new Header
        {
            AgreementNumber = "T-ASSET-SUMMARY",
            AgreementNumbersOnly = "ASSET-SUMMARY",
            Division = "DIV1",
            Facility = "FAC1",
            OrderSource = "CPQ",
        };
        dbContext.Headers.Add(header);
        dbContext.SaveChanges();

        var reservationLine = CreateLine(
            header.Id,
            "SERIALIZED-1",
            new DateTime(2026, 8, 1),
            new DateTime(2026, 8, 20),
            1);
        reservationLine.DeliveryDate = new DateTime(2026, 8, 10);
        dbContext.Lines.Add(reservationLine);
        dbContext.SaveChanges();

        dbContext.Database.ExecuteSqlRaw(
            """
            INSERT INTO WarehouseItems
                (WarehouseCode, Warehouse, DivisionCode, FacilityCode, Facility, CountryCode, Country)
            VALUES
                ('WH-QTY', 'Serialized Warehouse', 'DIV1', 'FAC1', 'Serialized Facility', 'GB', 'United Kingdom')
            """);

        const string assetId = "ASSET-1";
        dbContext.Assets.Add(new Asset
        {
            Id = assetId,
            IndividualItemNumber = assetId,
            ItemNumber = ItemNumber,
            Warehouse = WarehouseCode,
            Division = "DIV1",
            Facility = "FAC1",
            Status = "Available",
        });
        dbContext.Reservations.Add(new Reservation
        {
            AssetId = assetId,
            ItemNumber = ItemNumber,
            Warehouse = WarehouseCode,
            LineId = reservationLine.Id,
            Quantity = 1,
            EffectiveQuantity = 1,
            IsConfirmed = false,
        });

        dbContext.SaveChanges();
        return header;
    }

    private static Header SeedDateAwareSerializedAvailabilityData(ApplicationDbContext dbContext)
    {
        var header = SeedSerializedAvailabilityData(dbContext);
        var reservationLine = dbContext.Lines.Single();
        reservationLine.DeliveryDate = new DateTime(2026, 1, 1);
        reservationLine.ValidFromDate = new DateTime(2026, 1, 1);
        reservationLine.TerminationDate = new DateTime(2026, 2, 1);
        reservationLine.ValidToDate = new DateTime(2026, 2, 1);

        var asset = dbContext.Assets.Single(item => item.Id == "ASSET-1");
        asset.Status = "OnHire";
        asset.AgreementNumber = "A-CURRENT-HIRE";
        asset.DeliveryDate = new DateTime(2026, 1, 1);
        asset.AgreementLineValidFromDate = new DateTime(2026, 1, 1);
        asset.AgreementLineValidToDate = new DateTime(2026, 2, 1);

        dbContext.SaveChanges();
        return header;
    }

    private static Line CreateLine(int headerId, string lineNumber, DateTime startDate, DateTime endDate, int quantity)
        => new()
        {
            HeaderId = headerId,
            AgreementLineNumber = lineNumber,
            ItemNumber = ItemNumber,
            GenericItemNumber = GenericCode,
            DeliveryDate = startDate,
            ValidFromDate = startDate,
            TerminationDate = endDate,
            ValidToDate = endDate,
            Quantity = quantity,
            Warehouse = WarehouseCode,
            Division = "DIV1",
            Facility = "FAC1",
            OrderSource = "CPQ",
            RequiresFulfilment = true,
        };

    private static Reservation CreateReservation(int lineId, int quantity, bool isConfirmed)
        => new()
        {
            AssetId = ItemNumber,
            ItemNumber = ItemNumber,
            Quantity = quantity,
            EffectiveQuantity = quantity,
            Warehouse = WarehouseCode,
            LineId = lineId,
            IsConfirmed = isConfirmed,
        };
}
