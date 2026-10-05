using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OF.Api.UseCases.Reservations;
public class ReservationDto
{
    public int Id { get; set; }
    public string? AssetId { get; set; }
    public string? ItemNumber { get; set; }
    public int Quantity { get; set; }
    public int LineId { get; set; }
    public string? Attributes { get; set; } 
}

