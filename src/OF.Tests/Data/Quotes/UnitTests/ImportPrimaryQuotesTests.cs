using AutoFixture;
using FluentAssertions;
using Moq;
using Newtonsoft.Json;
using OF.Common.Infrastructure.IPG.Orders.Models.Quotes.Response;
using OF.Common.Infrastructure.IPG.Orders;
using OF.Common.Infrastructure.IPG.Orders.Models.Quotes;
using OF.Data.Database;
using OF.Data.Quotes.UseCases;
using Xunit.Abstractions;
using static OF.Common.Constants;

namespace OF.Tests.Data.Quotes.UnitTests;

public class ImportPrimaryQuotesTests
{
    private readonly Mock<IOrderManagementIntegration> _mockSalesforceService;
    private readonly ITestOutputHelper _output;

    public ImportPrimaryQuotesTests(ITestOutputHelper output)
    {
        _mockSalesforceService = new Mock<IOrderManagementIntegration>();
        _output = output;
    }

    [Theory]
    [InlineData("<strong>this is a strong description</strong>", "this is a strong description")]
    [InlineData("this is a plain text description", "this is a plain text description")]
    [InlineData("this     is     a  plain  text  description  with    some  extra    space", "this is a plain text description with some extra space")]
    [InlineData(
        "this is a very long description that exceeds one hundred and twenty characters and should be truncated to exactly one hundred twenty characters when processed by the import functionality",
        "this is a very long description that exceeds one hundred and twenty characters and should be truncated to exactly one hu")]
    [InlineData(
        "<p><li><italic>this is a very long description with HTML tags that exceeds one hundred and twenty characters after tag removal and should be truncated appropriately<\\p><\\li><\\italic>",
        "this is a very long description with HTML tags that exceeds one hundred and twenty characters after tag removal and shou")]
    public async Task GivenQuoteLine_WithItemDescription_WhenImporting_TagsAreStrippedAndDescriptionIsTruncated(string lineDescription, string expected)
    {
        using (var context = new InMemoryDBContext())
        {

            // Arrange
            Fixture fixture = new();
            Opportunity opp = fixture.Build<Opportunity>().With(x => x.StageName, OpportunityStage.Develop)
                    .With(x => x.EffectiveProbability, 90).Create();
            opp.Quote.OnHireDate = DateTime.Now;
            opp.Quote.OffHireDate = DateTime.Now.AddDays(7);

            SetupSalesforceOpportunityResponse(opp);
            SetupSalesforceQuoteLineResponse(fixture.Build<OpportunityQuoteLine>()
                .With(x => x.Id, Guid.NewGuid().ToString("N"))
                .With(x => x.OnHireDate, opp.Quote.OnHireDate)
                .With(x => x.OffHireDate, opp.Quote.OffHireDate)
                .With(x => x.ItemDescription, lineDescription)
            .Create());

            var sut = new ImportPrimaryQuotes(
                _mockSalesforceService.Object,
                context,
                _output.ToLogger<ImportPrimaryQuotes>().Object);

            // Act
            await sut.Handle();

            // Assert
            var header = context.Headers.Where(h => h.AgreementNumber == opp.Quote.Name).Single();

            var lines = context.Lines.Where(l => l.HeaderId == header.Id).ToList();
            lines.Should().HaveCount(1);
            lines[0].ItemDescription.Should().Be(expected);
        }
    }

    [Fact]
    public async Task GivenClosedLostOpportunity_WhenImporting_ExistingHeaderIsMarkedDeleted()
    {
        using (var context = new InMemoryDBContext())
        {
            // Arrange
            Fixture fixture = new();
            fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList().ForEach(b => fixture.Behaviors.Remove(b));
            fixture.Behaviors.Add(new OmitOnRecursionBehavior());

            var quotePublicId = "QU-CL-TEST";
            Opportunity opp = fixture.Build<Opportunity>()
                .With(x => x.StageName, OpportunityStage.ClosedLost)
                .With(x => x.EffectiveProbability, 90)
                .With(x => x.Quote, fixture.Build<Quote>()
                    .With(x => x.Name, quotePublicId)
                    .Create())
                .Create();

            SetupSalesforceOpportunityResponse(opp);

            var headerId = await context.AddOrderHeader(
                fixture.Build<Header>()
                    .Without(x => x.Lines)
                    .Without(x => x.ChangeOrders)
                    .Without(x => x.ChangeOrderHeaders)
                    .With(x => x.QuotePublicId, quotePublicId)
                    .With(x => x.OpportunityNumber, opp.Id)
                    .With(x => x.IsDeleted, false)
                    .Create());

            await context.AddHeaderLine(headerId,
                fixture.Build<Line>()
                    .Without(x => x.Header)
                    .Without(x => x.ChangeOrderLines)
                    .With(x => x.IsDeleted, false)
                    .Create());

            var sut = new ImportPrimaryQuotes(
                _mockSalesforceService.Object,
                context,
                _output.ToLogger<ImportPrimaryQuotes>().Object);

            // Act
            await sut.Handle();

            // Assert
            var header = await context.Headers.FindAsync(headerId);
            header.Should().NotBeNull();
            header!.IsDeleted.Should().BeTrue();
            header.OpportunityStage.Should().Be(OpportunityStage.ClosedLost);

            var lines = context.Lines.Where(l => l.HeaderId == headerId).ToList();
            lines.Should().AllSatisfy(l => l.IsDeleted.Should().BeTrue());
        }
    }

    [Fact]
    public async Task GivenNotAcceptedOpportunity_WhenImporting_ExistingHeaderIsMarkedDeleted()
    {
        using (var context = new InMemoryDBContext())
        {
            // Arrange
            Fixture fixture = new();
            fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList().ForEach(b => fixture.Behaviors.Remove(b));
            fixture.Behaviors.Add(new OmitOnRecursionBehavior());

            var quotePublicId = "QU-NA-TEST";
            Opportunity opp = fixture.Build<Opportunity>()
                .With(x => x.StageName, OpportunityStage.NotAccepted)
                .With(x => x.EffectiveProbability, 90)
                .With(x => x.Quote, fixture.Build<Quote>()
                    .With(x => x.Name, quotePublicId)
                    .Create())
                .Create();

            SetupSalesforceOpportunityResponse(opp);

            var headerId = await context.AddOrderHeader(
                fixture.Build<Header>()
                    .Without(x => x.Lines)
                    .Without(x => x.ChangeOrders)
                    .Without(x => x.ChangeOrderHeaders)
                    .With(x => x.QuotePublicId, quotePublicId)
                    .With(x => x.OpportunityNumber, opp.Id)
                    .With(x => x.IsDeleted, false)
                    .Create());

            await context.AddHeaderLine(headerId,
                fixture.Build<Line>()
                    .Without(x => x.Header)
                    .Without(x => x.ChangeOrderLines)
                    .With(x => x.IsDeleted, false)
                    .Create());

            var sut = new ImportPrimaryQuotes(
                _mockSalesforceService.Object,
                context,
                _output.ToLogger<ImportPrimaryQuotes>().Object);

            // Act
            await sut.Handle();

            // Assert
            var header = await context.Headers.FindAsync(headerId);
            header.Should().NotBeNull();
            header!.IsDeleted.Should().BeTrue();
            header.OpportunityStage.Should().Be(OpportunityStage.NotAccepted);

            var lines = context.Lines.Where(l => l.HeaderId == headerId).ToList();
            lines.Should().AllSatisfy(l => l.IsDeleted.Should().BeTrue());
        }
    }

    [Fact]
    public async Task GivenClosedLostOpportunity_WhenImporting_DoesNotCreateNewHeader()
    {
        using (var context = new InMemoryDBContext())
        {
            // Arrange - Closed Lost with no existing header should NOT create a new one
            Fixture fixture = new();
            Opportunity opp = fixture.Build<Opportunity>()
                .With(x => x.StageName, OpportunityStage.ClosedLost)
                .With(x => x.EffectiveProbability, 90)
                .Create();

            SetupSalesforceOpportunityResponse(opp);

            var sut = new ImportPrimaryQuotes(
                _mockSalesforceService.Object,
                context,
                _output.ToLogger<ImportPrimaryQuotes>().Object);

            // Act
            await sut.Handle();

            // Assert - No headers should be created for Closed Lost
            context.Headers.Count().Should().Be(0);
        }
    }

    [Fact]
    public async Task GivenNoOpportunities_WhenImporting_HandlesGracefully()
    {
        using (var context = new InMemoryDBContext())
        {
            // Arrange
            _mockSalesforceService
                .Setup(x => x.Query<Opportunity>(It.IsAny<string>()))
                .ReturnsAsync(new SOQLResponse<Opportunity>
                {
                    Records = new List<Opportunity>(),
                    TotalSize = 0
                });

            var sut = new ImportPrimaryQuotes(
                _mockSalesforceService.Object,
                context,
                _output.ToLogger<ImportPrimaryQuotes>().Object);

            // Act & Assert - should not throw
            await sut.Handle();

            context.Headers.Count().Should().Be(0);
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
        _mockSalesforceService
            .Setup(x => x.QueryRaw<OpportunityQuoteLine>(It.IsAny<string>()))
            .ReturnsAsync(new HttpResponseMessage()
            {
                Content = new StringContent(JsonConvert.SerializeObject(new SOQLResponse<OpportunityQuoteLine>
                {
                    Records = new List<OpportunityQuoteLine> { line },
                    TotalSize = 1
                }))
            });
    }
}
