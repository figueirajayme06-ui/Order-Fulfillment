using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using OF.Common.Infrastructure.IPG.Orders.Models.Quotes;
using OF.Common.UseCases.Quotes;
using OF.Data.Database;

namespace OF.Tests.Data.Quotes.UnitTests;

public class PersistOrdersHandlerTests
{
    private readonly Mock<ILogger> _mockLogger = new();

    private static Header BuildHeader(string quotePublicId) => new()
    {
        QuotePublicId = quotePublicId,
        AgreementNumber = quotePublicId,
        OrderSource = "SF",
        IsDeleted = false,
        Division = "DIV",
        Facility = "FAC"
    };

    private static Line BuildExistingLine(int headerId, string agreementLineNumber, string? sfLineId, int index) => new()
    {
        HeaderId = headerId,
        AgreementLineNumber = agreementLineNumber,
        QuoteLineNumber = sfLineId,
        QuoteLineIndex = index,
        ItemNumber = "GEN001",
        IsDeleted = false,
        OrderSource = "SF",
        Division = "DIV",
        Facility = "FAC",
        Warehouse = "TestWarehouse"
    };

    private static QuoteData BuildQuote(string quotePublicId, params QuoteLineData[] lines) => new()
    {
        QuotePublicId = quotePublicId,
        SourceWarehouse = new WarehouseData { Name = "TestWarehouse", Division = "DIV", Facility = "FAC" },
        Lines = lines.ToList()
    };

    private static QuoteLineData BuildLine(string sfLineId, int index, string genericCode = "GEN001") => new()
    {
        QuoteLineId = sfLineId,
        QuoteLineIndex = index,
        GenericCode = genericCode,
        OnHireDate = DateTime.UtcNow,
        OffHireDate = DateTime.UtcNow.AddMonths(3)
    };

    // ──────────────────────────────────────────────────────────────────────
    // Line matching
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task WhenQuoteLineIndexChanges_ExistingLineIsMatchedBySalesforceId_AndAgreementLineNumberIsUpdated()
    {
        using var context = new InMemoryDBContext();
        var sut = new PersistOrdersHandler(context, _mockLogger.Object);

        const string quoteId = "Q-001";
        const string sfLineId = "SF_LINE_STABLE_ID";

        // Arrange: existing header + line with original index 1 (AgreementLineNumber = "Q001-1")
        var header = BuildHeader(quoteId);
        context.Headers.Add(header);
        await context.SaveChangesAsync();

        var existingLine = BuildExistingLine(header.Id, "Q001-1", sfLineId, 1);
        context.Lines.Add(existingLine);
        await context.SaveChangesAsync();

        // Act: re-sync same quote but line index has changed to 2
        var quote = BuildQuote(quoteId, BuildLine(sfLineId, 2));
        await sut.Handle(new List<QuoteData> { quote });

        // Assert: line should still exist and AgreementLineNumber updated to new index
        var lines = context.Lines.Where(l => l.HeaderId == header.Id).ToList();
        lines.Should().HaveCount(1);
        lines[0].AgreementLineNumber.Should().Be("Q001-2");
        lines[0].QuoteLineNumber.Should().Be(sfLineId);
        lines[0].IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task WhenQuoteLineIndexChanges_ReservationOnExistingLine_IsPreserved()
    {
        using var context = new InMemoryDBContext();
        var sut = new PersistOrdersHandler(context, _mockLogger.Object);

        const string quoteId = "Q-002";
        const string sfLineId = "SF_LINE_WITH_BOOKING";

        // Arrange: existing line with a reservation
        var header = BuildHeader(quoteId);
        context.Headers.Add(header);
        await context.SaveChangesAsync();

        var existingLine = BuildExistingLine(header.Id, "Q002-1", sfLineId, 1);
        context.Lines.Add(existingLine);
        await context.SaveChangesAsync();

        var reservation = new Reservation
        {
            AssetId = "ASSET001",
            ItemNumber = "GEN001",
            Warehouse = "TestWarehouse",
            LineId = existingLine.Id,
            Quantity = 1,
            EffectiveQuantity = 1
        };
        await context.AddReservation(reservation);

        // Act: re-sync with changed index
        var quote = BuildQuote(quoteId, BuildLine(sfLineId, 2));
        await sut.Handle(new List<QuoteData> { quote });

        // Assert: reservation still linked to the (now-updated) line
        var reservations = context.Reservations.Where(r => r.LineId == existingLine.Id).ToList();
        reservations.Should().HaveCount(1);
        reservations[0].AssetId.Should().Be("ASSET001");
    }

    [Fact]
    public async Task WhenLineIsRemovedFromQuote_AndHasNoReservations_LineIsDeleted()
    {
        using var context = new InMemoryDBContext();
        var sut = new PersistOrdersHandler(context, _mockLogger.Object);

        const string quoteId = "Q-003";

        // Arrange: two existing lines
        var header = BuildHeader(quoteId);
        context.Headers.Add(header);
        await context.SaveChangesAsync();

        var lineToKeep = BuildExistingLine(header.Id, "Q003-1", "SF_KEEP", 1);
        var lineToDelete = BuildExistingLine(header.Id, "Q003-2", "SF_DELETE", 2);
        lineToDelete.ItemNumber = "GEN002";
        context.Lines.AddRange(lineToKeep, lineToDelete);
        await context.SaveChangesAsync();

        // Act: re-sync with only the first line present
        var quote = BuildQuote(quoteId, BuildLine("SF_KEEP", 1));
        await sut.Handle(new List<QuoteData> { quote });

        // Assert: only the kept line remains
        var lines = context.Lines.Where(l => l.HeaderId == header.Id).ToList();
        lines.Should().HaveCount(1);
        lines[0].QuoteLineNumber.Should().Be("SF_KEEP");
    }

    [Fact]
    public async Task WhenLineIsRemovedFromQuote_AndHasActiveReservations_LineIsNotDeleted()
    {
        using var context = new InMemoryDBContext();
        var sut = new PersistOrdersHandler(context, _mockLogger.Object);

        const string quoteId = "Q-004";

        // Arrange: existing line with a reservation that is NOT in the incoming sync
        var header = BuildHeader(quoteId);
        context.Headers.Add(header);
        await context.SaveChangesAsync();

        var lineWithReservation = BuildExistingLine(header.Id, "Q004-1", "SF_BOOKED", 1);
        context.Lines.Add(lineWithReservation);
        await context.SaveChangesAsync();

        await context.AddReservation(new Reservation
        {
            AssetId = "ASSET_BOOKED",
            ItemNumber = "GEN001",
            Warehouse = "TestWarehouse",
            LineId = lineWithReservation.Id,
            Quantity = 1,
            EffectiveQuantity = 1
        });

        // Act: sync comes in with NO lines (line was removed in CPQ)
        var quote = BuildQuote(quoteId);
        await sut.Handle(new List<QuoteData> { quote });

        // Assert: line is soft-deleted but NOT physically removed — reservation intact
        context.Lines.Any(l => l.Id == lineWithReservation.Id).Should().BeTrue();
        context.Reservations.Any(r => r.LineId == lineWithReservation.Id).Should().BeTrue();
    }

    [Fact]
    public async Task WhenNewQuoteArrives_LineIsCreatedWithQuoteLineNumber()
    {
        using var context = new InMemoryDBContext();
        var sut = new PersistOrdersHandler(context, _mockLogger.Object);

        const string quoteId = "Q-005";
        const string sfLineId = "SF_NEW_LINE";

        var quote = BuildQuote(quoteId, BuildLine(sfLineId, 1));
        await sut.Handle(new List<QuoteData> { quote });

        var lines = context.Lines.ToList();
        lines.Should().HaveCount(1);
        lines[0].QuoteLineNumber.Should().Be(sfLineId);
        lines[0].AgreementLineNumber.Should().Be("Q005-1");
    }

    [Fact]
    public async Task WhenLineHasNoQuoteLineNumber_FallsBackToAgreementLineNumberMatching()
    {
        using var context = new InMemoryDBContext();
        var sut = new PersistOrdersHandler(context, _mockLogger.Object);

        const string quoteId = "Q-006";

        // Arrange: legacy line without a QuoteLineNumber
        var header = BuildHeader(quoteId);
        context.Headers.Add(header);
        await context.SaveChangesAsync();

        var legacyLine = BuildExistingLine(header.Id, "Q006-1", null, 1);
        context.Lines.Add(legacyLine);
        await context.SaveChangesAsync();

        // Act: sync with matching index — should find by AgreementLineNumber
        var quote = BuildQuote(quoteId, BuildLine("SF_NOW_HAS_ID", 1));
        await sut.Handle(new List<QuoteData> { quote });

        // Assert: existing line updated (not duplicated), now stamped with the SF ID
        var lines = context.Lines.Where(l => l.HeaderId == header.Id).ToList();
        lines.Should().HaveCount(1);
        lines[0].Id.Should().Be(legacyLine.Id);
        lines[0].QuoteLineNumber.Should().Be("SF_NOW_HAS_ID");
    }
}
