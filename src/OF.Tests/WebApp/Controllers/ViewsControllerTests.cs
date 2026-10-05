using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using OF.Data.Database;
using OF.UI.Database;
using OF.UI.Identity;
using OF.WebApp.Controllers;

namespace OF.Tests.WebApp.Controllers;

public class ViewsControllerTests
{
    private const string LoginName = "test.user@example.com";

    private readonly Mock<IDataRepository> _repository = new();
    private readonly Mock<IUserIdentity> _identity = new();

    [Fact]
    public void GetViews_ReturnsUnauthorized_WhenIdentityIsMissing()
    {
        _identity.Setup(identity => identity.GetIdentity()).Returns((User?)null!);

        var result = CreateSubject().GetViews();

        result.Should().BeOfType<UnauthorizedResult>();
        _repository.Verify(repository => repository.GetViewsWithRecipients(), Times.Never);
    }

    [Fact]
    public void CreateView_ReturnsUnauthorizedWithoutWriting_WhenIdentityIsMissing()
    {
        _identity.Setup(identity => identity.GetIdentity()).Returns((User?)null!);

        var result = CreateSubject().CreateView(CreateRequest());

        result.Should().BeOfType<UnauthorizedResult>();
        _repository.Verify(repository => repository.AddView(It.IsAny<View>()), Times.Never);
    }

    [Fact]
    public void CreateView_CurrentBehavior_PersistsPascalCaseVersionTwoEnvelope()
    {
        SetIdentity("UK");
        var state = ParseJson("""{"viewMode":"timeline","filters":{"status":"Ready"}}""");
        View? persistedView = null;
        _repository
            .Setup(repository => repository.AddView(It.IsAny<View>()))
            .Callback<View>(view => persistedView = view)
            .Returns<View>(view =>
            {
                view.Id = 17;
                return view;
            });

        var result = CreateSubject().CreateView(new UpsertSavedViewRequest
        {
            Name = "  My asset view  ",
            Page = "assets",
            Scope = "personal",
            State = state,
        });

        result.Should().BeOfType<OkObjectResult>();
        persistedView.Should().NotBeNull();
        persistedView!.Name.Should().Be("My asset view");
        persistedView.AssetView.Should().BeTrue();
        persistedView.ForEveryone.Should().Be(0);
        persistedView.Owner.Should().Be(LoginName);
        persistedView.ViewJson.Should().Be(
            "{\"App\":\"nof-frontend\",\"Version\":2,\"Page\":\"assets\"," +
            "\"State\":{\"viewMode\":\"timeline\",\"filters\":{\"status\":\"Ready\"}}}");

        using var document = JsonDocument.Parse(persistedView.ViewJson);
        var envelope = document.RootElement;
        envelope.GetProperty("App").GetString().Should().Be("nof-frontend");
        envelope.GetProperty("Version").GetInt32().Should().Be(2);
        envelope.GetProperty("Page").GetString().Should().Be("assets");
        envelope.GetProperty("State").GetProperty("viewMode").GetString().Should().Be("timeline");
        envelope.TryGetProperty("app", out _).Should().BeFalse(
            "persisted envelope casing is a known current behavior that must be changed deliberately");
    }

    [Fact]
    public void CreateView_AdminDivisionScope_PersistsNormalizedValuesAndUsesRepositoryReturnedMetadata()
    {
        SetIdentity(" uk,IE,UK ", isAdmin: true);
        var state = ParseJson("""{"sortField":"offHireDate"}""");
        View? addedView = null;
        var repositoryResult = new View
        {
            Id = 42,
            Name = "Stored planning view",
            Owner = "stored.owner@example.com",
            ForEveryone = 2,
            ForDivisions = "DATABASE",
            AssetView = false,
            GanttView = false,
            ViewJson = "stored-json",
        };
        _repository
            .Setup(repository => repository.AddView(It.IsAny<View>()))
            .Callback<View>(view => addedView = view)
            .Returns(repositoryResult);

        var result = CreateSubject().CreateView(new UpsertSavedViewRequest
        {
            Name = "  Off-hire planning  ",
            Page = "orders",
            Scope = "division",
            State = state,
        });

        addedView.Should().NotBeNull();
        addedView!.Name.Should().Be("Off-hire planning");
        addedView.Owner.Should().Be(LoginName);
        addedView.ForEveryone.Should().Be(2);
        addedView.ForDivisions.Should().Be("uk,IE");
        addedView.AssetView.Should().BeFalse();
        addedView.GanttView.Should().BeFalse();
        addedView.ViewJson.Should().Be(
            "{\"App\":\"nof-frontend\",\"Version\":2,\"Page\":\"agreements\"," +
            "\"State\":{\"sortField\":\"offHireDate\"}}");

        var response = GetOkView(result);
        response.Id.Should().Be(42);
        response.Name.Should().Be("Stored planning view");
        response.Page.Should().Be("agreements");
        response.Scope.Should().Be("division");
        response.Owner.Should().Be("stored.owner@example.com");
        response.CanEdit.Should().BeTrue();
        response.CanDelete.Should().BeTrue();
        response.IsDefault.Should().BeFalse();
        response.State.GetRawText().Should().Be("""{"sortField":"offHireDate"}""");
    }

    [Fact]
    public void GetViews_CurrentBehavior_ReadsPascalCaseButIgnoresCamelCaseEnvelopeProperties()
    {
        SetIdentity("UK");
        _repository.Setup(repository => repository.GetViewsWithRecipients()).Returns(new[]
        {
            CreateView(1, "Pascal case", """{"App":"nof-frontend","Version":2,"Page":"agreements","State":{"viewMode":"grid"}}"""),
            CreateView(2, "Camel case", """{"app":"nof-frontend","version":2,"page":"agreements","state":{"viewMode":"timeline"}}"""),
        }.AsQueryable());

        var result = CreateSubject().GetViews("agreements");

        var views = GetOkViews(result);
        views.Should().ContainSingle()
            .Which.Name.Should().Be("Pascal case");
    }

    [Fact]
    public void GetViews_AcceptsVersionOneOrdersEnvelopeAndNormalizesPageToAgreements()
    {
        SetIdentity("UK");
        _repository.Setup(repository => repository.GetViewsWithRecipients()).Returns(new[]
        {
            CreateView(9, "Legacy agreements", """{"App":"nof-frontend","Version":1,"Page":"orders","State":{"viewMode":"grid"}}"""),
        }.AsQueryable());

        var result = CreateSubject().GetViews("orders");

        var view = GetOkViews(result).Should().ContainSingle().Subject;
        view.Page.Should().Be("agreements");
        view.State.GetProperty("viewMode").GetString().Should().Be("grid");
    }

    [Fact]
    public void GetViews_ReturnsOnlyPersonalGlobalAndMatchingDivisionViewsVisibleToCaller()
    {
        SetIdentity("UK, IE");
        _repository.Setup(repository => repository.GetViewsWithRecipients()).Returns(new[]
        {
            CreateView(1, "Owned personal", ValidEnvelope(), owner: LoginName),
            CreateView(2, "Other personal", ValidEnvelope(), owner: "other@example.com"),
            CreateView(3, "Global", ValidEnvelope(), owner: "admin@example.com", forEveryone: 1),
            CreateView(4, "Matching division", ValidEnvelope(), owner: "admin@example.com", forEveryone: 2, forDivisions: "FR,ie"),
            CreateView(5, "Other division", ValidEnvelope(), owner: "admin@example.com", forEveryone: 2, forDivisions: "FR,DE"),
        }.AsQueryable());

        var result = CreateSubject().GetViews("agreements");

        GetOkViews(result).Select(view => view.Name).Should().Equal(
            "Global",
            "Matching division",
            "Owned personal");
    }

    [Fact]
    public void GetViews_MapsOwnerPermissionsAndDefaultFlagAndOrdersByNameThenId()
    {
        SetIdentity("UK");
        _repository.Setup(repository => repository.GetViewsWithRecipients()).Returns(new[]
        {
            CreateView(7, "Same name", ValidEnvelope(), owner: LoginName),
            CreateView(1, "Default", ValidEnvelope(), owner: "admin@example.com", forEveryone: 1),
            CreateView(3, "Same name", ValidEnvelope(), owner: "admin@example.com", forEveryone: 1),
        }.AsQueryable());

        var result = CreateSubject().GetViews("agreements");

        GetOkViews(result).Select(view =>
                (view.Id, view.Name, view.CanEdit, view.CanDelete, view.IsDefault))
            .Should().Equal(
                (1, "Default", false, false, true),
                (3, "Same name", false, false, false),
                (7, "Same name", true, true, false));
    }

    [Fact]
    public void GetViews_AdminCanEditVisibleSharedViewsButDoesNotReadAnotherOwnersPersonalView()
    {
        SetIdentity("UK", isAdmin: true);
        _repository.Setup(repository => repository.GetViewsWithRecipients()).Returns(new[]
        {
            CreateView(1, "Other personal", ValidEnvelope(), owner: "other@example.com"),
            CreateView(2, "Global", ValidEnvelope(), owner: "other@example.com", forEveryone: 1),
            CreateView(3, "Division", ValidEnvelope(), owner: "other@example.com", forEveryone: 2, forDivisions: "uk"),
        }.AsQueryable());

        var result = CreateSubject().GetViews("agreements");

        var views = GetOkViews(result);
        views.Select(view => view.Name).Should().Equal("Division", "Global");
        views.Should().OnlyContain(view => view.CanEdit && view.CanDelete);
    }

    [Fact]
    public void GetViews_CurrentBehavior_TreatsBlankDivisionScopeAsVisibleWithoutDivisionOverlap()
    {
        SetIdentity("");
        _repository.Setup(repository => repository.GetViewsWithRecipients()).Returns(new[]
        {
            CreateView(
                8,
                "Blank division assignment",
                ValidEnvelope(),
                owner: "admin@example.com",
                forEveryone: 2,
                forDivisions: "  "),
        }.AsQueryable());

        var result = CreateSubject().GetViews("agreements");

        GetOkViews(result).Should().ContainSingle()
            .Which.Name.Should().Be("Blank division assignment",
                "blank division-scope visibility is unresolved current behavior and must change deliberately");
    }

    [Fact]
    public void CreateView_ReturnsForbidden_WhenNonAdminRequestsGlobalScope()
    {
        SetIdentity("UK");

        var result = CreateSubject().CreateView(new UpsertSavedViewRequest
        {
            Name = "Shared",
            Page = "agreements",
            Scope = "global",
            State = ParseJson("{}"),
        });

        result.Should().BeOfType<ForbidResult>();
        _repository.Verify(repository => repository.AddView(It.IsAny<View>()), Times.Never);
    }

    [Fact]
    public void CreateView_ReturnsBadRequest_WhenStateIsNotAnObject()
    {
        SetIdentity("UK", isAdmin: true);

        var result = CreateSubject().CreateView(new UpsertSavedViewRequest
        {
            Name = "Invalid state",
            Page = "agreements",
            Scope = "personal",
            State = ParseJson("[]"),
        });

        result.Should().BeOfType<BadRequestObjectResult>();
        _repository.Verify(repository => repository.AddView(It.IsAny<View>()), Times.Never);
    }

    [Fact]
    public void UpdateView_ReturnsUnauthorizedWithoutReadingOrWriting_WhenIdentityIsMissing()
    {
        _identity.Setup(identity => identity.GetIdentity()).Returns((User?)null!);

        var result = CreateSubject().UpdateView(12, CreateRequest());

        result.Should().BeOfType<UnauthorizedResult>();
        _repository.Verify(repository => repository.GetView(It.IsAny<int>()), Times.Never);
        _repository.Verify(repository => repository.UpdateViewWithRecipients(
            It.IsAny<View>(),
            It.IsAny<IReadOnlyCollection<string>>()), Times.Never);
    }

    [Fact]
    public void UpdateView_ValidatesRequestBeforeReadingExistingView()
    {
        SetIdentity("UK");

        var result = CreateSubject().UpdateView(12, CreateRequest(name: "  "));

        result.Should().BeOfType<BadRequestObjectResult>();
        _repository.Verify(repository => repository.GetView(It.IsAny<int>()), Times.Never);
        _repository.Verify(repository => repository.UpdateViewWithRecipients(
            It.IsAny<View>(),
            It.IsAny<IReadOnlyCollection<string>>()), Times.Never);
    }

    [Fact]
    public void UpdateView_ReturnsNotFoundWithoutWriting_WhenViewDoesNotExist()
    {
        SetIdentity("UK");
        _repository.Setup(repository => repository.GetView(12)).Returns((View)null!);

        var result = CreateSubject().UpdateView(12, CreateRequest());

        result.Should().BeOfType<NotFoundResult>();
        _repository.Verify(repository => repository.UpdateViewWithRecipients(
            It.IsAny<View>(),
            It.IsAny<IReadOnlyCollection<string>>()), Times.Never);
    }

    [Fact]
    public void UpdateView_ReturnsForbiddenWithoutWriting_ForNonOwnerNonAdmin()
    {
        SetIdentity("UK");
        _repository.Setup(repository => repository.GetView(12)).Returns(
            CreateView(12, "Other view", ValidEnvelope(), owner: "other@example.com"));

        var result = CreateSubject().UpdateView(12, CreateRequest());

        result.Should().BeOfType<ForbidResult>();
        _repository.Verify(repository => repository.UpdateViewWithRecipients(
            It.IsAny<View>(),
            It.IsAny<IReadOnlyCollection<string>>()), Times.Never);
    }

    [Fact]
    public void UpdateView_ReturnsForbiddenWithoutWriting_WhenNonAdminOwnerRequestsSharedScope()
    {
        SetIdentity("UK");
        _repository.Setup(repository => repository.GetView(12)).Returns(
            CreateView(12, "Owned view", ValidEnvelope()));

        var result = CreateSubject().UpdateView(12, CreateRequest(scope: "division"));

        result.Should().BeOfType<ForbidResult>();
        _repository.Verify(repository => repository.UpdateViewWithRecipients(
            It.IsAny<View>(),
            It.IsAny<IReadOnlyCollection<string>>()), Times.Never);
    }

    [Fact]
    public void UpdateView_OwnerMutation_PreservesExactPersistenceAndUsesRepositoryReturnedResponseMetadata()
    {
        SetIdentity("UK");
        var existing = CreateView(
            12,
            "Before",
            "old-json",
            owner: LoginName.ToUpperInvariant(),
            forEveryone: 2,
            forDivisions: "OLD");
        existing.AssetView = true;
        existing.GanttView = true;
        var repositoryResult = new View
        {
            Id = 77,
            Name = "Stored result",
            Owner = "stored.owner@example.com",
            ForEveryone = 1,
            ForDivisions = null,
            AssetView = false,
            GanttView = false,
            ViewJson = "repository-returned-json",
        };
        View? updatedView = null;
        _repository.Setup(repository => repository.UpdateViewWithRecipients(
                It.IsAny<View>(),
                It.IsAny<IReadOnlyCollection<string>>()))
            .Callback<View, IReadOnlyCollection<string>>((view, _) => updatedView = view)
            .Returns(repositoryResult);
        _repository.Setup(repository => repository.GetView(12)).Returns(existing);
        var state = ParseJson("""{"viewMode":"table","sortField":"agreementNumber"}""");

        var result = CreateSubject().UpdateView(12, new UpsertSavedViewRequest
        {
            Name = "  Renamed view  ",
            Page = "orders",
            Scope = "personal",
            State = state,
        });

        updatedView.Should().BeSameAs(existing);
        updatedView!.Name.Should().Be("Renamed view");
        updatedView.Owner.Should().Be(LoginName.ToUpperInvariant());
        updatedView.ForEveryone.Should().Be(0);
        updatedView.ForDivisions.Should().BeNull();
        updatedView.AssetView.Should().BeFalse();
        updatedView.GanttView.Should().BeFalse();
        updatedView.ViewJson.Should().Be(
            "{\"App\":\"nof-frontend\",\"Version\":2,\"Page\":\"agreements\"," +
            "\"State\":{\"viewMode\":\"table\",\"sortField\":\"agreementNumber\"}}");

        var response = GetOkView(result);
        response.Id.Should().Be(77);
        response.Name.Should().Be("Stored result");
        response.Page.Should().Be("agreements");
        response.Scope.Should().Be("global");
        response.Owner.Should().Be("stored.owner@example.com");
        response.CanEdit.Should().BeFalse();
        response.CanDelete.Should().BeFalse();
        response.IsDefault.Should().BeFalse();
        response.State.GetRawText().Should().Be(
            """{"viewMode":"table","sortField":"agreementNumber"}""");
    }

    [Fact]
    public void UpdateView_AdminCanUpdateAnotherOwnersDivisionScopeUsingNormalizedCallerDivisions()
    {
        SetIdentity(" uk,IE,UK ", isAdmin: true);
        var existing = CreateView(12, "Other view", ValidEnvelope(), owner: "other@example.com");
        _repository.Setup(repository => repository.GetView(12)).Returns(existing);
        _repository.Setup(repository => repository.UpdateViewWithRecipients(
            existing,
            It.IsAny<IReadOnlyCollection<string>>())).Returns(existing);

        var result = CreateSubject().UpdateView(12, CreateRequest(page: "assets", scope: "division"));

        existing.Owner.Should().Be("other@example.com");
        existing.ForEveryone.Should().Be(2);
        existing.ForDivisions.Should().Be("uk,IE");
        existing.AssetView.Should().BeTrue();
        existing.GanttView.Should().BeFalse();
        var response = GetOkView(result);
        response.Scope.Should().Be("division");
        response.CanEdit.Should().BeTrue();
        response.CanDelete.Should().BeTrue();
    }

    [Fact]
    public void DeleteView_ReturnsUnauthorizedWithoutReadingOrDeleting_WhenIdentityIsMissing()
    {
        _identity.Setup(identity => identity.GetIdentity()).Returns((User?)null!);

        var result = CreateSubject().DeleteView(12);

        result.Should().BeOfType<UnauthorizedResult>();
        _repository.Verify(repository => repository.GetView(It.IsAny<int>()), Times.Never);
        _repository.Verify(repository => repository.DeleteView(It.IsAny<View>()), Times.Never);
    }

    [Fact]
    public void DeleteView_ReturnsNotFoundWithoutDeleting_WhenViewDoesNotExist()
    {
        SetIdentity("UK");
        _repository.Setup(repository => repository.GetView(12)).Returns((View)null!);

        var result = CreateSubject().DeleteView(12);

        result.Should().BeOfType<NotFoundResult>();
        _repository.Verify(repository => repository.DeleteView(It.IsAny<View>()), Times.Never);
    }

    [Fact]
    public void DeleteView_ReturnsForbiddenWithoutDeleting_ForNonOwnerNonAdmin()
    {
        SetIdentity("UK");
        _repository.Setup(repository => repository.GetView(12)).Returns(
            CreateView(12, "Other view", ValidEnvelope(), owner: "other@example.com"));

        var result = CreateSubject().DeleteView(12);

        result.Should().BeOfType<ForbidResult>();
        _repository.Verify(repository => repository.DeleteView(It.IsAny<View>()), Times.Never);
    }

    [Fact]
    public void DeleteView_DeletesExactFetchedView_ForCaseInsensitiveOwnerMatch()
    {
        SetIdentity("UK");
        var existing = CreateView(
            12,
            "Owned view",
            ValidEnvelope(),
            owner: LoginName.ToUpperInvariant());
        _repository.Setup(repository => repository.GetView(12)).Returns(existing);

        var result = CreateSubject().DeleteView(12);

        result.Should().BeOfType<NoContentResult>();
        _repository.Verify(repository => repository.DeleteView(existing), Times.Once);
    }

    [Fact]
    public void DeleteView_CurrentBehavior_AllowsAdminToDeleteDefaultId()
    {
        SetIdentity("UK", isAdmin: true);
        var defaultView = CreateView(
            1,
            "Default",
            ValidEnvelope(),
            owner: "other@example.com",
            forEveryone: 1);
        _repository.Setup(repository => repository.GetView(1)).Returns(defaultView);

        var result = CreateSubject().DeleteView(1);

        result.Should().BeOfType<NoContentResult>(
            "default-ID mutation is unresolved current behavior and must change deliberately");
        _repository.Verify(repository => repository.DeleteView(defaultView), Times.Once);
    }

    private ViewsController CreateSubject() => new(_repository.Object, _identity.Object);

    private static UpsertSavedViewRequest CreateRequest(
        string name = "Saved view",
        string page = "agreements",
        string scope = "personal")
    {
        return new UpsertSavedViewRequest
        {
            Name = name,
            Page = page,
            Scope = scope,
            State = ParseJson("{}"),
        };
    }

    private void SetIdentity(string division, bool isAdmin = false)
    {
        _identity.Setup(identity => identity.GetIdentity()).Returns(new User
        {
            LoginName = LoginName,
            FullName = "Test User",
            Division = division,
            IsAdmin = isAdmin,
            DateFormat = "dd/MM/yyyy",
        });
    }

    private static IReadOnlyList<SavedViewDto> GetOkViews(IActionResult result)
    {
        return result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeAssignableTo<IEnumerable<SavedViewDto>>().Subject
            .ToList();
    }

    private static SavedViewDto GetOkView(IActionResult result)
    {
        return result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<SavedViewDto>().Subject;
    }

    private static View CreateView(
        int id,
        string name,
        string viewJson,
        string owner = LoginName,
        int forEveryone = 0,
        string? forDivisions = null)
    {
        return new View
        {
            Id = id,
            Name = name,
            Owner = owner,
            ForEveryone = forEveryone,
            ForDivisions = forDivisions,
            AssetView = false,
            ViewJson = viewJson,
        };
    }

    private static string ValidEnvelope()
    {
        return """{"App":"nof-frontend","Version":2,"Page":"agreements","State":{}}""";
    }

    private static JsonElement ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
