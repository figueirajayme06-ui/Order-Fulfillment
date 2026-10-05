using System.Threading.Tasks;
using Refit;
using OF.Common.Infrastructure.IPG.Pricing.Models;

namespace OF.Common.Infrastructure.IPG.Pricing
{
    public interface IPricingApi
    {
        [Post("/calculation")]
        Task<CalculationResult> CalculateAsync([Body] CalculationRequest request);
    }
}
