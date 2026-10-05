using OF.Common.Infrastructure.IPG.Orders.Enums;

namespace OF.Common.Infrastructure.IPG.Orders.Models.Orders;

public class OrderLineDeleteRequest : ICorrelatable
{
    public OrderLineDeleteRequest()
    {
        CorrelationId = Guid.NewGuid().ToString();
        Company = Constants.IPG.Company;
        ReasonCodeDeleted = ReasonCode.T04;
    }
    public required string AgreementNumber { get; set; }

    public required string AgreementLineNumber { get; set; }

    public string Company { get; }

    public required string Division { get; set; }

    public string ReasonCodeDeleted { get; }

    public string CorrelationId { get; set; }
}
