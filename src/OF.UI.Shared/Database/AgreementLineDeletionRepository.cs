using System.Data;
using Microsoft.EntityFrameworkCore;
using OF.Data;
using OF.Data.Database;
using OF.UI.Identity;
using static OF.Common.Enums;

namespace OF.UI.Database;

public interface IAgreementLineDeletionRepository
{
    AgreementLineDeletionResult Delete(IUserIdentity identity, int headerId, string expectedDivision, int lineId);
}

public sealed class AgreementLineDeletionRepository(ApplicationDbContext context, TimeProvider timeProvider)
    : IAgreementLineDeletionRepository
{
    public AgreementLineDeletionResult Delete(IUserIdentity identity, int headerId, string expectedDivision, int lineId)
    {
        using var transaction = context.Database.IsRelational()
            ? context.Database.BeginTransaction(IsolationLevel.Serializable)
            : null;
        if (context.Database.IsSqlServer())
        {
            var resource = $"OF:AgreementMutation:{headerId}";
            context.Database.ExecuteSqlInterpolated($@"
DECLARE @lockResult int;
EXEC @lockResult = sys.sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000;
IF @lockResult < 0 THROW 51000, 'Could not acquire the agreement mutation lock.', 1;");
        }

        var header = context.Headers.SingleOrDefault(candidate => candidate.Id == headerId);
        if (header != null) context.Entry(header).Reload();
        if (header == null || header.IsDeleted
            || !string.Equals(header.Division?.Trim(), expectedDivision.Trim(), StringComparison.OrdinalIgnoreCase))
            return new(AgreementLineDeletionFailure.NotFound);
        if (!AgreementLineDeletionEligibility.CanDelete(header))
            return new(AgreementLineDeletionFailure.HeaderNotEligible);

        var lines = context.Lines.AsNoTracking().Where(candidate => candidate.HeaderId == headerId).ToArray();
        var line = lines.SingleOrDefault(candidate => candidate.Id == lineId && !candidate.IsDeleted);
        if (line == null) return new(AgreementLineDeletionFailure.NotFound);
        if (line.ActivationStatus != (int)ActivationStatus.TODO
            || !string.IsNullOrWhiteSpace(line.ActivationInstanceId)
            || !string.Equals(line.Division?.Trim(), header.Division?.Trim(), StringComparison.OrdinalIgnoreCase))
            return new(AgreementLineDeletionFailure.LineNotEligible);
        var liveLines = lines.Where(candidate => !candidate.IsDeleted).ToArray();
        if (liveLines.Length <= 1) return new(AgreementLineDeletionFailure.LastLine);
        if (!string.IsNullOrWhiteSpace(line.AgreementLineNumber)
            && liveLines.Any(candidate => candidate.Id != line.Id
                && candidate.AgreementLineNumber?.StartsWith(line.AgreementLineNumber + ".", StringComparison.OrdinalIgnoreCase) == true))
            return new(AgreementLineDeletionFailure.HasChildren);

        var reservations = context.Reservations.AsNoTracking().Where(reservation => reservation.LineId == lineId).ToArray();
        if (reservations.Any(reservation => reservation.IsConfirmed
                || !string.IsNullOrWhiteSpace(reservation.ActualAssetId)
                || !string.IsNullOrWhiteSpace(reservation.ActualItemNumber)
                || reservation.ActualQuantity.HasValue))
            return new(AgreementLineDeletionFailure.ConfirmedReservations);

        // Read one fresh snapshot for the full agreement; attach only rows being changed.
        var trackedLine = context.Lines.Local.SingleOrDefault(candidate => candidate.Id == lineId);
        if (trackedLine != null) context.Entry(trackedLine).State = EntityState.Detached;
        context.Lines.Attach(line);
        var reservationIds = reservations.Select(reservation => reservation.Id).ToHashSet();
        foreach (var entry in context.ChangeTracker.Entries<Reservation>().Where(entry => reservationIds.Contains(entry.Entity.Id)).ToArray())
            entry.State = EntityState.Detached;
        context.Reservations.RemoveRange(reservations);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var loginName = identity.GetIdentity()?.LoginName;
        line.IsDeleted = true;
        line.QuantityFulfilled = 0;
        line.FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled;
        line.LastUpdatedDate = now;
        line.LastUpdatedBy = loginName;

        var services = context.CpqServices.AsNoTracking().Select(service => service.ProductCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var fulfilmentLines = liveLines.Where(candidate => !candidate.IsDeleted && candidate.RequiresFulfilment
            && candidate.AgreementLineNumber != null
            && (candidate.ItemNumber == null || !services.Contains(candidate.ItemNumber))).ToArray();
        var nonQuoteLines = fulfilmentLines.Where(candidate => !candidate.AgreementLineNumber!.StartsWith("Q", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (nonQuoteLines.Length > 0) fulfilmentLines = nonQuoteLines;
        header.FulfilmentStatus = (int)(fulfilmentLines.Length > 0 && fulfilmentLines.All(candidate => candidate.FulfilmentStatus == (int)FulfilmentStatus.FullyFulfiled)
            ? FulfilmentStatus.FullyFulfiled
            : fulfilmentLines.Any(candidate => candidate.FulfilmentStatus != (int)FulfilmentStatus.Unfulfilled)
                ? FulfilmentStatus.PartiallyFulfilled : FulfilmentStatus.Unfulfilled);
        header.LastUpdatedDate = now;
        header.LastUpdatedBy = loginName;
        context.SaveChanges();
        transaction?.Commit();
        return new(AgreementLineDeletionFailure.None, lineId, reservations.Length, header.FulfilmentStatus);
    }
}

public static class AgreementLineDeletionEligibility
{
    public static bool CanDelete(Header header) => !header.IsDeleted
        && header.AgreementNumber?.StartsWith("T", StringComparison.OrdinalIgnoreCase) == true
        && header.ActivationStatus == (int)ActivationStatus.TODO
        && string.IsNullOrWhiteSpace(header.ActivationInstanceId);
}

public enum AgreementLineDeletionFailure
{
    None, NotFound, HeaderNotEligible, LineNotEligible, LastLine, HasChildren, ConfirmedReservations,
}

public sealed record AgreementLineDeletionResult(
    AgreementLineDeletionFailure Failure, int LineId = 0, int RemovedReservationCount = 0, int HeaderStatus = 0);
