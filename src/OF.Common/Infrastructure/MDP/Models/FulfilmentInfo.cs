namespace OF.Common.Infrastructure.MDP.Models
{
    public class FulfilmentInfo
    {
        public string? WarehouseCode { get; set; }
        public string? Warehouse { get; set; }
        public string? ItemNumber { get; set; }
        public string? DescriptionIntl { get; set; }
        public int Available { get; set; }
        public int Count { get; set; }
    }
}
