using OF.Common.Infrastructure.MDP.Models;

namespace OF.Common.Infrastructure.MDP.Services
{
    public interface ISalesforceUserLanguageService
    {
        Task<UserLanguage?> GetUserLanguageAsync(string userName);
    }
}