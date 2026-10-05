using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OF.Data.Database;
using OF.UI.Identity;
using OF.WebApp.Controllers;
using OF.WebApp.Features.Authorization;

namespace OF.Tests.WebApp.Features.Authorization;

public class ReadOnlyEndpointAttributesTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("ChangeOrder", false)]
    [InlineData("ChangeOrder, readonly ", true)]
    public void HasReadOnlyRole_ParsesRolesCaseInsensitively(string? roles, bool expected)
    {
        ReadOnlyAccess.HasReadOnlyRole(roles).Should().Be(expected);
    }

    [Fact]
    public void DenyReadOnly_ReturnsForbiddenForReadOnlyIdentity()
    {
        var context = CreateFilterContext(new User { Roles = "ReadOnly" });

        new DenyReadOnlyAttribute().OnAuthorization(context);

        context.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public void DenyReadOnly_AllowsNonReadOnlyIdentityToContinue()
    {
        var context = CreateFilterContext(new User { Roles = "ChangeOrder" });

        new DenyReadOnlyAttribute().OnAuthorization(context);

        context.Result.Should().BeNull();
    }

    [Fact]
    public void EveryNonGetEndpoint_IsExplicitlyClassifiedForReadOnlyAccess()
    {
        var unclassified = GetNonGetActions()
            .Where(action =>
                action.Method.GetCustomAttributes(typeof(DenyReadOnlyAttribute), true).Length
                + action.Method.GetCustomAttributes(typeof(ReadOnlyQueryAttribute), true).Length != 1)
            .Select(action => action.Name)
            .OrderBy(name => name)
            .ToArray();

        unclassified.Should().BeEmpty(
            "every non-GET endpoint must explicitly deny read-only users or be marked as a permitted query");
    }

    [Fact]
    public void AgreementParityMutations_DenyReadOnlyAccess()
    {
        typeof(AgreementResetController).GetMethod(nameof(AgreementResetController.Unfulfil))!
            .IsDefined(typeof(DenyReadOnlyAttribute), true).Should().BeTrue();
        typeof(AgreementLinesController).GetMethod(nameof(AgreementLinesController.Delete))!
            .IsDefined(typeof(DenyReadOnlyAttribute), true).Should().BeTrue();
    }

    [Fact]
    public void OnlyKnownPostQueries_ArePermittedForReadOnlyUsers()
    {
        var permittedQueries = GetNonGetActions()
            .Where(action => action.Method.IsDefined(typeof(ReadOnlyQueryAttribute), true))
            .Select(action => action.Name)
            .OrderBy(name => name)
            .ToArray();

        permittedQueries.Should().Equal(
            "EventController.Events",
            "RingfenceController.GetItemOverlaps",
            "RingfenceController.PreflightItemsForRingfence");
    }

    private static IEnumerable<(System.Reflection.MethodInfo Method, string Name)> GetNonGetActions() =>
        typeof(AuthController).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type))
            .SelectMany(type => type.GetMethods(
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.DeclaredOnly)
                .Select(method => (Method: method, Name: $"{type.Name}.{method.Name}")))
            .Where(action => action.Method.GetCustomAttributes(true).OfType<HttpMethodAttribute>()
                .SelectMany(attribute => attribute.HttpMethods)
                .Any(httpMethod => !string.Equals(httpMethod, HttpMethods.Get, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(httpMethod, HttpMethods.Head, StringComparison.OrdinalIgnoreCase)));

    private static AuthorizationFilterContext CreateFilterContext(User user)
    {
        var identity = new Mock<IUserIdentity>();
        identity.Setup(service => service.GetIdentity()).Returns(user);
        var services = new ServiceCollection()
            .AddSingleton(identity.Object)
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = services };
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor(),
            new ModelStateDictionary());
        return new AuthorizationFilterContext(actionContext, []);
    }
}
