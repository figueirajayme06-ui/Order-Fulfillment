namespace OF.UI.Models
{
    public class AddLineRequest
    {
        public int LineId { get; set; }
        public bool ByGeneric { get; set; }
        public int GenericId { get; set; }
        public string? Attributes { get; set; }
        public string ItemNumber { get; set; }
        public int Quantity { get; set; }
    }
}
