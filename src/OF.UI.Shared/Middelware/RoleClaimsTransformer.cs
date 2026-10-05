using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using OF.UI.Database;
using System.Security.Claims;

public class RoleClaimsTransformer : IClaimsTransformation
{
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public RoleClaimsTransformer(IServiceScopeFactory serviceScopeFactory)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var identity = (ClaimsIdentity)principal.Identity;

        if (identity == null || !identity.IsAuthenticated)
            return principal;

        var userEmail = identity.FindFirst(ClaimTypes.Name)?.Value;
        if (string.IsNullOrEmpty(userEmail))
            return principal;

        using (var scope = _serviceScopeFactory.CreateScope())
        {
            var dataRepository = scope.ServiceProvider.GetRequiredService<IDataRepository>();

            var user = await dataRepository.GetUserAsync(userEmail);
            if (user != null)
            {
                foreach (var role in user.ActiveRoles)
                {
                    identity.AddClaim(new Claim(ClaimTypes.Role, role));
                }
            }
        }

        return principal;
    }
}