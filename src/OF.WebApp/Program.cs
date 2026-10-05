using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Microsoft.Extensions.Options;
using OF.Common;
using OF.Common.Infrastructure.Features;
using OF.Common.Infrastructure.OF;
using OF.UI.Database;
using OF.UI.Identity;
using OF.WebApp.Features.Administration;
using OF.WebApp.Features.Assets;
using OF.WebApp.Hosting;
using OF.WebApp.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Azure Key Vault configuration
var keyVaultUri = builder.Configuration["KeyVaultUri"]
    ?? Environment.GetEnvironmentVariable("KeyVaultUri", EnvironmentVariableTarget.Process);

if (!string.IsNullOrWhiteSpace(keyVaultUri))
{
    builder.Configuration.AddAzureKeyVault(
        new Uri(keyVaultUri),
        new DefaultAzureCredential(),
        new AzureKeyVaultConfigurationOptions
        {
            ReloadInterval = TimeSpan.FromHours(12)
        });
}

// In Development, re-add environment variables and user secrets AFTER Key Vault
// so they can override Key Vault values (e.g. SqlConnection with local-friendly auth)
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>(optional: true, reloadOnChange: true);
    builder.Configuration.AddEnvironmentVariables();
}

// Application Insights
var appInsightsConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
{
    builder.Services.AddApplicationInsightsTelemetry(options =>
    {
        options.ConnectionString = appInsightsConnectionString;
    });
}

// Register shared services from OF.Common
builder.Services.AddMemoryCache();
builder.Services.RegisterCommonDependencies(builder.Configuration);
builder.Services.AddScoped<IDataRepository, DataRepository>();
builder.Services.AddScoped<IAgreementEquipmentRepository, AgreementEquipmentRepository>();
builder.Services.AddScoped<IAgreementResetRepository, AgreementResetRepository>();
builder.Services.AddScoped<IAgreementLineDeletionRepository, AgreementLineDeletionRepository>();
builder.Services.AddScoped<ICoreDataRepository>(sp => sp.GetRequiredService<IDataRepository>());
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
builder.Services.AddScoped<IUserIdentity, UserIdentity>();
builder.Services.AddScoped<ICoreFulfilmentEngine, CoreFulfilmentEngine>();
builder.Services.AddSingleton<FeatureProvider>();
builder.Services.AddScoped<IAssetEnrichmentService, MdpAssetEnrichmentService>();
builder.Services.AddHttpClient("MicrosoftGraph", client =>
{
    client.BaseAddress = new Uri("https://graph.microsoft.com/v1.0/");
});
builder.Services.AddScoped<IAdminDirectoryService, MicrosoftGraphAdminDirectoryService>();
builder.Services.Configure<FrontendExperienceOptions>(
    builder.Configuration.GetSection(FrontendExperienceOptions.SectionName));

// API Controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });
builder.Services.AddWebAppRouting();

// ProblemDetails for consistent error responses
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = ctx =>
    {
        ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;
        ctx.ProblemDetails.Instance = ctx.HttpContext.Request.Path;
    };
});

var app = builder.Build();
var frontendExperience = app.Services.GetRequiredService<IOptions<FrontendExperienceOptions>>().Value;
app.Logger.LogInformation(
    "Starting OF.WebApp release {AppVersion} at commit {CommitSha}; deployment identity enabled: {ShowDeploymentInfo}",
    frontendExperience.AppVersion,
    frontendExperience.CommitSha,
    frontendExperience.ShowDeploymentInfo);

// EasyAuth middleware — parses x-ms-client-principal-id header
app.UseMiddleware<EasyAuthMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();

// Serve SPA static files from wwwroot in production
app.UseDefaultFiles();
app.UseWebAppStaticFiles();

app.UseRouting();
app.UseWebAppApiCachePolicy();

app.MapWebAppEndpoints();

app.Run();
