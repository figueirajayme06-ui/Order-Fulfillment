using OF.Common.Infrastructure.IPG.Pricing.Models;

namespace ExternalConfigurator.Controllers
{
    public record AgreementQuery(
        string AgreementNumber,
        string Attributes,
        PricingRequest PricingRequest,
        string? LineId,
        decimal Quantity
        );
}
