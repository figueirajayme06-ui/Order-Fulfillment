using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OF.Common.Infrastructure.Features;
using OF.Data.Database;
using OF.UI.Database;
using OF.UI.Identity;
using OF.WebApp.Controllers;
using OF.WebApp.Features.Administration;
using System.Text.Json;

namespace OF.Tests.WebApp.Controllers;

public class AdminControllerTests
{
    private readonly Mock<IDataRepository> _repository = new();
    private readonly Mock<IUserIdentity> _identity = new();
    private readonly Mock<IAdminDirectoryService> _directoryService = new();

    [Theory]
    [InlineData("missing")]
    [InlineData("user")]
    public void GetUsers_ReturnsForbiddenWithoutReadingUsers_WhenCallerIsNotAnAdmin(string caller)
    {
        SetCaller(caller);

        var result = CreateSubject().GetUsers();

        result.Should().BeOfType<ForbidResult>();
        _repository.Verify(repository => repository.GetUsers(), Times.Never);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("super")]
    public void GetUsers_AllowsAdminAndSuperAdmin(string caller)
    {
        SetCaller(caller);
        _repository.Setup(repository => repository.GetUsers())
            .Returns(new[]
            {
                CreateUser(
                    "second@example.com",
                    isAdmin: true,
                    fullName: "Second User",
                    division: "UK,IE",
                    language: 2,
                    lastLoginAtUtc: new DateTime(2026, 9, 2, 8, 15, 0)),
                CreateUser(
                    "first@example.com",
                    isSuperAdmin: true,
                    fullName: "First User",
                    division: "FR",
                    language: 3),
            }.AsQueryable());

        var result = CreateSubject().GetUsers();

        var users = GetOkUsers(result);
        users.Select(user => (
            user.LoginName,
            user.FullName,
            user.Division,
            user.IsAdmin,
            user.IsSuperAdmin,
            user.Language,
            user.DateFormat,
            user.Roles,
            user.LastLoginAtUtc)).Should().Equal(
                ("second@example.com", "Second User", "UK,IE", true, false, 2, "MM/dd/yyyy", "Operations", new DateTime(2026, 9, 2, 8, 15, 0, DateTimeKind.Utc)),
                ("first@example.com", "First User", "FR", false, true, 3, "MM/dd/yyyy", "Operations", (DateTime?)null));
        _repository.Verify(repository => repository.GetUsers(), Times.Once);
    }

    [Fact]
    public void GetUsers_SerializesToExpandedEditablePropertyWebJsonContract()
    {
        SetCaller("admin");
        _repository.Setup(repository => repository.GetUsers())
            .Returns(new[]
            {
                CreateUser(
                    "target@example.com",
                    isAdmin: true,
                    fullName: "Target User",
                    division: "UK,IE",
                    language: 2,
                    lastLoginAtUtc: new DateTime(2026, 9, 2, 8, 15, 0)),
            }.AsQueryable());
        var users = GetOkUsers(CreateSubject().GetUsers());

        var json = JsonSerializer.Serialize(
            users,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        json.Should().Be(
            """[{"loginName":"target@example.com","fullName":"Target User","division":"UK,IE","isAdmin":true,"isSuperAdmin":false,"language":2,"dateFormat":"MM/dd/yyyy","roles":"Operations","lastLoginAtUtc":"2026-09-02T08:15:00Z"}]""");
        using var document = JsonDocument.Parse(json);
        var user = document.RootElement[0];
        user.EnumerateObject().Select(property => (property.Name, property.Value.ValueKind)).Should().Equal(
            ("loginName", JsonValueKind.String),
            ("fullName", JsonValueKind.String),
            ("division", JsonValueKind.String),
            ("isAdmin", JsonValueKind.True),
            ("isSuperAdmin", JsonValueKind.False),
            ("language", JsonValueKind.Number),
            ("dateFormat", JsonValueKind.String),
            ("roles", JsonValueKind.String),
            ("lastLoginAtUtc", JsonValueKind.String));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("user")]
    public void GetOptions_ReturnsForbiddenWithoutReadingLookups_WhenCallerIsNotAnAdmin(string caller)
    {
        SetCaller(caller);

        var result = CreateSubject().GetOptions();

        result.Should().BeOfType<ForbidResult>();
        _repository.Verify(repository => repository.GetWarehouseDivisionCodes(), Times.Never);
    }

    [Fact]
    public void GetOptions_ReturnsControlledLegacyChoicesAndEnabledRoles()
    {
        SetCaller("admin");
        _repository.Setup(repository => repository.GetWarehouseDivisionCodes()).Returns(["150", "110", "110"]);
        _repository.Setup(repository => repository.GetWarehouses(It.IsAny<string[]>())).Returns([
            new WarehouseItem { DivisionCode = "110", Country = "United Kingdom" },
            new WarehouseItem { DivisionCode = "150", Country = "Ireland" },
        ]);

        var result = CreateSubject(rolesEnabled: true).GetOptions();

        var options = result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<AdminOptionsResponse>().Subject;
        options.Divisions.Select(division => (division.Code, division.Name)).Should().Equal(
            ("110", "United Kingdom"),
            ("150", "Ireland"));
        options.Languages.Select(language => (language.Value, language.Label)).Should().Equal(
            (0, "English"),
            (1, "German"),
            (2, "French"),
            (3, "Spanish"),
            (4, "Italian"));
        options.DateFormats.Should().Equal("dd/MM/yyyy", "MM/dd/yyyy");
        options.RolesEnabled.Should().BeTrue();
        options.Roles.Should().Equal("ReadOnly", "ChangeOrder", "ChangeApproval", "NewFrontendPreview");
    }

    [Fact]
    public void GetOptions_AlwaysOffersReadOnly_WhenOptionalRolesAreDisabled()
    {
        SetCaller("admin");
        _repository.Setup(repository => repository.GetWarehouseDivisionCodes()).Returns([]);
        _repository.Setup(repository => repository.GetWarehouses(It.IsAny<string[]>())).Returns([]);

        var result = CreateSubject(rolesEnabled: false).GetOptions();

        var options = result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<AdminOptionsResponse>().Subject;
        options.RolesEnabled.Should().BeFalse();
        options.Roles.Should().Equal("ReadOnly");
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("user")]
    public async Task SearchPeople_ReturnsForbiddenWithoutCallingDirectory_WhenCallerIsNotAnAdmin(string caller)
    {
        SetCaller(caller);

        var result = await CreateSubject().SearchPeople("alex", CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
        _directoryService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SearchPeople_RequiresAtLeastTwoCharacters()
    {
        SetCaller("admin");

        var result = await CreateSubject().SearchPeople("a", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        _directoryService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SearchPeople_ReturnsDirectoryMatches()
    {
        SetCaller("admin");
        DirectoryPerson[] people = [new("alex@example.com", "Alex Smith", "alex@example.com")];
        _directoryService.Setup(service => service.SearchAsync("alex", It.IsAny<CancellationToken>()))
            .ReturnsAsync(people);

        var result = await CreateSubject().SearchPeople("alex", CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>().Subject.Value.Should().BeSameAs(people);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("user")]
    [InlineData("admin")]
    public void DeleteUser_ReturnsForbiddenWithoutReadingTarget_WhenCallerIsNotSuperAdmin(string caller)
    {
        SetCaller(caller);

        var result = CreateSubject().DeleteUser("target@example.com");

        result.Should().BeOfType<ForbidResult>();
        _repository.Verify(repository => repository.GetUser(It.IsAny<string>()), Times.Never);
        _repository.Verify(repository => repository.DeleteUser(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public void DeleteUser_ReturnsNotFoundWithoutDeleting_WhenTargetIsMissing()
    {
        SetCaller("super");

        var result = CreateSubject().DeleteUser("missing@example.com");

        result.Should().BeOfType<NotFoundResult>();
        _repository.Verify(repository => repository.GetUser("missing@example.com"), Times.Once);
        _repository.Verify(repository => repository.DeleteUser(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public void DeleteUser_AllowsSuperAdminToDeleteSelfWithoutLastSuperAdminLookup()
    {
        SetCaller("super");
        var target = CreateUser("super@example.com", isSuperAdmin: true);
        _repository.Setup(repository => repository.GetUser(target.LoginName)).Returns(target);

        var result = CreateSubject().DeleteUser(target.LoginName);

        result.Should().BeOfType<NoContentResult>();
        _repository.Verify(repository => repository.DeleteUser(target), Times.Once);
        _repository.Verify(repository => repository.GetUsers(), Times.Never);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("user")]
    public void CreateUser_ReturnsForbiddenWithoutRepositoryAccess_WhenCallerIsNotAnAdmin(string caller)
    {
        SetCaller(caller);

        var result = CreateSubject().CreateUser(CreateRequest(isSuperAdmin: false));

        result.Should().BeOfType<ForbidResult>();
        _repository.Verify(repository => repository.AddUser(It.IsAny<User>()), Times.Never);
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public void CreateUser_ReturnsForbiddenWithoutWriting_WhenOrdinaryAdminRequestsSuperAdmin()
    {
        SetCaller("admin");

        var result = CreateSubject().CreateUser(CreateRequest(isSuperAdmin: true));

        result.Should().BeOfType<ForbidResult>();
        _repository.Verify(repository => repository.AddUser(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public void CreateUser_ReturnsConflictWithoutWriting_WhenLoginAlreadyExists()
    {
        SetCaller("admin");
        var request = CreateRequest() with { LoginName = " New.User@Example.com " };
        _repository.Setup(repository => repository.GetUser("new.user@example.com"))
            .Returns(CreateUser("new.user@example.com"));

        var result = CreateSubject().CreateUser(request);

        result.Should().BeOfType<ConflictObjectResult>();
        _repository.Verify(repository => repository.AddUser(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public void CreateUser_ReturnsCreatedAtActionWithExactMapping_ForOrdinaryAdminNonSuperUser()
    {
        SetCaller("admin");
        var request = CreateRequest(isAdmin: true, isSuperAdmin: false);
        User? captured = null;
        var persisted = CreateUser(
            request.LoginName,
            isAdmin: true,
            fullName: "Persisted User",
            division: request.Division,
            language: request.Language);
        persisted.DateFormat = "yyyy-MM-dd";
        persisted.Roles = "Viewer, Admin";
        _repository.Setup(repository => repository.AddUser(It.IsAny<User>()))
            .Callback<User>(user => captured = user)
            .Returns(persisted);

        var result = CreateSubject().CreateUser(request);

        var response = GetCreatedMutationResponse(result, persisted.LoginName);
        captured.Should().NotBeNull();
        AssertMutationResponseMatches(response, persisted);
        SerializeWeb(response).Should().Be(
            """{"languageName":"French","activeRoles":["Admin","Viewer"],"loginName":"new.user@example.com","fullName":"Persisted User","division":"UK,IE","isAdmin":true,"isSuperAdmin":false,"dateFormat":"yyyy-MM-dd","language":2,"roles":"Viewer, Admin","lastLoginAtUtc":null}""");
        captured!.LoginName.Should().Be(request.LoginName);
        captured.FullName.Should().Be(request.FullName);
        captured.Division.Should().Be(request.Division);
        captured.IsAdmin.Should().BeTrue();
        captured.IsSuperAdmin.Should().BeFalse();
        captured.DateFormat.Should().Be("dd/MM/yyyy");
        captured.Language.Should().Be(request.Language);
        captured.Roles.Should().BeNull();
        _repository.Verify(repository => repository.AddUser(captured), Times.Once);
    }

    [Fact]
    public void CreateUser_AllowsSuperAdminWithoutRequiringCallerOrTargetIsAdmin()
    {
        SetCaller("super");
        var request = CreateRequest(isAdmin: false, isSuperAdmin: true);
        _repository.Setup(repository => repository.AddUser(It.IsAny<User>()))
            .Returns<User>(user => user);

        var result = CreateSubject().CreateUser(request);

        GetCreatedMutationResponse(result, request.LoginName).IsSuperAdmin.Should().BeTrue();
        _repository.Verify(repository => repository.AddUser(It.Is<User>(user =>
            user.IsSuperAdmin
            && !user.IsAdmin
            && user.LoginName == request.LoginName)), Times.Once);
    }

    [Fact]
    public void CreateUser_PersistsControlledDateFormatAndRoles_WhenRolesAreEnabled()
    {
        SetCaller("admin");
        var request = CreateRequest(dateFormat: "MM/dd/yyyy", roles: "ChangeOrder, ChangeApproval,ChangeOrder");
        User? captured = null;
        _repository.Setup(repository => repository.AddUser(It.IsAny<User>()))
            .Callback<User>(user => captured = user)
            .Returns<User>(user => user);

        var result = CreateSubject(rolesEnabled: true).CreateUser(request);

        GetCreatedMutationResponse(result, request.LoginName);
        captured.Should().NotBeNull();
        captured!.DateFormat.Should().Be("MM/dd/yyyy");
        captured.Roles.Should().Be("ChangeOrder,ChangeApproval");
    }

    [Fact]
    public void CreateUser_PersistsReadOnly_WhenOptionalRolesAreDisabled()
    {
        SetCaller("admin");
        var request = CreateRequest(roles: "ReadOnly");
        _repository.Setup(repository => repository.AddUser(It.IsAny<User>()))
            .Returns<User>(user => user);

        var result = CreateSubject(rolesEnabled: false).CreateUser(request);

        GetCreatedMutationResponse(result, request.LoginName);
        _repository.Verify(repository => repository.AddUser(It.Is<User>(user =>
            user.Roles == "ReadOnly")), Times.Once);
    }

    [Fact]
    public void CreateUser_RejectsReadOnlyCombinedWithAdmin()
    {
        SetCaller("admin");

        var result = CreateSubject().CreateUser(CreateRequest(isAdmin: true, roles: "ReadOnly"));

        result.Should().BeOfType<BadRequestObjectResult>();
        _repository.Verify(repository => repository.AddUser(It.IsAny<User>()), Times.Never);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("user")]
    public void UpdateUser_ReturnsForbiddenBeforeReadingTarget_WhenCallerIsNotAnAdmin(string caller)
    {
        SetCaller(caller);

        var result = CreateSubject().UpdateUser("target@example.com", UpdateRequest());

        result.Should().BeOfType<ForbidResult>();
        _repository.Verify(repository => repository.GetUser(It.IsAny<string>()), Times.Never);
        _repository.Verify(repository => repository.UpdateUser(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public void UpdateUser_ReturnsNotFoundBeforeTransitionAuthorization_WhenTargetIsMissing()
    {
        SetCaller("admin");

        var result = CreateSubject().UpdateUser(
            "missing@example.com",
            UpdateRequest(isSuperAdmin: true));

        result.Should().BeOfType<NotFoundResult>();
        _repository.Verify(repository => repository.GetUser("missing@example.com"), Times.Once);
        _repository.Verify(repository => repository.UpdateUser(It.IsAny<User>()), Times.Never);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void UpdateUser_ReturnsForbiddenWithoutMutatingTrackedTarget_WhenOrdinaryAdminChangesSuperAdmin(
        bool currentSuperAdmin,
        bool requestedSuperAdmin)
    {
        SetCaller("admin");
        var target = CreateUser(
            "target@example.com",
            isAdmin: currentSuperAdmin,
            isSuperAdmin: currentSuperAdmin);
        var original = Clone(target);
        _repository.Setup(repository => repository.GetUser(target.LoginName)).Returns(target);
        var request = UpdateRequest(
            isAdmin: !target.IsAdmin,
            isSuperAdmin: requestedSuperAdmin);

        var result = CreateSubject().UpdateUser(target.LoginName, request);

        result.Should().BeOfType<ForbidResult>();
        AssertSameState(target, original);
        _repository.Verify(repository => repository.GetUser(target.LoginName), Times.Once);
        _repository.Verify(repository => repository.UpdateUser(It.IsAny<User>()), Times.Never);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void UpdateUser_AllowsOrdinaryAdminToChangeNonSuperFields_WhenSuperAdminValueIsUnchanged(
        bool currentSuperAdmin,
        bool requestedIsAdmin)
    {
        SetCaller("admin");
        var target = CreateUser(
            "target@example.com",
            isAdmin: !requestedIsAdmin,
            isSuperAdmin: currentSuperAdmin,
            lastLoginAtUtc: new DateTime(2026, 9, 2, 8, 15, 0));
        var originalDateFormat = target.DateFormat;
        var originalRoles = target.Roles;
        var request = UpdateRequest(
            isAdmin: requestedIsAdmin,
            isSuperAdmin: currentSuperAdmin);
        var persisted = CreateUser(
            target.LoginName,
            isAdmin: request.IsAdmin,
            isSuperAdmin: request.IsSuperAdmin,
            fullName: request.FullName,
            division: request.Division,
            language: request.Language,
            lastLoginAtUtc: target.LastLoginAtUtc);
        persisted.DateFormat = originalDateFormat;
        persisted.Roles = originalRoles;
        _repository.Setup(repository => repository.GetUser(target.LoginName)).Returns(target);
        _repository.Setup(repository => repository.UpdateUser(target)).Returns(persisted);

        var result = CreateSubject().UpdateUser(target.LoginName, request);

        AssertMutationResponseMatches(GetOkMutationResponse(result), persisted);
        AssertUpdateMapping(target, request);
        target.LoginName.Should().Be("target@example.com");
        target.DateFormat.Should().Be(originalDateFormat);
        target.Roles.Should().Be(originalRoles);
        target.LastLoginAtUtc.Should().Be(new DateTime(2026, 9, 2, 8, 15, 0));
        _repository.Verify(repository => repository.UpdateUser(target), Times.Once);
    }

    [Fact]
    public void UpdateUser_PreservesNullAndDefaultRepositoryResponseInWebJsonContract()
    {
        SetCaller("admin");
        var target = CreateUser("target@example.com");
        var request = UpdateRequest(isAdmin: false, isSuperAdmin: false);
        var persisted = new User();
        _repository.Setup(repository => repository.GetUser(target.LoginName)).Returns(target);
        _repository.Setup(repository => repository.UpdateUser(target)).Returns(persisted);

        var result = CreateSubject().UpdateUser(target.LoginName, request);

        var response = GetOkMutationResponse(result);
        AssertMutationResponseMatches(response, persisted);
        SerializeWeb(response).Should().Be(
            """{"languageName":"English","activeRoles":[],"loginName":null,"fullName":null,"division":null,"isAdmin":false,"isSuperAdmin":false,"dateFormat":null,"language":0,"roles":null,"lastLoginAtUtc":null}""");
        AssertUpdateMapping(target, request);
        _repository.Verify(repository => repository.UpdateUser(target), Times.Once);
    }

    [Fact]
    public void UpdateUser_PersistsDateFormatAndRoles_WhenRolesAreEnabled()
    {
        SetCaller("admin");
        var target = CreateUser("target@example.com");
        var request = UpdateRequest(
            dateFormat: "dd/MM/yyyy",
            roles: " ChangeOrder, ChangeApproval,ChangeOrder ");
        _repository.Setup(repository => repository.GetUser(target.LoginName)).Returns(target);
        _repository.Setup(repository => repository.UpdateUser(target)).Returns(target);

        var result = CreateSubject(rolesEnabled: true).UpdateUser(target.LoginName, request);

        GetOkMutationResponse(result).Should().NotBeNull();
        target.DateFormat.Should().Be("dd/MM/yyyy");
        target.Roles.Should().Be("ChangeOrder,ChangeApproval");
        _repository.Verify(repository => repository.UpdateUser(target), Times.Once);
    }

    [Fact]
    public void UpdateUser_RejectsReadOnlyCombinedWithSuperAdminWithoutMutatingTarget()
    {
        SetCaller("super");
        var target = CreateUser("target@example.com");
        var original = Clone(target);
        _repository.Setup(repository => repository.GetUser(target.LoginName)).Returns(target);

        var result = CreateSubject().UpdateUser(
            target.LoginName,
            UpdateRequest(isAdmin: false, isSuperAdmin: true, roles: "ReadOnly"));

        result.Should().BeOfType<BadRequestObjectResult>();
        AssertSameState(target, original);
        _repository.Verify(repository => repository.UpdateUser(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public void UpdateUser_ChangesReadOnlyWithoutRemovingExistingOptionalRoles_WhenOptionalRolesAreDisabled()
    {
        SetCaller("admin");
        var target = CreateUser("target@example.com");
        target.Roles = "ChangeOrder";
        _repository.Setup(repository => repository.GetUser(target.LoginName)).Returns(target);
        _repository.Setup(repository => repository.UpdateUser(target)).Returns(target);

        var result = CreateSubject().UpdateUser(
            target.LoginName,
            UpdateRequest(isAdmin: false, roles: "ReadOnly"));

        GetOkMutationResponse(result);
        target.Roles.Should().Be("ChangeOrder,ReadOnly");
    }

    [Fact]
    public void UpdateUser_AllowsSuperAdminToPromoteWithoutIsAdminImplication()
    {
        SetCaller("super");
        var target = CreateUser("target@example.com", isAdmin: true, isSuperAdmin: false);
        _repository.Setup(repository => repository.GetUser(target.LoginName)).Returns(target);
        _repository.Setup(repository => repository.UpdateUser(target)).Returns(target);
        var request = UpdateRequest(isAdmin: false, isSuperAdmin: true);

        var result = CreateSubject().UpdateUser(target.LoginName, request);

        AssertMutationResponseMatches(GetOkMutationResponse(result), target);
        AssertUpdateMapping(target, request);
        target.IsSuperAdmin.Should().BeTrue();
        target.IsAdmin.Should().BeFalse();
        _repository.Verify(repository => repository.UpdateUser(target), Times.Once);
    }

    [Fact]
    public void UpdateUser_AllowsSuperAdminToDemoteSelfWithoutLastSuperAdminLookup()
    {
        var target = CreateUser(
            "super@example.com",
            isAdmin: false,
            isSuperAdmin: true);
        _identity.Setup(identity => identity.GetIdentity()).Returns(target);
        _repository.Setup(repository => repository.GetUser(target.LoginName)).Returns(target);
        _repository.Setup(repository => repository.UpdateUser(target)).Returns(target);
        var request = UpdateRequest(isAdmin: false, isSuperAdmin: false);

        var result = CreateSubject().UpdateUser(target.LoginName, request);

        AssertMutationResponseMatches(GetOkMutationResponse(result), target);
        AssertUpdateMapping(target, request);
        target.IsSuperAdmin.Should().BeFalse();
        target.IsAdmin.Should().BeFalse();
        _repository.Verify(repository => repository.UpdateUser(target), Times.Once);
        _repository.Verify(repository => repository.GetUsers(), Times.Never);
    }

    private AdminController CreateSubject(bool rolesEnabled = false) => new(
        _repository.Object,
        _identity.Object,
        _directoryService.Object,
        CreateFeatureProvider(rolesEnabled),
        NullLogger<AdminController>.Instance);

    private static FeatureProvider CreateFeatureProvider(bool rolesEnabled)
    {
        var values = rolesEnabled
            ? new Dictionary<string, string?> { ["Features:ChangeOrders"] = "true" }
            : new Dictionary<string, string?>();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        return new FeatureProvider(configuration);
    }

    private static IReadOnlyList<AdminUserListResponse> GetOkUsers(IActionResult result)
    {
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        return ok.Value
            .Should().BeAssignableTo<IEnumerable<AdminUserListResponse>>().Subject
            .ToList();
    }

    private static AdminUserMutationResponse GetCreatedMutationResponse(
        IActionResult result,
        string expectedLoginName)
    {
        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.StatusCode.Should().Be(201);
        created.ActionName.Should().Be(nameof(AdminController.GetUsers));
        created.RouteValues.Should().NotBeNull();
        created.RouteValues!["loginName"].Should().Be(expectedLoginName);
        return created.Value.Should().BeOfType<AdminUserMutationResponse>().Subject;
    }

    private static AdminUserMutationResponse GetOkMutationResponse(IActionResult result)
    {
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        return ok.Value.Should().BeOfType<AdminUserMutationResponse>().Subject;
    }

    private static void AssertMutationResponseMatches(AdminUserMutationResponse actual, User expected)
    {
        actual.LanguageName.Should().Be(expected.LanguageName);
        actual.ActiveRoles.Should().Equal(expected.ActiveRoles);
        actual.LoginName.Should().Be(expected.LoginName);
        actual.FullName.Should().Be(expected.FullName);
        actual.Division.Should().Be(expected.Division);
        actual.IsAdmin.Should().Be(expected.IsAdmin);
        actual.IsSuperAdmin.Should().Be(expected.IsSuperAdmin);
        actual.DateFormat.Should().Be(expected.DateFormat);
        actual.Language.Should().Be(expected.Language);
        actual.Roles.Should().Be(expected.Roles);
        actual.LastLoginAtUtc.Should().Be(expected.LastLoginAtUtc);
    }

    private static string SerializeWeb(AdminUserMutationResponse response) => JsonSerializer.Serialize(
        response,
        new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private void SetCaller(string caller)
    {
        if (caller == "missing")
        {
            _identity.Setup(identity => identity.GetIdentity()).Returns((User?)null!);
            return;
        }

        _identity.Setup(identity => identity.GetIdentity()).Returns(CreateUser(
            $"{caller}@example.com",
            isAdmin: caller == "admin",
            isSuperAdmin: caller == "super"));
    }

    private static CreateUserRequest CreateRequest(
        bool isAdmin = false,
        bool isSuperAdmin = false,
        string? dateFormat = null,
        string? roles = null) => new()
        {
            LoginName = "new.user@example.com",
            FullName = "New User",
            Division = "UK,IE",
            IsAdmin = isAdmin,
            IsSuperAdmin = isSuperAdmin,
            Language = 2,
            DateFormat = dateFormat,
            Roles = roles,
        };

    private static UpdateUserRequest UpdateRequest(
        bool isAdmin = true,
        bool isSuperAdmin = false,
        string? dateFormat = null,
        string? roles = null) => new()
        {
            FullName = "Updated User",
            Division = "FR,DE",
            IsAdmin = isAdmin,
            IsSuperAdmin = isSuperAdmin,
            Language = 3,
            DateFormat = dateFormat,
            Roles = roles,
        };

    private static User CreateUser(
        string loginName,
        bool isAdmin = false,
        bool isSuperAdmin = false,
        string fullName = "Original User",
        string division = "UK",
        int language = 1,
        DateTime? lastLoginAtUtc = null) => new()
        {
            LoginName = loginName,
            FullName = fullName,
            Division = division,
            IsAdmin = isAdmin,
            IsSuperAdmin = isSuperAdmin,
            DateFormat = "MM/dd/yyyy",
            Language = language,
            Roles = "Operations",
            LastLoginAtUtc = lastLoginAtUtc,
        };

    private static User Clone(User user) => new()
    {
        LoginName = user.LoginName,
        FullName = user.FullName,
        Division = user.Division,
        IsAdmin = user.IsAdmin,
        IsSuperAdmin = user.IsSuperAdmin,
        DateFormat = user.DateFormat,
        Language = user.Language,
        Roles = user.Roles,
        LastLoginAtUtc = user.LastLoginAtUtc,
    };

    private static void AssertSameState(User actual, User expected)
    {
        actual.LoginName.Should().Be(expected.LoginName);
        actual.FullName.Should().Be(expected.FullName);
        actual.Division.Should().Be(expected.Division);
        actual.IsAdmin.Should().Be(expected.IsAdmin);
        actual.IsSuperAdmin.Should().Be(expected.IsSuperAdmin);
        actual.DateFormat.Should().Be(expected.DateFormat);
        actual.Language.Should().Be(expected.Language);
        actual.Roles.Should().Be(expected.Roles);
        actual.LastLoginAtUtc.Should().Be(expected.LastLoginAtUtc);
    }

    private static void AssertUpdateMapping(User target, UpdateUserRequest request)
    {
        target.FullName.Should().Be(request.FullName);
        target.Division.Should().Be(request.Division);
        target.IsAdmin.Should().Be(request.IsAdmin);
        target.IsSuperAdmin.Should().Be(request.IsSuperAdmin);
        target.Language.Should().Be(request.Language);
    }
}
