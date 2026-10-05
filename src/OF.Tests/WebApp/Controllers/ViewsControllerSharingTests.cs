using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using OF.Common;
using OF.Data.Database;
using OF.UI.Database;
using OF.UI.Identity;
using OF.WebApp.Controllers;

namespace OF.Tests.WebApp.Controllers;

public class ViewsControllerSharingTests
{
    private const string OwnerLogin = "owner@example.com";

    private readonly Mock<IDataRepository> _repository = new();
    private readonly Mock<IUserIdentity> _identity = new();

    [Fact]
    public void GetRecipientCandidates_ReturnsOnlyMatchingConfiguredUsersInCallerDivisions()
    {
        SetIdentity("UK,IE");
        _repository.Setup(repository => repository.GetUsers()).Returns(new[]
        {
            CreateUser(OwnerLogin, "Owner", "UK"),
            CreateUser("zara@example.com", "Zara", "UK"),
            CreateUser("alex@example.com", "Alex", "FR,IE"),
            CreateUser("outsider@example.com", "Outsider", "FR"),
        }.AsQueryable());

        var result = CreateSubject().GetRecipientCandidates("a");

        var candidates = GetOk<IEnumerable<SavedViewRecipientDto>>(result).ToList();
        candidates.Select(candidate => candidate.LoginName).Should().Equal(
            "alex@example.com",
            "zara@example.com");
    }

    [Fact]
    public void GetRecipientCandidates_WithoutSearch_ReturnsEmptyWithoutQueryingUsers()
    {
        SetIdentity("UK");

        var result = CreateSubject().GetRecipientCandidates();

        GetOk<IEnumerable<SavedViewRecipientDto>>(result).Should().BeEmpty();
        _repository.Verify(repository => repository.GetUsers(), Times.Never);
    }

    [Fact]
    public void GetRecipientCandidates_OrdersBestMatchesAndAppliesRequestedLimit()
    {
        SetIdentity("UK");
        _repository.Setup(repository => repository.GetUsers()).Returns(new[]
        {
            CreateUser("surname@example.com", "Taylor Alex", "UK"),
            CreateUser("alex.login@example.com", "Unrelated Person", "UK"),
            CreateUser("contains@example.com", "Calex Person", "UK"),
            CreateUser("word@example.com", "Taylor Alexson", "UK"),
            CreateUser("exact@example.com", "Alex", "UK"),
        }.AsQueryable());

        var result = CreateSubject().GetRecipientCandidates(" alex ", limit: 4);

        GetOk<IEnumerable<SavedViewRecipientDto>>(result)
            .Select(candidate => candidate.LoginName)
            .Should().Equal(
                "exact@example.com",
                "alex.login@example.com",
                "surname@example.com",
                "word@example.com");
    }

    [Fact]
    public void GetRecipientCandidates_UsesBoundedDefaultLimit()
    {
        SetIdentity("UK");
        _repository.Setup(repository => repository.GetUsers()).Returns(
            Enumerable.Range(1, 12)
                .Select(index => CreateUser(
                    $"match{index:D2}@example.com",
                    $"Match {index:D2}",
                    "UK"))
                .AsQueryable());

        var result = CreateSubject().GetRecipientCandidates("match");

        GetOk<IEnumerable<SavedViewRecipientDto>>(result).Should().HaveCount(10);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(26)]
    public void GetRecipientCandidates_RejectsLimitOutsideBoundedRange(int limit)
    {
        SetIdentity("UK");

        var result = CreateSubject().GetRecipientCandidates("alex", limit);

        result.Should().BeOfType<BadRequestObjectResult>();
        _repository.Verify(repository => repository.GetUsers(), Times.Never);
    }

    [Fact]
    public void GetRecipientCandidates_SuperAdminCanSelectAcrossDivisions()
    {
        SetIdentity("UK", isSuperAdmin: true);
        _repository.Setup(repository => repository.GetUsers()).Returns(new[]
        {
            CreateUser(OwnerLogin, "Owner", "UK"),
            CreateUser("global@example.com", "Global User", "FR"),
        }.AsQueryable());

        var result = CreateSubject().GetRecipientCandidates("global");

        GetOk<IEnumerable<SavedViewRecipientDto>>(result)
            .Should().ContainSingle()
            .Which.LoginName.Should().Be("global@example.com");
    }

    [Fact]
    public void GetRecipientCandidates_ReadOnlyUserIsForbiddenWithoutQueryingUsers()
    {
        SetIdentity("UK", roles: Constants.Roles.ReadOnly);

        var result = CreateSubject().GetRecipientCandidates("recipient");

        result.Should().BeOfType<ForbidResult>();
        _repository.Verify(repository => repository.GetUsers(), Times.Never);
    }

    [Fact]
    public void GetViews_RecipientCanReadUsersScopeButCannotManageIt()
    {
        SetIdentity("UK", loginName: "recipient@example.com");
        var sharedView = CreateView(owner: OwnerLogin);
        sharedView.ViewRecipients.Add(new ViewRecipient
        {
            ViewId = sharedView.Id,
            RecipientLoginName = "RECIPIENT@example.com",
            Recipient = CreateUser("recipient@example.com", "Recipient", "UK"),
        });
        _repository.Setup(repository => repository.GetViewsWithRecipients())
            .Returns(new[] { sharedView }.AsQueryable());

        var result = CreateSubject().GetViews();

        var view = GetOk<IEnumerable<SavedViewDto>>(result).Should().ContainSingle().Subject;
        view.Scope.Should().Be("users");
        view.Owner.Should().Be(OwnerLogin);
        view.IsOwner.Should().BeFalse();
        view.CanEdit.Should().BeFalse();
        view.CanDelete.Should().BeFalse();
        view.Recipients.Should().BeEmpty("recipient metadata is only needed by a manager");
    }

    [Fact]
    public void GetViews_UnrelatedAdminDoesNotGainVisibilityToUsersScope()
    {
        SetIdentity("UK", loginName: "admin@example.com", isAdmin: true);
        var sharedView = CreateView(owner: OwnerLogin);
        sharedView.ViewRecipients.Add(new ViewRecipient
        {
            ViewId = sharedView.Id,
            RecipientLoginName = "recipient@example.com",
        });
        _repository.Setup(repository => repository.GetViewsWithRecipients())
            .Returns(new[] { sharedView }.AsQueryable());

        var result = CreateSubject().GetViews();

        GetOk<IEnumerable<SavedViewDto>>(result).Should().BeEmpty();
    }

    [Fact]
    public void GetViews_ReadOnlyOwnerCannotManageOwnedView()
    {
        SetIdentity("UK", roles: Constants.Roles.ReadOnly);
        var ownedView = CreateView(owner: OwnerLogin);
        _repository.Setup(repository => repository.GetViewsWithRecipients())
            .Returns(new[] { ownedView }.AsQueryable());

        var result = CreateSubject().GetViews();

        var view = GetOk<IEnumerable<SavedViewDto>>(result).Should().ContainSingle().Subject;
        view.IsOwner.Should().BeTrue();
        view.CanEdit.Should().BeFalse();
        view.CanDelete.Should().BeFalse();
    }

    [Fact]
    public void GetViews_ReadOnlyAdminCannotManageVisibleView()
    {
        SetIdentity(
            "UK",
            loginName: "admin@example.com",
            isAdmin: true,
            roles: Constants.Roles.ReadOnly);
        var globalView = CreateView(owner: OwnerLogin);
        globalView.ForEveryone = 1;
        _repository.Setup(repository => repository.GetViewsWithRecipients())
            .Returns(new[] { globalView }.AsQueryable());

        var result = CreateSubject().GetViews();

        var view = GetOk<IEnumerable<SavedViewDto>>(result).Should().ContainSingle().Subject;
        view.IsOwner.Should().BeFalse();
        view.CanEdit.Should().BeFalse();
        view.CanDelete.Should().BeFalse();
    }

    [Fact]
    public void CreateView_UsersScope_NormalizesAndPersistsEligibleRecipientsAtomically()
    {
        SetIdentity("UK");
        _repository.Setup(repository => repository.GetUsers()).Returns(new[]
        {
            CreateUser("RECIPIENT@example.com", "Recipient User", "UK"),
        }.AsQueryable());
        View? persistedView = null;
        IReadOnlyCollection<string>? persistedRecipients = null;
        _repository
            .Setup(repository => repository.AddViewWithRecipients(
                It.IsAny<View>(),
                It.IsAny<IReadOnlyCollection<string>>()))
            .Callback<View, IReadOnlyCollection<string>>((view, recipients) =>
            {
                persistedView = view;
                persistedRecipients = recipients;
                view.Id = 23;
            })
            .Returns<View, IReadOnlyCollection<string>>((view, _) => view);

        var result = CreateSubject().CreateView(CreateRequest(
            "users",
            [" recipient@example.com ", "RECIPIENT@example.com", OwnerLogin]));

        persistedView.Should().NotBeNull();
        persistedView!.ForEveryone.Should().Be(0);
        persistedView.ForDivisions.Should().BeNull();
        persistedRecipients.Should().Equal("RECIPIENT@example.com");
        _repository.Verify(repository => repository.AddView(It.IsAny<View>()), Times.Never);

        var response = GetOk<SavedViewDto>(result);
        response.Id.Should().Be(23);
        response.Scope.Should().Be("users");
        response.IsOwner.Should().BeTrue();
        response.Recipients.Should().ContainSingle()
            .Which.FullName.Should().Be("Recipient User");
    }

    [Fact]
    public void CreateView_UsersScope_RejectsInvalidRecipientsWithoutWriting()
    {
        SetIdentity("UK");
        _repository.Setup(repository => repository.GetUsers()).Returns(new[]
        {
            CreateUser("outsider@example.com", "Outsider", "FR"),
        }.AsQueryable());

        var result = CreateSubject().CreateView(CreateRequest("users", ["outsider@example.com"]));

        result.Should().BeOfType<BadRequestObjectResult>();
        _repository.Verify(repository => repository.AddView(It.IsAny<View>()), Times.Never);
        _repository.Verify(repository => repository.AddViewWithRecipients(
            It.IsAny<View>(),
            It.IsAny<IReadOnlyCollection<string>>()), Times.Never);
    }

    [Fact]
    public void CreateView_UsersScope_ReturnsBadRequestWhenRecipientIsDeletedDuringWrite()
    {
        SetIdentity("UK");
        _repository.Setup(repository => repository.GetUsers()).Returns(new[]
        {
            CreateUser("recipient@example.com", "Recipient", "UK"),
        }.AsQueryable());
        _repository.Setup(repository => repository.AddViewWithRecipients(
                It.IsAny<View>(),
                It.IsAny<IReadOnlyCollection<string>>()))
            .Throws(new DbUpdateException("FK recipient deleted"));

        var result = CreateSubject().CreateView(CreateRequest("users", ["recipient@example.com"]));

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        JsonSerializer.Serialize(badRequest.Value).Should().Contain("no longer available");
        _repository.Verify(repository => repository.AddView(It.IsAny<View>()), Times.Never);
    }

    [Fact]
    public void UpdateView_RecipientCannotModifySharedView()
    {
        SetIdentity("UK", loginName: "recipient@example.com");
        var sharedView = CreateView(owner: OwnerLogin);
        sharedView.ViewRecipients.Add(new ViewRecipient
        {
            ViewId = sharedView.Id,
            RecipientLoginName = "recipient@example.com",
        });
        _repository.Setup(repository => repository.GetView(sharedView.Id))
            .Returns(sharedView);

        var result = CreateSubject().UpdateView(
            sharedView.Id,
            CreateRequest("users", ["recipient@example.com"]));

        result.Should().BeOfType<ForbidResult>();
        _repository.Verify(repository => repository.GetUsers(), Times.Never);
        _repository.Verify(repository => repository.UpdateViewWithRecipients(
            It.IsAny<View>(),
            It.IsAny<IReadOnlyCollection<string>>()), Times.Never);
    }

    [Fact]
    public void UpdateView_RecipientCannotReshareSharedViewWithDifferentRecipients()
    {
        SetIdentity("UK", loginName: "recipient@example.com");
        var sharedView = CreateView(owner: OwnerLogin);
        sharedView.ViewRecipients.Add(new ViewRecipient
        {
            ViewId = sharedView.Id,
            RecipientLoginName = "recipient@example.com",
        });
        _repository.Setup(repository => repository.GetView(sharedView.Id))
            .Returns(sharedView);

        var result = CreateSubject().UpdateView(
            sharedView.Id,
            CreateRequest("users", ["another@example.com"]));

        result.Should().BeOfType<ForbidResult>();
        _repository.Verify(repository => repository.GetUsers(), Times.Never);
        _repository.Verify(repository => repository.UpdateViewWithRecipients(
            It.IsAny<View>(),
            It.IsAny<IReadOnlyCollection<string>>()), Times.Never);
    }

    [Fact]
    public void DeleteView_RecipientCannotDeleteSharedView()
    {
        SetIdentity("UK", loginName: "recipient@example.com");
        var sharedView = CreateView(owner: OwnerLogin);
        sharedView.ViewRecipients.Add(new ViewRecipient
        {
            ViewId = sharedView.Id,
            RecipientLoginName = "recipient@example.com",
        });
        _repository.Setup(repository => repository.GetView(sharedView.Id))
            .Returns(sharedView);

        var result = CreateSubject().DeleteView(sharedView.Id);

        result.Should().BeOfType<ForbidResult>();
        _repository.Verify(repository => repository.DeleteView(It.IsAny<View>()), Times.Never);
    }

    [Fact]
    public void UpdateView_AdminRetainsManagementOverrideWithinRecipientDivisionBoundary()
    {
        SetIdentity("UK", loginName: "admin@example.com", isAdmin: true);
        var sharedView = CreateView(owner: OwnerLogin);
        _repository.Setup(repository => repository.GetView(sharedView.Id)).Returns(sharedView);
        _repository.Setup(repository => repository.GetUsers()).Returns(new[]
        {
            CreateUser("recipient@example.com", "Recipient", "UK"),
            CreateUser("outsider@example.com", "Outsider", "FR"),
        }.AsQueryable());
        _repository.Setup(repository => repository.UpdateViewWithRecipients(
                sharedView,
                It.IsAny<IReadOnlyCollection<string>>()))
            .Returns(sharedView);

        var allowedResult = CreateSubject().UpdateView(
            sharedView.Id,
            CreateRequest("users", ["recipient@example.com"]));
        var rejectedResult = CreateSubject().UpdateView(
            sharedView.Id,
            CreateRequest("users", ["outsider@example.com"]));

        allowedResult.Should().BeOfType<OkObjectResult>();
        rejectedResult.Should().BeOfType<BadRequestObjectResult>();
        _repository.Verify(repository => repository.UpdateViewWithRecipients(
            sharedView,
            It.Is<IReadOnlyCollection<string>>(recipients =>
                recipients.SequenceEqual(new[] { "recipient@example.com" }))), Times.Once);
    }

    [Fact]
    public void UpdateView_OwnerReplacesRecipientsAndReceivesMetadata()
    {
        SetIdentity("UK");
        var sharedView = CreateView(owner: OwnerLogin);
        _repository.Setup(repository => repository.GetView(sharedView.Id))
            .Returns(sharedView);
        _repository.Setup(repository => repository.GetUsers()).Returns(new[]
        {
            CreateUser("new@example.com", "New Recipient", "UK"),
        }.AsQueryable());
        _repository.Setup(repository => repository.UpdateViewWithRecipients(
                sharedView,
                It.IsAny<IReadOnlyCollection<string>>()))
            .Returns(sharedView);

        var result = CreateSubject().UpdateView(
            sharedView.Id,
            CreateRequest("users", ["new@example.com"]));

        _repository.Verify(repository => repository.UpdateViewWithRecipients(
            sharedView,
            It.Is<IReadOnlyCollection<string>>(recipients => recipients.SequenceEqual(new[] { "new@example.com" }))),
            Times.Once);
        var response = GetOk<SavedViewDto>(result);
        response.Scope.Should().Be("users");
        response.Recipients.Should().ContainSingle()
            .Which.LoginName.Should().Be("new@example.com");
    }

    [Fact]
    public void UpdateView_UsersScope_ReturnsBadRequestWhenRecipientIsDeletedDuringWrite()
    {
        SetIdentity("UK");
        var sharedView = CreateView(owner: OwnerLogin);
        _repository.Setup(repository => repository.GetView(sharedView.Id)).Returns(sharedView);
        _repository.Setup(repository => repository.GetUsers()).Returns(new[]
        {
            CreateUser("recipient@example.com", "Recipient", "UK"),
        }.AsQueryable());
        _repository.Setup(repository => repository.UpdateViewWithRecipients(
                sharedView,
                It.IsAny<IReadOnlyCollection<string>>()))
            .Throws(new DbUpdateException("FK recipient deleted"));

        var result = CreateSubject().UpdateView(
            sharedView.Id,
            CreateRequest("users", ["recipient@example.com"]));

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        JsonSerializer.Serialize(badRequest.Value).Should().Contain("no longer available");
        _repository.Verify(repository => repository.UpdateView(It.IsAny<View>()), Times.Never);
    }

    private ViewsController CreateSubject() => new(_repository.Object, _identity.Object);

    private void SetIdentity(
        string division,
        string loginName = OwnerLogin,
        bool isAdmin = false,
        bool isSuperAdmin = false,
        string? roles = null)
    {
        _identity.Setup(identity => identity.GetIdentity()).Returns(
            CreateUser(loginName, "Current User", division, isAdmin, isSuperAdmin, roles));
    }

    private static User CreateUser(
        string loginName,
        string fullName,
        string division,
        bool isAdmin = false,
        bool isSuperAdmin = false,
        string? roles = null) => new()
    {
        LoginName = loginName,
        FullName = fullName,
        Division = division,
        IsAdmin = isAdmin,
        IsSuperAdmin = isSuperAdmin,
        Roles = roles,
        DateFormat = "dd/MM/yyyy",
    };

    private static View CreateView(string owner) => new()
    {
        Id = 12,
        Name = "Shared view",
        Owner = owner,
        ForEveryone = 0,
        AssetView = false,
        ViewJson = """{"App":"nof-frontend","Version":2,"Page":"agreements","State":{}}""",
    };

    private static UpsertSavedViewRequest CreateRequest(string scope, string[] recipients) => new()
    {
        Name = "Shared view",
        Page = "agreements",
        Scope = scope,
        Recipients = recipients,
        State = ParseJson("{}"),
    };

    private static T GetOk<T>(IActionResult result) => result
        .Should().BeOfType<OkObjectResult>().Subject.Value
        .Should().BeAssignableTo<T>().Subject;

    private static JsonElement ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
