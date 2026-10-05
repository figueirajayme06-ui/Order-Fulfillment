namespace OF.Common.Infrastructure.IPG.Orders.Models.Orders;

public class AdditionalChargeRequest
{
    public required string Charge { get; set; }

    public required string ChargeCheckinQty { get; set; }

    public required string ChargeCheckoutQty { get; set; }

    public required string ChargeItem { get; set; }

    public required string ChargeSuffix { get; set; }

    public required string ChargeType { get; set; }

    public required string MeterUnitPrice { get; set; }

    public required string OverusageMeterUnitPrice { get; set; }

    public required string AgreedMeterHours { get; set; }
}
