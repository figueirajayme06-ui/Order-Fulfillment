using OF.Data.Database;
using System.ComponentModel.DataAnnotations;

namespace OF.UI.Models
{
    public class AddCommentRequest
    {
        [Required]
        public string Comment { get; set; }
    }
}
