namespace OF.UI.Models
{
    public class StockRequest
    {
        public int LineId { get; set; }  
        public bool IsSerialized { get; set; }
        public string Warehouse { get; set; }
        public string[] Attributes { get; set; }
    }
}
