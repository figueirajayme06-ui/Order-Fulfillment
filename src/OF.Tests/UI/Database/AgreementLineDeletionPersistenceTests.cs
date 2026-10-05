using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OF.Data;
using OF.Data.Database;
using OF.UI.Database;

namespace OF.Tests.UI.Database;

[Collection("DatabaseCollection")]
public sealed class AgreementLineDeletionPersistenceTests(DatabaseFixture fixture)
{
    [SkippableFact]
    public void Delete_RollsBackReservationsLineAndHeaderWhenSaveFailsAfterSqlWrites()
    {
        var factory = fixture.SetupDbContext(nameof(Delete_RollsBackReservationsLineAndHeaderWhenSaveFailsAfterSqlWrites));
        using var seed = factory.CreateDbContext();
        var (headerId, firstId, _) = Seed(seed);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(seed.Database.GetConnectionString())
            .AddInterceptors(new FailAfterSave()).Options;
        using (var failingContext = new ApplicationDbContext(options))
        {
            Action delete = () => new AgreementLineDeletionRepository(failingContext, TimeProvider.System)
                .Delete(AgreementLineDeletionRepositoryTests.Identity(), headerId, "UK", firstId);
            delete.Should().Throw<DbUpdateException>();
        }
        using var verify = factory.CreateDbContext();
        verify.Lines.Single(line => line.Id == firstId).IsDeleted.Should().BeFalse();
        verify.Reservations.Count(reservation => reservation.LineId == firstId).Should().Be(1);
        verify.Headers.Single(header => header.Id == headerId).LastUpdatedBy.Should().BeNull();
    }

    [SkippableFact]
    public async Task Delete_ConcurrentRequestsCannotDeleteBothRemainingLines()
    {
        var factory = fixture.SetupDbContext(nameof(Delete_ConcurrentRequestsCannotDeleteBothRemainingLines));
        using var seed = factory.CreateDbContext();
        var (headerId, firstId, secondId) = Seed(seed);
        using var ready = new Barrier(2);
        Task<AgreementLineDeletionResult> Delete(int lineId) => Task.Run(() =>
        {
            using var context = factory.CreateDbContext();
            ready.SignalAndWait(TimeSpan.FromSeconds(30)).Should().BeTrue();
            return new AgreementLineDeletionRepository(context, TimeProvider.System)
                .Delete(AgreementLineDeletionRepositoryTests.Identity(), headerId, "UK", lineId);
        });
        var results = await Task.WhenAll(Delete(firstId), Delete(secondId));
        results.Count(result => result.Failure == AgreementLineDeletionFailure.None).Should().Be(1);
        results.Count(result => result.Failure == AgreementLineDeletionFailure.LastLine).Should().Be(1);
        using var verify = factory.CreateDbContext();
        verify.Lines.Count(line => line.HeaderId == headerId && !line.IsDeleted).Should().Be(1);
    }

    private static (int HeaderId, int FirstId, int SecondId) Seed(ApplicationDbContext context)
    {
        var header = new Header { AgreementNumber = "T100", Division = "UK", Facility = "UK1", OrderSource = "IPG", FulfilmentStatus = 1 };
        context.Headers.Add(header);
        context.SaveChanges();
        var first = AgreementLineDeletionRepositoryTests.Line(0, "T100-1");
        var second = AgreementLineDeletionRepositoryTests.Line(0, "T100-2");
        first.HeaderId = header.Id;
        second.HeaderId = header.Id;
        second.FulfilmentStatus = 3;
        second.QuantityFulfilled = 1;
        context.Lines.AddRange(first, second);
        context.SaveChanges();
        context.Reservations.Add(new Reservation
        {
            LineId = first.Id, AssetId = "ASSET", ItemNumber = "ITEM", Warehouse = "UK1", Quantity = 1, EffectiveQuantity = 1,
        });
        context.SaveChanges();
        return (header.Id, first.Id, second.Id);
    }

    private sealed class FailAfterSave : SaveChangesInterceptor
    {
        public override int SavedChanges(SaveChangesCompletedEventData eventData, int result) =>
            throw new DbUpdateException("Injected failure after SQL writes, before transaction commit.");
    }
}
