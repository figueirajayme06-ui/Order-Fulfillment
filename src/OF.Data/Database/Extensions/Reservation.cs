using System.ComponentModel.DataAnnotations.Schema;

namespace OF.Data.Database;

public partial class Reservation
{
    [NotMapped]
    public bool IsIndividualItem => AssetId != null && ItemNumber != null && AssetId != ItemNumber;
}