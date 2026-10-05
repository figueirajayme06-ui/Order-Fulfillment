namespace OF.UI.Controllers
{
    public record EditHeaderRequest(
        string OnHireDate,
        string OffHireDate,
        string? RateType,
        string? JobAic,
        string? ProjectCode,
        string? Ponumber,
        string? Poamount,
        string? RentalDuration,
        string? MinimumRental,
        string? DaysInWeek,
        string? WeeksInMonth,
        string? PriceBase,
        string? TargetCustomerAmount,
        string? PriceAdjustment);
}
