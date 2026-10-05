namespace OF.Common.Infrastructure.IPG.Orders.Models.Orders;

public class OrderLineCreateRequest : ICorrelatable
{
    public OrderLineCreateRequest()
    {
        OrderSource = Constants.IPG.OrderSource;
        CorrelationId = Guid.NewGuid().ToString();
        Company = Constants.IPG.Company;
    }

    public required string AgreementNumber { get; set; }

    public required string AgreementLineType { get; set; }

    public required string CustomerSiteAccount { get; set; }

    public required string CustomerSiteAddress { get; set; }

    public required string DeliveryDate { get; set; }

    public required string DeliveryStartTime { get; set; }

    public required string Division { get; set; }

    public required string Facility { get; set; }

    public required string FromWarehouse { get; set; }

    public required string ItemNumber { get; set; }

    public required string NumberOfShifts { get; set; }

    public required string OrderedQuantity { get; set; }

    public string? OrderItemLine { get; set; }

    public string? OrderItemRecordId { get; set; }

    public string? QuoteLineRecordId { get; set; }

    public required string RateType { get; set; }

    public required string ValidFromDate { get; set; }

    public required string ValidToDate { get; set; }

    public string? ItemAttributesAsText { get; set; }

    public string OrderSource { get; }

    public string Company { get; }

    public string CorrelationId { get; }

    public string LinkedAgreementLine { get; set; }

    public List<AdditionalChargeRequest> AdditionalCharges { get; set; } = new List<AdditionalChargeRequest>();
}
