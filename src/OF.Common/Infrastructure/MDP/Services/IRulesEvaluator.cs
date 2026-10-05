using OF.Common.Infrastructure.MDP.Models;

namespace OF.Common.Infrastructure.MDP.Services
{
    public interface IRulesEvaluator
    {
        Task<IList<ConfigurationRule>> GetRulesForProduct(string productId);
    }
}
