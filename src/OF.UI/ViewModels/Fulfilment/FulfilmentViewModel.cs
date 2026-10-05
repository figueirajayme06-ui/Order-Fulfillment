using OF.Data.Database;
using OF.UI.Helpers;
using OF.UI.Models;
using static OF.Common.Enums;

namespace OF.UI.ViewModels.Asset
{
    public class FulfilmentViewModel
    {
        public Header Header { get; set; } = new Header();

        public Line[] Lines { get; set; }

        public Dictionary<int, string> ReservationText { get; set; }

        public Dictionary<int, string> ReservationDeletedAssetWarnings { get; set; }

        public string[] PackageGroups { get; set; }

        public Rating[] Ratings { get; set; }

        public Rating SelectedRating { get; set; }

        public DateTime? DateFrom { get; set; }

        public DateTime? DateTo { get; set; }

        public CpqService[] Services { get; set; }

        public bool DisplayActivation => IsActivatable && (IsFulfilled || AreNonQuoteLinesFullyFulfilled);

        public bool IsActivatable => this.Header.IsActivatable();

        public ActivationStatus ActivatedStatus
        {
            get
            {
                var isHeaderStatus = Header.ActivationStatus;
                var statuses = Lines.Where(i => !i.IsDeleted).Select(i => i.ActivationStatus).Distinct().ToList();

                if (!statuses.Contains(Header.ActivationStatus))
                {
                    statuses.Add(Header.ActivationStatus);
                }

                if (statuses.All(i => i == (int)ActivationStatus.Activated))
                {
                    return ActivationStatus.Activated;
                }

                if (statuses.Any(i => i == (int)ActivationStatus.Failed))
                {
                    return ActivationStatus.Failed;
                }

                if (statuses.Any(i => i == (int)ActivationStatus.Requested))
                {
                    return ActivationStatus.Requested;
                }

                return ActivationStatus.TODO;
            }
        }

        public bool IsActivationStale
        {
            get
            {
                if (ActivatedStatus != ActivationStatus.Requested)
                {
                    return false;
                }

                var now = DateTime.UtcNow;
                var fiveMinutesAgo = now.AddMinutes(-5);

                // Check if header is in Requested status and the LastUpdatedDate is older than 5 minutes
                // (meaning it was set to Requested more than 5 minutes ago and hasn't been updated since)
                if (Header.ActivationStatus == (int)ActivationStatus.Requested && 
                    Header.LastUpdatedDate.HasValue && 
                    Header.LastUpdatedDate.Value <= fiveMinutesAgo)
                {
                    return true;
                }

                // Check if any line in Requested status has a LastUpdatedDate older than 5 minutes
                var requestedLines = Lines.Where(i => !i.IsDeleted && i.ActivationStatus == (int)ActivationStatus.Requested);
                if (requestedLines.Any(l => l.LastUpdatedDate.HasValue && l.LastUpdatedDate.Value <= fiveMinutesAgo))
                {
                    return true;
                }

                return false;
            }
        }

        public bool DisplayOrderSummary => IsHeaderValidForOrderSummary && IsOrderSummaryFulfilled;

        public bool IsHeaderValidForOrderSummary => Header.AgreementNumber?.ToUpper()?.StartsWith("A") == true;

        public bool IsHeaderAQuote => Header.AgreementNumber?.ToUpper()?.StartsWith("Q") == true;

        public Dictionary<int, string> ReservationWarehouseText { get; set; }

        private bool IsOrderSummaryFulfilled => IsFulfilled;

        private bool IsFulfilled => Header.FulfilmentStatus is (int)FulfilmentStatus.FullyFulfiled or (int)FulfilmentStatus.OverFulfilled;

        private bool AreNonQuoteLinesFullyFulfilled
        {
            get
            {
                if (Lines == null)
                {
                    return false;
                }

                var nonQuoteLines = Lines
                    .Where(l => !l.IsDeleted && l.RequiresFulfilment && (l.AgreementLineNumber == null || !l.AgreementLineNumber.ToUpper().StartsWith("Q")))
                    .ToList();

                return nonQuoteLines.Count > 0 && nonQuoteLines
                    .All(l => l.FulfilmentStatus == (int)FulfilmentStatus.FullyFulfiled || l.FulfilmentStatus == (int)FulfilmentStatus.OverFulfilled);
            }
        }
    }
}
