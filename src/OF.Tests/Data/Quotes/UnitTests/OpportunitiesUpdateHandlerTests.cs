using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using OF.Common.Infrastructure.IPG.Orders.Models.Quotes;
using OF.Common.UseCases.Quotes;
using OF.Data.Database;
using Xunit.Abstractions;
using static OF.Common.Constants;

namespace OF.Tests.Data.Quotes.UnitTests;

public class OpportunitiesUpdateHandlerTests
{
    private readonly ITestOutputHelper _output;

    public OpportunitiesUpdateHandlerTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(OpportunityStage.ClosedLost, true)]
    [InlineData(OpportunityStage.ClosedWon, false)]
    [InlineData(OpportunityStage.Develop, false)]
    [InlineData(OpportunityStage.NotAccepted, false)]
    [InlineData("closed lost", true)]
    [InlineData("CLOSED LOST", true)]
    public void IsClosedLost_ReturnsExpectedResult(string stageName, bool expected)
    {
        var fixture = new Fixture();
        var opp = fixture.Build<Opportunity>()
            .With(x => x.StageName, stageName)
            .Create();

        OpportunitiesUpdateHandler.IsClosedLost(opp).Should().Be(expected);
    }

    [Theory]
    [InlineData(OpportunityStage.NotAccepted, true)]
    [InlineData(OpportunityStage.ClosedLost, false)]
    [InlineData(OpportunityStage.Develop, false)]
    [InlineData("not accepted", true)]
    [InlineData("NOT ACCEPTED", true)]
    public void IsNotAccepted_ReturnsExpectedResult(string stageName, bool expected)
    {
        var fixture = new Fixture();
        var opp = fixture.Build<Opportunity>()
            .With(x => x.StageName, stageName)
            .Create();

        OpportunitiesUpdateHandler.IsNotAccepted(opp).Should().Be(expected);
    }

    [Fact]
    public void IsClosedLost_NullOpportunity_ReturnsFalse()
    {
        OpportunitiesUpdateHandler.IsClosedLost(null).Should().BeFalse();
    }

    [Fact]
    public void IsNotAccepted_NullOpportunity_ReturnsFalse()
    {
        OpportunitiesUpdateHandler.IsNotAccepted(null).Should().BeFalse();
    }

    [Fact]
    public async Task MarkClosedLostOpportunitiesAsync_WithNoClosedLost_ReturnsZero()
    {
        using var context = new InMemoryDBContext();
        var logger = _output.ToLogger<OpportunitiesUpdateHandler>().Object;
        var sut = new OpportunitiesUpdateHandler(context, logger);

        var fixture = new Fixture();
        var opportunities = new List<Opportunity>
        {
            fixture.Build<Opportunity>()
                .With(x => x.StageName, OpportunityStage.Develop)
                .Create()
        };

        var result = await sut.MarkClosedLostOpportunitiesAsync(opportunities, "test-process");

        result.Should().Be(0);
    }

    [Fact]
    public async Task MarkClosedLostOpportunitiesAsync_WithMatchingHeader_MarksHeaderAndLinesDeleted()
    {
        using var context = new InMemoryDBContext();
        var logger = _output.ToLogger<OpportunitiesUpdateHandler>().Object;
        var sut = new OpportunitiesUpdateHandler(context, logger);

        var fixture = new Fixture();
        fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList().ForEach(b => fixture.Behaviors.Remove(b));
        fixture.Behaviors.Add(new OmitOnRecursionBehavior());

        var quotePublicId = "QU-99999";
        var opp = fixture.Build<Opportunity>()
            .With(x => x.StageName, OpportunityStage.ClosedLost)
            .With(x => x.Quote, fixture.Build<OF.Common.Infrastructure.IPG.Orders.Models.Quotes.Quote>()
                .With(x => x.Name, quotePublicId)
                .Create())
            .Create();

        var headerId = await context.AddOrderHeader(
            fixture.Build<Header>()
                .Without(x => x.Lines)
                .Without(x => x.ChangeOrders)
                .Without(x => x.ChangeOrderHeaders)
                .With(x => x.QuotePublicId, quotePublicId)
                .With(x => x.IsDeleted, false)
                .Create());

        await context.AddHeaderLine(headerId,
            fixture.Build<Line>()
                .Without(x => x.Header)
                .Without(x => x.ChangeOrderLines)
                .With(x => x.IsDeleted, false)
                .Create());

        var result = await sut.MarkClosedLostOpportunitiesAsync(new List<Opportunity> { opp }, "test-process");

        result.Should().Be(1);

        var header = await context.Headers.FindAsync(headerId);
        header.Should().NotBeNull();
        header!.IsDeleted.Should().BeTrue();
        header.LastUpdatedBy.Should().Be("SF");
        header.OpportunityStage.Should().Be(OpportunityStage.ClosedLost);

        var lines = context.Lines.Where(l => l.HeaderId == headerId).ToList();
        lines.Should().AllSatisfy(l =>
        {
            l.IsDeleted.Should().BeTrue();
            l.LastUpdatedBy.Should().Be("SF");
        });
    }

    [Fact]
    public async Task MarkClosedLostOpportunitiesAsync_WithReservations_RemovesReservations()
    {
        using var context = new InMemoryDBContext();
        var logger = _output.ToLogger<OpportunitiesUpdateHandler>().Object;
        var sut = new OpportunitiesUpdateHandler(context, logger);

        var fixture = new Fixture();
        fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList().ForEach(b => fixture.Behaviors.Remove(b));
        fixture.Behaviors.Add(new OmitOnRecursionBehavior());

        var quotePublicId = "QU-CL-RES";
        var opp = fixture.Build<Opportunity>()
            .With(x => x.StageName, OpportunityStage.ClosedLost)
            .With(x => x.Quote, fixture.Build<OF.Common.Infrastructure.IPG.Orders.Models.Quotes.Quote>()
                .With(x => x.Name, quotePublicId)
                .Create())
            .Create();

        var headerId = await context.AddOrderHeader(
            fixture.Build<Header>()
                .Without(x => x.Lines)
                .Without(x => x.ChangeOrders)
                .Without(x => x.ChangeOrderHeaders)
                .With(x => x.QuotePublicId, quotePublicId)
                .With(x => x.IsDeleted, false)
                .Create());

        var lineId = await context.AddHeaderLine(headerId,
            fixture.Build<Line>()
                .Without(x => x.Header)
                .Without(x => x.ChangeOrderLines)
                .With(x => x.IsDeleted, false)
                .Create());

        var reservations = fixture.Build<Reservation>()
            .With(r => r.LineId, lineId)
            .CreateMany(2)
            .ToList();

        await context.AddReservationRange(reservations);

        await sut.MarkClosedLostOpportunitiesAsync(new List<Opportunity> { opp }, "test-process");

        var remainingReservations = context.Reservations.Where(r => r.LineId == lineId).ToList();
        remainingReservations.Should().BeEmpty();
    }

    [Fact]
    public async Task MarkClosedLostOpportunitiesAsync_WithAlreadyDeletedHeader_SkipsIt()
    {
        using var context = new InMemoryDBContext();
        var logger = _output.ToLogger<OpportunitiesUpdateHandler>().Object;
        var sut = new OpportunitiesUpdateHandler(context, logger);

        var fixture = new Fixture();
        fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList().ForEach(b => fixture.Behaviors.Remove(b));
        fixture.Behaviors.Add(new OmitOnRecursionBehavior());

        var quotePublicId = "QU-88888";
        var opp = fixture.Build<Opportunity>()
            .With(x => x.StageName, OpportunityStage.ClosedLost)
            .With(x => x.Quote, fixture.Build<OF.Common.Infrastructure.IPG.Orders.Models.Quotes.Quote>()
                .With(x => x.Name, quotePublicId)
                .Create())
            .Create();

        await context.AddOrderHeader(
            fixture.Build<Header>()
                .Without(x => x.Lines)
                .Without(x => x.ChangeOrders)
                .Without(x => x.ChangeOrderHeaders)
                .With(x => x.QuotePublicId, quotePublicId)
                .With(x => x.IsDeleted, true)
                .Create());

        var result = await sut.MarkClosedLostOpportunitiesAsync(new List<Opportunity> { opp }, "test-process");

        result.Should().Be(0);
    }

    [Fact]
    public async Task MarkClosedLostOpportunitiesAsync_WithNoMatchingHeader_ReturnsZero()
    {
        using var context = new InMemoryDBContext();
        var logger = _output.ToLogger<OpportunitiesUpdateHandler>().Object;
        var sut = new OpportunitiesUpdateHandler(context, logger);

        var fixture = new Fixture();
        var opp = fixture.Build<Opportunity>()
            .With(x => x.StageName, OpportunityStage.ClosedLost)
            .Create();

        var result = await sut.MarkClosedLostOpportunitiesAsync(new List<Opportunity> { opp }, "test-process");

        result.Should().Be(0);
    }

    [Fact]
    public async Task MarkNotAcceptedOpportunitiesAsync_WithMatchingHeader_MarksHeaderAndLinesDeleted()
    {
        using var context = new InMemoryDBContext();
        var logger = _output.ToLogger<OpportunitiesUpdateHandler>().Object;
        var sut = new OpportunitiesUpdateHandler(context, logger);

        var fixture = new Fixture();
        fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList().ForEach(b => fixture.Behaviors.Remove(b));
        fixture.Behaviors.Add(new OmitOnRecursionBehavior());

        var quotePublicId = "QU-77777";
        var opp = fixture.Build<Opportunity>()
            .With(x => x.StageName, OpportunityStage.NotAccepted)
            .With(x => x.Quote, fixture.Build<OF.Common.Infrastructure.IPG.Orders.Models.Quotes.Quote>()
                .With(x => x.Name, quotePublicId)
                .Create())
            .Create();

        var headerId = await context.AddOrderHeader(
            fixture.Build<Header>()
                .Without(x => x.Lines)
                .Without(x => x.ChangeOrders)
                .Without(x => x.ChangeOrderHeaders)
                .With(x => x.QuotePublicId, quotePublicId)
                .With(x => x.IsDeleted, false)
                .Create());

        await context.AddHeaderLine(headerId,
            fixture.Build<Line>()
                .Without(x => x.Header)
                .Without(x => x.ChangeOrderLines)
                .With(x => x.IsDeleted, false)
                .Create());

        var result = await sut.MarkNotAcceptedOpportunitiesAsync(new List<Opportunity> { opp }, "test-process");

        result.Should().Be(1);

        var header = await context.Headers.FindAsync(headerId);
        header.Should().NotBeNull();
        header!.IsDeleted.Should().BeTrue();
        header.LastUpdatedBy.Should().Be("SF");
        header.OpportunityStage.Should().Be(OpportunityStage.NotAccepted);

        var lines = context.Lines.Where(l => l.HeaderId == headerId).ToList();
        lines.Should().AllSatisfy(l =>
        {
            l.IsDeleted.Should().BeTrue();
            l.LastUpdatedBy.Should().Be("SF");
        });
    }

    [Fact]
    public async Task MarkNotAcceptedOpportunitiesAsync_WithReservations_RemovesReservations()
    {
        using var context = new InMemoryDBContext();
        var logger = _output.ToLogger<OpportunitiesUpdateHandler>().Object;
        var sut = new OpportunitiesUpdateHandler(context, logger);

        var fixture = new Fixture();
        fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList().ForEach(b => fixture.Behaviors.Remove(b));
        fixture.Behaviors.Add(new OmitOnRecursionBehavior());

        var quotePublicId = "QU-NA-RES";
        var opp = fixture.Build<Opportunity>()
            .With(x => x.StageName, OpportunityStage.NotAccepted)
            .With(x => x.Quote, fixture.Build<OF.Common.Infrastructure.IPG.Orders.Models.Quotes.Quote>()
                .With(x => x.Name, quotePublicId)
                .Create())
            .Create();

        var headerId = await context.AddOrderHeader(
            fixture.Build<Header>()
                .Without(x => x.Lines)
                .Without(x => x.ChangeOrders)
                .Without(x => x.ChangeOrderHeaders)
                .With(x => x.QuotePublicId, quotePublicId)
                .With(x => x.IsDeleted, false)
                .Create());

        var lineId = await context.AddHeaderLine(headerId,
            fixture.Build<Line>()
                .Without(x => x.Header)
                .Without(x => x.ChangeOrderLines)
                .With(x => x.IsDeleted, false)
                .Create());

        var reservations = fixture.Build<Reservation>()
            .With(r => r.LineId, lineId)
            .CreateMany(2)
            .ToList();

        await context.AddReservationRange(reservations);

        await sut.MarkNotAcceptedOpportunitiesAsync(new List<Opportunity> { opp }, "test-process");

        var remainingReservations = context.Reservations.Where(r => r.LineId == lineId).ToList();
        remainingReservations.Should().BeEmpty();
    }

    [Fact]
    public async Task MarkNotAcceptedOpportunitiesAsync_WithNoNotAccepted_ReturnsZero()
    {
        using var context = new InMemoryDBContext();
        var logger = _output.ToLogger<OpportunitiesUpdateHandler>().Object;
        var sut = new OpportunitiesUpdateHandler(context, logger);

        var fixture = new Fixture();
        var opportunities = new List<Opportunity>
        {
            fixture.Build<Opportunity>()
                .With(x => x.StageName, OpportunityStage.Develop)
                .Create()
        };

        var result = await sut.MarkNotAcceptedOpportunitiesAsync(opportunities, "test-process");

        result.Should().Be(0);
    }

    [Fact]
    public async Task MarkClosedLostOpportunitiesAsync_MixedList_OnlyMarksClosedLost()
    {
        using var context = new InMemoryDBContext();
        var logger = _output.ToLogger<OpportunitiesUpdateHandler>().Object;
        var sut = new OpportunitiesUpdateHandler(context, logger);

        var fixture = new Fixture();
        fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList().ForEach(b => fixture.Behaviors.Remove(b));
        fixture.Behaviors.Add(new OmitOnRecursionBehavior());

        var closedLostQuoteId = "QU-CL001";
        var developQuoteId = "QU-DEV01";

        var closedLostOpp = fixture.Build<Opportunity>()
            .With(x => x.StageName, OpportunityStage.ClosedLost)
            .With(x => x.Quote, fixture.Build<OF.Common.Infrastructure.IPG.Orders.Models.Quotes.Quote>()
                .With(x => x.Name, closedLostQuoteId)
                .Create())
            .Create();

        var developOpp = fixture.Build<Opportunity>()
            .With(x => x.StageName, OpportunityStage.Develop)
            .With(x => x.Quote, fixture.Build<OF.Common.Infrastructure.IPG.Orders.Models.Quotes.Quote>()
                .With(x => x.Name, developQuoteId)
                .Create())
            .Create();

        var closedLostHeaderId = await context.AddOrderHeader(
            fixture.Build<Header>()
                .Without(x => x.Lines)
                .Without(x => x.ChangeOrders)
                .Without(x => x.ChangeOrderHeaders)
                .With(x => x.QuotePublicId, closedLostQuoteId)
                .With(x => x.IsDeleted, false)
                .Create());

        var developHeaderId = await context.AddOrderHeader(
            fixture.Build<Header>()
                .Without(x => x.Lines)
                .Without(x => x.ChangeOrders)
                .Without(x => x.ChangeOrderHeaders)
                .With(x => x.QuotePublicId, developQuoteId)
                .With(x => x.IsDeleted, false)
                .Create());

        var result = await sut.MarkClosedLostOpportunitiesAsync(
            new List<Opportunity> { closedLostOpp, developOpp }, "test-process");

        result.Should().Be(1);

        var closedLostHeader = await context.Headers.FindAsync(closedLostHeaderId);
        closedLostHeader!.IsDeleted.Should().BeTrue();

        var developHeader = await context.Headers.FindAsync(developHeaderId);
        developHeader!.IsDeleted.Should().BeFalse();
    }
}
