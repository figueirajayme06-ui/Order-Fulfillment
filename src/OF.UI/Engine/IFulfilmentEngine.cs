using OF.Common.Infrastructure.OF;
using OF.Data.Database;
using OF.UI.Identity;
using OF.UI.Models;

namespace OF.UI.Engine
{
    public interface IFulfilmentEngine : ICoreFulfilmentEngine
    {
        FulfilmentResponse SatisfyLine(FulfilmentRequest request);

        StockResponse GetStock(IUserIdentity userIdentity, StockRequest request, string[] divisions);

        ReserveResponse Reserve(ReserveRequest request);

        BulkActionResponse BulkAction(BulkActionRequest request);
    }
}
