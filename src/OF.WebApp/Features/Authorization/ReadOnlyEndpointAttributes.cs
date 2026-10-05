using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using OF.Common;
using OF.Data.Database;
using OF.UI.Identity;

namespace OF.WebApp.Features.Authorization;

public static class ReadOnlyAccess
{
    public static bool IsReadOnly(User? user) =>
        user?.ActiveRoles.Contains(Constants.Roles.ReadOnly, StringComparer.OrdinalIgnoreCase) == true;

    public static bool HasReadOnlyRole(string? roles) =>
        roles?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(Constants.Roles.ReadOnly, StringComparer.OrdinalIgnoreCase)
        == true;
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class DenyReadOnlyAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var userIdentity = context.HttpContext.RequestServices.GetRequiredService<IUserIdentity>();
        if (ReadOnlyAccess.IsReadOnly(userIdentity.GetIdentity()))
        {
            context.Result = new ForbidResult();
        }
    }
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class ReadOnlyQueryAttribute : Attribute;
