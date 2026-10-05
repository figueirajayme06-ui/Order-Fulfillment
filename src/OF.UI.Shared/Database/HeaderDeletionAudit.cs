namespace OF.UI.Database
{
    public class HeaderDeletionAudit
    {
        public int HeaderId { get; set; }

        public string? QuotePublicId { get; set; }

        public bool WasHeaderAlreadyDeleted { get; set; }

        public int ActiveLinesMarkedDeleted { get; set; }

        public int AlreadyDeletedLines { get; set; }

        public int ReservationsDeleted { get; set; }

        public string? DeletedBy { get; set; }

        public DateTime DeletedAtUtc { get; set; }
    }
}