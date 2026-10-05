using System.Security.Claims;
using System.Security.Principal;

namespace OF.WebApp.Middleware;

/// <summary>
/// Parses Azure App Service EasyAuth headers and establishes a ClaimsPrincipal.
/// In production, Azure App Service injects x-ms-client-principal-id and
/// x-ms-client-principal-name headers after authenticating the user.
/// In Development, falls back to LocalUserName/LocalUserEmail from configuration.
/// </summary>
public class EasyAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;

    public EasyAuthMiddleware(RequestDelegate next, IConfiguration configuration, IHostEnvironment environment)
    {
        _next = next;
        _configuration = configuration;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var principalId = context.Request.Headers["x-ms-client-principal-id"].FirstOrDefault();
        var principalName = context.Request.Headers["x-ms-client-principal-name"].FirstOrDefault();

        // In Development, fall back to config values if no EasyAuth headers
        if (string.IsNullOrEmpty(principalId) && _environment.IsDevelopment())
        {
            principalName = _configuration["LocalUserEmail"];
            principalId = principalName;
        }

        if (!string.IsNullOrEmpty(principalId))
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, principalId),
            };

            if (!string.IsNullOrEmpty(principalName))
            {
                claims.Add(new Claim(ClaimTypes.Name, principalName));
            }

            var identity = new ClaimsIdentity(claims, "EasyAuth");
            context.User = new ClaimsPrincipal(identity);
        }

        await _next(context);
    }
}
