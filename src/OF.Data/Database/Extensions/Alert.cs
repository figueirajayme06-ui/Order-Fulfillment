using System.ComponentModel.DataAnnotations.Schema;

namespace OF.Data.Database;

public partial class Alert
{
    [NotMapped]
    public int HeaderId { get; set; }
}