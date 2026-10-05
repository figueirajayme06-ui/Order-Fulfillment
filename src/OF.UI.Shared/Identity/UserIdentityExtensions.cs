using Microsoft.AspNetCore.Http;
using OF.Data.Database;

namespace OF.UI.Identity
{
    public static class UserIdentityExtensions
    {
        public static bool DivisionValidForUser(this User id, string division, IRequestCookieCollection? requestCookies = null)
            => GetDivisionsForUser(id, requestCookies).Contains(division);

        public static string[] GetDivisionsForUser(this User id, IRequestCookieCollection? requestCookies = null)
        {
            if(string.IsNullOrWhiteSpace(id?.Division)) 
            {
                return Array.Empty<string>();
            }

            string[] userDivisions = id.Division.Split(",", StringSplitOptions.TrimEntries);

            if (requestCookies != null) 
            {
                var cookieDivisions = requestCookies["user_divisions"];
                if (!string.IsNullOrWhiteSpace(cookieDivisions))
                {
                    return cookieDivisions.Split(",", StringSplitOptions.TrimEntries).Where(cd => userDivisions.Contains(cd)).ToArray(); 
                }
            }

            return userDivisions;
        }
    }
}
