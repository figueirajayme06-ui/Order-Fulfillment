namespace OF.Common.Models
{
    public class ItemsFromAttributes
    {
        public required string ItemNumber { get; set; }
               
        public required string LotNumber { get; set; }
               
        public required string Warehouse { get; set; }
               
        public bool IsDepotFulfil { get; set; }
               
        public bool IsRehire { get; set; }
               
        public required float Quantity { get; set; }

        public bool ContainsAllocation { get; set; } = false;
    }
}
