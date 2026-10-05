using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using OF.Data;
using OF.Data.Database;
using OF.UI.Database;

namespace OF.Tests.UI.Database;

public class AgreementResetRepositoryTests
{
    internal static readonly DateTimeOffset Now = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Unfulfil_ClearsCompleteAgreementIncludingDeletedLines_PreservesOtherAgreements_AndCanRepeat()
    {
        using var context = new InMemoryDBContext();
        var header = Seed(context);
        var deleted = NewLine(header.Id, "T100.3");
        deleted.IsDeleted = true;
        var nonRequiring = NewLine(header.Id, "T100.4");
        nonRequiring.RequiresFulfilment = false;
        var zero = NewLine(header.Id, "T100.5");
        zero.Quantity = 0;
        var otherHeader = NewHeader();
        context.Headers.Add(otherHeader);
        context.SaveChanges();
        var other = NewLine(otherHeader.Id, "T200.1");
        context.Lines.AddRange(deleted, nonRequiring, zero, other);
        context.SaveChanges();
        var depot = NewReservation(deleted.Id);
        depot.IsDepotFulfilled = true;
        var rehire = NewReservation(nonRequiring.Id);
        rehire.IsRehire = true;
        context.Reservations.AddRange(depot, rehire, NewReservation(other.Id));
        context.SaveChanges();

        var result = Subject(context).Unfulfil(header.Id, "UK", "planner");

        result.Should().Be(new AgreementResetResult(AgreementResetFailure.None, 4, 3, 1));
        context.ChangeTracker.Clear();
        context.Reservations.Should().ContainSingle().Which.LineId.Should().Be(other.Id);
        var lines = context.Lines.Where(line => line.HeaderId == header.Id).ToList();
        lines.Should().HaveCount(4).And.OnlyContain(line => line.QuantityFulfilled == 0);
        lines.Single(line => line.Id == deleted.Id).IsDeleted.Should().BeTrue();
        lines.Where(line => line.RequiresFulfilment && line.Quantity > 0).Should().OnlyContain(line => line.FulfilmentStatus == 0);
        lines.Where(line => !line.RequiresFulfilment || line.Quantity == 0).Should().OnlyContain(line => line.FulfilmentStatus == 3);
        lines.Should().OnlyContain(line => line.LastUpdatedBy == "planner" && line.LastUpdatedDate == Now.UtcDateTime);
        context.Lines.Single(line => line.Id == other.Id).QuantityFulfilled.Should().Be(2);

        Subject(context).Unfulfil(header.Id, "UK", "another-user")
            .Should().Be(new AgreementResetResult(AgreementResetFailure.None, 0, 0, 1));
        context.Headers.Single(candidate => candidate.Id == header.Id).LastUpdatedBy.Should().Be("planner");

        context.Reservations.Add(NewReservation(lines[0].Id));
        context.SaveChanges();
        context.Reservations.Should().HaveCount(2, "normal fulfilment can reserve again after a reset");
    }

    [Theory]
    [InlineData("T100", 0, true)]
    [InlineData("A100", 0, true)]
    [InlineData("Q100", 0, true)]
    [InlineData("T100", 1, false)]
    [InlineData("T100", 2, false)]
    [InlineData("T100", 3, false)]
    [InlineData("T100", 99, false)]
    public void Eligibility_OnlyPermitsUnactivatedHeaders(string number, int status, bool expected)
    {
        AgreementResetEligibility.CanReset(new Header { AgreementNumber = number, ActivationStatus = status })
            .Should().Be(expected);
    }

    [Theory]
    [InlineData("header")]
    [InlineData("header-instance")]
    [InlineData("deleted")]
    [InlineData("line")]
    [InlineData("deleted-line")]
    [InlineData("line-instance")]
    [InlineData("confirmed")]
    [InlineData("actual-asset")]
    [InlineData("actual-item")]
    [InlineData("actual-quantity")]
    public void Unfulfil_RejectsExternalOrUnstableState_WithoutChangingAnyRows(string state)
    {
        using var context = new InMemoryDBContext();
        var header = Seed(context);
        var line = context.Lines.Single();
        var reservation = context.Reservations.Single();
        switch (state)
        {
            case "header": header.ActivationStatus = 2; break;
            case "header-instance": header.ActivationInstanceId = "pending"; break;
            case "deleted": header.IsDeleted = true; break;
            case "line": line.ActivationStatus = 3; break;
            case "deleted-line": line.IsDeleted = true; line.ActivationStatus = 3; break;
            case "line-instance": line.ActivationInstanceId = "pending"; break;
            case "confirmed": reservation.IsConfirmed = true; break;
            case "actual-asset": reservation.ActualAssetId = "external"; break;
            case "actual-item": reservation.ActualItemNumber = "external"; break;
            case "actual-quantity": reservation.ActualQuantity = 0; break;
        }
        context.SaveChanges();

        Subject(context).Unfulfil(header.Id, "UK", "planner").Failure.Should().NotBe(AgreementResetFailure.None);

        context.ChangeTracker.Clear();
        context.Reservations.Should().ContainSingle();
        context.Lines.Single().QuantityFulfilled.Should().Be(2);
        context.Headers.Single().LastUpdatedBy.Should().BeNull();
    }

    [Fact]
    public void Unfulfil_RechecksPersistedDivisionInsteadOfStaleTrackedValues()
    {
        using var context = new InMemoryDBContext();
        var header = Seed(context);
        header.Division = "FR";
        context.SaveChanges();
        header.Division = "UK";

        Subject(context).Unfulfil(header.Id, "UK", "planner").Failure.Should().Be(AgreementResetFailure.NotFound);
        context.Reservations.Should().ContainSingle();
    }

    [Fact]
    public void Unfulfil_PrefersNonQuoteLinesInHeaderStatus()
    {
        using var context = new InMemoryDBContext();
        var header = Seed(context);
        var quote = NewLine(header.Id, "Q100.1");
        quote.Quantity = 0;
        context.Lines.Add(quote);
        context.SaveChanges();

        Subject(context).Unfulfil(header.Id, "UK", "planner").HeaderStatus.Should().Be(0);
    }

    internal static AgreementResetRepository Subject(ApplicationDbContext context) => new(context, new FakeTimeProvider(Now));

    internal static Header Seed(ApplicationDbContext context)
    {
        var header = NewHeader();
        context.Headers.Add(header);
        context.SaveChanges();
        var line = NewLine(header.Id, "T100.1");
        context.Lines.Add(line);
        context.SaveChanges();
        context.Reservations.Add(NewReservation(line.Id));
        context.SaveChanges();
        return header;
    }

    internal static Header NewHeader() => new()
    {
        AgreementNumber = "T100", Division = "UK", Facility = "UK1", OrderSource = "OF", FulfilmentStatus = 3,
    };

    internal static Line NewLine(int headerId, string number) => new()
    {
        HeaderId = headerId, AgreementLineNumber = number, Division = "UK", Facility = "UK1", Warehouse = "UK0",
        OrderSource = "OF", ItemNumber = "GEN", Quantity = 2, QuantityFulfilled = 2, FulfilmentStatus = 3,
        RequiresFulfilment = true, ValidFromDate = Now.UtcDateTime, ValidToDate = Now.UtcDateTime.AddDays(2),
    };

    internal static Reservation NewReservation(int lineId) => new()
    {
        LineId = lineId, AssetId = "ASSET", ItemNumber = "GEN", Warehouse = "UK0", Quantity = 2, EffectiveQuantity = 2,
    };
}
