using OF.UI.Database;
using OF.UI.Identity;
using OF.Common;
using Azure.Identity;
using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Razor;
using OF.UI.Middelware;
using OF.Data.Enums;
using OF.Common.Utils;
using OF.Common.Infrastructure.Features;
using OF.UI.Shared.Controllers;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddApplicationInsightsTelemetry();
var assembly = typeof(DocumentController).Assembly;
builder.Services.AddControllersWithViews().AddApplicationPart(assembly).AddRazorRuntimeCompilation();
builder.Services.Configure<MvcRazorRuntimeCompilationOptions>(options =>
{
    options.FileProviders.Add(new EmbeddedFileProvider(assembly));
});
var keyVaultUri = Environment.GetEnvironmentVariable("KeyVaultUri", EnvironmentVariableTarget.Process) ?? throw new ArgumentException("KeyVaultUri must have a value.");
builder.Configuration
    .AddAzureKeyVault(new Uri(keyVaultUri), new DefaultAzureCredential())
    .AddJsonFile("appsettings.json")
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddEnvironmentVariables();

builder.Services.RegisterCommonDependencies(builder.Configuration);
builder.Services.AddScoped<IDataRepository, DataRepository>();
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
builder.Services.AddSingleton<FeatureProvider>();
builder.Services.AddScoped<IUserIdentity, UserIdentity>();

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.AddControllersWithViews()
    .AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix)
    .AddDataAnnotationsLocalization();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/Login";
                options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
            });

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
