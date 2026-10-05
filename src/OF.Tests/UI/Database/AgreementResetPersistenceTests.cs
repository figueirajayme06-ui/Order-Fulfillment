using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OF.UI.Database;
using static OF.Tests.UI.Database.AgreementResetRepositoryTests;

namespace OF.Tests.UI.Database;

[Collection("DatabaseCollection")]
public class AgreementResetPersistenceTests(DatabaseFixture fixture)
{
    [SkippableFact]
    public void Unfulfil_CommitsReservationDeletionStatusesAndAuditTogether()
    {
        var factory = fixture.SetupDbContext(nameof(Unfulfil_CommitsReservationDeletionStatusesAndAuditTogether));
        using var context = factory.CreateDbContext();
        var header = Seed(context);
        var deleted = NewLine(header.Id, "T100.2");
        deleted.IsDeleted = true;
        context.Lines.Add(deleted);
        context.SaveChanges();
        var reservation = NewReservation(deleted.Id);
        reservation.IsRehire = true;
        context.Reservations.Add(reservation);
        context.SaveChanges();

        Subject(context).Unfulfil(header.Id, "UK", "reset-planner")
            .Should().Be(new AgreementResetResult(AgreementResetFailure.None, 2, 2, 0));

        using var persisted = factory.CreateDbContext();
        persisted.Reservations.Should().BeEmpty();
        persisted.Lines.Should().HaveCount(2).And.OnlyContain(line => line.QuantityFulfilled == 0 && line.FulfilmentStatus == 0);
        persisted.Headers.Single().LastUpdatedBy.Should().Be("reset-planner");
        persisted.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM AuditLog WHERE TableName = 'Reservations' AND OperationType = 'D'")
            .Single().Should().Be(2);
    }

    [SkippableFact]
    public void Unfulfil_RollsBackReservationDeletionLineHeaderAndAuditOnPersistenceFailure()
    {
        var factory = fixture.SetupDbContext(nameof(Unfulfil_RollsBackReservationDeletionLineHeaderAndAuditOnPersistenceFailure));
        using var context = factory.CreateDbContext();
        var header = Seed(context);
        context.Database.ExecuteSqlRaw(@"
CREATE TRIGGER RejectReset ON Reservations AFTER DELETE AS
BEGIN
    THROW 51001, 'Injected reset persistence failure', 1;
END");

        var act = () => Subject(context).Unfulfil(header.Id, "UK", "reset-planner");
        act.Should().Throw<DbUpdateException>();

        using var persisted = factory.CreateDbContext();
        persisted.Reservations.Should().ContainSingle();
        persisted.Lines.Single().QuantityFulfilled.Should().Be(2);
        persisted.Lines.Single().FulfilmentStatus.Should().Be(3);
        persisted.Headers.Single().FulfilmentStatus.Should().Be(3);
        persisted.Headers.Single().LastUpdatedBy.Should().BeNull();
        persisted.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM AuditLog WHERE TableName = 'Reservations' AND OperationType = 'D'")
            .Single().Should().Be(0);
    }

    [SkippableFact]
    public async Task Unfulfil_ObservesConcurrentConfirmationBeforeDeletingReservations()
    {
        var factory = fixture.SetupDbContext(nameof(Unfulfil_ObservesConcurrentConfirmationBeforeDeletingReservations));
        using var context = factory.CreateDbContext();
        var header = Seed(context);
        using var external = factory.CreateDbContext();
        using var transaction = external.Database.BeginTransaction();
        external.Database.ExecuteSqlInterpolated($"UPDATE Reservations SET IsConfirmed = 1 WHERE LineId IN (SELECT Id FROM Lines WHERE HeaderId = {header.Id})");

        // The serializable reset must wait for this uncommitted confirmation, then reject it.
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reset = Task.Run(() =>
        {
            using var resetContext = factory.CreateDbContext();
            entered.SetResult();
            return Subject(resetContext).Unfulfil(header.Id, "UK", "reset-planner");
        });
        await entered.Task;
        await Task.Delay(150);
        reset.IsCompleted.Should().BeFalse();
        transaction.Commit();

        (await reset.WaitAsync(TimeSpan.FromSeconds(20))).Failure.Should().Be(AgreementResetFailure.ConfirmedReservations);
        using var persisted = factory.CreateDbContext();
        persisted.Reservations.Should().ContainSingle().Which.IsConfirmed.Should().BeTrue();
        persisted.Lines.Single().QuantityFulfilled.Should().Be(2);
    }
}
