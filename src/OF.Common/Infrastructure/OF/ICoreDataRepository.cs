using OF.Data.Database;

namespace OF.Common.Infrastructure.OF
{
    public interface ICoreDataRepository
    {
        Header? GetHeader(int id);
        Header? GetHeaderWithChanges(int id);
        Header? GetHeaderWithLines(int id);
        IQueryable<Line> GetNonServiceLines(int headerId);
        double GetReservationSumForLine(int lineId);
        Header UpdateHeader(Header header, string? loginName = null);
        Line UpdateLine(Line line, string? loginName = null);

        /// <summary>
        /// Gets a warehouse by code
        /// </summary>
        ValueTask<WarehouseItem?> GetWarehouseByCode(string code, CancellationToken cancellationToken = default);
    }
}