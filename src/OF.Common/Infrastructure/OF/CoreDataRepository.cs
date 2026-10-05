using Microsoft.EntityFrameworkCore;
using OF.Data;
using OF.Data.Database;

namespace OF.Common.Infrastructure.OF
{
    public class CoreDataRepository : ICoreDataRepository
    {
        private readonly ApplicationDbContext _dbContext;

        public CoreDataRepository(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public IQueryable<Line> GetNonServiceLines(int headerId)
        {
            return _dbContext.Lines.FromSql(@$"
SELECT l.*
from Lines l
LEFT JOIN CPQ_Service s ON l.ItemNumber = s.ProductCode
WHERE s.Id IS NULL AND l.HeaderId={headerId} AND l.IsDeleted=0 AND l.RequiresFulfilment=1");
        }

        public Header UpdateHeader(Header header, string? loginName = null)
        {
            header.LastUpdatedDate = DateTime.UtcNow;
            header.LastUpdatedBy = UpdateLastUpdatedBy(header.LastUpdatedBy, loginName); 
            _dbContext.Headers.Update(header);
            _dbContext.SaveChanges();
            return header;
        }

        public double GetReservationSumForLine(int lineId)
        {
            return _dbContext.Reservations.Where(r => r.LineId == lineId).Sum(r => r.Quantity);
        }

        public Header? GetHeader(int id)
        {
            return _dbContext.Headers.FirstOrDefault(h => h.Id == id);
        }

        public Header? GetHeaderWithChanges(int id)
        {
            return _dbContext.Headers
                .Include(i => i.Lines)
                .Include(i => i.ChangeOrders)
                .ThenInclude(i => i.ChangeOrderHeader)
                .Include(i => i.ChangeOrders)
                .ThenInclude(i => i.InvoiceAddress)
                .Include(i => i.ChangeOrders)
                .ThenInclude(i => i.SiteAddress)
                .Include(i => i.ChangeOrders)
                .ThenInclude(i => i.Armcontact)
                .Include(i => i.ChangeOrders)
                .ThenInclude(i => i.PrimaryContact)
                .Include(i => i.ChangeOrders)
                .ThenInclude(i => i.SiteContact)
                .Include(i => i.ChangeOrders)
                .ThenInclude(i => i.BillingContact)
                .Include(i => i.ChangeOrders)
                .ThenInclude(i => i.ChangeOrderLines)
                .Include(i => i.ChangeOrders)
                .ThenInclude(i => i.ChangeOrderComments)
                .FirstOrDefault(h => h.Id == id);
        }

        public Header? GetHeaderWithLines(int id)
        {
            return _dbContext.Headers.Include(i => i.Lines).FirstOrDefault(h => h.Id == id);
        }

        public Line UpdateLine(Line line, string? loginName = null)
        {
            line.LastUpdatedDate = DateTime.UtcNow;
            line.LastUpdatedBy = UpdateLastUpdatedBy(line.LastUpdatedBy, loginName);
            _dbContext.Lines.Update(line);
            _dbContext.SaveChanges();
            return line;
        }

        private string UpdateLastUpdatedBy(string? current, string? requested)
        {
            // If this empty then update it with the requested, if thats null default to NOF.

            if (string.IsNullOrWhiteSpace(current))
            {
                return requested ?? Constants.IPG.OrderSource;
            }
            else
            {
                // If its populated only update it if it isnt null or equal to NOF.

                if (!string.IsNullOrWhiteSpace(requested) && requested != Constants.IPG.OrderSource)
                {
                    return requested;
                }
            }

            // Or just return the original
            return current;
        }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        public ValueTask<WarehouseItem?> GetWarehouseByCode(string code, CancellationToken cancellationToken = default)
        {
            return new(_dbContext.WarehouseItems.FirstOrDefaultAsync(x => x.WarehouseCode == code, cancellationToken));
        }
    }
}
