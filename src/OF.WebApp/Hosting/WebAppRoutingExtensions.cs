using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Matching;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using System.Text.RegularExpressions;

namespace OF.WebApp.Hosting;

public static class WebAppRoutingExtensions
{
    private const string NonApiConstraintName = "nonapi";
    private const string SpaFallbackPattern = "{*path:nonfile:nonapi}";
    private const string NoStoreCacheControl = "no-store";
    private const string ImmutableCacheControl = "public, max-age=31536000, immutable";
    private static readonly Regex FingerprintedAssetPattern = new(
        @"-[a-zA-Z0-9_-]{8,}\.[a-zA-Z0-9]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IServiceCollection AddWebAppRouting(this IServiceCollection services)
    {
        services.AddHealthChecks();
        services.Configure<RouteOptions>(options =>
        {
            options.SetParameterPolicy<NonApiRouteConstraint>(NonApiConstraintName);
        });

        return services;
    }

    public static IEndpointRouteBuilder MapWebAppEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapControllers();
        endpoints.MapHealthChecks("/health/live");
        endpoints.MapHealthChecks("/health/ready");
        endpoints.MapFallbackToFile(SpaFallbackPattern, "index.html", CreateStaticFileOptions());
        return endpoints;
    }

    public static IApplicationBuilder UseWebAppStaticFiles(this IApplicationBuilder app) =>
        app.UseStaticFiles(CreateStaticFileOptions());

    public static IApplicationBuilder UseWebAppApiCachePolicy(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.Headers.CacheControl = NoStoreCacheControl;
            }

            await next();
        });

    private static StaticFileOptions CreateStaticFileOptions() => new()
    {
        OnPrepareResponse = context =>
        {
            var fileName = Path.GetFileName(context.File.PhysicalPath ?? context.File.Name);
            context.Context.Response.Headers.CacheControl =
                fileName.Equals("index.html", StringComparison.OrdinalIgnoreCase)
                    ? NoStoreCacheControl
                    : FingerprintedAssetPattern.IsMatch(fileName)
                        ? ImmutableCacheControl
                        : "no-cache";
        },
    };
}

public sealed class NonApiRouteConstraint : IRouteConstraint, IParameterLiteralNodeMatchingPolicy
{
    public bool MatchesLiteral(string parameterName, string literal) =>
        !literal.Equals("api", StringComparison.OrdinalIgnoreCase);

    public bool Match(
        HttpContext? httpContext,
        IRouter? route,
        string routeKey,
        RouteValueDictionary values,
        RouteDirection routeDirection)
    {
        return httpContext is not null
            && !httpContext.Request.Path.StartsWithSegments(
                new PathString("/api"),
                StringComparison.OrdinalIgnoreCase);
    }
}
