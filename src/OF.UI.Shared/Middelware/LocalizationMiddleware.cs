using OF.Data.Database;
using OF.Data.Enums;
using OF.UI.Database;
using OF.Common.Utils;
using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Http;

namespace OF.UI.Middelware
{
    public class LocalizationMiddleware
    {
        private readonly RequestDelegate _next;

        public LocalizationMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, IDataRepository repository)
        {
            string? email = context.User.Identity.Name;

            if (string.IsNullOrWhiteSpace(email))
            {
                await _next(context);
                return;
            }

            User user = repository.GetUser(email);

            if (user == null)
            {
                await _next(context);
                return;
            }

            var cultureQuery = ((Languages)user.Language).GetCultureforLanguage();

            var culture = new CultureInfo(cultureQuery);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;

            var requestCulture = new RequestCulture(culture);
            context.Features.Set<IRequestCultureFeature>(new RequestCultureFeature(requestCulture, null));

            await _next(context);
        }
    }
}
