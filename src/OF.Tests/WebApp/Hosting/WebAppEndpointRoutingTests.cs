using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using OF.UI.Database;
using OF.UI.Identity;
using OF.WebApp.Controllers;
using OF.WebApp.Hosting;

namespace OF.Tests.WebApp.Hosting;

public class WebAppEndpointRoutingTests : IClassFixture<SpaFallbackHost>
{
    private readonly SpaFallbackHost _host;

    public WebAppEndpointRoutingTests(SpaFallbackHost host)
    {
        _host = host;
    }

    [Fact]
    public async Task Get_ReactDeepLink_ReturnsSpaIndex()
    {
        using var response = await _host.Client.GetAsync("/agreements/42");

        await AssertSpaIndex(response);
        response.Headers.CacheControl?.NoStore.Should().BeTrue();
    }

    [Fact]
    public async Task Get_Index_ReturnsNoStoreCachePolicy()
    {
        using var response = await _host.Client.GetAsync("/");

        await AssertSpaIndex(response);
        response.Headers.CacheControl?.NoStore.Should().BeTrue();
    }

    [Fact]
    public async Task Get_FingerprintedAsset_ReturnsImmutableCachePolicy()
    {
        using var response = await _host.Client.GetAsync("/assets/index-AbCdEf123.js");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl?.Public.Should().BeTrue();
        response.Headers.CacheControl?.MaxAge.Should().Be(TimeSpan.FromDays(365));
        response.Headers.CacheControl?.Extensions.Should().ContainSingle(value => value.Name == "immutable");
    }

    [Theory]
    [InlineData("/api")]
    [InlineData("/api/__d07_missing")]
    [InlineData("/api/x/y")]
    [InlineData("/api/__d07_missing.json")]
    public async Task Get_UnknownApiPath_ReturnsNotFoundWithoutSpaFallback(string path)
    {
        using var response = await _host.Client.GetAsync(path);

        await AssertNotSpa(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Head_UnknownApiPath_ReturnsNotFoundWithoutSpaFallback()
    {
        using var request = new HttpRequestMessage(HttpMethod.Head, "/api/x/y");
        using var response = await _host.Client.SendAsync(request);

        await AssertNotSpa(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_ApiLookalikePath_ReturnsSpaIndex()
    {
        using var response = await _host.Client.GetAsync("/apix/__d07_missing");

        await AssertSpaIndex(response);
    }

    [Fact]
    public async Task Get_KnownApiRoute_UsesControllerInsteadOfSpaFallback()
    {
        using var response = await _host.Client.GetAsync("/api/auth/me");

        await AssertNotSpa(response, HttpStatusCode.Unauthorized);
        response.Headers.CacheControl?.NoStore.Should().BeTrue();
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Get_HealthEndpoint_ReturnsHealthyWithoutSpaFallback(string path)
    {
        using var response = await _host.Client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/plain");
        (await response.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }

    [Fact]
    public async Task Post_KnownGetOnlyApiRoute_ReturnsMethodNotAllowedWithoutSpaFallback()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/me");
        using var response = await _host.Client.SendAsync(request);

        await AssertNotSpa(response, HttpStatusCode.MethodNotAllowed);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    public async Task NonPost_KnownBodyBoundPostRoute_ReturnsNotFoundWithoutSpaFallback(string method)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), "/api/event/events");
        using var response = await _host.Client.SendAsync(request);

        await AssertNotSpa(response, HttpStatusCode.NotFound);
    }

    private static async Task AssertSpaIndex(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
        (await response.Content.ReadAsStringAsync()).Should().Contain(SpaFallbackHost.Marker);
    }

    private static async Task AssertNotSpa(HttpResponseMessage response, HttpStatusCode expectedStatus)
    {
        response.StatusCode.Should().Be(expectedStatus);
        response.Content.Headers.ContentType?.MediaType.Should().NotBe("text/html");
        (await response.Content.ReadAsStringAsync()).Should().NotContain(SpaFallbackHost.Marker);
    }
}

public sealed class SpaFallbackHost : IAsyncLifetime
{
    public const string Marker = "D07 SPA sentinel";

    private DirectoryInfo? _contentRoot;
    private WebApplication? _app;
    private HttpClient? _client;

    public HttpClient Client => _client ?? throw new InvalidOperationException("The test host has not started.");

    public async Task InitializeAsync()
    {
        _contentRoot = Directory.CreateTempSubdirectory("of-webapp-routing-");
        var webRoot = Path.Combine(_contentRoot.FullName, "wwwroot");
        Directory.CreateDirectory(webRoot);
        await File.WriteAllTextAsync(
            Path.Combine(webRoot, "index.html"),
            $"<!doctype html><html><body><div id=\"d07-spa-index\">{Marker}</div></body></html>");
        var assetRoot = Path.Combine(webRoot, "assets");
        Directory.CreateDirectory(assetRoot);
        await File.WriteAllTextAsync(Path.Combine(assetRoot, "index-AbCdEf123.js"), "export default true;");

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = Array.Empty<string>(),
            EnvironmentName = Environments.Production,
            ContentRootPath = _contentRoot.FullName,
            WebRootPath = webRoot,
        });

        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(Mock.Of<IDataRepository>());
        builder.Services.AddSingleton(Mock.Of<IUserIdentity>());
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddWebAppRouting();
        builder.Services
            .AddControllers()
            .AddApplicationPart(typeof(AuthController).Assembly);

        _app = builder.Build();
        _app.UseDefaultFiles();
        _app.UseWebAppStaticFiles();
        _app.UseRouting();
        _app.UseWebAppApiCachePolicy();
        _app.MapWebAppEndpoints();

        await _app.StartAsync();

        var addresses = _app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()?
            .Addresses;
        var address = addresses?.SingleOrDefault();

        if (string.IsNullOrWhiteSpace(address) || address.EndsWith(":0", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Kestrel did not expose its assigned loopback address.");
        }

        _client = new HttpClient(new SocketsHttpHandler { UseProxy = false })
        {
            BaseAddress = new Uri(address),
        };
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();

        try
        {
            if (_app != null)
            {
                await _app.StopAsync();
            }
        }
        finally
        {
            try
            {
                if (_app != null)
                {
                    await _app.DisposeAsync();
                }
            }
            finally
            {
                if (_contentRoot != null)
                {
                    _contentRoot.Refresh();
                    if (_contentRoot.Exists)
                    {
                        _contentRoot.Delete(recursive: true);
                    }
                }
            }
        }
    }
}
