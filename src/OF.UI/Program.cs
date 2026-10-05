using OF.UI.Database;
using System.Security.Claims;
using System.Security.Principal;
using OF.UI.Identity;
using OF.UI.Grid;
using OF.Common;
using Azure.Identity;
using OF.UI.Engine;
using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Razor;
using OF.UI.Middelware;
using OF.Data.Enums;
using OF.Common.Utils;
using OF.Common.Infrastructure.Features;
using OF.UI.Features.FrontendPreview;

var builder = WebApplication.CreateBuilder(args);

var keyVaultUri = Environment.GetEnvironmentVariable("KeyVaultUri", EnvironmentVariableTarget.Process) ?? throw new ArgumentException("KeyVaultUri must have a value.");
builder.Configuration
    .AddAzureKeyVault(new Uri(keyVaultUri), new DefaultAzureCredential())
    .AddJsonFile("appsettings.json")
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddEnvironmentVariables();

// Add services to the container.
builder.Services.AddApplicationInsightsTelemetry(builder.Configuration);
builder.Services.AddLogging(logging => logging.AddApplicationInsights());
builder.Services.AddControllersWithViews()
    .AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix)
    .AddDataAnnotationsLocalization();
builder.Services.AddRazorPages();
builder.Services.AddEndpointsApiExplorer();

builder.Services.RegisterCommonDependencies(builder.Configuration);
builder.Services.AddScoped<IDataRepository, DataRepository>();
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
builder.Services.AddSingleton<FeatureProvider>();
builder.Services.Configure<FrontendExperienceOptions>(
    builder.Configuration.GetSection(FrontendExperienceOptions.SectionName));
builder.Services.AddSingleton<FrontendPreviewLinkProvider>();
builder.Services.AddScoped<IUserIdentity, UserIdentity>();
builder.Services.AddScoped<IGridFactory, GridFactory>();
builder.Services.AddScoped<IRemoteHandlers, RemoteHandlers>();
builder.Services.AddScoped<IFulfilmentEngine, FulfilmentEngine>();

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

var supportedCultures = new[]
{
        new CultureInfo(Languages.English.GetCultureforLanguage()),
        new CultureInfo(Languages.French.GetCultureforLanguage()),
        new CultureInfo(Languages.German.GetCultureforLanguage()),
        new CultureInfo(Languages.Italian.GetCultureforLanguage()),
        new CultureInfo(Languages.Spanish.GetCultureforLanguage()),
    };

var defaultCulture = new RequestCulture(Languages.English.GetCultureforLanguage());

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = defaultCulture;
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Azure app service will send the x-ms-client-principal-id when authenticated
app.Use(async (context, next) =>
{
    async Task<Claim[]> GetClaims(string loginName)
    {
        var claims = new List<Claim>();
        var serviceScopeFactory = context.RequestServices.GetRequiredService<IServiceScopeFactory>();

        using (var scope = serviceScopeFactory.CreateScope())
        {
            var featureProvider = scope.ServiceProvider.GetRequiredService<FeatureProvider>();

            if (featureProvider.RolesEnabled)
            {
                var dataRepository = scope.ServiceProvider.GetRequiredService<IDataRepository>();
                var user = await dataRepository.GetUserAsync(loginName);
                if (user != null)
                {
                    foreach (var role in user.ActiveRoles)
                    {
                        claims.Add(new Claim(ClaimTypes.Role, role));
                    }
                }
            }
        }

        return claims.ToArray();
    }

    if (app.Environment.IsDevelopment())
    {
        var email = app.Configuration["LocalUserEmail"];
        var name = app.Configuration["LocalUserName"];
        var identity = new GenericIdentity(email);
        var claims = new List<Claim> {
            new Claim("http://schemas.microsoft.com/identity/claims/objectidentifier", email),
            new Claim("name", email)
        };
        var roles = await GetClaims(email);
        claims.AddRange(roles);
        identity.AddClaims(claims);
        var rolesArray = roles.Select(i => i.Value)?.ToArray();
        context.User = new GenericPrincipal(identity, rolesArray);
    }
    else
    {
        // Create a user on current thread from provided header
        if (context.Request.Headers.ContainsKey("X-MS-CLIENT-PRINCIPAL-ID"))
        {
            // Read headers from Azure
            var azureAppServicePrincipalNameHeader = context.Request.Headers["X-MS-CLIENT-PRINCIPAL-NAME"][0];

            // Create claims id
            var claims = new List<Claim> {
                new Claim("http://schemas.microsoft.com/identity/claims/objectidentifier", azureAppServicePrincipalNameHeader!),
                new Claim("name", azureAppServicePrincipalNameHeader)
            };

            var roles = await GetClaims(azureAppServicePrincipalNameHeader);
            claims.AddRange(roles);

            // Set user in current context as claims principal
            var identity = new GenericIdentity(azureAppServicePrincipalNameHeader);
            identity.AddClaims(claims);

            // Set current thread user to identity
            var rolesArray = roles.Select(i => i.Value)?.ToArray();
            context.User = new GenericPrincipal(identity, rolesArray);
        }
    };

    await next.Invoke();
});

var localizationOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = defaultCulture,
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures,
};
app.UseRequestLocalization(localizationOptions);

app.UseMiddleware<LocalizationMiddleware>();

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers();

app.Run();
