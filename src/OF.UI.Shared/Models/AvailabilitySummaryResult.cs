namespace OF.UI.Models
{
    public class AvailabilitySummaryResult
    {
        public string WarehouseCode { get; set; } = string.Empty;
        public string Warehouse { get; set; } = string.Empty;
        public string GenericCode { get; set; } = string.Empty;
        public string GenericDescription { get; set; } = string.Empty;
        public string ItemNumber { get; set; } = string.Empty;
        public string DescriptionIntl { get; set; } = string.Empty;
        public string Facility { get; set; } = string.Empty;
        public string DivisionCode { get; set; } = string.Empty;
        public string DivisionName { get; set; } = string.Empty;
        public int Available { get; set; }
        public int Count { get; set; }
        public bool GenericOnly { get; set; }
        public string ReservationMode { get; set; } = "asset";
        public string? SubstitutionReason { get; set; }
    }
}
