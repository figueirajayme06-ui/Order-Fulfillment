namespace OF.UI.Models
{
    public class Substitution
    {
        public int GenericId { get; set; }

        public string GenericCode { get; set; } = null!;

        public string? GenericDescription { get; set; }

        public string Purpose { get; set; }
        public string? UomIntl { get; set; }

        public string? RatingIntl { get; set; }

        public string? UomUs { get; set; }

        public string? RatingUs { get; set; }

        public string? Rehire { get; set; }
    }
}
