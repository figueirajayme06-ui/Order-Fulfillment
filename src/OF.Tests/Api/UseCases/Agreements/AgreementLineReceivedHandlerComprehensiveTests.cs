using AutoFixture;
using AutoFixture.AutoMoq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OF.Api.UseCases.Agreements;
using OF.Common.Infrastructure.IPG.Orders;
using OF.Common.Infrastructure.OF;
using OF.Data;
using OF.Data.Database;
using OF.Tests.Common;
using OF.Tests.Data.Test.BODs.AgreementLines;
using static OF.Common.Enums;

namespace OF.Tests.Api.UseCases.Agreements
{
    [Collection("DatabaseCollection")]
    public class AgreementLineReceivedHandlerComprehensiveTests : CommonDBTest
    {
        private readonly IFixture _fixture;

        public AgreementLineReceivedHandlerComprehensiveTests(DatabaseFixture databaseFixture) : base(databaseFixture)
        {
            _fixture = new Fixture().Customize(new AutoMoqCustomization());
            _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
                .ForEach(b => _fixture.Behaviors.Remove(b));
            _fixture.Behaviors.Add(new OmitOnRecursionBehavior());
        }

        [SkippableFact]
        public async Task Existing_Header_New_Line_Insert_Creates_Line_In_Production_Environment()
        {
            // Arrange
            var dbContextFactory = await SeedData(nameof(Existing_Header_New_Line_Insert_Creates_Line_In_Production_Environment));
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var targetHeader = new Header
            {
                AgreementNumber = "A712565",
                AgreementNumbersOnly = "712565",
                CustomerNumber = "US00103535",
                CustomerAddressCode = "800000",
                CustomerAddress = "123 Test St, Test City, TS 12345",
                Division = "200",
                Facility = "USG",
                OrderSource = "CPQ",
                FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled,
                ActivationStatus = (int)ActivationStatus.Activated
            };

            dbContext.Headers.Add(targetHeader);
            await dbContext.SaveChangesAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            var fulfilmentEngine = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var mockLogger = new Mock<ILogger>();

            var sut = new AgreementLineReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngine, mockLogger.Object);

            // Act
            await sut.Handle(AgreementLineBOD.Sync);

            // Assert
            var headers = await dbContext.Headers.ToListAsync();
            var targetHeaderRefreshed = await dbContext.Headers
                .Include(h => h.Lines)
                .FirstAsync(h => h.AgreementNumbersOnly == "712565");
            var newLine = targetHeaderRefreshed.Lines.FirstOrDefault(l => l.AgreementLineNumber == "A712565-3");

            // Verify production environment (multiple headers exist)
            Assert.True(headers.Count > 1, "Should have multiple headers in production environment");

            // Verify the new line was created
            Assert.NotNull(newLine);
            Assert.Equal("XDH51005R", newLine.ItemNumber);
            Assert.Equal("BD0", newLine.Warehouse);
            Assert.Equal(1, newLine.Quantity);
            Assert.Equal("712565", newLine.AgreementNumbersOnly);
            Assert.Equal(targetHeader.Id, newLine.HeaderId);
            Assert.Equal("ABC", newLine.PackageGroupNumber);
        }

        [SkippableFact]
        public async Task Existing_Header_Existing_Line_Update_Modifies_Line_In_Production_Environment()
        {
            // Arrange
            var dbContextFactory = await SeedData(nameof(Existing_Header_Existing_Line_Update_Modifies_Line_In_Production_Environment));
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var targetHeader = new Header
            {
                AgreementNumber = "A712565",
                AgreementNumbersOnly = "712565",
                CustomerNumber = "US00103535",
                CustomerAddressCode = "800000",
                CustomerAddress = "123 Test St, Test City, TS 12345",
                Division = "200",
                Facility = "USG",
                OrderSource = "CPQ",
                FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled,
                ActivationStatus = (int)ActivationStatus.TODO
            };

            var existingLine = new Line
            {
                Header = targetHeader,
                AgreementLineNumber = "A712565-3",
                AgreementNumbersOnly = "712565",
                AgreementLineIndex = 3,
                ItemNumber = "OLD_ITEM",
                GenericItemNumber = "OLD_GENERIC",
                Warehouse = "OLD_WH",
                Quantity = 5,
                Division = "200",
                Facility = "USG",
                OrderSource = "CPQ",
                ValidFromDate = DateTime.UtcNow.AddDays(-1),
                ValidToDate = DateTime.UtcNow.AddDays(1),
                FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled,
                ActivationStatus = (int)ActivationStatus.TODO,
                RequiresFulfilment = true
            };

            dbContext.Headers.Add(targetHeader);
            dbContext.Lines.Add(existingLine);
            await dbContext.SaveChangesAsync();

            var originalLineId = existingLine.Id;

            var mockOmService = new Mock<IOrderManagementIntegration>();
            var fulfilmentEngine = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var mockLogger = new Mock<ILogger>();

            var sut = new AgreementLineReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngine, mockLogger.Object);

            // Act
            await sut.Handle(AgreementLineBOD.Sync);

            // Assert
            var headers = await dbContext.Headers.ToListAsync();
            var lines = await dbContext.Lines.ToListAsync();
            var updatedLine = await dbContext.Lines.FirstAsync(l => l.Id == originalLineId);

            // Verify production environment
            Assert.True(headers.Count > 1, "Should have multiple headers in production environment");
            Assert.True(lines.Count > 1, "Should have multiple lines in production environment");

            // Verify the line was updated (not duplicated)
            Assert.Equal(1, lines.Count(l => l.AgreementLineNumber == "A712565-3"));
            Assert.Equal(originalLineId, updatedLine.Id);
            Assert.Equal("XDH51005R", updatedLine.ItemNumber);
            Assert.Equal("BD0", updatedLine.Warehouse);
            Assert.Equal(1, updatedLine.Quantity);
            Assert.Equal("ABC", updatedLine.PackageGroupNumber);
        }

        [SkippableFact]
        public async Task Delete_Action_Code_Removes_Line_In_Production_Environment()
        {
            // Arrange
            var dbContextFactory = await SeedData(nameof(Delete_Action_Code_Removes_Line_In_Production_Environment));
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var targetHeader = new Header
            {
                AgreementNumber = "A712794",
                AgreementNumbersOnly = "712794",
                CustomerNumber = "US00103535",
                CustomerAddressCode = "900000",
                Division = "200",
                Facility = "USG",
                OrderSource = "CPQ",
                FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled,
                ActivationStatus = (int)ActivationStatus.TODO
            };

            var lineToDelete = new Line
            {
                Header = targetHeader,
                AgreementLineNumber = "A712794-1",
                AgreementNumbersOnly = "712794",
                AgreementLineIndex = 1,
                ItemNumber = "XGCE1500",
                GenericItemNumber = "XGCE1500",
                Warehouse = "ED0",
                Quantity = 2,
                Division = "200",
                Facility = "USG",
                OrderSource = "CPQ",
                ValidFromDate = DateTime.UtcNow.AddDays(-1),
                ValidToDate = DateTime.UtcNow.AddDays(1),
                FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled,
                ActivationStatus = (int)ActivationStatus.TODO,
                RequiresFulfilment = true
            };

            dbContext.Headers.Add(targetHeader);
            dbContext.Lines.Add(lineToDelete);
            await dbContext.SaveChangesAsync();

            var lineIdToDelete = lineToDelete.Id;

            var mockOmService = new Mock<IOrderManagementIntegration>();
            var fulfilmentEngine = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var mockLogger = new Mock<ILogger>();

            var sut = new AgreementLineReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngine, mockLogger.Object);

            // Act
            await sut.Handle(AgreementLineBOD.SyncDeleted);

            // Assert
            var deletedLine = await dbContext.Lines.FirstOrDefaultAsync(l => l.Id == lineIdToDelete);
            Assert.NotNull(deletedLine);
            Assert.True(deletedLine.IsDeleted);
        }

        [SkippableFact]
        public async Task No_Existing_Header_Creates_Skeleton_Header_In_Production_Environment()
        {
            // Arrange
            var dbContextFactory = await SeedData(nameof(No_Existing_Header_Creates_Skeleton_Header_In_Production_Environment));
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            var fulfilmentEngine = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var mockLogger = new Mock<ILogger>();

            var sut = new AgreementLineReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngine, mockLogger.Object);

            // Act
            await sut.Handle(AgreementLineBOD.Sync);

            // Assert
            var headers = await dbContext.Headers.ToListAsync();
            var skeletonHeader = await dbContext.Headers
                .Include(h => h.Lines)
                .FirstOrDefaultAsync(h => h.AgreementNumbersOnly == "712565");

            // Verify production environment
            Assert.True(headers.Count > 1, "Should have multiple headers in production environment");

            // Verify skeleton header was created
            Assert.NotNull(skeletonHeader);
            Assert.Equal("A712565", skeletonHeader.AgreementNumber);
            Assert.Equal("712565", skeletonHeader.AgreementNumbersOnly);
            Assert.True(skeletonHeader.IsSkeleton);

            // Verify the line was added to skeleton header
            Assert.Single(skeletonHeader.Lines);
            var line = skeletonHeader.Lines.First();
            Assert.Equal("XDH51005R", line.ItemNumber);
            Assert.Equal("BD0", line.Warehouse);
        }

        [SkippableFact]
        public async Task Parallel_Processing_Different_Agreements_Succeeds()
        {
            // Arrange  
            const int bodCount = 20;
            Dictionary<int, string> bods = new();

            var dbContextFactory = await SeedData(nameof(Parallel_Processing_Different_Agreements_Succeeds));
            var mockOmService = new Mock<IOrderManagementIntegration>();
            var mockLogger = new Mock<ILogger>();

            // Create BODs for different agreements
            for (int i = 1; i <= bodCount; i++)
            {
                int num = 712000 + i;

                bods.Add(num, CreateBodForAgreement(
                    agreementNumbersOnly: num.ToString(),
                    agreementNumber: $"A{num}",
                    agreementLineId: $"A{num}-1",
                    itemNumber: $"ITEM_{num}"));
            }

            // Act
            var tasks = new List<Task>();
            foreach (var bod in bods)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var dbContext = await dbContextFactory.CreateDbContextAsync();
                    var fulfilmentEngine = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
                    var sut = new AgreementLineReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngine, mockLogger.Object);
                    await sut.Handle(bod.Value);
                }));
            }

            // Process BODs in parallel
            await Task.WhenAll(tasks);

            // Assert
            var dbContext = await dbContextFactory.CreateDbContextAsync();
            var headers = await dbContext.Headers.Include(h => h.Lines).ToListAsync();

            // Verify all agreements were processed
            foreach (var bod in bods)
            {
                var agreement = headers.FirstOrDefault(h => h.AgreementNumbersOnly == bod.Key.ToString());
                Assert.NotNull(agreement);
                Assert.Single(agreement.Lines);
                Assert.Equal($"ITEM_{bod.Key}", agreement.Lines.First().ItemNumber);
            }
        }

        /// <summary>
        /// Sets up an environment with multiple headers and lines
        /// </summary>
        private async Task<IDbContextFactory<ApplicationDbContext>> SeedData(string testName)
        {
            var dbContextFactory = databaseFixture.SetupDbContext(testName);
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            // Create headers with varying numbers of lines
            var headers = _fixture.Build<Header>()
                .Without(h => h.Id)
                .With(h => h.Division, "200")
                .With(h => h.Facility, () => $"F{_fixture.Create<int>() % 10}")
                .With(h => h.OrderSource, "CPQ")
                .With(h => h.RentalDepot, () => $"RD{_fixture.Create<int>() % 100:D2}")
                .With(h => h.FulfilmentStatus, () => _fixture.Create<int>() % 4)
                .With(h => h.ActivationStatus, () => _fixture.Create<int>() % 4)
                .With(h => h.IsDeleted, false)
                .With(h => h.IsSkeleton, false)
                .Without(h => h.Lines)
                .Without(h => h.ChangeOrderHeaders)
                .Without(h => h.ChangeOrders)
                .CreateMany(1000)
                .ToList();

            for (int i = 0; i < headers.Count; i++)
            {
                var header = headers[i];
                header.AgreementNumber = $"A{800000 + i}";
                header.AgreementNumbersOnly = $"{800000 + i}";

                dbContext.Headers.Add(header);

                // Add 2-5 lines per header
                var lineCount = 2 + (i % 4);
                var lines = _fixture.Build<Line>()
                    .Without(l => l.Id)
                    .With(l => l.Header, header)
                    .With(l => l.HeaderId, (int?)null)
                    .With(l => l.AgreementNumbersOnly, $"{800000 + i}")
                    .With(l => l.Division, "200")
                    .With(l => l.Facility, header.Facility)
                    .With(l => l.OrderSource, "CPQ")
                    .With(l => l.ValidFromDate, DateTime.UtcNow.AddDays(-7))
                    .With(l => l.ValidToDate, DateTime.UtcNow.AddDays(30))
                    .With(l => l.FulfilmentStatus, () => _fixture.Create<int>() % 4)
                    .With(l => l.ActivationStatus, () => _fixture.Create<int>() % 4)
                    .With(l => l.RequiresFulfilment, true)
                    .With(l => l.IsDeleted, false)
                    .With(l => l.Status, (string?)null)
                    .With(l => l.Warehouse, () => $"WH{_fixture.Create<int>() % 10}")
                    .Without(l => l.ChangeOrderLines)
                    .CreateMany(lineCount)
                    .ToList();

                for (int j = 0; j < lines.Count; j++)
                {
                    lines[j].AgreementLineNumber = $"A{800000 + i}-{j + 1}";
                    lines[j].AgreementLineIndex = j + 1;
                    dbContext.Lines.Add(lines[j]);
                }
            }

            await dbContext.SaveChangesAsync();
            return dbContextFactory;
        }

        /// <summary>
        /// Creates a BOD XML string for a specific agreement and line using the Sync.xml template
        /// </summary>
        private string CreateBodForAgreement(string agreementNumbersOnly, string agreementNumber, string agreementLineId, string itemNumber)
        {
            var lineNumber = agreementLineId.Split('-').Last();
            
            var doc = new System.Xml.XmlDocument();
            doc.LoadXml(AgreementLineBOD.Sync);
            
            var nsmgr = new System.Xml.XmlNamespaceManager(doc.NameTable);
            nsmgr.AddNamespace("ns", "http://schema.infor.com/InforOAGIS/2");
            
            void SetNodeValue(string path, string value)
            {
                doc.SelectSingleNode(path, nsmgr)!.InnerText = value;
            }
            
            // Update ApplicationArea with new timestamps and BODID
            SetNodeValue("//ns:ApplicationArea/ns:CreationDateTime", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
            SetNodeValue("//ns:ApplicationArea/ns:BODID", Guid.NewGuid().ToString());
            
            // Update agreement identifiers
            SetNodeValue("//ns:AGKRentalOrderLine/ns:agreementNumber", agreementNumber);
            SetNodeValue("//ns:AGKRentalOrderLine/ns:agreementNumberId", agreementNumbersOnly);
            SetNodeValue("//ns:AGKRentalOrderLine/ns:agreementLineNumber", lineNumber);
            SetNodeValue("//ns:AGKRentalOrderLine/ns:agreementLineId", agreementLineId);
            
            // Update item number
            SetNodeValue("//ns:AgreementLines/ns:itemNumber", itemNumber);

            // Update package group number
            SetNodeValue("//ns:AgreementLines/ns:packageGroupNumber", $"PKG{lineNumber}");
            
            // Update correlation ID
            SetNodeValue("//ns:AgreementLines/ns:correlationId", Guid.NewGuid().ToString());
            
            return doc.OuterXml;
        }
    }
}
