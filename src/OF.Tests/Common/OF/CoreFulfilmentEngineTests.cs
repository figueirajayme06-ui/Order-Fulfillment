using OF.Common.Infrastructure.OF;
using OF.Data.Database;
using OF.Tests.Data.Test;

namespace OF.Tests.Common.OF
{
    [Collection("DatabaseCollection")]
    public class CoreFulfilmentEngineTests : CommonDBTest
    {
        public CoreFulfilmentEngineTests(DatabaseFixture databaseFixture) : base(databaseFixture)
        {
        }

        [Fact]
        public async Task RecalculateStatusForLineAndHeader_Fulfilled_When_All_Fulfilled()
        {
            // Arrange
            var dbContextFactory = await ArrangeAndAct((context) => TestData.AgreementLines.PartiallyFulfilledHeader_All_Lines_Reserved_For_Calculating_Status(context, 2));
            var dbContext = await dbContextFactory.CreateDbContextAsync();
            var repository = new CoreDataRepository(dbContext);
            var fulfulment = new CoreFulfilmentEngine(repository);

            var line = dbContext.Lines.Skip(1).First();

            // Act
            fulfulment.RecalculateStatusForLineAndHeader(line, "michael.law@aggreko.com");

            var header = dbContext.Headers.First();
            // Assert
            Assert.Equal(FulfilmentStatus.FullyFulfiled, (FulfilmentStatus)header.FulfilmentStatus);
        }

        [Fact]
        public async Task RecalculateStatusForLineAndHeader_Fulfilled_When_All_Fulfilled_Contains_Excluded_Line()
        {
            // Arrange
            var dbContextFactory = await ArrangeAndAct((context) => TestData.AgreementLines.PartiallyFulfilledHeader_All_Lines_Reserved_For_Calculating_Status_With_Excluded(context, 2));
            var dbContext = await dbContextFactory.CreateDbContextAsync();
            var repository = new CoreDataRepository(dbContext);
            var fulfulment = new CoreFulfilmentEngine(repository);

            var line = dbContext.Lines.Skip(1).First();

            // Act
            fulfulment.RecalculateStatusForLineAndHeader(line, "michael.law@aggreko.com");

            var header = dbContext.Headers.First();
            // Assert
            Assert.Equal(FulfilmentStatus.FullyFulfiled, (FulfilmentStatus)header.FulfilmentStatus);
        }

        [Fact]
        public async Task AbandonOrphanedQuoteLines_Should_Abandon_Lines_Starting_With_Q()
        {
            // Arrange
            var dbContextFactory = await ArrangeAndAct((context) => {
                var header = TestData.AgreementLines.FulfilledHeaderAndLine(context);

                // Add an orphaned quote line
                var orphanedLine = new Line
                {
                    HeaderId = header.Id,
                    AgreementLineNumber = "Q123456.2",
                    ItemNumber = "TEST002",
                    Quantity = 1,
                    IsDeleted = false,
                    RequiresFulfilment = true
                };
                context.Lines.Add(orphanedLine);

                context.SaveChanges();
                return header;
            });

            var dbContext = await dbContextFactory.CreateDbContextAsync();
            var repository = new CoreDataRepository(dbContext);
            var fulfilmentEngine = new CoreFulfilmentEngine(repository);

            var header = dbContext.Headers.First();

            // Act
            var abandonedCount = fulfilmentEngine.AbandonOrphanedQuoteLines(header.Id, "test-user");

            // Assert
            Assert.Equal(1, abandonedCount);

            // Verify that the orphaned line is now deleted
            var orphanedLine = dbContext.Lines.First(l => l.AgreementLineNumber!.StartsWith("Q"));
            Assert.True(orphanedLine.IsDeleted);

            // Verify that the regular line is not affected
            var regularLine = dbContext.Lines.First(l => l.AgreementLineNumber!.StartsWith("A"));
            Assert.False(regularLine.IsDeleted);
        }

        [Fact]
        public async Task RecalculateStatusForHeader_Should_Be_Partially_Fulfilled_For_Q473715()
        {
            // Arrange
            var dbContextFactory = await ArrangeAndAct((context) =>
            {
                var header = new Header
                {
                    AgreementNumber = "Q473715",
                    AgreementNumbersOnly = "473715",
                    CustomerNumber = "GB00013290",
                    CustomerAddressCode = "900000",
                    FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled
                };

                context.Headers.Add(header);

                var lines = new List<Line>
                {
                    new Line
                    {
                        RequiresFulfilment = true,
                        AgreementLineIndex = 1,
                        AgreementLineNumber = "Q-473715-1",
                        DeliveryDate = DateTime.Now,
                        ItemNumber = "XGCB0063EXT",
                        GenericItemNumber = "XGCB0063EXT",
                        RateType = "4",
                        Division = "200",
                        Facility = "USG",
                        Warehouse = "ED0",
                        NumberOfShifts = "1",
                        Quantity = 4,
                        OrderLineNumber = "Q017a000003GAerrBBG",
                        QuoteLineNumber = "8017a000003GAerAAG",
                        ValidToDate = DateTime.Now.AddDays(3),
                        ValidFromDate = DateTime.Now.AddDays(1),
                        Attributes = "Test",
                        FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled
                    },
                    new Line
                    {
                        RequiresFulfilment = true,
                        AgreementLineIndex = 4,
                        AgreementLineNumber = "Q-473715-4",
                        DeliveryDate = DateTime.Now,
                        ItemNumber = "XGCB0063TMM",
                        GenericItemNumber = "XGCB0063TMM",
                        RateType = "4",
                        Division = "200",
                        Facility = "USG",
                        Warehouse = "ED0",
                        NumberOfShifts = "1",
                        Quantity = 2,
                        OrderLineNumber = "Q017a000003GAerrBBG",
                        QuoteLineNumber = "8017a000003GAerAAG",
                        ValidToDate = DateTime.Now.AddDays(3),
                        ValidFromDate = DateTime.Now.AddDays(1),
                        Attributes = "Test",
                        FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled
                    },
                    new Line
                    {
                        RequiresFulfilment = true,
                        AgreementLineIndex = 5,
                        AgreementLineNumber = "Q-473715-5",
                        DeliveryDate = DateTime.Now,
                        ItemNumber = "XGCB0065EXT",
                        GenericItemNumber = "XGCB0065EXT",
                        RateType = "4",
                        Division = "200",
                        Facility = "USG",
                        Warehouse = "ED0",
                        NumberOfShifts = "1",
                        Quantity = 2,
                        OrderLineNumber = "Q017a000003GAerrBBG",
                        QuoteLineNumber = "8017a000003GAerAAG",
                        ValidToDate = DateTime.Now.AddDays(3),
                        ValidFromDate = DateTime.Now.AddDays(1),
                        Attributes = "Test",
                        FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled
                    },
                    new Line
                    {
                        RequiresFulfilment = true,
                        AgreementLineIndex = 6,
                        AgreementLineNumber = "Q-473715-6",
                        DeliveryDate = DateTime.Now,
                        ItemNumber = "XGCB0065STA",
                        GenericItemNumber = "XGCB0065STA",
                        RateType = "4",
                        Division = "200",
                        Facility = "USG",
                        Warehouse = "ED0",
                        NumberOfShifts = "1",
                        Quantity = 2,
                        OrderLineNumber = "Q017a000003GAerrBBG",
                        QuoteLineNumber = "8017a000003GAerAAG",
                        ValidToDate = DateTime.Now.AddDays(3),
                        ValidFromDate = DateTime.Now.AddDays(1),
                        Attributes = "Test",
                        FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled
                    }
                };

                foreach (var line in lines)
                {
                    context.Lines.Add(line);
                    header.Lines.Add(line);
                }

                context.SaveChanges();
                return header;
            });

            var dbContext = await dbContextFactory.CreateDbContextAsync();
            var repository = new CoreDataRepository(dbContext);
            var fulfilmentEngine = new CoreFulfilmentEngine(repository);

            var header = dbContext.Headers.First();

            // Act
            fulfilmentEngine.RecalculateStatusForHeader(header, "test-user");

            // Assert
            Assert.Equal(FulfilmentStatus.PartiallyFulfilled, (FulfilmentStatus)header.FulfilmentStatus);
        }

        [Fact]
        public async Task RecalculateStatusForHeader_Should_Be_Unfulfilled_For_Q473715()
        {
            // Arrange
            var dbContextFactory = await ArrangeAndAct((context) =>
            {
                var header = new Header
                {
                    AgreementNumber = "Q473715",
                    AgreementNumbersOnly = "473715",
                    CustomerNumber = "GB00013290",
                    CustomerAddressCode = "900000",
                    FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled
                };

                context.Headers.Add(header);

                var lines = new List<Line>
                {
                    new Line
                    {
                        RequiresFulfilment = true,
                        AgreementLineIndex = 1,
                        AgreementLineNumber = "Q-473715-1",
                        DeliveryDate = DateTime.Now,
                        ItemNumber = "XGCB0063EXT",
                        GenericItemNumber = "XGCB0063EXT",
                        RateType = "4",
                        Division = "200",
                        Facility = "USG",
                        Warehouse = "ED0",
                        NumberOfShifts = "1",
                        Quantity = 4,
                        OrderLineNumber = "Q017a000003GAerrBBG",
                        QuoteLineNumber = "8017a000003GAerAAG",
                        ValidToDate = DateTime.Now.AddDays(3),
                        ValidFromDate = DateTime.Now.AddDays(1),
                        Attributes = "Test",
                        FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled
                    },
                    new Line
                    {
                        RequiresFulfilment = true,
                        AgreementLineIndex = 4,
                        AgreementLineNumber = "Q-473715-4",
                        DeliveryDate = DateTime.Now,
                        ItemNumber = "XGCB0063TMM",
                        GenericItemNumber = "XGCB0063TMM",
                        RateType = "4",
                        Division = "200",
                        Facility = "USG",
                        Warehouse = "ED0",
                        NumberOfShifts = "1",
                        Quantity = 2,
                        OrderLineNumber = "Q017a000003GAerrBBG",
                        QuoteLineNumber = "8017a000003GAerAAG",
                        ValidToDate = DateTime.Now.AddDays(3),
                        ValidFromDate = DateTime.Now.AddDays(1),
                        Attributes = "Test",
                        FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled
                    },
                    new Line
                    {
                        RequiresFulfilment = true,
                        AgreementLineIndex = 5,
                        AgreementLineNumber = "Q-473715-5",
                        DeliveryDate = DateTime.Now,
                        ItemNumber = "XGCB0065EXT",
                        GenericItemNumber = "XGCB0065EXT",
                        RateType = "4",
                        Division = "200",
                        Facility = "USG",
                        Warehouse = "ED0",
                        NumberOfShifts = "1",
                        Quantity = 2,
                        OrderLineNumber = "Q017a000003GAerrBBG",
                        QuoteLineNumber = "8017a000003GAerAAG",
                        ValidToDate = DateTime.Now.AddDays(3),
                        ValidFromDate = DateTime.Now.AddDays(1),
                        Attributes = "Test",
                        FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled
                    },
                    new Line
                    {
                        RequiresFulfilment = true,
                        AgreementLineIndex = 6,
                        AgreementLineNumber = "Q-473715-6",
                        DeliveryDate = DateTime.Now,
                        ItemNumber = "XGCB0065STA",
                        GenericItemNumber = "XGCB0065STA",
                        RateType = "4",
                        Division = "200",
                        Facility = "USG",
                        Warehouse = "ED0",
                        NumberOfShifts = "1",
                        Quantity = 2,
                        OrderLineNumber = "Q017a000003GAerrBBG",
                        QuoteLineNumber = "8017a000003GAerAAG",
                        ValidToDate = DateTime.Now.AddDays(3),
                        ValidFromDate = DateTime.Now.AddDays(1),
                        Attributes = "Test",
                        FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled
                    }
                };

                foreach (var line in lines)
                {
                    context.Lines.Add(line);
                    header.Lines.Add(line);
                }

                context.SaveChanges();
                return header;
            });

            var dbContext = await dbContextFactory.CreateDbContextAsync();
            var repository = new CoreDataRepository(dbContext);
            var fulfilmentEngine = new CoreFulfilmentEngine(repository);

            var header = dbContext.Headers.First();

            // Act
            fulfilmentEngine.RecalculateStatusForHeader(header, "test-user");

            // Assert
            Assert.Equal(FulfilmentStatus.Unfulfilled, (FulfilmentStatus)header.FulfilmentStatus);
        }

        [Fact]
        public async Task RecalculateStatusForLineAndHeader_Should_Propagate_Identity_To_Header()
        {
            // Arrange
            var dbContextFactory = await ArrangeAndAct((context) => TestData.AgreementLines.PartiallyFulfilledHeader_All_Lines_Reserved_For_Calculating_Status(context, 2));
            var dbContext = await dbContextFactory.CreateDbContextAsync();
            var repository = new CoreDataRepository(dbContext);
            var fulfilment = new CoreFulfilmentEngine(repository);

            var line = dbContext.Lines.Skip(1).First();

            // Act
            fulfilment.RecalculateStatusForLineAndHeader(line, "suzy.larochelle@aggreko.com");

            // Assert
            var header = dbContext.Headers.First();
            Assert.Equal("suzy.larochelle@aggreko.com", header.LastUpdatedBy);
        }
    }
}
