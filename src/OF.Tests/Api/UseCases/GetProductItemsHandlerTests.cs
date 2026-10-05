using AutoFixture;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OF.Common.Infrastructure.CloudSuite;
using OF.Data;
using OF.Data.Database;
using OF.Data.ProductItems.UseCases;
using OF.Tests.AutoFixture;
using OF.Tests.AutoFixture.Customizations;
using OF.Tests.Common;
using OF.Tests.Data;
using System.Reflection;
using Xunit;

namespace OF.Tests.Api.UseCases
{
    [Collection("DatabaseCollection")]
    public class GetProductItemsHandlerTests : CommonDBTest
    {
        private readonly Fixture _fixture;

        public GetProductItemsHandlerTests(DatabaseFixture databaseFixture) : base(databaseFixture)
        {
            _fixture = new Fixture();
            _fixture.Customize(new MoqCustomization());

            // Configure AutoFixture to handle circular references
            _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
                .ForEach(b => _fixture.Behaviors.Remove(b));
            _fixture.Behaviors.Add(new OmitOnRecursionBehavior());
        }

        [SkippableFact]
        public async Task Handle_WithCpqItemsAndSyncListItems_FetchesCombinedItemNumbers()
        {
            // Arrange
            var dbContextFactory = await ArrangeAndAct();
            using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockCloudSuiteService = new Mock<ICloudSuiteService>();
            var mockLogger = new Mock<ILogger<GetProductItemsHandler>>();

            // Create the full hierarchy: CpqFamily -> CpqLine -> CpqGeneric -> CpqItem
            var cpqFamily = new CpqFamily { FamilyDescription = "Test Family" };
            dbContext.CpqFamilies.Add(cpqFamily);
            await dbContext.SaveChangesAsync(); // Save to get the ID

            var cpqLine = new CpqLine { FamilyId = cpqFamily.Id, LineDescription = "Test Line" };
            dbContext.CpqLines.Add(cpqLine);
            await dbContext.SaveChangesAsync(); // Save to get the ID

            var cpqGeneric = new CpqGeneric { LineId = cpqLine.Id, GenericCode = "GEN001", GenericDescription = "Test Generic", Active = true, Deleted = false, VerCol = new byte[8] };
            dbContext.CpqGenerics.Add(cpqGeneric);
            await dbContext.SaveChangesAsync(); // Save to get the ID

            // Add test data to CPQ_Item table manually to avoid circular reference issues
            var cpqItems = new List<CpqItem>
            {
                new() { ItemNumber = "CPQ001", DescriptionNam = "Test1", DescriptionIntl = "Test1", GenericId = cpqGeneric.Id, Active = true, Deleted = false, VerCol = new byte[8] },
                new() { ItemNumber = "CPQ002", DescriptionNam = "Test2", DescriptionIntl = "Test2", GenericId = cpqGeneric.Id, Active = true, Deleted = false, VerCol = new byte[8] },
                new() { ItemNumber = "SHARED001", DescriptionNam = "Test3", DescriptionIntl = "Test3", GenericId = cpqGeneric.Id, Active = true, Deleted = false, VerCol = new byte[8] }
            };

            dbContext.CpqItems.AddRange(cpqItems);

            // Add test data to ProductItemsSyncList table manually
            var syncListItems = new List<ProductItemsSyncList>
            {
                new() { ItemNumber = "SYNC001", IsActive = true, CreatedDate = DateTime.Now },
                new() { ItemNumber = "SYNC002", IsActive = true, CreatedDate = DateTime.Now },
                new() { ItemNumber = "SHARED001", IsActive = true, CreatedDate = DateTime.Now }, // Duplicate should be handled
                new() { ItemNumber = "INACTIVE001", IsActive = false, CreatedDate = DateTime.Now } // Should be excluded
            };

            dbContext.ProductItemsSyncList.AddRange(syncListItems);
            await dbContext.SaveChangesAsync();

            var mockProductItems = new List<ProductItemsStaging>
            {
                new() { ItemNumber = "CPQ001", Warehouse = "100", StockQuantity = 10, AllocatedQuantity = 5, Facility = "1", Division = "1", Status = "20" },
                new() { ItemNumber = "SYNC001", Warehouse = "200", StockQuantity = 20, AllocatedQuantity = 10, Facility = "1", Division = "1", Status = "20" }
            };

            mockCloudSuiteService.Setup(x => x.RunDataLakeQuery<ProductItemsStaging>(It.IsAny<string>()))
                .ReturnsAsync(mockProductItems);

            var handler = new GetProductItemsHandler(mockCloudSuiteService.Object, dbContext, mockLogger.Object);

            // Act
            await handler.Handle();

            // Assert
            mockCloudSuiteService.Verify(x => x.RunDataLakeQuery<ProductItemsStaging>(
                It.Is<string>(sql =>
                    sql.Contains("'CPQ001'") &&
                    sql.Contains("'CPQ002'") &&
                    sql.Contains("'SYNC001'") &&
                    sql.Contains("'SYNC002'") &&
                    sql.Contains("'SHARED001'") &&
                    !sql.Contains("'INACTIVE001'"))), Times.Once);
        }

        [SkippableFact]
        public async Task Handle_WithNoItemNumbers_LogsWarningAndReturns()
        {
            // Arrange
            var dbContextFactory = await ArrangeAndAct();
            using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockCloudSuiteService = new Mock<ICloudSuiteService>();
            var mockLogger = new Mock<ILogger<GetProductItemsHandler>>();
            var handler = new GetProductItemsHandler(mockCloudSuiteService.Object, dbContext, mockLogger.Object);

            // Act
            await handler.Handle();

            // Assert
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No item numbers found for product item sync")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            mockCloudSuiteService.Verify(x => x.RunDataLakeQuery<ProductItemsStaging>(It.IsAny<string>()), Times.Never);
        }

        [SkippableFact]
        public async Task Handle_WithDuplicateItemNumbers_DeduplicatesCorrectly()
        {
            // Arrange
            var dbContextFactory = await ArrangeAndAct();
            using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockCloudSuiteService = new Mock<ICloudSuiteService>();
            var mockLogger = new Mock<ILogger<GetProductItemsHandler>>();

            // Create the full hierarchy: CpqFamily -> CpqLine -> CpqGeneric -> CpqItem
            var cpqFamily = new CpqFamily { FamilyDescription = "Test Family" };
            dbContext.CpqFamilies.Add(cpqFamily);
            await dbContext.SaveChangesAsync();

            var cpqLine = new CpqLine { FamilyId = cpqFamily.Id, LineDescription = "Test Line" };
            dbContext.CpqLines.Add(cpqLine);
            await dbContext.SaveChangesAsync();

            var cpqGeneric = new CpqGeneric { LineId = cpqLine.Id, GenericCode = "GEN001", GenericDescription = "Test Generic", Active = true, Deleted = false, VerCol = new byte[8] };
            dbContext.CpqGenerics.Add(cpqGeneric);
            await dbContext.SaveChangesAsync();

            var cpqItems = new List<CpqItem>
            {
                new() { ItemNumber = "DUPLICATE001", DescriptionNam = "Test", DescriptionIntl = "Test", GenericId = cpqGeneric.Id, Active = true, Deleted = false, VerCol = new byte[8] }
            };

            dbContext.CpqItems.AddRange(cpqItems);

            var syncListItems = new List<ProductItemsSyncList>
            {
                new() { ItemNumber = "DUPLICATE001", IsActive = true, CreatedDate = DateTime.Now } // Same as CPQ item
            };

            dbContext.ProductItemsSyncList.AddRange(syncListItems);
            await dbContext.SaveChangesAsync();

            mockCloudSuiteService.Setup(x => x.RunDataLakeQuery<ProductItemsStaging>(It.IsAny<string>()))
                .ReturnsAsync(new List<ProductItemsStaging>());

            var handler = new GetProductItemsHandler(mockCloudSuiteService.Object, dbContext, mockLogger.Object);

            // Act
            await handler.Handle();

            // Assert - Verify that DUPLICATE001 appears only once in the SQL
            mockCloudSuiteService.Verify(x => x.RunDataLakeQuery<ProductItemsStaging>(
                It.Is<string>(sql =>
                    sql.IndexOf("'DUPLICATE001'") == sql.LastIndexOf("'DUPLICATE001'"))), // Should appear only once
                Times.Once);
        }

        [SkippableTheory]
        [AutoData]
        public void GenerateDynamicProductDetailsQuery_WithValidItems_GeneratesCorrectSQL(
            List<string> itemNumbers)
        {
            // Arrange
            itemNumbers = new List<string> { "ITEM001", "ITEM002", "ITEM003" };

            var mockCloudSuiteService = new Mock<ICloudSuiteService>();
            var mockLogger = new Mock<ILogger<GetProductItemsHandler>>();
            var handler = new GetProductItemsHandler(mockCloudSuiteService.Object, dbContext, mockLogger.Object);

            var method = typeof(GetProductItemsHandler).GetMethod("GenerateDynamicProductDetailsQuery",
                BindingFlags.NonPublic | BindingFlags.Instance);

            // Act
            var result = (string)method!.Invoke(handler, new object[] { itemNumbers })!;

            // Assert
            result.Should().Contain("select WHLO, ITNO, STQT, ALQT, AVAL, WHSL, FACI, DIVI, STAT from default.mitbal mb");
            result.Should().Contain("where mb.CONO = '1'");
            result.Should().Contain("and mb.ITNO IN");
            result.Should().Contain("(select ITNO from default.MITMAS mm");
            result.Should().Contain("where mm.ITNO in ('ITEM001','ITEM002','ITEM003')");
            result.Should().Contain("and mm.STAT = 20");
            result.Should().Contain("and mm.INDI <> 2");
            result.Should().Contain("and mb.STAT = '20'");
            result.Should().Contain("and (mb.WHLO like '%0' or mb.WHLO like '%5')");
            result.Should().Contain("order by ITNO, WHLO");
        }

        [SkippableFact]
        public void GenerateDynamicProductDetailsQuery_WithApostropheInItemNumber_EscapesCorrectly()
        {
            // Arrange
            var itemNumbers = new List<string> { "ITEM'001", "ITEM002" };

            var mockCloudSuiteService = new Mock<ICloudSuiteService>();
            var mockLogger = new Mock<ILogger<GetProductItemsHandler>>();
            var handler = new GetProductItemsHandler(mockCloudSuiteService.Object, dbContext, mockLogger.Object);

            var method = typeof(GetProductItemsHandler).GetMethod("GenerateDynamicProductDetailsQuery",
                BindingFlags.NonPublic | BindingFlags.Instance);

            // Act
            var result = (string)method!.Invoke(handler, new object[] { itemNumbers })!;

            // Assert
            result.Should().Contain("'ITEM''001','ITEM002'");
        }

        [SkippableFact]
        public void GenerateDynamicProductDetailsQuery_WithLargeItemList_GeneratesInClause()
        {
            // Arrange
            var itemNumbers = Enumerable.Range(1, 100).Select(i => $"ITEM{i:D4}").ToList();

            var mockCloudSuiteService = new Mock<ICloudSuiteService>();
            var mockLogger = new Mock<ILogger<GetProductItemsHandler>>();
            var handler = new GetProductItemsHandler(mockCloudSuiteService.Object, dbContext, mockLogger.Object);

            var method = typeof(GetProductItemsHandler).GetMethod("GenerateDynamicProductDetailsQuery",
                BindingFlags.NonPublic | BindingFlags.Instance);

            // Act
            var result = (string)method!.Invoke(handler, new object[] { itemNumbers })!;

            // Assert
            result.Should().Contain("where mm.ITNO in (");
            result.Should().Contain("'ITEM0001'");
            result.Should().Contain("'ITEM0100'");
            // Verify all items are included in a single IN clause
            var itemCount = result.Split("'ITEM").Length - 1;
            itemCount.Should().Be(100);
        }

        [SkippableFact]
        public void GenerateDynamicProductDetailsQuery_WithEmptyList_GeneratesEmptyInClause()
        {
            // Arrange
            var itemNumbers = new List<string>();

            var mockCloudSuiteService = new Mock<ICloudSuiteService>();
            var mockLogger = new Mock<ILogger<GetProductItemsHandler>>();
            var handler = new GetProductItemsHandler(mockCloudSuiteService.Object, dbContext, mockLogger.Object);

            var method = typeof(GetProductItemsHandler).GetMethod("GenerateDynamicProductDetailsQuery",
                BindingFlags.NonPublic | BindingFlags.Instance);

            // Act
            var result = (string)method!.Invoke(handler, new object[] { itemNumbers })!;

            // Assert
            result.Should().Contain("where mm.ITNO in ()");
        }

        [SkippableFact]
        public async Task Handle_LogsCorrectInformation()
        {
            // Arrange
            var dbContextFactory = await ArrangeAndAct();
            using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockCloudSuiteService = new Mock<ICloudSuiteService>();
            var mockLogger = new Mock<ILogger<GetProductItemsHandler>>();

            // Create the full hierarchy: CpqFamily -> CpqLine -> CpqGeneric -> CpqItem
            var cpqFamily = new CpqFamily { FamilyDescription = "Test Family" };
            dbContext.CpqFamilies.Add(cpqFamily);
            await dbContext.SaveChangesAsync();

            var cpqLine = new CpqLine { FamilyId = cpqFamily.Id, LineDescription = "Test Line" };
            dbContext.CpqLines.Add(cpqLine);
            await dbContext.SaveChangesAsync();

            var cpqGeneric = new CpqGeneric { LineId = cpqLine.Id, GenericCode = "GEN001", GenericDescription = "Test Generic", Active = true, Deleted = false, VerCol = new byte[8] };
            dbContext.CpqGenerics.Add(cpqGeneric);
            await dbContext.SaveChangesAsync();

            var cpqItems = new List<CpqItem>
            {
                new() { ItemNumber = "TEST001", DescriptionNam = "Test1", DescriptionIntl = "Test1", GenericId = cpqGeneric.Id, Active = true, Deleted = false, VerCol = new byte[8] },
                new() { ItemNumber = "TEST002", DescriptionNam = "Test2", DescriptionIntl = "Test2", GenericId = cpqGeneric.Id, Active = true, Deleted = false, VerCol = new byte[8] }
            };

            dbContext.CpqItems.AddRange(cpqItems);
            await dbContext.SaveChangesAsync();

            mockCloudSuiteService.Setup(x => x.RunDataLakeQuery<ProductItemsStaging>(It.IsAny<string>()))
                .ReturnsAsync(new List<ProductItemsStaging>());

            var handler = new GetProductItemsHandler(mockCloudSuiteService.Object, dbContext, mockLogger.Object);

            // Act
            await handler.Handle();

            // Assert
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Getting product items from ION Data lake")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Found 2 unique item numbers for sync")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }
}
