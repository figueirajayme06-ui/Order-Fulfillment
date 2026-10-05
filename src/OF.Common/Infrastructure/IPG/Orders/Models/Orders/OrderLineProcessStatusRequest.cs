namespace OF.Common.Infrastructure.IPG.Orders.Models.Orders;

public class OrderLineProcessStatusRequest : BaseProcessStatusRequest
{
    public required string AgreementLineNumber { get; set; }

    public int OrderLineProcessStatus { get; set; }
}
