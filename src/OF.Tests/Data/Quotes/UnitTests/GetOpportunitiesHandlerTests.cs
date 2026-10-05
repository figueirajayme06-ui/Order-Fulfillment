using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using OF.Common.Infrastructure.IPG.Orders;
using OF.Common.Infrastructure.IPG.Orders.Models.Quotes;
using OF.Common.Infrastructure.IPG.Orders.Models.Quotes.Response;
using OF.Common.UseCases.Quotes;
using OF.Data.Database;
using Xunit.Abstractions;
using static OF.Common.Constants;

namespace OF.Tests.Data.Quotes.UnitTests;

public class GetOpportunitiesHandlerTests
{
    private readonly Mock<IOrderManagementIntegration> _mockSalesforceService;
    private readonly ITestOutputHelper _output;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    public GetOpportunitiesHandlerTests(ITestOutputHelper output)
    {
        _mockSalesforceService = new Mock<IOrderManagementIntegration>();
        _output = output;
    }

    [Theory]
    [InlineData(OpportunityStage.Develop, 1)]
    [InlineData(OpportunityStage.Build, 1)]
    [InlineData(OpportunityStage.Negotiate, 1)]
    [InlineData(OpportunityStage.ClosedLost, 1)]
    [InlineData(OpportunityStage.ClosedWon, 0)]
    [InlineData(OpportunityStage.NotAccepted, 1)]
    public async Task ClosedOpportunitiesCorrectlyFilteredOut(string stageName, int expectedOpportunityCount)
    {
        using (var context = new InMemoryDBContext())
        {
            // Arrange
            Fixture fixture = new();
            SetupSalesforceOpportunityResponse(
                fixture
                    .Build<Opportunity>()
                    .With(x => x.EffectiveProbability, 90)
                    .With(x => x.StageName, stageName)
                    .Create());

            GetOpportunitiesHandler sut = new(_mockSalesforceService.Object, context, Mock.Of<ILogger>());

            // Act
            IList<Opportunity>? result = await sut.Handle(string.Empty, 90);

            // Assert
            result.Should().NotBeNull();
            result!.Count.Should().Be(expectedOpportunityCount);
        }
    }

    [Theory]
    [InlineData(OpportunityStage.Develop, 0)]
    [InlineData(OpportunityStage.Build, 0)]
    [InlineData(OpportunityStage.Negotiate, 0)]
    public async Task LowProbabilityOpportunitiesCorrectlyFilteredOut(string stageName, int expectedOpportunityCount)
    {
        using (var context = new InMemoryDBContext())
        {
            // Arrange
            Fixture fixture = new();
            fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList().ForEach(b => fixture.Behaviors.Remove(b));
            fixture.Behaviors.Add(new OmitOnRecursionBehavior());
            SetupSalesforceOpportunityResponse(
                fixture
                    .Build<Opportunity>()
                    .With(x => x.StageName, stageName)
                    .With(x => x.EffectiveProbability, 89)
                    .Create());

            GetOpportunitiesHandler sut = new(_mockSalesforceService.Object, context, Mock.Of<ILogger>());

            // Act
            IList<Opportunity>? result = await sut.Handle(string.Empty, 90);

            // Assert
            result.Should().NotBeNull();
            result!.Count.Should().Be(expectedOpportunityCount);
        }
    }

    [Theory]
    [InlineData(OpportunityStage.ClosedLost, 1)]
    [InlineData(OpportunityStage.NotAccepted, 1)]
    public async Task LowProbabilityClosedLostAndNotAccepted_StillReturned(string stageName, int expectedOpportunityCount)
    {
        using (var context = new InMemoryDBContext())
        {
            // Arrange - even with low probability, Closed Lost and Not Accepted should be returned for deletion marking
            Fixture fixture = new();
            fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList().ForEach(b => fixture.Behaviors.Remove(b));
            fixture.Behaviors.Add(new OmitOnRecursionBehavior());
            SetupSalesforceOpportunityResponse(
                fixture
                    .Build<Opportunity>()
                    .With(x => x.StageName, stageName)
                    .With(x => x.EffectiveProbability, 10)
                    .Create());

            GetOpportunitiesHandler sut = new(_mockSalesforceService.Object, context, Mock.Of<ILogger>());

            // Act
            IList<Opportunity>? result = await sut.Handle(string.Empty, 90);

            // Assert
            result.Should().NotBeNull();
            result!.Count.Should().Be(expectedOpportunityCount);
        }
    }

    [Fact]
    public async Task ClosedWonOpportunity_NeverReturned_RegardlessOfProbability()
    {
        using (var context = new InMemoryDBContext())
        {
            // Arrange
            Fixture fixture = new();
            fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList().ForEach(b => fixture.Behaviors.Remove(b));
            fixture.Behaviors.Add(new OmitOnRecursionBehavior());
            SetupSalesforceOpportunityResponse(
                fixture
                    .Build<Opportunity>()
                    .With(x => x.StageName, OpportunityStage.ClosedWon)
                    .With(x => x.EffectiveProbability, 100)
                    .Create());

            GetOpportunitiesHandler sut = new(_mockSalesforceService.Object, context, Mock.Of<ILogger>());

            // Act
            IList<Opportunity>? result = await sut.Handle(string.Empty, 90);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }
    }

    [Fact(Skip = "ML: This test randomly fails on build server and local, disabling for now until fixed.")]
    public async Task QuoteWithMatchingHeader_DifferentPublicId_AndNotConvertedToAnOrder_ExistingHeaderAndLinesRemoved()
    {
        using (var context = new InMemoryDBContext())
        {
            // Arrange
            Fixture fixture = new();
            fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList().ForEach(b => fixture.Behaviors.Remove(b));
            fixture.Behaviors.Add(new OmitOnRecursionBehavior());
            fixture.Customize<Header>(x => x.Without(xx => xx.Lines));
            fixture.Customize<Header>(x => x.Without(xx => xx.ChangeOrderHeaders));
            fixture.Customize<Header>(x => x.Without(xx => xx.ChangeOrders));
            fixture.Customize<Line>(x => x.Without(xx => xx.Header));
            fixture.Customize<Line>(x => x.Without(xx => xx.ChangeOrderLines));
            Opportunity opp = fixture.Build<Opportunity>().With(x => x.StageName, OpportunityStage.Develop)
                    .With(x => x.EffectiveProbability, 90).Create();
            SetupSalesforceOpportunityResponse(opp);

            int headerId = await context.AddOrderHeader(
                fixture.Build<Header>()
                    .Without(x => x.Lines)
                    .Without(x => x.AgreementNumber)
                    .Without(x => x.ChangeOrders)
                    .Without(x => x.ChangeOrderHeaders)
                    .With(x => x.OpportunityNumber, opp.Id)
                    .With(x => x.QuotePublicId, $"{opp.Quote.Name}1")
                    .Create());

            await context.UpsertHeaderLine(headerId,
                fixture.Build<Line>()
                    .Without(x => x.Header)
                    .Without(x => x.ChangeOrderLines)
                    .With(x => x.QuotePublicId, $"{opp.Quote.Name}1")
                    .Create());

            GetOpportunitiesHandler sut = new(_mockSalesforceService.Object, context, Mock.Of<ILogger>());

            // Act
            IList<Opportunity>? result = await sut.Handle(string.Empty, 90);

            // Assert
            result.Should().NotBeNull();
            result!.Count.Should().Be(1);

            Header? header = await context.Headers.FindAsync(headerId);
            header.Should().BeNull();

            context.Lines.Count(x => x.HeaderId == headerId).Should().Be(0);
        }
    }

    [Fact]
    public async Task QuoteWithMatchingHeader_DifferentPublicId_AndConvertedToAnOrder_QuotePublicIdUpdated()
    {
        using (var context = new InMemoryDBContext())
        {
            // Arrange
            Fixture fixture = new();
            fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList().ForEach(b => fixture.Behaviors.Remove(b));
            fixture.Behaviors.Add(new OmitOnRecursionBehavior());
            var quotePublicId = "QU-12345";
            var quoteId = "00Q123456789ABC";  // Quote record ID
            Opportunity opp = fixture.Build<Opportunity>()
                .With(x => x.StageName, OpportunityStage.Develop)
                .With(x => x.EffectiveProbability, 90)
                .With(x => x.QuoteNumber, quoteId)  // Set the QuoteNumber to the quote's ID
                .With(x => x.Quote, fixture.Build<Quote>()
                    .With(x => x.Id, quoteId)  // Set the Quote's ID
                    .With(x => x.Name, quotePublicId)
                    .Create())
                .Create();

            SetupSalesforceOpportunityResponse(opp);

            var oldQuotePublicId = "QU-54321";
            int headerId = await context.AddOrderHeader(
                fixture.Build<Header>()
                    .Without(x => x.Lines)
                    .With(x => x.AgreementNumber, "A123456")
                    .With(x => x.OpportunityNumber, opp.Id)
                    .With(x => x.QuotePublicId, oldQuotePublicId)
                    .With(x => x.QuotePublicIdNumbersOnly, "54321")
                    .Create());

            var lineId = await context.UpsertHeaderLine(headerId,
                fixture.Build<Line>()
                    .Without(x => x.Header)
                    .With(x => x.AgreementLineNumber, "A123456-1")
                    .With(x => x.QuotePublicId, oldQuotePublicId)
                    .With(x => x.QuotePublicIdNumbersOnly, "54321")
                    .With(x => x.QuoteLineNumber, "QL-1")
                    .With(x => x.IsDeleted, false)
                    .Create());

            var quoteLine = fixture.Build<OpportunityQuoteLine>()
                .With(x => x.Id, "QL-1")
                .With(x => x.LineId, 1.0)
                .Create();
            SetupSalesforceQuoteLineResponse(quoteLine);

            GetOpportunitiesHandler sut = new(_mockSalesforceService.Object, context, Mock.Of<ILogger>());

            // Act
            IList<Opportunity>? result = await sut.Handle(string.Empty, 90);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();

            Header? header = await context.Headers.FindAsync(headerId);
            header.Should().NotBeNull();
            header!.QuotePublicId.Should().Be(quotePublicId);

            var lines = context.Lines.Where(x => x.HeaderId == headerId).ToList();
            lines.Should().HaveCount(1);
            lines[0].QuotePublicId.Should().Be(quotePublicId);
        }
    }

    [Fact]
    public async Task QuoteWithMatchingHeader_SamePublicId_DifferentLastUpdatedDate_AndNotConvertedToAndOrder_QuoteLastModifiedDateUpdated()
    {
        using (var context = new InMemoryDBContext())
        {
            // Arrange
            Fixture fixture = new();
            fixture.Customize<Header>(x => x.Without(xx => xx.ChangeOrderHeaders));
            fixture.Customize<Header>(x => x.Without(xx => xx.ChangeOrders));
            fixture.Customize<Line>(x => x.Without(xx => xx.ChangeOrderLines));
            fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList().ForEach(b => fixture.Behaviors.Remove(b));
            fixture.Behaviors.Add(new OmitOnRecursionBehavior());
            var referenceTime = DateTime.UtcNow;
            var oldUpdateTime = referenceTime.AddDays(-14);
            Opportunity opp = fixture.Build<Opportunity>()
                .With(x => x.StageName, OpportunityStage.Develop)
                .With(x => x.EffectiveProbability, 90)
                .With(x => x.LastModifiedDate, referenceTime)
                .Create();

            int headerId = await context.AddOrderHeader(fixture.Build<Header>()
                .Without(x => x.Lines)
                .Without(x => x.AgreementNumber)
                .Without(x => x.ChangeOrders)
                .Without(x => x.ChangeOrderHeaders)
                .With(x => x.OpportunityNumber, opp.Id)
                .With(x => x.QuotePublicId, opp.Quote.Name)
                .With(x => x.LastUpdatedDate, oldUpdateTime)
                .Create());

            var line = fixture.Build<Line>()
                    .Without(x => x.Header)
                    .Without(x => x.ChangeOrderLines)
                    .With(x => x.QuoteLineNumber, "1234")
                    .With(x => x.QuotePublicId, opp.Quote.Name!)
                    .With(x => x.ValidFromDate, DateTime.Now.AddDays(-14))
                    .With(x => x.ValidToDate, DateTime.Now.AddDays(-7))
                    .With(x => x.LastUpdatedDate, oldUpdateTime)
                    .Create();

            int lineId = await context.UpsertHeaderLine(headerId, line);

            SetupSalesforceOpportunityResponse(opp);
            SetupSalesforceQuoteLineResponse(fixture.Build<OpportunityQuoteLine>()
                .With(x => x.Id, line.QuoteLineNumber)
                .Create());

            GetOpportunitiesHandler sut = new(_mockSalesforceService.Object, context, Mock.Of<ILogger>());

            // Act
            IList<Opportunity>? result = await sut.Handle(string.Empty, 90);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();

            Header? header = await context.Headers.FindAsync(headerId);
            header.Should().NotBeNull();
            header!.LastUpdatedDate.Should().BeAfter(oldUpdateTime);
        }
    }

    [Theory]
    [InlineData(5)]
    [InlineData(0)]
    public async Task QuoteWithMatchingHeader_SamePublicId_DifferentLastUpdatedDate_AndNotConvertedToAndOrder_QuoteLineLastUpdatedDatesUpdated(int reservationCount)
    {
        using (var context = new InMemoryDBContext())
        {
            // Arrange
            Fixture fixture = new();
            fixture.Customize<Header>(x => x.Without(xx => xx.ChangeOrderHeaders));
            fixture.Customize<Header>(x => x.Without(xx => xx.ChangeOrders));
            fixture.Customize<Line>(x => x.Without(xx => xx.ChangeOrderLines));
            fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList().ForEach(b => fixture.Behaviors.Remove(b));
            fixture.Behaviors.Add(new OmitOnRecursionBehavior());
            var referenceTime = DateTime.UtcNow;
            var oldUpdateTime = referenceTime.AddDays(-14);

            Opportunity opp = fixture.Build<Opportunity>()
                .With(x => x.StageName, OpportunityStage.Develop)
                .With(x => x.EffectiveProbability, 90)
                .With(x => x.LastModifiedDate, referenceTime)
                .Create();

            int headerId = await context.AddOrderHeader(fixture.Build<Header>()
                .Without(x => x.Lines)
                .Without(x => x.AgreementNumber)
                .Without(x => x.ChangeOrders)
                .Without(x => x.ChangeOrderHeaders)
                .With(x => x.OpportunityNumber, opp.Id)
                .With(x => x.QuotePublicId, opp.Quote.Name)
                .With(x => x.LastUpdatedDate, oldUpdateTime)
                .Create());

            var line = fixture.Build<Line>()
                    .Without(x => x.Header)
                    .Without(x => x.ChangeOrderLines)
                    .With(x => x.QuoteLineNumber, "1234")
                    .With(x => x.QuotePublicId, opp.Quote.Name!)
                    .With(x => x.ValidFromDate, DateTime.Now.AddDays(-14))
                    .With(x => x.ValidToDate, DateTime.Now.AddDays(-7))
                    .With(x => x.LastUpdatedDate, oldUpdateTime)
                    .Create();

            int lineId = await context.UpsertHeaderLine(headerId, line);

            await context.AddReservationRange(
                fixture.Build<Reservation>()
                    .With(x => x.LineId, lineId)
                    .CreateMany(reservationCount)
                    .ToList());

            SetupSalesforceOpportunityResponse(opp);
            SetupSalesforceQuoteLineResponse(fixture.Build<OpportunityQuoteLine>()
                .With(x => x.Id, line.QuoteLineNumber)
                .With(x => x.LastModified, oldUpdateTime.AddDays(2))
                .Create());

            GetOpportunitiesHandler sut = new(_mockSalesforceService.Object, context, Mock.Of<ILogger>());

            // Act
            IList<Opportunity>? result = await sut.Handle(string.Empty, 90);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();

            Header? header = await context.Headers.FindAsync(headerId);
            header.Should().NotBeNull();
            header!.LastUpdatedDate.Should().BeAfter(oldUpdateTime);

            Line? dbLine = await context.Lines.FindAsync(lineId);
            dbLine.Should().NotBeNull();
            dbLine!.LastUpdatedDate.Should().BeAfter(oldUpdateTime);
        }
    }

    [Fact]
    public async Task ExistingQuote_WhenNewLinesAddedInSalesforce_ShouldAddNewLinesToNOF()
    {
        using (var context = new InMemoryDBContext())
        {
            // Arrange
            Fixture fixture = new();
            fixture.Customize<Header>(x => x.Without(xx => xx.ChangeOrderHeaders));
            fixture.Customize<Header>(x => x.Without(xx => xx.ChangeOrders));
            fixture.Customize<Line>(x => x.Without(xx => xx.ChangeOrderLines));
            fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList().ForEach(b => fixture.Behaviors.Remove(b));
            fixture.Behaviors.Add(new OmitOnRecursionBehavior());
            var referenceTime = DateTime.UtcNow;
            var oldUpdateTime = referenceTime.AddDays(-14);

            Opportunity opp = fixture.Build<Opportunity>()
                .With(x => x.StageName, OpportunityStage.Develop)
                .With(x => x.EffectiveProbability, 90)
                .With(x => x.LastModifiedDate, referenceTime)
                .Create();

            SetupSalesforceOpportunityResponse(opp);

            // Create existing header in NOF
            int headerId = await context.AddOrderHeader(
                fixture.Build<Header>()
                    .Without(x => x.Lines)
                    .Without(x => x.AgreementNumber)
                    .Without(x => x.ChangeOrders)
                    .Without(x => x.ChangeOrderHeaders)
                    .With(x => x.OpportunityNumber, opp.Id)
                    .With(x => x.QuotePublicId, opp.Quote.Name)
                    .With(x => x.LastUpdatedDate, oldUpdateTime)
                    .With(x => x.IsDeleted, false)
                    .Create());

            // Create existing line in NOF
            var existingLineId = await context.UpsertHeaderLine(headerId,
                fixture.Build<Line>()
                    .Without(x => x.Header)
                    .With(x => x.QuotePublicId, opp.Quote.Name)
                    .With(x => x.QuoteLineNumber, "1")
                    .Create());

            // Setup quote lines response with both existing and new lines
            var quoteLines = new List<OpportunityQuoteLine>
            {
                fixture.Build<OpportunityQuoteLine>()
                    .With(x => x.Id, "1")
                    .With(x => x.LineId, 1.0)
                    .Create(),
                fixture.Build<OpportunityQuoteLine>()
                    .With(x => x.Id, "2")
                    .With(x => x.LineId, 2.0)
                    .Create()
            };

            SetupSalesforceMultipleQuoteLinesResponse(quoteLines);

            GetOpportunitiesHandler sut = new(_mockSalesforceService.Object, context, Mock.Of<ILogger>());

            // Act
            IList<Opportunity>? result = await sut.Handle(string.Empty, 90);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();

            var lines = context.Lines.Where(x => x.HeaderId == headerId).ToList();
            lines.Should().HaveCount(2);

            var newLine = lines.FirstOrDefault(l => l.QuoteLineNumber == "2");
            newLine.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task UpdatedQuote_WithNewPublicIdAndNewLines_ShouldUpdateExistingAndAddNewLines()
    {
        using (var context = new InMemoryDBContext())
        {
            // Arrange
            Fixture fixture = new();
            fixture.Customize<Header>(x => x.Without(xx => xx.ChangeOrderHeaders));
            fixture.Customize<Header>(x => x.Without(xx => xx.ChangeOrders));
            fixture.Customize<Line>(x => x.Without(xx => xx.ChangeOrderLines));
            fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList().ForEach(b => fixture.Behaviors.Remove(b));
            fixture.Behaviors.Add(new OmitOnRecursionBehavior());
            var referenceTime = DateTime.UtcNow;
            var oldUpdateTime = referenceTime.AddDays(-14);

            Opportunity opp = fixture.Build<Opportunity>()
                .With(x => x.StageName, OpportunityStage.Develop)
                .With(x => x.EffectiveProbability, 90)
                .With(x => x.LastModifiedDate, referenceTime)
                .Create();

            SetupSalesforceOpportunityResponse(opp);

            // Create existing header in NOF with different QuotePublicId
            int headerId = await context.AddOrderHeader(
                fixture.Build<Header>()
                    .Without(x => x.Lines)
                    .Without(x => x.AgreementNumber)
                    .Without(x => x.ChangeOrders)
                    .Without(x => x.ChangeOrderHeaders)
                    .With(x => x.OpportunityNumber, opp.Id)
                    .With(x => x.QuotePublicId, "Q12345")
                    .With(x => x.LastUpdatedDate, oldUpdateTime)
                    .Create());

            // Create existing line in NOF
            var existingLineId = await context.UpsertHeaderLine(headerId,
                fixture.Build<Line>()
                    .Without(x => x.Header)
                    .With(x => x.QuotePublicId, "Q12345")
                    .With(x => x.QuoteLineNumber, "1")
                    .With(x => x.IsDeleted, false)
                    .Create());

            // Setup quote lines response with both existing and new lines
            var quoteLines = new List<OpportunityQuoteLine>
            {
                fixture.Build<OpportunityQuoteLine>()
                    .With(x => x.Id, "1")// Existing line should be updated
                    .With(x => x.LineId, 1.0)
                    .Create(),
                fixture.Build<OpportunityQuoteLine>()
                    .With(x => x.Id, "2")// New line
                    .With(x => x.LineId, 2.0)
                    .Create()
            };

            SetupSalesforceMultipleQuoteLinesResponse(quoteLines);

            GetOpportunitiesHandler sut = new(_mockSalesforceService.Object, context, Mock.Of<ILogger>());

            // Act
            IList<Opportunity>? result = await sut.Handle(string.Empty, 90);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();

            // Verify header was updated
            Header? header = await context.Headers.FindAsync(headerId);
            header!.QuotePublicId.Should().Be(opp.Quote.Name);

            // Verify existing line updated and new line added
            var lines = context.Lines.Where(x => x.HeaderId == headerId).ToList();
            lines.Should().HaveCount(2);

            lines.All(l => l.QuotePublicId == opp.Quote.Name).Should().BeTrue();
            lines.Any(l => l.QuoteLineNumber == "2" && l.QuotePublicId == opp.Quote.Name).Should().BeTrue();
        }
    }

    [Fact]
    public async Task QuoteLineDeletedInSalesforce_ShouldDeleteAssociatedReservations()
    {
        using (var context = new InMemoryDBContext())
        {
            // Arrange
            Fixture fixture = new();
            fixture.Customize<Header>(x => x.Without(xx => xx.ChangeOrderHeaders));
            fixture.Customize<Header>(x => x.Without(xx => xx.ChangeOrders));
            fixture.Customize<Line>(x => x.Without(xx => xx.ChangeOrderLines));
            fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList().ForEach(b => fixture.Behaviors.Remove(b));
            fixture.Behaviors.Add(new OmitOnRecursionBehavior());

            // Create test data using AutoFixture
            var quotePublicId = fixture.Create<string>();
            var deletedLineNumber = fixture.Create<string>();
            var remainingLineNumber = fixture.Create<string>();

            var opp = fixture.Build<Opportunity>()
                .With(x => x.StageName, OpportunityStage.Develop)
                .With(x => x.EffectiveProbability, 90)
                .With(x => x.Quote, fixture.Build<Quote>()
                    .With(x => x.Name, quotePublicId)
                    .Create())
                .Create();

            SetupSalesforceOpportunityResponse(opp);

            // Create existing header with matching opportunity
            var header = fixture.Build<Header>()
                .Without(x => x.Lines)
                .Without(x => x.AgreementNumber)
                .Without(x => x.ChangeOrders)
                .Without(x => x.ChangeOrderHeaders)
                .With(x => x.OpportunityNumber, opp.Id)
                .With(x => x.QuotePublicId, quotePublicId)
                .Create();

            int headerId = await context.AddOrderHeader(header);

            // Create existing line that will be deleted in Salesforce
            var lineToBeDeletedData = fixture.Build<Line>()
                .Without(x => x.Header)
                .Without(x => x.ChangeOrderLines)
                .With(x => x.QuotePublicId, quotePublicId)
                .With(x => x.QuoteLineNumber, deletedLineNumber)
                .With(x => x.IsDeleted, false)
                .Create();

            var lineToBeDeleted = await context.UpsertHeaderLine(headerId, lineToBeDeletedData);

            // Create existing line that will remain in Salesforce
            var lineToRemainData = fixture.Build<Line>()
                .Without(x => x.Header)
                .Without(x => x.ChangeOrderLines)
                .With(x => x.QuotePublicId, quotePublicId)
                .With(x => x.QuoteLineNumber, remainingLineNumber)
                .With(x => x.IsDeleted, false)
                .Create();

            var lineToRemain = await context.UpsertHeaderLine(headerId, lineToRemainData);

            // Create reservations for both lines using AutoFixture
            var reservationForDeletedLineData = fixture.Build<Reservation>()
                .With(x => x.LineId, lineToBeDeleted)
                .Create();

            var reservationForDeletedLine = await context.AddReservation(reservationForDeletedLineData);

            var reservationForRemainingLineData = fixture.Build<Reservation>()
                .With(x => x.LineId, lineToRemain)
                .Create();

            var reservationForRemainingLine = await context.AddReservation(reservationForRemainingLineData);

            // Setup Salesforce response to only return the remaining line (simulating deletion of the other line)
            var remainingQuoteLine = fixture.Build<OpportunityQuoteLine>()
                .With(x => x.Id, remainingLineNumber)
                .With(x => x.LineId, fixture.Create<double>())
                .Create();

            SetupSalesforceQuoteLineResponse(remainingQuoteLine);

            GetOpportunitiesHandler sut = new(_mockSalesforceService.Object, context, Mock.Of<ILogger>());

            // Act
            IList<Opportunity>? result = await sut.Handle(string.Empty, 90);

            // Assert - Verify the opportunity was processed (filtered out since it matches existing)
            result.Should().NotBeNull();
            result.Should().BeEmpty();

            // Verify the deleted line is marked as deleted
            var deletedLine = await context.Lines.FindAsync(lineToBeDeleted);
            deletedLine.Should().NotBeNull();
            deletedLine!.IsDeleted.Should().BeTrue();
            deletedLine.AgreementLineNumber.Should().Contain("_"); // Should have timestamp appended

            // Verify the remaining line is still active
            var remainingLine = await context.Lines.FindAsync(lineToRemain);
            remainingLine.Should().NotBeNull();
            remainingLine!.IsDeleted.Should().BeFalse();

            // Verify the reservation for the deleted line is removed
            var deletedReservation = await context.Reservations.FindAsync(reservationForDeletedLine);
            deletedReservation.Should().BeNull("Reservation should be deleted when associated line is deleted in Salesforce");

            // Verify the reservation for the remaining line still exists
            var remainingReservation = await context.Reservations.FindAsync(reservationForRemainingLine);
            remainingReservation.Should().NotBeNull("Reservation should remain when associated line is not deleted");

            // Additional verification: Check total count of reservations in the database
            var totalReservations = context.Reservations.Count();
            totalReservations.Should().Be(1, "Only one reservation should remain after the sync process");
        }
    }

    private void SetupSalesforceOpportunityResponse(Opportunity opportunity)
    {
        _mockSalesforceService
            .Setup(x => x.Query<Opportunity>(It.IsAny<string>()))
            .ReturnsAsync(new SOQLResponse<Opportunity>
            {
                Records = new List<Opportunity>()
                {
                    opportunity
                },
                TotalSize = 1
            });
    }

    private void SetupSalesforceQuoteLineResponse(OpportunityQuoteLine line)
    {
        var response = new HttpResponseMessage()
        {
            Content = new StringContent(JsonConvert.SerializeObject(new SOQLResponse<OpportunityQuoteLine>
            {
                Records = new List<OpportunityQuoteLine> { line },
                TotalSize = 1
            }))
        };

        _mockSalesforceService
            .Setup(x => x.QueryRaw<OpportunityQuoteLine>(It.IsAny<string>()))
            .ReturnsAsync(response)
            .Callback<string>(query => _output.WriteLine($"Mock QueryRaw called with: {query}"));

        // Also setup for multiple calls
        _mockSalesforceService
            .Setup(x => x.QueryRaw<OpportunityQuoteLine>(It.IsAny<string>()))
            .ReturnsAsync(() => response);
    }

    private void SetupSalesforceMultipleQuoteLinesResponse(List<OpportunityQuoteLine> lines)
    {
        _mockSalesforceService
            .Setup(x => x.QueryRaw<OpportunityQuoteLine>(It.IsAny<string>()))
            .ReturnsAsync(new HttpResponseMessage()
            {
                Content = new StringContent(JsonConvert.SerializeObject(new SOQLResponse<OpportunityQuoteLine>
                {
                    Records = lines,
                    TotalSize = lines.Count
                }))
            });
    }
}
