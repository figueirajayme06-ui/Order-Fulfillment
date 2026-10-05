namespace OF.Common.Infrastructure.IPG.Orders.Models.Orders;

public class OrderLineUpdateRequest : ICorrelatable
{
    public OrderLineUpdateRequest()
    {
        Company = Constants.IPG.Company;
        AgreementLineSuffix = Constants.IPG.AgreementLineSuffix;
        OrderSource = Constants.IPG.OrderSource;
        CorrelationId = Guid.NewGuid().ToString();
    }

    public required string AgreementLineNumber { get; set; }

    public required string AgreementNumber { get; set; }

    public required string Division { get; set; }

    public required string Facility { get; set; }

    public required string ItemAttributesAsText { get; set; }

    public required string FromWarehouse { get; set; }

    public required string OrderedQuantity { get; set; }

    public string? OrderItemLine { get; set; }

    public string? OrderItemRecordId { get; set; }

    public string? QuoteLineRecordId { get; set; }

    public string AgreementLineSuffix { get; }

    public string Company { get; }

    public string OrderSource { get; }

    public string CorrelationId { get; }

    public string LinkedAgreementLine { get; set; }
}
