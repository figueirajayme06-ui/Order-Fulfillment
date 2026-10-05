using OF.Data.Database;
using System.Security.Principal;

namespace OF.Common.Infrastructure.OF
{
    public class CoreFulfilmentEngine : ICoreFulfilmentEngine
    {
        private readonly ICoreDataRepository _repository;

        public CoreFulfilmentEngine(ICoreDataRepository repository)
        {
            _repository = repository;
        }

        public virtual void RecalculateStatusForHeader(Header header, string? identity = null)
        {
            var lines = _repository.GetNonServiceLines(header.Id);

            var activeLines = lines.Where(l => l.AgreementLineNumber != null && !l.IsDeleted)
                .ToList();
            var nonQuoteLines = activeLines.Where(l => !l.AgreementLineNumber!.ToUpper().StartsWith("Q"))
                .ToList();
            var fulfilmentLines = nonQuoteLines.Count > 0 ? nonQuoteLines : activeLines;

            var status = FulfilmentStatus.Unfulfilled;

            if (fulfilmentLines.Count > 0)
            {
                // Headers cannot be overfulfiled - only lines
                if (fulfilmentLines.FirstOrDefault(l => l.FulfilmentStatus != (int)FulfilmentStatus.Unfulfilled) != null) status = FulfilmentStatus.PartiallyFulfilled;
                if (fulfilmentLines.Count(l => l.FulfilmentStatus == (int)FulfilmentStatus.FullyFulfiled) == fulfilmentLines.Count) status = FulfilmentStatus.FullyFulfiled;
            }

            header.FulfilmentStatus = (int)status;
            _repository.UpdateHeader(header, identity);
        }

        public virtual void RecalculateStatusForLineAndHeader(Line line, string? identity = null)
        {
            var fulfilled = _repository.GetReservationSumForLine(line.Id);
            var remaining = line.Quantity - fulfilled;
            line.QuantityFulfilled = fulfilled;

            if (line.RequiresFulfilment)
            {
                if (remaining <= 0)
                {
                    line.FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled;
                }
                else if (remaining > 0 && fulfilled > 0)
                {
                    line.FulfilmentStatus = (int)FulfilmentStatus.PartiallyFulfilled;
                }
                else
                {
                    line.FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled;
                }
            }
            else
            {
                line.FulfilmentStatus = (int)FulfilmentStatus.FullyFulfiled;
            }

            _repository.UpdateLine(line, identity);

            if (line.HeaderId.HasValue)
            {
                var header = _repository.GetHeader(line.HeaderId.Value);
                RecalculateStatusForHeader(header!, identity);
            }
        }

        public string CalcNextAgreementLineIndex(Line line, Line[] children)
        {
            int index = 1;
            if (children.Length > 0)
            {
                index = children.Where(l => !string.IsNullOrWhiteSpace(l.AgreementLineNumber)).Max(l => int.Parse(l.AgreementLineNumber!.Substring(line.AgreementLineNumber!.Length + 1))) + 1;
            }
            return line.AgreementLineNumber + "." + index.ToString();
        }

        public virtual int AbandonOrphanedQuoteLines(int headerId, string? identity = null)
        {
            var orphanedLines = _repository.GetNonServiceLines(headerId)
            .Where(l => l.AgreementLineNumber != null &&
                        l.AgreementLineNumber.ToUpper().StartsWith("Q") &&
                        !l.IsDeleted)
            .ToList();

            foreach (var orphanedLine in orphanedLines)
            {
                orphanedLine.IsDeleted = true;
                orphanedLine.LastUpdatedDate = DateTime.UtcNow;
                orphanedLine.LastUpdatedBy = identity ?? "System";
                _repository.UpdateLine(orphanedLine, identity);
            }

            return orphanedLines.Count;
        }
    }
}
