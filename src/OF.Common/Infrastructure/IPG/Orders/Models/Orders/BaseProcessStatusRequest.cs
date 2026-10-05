namespace OF.Common.Infrastructure.IPG.Orders.Models.Orders;

public abstract class BaseProcessStatusRequest
{
    public BaseProcessStatusRequest()
    {
        Company = Constants.IPG.Company;
    }

    public required string AgreementNumber { get; set; }

    public string Company { get; }

    public required string Division { get; set; }

    public required string Facility { get; set; }
}
