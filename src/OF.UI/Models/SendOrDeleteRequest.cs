using OF.Data.Database;
using System.ComponentModel.DataAnnotations;

namespace OF.UI.Models
{
    public class SendOrDeleteRequest
    {
        [Required]
        public string Comment { get; set; }

        public string Value { get; set; }

        public bool Status
        {
            get
            {
                if (bool.TryParse(Value, out var changeStatus))
                {
                    return changeStatus;
                }

                return false;
            }
        }
    }
}
