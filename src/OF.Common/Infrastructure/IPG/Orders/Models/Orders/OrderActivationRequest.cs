using OF.Common.Infrastructure.IPG.Orders.Enums;

namespace OF.Common.Infrastructure.IPG.Orders.Models.Orders;

public class OrderActivationRequest : ICorrelatable
{
    public OrderActivationRequest()
    {
        ReasonCodeCreated = ReasonCode.C09;
        AgreementType = ((int)OrderActivationAgreementType.ToActive).ToString();
        AgreementVersion = Constants.IPG.AgreementVersion;
        CorrelationId = Guid.NewGuid().ToString();
        Company = Constants.IPG.Company;
        Timestamp = DateTime.UtcNow.ToString("ddMMyyyy");
    }

    public required string AgreementNumber { get; set; }

    public string ReasonCodeCreated { get; }

    public required string Division { get; set; }

    public string AgreementVersion { get; }

    public string Timestamp { get; }

    public string AgreementType { get; }

    public string CorrelationId { get; }

    public string Company { get; }
}
