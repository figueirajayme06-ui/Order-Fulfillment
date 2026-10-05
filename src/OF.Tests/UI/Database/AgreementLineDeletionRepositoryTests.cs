using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using OF.Data.Database;
using OF.UI.Database;
using OF.UI.Identity;
using static OF.Common.Enums;

namespace OF.Tests.UI.Database;

public class AgreementLineDeletionRepositoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("T100", 0, true)]
    [InlineData("t100", 0, true)]
    [InlineData("A100", 0, false)]
    [InlineData("A100", 3, false)]
    [InlineData("Q100", 0, false)]
    [InlineData("X100", 0, false)]
    [InlineData(null, 0, false)]
    [InlineData("T100", 1, false)]
    [InlineData("T100", 2, false)]
    [InlineData("T100", 3, false)]
    [InlineData("T100", 99, false)]
    public void Delete_EnforcesPersistedHeaderTypeAndState(string? number, int state, bool allowed)
    {
        using var context = Seed();
        var header = context.Headers.Single();
        header.AgreementNumber = number;
        header.ActivationStatus = state;
        context.SaveChanges();
        var result = Delete(context);
        result.Failure.Should().Be(allowed ? AgreementLineDeletionFailure.None : AgreementLineDeletionFailure.HeaderNotEligible);
    }

    [Fact]
    public void Delete_RemovesAllReservationKindsAndAuditsSoftDeletionAndHeader()
    {
        using var context = Seed();
        context.Reservations.AddRange(
            Reservation(1), Reservation(2, depot: true), Reservation(3, rehire: true), Reservation(4, quantity: 10));
        context.Reservations.Add(Reservation(5, lineId: 12));
        context.SaveChanges();
        var result = Delete(context);
        result.Failure.Should().Be(AgreementLineDeletionFailure.None);
        result.RemovedReservationCount.Should().Be(4);
        context.Reservations.Should().ContainSingle(reservation => reservation.LineId == 12);
        var deleted = context.Lines.Single(line => line.Id == 11);
        deleted.IsDeleted.Should().BeTrue();
        deleted.QuantityFulfilled.Should().Be(0);
        deleted.FulfilmentStatus.Should().Be(0);
        deleted.LastUpdatedBy.Should().Be("planner");
        deleted.LastUpdatedDate.Should().Be(Now.UtcDateTime);
        var header = context.Headers.Single();
        header.LastUpdatedBy.Should().Be("planner");
        header.LastUpdatedDate.Should().Be(Now.UtcDateTime);
        header.FulfilmentStatus.Should().Be((int)FulfilmentStatus.FullyFulfiled);
        Delete(context).Failure.Should().Be(AgreementLineDeletionFailure.NotFound);
    }

    [Fact]
    public void Delete_RejectsLastLiveLineEvenWithDeletedLines()
    {
        using var context = Seed();
        context.Lines.Single(line => line.Id == 12).IsDeleted = true;
        context.SaveChanges();
        Delete(context).Failure.Should().Be(AgreementLineDeletionFailure.LastLine);
        context.Lines.Single(line => line.Id == 11).IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Delete_RejectsParentWithLiveDescendantsThenAllowsAfterChildDeletion()
    {
        using var context = Seed();
        context.Lines.Add(Line(13, "T100-1.1"));
        context.SaveChanges();
        Delete(context).Failure.Should().Be(AgreementLineDeletionFailure.HasChildren);
        Delete(context, lineId: 13).Failure.Should().Be(AgreementLineDeletionFailure.None);
        Delete(context).Failure.Should().Be(AgreementLineDeletionFailure.None);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(99)]
    public void Delete_RejectsNonTodoLine(int state)
    {
        using var context = Seed();
        context.Lines.Single(line => line.Id == 11).ActivationStatus = state;
        context.SaveChanges();
        Delete(context).Failure.Should().Be(AgreementLineDeletionFailure.LineNotEligible);
    }

    [Fact]
    public void Delete_RejectsConfirmedReservationsWithoutChanges()
    {
        using var context = Seed();
        var reservation = Reservation(1);
        reservation.IsConfirmed = true;
        context.Reservations.Add(reservation);
        context.SaveChanges();
        Delete(context).Failure.Should().Be(AgreementLineDeletionFailure.ConfirmedReservations);
        context.Lines.Should().OnlyContain(line => !line.IsDeleted);
        context.Reservations.Should().ContainSingle();
    }

    [Theory]
    [InlineData("header")]
    [InlineData("line")]
    [InlineData("asset")]
    [InlineData("item")]
    [InlineData("quantity")]
    public void Delete_RejectsExternalActivationEvidenceEvenWithTodoFlags(string evidence)
    {
        using var context = Seed();
        if (evidence == "header") context.Headers.Single().ActivationInstanceId = "workflow";
        else if (evidence == "line") context.Lines.Single(line => line.Id == 11).ActivationInstanceId = "workflow";
        else
        {
            var reservation = Reservation(1);
            if (evidence == "asset") reservation.ActualAssetId = "ACTUAL";
            if (evidence == "item") reservation.ActualItemNumber = "ACTUAL";
            if (evidence == "quantity") reservation.ActualQuantity = 0;
            context.Reservations.Add(reservation);
        }
        context.SaveChanges();
        Delete(context).Failure.Should().Be(evidence == "header" ? AgreementLineDeletionFailure.HeaderNotEligible
            : evidence == "line" ? AgreementLineDeletionFailure.LineNotEligible : AgreementLineDeletionFailure.ConfirmedReservations);
        context.Lines.Should().OnlyContain(line => !line.IsDeleted);
    }
    [Fact]
    public void Delete_RejectsForeignLineAndChangedDivision()
    {
        using var context = Seed();
        Delete(context, lineId: 999).Failure.Should().Be(AgreementLineDeletionFailure.NotFound);
        Delete(context, division: "US").Failure.Should().Be(AgreementLineDeletionFailure.NotFound);
        context.Lines.Should().OnlyContain(line => !line.IsDeleted);
    }

    [Fact]
    public void Delete_ReloadsPersistedHeaderAndLineInsteadOfTrustingTrackedChanges()
    {
        using var context = Seed();
        var header = context.Headers.Single();
        header.AgreementNumber = "A100";
        var line = context.Lines.Single(line => line.Id == 11);
        line.ActivationStatus = 2;
        context.SaveChanges();
        header.AgreementNumber = "T100";
        line.ActivationStatus = 0;
        Delete(context).Failure.Should().Be(AgreementLineDeletionFailure.HeaderNotEligible);
        header.AgreementNumber = "T100";
        context.Entry(line).Reload();
        context.SaveChanges();
        line.ActivationStatus = 0;
        Delete(context).Failure.Should().Be(AgreementLineDeletionFailure.LineNotEligible);
    }

    [Fact]
    public void Delete_CompatibilityEquipmentEndpointRejectsActivatedAAgreement()
    {
        using var context = Seed();
        var header = context.Headers.Single();
        header.AgreementNumber = "A100";
        header.ActivationStatus = 3;
        context.SaveChanges();
        new AgreementEquipmentRepository(context, new FakeTimeProvider(Now))
            .Delete(Identity(), 1, "UK", 11).Failure.Should().Be(AgreementEquipmentFailure.HeaderNotEligible);
    }

    private static AgreementLineDeletionResult Delete(InMemoryDBContext context, int lineId = 11, string division = "UK") =>
        new AgreementLineDeletionRepository(context, new FakeTimeProvider(Now)).Delete(Identity(), 1, division, lineId);

    internal static IUserIdentity Identity()
    {
        var identity = new Mock<IUserIdentity>();
        identity.Setup(candidate => candidate.GetIdentity()).Returns(new User { LoginName = "planner", Division = "UK" });
        return identity.Object;
    }

    private static InMemoryDBContext Seed()
    {
        var context = new InMemoryDBContext();
        context.Headers.Add(new Header { Id = 1, AgreementNumber = "T100", Division = "UK", Facility = "UK1", OrderSource = "IPG", FulfilmentStatus = 1 });
        var fulfilled = Line(12, "T100-2");
        fulfilled.FulfilmentStatus = 3;
        fulfilled.QuantityFulfilled = 1;
        context.Lines.AddRange(Line(11, "T100-1"), fulfilled);
        context.SaveChanges();
        return context;
    }

    internal static Line Line(int id, string number) => new()
    {
        Id = id, HeaderId = 1, AgreementLineNumber = number, Division = "UK", Facility = "UK1", Warehouse = "UK1",
        OrderSource = "IPG", RequiresFulfilment = true, Quantity = 1, ValidFromDate = Now.UtcDateTime, ValidToDate = Now.UtcDateTime.AddDays(1),
    };

    private static Reservation Reservation(int id, bool depot = false, bool rehire = false, int quantity = 1, int lineId = 11) => new()
    {
        Id = id, LineId = lineId, AssetId = "ASSET", ItemNumber = "ITEM", Warehouse = "UK1", Quantity = quantity, EffectiveQuantity = quantity,
        IsDepotFulfilled = depot, IsRehire = rehire,
    };
}
