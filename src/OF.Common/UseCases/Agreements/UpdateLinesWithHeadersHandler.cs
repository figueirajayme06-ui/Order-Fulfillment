using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OF.Data;

namespace OF.Common.UseCases.Agreements
{
    public class UpdateLinesWithHeadersHandler
    {
        private readonly ApplicationDbContext dbContext;
        private readonly ILogger logger;

        public UpdateLinesWithHeadersHandler(
            ApplicationDbContext dbContext,
            ILogger logger)
        {
            this.dbContext = dbContext;
            this.logger = logger;
        }

        public async Task Handle(int headerId)
        {
            try
            {
                if (headerId <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(headerId), "HeaderId must be greater than zero.");
                }

                // Update Headers OnHireDate and OffHireDate from Lines
                // Since this query is already filtered by headerId, we only want to update headers that actually have lines
                await dbContext.Database.ExecuteSqlInterpolatedAsync($@"
                    UPDATE h
                    SET h.OnHireDate = l.OnHireDate, h.OffHireDate = l.OffHireDate
                    FROM [dbo].[Headers] h
                    INNER JOIN (select Min(ValidFromDate) as OnHireDate, max(ValidToDate) as OffHireDate, HeaderId from [dbo].[Lines] group by HeaderId) l on h.Id = l.HeaderId
                    WHERE h.Id = {headerId} AND (h.OnHireDate IS NULL Or h.OffHireDate IS NULL)");

                // Update Headers ActivationStatus to 3 (Activated) where AgreementNumber starts with 'A' and current status is 2 (Requested)
                await dbContext.Database.ExecuteSqlInterpolatedAsync($@"
                    UPDATE h
                    SET h.ActivationStatus = 3, h.LastUpdatedDate = GETUTCDATE()
                    FROM [dbo].[Headers] h
                    WHERE h.Id = {headerId} AND h.AgreementNumber like 'A%' AND h.ActivationStatus = 2");
                
                await dbContext.SaveChangesAsync();
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Error updating lines with header id. {HeaderId}", headerId);
                throw;
            }
        }
    }
}
