using OF.Data.Database;
using System.Security.Principal;

namespace OF.Common.Infrastructure.OF
{
    public interface ICoreFulfilmentEngine
    {
        void RecalculateStatusForHeader(Header header, string? identity = null);

        void RecalculateStatusForLineAndHeader(Line line, string? identity = null);

        string CalcNextAgreementLineIndex(Line line, Line[] children);

        int AbandonOrphanedQuoteLines(int headerId, string? identity = null);
    }
}