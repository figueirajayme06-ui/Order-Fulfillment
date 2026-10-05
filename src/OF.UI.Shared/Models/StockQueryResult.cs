using OF.Data.Database;

namespace OF.UI.Models
{
    public class SerializedQueryResult
    {
        public CpqGeneric Generic { get; set; }
        public Asset Asset { get; set; }
        public string? SubstitutionReason { get; set; }
        public int SubstitutionMultiple { get; set; }
        public IList<StockReservation> Reservations { get; set; }
        public string WarehouseName { get; set; }
        public int NoteCount { get; set; }
    }

    public class NonSerializedQueryResult
    {
        public CpqGeneric Generic { get; set; }
        public ProductItem Asset { get; set; }
        public string? SubstitutionReason { get; set; }
        public int SubstitutionMultiple { get; set; }
        public IList<StockReservation> Reservations { get; set; }
        public string WarehouseName { get; set; }
    }

    public class StockReservation
    {
        public int ReservationId { get; set; }
        public int LineId { get; set; }
        public string CustomerName { get; set; }
        public string CustomerNumber { get; set; }
        public string AgreementNumber { get; set; }
        public string? AgreementLineNumber { get; set; }
        public DateTime? DeliveryDate { get; set; }
        public DateTime? ValidFromDate { get; set; } 
        public DateTime? ValidToDate { get; set; }  
        public DateTime? TerminationDate { get; set; }
        public string Notes { get; set; }
        public int Quantity { get; set; }
        public bool IsConfirmed { get; internal set; }
    }
}
