namespace OF.UI.Models
{
    public class BulkActionRequest
    {
        public int HeaderId { get; set; }
        public bool All { get; set; }
        public bool IsDepotFulfil { get; set; }
        public bool IsRehire { get; set; }
        public string Warehouse { get; set; }
        public int[] Items { get; set; }
    }
}
