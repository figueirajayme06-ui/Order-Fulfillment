using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OF.Api.UseCases.Agreements;
using OF.Common.Infrastructure.CloudSuite.Models.Agreements.DataLake;
using OF.Common.Infrastructure.IPG.Orders;
using OF.Common.Infrastructure.IPG.Orders.Models.Quotes;
using OF.Common.Infrastructure.IPG.Orders.Models.Quotes.Response;
using OF.Common.Infrastructure.IPG.Orders.Models.RAA.Agreementrs;
using OF.Common.Infrastructure.OF;
using OF.Data;
using OF.Data.Database;
using OF.Tests.Common;
using OF.Tests.Data.Test;
using OF.Tests.Data.Test.BODs.Agreements;

namespace OF.Tests.Api.UseCases.Agreements
{
    [Collection("DatabaseCollection")]
    public class AgreementReceivedHandlerTests : CommonDBTest
    {
        public AgreementReceivedHandlerTests(DatabaseFixture databaseFixture) 
            : base(databaseFixture)
        {
        }

        [SkippableFact]
        public async Task New_Header_Doesnt_Exist_In_Database()
        {
            var dbContextFactory = await ArrangeAndAct(nameof(New_Header_Doesnt_Exist_In_Database));
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            var mockLogger = new Mock<ILogger>();
            var mockLineFetcher = new Mock<IAgreementLineFetcher>();
            mockLineFetcher.Setup(x => x.FetchLinesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<IONAgreementLineData>());
            var fulfilmentEngine = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));

            var sut = new AgreementReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngine, mockLineFetcher.Object, mockLogger.Object);

            await sut.Handle(AgreementBOD.Sync);

            var headerCount = await dbContext.Headers.CountAsync();
            var header = await dbContext.Headers.FirstAsync();

            Assert.Equal(1, headerCount);
            Assert.Equal("BD1", header.RentalDepot);
        }

        [SkippableFact]
        public async Task Existing_Header_Exists_In_Database()
        {
            var dbContextFactory = await ArrangeAndAct(nameof(Existing_Header_Exists_In_Database), TestData.Agreements.SetupHeaderWithItemAndDeletedItem);
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            mockOmService.Setup(i => i.Query<Order>(It.IsAny<string>())).ReturnsAsync(new SOQLResponse<Order>()
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

            var mockLogger = new Mock<ILogger>();
            var mockLineFetcher = new Mock<IAgreementLineFetcher>();
            mockLineFetcher.Setup(x => x.FetchLinesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<IONAgreementLineData>());
            var fulfilmentEngione = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));

            var sut = new AgreementReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngione, mockLineFetcher.Object, mockLogger.Object);

            await sut.Handle(AgreementBOD.Sync);

            var headerCount = await dbContext.Headers.CountAsync();
            Assert.Equal(1, headerCount);
        }

        [SkippableFact]
        public async Task Existing_Header_Address_Doesnt_Update()
        {
            var dbContextFactory = await ArrangeAndAct(nameof(Existing_Header_Address_Doesnt_Update), TestData.Agreements.SetupHeaderWithItemAndDeletedItem);
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            mockOmService.Setup(i => i.Query<Order>(It.IsAny<string>())).ReturnsAsync(new SOQLResponse<Order>()
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

            var mockLogger = new Mock<ILogger>();
            var mockLineFetcher = new Mock<IAgreementLineFetcher>();
            mockLineFetcher.Setup(x => x.FetchLinesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<IONAgreementLineData>());
            var fulfilmentEngione = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));

            var sut = new AgreementReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngione, mockLineFetcher.Object, mockLogger.Object);

            await sut.Handle(AgreementBOD.Sync);

            var header = await dbContext.Headers.SingleAsync();

            Assert.NotNull(header);
            Assert.Equal("900009", header.CustomerAddressCode);
            Assert.Equal("Fake place", header.CustomerAddress);
        }

        [SkippableFact]
        public async Task Header_Not_Exists_With_Fulfilled_Lines_Goes_Fulfilled()
        {
            var dbContextFactory = await ArrangeAndAct(nameof(Header_Not_Exists_With_Fulfilled_Lines_Goes_Fulfilled), TestData.Agreements.HeaderNotExistsWithFullyFulfilledLinesGoesFulfilled);
            var dbContext = await dbContextFactory.CreateDbContextAsync();
            var mockOmService = new Mock<IOrderManagementIntegration>();
            mockOmService.Setup(i => i.Query<Order>(It.IsAny<string>())).ReturnsAsync(new SOQLResponse<Order>()
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

            var mockLogger = new Mock<ILogger>();
            var mockLineFetcher = new Mock<IAgreementLineFetcher>();
            mockLineFetcher.Setup(x => x.FetchLinesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<IONAgreementLineData>());
            var fulfilmentEngione = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var sut = new AgreementReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngione, mockLineFetcher.Object, mockLogger.Object);

            await sut.Handle(AgreementBOD.Sync);

            var header = await dbContext.Headers.Include(i => i.Lines).SingleAsync();

            Assert.NotNull(header);
            Assert.Equal(2, header.Lines.Count);
        }

        [SkippableFact]
        public async Task Should_Only_Link_Orphaned_Lines_Not_Already_Assigned_To_Different_Header()
        {
            var dbContextFactory = await ArrangeAndAct((ApplicationDbContext dbContext) =>
            {
                // Create header with different AgreementNumbersOnly to avoid update conflict
                var existingHeader = new Header
                {
                    AgreementNumber = "A712566",
                    AgreementNumbersOnly = "712566",
                    CustomerNumber = "US00103536",
                    CustomerAddressCode = "900001"
                };
                dbContext.Headers.Add(existingHeader);
                dbContext.SaveChanges();

                // Create line already assigned to the different header, but with AgreementNumbersOnly matching BOD
                var assignedLine = new Line
                {
                    HeaderId = existingHeader.Id,
                    AgreementLineNumber = "A712565-99",
                    AgreementNumbersOnly = "712565",
                    ItemNumber = "ASSIGNED_ITEM",
                    Division = "200",
                    Facility = "USG",
                    RequiresFulfilment = true
                };
                dbContext.Lines.Add(assignedLine);

                // Create orphaned line (no HeaderId) with AgreementNumbersOnly matching BOD
                var orphanedLine = new Line
                {
                    HeaderId = null,
                    AgreementLineNumber = "A712565-100",
                    AgreementNumbersOnly = "712565",
                    ItemNumber = "ORPHANED_ITEM",
                    Division = "200",
                    Facility = "USG",
                    RequiresFulfilment = true
                };
                dbContext.Lines.Add(orphanedLine);
                dbContext.SaveChanges();

                return existingHeader;
            });

            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            var mockLogger = new Mock<ILogger>();
            var mockLineFetcher = new Mock<IAgreementLineFetcher>();
            mockLineFetcher.Setup(x => x.FetchLinesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<IONAgreementLineData>());
            var fulfilmentEngine = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));

            var sut = new AgreementReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngine, mockLineFetcher.Object, mockLogger.Object);

            // Act - handle BOD which will create new header with AgreementNumbersOnly=712565
            await sut.Handle(AgreementBOD.Sync);

            // Assert
            var headers = await dbContext.Headers.Include(h => h.Lines).ToListAsync();
            Assert.Equal(2, headers.Count); // Original header (712566) + new BOD header (712565)

            var newHeader = await dbContext.Headers.FirstOrDefaultAsync(h => h.AgreementNumbersOnly == "712565");
            var originalHeader = await dbContext.Headers.FirstOrDefaultAsync(h => h.AgreementNumbersOnly == "712566");

            Assert.NotNull(newHeader);
            Assert.NotNull(originalHeader);

            // The orphaned line should be linked to the new header (712565)
            var orphanedLine = await dbContext.Lines.FirstOrDefaultAsync(l => l.ItemNumber == "ORPHANED_ITEM");
            Assert.NotNull(orphanedLine);
            Assert.Equal(newHeader.Id, orphanedLine.HeaderId);

            // The previously assigned line should remain with the original header (712566)
            var assignedLine = await dbContext.Lines.FirstOrDefaultAsync(l => l.ItemNumber == "ASSIGNED_ITEM");
            Assert.NotNull(assignedLine);
            Assert.Equal(originalHeader.Id, assignedLine.HeaderId);
        }

        [SkippableFact]
        public async Task Handle_WhenM3ReturnsLines_LinesCreatedInDatabase()
        {
            var dbContextFactory = await ArrangeAndAct(nameof(Handle_WhenM3ReturnsLines_LinesCreatedInDatabase));
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            var mockLogger = new Mock<ILogger>();

            var m3Lines = new List<IONAgreementLineData>
            {
                new IONAgreementLineData
                {
                    AgreementNumber = "A712565",
                    LineNumber = 1,
                    Division = "200",
                    Facility = "BD1",
                    ItemNumber = "GEN001",
                    LineType = "01",
                    AgreementLineStatus = "20",
                    OrderedQuantity = 1,
                    AgreementFromDate = "20240101",
                    FromWarehouse = "BD1"
                },
                new IONAgreementLineData
                {
                    AgreementNumber = "A712565",
                    LineNumber = 2,
                    Division = "200",
                    Facility = "BD1",
                    ItemNumber = "GEN002",
                    LineType = "01",
                    AgreementLineStatus = "20",
                    OrderedQuantity = 2,
                    AgreementFromDate = "20240101",
                    FromWarehouse = "BD1"
                }
            };

            var mockLineFetcher = new Mock<IAgreementLineFetcher>();
            mockLineFetcher
                .Setup(x => x.FetchLinesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(m3Lines);

            var fulfilmentEngine = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var sut = new AgreementReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngine, mockLineFetcher.Object, mockLogger.Object);

            await sut.Handle(AgreementBOD.Sync);

            var header = await dbContext.Headers.Include(h => h.Lines).FirstAsync(h => h.AgreementNumbersOnly == "712565");

            Assert.NotNull(header);
            Assert.Equal(2, header.Lines.Count);
            Assert.Contains(header.Lines, l => l.ItemNumber == "GEN001");
            Assert.Contains(header.Lines, l => l.ItemNumber == "GEN002");
        }

        [SkippableFact]
        public async Task Handle_WhenM3ReturnsEmpty_NoLinesCreated()
        {
            var dbContextFactory = await ArrangeAndAct(nameof(Handle_WhenM3ReturnsEmpty_NoLinesCreated));
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            var mockLogger = new Mock<ILogger>();
            var mockLineFetcher = new Mock<IAgreementLineFetcher>();
            mockLineFetcher
                .Setup(x => x.FetchLinesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<IONAgreementLineData>());

            var fulfilmentEngine = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var sut = new AgreementReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngine, mockLineFetcher.Object, mockLogger.Object);

            await sut.Handle(AgreementBOD.Sync);

            var lineCount = await dbContext.Lines.CountAsync();
            Assert.Equal(0, lineCount);
        }

        [SkippableFact]
        public async Task Handle_WhenM3Throws_HeaderHandlingSucceeds()
        {
            var dbContextFactory = await ArrangeAndAct(nameof(Handle_WhenM3Throws_HeaderHandlingSucceeds));
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            var mockLogger = new Mock<ILogger>();
            var mockLineFetcher = new Mock<IAgreementLineFetcher>();
            mockLineFetcher
                .Setup(x => x.FetchLinesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("M3 API unavailable"));

            var fulfilmentEngine = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var sut = new AgreementReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngine, mockLineFetcher.Object, mockLogger.Object);

            await sut.Handle(AgreementBOD.Sync);

            var headerCount = await dbContext.Headers.CountAsync();
            Assert.Equal(1, headerCount);
        }

        [SkippableFact]
        public async Task Handle_WhenM3ReturnsExcludedLines_ExcludedLinesFiltered()
        {
            var dbContextFactory = await ArrangeAndAct(nameof(Handle_WhenM3ReturnsExcludedLines_ExcludedLinesFiltered));
            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            var mockLogger = new Mock<ILogger>();

            var m3Lines = new List<IONAgreementLineData>
            {
                new IONAgreementLineData
                {
                    AgreementNumber = "A712565",
                    LineNumber = 1,
                    Division = "200",
                    Facility = "BD1",
                    ItemNumber = "GEN001",
                    LineType = "01",
                    AgreementLineStatus = "20",
                    OrderedQuantity = 1,
                    AgreementFromDate = "20240101",
                    FromWarehouse = "BD1"
                },
                new IONAgreementLineData
                {
                    AgreementNumber = "A712565",
                    LineNumber = 2,
                    Division = "200",
                    Facility = "BD1",
                    ItemNumber = "FSLLABOUR001",
                    LineType = "01",
                    AgreementLineStatus = "20",
                    OrderedQuantity = 1,
                    AgreementFromDate = "20240101",
                    FromWarehouse = "BD1"
                },
                new IONAgreementLineData
                {
                    AgreementNumber = "A712565",
                    LineNumber = 3,
                    Division = "200",
                    Facility = "BD1",
                    ItemNumber = "XXMISC001",
                    LineType = "01",
                    AgreementLineStatus = "20",
                    OrderedQuantity = 1,
                    AgreementFromDate = "20240101",
                    FromWarehouse = "BD1"
                }
            };

            var mockLineFetcher = new Mock<IAgreementLineFetcher>();
            mockLineFetcher
                .Setup(x => x.FetchLinesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(m3Lines);

            var fulfilmentEngine = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));
            var sut = new AgreementReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngine, mockLineFetcher.Object, mockLogger.Object);

            await sut.Handle(AgreementBOD.Sync);

            var header = await dbContext.Headers.Include(h => h.Lines).FirstAsync(h => h.AgreementNumbersOnly == "712565");

            Assert.NotNull(header);
            // GEN001 and XXMISC001 should be included, FSLLABOUR001 should be excluded
            Assert.Equal(2, header.Lines.Count);
            Assert.Contains(header.Lines, l => l.ItemNumber == "GEN001");
            Assert.Contains(header.Lines, l => l.ItemNumber == "XXMISC001");
            Assert.DoesNotContain(header.Lines, l => l.ItemNumber == "FSLLABOUR001");
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
                    CustomerAddressCode = "900009",
                    Probability = 50
                };
                dbContext.Headers.Add(header);
                dbContext.SaveChanges();
                return header;
            });

            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            mockOmService.Setup(i => i.Query<Order>(It.IsAny<string>())).ReturnsAsync(new SOQLResponse<Order>()
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

            var mockLogger = new Mock<ILogger>();
            var mockLineFetcher = new Mock<IAgreementLineFetcher>();
            mockLineFetcher.Setup(x => x.FetchLinesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<IONAgreementLineData>());
            var fulfilmentEngine = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));

            var sut = new AgreementReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngine, mockLineFetcher.Object, mockLogger.Object);

            await sut.Handle(AgreementBOD.Sync);

            var header = await dbContext.Headers.SingleAsync();

            Assert.NotNull(header);
            Assert.Equal(75.5, header.Probability);
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
                    CustomerAddressCode = "900009",
                    Probability = 50
                };
                dbContext.Headers.Add(header);
                dbContext.SaveChanges();
                return header;
            });

            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            mockOmService.Setup(i => i.Query<Order>(It.IsAny<string>())).ReturnsAsync(new SOQLResponse<Order>()
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
                                OpportunityStageName = "Proposal"
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

            var mockLogger = new Mock<ILogger>();
            var mockLineFetcher = new Mock<IAgreementLineFetcher>();
            mockLineFetcher.Setup(x => x.FetchLinesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<IONAgreementLineData>());
            var fulfilmentEngine = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));

            var sut = new AgreementReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngine, mockLineFetcher.Object, mockLogger.Object);

            await sut.Handle(AgreementBOD.Sync);

            var header = await dbContext.Headers.SingleAsync();

            Assert.NotNull(header);
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
                    CustomerAddressCode = "900009",
                    Probability = 50
                };
                dbContext.Headers.Add(header);
                dbContext.SaveChanges();
                return header;
            });

            var dbContext = await dbContextFactory.CreateDbContextAsync();

            var mockOmService = new Mock<IOrderManagementIntegration>();
            mockOmService.Setup(i => i.Query<Order>(It.IsAny<string>())).ReturnsAsync(new SOQLResponse<Order>()
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
                                OpportunityStageName = "Proposal"
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

            var mockLogger = new Mock<ILogger>();
            var mockLineFetcher = new Mock<IAgreementLineFetcher>();
            mockLineFetcher.Setup(x => x.FetchLinesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<IONAgreementLineData>());
            var fulfilmentEngine = new CoreFulfilmentEngine(new CoreDataRepository(dbContext));

            var sut = new AgreementReceivedHandler(mockOmService.Object, dbContext, fulfilmentEngine, mockLineFetcher.Object, mockLogger.Object);

            await sut.Handle(AgreementBOD.Sync);

            var header = await dbContext.Headers.SingleAsync();

            Assert.NotNull(header);
            Assert.Equal(50, header.Probability);
        }
    }
}
