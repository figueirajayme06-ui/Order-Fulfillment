namespace OF.UI.Models
{
    public class CreateRingfenceRequest
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Name { get; set; }
        public string Owner { get; set; }
        public string Division { get; set; }
        public string Warehouse { get; set; }
    }
}
