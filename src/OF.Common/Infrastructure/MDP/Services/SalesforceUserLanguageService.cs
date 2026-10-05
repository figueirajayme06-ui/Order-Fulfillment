using Microsoft.EntityFrameworkCore;
using OF.Common.Infrastructure.MDP.Models;
using OF.Data;
using System.Text.RegularExpressions;

namespace OF.Common.Infrastructure.MDP.Services
{
    public class SalesforceUserLanguageService : ISalesforceUserLanguageService
    {
        private readonly FDPDbContext context;

        public SalesforceUserLanguageService(FDPDbContext context)
        {
            this.context = context;
        }


        private static string EscapeForSoql(string input)
        {
            return input?.Replace("'", "''") ?? string.Empty;
        }

        public async Task<UserLanguage?> GetUserLanguageAsync(string userName)
        {
            if (string.IsNullOrEmpty(userName))
            {
                throw new Exception("User name could not be determined from the claims.");
            }

            if (!Regex.IsMatch(userName, "^[a-zA-Z0-9.@]+$"))
            {
                throw new ArgumentException("Invalid user name format.", nameof(userName));
            }

            try
            {
                const string soqlTemplate = @"SELECT Id, LanguageLocaleKey FROM salesforce_raw_bronze_db.user WHERE username like '{0}%'";

                var soql = string.Format(soqlTemplate, EscapeForSoql(userName.Split("@aggreko")[0]) + "@aggreko");
                var users = await context.Set<UserLanguage>().FromSqlRaw(soql).ToListAsync();

                UserLanguage? userLanguage = null;
                foreach (var user in users)
                {
                    userLanguage = user;
                    break;
                }

                if (userLanguage == null)
                {
                    return null; // User not found or inactive
                }

                return userLanguage;
            }
            catch (Exception ex)
            {
                throw new Exception("An error occurred while fetching user language.", ex);
            }
        }
    }
}
