using OF.Data.Database;
using System.ComponentModel.DataAnnotations;

namespace OF.UI.Models
{
    public class ApproveOrRejectRequest
    {
        [Required]
        public string Comment { get; set; }

        public string Value { get; set; }

        public ChangeStatus Status
        {
            get
            {
                if (bool.TryParse(Value, out var changeStatus))
                {
                    return changeStatus ? ChangeStatus.Approved : ChangeStatus.Rejected;
                }

                return ChangeStatus.Rejected;
            }
        }
    }
}
