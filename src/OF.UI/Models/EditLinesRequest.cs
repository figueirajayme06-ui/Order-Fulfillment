using System.ComponentModel.DataAnnotations;

namespace OF.UI.Models
{
    public class EditLinesRequest
    {
        public string ChangeId { get; set; }

        public string LineId { get; set; }

        [Required]
        public string GenericItemNumber { get; set; }

        [Required]
        public string Quantity { get; set; }

        public string Attributes { get; set; }

        public string DeliveryDate { get; set; }

        [Required]
        public string OnHireDate { get; set; }

        [Required]
        public string OffHireDate { get; set; }

        public string TerminationDate { get; set; }

        public string CollectionDate { get; set; }

        public string Price { get; set; }

        public bool Delete { get; set; }
    }
}