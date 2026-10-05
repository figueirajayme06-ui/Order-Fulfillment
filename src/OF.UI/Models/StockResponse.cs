using Microsoft.EntityFrameworkCore;
using OF.Data.Database;

namespace OF.UI.Models
{
    public class StockResponse
    {
        public int LineId { get; set; }

        [Precision(18, 2)]
        public double Quantity { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public string StartDateString { get; set; }

        public string EndDateString { get; set; }

        public bool IsSerialized { get; set; }

        public FulfilmentStatus FulfilmentStatus { get; set; }

        public int Cols { get; set; }

        public SerializedQueryResult[] SerializedItems { get; set; } = null!;

        public NonSerializedQueryResult[] NonSerializedItems { get; set; } = null!;

        public Reservation[] OtherReservations { get; set; } = null!; // Depot fulfil and rehire

        public Bar[][] Bars { get; set; } = null!;

        public bool IsSuccess { get; set; }

        public string ErrorMessage { get; set; }
    }

    public class Bar
    {
        public int Start { get; set; }
        public int End { get; set; }
        public int Quantity { get; set; }
        public string CssClass { get; set; }
        public string Text { get; set; }
        public string ShortText { get; set; }
    }
}
