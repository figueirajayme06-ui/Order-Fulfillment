namespace OF.UI.Models
{
    public class ReserveRequest
    {
        public bool IsDelete { get; set; }
        public int LineId { get; set; }
        public int ReservationId { get; set; }
        public string ItemNumber { get; set; }
        public string Warehouse { get; set; }
        public string AssetId { get; set; }
        public bool IsSerialized { get; set; }
        public int MinAvailableInPeriod { get; set; }
        public bool IsRehire { get; set; }
        public bool IsDepotFulfiled { get; set; }
        public int Multiple { get; set; }
        public int Quantity { get; set; }

    }
}
