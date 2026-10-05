namespace OF.UI.Models;

public sealed class AssetScheduleReservation
{
    public int ReservationId { get; init; }
    public int LineId { get; init; }
    public int? HeaderId { get; init; }
    public string? AgreementNumber { get; init; }
    public string? CustomerName { get; init; }
    public string? Warehouse { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public bool IsConfirmed { get; init; }
}
