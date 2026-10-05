using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OF.Data;
using OF.Data.Database;
using static OF.Common.Enums;

namespace OF.UI.Database;

public interface IAgreementResetRepository
{
    AgreementResetResult Unfulfil(int headerId, string expectedDivision, string loginName);
}

public sealed class AgreementResetRepository(ApplicationDbContext context, TimeProvider timeProvider)
    : IAgreementResetRepository
{
    public AgreementResetResult Unfulfil(int headerId, string expectedDivision, string loginName)
    {
        using var transaction = context.Database.IsRelational()
            ? context.Database.BeginTransaction(IsolationLevel.Serializable)
            : null;
        try
        {
            if (context.Database.IsSqlServer())
            {
                var resource = $"OF:AgreementMutation:{headerId}";
                context.Database.ExecuteSqlInterpolated($@"
DECLARE @lockResult int;
EXEC @lockResult = sys.sp_getapplock @Resource={resource}, @LockMode='Exclusive',
    @LockOwner='Transaction', @LockTimeout=10000;
IF @lockResult < 0 THROW 51000, 'The agreement is being changed. Refresh and try again.', 1;");
            }

            // The controller may have loaded an older tracked header. Always re-read under the lock.
            var header = context.Headers.AsNoTracking().SingleOrDefault(candidate => candidate.Id == headerId);
            if (header == null || !string.Equals(header.Division.Trim(), expectedDivision.Trim(), StringComparison.OrdinalIgnoreCase))
                return new(AgreementResetFailure.NotFound);
            if (!AgreementResetEligibility.CanReset(header))
                return new(AgreementResetFailure.ActivationConflict);

            var lines = context.Lines.AsNoTracking().Where(line => line.HeaderId == headerId).ToList();
            if (lines.Any(line => line.ActivationStatus != (int)ActivationStatus.TODO
                || !string.IsNullOrWhiteSpace(line.ActivationInstanceId)))
                return new(AgreementResetFailure.ActivationConflict);

            // A subquery avoids SQL's parameter limit on large agreements and includes deleted lines.
            var lineIds = context.Lines.Where(line => line.HeaderId == headerId).Select(line => line.Id);
            var reservations = context.Reservations.AsNoTracking().Where(reservation => lineIds.Contains(reservation.LineId)).ToList();
            if (reservations.Any(reservation => reservation.IsConfirmed
                || !string.IsNullOrWhiteSpace(reservation.ActualAssetId)
                || !string.IsNullOrWhiteSpace(reservation.ActualItemNumber)
                || reservation.ActualQuantity.HasValue))
                return new(AgreementResetFailure.ConfirmedReservations);

            // Detach stale snapshots once, rather than searching the tracker for every reservation.
            var scopeLineIds = lines.Select(line => line.Id).ToHashSet();
            var scopeReservationIds = reservations.Select(reservation => reservation.Id).ToHashSet();
            foreach (var entry in context.ChangeTracker.Entries<Line>().Where(entry => scopeLineIds.Contains(entry.Entity.Id)).ToArray())
                entry.State = EntityState.Detached;
            foreach (var entry in context.ChangeTracker.Entries<Reservation>().Where(entry => scopeReservationIds.Contains(entry.Entity.Id)).ToArray())
                entry.State = EntityState.Detached;
            var trackedHeader = context.Headers.Local.SingleOrDefault(candidate => candidate.Id == headerId);
            if (trackedHeader != null)
                context.Entry(trackedHeader).State = EntityState.Detached;

            var now = timeProvider.GetUtcNow().UtcDateTime;
            var linesReset = 0;
            foreach (var line in lines)
            {
                var status = line.RequiresFulfilment && line.Quantity > 0
                    ? (int)FulfilmentStatus.Unfulfilled
                    : (int)FulfilmentStatus.FullyFulfiled;
                if (line.QuantityFulfilled == 0 && line.FulfilmentStatus == status)
                    continue;

                context.Lines.Attach(line);
                line.QuantityFulfilled = 0;
                line.FulfilmentStatus = status;
                line.LastUpdatedBy = loginName;
                line.LastUpdatedDate = now;
                linesReset++;
            }

            var serviceItems = context.CpqServices.AsNoTracking().Select(service => service.ProductCode)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var fulfilmentLines = lines.Where(line => !line.IsDeleted && line.RequiresFulfilment
                && line.AgreementLineNumber != null && (line.ItemNumber == null || !serviceItems.Contains(line.ItemNumber))).ToList();
            var nonQuoteLines = fulfilmentLines.Where(line => !line.AgreementLineNumber!.StartsWith("Q", StringComparison.OrdinalIgnoreCase)).ToList();
            if (nonQuoteLines.Count > 0)
                fulfilmentLines = nonQuoteLines;
            var headerStatus = (int)FulfilmentStatus.Unfulfilled;
            if (fulfilmentLines.Any(line => line.FulfilmentStatus != (int)FulfilmentStatus.Unfulfilled))
                headerStatus = (int)FulfilmentStatus.PartiallyFulfilled;
            if (fulfilmentLines.Count > 0 && fulfilmentLines.All(line => line.FulfilmentStatus == (int)FulfilmentStatus.FullyFulfiled))
                headerStatus = (int)FulfilmentStatus.FullyFulfiled;

            if (reservations.Count > 0 || linesReset > 0 || header.FulfilmentStatus != headerStatus)
            {
                context.Headers.Attach(header);
                header.FulfilmentStatus = headerStatus;
                header.LastUpdatedBy = loginName;
                header.LastUpdatedDate = now;
                foreach (var reservation in reservations)
                {
                    context.Reservations.Remove(reservation);
                }
                // One save: reservation audit triggers and header/line metadata share the transaction.
                context.SaveChanges();
            }

            transaction?.Commit();
            return new(AgreementResetFailure.None, linesReset, reservations.Count, headerStatus);
        }
        catch (Exception exception) when (exception is DbUpdateConcurrencyException
            || exception is SqlException { Number: 1205 or 51000 or 1222 }
            || exception.InnerException is SqlException { Number: 1205 or 51000 or 1222 })
        {
            // Disposal rolls back an uncommitted transaction, including one already aborted by SQL Server.
            context.ChangeTracker.Clear();
            return new(AgreementResetFailure.ConcurrentChange);
        }
        catch
        {
            // Disposal rolls back an uncommitted transaction, including one already aborted by SQL Server.
            context.ChangeTracker.Clear();
            throw;
        }
    }

}

public static class AgreementResetEligibility
{
    public static bool CanReset(Header header) => !header.IsDeleted
        && header.ActivationStatus == (int)ActivationStatus.TODO
        && string.IsNullOrWhiteSpace(header.ActivationInstanceId);
}

public enum AgreementResetFailure { None, NotFound, ActivationConflict, ConfirmedReservations, ConcurrentChange }

public sealed record AgreementResetResult(
    AgreementResetFailure Failure,
    int LinesReset = 0,
    int ReservationsRemoved = 0,
    int HeaderStatus = 0);
