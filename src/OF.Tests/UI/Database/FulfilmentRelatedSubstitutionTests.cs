using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Time.Testing;
using Moq;
using OF.Data;
using OF.Data.Database;
using OF.Tests.Common;
using OF.UI.Database;

namespace OF.Tests.UI.Database
{
    [Collection("DatabaseCollection")]
    public class FulfilmentRelatedSubstitutionTests : CommonDBTest
    {
        public FulfilmentRelatedSubstitutionTests(DatabaseFixture databaseFixture) : base(databaseFixture)
        {
        }

        /// <summary>
        /// Seeds the minimum CPQ catalogue data needed to exercise the related-specific
        /// substitution path of FulfilNonSerialized.
        /// Returns the seeded Header so it can satisfy the ArrangeAndAct signature.
        /// </summary>
        private static Header SeedCableRelatedSubstitutionData(ApplicationDbContext ctx)
        {
            var family = new CpqFamily { FamilyDescription = "Cable" };
            ctx.CpqFamilies.Add(family);
            ctx.SaveChanges();

            var line = new CpqLine { FamilyId = family.Id, LineDescription = "Cable Line" };
            ctx.CpqLines.Add(line);
            ctx.SaveChanges();

            // Parent generic: XGCB2401STA (STA cable family)
            var staGeneric = new CpqGeneric
            {
                LineId = line.Id,
                GenericCode = "XGCB2401STA",
                GenericDescription = "240mm2 x1 Standard",
                Active = true,
                Deleted = false,
                RentalTermDays = 1,
                UomIntl = "m",
                RatingIntl = "240",
                UomUs = "ft",
                RatingUs = "240",
            };

            // Child generic: XGCB2401EXT (EXT cable family – different generic)
            var extGeneric = new CpqGeneric
            {
                LineId = line.Id,
                GenericCode = "XGCB2401EXT",
                GenericDescription = "240mm2 x1 Extension",
                Active = true,
                Deleted = false,
                RentalTermDays = 1,
                UomIntl = "m",
                RatingIntl = "240",
                UomUs = "ft",
                RatingUs = "240",
            };

            var reservedGeneric = new CpqGeneric
            {
                LineId = line.Id,
                GenericCode = "XGCB2401RES",
                GenericDescription = "Reserved generic",
                Active = true,
                Deleted = false,
                RentalTermDays = 1,
                UomIntl = "m",
                RatingIntl = "240",
                UomUs = "ft",
                RatingUs = "240",
            };

            ctx.CpqGenerics.AddRange(staGeneric, extGeneric, reservedGeneric);
            ctx.SaveChanges();

            var staItem = new CpqItem
            {
                ItemNumber = "CB2401STA060M",
                DescriptionNam = "Cable 240mm2 STA 60m",
                DescriptionIntl = "Cable 240mm2 STA 60m",
                GenericId = staGeneric.Id,
                Active = true,
                Deleted = false,
            };

            var otherStaItem = new CpqItem
            {
                ItemNumber = "CB2401STA120M",
                DescriptionNam = "Cable 240mm2 STA 120m",
                DescriptionIntl = "Cable 240mm2 STA 120m",
                GenericId = staGeneric.Id,
                Active = true,
                Deleted = false,
            };

            var extItem = new CpqItem
            {
                ItemNumber = "CB2401EXT060M",
                DescriptionNam = "Cable 240mm2 EXT 60m",
                DescriptionIntl = "Cable 240mm2 EXT 60m",
                GenericId = extGeneric.Id,
                Active = true,
                Deleted = false,
            };

            var otherExtItem = new CpqItem
            {
                ItemNumber = "CB2401EXT120M",
                DescriptionNam = "Cable 240mm2 EXT 120m",
                DescriptionIntl = "Cable 240mm2 EXT 120m",
                GenericId = extGeneric.Id,
                Active = true,
                Deleted = false,
            };

            var reservedParentItem = new CpqItem
            {
                ItemNumber = "CB2401RES060M",
                DescriptionNam = "Reserved parent cable",
                DescriptionIntl = "Reserved parent cable",
                GenericId = reservedGeneric.Id,
                Active = true,
                Deleted = false,
            };

            var reservedRelatedItem = new CpqItem
            {
                ItemNumber = "CB2401EXTRESM",
                DescriptionNam = "Reserved related cable",
                DescriptionIntl = "Reserved related cable",
                GenericId = extGeneric.Id,
                Active = true,
                Deleted = false,
            };

            ctx.CpqItems.AddRange(staItem, otherStaItem, extItem, otherExtItem, reservedParentItem, reservedRelatedItem);
            ctx.SaveChanges();

            // Related specific substitution: STA060M → EXT060M
            ctx.CpqRelatedSpecificSubstitutions.Add(new CpqRelatedSpecificSubstitution
            {
                ParentItemId = staItem.Id,
                ChildItemId = extItem.Id,
            });
            ctx.CpqRelatedSpecificSubstitutions.Add(new CpqRelatedSpecificSubstitution
            {
                ParentItemId = otherStaItem.Id,
                ChildItemId = otherExtItem.Id,
            });
            ctx.CpqRelatedSpecificSubstitutions.Add(new CpqRelatedSpecificSubstitution
            {
                ParentItemId = reservedParentItem.Id,
                ChildItemId = reservedRelatedItem.Id,
            });
            ctx.SaveChanges();

            // Agreement header and line
            var header = new Header
            {
                AgreementNumber = "T999001",
                AgreementNumbersOnly = "999001",
                CustomerNumber = "US00000001",
                CustomerAddressCode = "900000",
            };
            ctx.Headers.Add(header);
            ctx.SaveChanges();

            var agreementLine = new Line
            {
                HeaderId = header.Id,
                AgreementLineNumber = "T999001-1",
                AgreementLineType = "5",
                ItemNumber = "CB2401STA060M",
                // Deliberately differs from the item's CPQ generic. Master only
                // uses this value when an active non-depot reservation exists.
                GenericItemNumber = "XGCB2401RES",
                RequiresFulfilment = true,
                Quantity = 1,
            };
            ctx.Lines.Add(agreementLine);
            ctx.SaveChanges();

            // WarehouseItem is configured as HasNoKey() so we insert via raw SQL
            ctx.Database.ExecuteSqlRaw(
                @"INSERT INTO WarehouseItems (WarehouseCode, Warehouse, DivisionCode, FacilityCode, Facility, CountryCode, Country)
                  VALUES ('WH01', 'Test Warehouse One', 'DIV1', 'TST', 'Test', 'GB', 'United Kingdom')");

            // Stock: product item for the EXT cable
            ctx.ProductItems.AddRange(
                new ProductItem
                {
                    ItemNumber = "CB2401EXT060M",
                    Warehouse = "WH01",
                    Division = "DIV1",
                    Facility = "TST",
                    Status = "Available",
                    StockQuantity = 5,
                    AllocatedQuantity = 0,
                },
                new ProductItem
                {
                    ItemNumber = "CB2401EXT120M",
                    Warehouse = "WH01",
                    Division = "DIV1",
                    Facility = "TST",
                    Status = "Available",
                    StockQuantity = 5,
                    AllocatedQuantity = 0,
                },
                new ProductItem
                {
                    ItemNumber = "CB2401EXTRESM",
                    Warehouse = "WH01",
                    Division = "DIV1",
                    Facility = "TST",
                    Status = "Available",
                    StockQuantity = 5,
                    AllocatedQuantity = 0,
                });
            ctx.SaveChanges();

            return header;
        }

        [SkippableFact]
        public async Task GetNonSerializedStock_RelatedSpecificSubstitution_ReturnsChildWithRelatedReason()
        {
            // Arrange
            var dbContextFactory = await ArrangeAndAct(SeedCableRelatedSubstitutionData);

            var dbContext = await dbContextFactory.CreateDbContextAsync();
            var lineId = dbContext.Lines.First(l => l.ItemNumber == "CB2401STA060M").Id;

            var mockCache = new Mock<IMemoryCache>();
            var timeProvider = new FakeTimeProvider();
            var repository = new DataRepository(dbContext, mockCache.Object, timeProvider);

            // Act
            var result = repository.GetNonSerializedStock(lineId, ["DIV1"], string.Empty, []);

            // Assert — the EXT item should appear as a related substitute
            Assert.Contains(result, r => r.Asset.ItemNumber == "CB2401EXT060M");
            var extItem = result.First(r => r.Asset.ItemNumber == "CB2401EXT060M");
            Assert.Equal("RELATED", extItem.SubstitutionReason);
        }

        [SkippableFact]
        public async Task GetAvailabilitySummary_RelatedSpecificSubstitution_MirrorsLegacyLineRulesAndData()
        {
            var dbContextFactory = await ArrangeAndAct(SeedCableRelatedSubstitutionData);
            var dbContext = await dbContextFactory.CreateDbContextAsync();
            var repository = new DataRepository(
                dbContext,
                new Mock<IMemoryCache>().Object,
                new FakeTimeProvider());
            var lineId = dbContext.Lines.Single(line => line.ItemNumber == "CB2401STA060M").Id;

            var availabilityResults = await repository.GetAvailabilitySummaryAsync(
                // These deliberately use the line's stored generic. The procedure
                // must derive XGCB2401STA from Lines.ItemNumber, as master does.
                "XGCB2401RES",
                string.Empty,
                null,
                null,
                "DIV1",
                "CB2401STA060M",
                lineId);

            var relatedItem = Assert.Single(availabilityResults, item => item.ItemNumber == "CB2401EXT060M");
            Assert.Equal("RELATED", relatedItem.SubstitutionReason);
            Assert.Contains(availabilityResults, item =>
                item.ItemNumber == "CB2401EXT120M" && item.SubstitutionReason == "RELATED");
            Assert.DoesNotContain(availabilityResults, item => item.ItemNumber == "CB2401EXTRESM");

            var legacyRelatedItems = repository.GetNonSerializedStock(lineId, ["DIV1"], string.Empty, [])
                .Where(result => result.SubstitutionReason == "RELATED")
                .Select(result => result.Asset.ItemNumber)
                .ToHashSet();
            var availabilityRelatedItems = availabilityResults
                .Where(result => result.SubstitutionReason == "RELATED")
                .Select(result => result.ItemNumber)
                .ToHashSet();

            Assert.Equal(legacyRelatedItems, availabilityRelatedItems);

            dbContext.Reservations.Add(new Reservation
            {
                AssetId = "CB2401EXT060M",
                ItemNumber = "CB2401EXT060M",
                Warehouse = "WH01",
                LineId = lineId,
                Quantity = 1,
                EffectiveQuantity = 1,
                IsDepotFulfilled = false,
            });
            dbContext.SaveChanges();

            var resultsWithReservation = await repository.GetAvailabilitySummaryAsync(
                "XGCB2401RES",
                string.Empty,
                null,
                null,
                "DIV1",
                "CB2401STA060M",
                lineId);

            Assert.Contains(resultsWithReservation, item =>
                item.ItemNumber == "CB2401EXTRESM" && item.SubstitutionReason == "RELATED");

            var legacyRelatedItemsWithReservation = repository.GetNonSerializedStock(lineId, ["DIV1"], string.Empty, [])
                .Where(result => result.SubstitutionReason == "RELATED")
                .Select(result => result.Asset.ItemNumber)
                .ToHashSet();
            var availabilityRelatedItemsWithReservation = resultsWithReservation
                .Where(result => result.SubstitutionReason == "RELATED")
                .Select(result => result.ItemNumber)
                .ToHashSet();

            Assert.Equal(legacyRelatedItemsWithReservation, availabilityRelatedItemsWithReservation);
        }

        [SkippableFact]
        public async Task GetNonSerializedStock_DivisionLongerThanFiveCharacters_DoesNotTruncateDivision()
        {
            // Arrange
            var dbContextFactory = await ArrangeAndAct(ctx =>
            {
                var header = SeedCableRelatedSubstitutionData(ctx);

                ctx.Database.ExecuteSqlRaw(
                    @"INSERT INTO WarehouseItems (WarehouseCode, Warehouse, DivisionCode, FacilityCode, Facility, CountryCode, Country)
                      VALUES ('WH99', 'Unknown Division Warehouse', 'Unknown', 'TST', 'Test', 'GB', 'United Kingdom')");

                ctx.ProductItems.Add(new ProductItem
                {
                    ItemNumber = "CB2401EXT060M",
                    Warehouse = "WH99",
                    Division = "Unknown",
                    Facility = "TST",
                    Status = "Available",
                    StockQuantity = 5,
                    AllocatedQuantity = 0,
                });
                ctx.SaveChanges();

                return header;
            });

            var dbContext = await dbContextFactory.CreateDbContextAsync();
            var lineId = dbContext.Lines.First(line => line.ItemNumber == "CB2401STA060M").Id;
            var repository = new DataRepository(
                dbContext,
                new Mock<IMemoryCache>().Object,
                new FakeTimeProvider());

            // Act
            var result = repository.GetNonSerializedStock(lineId, ["Unknown"], string.Empty, []);

            // Assert
            Assert.Contains(result, item =>
                item.Asset.ItemNumber == "CB2401EXT060M" &&
                item.Asset.Warehouse == "WH99" &&
                item.Asset.Division == "Unknown");
        }

        [SkippableFact]
        public async Task GetNonSerializedStock_RelatedSpecificSubstitution_ExcludedWhenItDoesNotMatchAttributeFilter()
        {
        // Related substitutions follow the same FAM attribute filter as ordinary substitutions.
        // The EXT item has no matching attribute, so it is excluded.

            // Arrange
            var dbContextFactory = await ArrangeAndAct(SeedCableRelatedSubstitutionData);

            var dbContext = await dbContextFactory.CreateDbContextAsync();
            var lineId = dbContext.Lines.First(l => l.ItemNumber == "CB2401STA060M").Id;

            var mockCache = new Mock<IMemoryCache>();
            var timeProvider = new FakeTimeProvider();
            var repository = new DataRepository(dbContext, mockCache.Object, timeProvider);

            // Act — pass a dummy attribute that won't match any CPQ_LineAttributePurpose entry,
            // which forces the IF EXISTS(@att_table) branch
            var result = repository.GetNonSerializedStock(lineId, ["DIV1"], string.Empty, ["SomeAttribute:SomeValue"]);

        // Assert — related substitute is excluded because it does not match the attribute filter
            Assert.DoesNotContain(result, r => r.Asset.ItemNumber == "CB2401EXT060M");
        }
    }
}
