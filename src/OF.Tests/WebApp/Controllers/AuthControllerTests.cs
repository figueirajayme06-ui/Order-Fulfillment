using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using OF.Data.Database;
using OF.Data.Enums;
using OF.UI.Database;
using OF.UI.Identity;
using OF.WebApp.Controllers;

namespace OF.Tests.WebApp.Controllers;

public class AuthControllerTests
{
    private readonly Mock<IUserIdentity> _identity = new();
    private readonly Mock<IDataRepository> _repository = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 9, 2, 9, 30, 0, TimeSpan.Zero));
    private readonly Mock<ILogger<AuthController>> _logger = new();

    [Fact]
    public void GetCurrentUser_ReturnsUnauthorized_WhenIdentityIsMissing()
    {
        _identity.Setup(identity => identity.GetIdentity()).Returns((User?)null!);

        var result = CreateSubject().GetCurrentUser();

        result.Should().BeOfType<UnauthorizedResult>();
        _repository.Verify(
            repository => repository.UpdateLastLoginAtUtc(It.IsAny<string>(), It.IsAny<DateTimeOffset>()),
            Times.Never);
    }

    [Fact]
    public void GetCurrentUser_ReturnsProvisioningProblem_WhenAuthenticatedUserIsNotConfigured()
    {
        _identity.Setup(identity => identity.GetIdentity()).Returns((User?)null!);
        var subject = CreateSubject();
        subject.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.Name, "unconfigured@example.com")],
                    authenticationType: "EasyAuth")),
            },
        };

        var result = subject.GetCurrentUser();

        var response = result.Should().BeOfType<ObjectResult>().Subject;
        response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        var problem = response.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(StatusCodes.Status403Forbidden);
        problem.Title.Should().Be("NOF access required");
        problem.Extensions["code"].Should().Be("user_not_provisioned");
        JsonSerializer.Serialize(problem).Should().NotContain("unconfigured@example.com");
        _repository.Verify(
            repository => repository.UpdateLastLoginAtUtc(It.IsAny<string>(), It.IsAny<DateTimeOffset>()),
            Times.Never);
    }

    [Fact]
    public void GetCurrentUser_ReturnsExactCurrentUserValues()
    {
        SetIdentity(
            loginName: "planner@example.com",
            fullName: "Fleet Planner",
            division: "UK,IE",
            isAdmin: true,
            isSuperAdmin: false,
            language: Languages.French);

        var response = GetOkResponse(CreateSubject().GetCurrentUser());

        response.Should().BeEquivalentTo(new CurrentUserResponse
        {
            LoginName = "planner@example.com",
            DisplayName = "Fleet Planner",
            Division = "UK,IE",
            IsAdmin = true,
            IsSuperAdmin = false,
            IsReadOnly = false,
            Language = "fr",
        });
        _repository.Verify(repository => repository.UpdateLastLoginAtUtc(
            "planner@example.com",
            _timeProvider.GetUtcNow()), Times.Once);
    }

    [Fact]
    public void GetCurrentUser_ReturnsIdentityAndLogs_WhenAuditWriteFails()
    {
        SetIdentity(loginName: "planner@example.com", fullName: "Fleet Planner");
        _repository
            .Setup(repository => repository.UpdateLastLoginAtUtc("planner@example.com", _timeProvider.GetUtcNow()))
            .Throws(new InvalidOperationException("Database unavailable"));
        var subject = CreateSubject();
        subject.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-123" },
        };

        var response = GetOkResponse(subject.GetCurrentUser());

        response.LoginName.Should().Be("planner@example.com");
        _logger.Verify(logger => logger.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) =>
                state.ToString()!.Contains("planner@example.com")
                && state.ToString()!.Contains("trace-123")),
            It.Is<InvalidOperationException>(exception => exception.Message == "Database unavailable"),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public void GetCurrentUser_PreservesDisplayNameAndDivisionFallbacks()
    {
        SetIdentity(
            loginName: "planner@example.com",
            fullName: null,
            division: null,
            isAdmin: false,
            isSuperAdmin: false,
            language: Languages.English);

        var response = GetOkResponse(CreateSubject().GetCurrentUser());

        response.DisplayName.Should().Be("planner@example.com");
        response.Division.Should().BeEmpty();
    }

    [Theory]
    [InlineData(Languages.English, "en")]
    [InlineData(Languages.German, "de")]
    [InlineData(Languages.French, "fr")]
    [InlineData(Languages.Spanish, "es")]
    [InlineData(Languages.Italian, "it")]
    public void GetCurrentUser_MapsLanguageToCurrentCode(Languages language, string expectedCode)
    {
        SetIdentity(language: language);

        var response = GetOkResponse(CreateSubject().GetCurrentUser());

        response.Language.Should().Be(expectedCode);
    }

    [Fact]
    public void GetCurrentUser_SerializesToExactSevenPropertyWebJsonContract()
    {
        SetIdentity(
            loginName: "planner@example.com",
            fullName: "Fleet Planner",
            division: "UK",
            isAdmin: true,
            isSuperAdmin: true,
            roles: "ReadOnly",
            language: Languages.Italian);
        var response = GetOkResponse(CreateSubject().GetCurrentUser());

        var json = JsonSerializer.SerializeToElement(
            response,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        json.EnumerateObject().Select(property => property.Name).Should().Equal(
            "loginName",
            "displayName",
            "division",
            "isAdmin",
            "isSuperAdmin",
            "isReadOnly",
            "language");
        json.GetProperty("loginName").GetString().Should().Be("planner@example.com");
        json.GetProperty("displayName").GetString().Should().Be("Fleet Planner");
        json.GetProperty("division").GetString().Should().Be("UK");
        json.GetProperty("isAdmin").GetBoolean().Should().BeTrue();
        json.GetProperty("isSuperAdmin").GetBoolean().Should().BeTrue();
        json.GetProperty("isReadOnly").GetBoolean().Should().BeTrue();
        json.GetProperty("language").GetString().Should().Be("it");
    }

    private AuthController CreateSubject() => new(
        _identity.Object,
        _repository.Object,
        _timeProvider,
        _logger.Object);

    private void SetIdentity(
        string loginName = "test.user@example.com",
        string? fullName = "Test User",
        string? division = "UK",
        bool isAdmin = false,
        bool isSuperAdmin = false,
        string? roles = null,
        Languages language = Languages.English)
    {
        _identity.Setup(identity => identity.GetIdentity()).Returns(new User
        {
            LoginName = loginName,
            FullName = fullName!,
            Division = division!,
            IsAdmin = isAdmin,
            IsSuperAdmin = isSuperAdmin,
            Roles = roles,
            DateFormat = "dd/MM/yyyy",
            Language = (int)language,
        });
    }

    private static CurrentUserResponse GetOkResponse(IActionResult result)
    {
        return result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<CurrentUserResponse>().Subject;
    }
}
