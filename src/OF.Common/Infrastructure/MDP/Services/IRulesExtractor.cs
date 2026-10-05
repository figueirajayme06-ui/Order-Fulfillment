using OF.Common.Infrastructure.MDP.Models;

namespace OF.Common.Infrastructure.MDP.Services
{
    public interface IRulesExtractor
    {
        Task<IList<ConfigurationRule>> LoadConfigurationRules();
        Task<IList<ErrorCondition>> LoadErrorConditions();
        Task<IList<ProductAction>> LoadProductActions();
        Task<IList<ProductRule>> LoadProductRules();
        Task<IList<SummaryVariable>> LoadSummaryVariables();
    }
}