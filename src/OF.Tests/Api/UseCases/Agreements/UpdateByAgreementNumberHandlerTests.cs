using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OF.Api.UseCases.Agreements;
using OF.Common.Infrastructure.CloudSuite;
using OF.Common.Infrastructure.CloudSuite.Models.Agreements.DataLake;
using OF.Common.Infrastructure.IPG.Orders;
using OF.Common.Infrastructure.IPG.Orders.Models.Quotes;
using OF.Common.Infrastructure.IPG.Orders.Models.Quotes.Response;
using OF.Common.Infrastructure.IPG.Orders.Models.RAA.Agreementrs;
using OF.Common.Infrastructure.OF;
using OF.Data;
using OF.Data.Database;
using OF.Tests.Common;
using static OF.Common.Enums;

namespace OF.Tests.Api.UseCases.Agreements
{
    [Collection("DatabaseCollection")]
    public class UpdateByAgreementNumberHandlerTests : CommonDBTest
    {
        public UpdateByAgreementNumberHandlerTests(DatabaseFixture databaseFixture)
            : base(databaseFixture)
        {
        }

        [SkippableFact]
        public async Task Should_Only_Attach_Orphaned_Lines_When_Matching_By_AgreementLineNumber()
        {
            var dbContextFactory = await ArrangeAndAct((ApplicationDbContext dbContext) =>
            {
                // Create target header
                var targetHeader = new Header
                {
                    AgreementNumber = "A712565",
                    AgreementNumbersOnly = "712565",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000",
                    Division = "200",
                    Facility = "USG",
                    OrderSource = "CPQ",
                    FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled
                };
                dbContext.Headers.Add(targetHeader);
                dbContext.SaveChanges();

                // Create another header with different AgreementNumbersOnly
                var otherHeader = new Header
                {
                    AgreementNumber = "A712566",
                    AgreementNumbersOnly = "712566",
                    CustomerNumber = "US00103536",
                    CustomerAddressCode = "900001"
                };
                dbContext.Headers.Add(otherHeader);
                dbContext.SaveChanges();

                // Create line already assigned to different header
                var assignedLine = new Line
                {
                    HeaderId = otherHeader.Id,
                    AgreementLineNumber = "A712565-1",
                    AgreementNumbersOnly = "712565",
                    AgreementLineIndex = 1,
                    ItemNumber = "ALREADY_ASSIGNED",
                    Division = "200",
                    Facility = "USG",
                    RequiresFulfilment = true
                };
                dbContext.Lines.Add(assignedLine);

                // Create orphaned line with same AgreementLineNumber pattern
                var orphanedLine = new Line
                {
                    HeaderId = null,
                    AgreementLineNumber = "A712565-2",
                    AgreementNumbersOnly = "712565",
                    AgreementLineIndex = 2,
                    ItemNumber = "ORPHANED_LINE",
                    Division = "200",
                    Facility = "USG",
                    RequiresFulfilment = true
                };
                dbContext.Lines.Add(orphanedLine);
                dbContext.SaveChanges();

                return targetHeader;
            });

            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockCloudSuiteService = new Mock<ICloudSuiteService>();
            var mockSalesforceService = new Mock<IOrderManagementIntegration>();
            var mockLogger = new Mock<ILogger>();
            var fulfilmentEngine = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));

            // Mock CloudSuite to return header data
            // Note: Agreement must include the A/T prefix because ToHeaderEntity() calls Substring(1)
            mockCloudSuiteService
                .Setup(x => x.RunDataLakeQuery<IONAgreementData>(It.IsAny<string>()))
                .ReturnsAsync(new List<IONAgreementData>
                {
                    new IONAgreementData
                    {
                        Agreement = "A712565",
                        CustomerAccount = "US00103535",
                        Division = "200",
                        Facility = "USG",
                        Warehouse = "USG",
                        AgreementHighestStatus = "20",
                        CustomerName = "Test Customer"
                    }
                });

            // Mock CloudSuite to return line data
            mockCloudSuiteService
                .Setup(x => x.RunDataLakeQuery<IONAgreementLineData>(It.IsAny<string>()))
                .ReturnsAsync(new List<IONAgreementLineData>
                {
                    new IONAgreementLineData
                    {
                        AgreementNumber = "A712565",
                        LineNumber = 2,
                        ItemNumber = "ORPHANED_LINE",
                        GenericItem = "ORPHANED_LINE",
                        Division = "200",
                        Facility = "USG",
                        LineType = "5",
                        AgreementLineStatus = "20",
                        FromWarehouse = "USG",
                        OrderedQuantity = 1,
                        AgreementFromDate = DateTime.Now.ToString("yyyyMMdd"),
                        AgreementToDate = DateTime.Now.AddDays(30).ToString("yyyyMMdd")
                    }
                });

            var sut = new UpdateByAgreementNumberHandler(
                mockCloudSuiteService.Object,
                mockSalesforceService.Object,
                dbContext,
                fulfilmentEngine,
                mockLogger.Object);

            // Act
            var request = new UpdateByAgreementNumberRequest { AgreementNumber = "A712565" };
            await sut.Handle(Newtonsoft.Json.JsonConvert.SerializeObject(request));

            // Assert
            var targetHeader = await dbContext.Headers
                .Include(h => h.Lines)
                .FirstAsync(h => h.AgreementNumbersOnly == "712565");

            var otherHeader = await dbContext.Headers
                .Include(h => h.Lines)
                .FirstAsync(h => h.AgreementNumbersOnly == "712566");

            // Orphaned line should now be attached to target header
            var orphanedLine = await dbContext.Lines.FirstAsync(l => l.ItemNumber == "ORPHANED_LINE");
            Assert.Equal(targetHeader.Id, orphanedLine.HeaderId);
            Assert.Contains(targetHeader.Lines, l => l.ItemNumber == "ORPHANED_LINE");

            // Previously assigned line should remain with its original header
            var assignedLine = await dbContext.Lines.FirstAsync(l => l.ItemNumber == "ALREADY_ASSIGNED");
            Assert.Equal(otherHeader.Id, assignedLine.HeaderId);
            Assert.DoesNotContain(targetHeader.Lines, l => l.ItemNumber == "ALREADY_ASSIGNED");
        }

        [SkippableFact]
        public async Task Handle_WhenEffectiveProbabilityIsSet_ProbabilityIsUpdated()
        {
            var dbContextFactory = await ArrangeAndAct((ApplicationDbContext dbContext) =>
            {
                var header = new Header
                {
                    AgreementNumber = "A712565",
                    AgreementNumbersOnly = "712565",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000",
                    Division = "200",
                    Facility = "USG",
                    OrderSource = "CPQ",
                    Probability = 50,
                    OpportunityStage = "Proposal"
                };
                dbContext.Headers.Add(header);
                dbContext.SaveChanges();
                return header;
            });

            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockCloudSuiteService = new Mock<ICloudSuiteService>();
            var mockSalesforceService = new Mock<IOrderManagementIntegration>();
            var mockLogger = new Mock<ILogger>();
            var fulfilmentEngine = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));

            mockCloudSuiteService
                .Setup(x => x.RunDataLakeQuery<IONAgreementData>(It.IsAny<string>()))
                .ReturnsAsync(new List<IONAgreementData>
                {
                    new IONAgreementData
                    {
                        Agreement = "A712565",
                        CustomerAccount = "US00103535",
                        Division = "200",
                        Facility = "USG",
                        Warehouse = "USG",
                        AgreementHighestStatus = "20",
                        CustomerName = "Test Customer",
                        ProposalNumber = "Q-438254"
                    }
                });

            mockCloudSuiteService
                .Setup(x => x.RunDataLakeQuery<IONAgreementLineData>(It.IsAny<string>()))
                .ReturnsAsync(new List<IONAgreementLineData>());

            mockSalesforceService.Setup(i => i.Query<Order>(It.IsAny<string>())).ReturnsAsync(new SOQLResponse<Order>()
            {
                Records = new List<Order>()
                {
                    new Order()
                    {
                        QuoteId = "Q017a000003GAerrBBG",
                        Quote = new OrderQuote()
                        {
                            Name = "Q-438254",
                            Contact = new OpportunityContact()
                            {
                                Name = "Michael Law",
                                Email = "michael.law@aggreko.com",
                                Phone = "012345678790"
                            },
                            OpportunityId = "O017a000003GAerrCCG",
                            OverviewOfServices = "Overview",
                            Opportunity = new OrderQuoteOpportunity()
                            {
                                EffectiveProbability = 75.5,
                                Probability = "75.5",
                                Name = "Test Opportunity",
                                OpportunityStageName = "Closed Won"
                            }
                        },
                        Id = "8017a000003GAerAAG",
                        OnHireDate = DateTime.Now.AddDays(1),
                        OffHireDate = DateTime.Now.AddDays(2),
                        ShippingAddress = "! Fake Place"
                    }
                },
                TotalSize = 1,
                Done = true
            });

            var sut = new UpdateByAgreementNumberHandler(
                mockCloudSuiteService.Object,
                mockSalesforceService.Object,
                dbContext,
                fulfilmentEngine,
                mockLogger.Object);

            var request = new UpdateByAgreementNumberRequest { AgreementNumber = "A712565" };
            await sut.Handle(Newtonsoft.Json.JsonConvert.SerializeObject(request));

            var header = await dbContext.Headers.FirstAsync(h => h.AgreementNumbersOnly == "712565");

            Assert.Equal(75.5, header.Probability);
            Assert.Equal("Closed Won", header.OpportunityStage);
        }

        [SkippableFact]
        public async Task Handle_WhenEffectiveProbabilityIsNull_FallsBackToProbabilityString()
        {
            var dbContextFactory = await ArrangeAndAct((ApplicationDbContext dbContext) =>
            {
                var header = new Header
                {
                    AgreementNumber = "A712565",
                    AgreementNumbersOnly = "712565",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000",
                    Division = "200",
                    Facility = "USG",
                    OrderSource = "CPQ",
                    Probability = 50,
                    OpportunityStage = "Proposal"
                };
                dbContext.Headers.Add(header);
                dbContext.SaveChanges();
                return header;
            });

            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockCloudSuiteService = new Mock<ICloudSuiteService>();
            var mockSalesforceService = new Mock<IOrderManagementIntegration>();
            var mockLogger = new Mock<ILogger>();
            var fulfilmentEngine = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));

            mockCloudSuiteService
                .Setup(x => x.RunDataLakeQuery<IONAgreementData>(It.IsAny<string>()))
                .ReturnsAsync(new List<IONAgreementData>
                {
                    new IONAgreementData
                    {
                        Agreement = "A712565",
                        CustomerAccount = "US00103535",
                        Division = "200",
                        Facility = "USG",
                        Warehouse = "USG",
                        AgreementHighestStatus = "20",
                        CustomerName = "Test Customer",
                        ProposalNumber = "Q-438254"
                    }
                });

            mockCloudSuiteService
                .Setup(x => x.RunDataLakeQuery<IONAgreementLineData>(It.IsAny<string>()))
                .ReturnsAsync(new List<IONAgreementLineData>());

            mockSalesforceService.Setup(i => i.Query<Order>(It.IsAny<string>())).ReturnsAsync(new SOQLResponse<Order>()
            {
                Records = new List<Order>()
                {
                    new Order()
                    {
                        QuoteId = "Q017a000003GAerrBBG",
                        Quote = new OrderQuote()
                        {
                            Name = "Q-438254",
                            Contact = new OpportunityContact()
                            {
                                Name = "Michael Law",
                                Email = "michael.law@aggreko.com",
                                Phone = "012345678790"
                            },
                            OpportunityId = "O017a000003GAerrCCG",
                            OverviewOfServices = "Overview",
                            Opportunity = new OrderQuoteOpportunity()
                            {
                                EffectiveProbability = null,
                                Probability = "85",
                                Name = "Test Opportunity",
                                OpportunityStageName = "Negotiation"
                            }
                        },
                        Id = "8017a000003GAerAAG",
                        OnHireDate = DateTime.Now.AddDays(1),
                        OffHireDate = DateTime.Now.AddDays(2),
                        ShippingAddress = "! Fake Place"
                    }
                },
                TotalSize = 1,
                Done = true
            });

            var sut = new UpdateByAgreementNumberHandler(
                mockCloudSuiteService.Object,
                mockSalesforceService.Object,
                dbContext,
                fulfilmentEngine,
                mockLogger.Object);

            var request = new UpdateByAgreementNumberRequest { AgreementNumber = "A712565" };
            await sut.Handle(Newtonsoft.Json.JsonConvert.SerializeObject(request));

            var header = await dbContext.Headers.FirstAsync(h => h.AgreementNumbersOnly == "712565");

            Assert.Equal(85, header.Probability);
        }

        [SkippableFact]
        public async Task Handle_WhenBothProbabilitiesNull_ExistingProbabilityPreserved()
        {
            var dbContextFactory = await ArrangeAndAct((ApplicationDbContext dbContext) =>
            {
                var header = new Header
                {
                    AgreementNumber = "A712565",
                    AgreementNumbersOnly = "712565",
                    CustomerNumber = "US00103535",
                    CustomerAddressCode = "900000",
                    Division = "200",
                    Facility = "USG",
                    OrderSource = "CPQ",
                    Probability = 50,
                    OpportunityStage = "Proposal"
                };
                dbContext.Headers.Add(header);
                dbContext.SaveChanges();
                return header;
            });

            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockCloudSuiteService = new Mock<ICloudSuiteService>();
            var mockSalesforceService = new Mock<IOrderManagementIntegration>();
            var mockLogger = new Mock<ILogger>();
            var fulfilmentEngine = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));

            mockCloudSuiteService
                .Setup(x => x.RunDataLakeQuery<IONAgreementData>(It.IsAny<string>()))
                .ReturnsAsync(new List<IONAgreementData>
                {
                    new IONAgreementData
                    {
                        Agreement = "A712565",
                        CustomerAccount = "US00103535",
                        Division = "200",
                        Facility = "USG",
                        Warehouse = "USG",
                        AgreementHighestStatus = "20",
                        CustomerName = "Test Customer",
                        ProposalNumber = "Q-438254"
                    }
                });

            mockCloudSuiteService
                .Setup(x => x.RunDataLakeQuery<IONAgreementLineData>(It.IsAny<string>()))
                .ReturnsAsync(new List<IONAgreementLineData>());

            mockSalesforceService.Setup(i => i.Query<Order>(It.IsAny<string>())).ReturnsAsync(new SOQLResponse<Order>()
            {
                Records = new List<Order>()
                {
                    new Order()
                    {
                        QuoteId = "Q017a000003GAerrBBG",
                        Quote = new OrderQuote()
                        {
                            Name = "Q-438254",
                            Contact = new OpportunityContact()
                            {
                                Name = "Michael Law",
                                Email = "michael.law@aggreko.com",
                                Phone = "012345678790"
                            },
                            OpportunityId = "O017a000003GAerrCCG",
                            OverviewOfServices = "Overview",
                            Opportunity = new OrderQuoteOpportunity()
                            {
                                EffectiveProbability = null,
                                Probability = null,
                                Name = "Test Opportunity",
                                OpportunityStageName = null
                            }
                        },
                        Id = "8017a000003GAerAAG",
                        OnHireDate = DateTime.Now.AddDays(1),
                        OffHireDate = DateTime.Now.AddDays(2),
                        ShippingAddress = "! Fake Place"
                    }
                },
                TotalSize = 1,
                Done = true
            });

            var sut = new UpdateByAgreementNumberHandler(
                mockCloudSuiteService.Object,
                mockSalesforceService.Object,
                dbContext,
                fulfilmentEngine,
                mockLogger.Object);

            var request = new UpdateByAgreementNumberRequest { AgreementNumber = "A712565" };
            await sut.Handle(Newtonsoft.Json.JsonConvert.SerializeObject(request));

            var header = await dbContext.Headers.FirstAsync(h => h.AgreementNumbersOnly == "712565");

            Assert.Equal(50, header.Probability);
            Assert.Equal("Proposal", header.OpportunityStage);
        }
    }
}
