using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OF.Data;
using OF.Data.Database;

namespace OF.Common.Extensions
{
    public static class LineExtensions
    {
        public static async Task<Header?> GetHistoricalHeaderAsync(
            this Line line,
            ApplicationDbContext dbContext)
        {
            if (!line.HeaderId.HasValue)
            {
                return null;
            }

            var header = line.Header
                ?? await dbContext.Headers.FirstOrDefaultAsync(h => h.Id == line.HeaderId.Value);

            if (header != null && Constants.HeaderStatus.HistoricalStatuses.Contains(header.Status ?? string.Empty))
            {
                return header;
            }

            return null;
        }

        public static async Task<int> MarkAsDeletedAndRemoveReservationsAsync(
            this Line line,
            ApplicationDbContext dbContext,
            ILogger logger)
        {
            // Check if this line belongs to a historical order if so preserve the reservations.
            var historicalHeader = await line.GetHistoricalHeaderAsync(dbContext);
            if (historicalHeader != null)
            {
                logger.LogInformation(
                    "Preserving reservations for historical order line {AgreementLineNumber} (Header Status: {Status}).",
                    line.AgreementLineNumber,
                    historicalHeader.Status);

                line.IsDeleted = true;
                line.AgreementLineNumber = $"{line.AgreementLineNumber}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
                line.LastUpdatedDate = DateTime.UtcNow;

                return 0;
            }

            var reservations = await dbContext.Reservations
                .Where(r => r.LineId == line.Id)
                .ToListAsync();

            foreach (var reservation in reservations)
            {
                dbContext.Reservations.Remove(reservation);
            }

            line.IsDeleted = true;
            line.AgreementLineNumber = $"{line.AgreementLineNumber}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            line.LastUpdatedDate = DateTime.UtcNow;

            logger.LogInformation($"Marked line {line.AgreementLineNumber} as deleted and removed {reservations.Count} associated reservations");

            return reservations.Count;
        }
    }
}
