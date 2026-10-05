namespace OF.UI.Models
{
    public class Event
    {
        public string? AssetId { get; set; }
        public string? EventType { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Title { get; set; }
        public string? CssClass { get; set; }
    }
}
