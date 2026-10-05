using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Time.Testing;
using Moq;
using OF.Data.Database;
using OF.UI.Database;
using OF.UI.Identity;
using OF.WebApp.Controllers;
using OF.WebApp.Features.Ringfences;
using RingfenceAssestDetails = OF.UI.Models.RingfenceAssestDetails;

namespace OF.Tests.WebApp.Controllers;

public class RingfenceControllerTests
{
    private readonly Mock<IDataRepository> _repository = new();
    private readonly Mock<IUserIdentity> _identity = new();
    private readonly FakeTimeProvider _timeProvider = new(
        new DateTimeOffset(2026, 8, 24, 9, 0, 0, TimeSpan.Zero));

    [Theory]
    [InlineData("list")]
    [InlineData("detail")]
    [InlineData("create")]
    [InlineData("update")]
    [InlineData("delete")]
    [InlineData("add-item")]
    [InlineData("remove-item")]
    public void Routes_ReturnUnauthorizedWithoutRepositoryAccess_WhenIdentityIsMissing(string action)
    {
        SetMissingIdentity();

        var result = Invoke(action);

        result.Should().BeOfType<UnauthorizedResult>();
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public void GetRingfences_ReturnsAnyOverlappingDivisionsIgnoringCaseAndWhitespace_InDescendingDateOrderWithExactShape()
    {
        SetIdentity(" uk, IE ");
        SetRingfences(
            CreateRingfence(1, "UK", new DateTime(2026, 1, 1), owner: "uk-owner@example.com"),
            CreateRingfence(2, " FR, ie ", new DateTime(2026, 3, 1), owner: null),
            CreateRingfence(3, "FR", new DateTime(2026, 4, 1)),
            CreateRingfence(4, " , ", new DateTime(2026, 5, 1)));

        var result = CreateSubject().GetRingfences();

        GetListResponse(result).Select(row => row.Id).Should().Equal(2, 1);
        var rows = GetJson(result).EnumerateArray().ToList();
        rows.Select(row => row.GetProperty("id").GetInt32()).Should().Equal(2, 1);
        rows[0].EnumerateObject().Select(property => property.Name).Should().Equal(
            "id",
            "title",
            "fromDate",
            "toDate",
            "divisions",
            "warehouse",
            "owner",
            "createdBy",
            "createdAt",
            "assetCount");
        rows[0].GetProperty("title").GetString().Should().Be("Ringfence 2");
        rows[0].GetProperty("divisions").GetString().Should().Be(" FR, ie ");
        rows[0].GetProperty("warehouse").GetString().Should().Be("ED1");
        rows[0].GetProperty("owner").ValueKind.Should().Be(JsonValueKind.Null);
        rows[0].GetProperty("createdBy").GetString().Should().Be("creator@example.com");
    }

    [Fact]
    public void GetRingfences_SerializesToExactCurrentWebJsonContract()
    {
        SetIdentity("IE");
        SetRingfences(CreateRingfence(
            2,
            " FR, ie ",
            new DateTime(2026, 3, 1),
            owner: null));

        var result = CreateSubject().GetRingfences();

        var response = GetListResponse(result);
        JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .Should().Be(
                "[{\"id\":2,\"title\":\"Ringfence 2\",\"fromDate\":\"2026-03-01T00:00:00\","
                + "\"toDate\":\"2026-12-31T00:00:00\",\"divisions\":\" FR, ie \",\"warehouse\":\"ED1\","
                + "\"owner\":null,\"createdBy\":\"creator@example.com\",\"createdAt\":\"2025-12-01T12:00:00+00:00\",\"assetCount\":0}]");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" , ")]
    public void GetRingfences_ReturnsEmpty_WhenNormalUserHasNoAssignedDivisions(string? assignedDivisions)
    {
        SetIdentity(assignedDivisions);
        SetRingfences(CreateRingfence(1, "UK"));

        var result = CreateSubject().GetRingfences();

        GetJson(result).EnumerateArray().Should().BeEmpty();
        _repository.Verify(repository => repository.GetRingfences(), Times.Once);
    }

    [Fact]
    public void GetRingfences_ReturnsAllRingfencesForSuperAdmin_IncludingBlankDivisions()
    {
        SetIdentity(null, isSuperAdmin: true);
        SetRingfences(
            CreateRingfence(1, null, new DateTime(2026, 1, 1)),
            CreateRingfence(2, " , ", new DateTime(2026, 2, 1)),
            CreateRingfence(3, "FR", new DateTime(2026, 3, 1)));

        var result = CreateSubject().GetRingfences();

        GetJson(result).EnumerateArray()
            .Select(row => row.GetProperty("id").GetInt32())
            .Should().Equal(3, 2, 1);
    }

    [Fact]
    public void GetRingfences_ReturnsBatchedAssetCountsForVisibleRingfences()
    {
        SetIdentity("UK");
        SetRingfences(
            CreateRingfence(1, "UK", new DateTime(2026, 9, 1)),
            CreateRingfence(2, "UK", new DateTime(2026, 9, 2)),
            CreateRingfence(3, "FR", new DateTime(2026, 9, 3)));
        _repository.Setup(repository => repository.GetRingfenceItemCounts(
                It.Is<IReadOnlyCollection<int>>(ids => ids.OrderBy(id => id).SequenceEqual(new[] { 1, 2 }))))
            .Returns(new Dictionary<int, int> { [1] = 4, [2] = 1 });

        var result = CreateSubject().GetRingfences();

        var rows = GetListResponse(result);
        rows.Should().BeEquivalentTo(
            [
                new { Id = 2, AssetCount = 1 },
                new { Id = 1, AssetCount = 4 },
            ],
            options => options.ExcludingMissingMembers());
        _repository.Verify(repository => repository.GetRingfenceItemCounts(
            It.Is<IReadOnlyCollection<int>>(ids => ids.OrderBy(id => id).SequenceEqual(new[] { 1, 2 }))), Times.Once);
    }

    [Theory]
    [InlineData(false, "UK", "UK")]
    [InlineData(true, "UK", "FR")]
    [InlineData(true, "", "UK")]
    [InlineData(true, "UK", " , ")]
    public void GetRingfence_ReturnsNotFoundWithoutReadingItems_WhenParentIsMissingOrInaccessible(
        bool parentExists,
        string? callerDivisions,
        string? ringfenceDivisions)
    {
        SetIdentity(callerDivisions);
        if (parentExists)
        {
            _repository.Setup(repository => repository.GetRingfence(42))
                .Returns(CreateRingfence(42, ringfenceDivisions));
        }

        var result = CreateSubject().GetRingfence(42);

        result.Should().BeOfType<NotFoundResult>();
        _repository.Verify(repository => repository.GetRingfence(42), Times.Once);
        _repository.Verify(repository => repository.GetRingfenceItems(It.IsAny<int>()), Times.Never);
    }

    [Theory]
    [InlineData(" uk, IE ", false, "FR, Uk")]
    [InlineData(null, true, null)]
    public void GetRingfence_ReturnsParentAndItems_ForCollaboratorOrSuperAdmin(
        string? callerDivisions,
        bool isSuperAdmin,
        string? ringfenceDivisions)
    {
        SetIdentity(callerDivisions, isSuperAdmin);
        var ringfence = CreateRingfence(42, ringfenceDivisions, owner: "someone-else@example.com");
        var items = new[]
        {
            CreateRingfenceItem(8, 42, "ASSET-8", "recent@example.com"),
            CreateRingfenceItem(7, 42, "ASSET-7", "historical@example.com"),
        };
        _repository.Setup(repository => repository.GetRingfence(42)).Returns(ringfence);
        _repository.Setup(repository => repository.GetRingfenceItems(42))
            .Returns(items.AsQueryable());

        var result = CreateSubject().GetRingfence(42);

        var response = GetDetailResponse(result);
        AssertRingfenceResponseMatches(response.Ringfence, ringfence);
        response.Items.Select(item => item.Id).Should().Equal(8, 7);
        AssertRingfenceItemResponseMatches(response.Items[0], items[0]);
        AssertRingfenceItemResponseMatches(response.Items[1], items[1]);
        _repository.Verify(repository => repository.GetRingfenceItems(42), Times.Once);
    }

    [Fact]
    public void GetRingfence_SerializesFullyPopulatedCurrentWebJsonContract()
    {
        SetIdentity("UK");
        var ringfence = CreateFullyPopulatedRingfence();
        var item = CreateFullyPopulatedRingfenceItem();
        _repository.Setup(repository => repository.GetRingfence(ringfence.Id)).Returns(ringfence);
        _repository.Setup(repository => repository.GetRingfenceItems(ringfence.Id))
            .Returns(new[] { item }.AsQueryable());

        var response = GetDetailResponse(CreateSubject().GetRingfence(ringfence.Id));

        SerializeWeb(response).Should().Be(
            """{"ringfence":{"id":42,"fromDate":"2026-01-01T08:30:00Z","toDate":"2026-12-31T17:45:00Z","title":"Annual fleet ringfence","lastUpdatedBy":"editor@example.com","lastUpdatedDate":"2026-08-14T12:34:56Z","divisions":"UK,IE","owner":"owner@example.com","warehouse":"ED1","createdAt":"2025-12-01T12:00:00+01:00","createdBy":"creator@example.com"},"items":[{"id":7,"ringfenceId":42,"assetId":"ASSET-7","lastUpdatedBy":"item.editor@example.com","lastUpdatedDate":"2026-08-15T13:35:57Z","createdAt":"2025-12-02T12:00:00+00:00","createdBy":"item.creator@example.com"}],"assets":[]}""");
    }

    [Fact]
    public void GetRingfence_PreservesNullAndDefaultValuesInCurrentWebJsonContract()
    {
        SetIdentity(null, isSuperAdmin: true);
        var ringfence = CreateNullAndDefaultRingfence();
        var item = CreateNullAndDefaultRingfenceItem();
        _repository.Setup(repository => repository.GetRingfence(ringfence.Id)).Returns(ringfence);
        _repository.Setup(repository => repository.GetRingfenceItems(ringfence.Id))
            .Returns(new[] { item }.AsQueryable());

        var response = GetDetailResponse(CreateSubject().GetRingfence(ringfence.Id));

        SerializeWeb(response).Should().Be(
            """{"ringfence":{"id":0,"fromDate":"0001-01-01T00:00:00","toDate":"0001-01-01T00:00:00","title":null,"lastUpdatedBy":null,"lastUpdatedDate":null,"divisions":null,"owner":null,"warehouse":null,"createdAt":"0001-01-01T00:00:00+00:00","createdBy":null},"items":[{"id":0,"ringfenceId":null,"assetId":null,"lastUpdatedBy":null,"lastUpdatedDate":null,"createdAt":"0001-01-01T00:00:00+00:00","createdBy":null}],"assets":[]}""");
    }

    [Fact]
    public void CreateRingfence_CreatesExactMappingAndCreatedAtAction_WhenAllDivisionsAreAssigned()
    {
        SetIdentity(" uk, IE ");
        var request = CreateRequest(divisions: " ie, UK, uk ");
        Ringfence? captured = null;
        var persisted = CreateRingfence(73, request.Divisions, request.FromDate, owner: request.Owner);
        persisted.Title = request.Title;
        persisted.ToDate = request.ToDate;
        persisted.Warehouse = request.Warehouse;
        persisted.LastUpdatedBy = "planner@example.com";
        persisted.LastUpdatedDate = new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);
        _repository.Setup(repository => repository.CreateRingfence(_identity.Object, It.IsAny<Ringfence>()))
            .Callback<IUserIdentity, Ringfence>((_, ringfence) => captured = ringfence)
            .Returns(persisted);

        var result = CreateSubject().CreateRingfence(request);

        var response = GetCreatedRingfenceResponse(result, 73);
        captured.Should().NotBeNull();
        AssertRingfenceResponseMatches(response, persisted);
        captured!.Title.Should().Be(request.Title);
        captured.FromDate.Should().Be(request.FromDate);
        captured.ToDate.Should().Be(request.ToDate);
        captured.Divisions.Should().Be(request.Divisions);
        captured.Warehouse.Should().Be(request.Warehouse);
        captured.Owner.Should().Be(request.Owner);
        captured.CreatedBy.Should().Be("planner@example.com");
        _repository.Verify(repository => repository.CreateRingfence(_identity.Object, captured), Times.Once);
    }

    [Fact]
    public void CreateRingfence_ReturnsForbiddenWithoutWriting_WhenAnyDivisionIsUnassigned()
    {
        SetIdentity("UK,IE");

        var result = CreateSubject().CreateRingfence(CreateRequest(divisions: "UK,FR"));

        result.Should().BeOfType<ForbidResult>();
        _repository.Verify(repository => repository.CreateRingfence(
            It.IsAny<IUserIdentity>(), It.IsAny<Ringfence>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" , ")]
    public void CreateRingfence_ReturnsBadRequestWithoutWriting_WhenDivisionsAreBlank(string divisions)
    {
        SetIdentity("UK,IE");

        var result = CreateSubject().CreateRingfence(CreateRequest(divisions: divisions));

        GetBadRequestMessage(result).Should().Be("Ringfence division cannot be empty.");
        _repository.Verify(repository => repository.CreateRingfence(
            It.IsAny<IUserIdentity>(), It.IsAny<Ringfence>()), Times.Never);
    }

    [Fact]
    public void CreateRingfence_AllowsUnrestrictedSuperAdminAssignments()
    {
        SetIdentity(null, isSuperAdmin: true);
        _repository.Setup(repository => repository.CreateRingfence(_identity.Object, It.IsAny<Ringfence>()))
            .Returns<IUserIdentity, Ringfence>((_, ringfence) => ringfence);

        var result = CreateSubject().CreateRingfence(CreateRequest(divisions: "FR"));

        GetCreatedRingfenceResponse(result, 0).Divisions.Should().Be("FR");
        _repository.Verify(repository => repository.CreateRingfence(
            _identity.Object,
            It.Is<Ringfence>(ringfence => ringfence.Divisions == "FR")), Times.Once);
    }

    [Fact]
    public void CreateRingfence_RejectsWarehouseOutsideTheSelectedDivisionsWithoutWriting()
    {
        SetIdentity("UK,IE");
        _repository.Setup(repository => repository.GetWarehouses(It.IsAny<string[]>()))
            .Returns(new List<WarehouseItem> { CreateWarehouse("IE", "ED0") });

        var result = CreateSubject().CreateRingfence(CreateRequest(divisions: "UK"));

        GetValidationProblem(result).Errors["warehouse"]
            .Should().Equal("Select a warehouse in one of the chosen ringfence divisions.");
        _repository.Verify(repository => repository.CreateRingfence(
            It.IsAny<IUserIdentity>(), It.IsAny<Ringfence>()), Times.Never);
    }

    [Fact]
    public void CreateRingfence_RejectsConfiguredWarehouseWhoseNormalisedCodeDoesNotEndInZeroWithoutWriting()
    {
        SetIdentity("UK");
        _repository.Setup(repository => repository.GetWarehouses(It.IsAny<string[]>()))
            .Returns(new List<WarehouseItem> { CreateWarehouse("UK", "ED1") });

        var result = CreateSubject().CreateRingfence(CreateRequest() with { Warehouse = " ed1 " });

        GetValidationProblem(result).Errors["warehouse"].Should().Equal("Select a warehouse code that ends in 0.");
        _repository.Verify(repository => repository.CreateRingfence(
            It.IsAny<IUserIdentity>(), It.IsAny<Ringfence>()), Times.Never);
    }

    [Fact]
    public void CreateRingfence_AllowsWarehouseAndOwnerFromAnySelectedDivision()
    {
        SetIdentity("UK,IE");
        var request = CreateRequest(divisions: "UK,IE");
        Ringfence? captured = null;
        _repository.Setup(repository => repository.GetWarehouses(It.IsAny<string[]>()))
            .Returns(new List<WarehouseItem> { CreateWarehouse("IE", "ED0") });
        _repository.Setup(repository => repository.GetUsers())
            .Returns(new[] { CreateUser("owner@example.com", "UK") }.AsQueryable());
        _repository.Setup(repository => repository.CreateRingfence(_identity.Object, It.IsAny<Ringfence>()))
            .Callback<IUserIdentity, Ringfence>((_, ringfence) => captured = ringfence)
            .Returns<IUserIdentity, Ringfence>((_, ringfence) => ringfence);

        var result = CreateSubject().CreateRingfence(request);

        GetCreatedRingfenceResponse(result, 0).Warehouse.Should().Be("ED0");
        captured.Should().NotBeNull();
        captured!.Divisions.Should().Be("UK,IE");
        _repository.Verify(repository => repository.CreateRingfence(_identity.Object, captured), Times.Once);
    }

    [Fact]
    public void CreateRingfence_RejectsOwnerOutsideTheSelectedDivisionsWithoutWriting()
    {
        SetIdentity("UK,IE");
        _repository.Setup(repository => repository.GetUsers())
            .Returns(new[] { CreateUser("other@example.com", "IE") }.AsQueryable());

        var result = CreateSubject().CreateRingfence(CreateRequest(divisions: "UK") with
        {
            Owner = "other@example.com",
        });

        GetValidationProblem(result).Errors["owner"]
            .Should().Equal("Select an owner assigned to one of the chosen ringfence divisions.");
        _repository.Verify(repository => repository.CreateRingfence(
            It.IsAny<IUserIdentity>(), It.IsAny<Ringfence>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void CreateRingfence_DefaultsAnOmittedOrBlankOwnerToTheAuthenticatedUser(string? requestedOwner)
    {
        SetIdentity("UK");
        _repository.Setup(repository => repository.GetUsers()).Returns(Enumerable.Empty<User>().AsQueryable());
        _repository.Setup(repository => repository.CreateRingfence(_identity.Object, It.IsAny<Ringfence>()))
            .Returns<IUserIdentity, Ringfence>((_, ringfence) => ringfence);

        var result = CreateSubject().CreateRingfence(CreateRequest() with
        {
            Owner = requestedOwner,
        });

        GetCreatedRingfenceResponse(result, 0).Owner.Should().Be("planner@example.com");
    }

    [Theory]
    [InlineData(false, "UK")]
    [InlineData(true, "FR")]
    public void UpdateRingfence_ReturnsNotFoundBeforeAssignmentChecksOrMutation_WhenParentIsMissingOrInaccessible(
        bool parentExists,
        string? ringfenceDivisions)
    {
        SetIdentity("UK");
        var ringfence = CreateRingfence(42, ringfenceDivisions, owner: "owner@example.com");
        if (parentExists)
        {
            _repository.Setup(repository => repository.GetRingfence(42)).Returns(ringfence);
        }

        var result = CreateSubject().UpdateRingfence(42, UpdateRequest(divisions: "FR"));

        result.Should().BeOfType<NotFoundResult>();
        AssertOriginalRingfence(ringfence, ringfenceDivisions);
        _repository.Verify(repository => repository.UpdateRingfence(
            It.IsAny<IUserIdentity>(), It.IsAny<Ringfence>()), Times.Never);
    }

    [Fact]
    public void UpdateRingfence_ReturnsForbiddenWithoutMutatingTrackedParent_WhenChangedDivisionsIncludeAnUnassignedCode()
    {
        SetIdentity("UK,IE");
        var ringfence = CreateRingfence(42, "FR,UK", owner: "owner@example.com");
        _repository.Setup(repository => repository.GetRingfence(42)).Returns(ringfence);

        var result = CreateSubject().UpdateRingfence(42, UpdateRequest(divisions: "UK,FR,DE"));

        result.Should().BeOfType<ForbidResult>();
        AssertOriginalRingfence(ringfence, "FR,UK");
        _repository.Verify(repository => repository.UpdateRingfence(
            It.IsAny<IUserIdentity>(), It.IsAny<Ringfence>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" , ")]
    public void UpdateRingfence_ReturnsBadRequestWithoutMutatingTrackedParent_WhenDivisionsAreBlank(string divisions)
    {
        SetIdentity("UK,IE");
        var ringfence = CreateRingfence(42, "FR,UK", owner: "owner@example.com");
        _repository.Setup(repository => repository.GetRingfence(42)).Returns(ringfence);

        var result = CreateSubject().UpdateRingfence(42, UpdateRequest(divisions: divisions));

        GetBadRequestMessage(result).Should().Be("Ringfence division cannot be empty.");
        AssertOriginalRingfence(ringfence, "FR,UK");
        _repository.Verify(repository => repository.UpdateRingfence(
            It.IsAny<IUserIdentity>(), It.IsAny<Ringfence>()), Times.Never);
    }

    [Fact]
    public void UpdateRingfence_AllowsCollaboratorAndUpdatesOwner_WhenAllRequestedDivisionsAreAssigned()
    {
        SetIdentity(" uk, IE ");
        var ringfence = CreateRingfence(42, "FR,UK", owner: "owner@example.com");
        var originalCreatedBy = ringfence.CreatedBy;
        var request = UpdateRequest(divisions: " ie, UK ");
        var persisted = CreateRingfence(42, request.Divisions, request.FromDate, owner: ringfence.Owner);
        persisted.Title = request.Title;
        persisted.ToDate = request.ToDate;
        persisted.Warehouse = request.Warehouse;
        persisted.Owner = request.Owner;
        persisted.LastUpdatedBy = "planner@example.com";
        persisted.LastUpdatedDate = new DateTime(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc);
        _repository.Setup(repository => repository.GetRingfence(42)).Returns(ringfence);
        _repository.Setup(repository => repository.UpdateRingfence(_identity.Object, ringfence))
            .Returns(persisted);

        var result = CreateSubject().UpdateRingfence(42, request);

        AssertRingfenceResponseMatches(GetOkRingfenceResponse(result), persisted);
        ringfence.Title.Should().Be(request.Title);
        ringfence.FromDate.Should().Be(request.FromDate);
        ringfence.ToDate.Should().Be(request.ToDate);
        ringfence.Divisions.Should().Be(request.Divisions);
        ringfence.Warehouse.Should().Be(request.Warehouse);
        ringfence.Owner.Should().Be(request.Owner);
        ringfence.CreatedBy.Should().Be(originalCreatedBy);
        _repository.Verify(repository => repository.UpdateRingfence(_identity.Object, ringfence), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void UpdateRingfence_PreservesOmittedOrBlankLegacyOwnerAndWarehouse_WhenDivisionsAreUnchanged(
        string? requestedOwner)
    {
        SetIdentity("UK");
        var ringfence = CreateRingfence(42, "UK", owner: "legacy.owner@example.com");
        ringfence.Warehouse = "LEGACY";
        _repository.Setup(repository => repository.GetRingfence(42)).Returns(ringfence);
        _repository.Setup(repository => repository.UpdateRingfence(_identity.Object, ringfence))
            .Returns(ringfence);

        var result = CreateSubject().UpdateRingfence(42, UpdateRequest() with
        {
            Owner = requestedOwner,
            Warehouse = null,
        });

        var response = GetOkRingfenceResponse(result);
        response.Owner.Should().Be("legacy.owner@example.com");
        response.Warehouse.Should().Be("LEGACY");
        _repository.Verify(repository => repository.GetUsers(), Times.Never);
        _repository.Verify(repository => repository.GetWarehouses(It.IsAny<string[]>()), Times.Never);
        _repository.Verify(repository => repository.UpdateRingfence(_identity.Object, ringfence), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void UpdateRingfence_PreservesAnOmittedOrBlankLegacyWarehouse_WhenItIsBlank(string? requestedWarehouse)
    {
        SetIdentity("UK");
        var ringfence = CreateRingfence(42, "UK", owner: "owner@example.com");
        ringfence.Warehouse = null;
        _repository.Setup(repository => repository.GetRingfence(42)).Returns(ringfence);
        _repository.Setup(repository => repository.UpdateRingfence(_identity.Object, ringfence))
            .Returns(ringfence);

        var result = CreateSubject().UpdateRingfence(42, UpdateRequest() with
        {
            Warehouse = requestedWarehouse,
        });

        GetOkRingfenceResponse(result).Warehouse.Should().BeNull();
        _repository.Verify(repository => repository.GetUsers(), Times.Never);
        _repository.Verify(repository => repository.GetWarehouses(It.IsAny<string[]>()), Times.Never);
        _repository.Verify(repository => repository.UpdateRingfence(_identity.Object, ringfence), Times.Once);
    }

    [Fact]
    public void UpdateRingfence_DefaultsAnOmittedOwnerToTheAuthenticatedUser_WhenTheLegacyOwnerIsBlank()
    {
        SetIdentity("UK");
        var ringfence = CreateRingfence(42, "UK", owner: " ");
        ringfence.Warehouse = "LEGACY";
        _repository.Setup(repository => repository.GetRingfence(42)).Returns(ringfence);
        _repository.Setup(repository => repository.UpdateRingfence(_identity.Object, ringfence))
            .Returns(ringfence);

        var result = CreateSubject().UpdateRingfence(42, UpdateRequest() with
        {
            Owner = null,
            Warehouse = "LEGACY",
        });

        GetOkRingfenceResponse(result).Owner.Should().Be("planner@example.com");
        _repository.Verify(repository => repository.GetUsers(), Times.Never);
        _repository.Verify(repository => repository.GetWarehouses(It.IsAny<string[]>()), Times.Never);
    }

    [Fact]
    public void UpdateRingfence_RevalidatesLegacyReferences_WhenTheDivisionSelectionChanges()
    {
        SetIdentity("UK,IE");
        var ringfence = CreateRingfence(42, "UK", owner: "legacy.owner@example.com");
        ringfence.Warehouse = "LEGACY";
        _repository.Setup(repository => repository.GetRingfence(42)).Returns(ringfence);
        _repository.Setup(repository => repository.GetWarehouses(It.IsAny<string[]>()))
            .Returns(Enumerable.Empty<WarehouseItem>().ToList());
        _repository.Setup(repository => repository.GetUsers()).Returns(Enumerable.Empty<User>().AsQueryable());

        var result = CreateSubject().UpdateRingfence(42, UpdateRequest(divisions: "IE") with
        {
            Owner = null,
            Warehouse = "LEGACY",
        });

        var problem = GetValidationProblem(result);
        problem.Errors.Keys.Should().Contain(["owner", "warehouse"]);
        _repository.Verify(repository => repository.UpdateRingfence(
            It.IsAny<IUserIdentity>(), It.IsAny<Ringfence>()), Times.Never);
    }

    [Fact]
    public void UpdateRingfence_AllowsPartialCollaboratorToEditMetadata_WhenTheDivisionSelectionIsUnchanged()
    {
        SetIdentity("UK");
        var ringfence = CreateRingfence(42, "FR,UK", owner: "legacy.owner@example.com");
        ringfence.Warehouse = "LEGACY";
        _repository.Setup(repository => repository.GetRingfence(42)).Returns(ringfence);
        _repository.Setup(repository => repository.UpdateRingfence(_identity.Object, ringfence))
            .Returns(ringfence);

        var result = CreateSubject().UpdateRingfence(42, UpdateRequest(divisions: " uk, FR, uk ") with
        {
            Title = "Corrected shared reserve",
            Owner = ringfence.Owner,
            Warehouse = ringfence.Warehouse,
        });

        GetOkRingfenceResponse(result).Title.Should().Be("Corrected shared reserve");
        ringfence.Divisions.Should().Be(" uk, FR, uk ");
        ringfence.Owner.Should().Be("legacy.owner@example.com");
        ringfence.Warehouse.Should().Be("LEGACY");
        _repository.Verify(repository => repository.GetUsers(), Times.Never);
        _repository.Verify(repository => repository.GetWarehouses(It.IsAny<string[]>()), Times.Never);
        _repository.Verify(repository => repository.UpdateRingfence(_identity.Object, ringfence), Times.Once);
    }

    [Fact]
    public void UpdateRingfence_RejectsWarehouseOutsideTheSelectedDivisionsWithoutMutating()
    {
        SetIdentity("UK,IE");
        var ringfence = CreateRingfence(42, "UK", owner: "owner@example.com");
        _repository.Setup(repository => repository.GetRingfence(42)).Returns(ringfence);
        _repository.Setup(repository => repository.GetWarehouses(It.IsAny<string[]>()))
            .Returns(new List<WarehouseItem> { CreateWarehouse("IE", "ED3") });

        var result = CreateSubject().UpdateRingfence(42, UpdateRequest(divisions: "UK"));

        GetValidationProblem(result).Errors["warehouse"]
            .Should().Equal("Select a warehouse in one of the chosen ringfence divisions.");
        AssertOriginalRingfence(ringfence, "UK");
        _repository.Verify(repository => repository.UpdateRingfence(
            It.IsAny<IUserIdentity>(), It.IsAny<Ringfence>()), Times.Never);
    }

    [Fact]
    public void UpdateRingfence_RejectsReplacementWarehouseWhoseCodeDoesNotEndInZeroWithoutMutating()
    {
        SetIdentity("UK");
        var ringfence = CreateRingfence(42, "UK", owner: "owner@example.com");
        _repository.Setup(repository => repository.GetRingfence(42)).Returns(ringfence);
        _repository.Setup(repository => repository.GetWarehouses(It.IsAny<string[]>()))
            .Returns(new List<WarehouseItem> { CreateWarehouse("UK", "ED2") });

        var result = CreateSubject().UpdateRingfence(42, UpdateRequest() with { Warehouse = "ED2" });

        GetValidationProblem(result).Errors["warehouse"].Should().Equal("Select a warehouse code that ends in 0.");
        AssertOriginalRingfence(ringfence, "UK");
        _repository.Verify(repository => repository.UpdateRingfence(
            It.IsAny<IUserIdentity>(), It.IsAny<Ringfence>()), Times.Never);
    }

    [Theory]
    [InlineData(false, "UK")]
    [InlineData(true, "FR")]
    [InlineData(true, " , ")]
    public void DeleteRingfence_ReturnsNotFoundWithoutDeleting_WhenParentIsMissingOrInaccessible(
        bool parentExists,
        string? ringfenceDivisions)
    {
        SetIdentity("UK");
        if (parentExists)
        {
            _repository.Setup(repository => repository.GetRingfence(42))
                .Returns(CreateRingfence(42, ringfenceDivisions));
        }

        var result = CreateSubject().DeleteRingfence(42);

        result.Should().BeOfType<NotFoundResult>();
        _repository.Verify(repository => repository.DeleteRingfence(It.IsAny<Ringfence>()), Times.Never);
    }

    [Fact]
    public void DeleteRingfence_AllowsDivisionCollaboratorRegardlessOfOwner()
    {
        SetIdentity(" uk, IE ");
        var ringfence = CreateRingfence(42, "FR,UK", owner: "owner@example.com");
        _repository.Setup(repository => repository.GetRingfence(42)).Returns(ringfence);

        var result = CreateSubject().DeleteRingfence(42);

        result.Should().BeOfType<NoContentResult>();
        _repository.Verify(repository => repository.DeleteRingfence(ringfence), Times.Once);
    }

    [Theory]
    [InlineData(false, "UK")]
    [InlineData(true, "FR")]
    [InlineData(true, " , ")]
    public void AddItem_ReturnsNotFoundBeforeReadingAsset_WhenParentIsMissingOrInaccessible(
        bool parentExists,
        string? ringfenceDivisions)
    {
        SetIdentity("UK");
        if (parentExists)
        {
            _repository.Setup(repository => repository.GetRingfence(42))
                .Returns(CreateRingfence(42, ringfenceDivisions));
        }

        var result = CreateSubject().AddItemToRingfence(42, new AddRingfenceItemRequest { AssetId = "ASSET-7" });

        result.Should().BeOfType<NotFoundResult>();
        _repository.Verify(repository => repository.GetAsset(It.IsAny<string>()), Times.Never);
        VerifyNoItemCreated();
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(true, "FR")]
    [InlineData(true, null)]
    public void AddItem_ReturnsNotFoundWithoutCreating_WhenAssetIsMissingOrInaccessible(
        bool assetExists,
        string? assetDivision)
    {
        SetIdentity("UK");
        _repository.Setup(repository => repository.GetRingfence(42))
            .Returns(CreateRingfence(42, "UK"));
        _repository.Setup(repository => repository.GetRingfenceItems(42))
            .Returns(Enumerable.Empty<RingfenceItem>().AsQueryable());
        if (assetExists)
        {
            _repository.Setup(repository => repository.GetAsset("ASSET-7"))
                .Returns(CreateAsset("ASSET-7", assetDivision));
        }

        var result = CreateSubject().AddItemToRingfence(42, new AddRingfenceItemRequest { AssetId = "ASSET-7" });

        result.Should().BeOfType<NotFoundResult>();
        _repository.Verify(repository => repository.GetAsset("ASSET-7"), Times.Once);
        VerifyNoItemCreated();
    }

    [Fact]
    public void AddItem_AllowsAccessibleAssetFromDifferentRingfenceDivision_WithExactMapping()
    {
        SetIdentity(" UK, ie ");
        var ringfence = CreateRingfence(42, "UK", owner: "owner@example.com");
        _repository.Setup(repository => repository.GetRingfence(42)).Returns(ringfence);
        _repository.Setup(repository => repository.GetAsset("ASSET-7"))
            .Returns(CreateAsset("ASSET-7", "IE"));
        _repository.Setup(repository => repository.GetRingfenceItems(42))
            .Returns(Enumerable.Empty<RingfenceItem>().AsQueryable());
        var persisted = CreateRingfenceItem(17, 42, "ASSET-7", "planner@example.com");
        persisted.LastUpdatedBy = "planner@example.com";
        persisted.LastUpdatedDate = new DateTime(2026, 8, 14, 12, 0, 0, DateTimeKind.Utc);
        _repository.Setup(repository => repository.AddAssetToRingfence(_identity.Object, ringfence, "ASSET-7"))
            .Returns(persisted);

        var result = CreateSubject().AddItemToRingfence(42, new AddRingfenceItemRequest { AssetId = "ASSET-7" });

        AssertRingfenceItemResponseMatches(GetOkRingfenceItemResponse(result), persisted);
        _repository.Verify(repository => repository.AddAssetToRingfence(
            _identity.Object, ringfence, "ASSET-7"), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AddItem_ForSuperAdmin_RequiresAssetExistenceButAllowsNullAssetDivision(bool assetExists)
    {
        SetIdentity(null, isSuperAdmin: true);
        _repository.Setup(repository => repository.GetRingfence(42))
            .Returns(CreateRingfence(42, null));
        _repository.Setup(repository => repository.GetRingfenceItems(42))
            .Returns(Enumerable.Empty<RingfenceItem>().AsQueryable());
        if (assetExists)
        {
            _repository.Setup(repository => repository.GetAsset("ASSET-7"))
                .Returns(CreateAsset("ASSET-7", null));
            _repository.Setup(repository => repository.AddAssetToRingfence(
                    _identity.Object,
                    It.IsAny<Ringfence>(),
                    "ASSET-7"))
                .Returns<IUserIdentity, Ringfence, string>((_, ringfence, assetId) => new RingfenceItem("planner@example.com")
                {
                    RingfenceId = ringfence.Id,
                    AssetId = assetId,
                });
        }

        var result = CreateSubject().AddItemToRingfence(42, new AddRingfenceItemRequest { AssetId = "ASSET-7" });

        _repository.Verify(repository => repository.GetAsset("ASSET-7"), Times.Once);
        if (assetExists)
        {
            GetOkRingfenceItemResponse(result).AssetId.Should().Be("ASSET-7");
            _repository.Verify(repository => repository.AddAssetToRingfence(
                _identity.Object, It.IsAny<Ringfence>(), "ASSET-7"), Times.Once);
        }
        else
        {
            result.Should().BeOfType<NotFoundResult>();
            VerifyNoItemCreated();
        }
    }

    [Fact]
    public void AddItem_ReturnsTheExistingAssignmentWithoutWriting_WhenAssetIsAlreadyOnTheRingfence()
    {
        SetIdentity("UK");
        var ringfence = CreateRingfence(42, "UK");
        var existing = CreateRingfenceItem(17, 42, "ASSET-7");
        _repository.Setup(repository => repository.GetRingfence(42)).Returns(ringfence);
        _repository.Setup(repository => repository.GetAsset("asset-7"))
            .Returns(CreateAsset("ASSET-7", "UK"));
        _repository.Setup(repository => repository.GetRingfenceItems(42))
            .Returns(new[] { existing }.AsQueryable());

        var result = CreateSubject().AddItemToRingfence(42, new AddRingfenceItemRequest { AssetId = " asset-7 " });

        AssertRingfenceItemResponseMatches(GetOkRingfenceItemResponse(result), existing);
        _repository.Verify(repository => repository.GetAsset(It.IsAny<string>()), Times.Never);
        _repository.Verify(repository => repository.AddAssetToRingfence(
            It.IsAny<IUserIdentity>(), It.IsAny<Ringfence>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task PreflightItems_ReturnsReadyExistingUnavailableAndOverlappingAssets_WithoutWriting()
    {
        SetIdentity("UK");
        var ringfence = CreateRingfence(42, "UK", new DateTime(2026, 9, 1));
        ringfence.ToDate = new DateTime(2026, 9, 30);
        _repository.Setup(repository => repository.GetRingfence(42)).Returns(ringfence);
        _repository.Setup(repository => repository.GetAssets()).Returns(new[]
        {
            CreateAsset("ASSET-1", "UK"),
            CreateAsset("ASSET-2", "UK"),
            CreateAsset("ASSET-3", "FR"),
        }.AsQueryable());
        _repository.Setup(repository => repository.GetRingfenceItems(42))
            .Returns(new[] { CreateRingfenceItem(1, 42, "ASSET-1") }.AsQueryable());
        _repository.Setup(repository => repository.GetOverlappingRingfenceDetailsAsync(
                42,
                It.Is<IReadOnlyList<string>>(ids => ids.SequenceEqual(new[] { "ASSET-2" })),
                ringfence.FromDate,
                ringfence.ToDate,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RingfenceAssestDetails>
            {
                new()
                {
                    RingfenceId = 99,
                    AssetIds = ["ASSET-2"],
                    Title = "Overlapping reserve",
                    FromDate = new DateTime(2026, 9, 10),
                    ToDate = new DateTime(2026, 9, 20),
                    Owner = "other@example.com",
                },
            });

        var result = await CreateSubject().PreflightItemsForRingfence(
            42,
            new RingfenceItemBatchRequest { AssetIds = [" asset-1 ", "ASSET-2", "asset-3", "MISSING", "ASSET-2"] },
            CancellationToken.None);

        var response = GetBatchResponse(result);
        response.ReadyAssetIds.Should().Equal("ASSET-2");
        response.AlreadyAssignedAssetIds.Should().Equal("ASSET-1");
        response.UnavailableAssetIds.Should().Equal("asset-3", "MISSING");
        response.AddedAssetIds.Should().BeEmpty();
        response.RequiresOverlapAcknowledgement.Should().BeTrue();
        response.Overlaps.Should().ContainSingle().Which.RingfenceId.Should().Be(99);
        _repository.Verify(repository => repository.AddAssetToRingfence(
            It.IsAny<IUserIdentity>(), It.IsAny<Ringfence>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task AddItems_ReturnsConflictAndDoesNotWrite_WhenAnOverlapHasNotBeenAcknowledged()
    {
        SetIdentity("UK");
        var ringfence = CreateRingfence(42, "UK", new DateTime(2026, 9, 1));
        ringfence.ToDate = new DateTime(2026, 9, 30);
        SetupBatchCandidate(ringfence, "ASSET-7");
        _repository.Setup(repository => repository.GetOverlappingRingfenceDetailsAsync(
                42,
                It.IsAny<IReadOnlyList<string>>(),
                ringfence.FromDate,
                ringfence.ToDate,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RingfenceAssestDetails>
            {
                new()
                {
                    RingfenceId = 99,
                    AssetIds = ["ASSET-7"],
                    Title = "Overlapping reserve",
                    FromDate = ringfence.FromDate,
                    ToDate = ringfence.ToDate,
                },
            });

        var result = await CreateSubject().AddItemsToRingfence(
            42,
            new RingfenceItemBatchRequest { AssetIds = ["ASSET-7"] },
            CancellationToken.None);

        var conflict = result.Should().BeOfType<ConflictObjectResult>().Subject;
        var response = conflict.Value.Should().BeOfType<RingfenceItemBatchResponse>().Subject;
        response.RequiresOverlapAcknowledgement.Should().BeTrue();
        _repository.Verify(repository => repository.AddAssetToRingfence(
            It.IsAny<IUserIdentity>(), It.IsAny<Ringfence>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task AddItems_RechecksAndAddsReadyAssets_WhenOverlapsHaveBeenAcknowledged()
    {
        SetIdentity("UK");
        var ringfence = CreateRingfence(42, "UK", new DateTime(2026, 9, 1));
        ringfence.ToDate = new DateTime(2026, 9, 30);
        SetupBatchCandidate(ringfence, "ASSET-7");
        _repository.Setup(repository => repository.GetOverlappingRingfenceDetailsAsync(
                42,
                It.IsAny<IReadOnlyList<string>>(),
                ringfence.FromDate,
                ringfence.ToDate,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RingfenceAssestDetails>
            {
                new()
                {
                    RingfenceId = 99,
                    AssetIds = ["ASSET-7"],
                    Title = "Overlapping reserve",
                    FromDate = ringfence.FromDate,
                    ToDate = ringfence.ToDate,
                },
            });
        _repository.Setup(repository => repository.AddAssetToRingfence(_identity.Object, ringfence, "ASSET-7"))
            .Returns(CreateRingfenceItem(17, 42, "ASSET-7"));

        var result = await CreateSubject().AddItemsToRingfence(
            42,
            new RingfenceItemBatchRequest
            {
                AssetIds = ["ASSET-7"],
                AcknowledgeOverlaps = true,
            },
            CancellationToken.None);

        var response = GetBatchResponse(result);
        response.AddedAssetIds.Should().Equal("ASSET-7");
        response.ReadyAssetIds.Should().Equal("ASSET-7");
        _repository.Verify(repository => repository.AddAssetToRingfence(
            _identity.Object, ringfence, "ASSET-7"), Times.Once);
    }

    [Theory]
    [InlineData(false, "UK")]
    [InlineData(true, "FR")]
    [InlineData(true, " , ")]
    public void RemoveItem_ReturnsNotFoundBeforeReadingItems_WhenParentIsMissingOrInaccessible(
        bool parentExists,
        string? ringfenceDivisions)
    {
        SetIdentity("UK");
        if (parentExists)
        {
            _repository.Setup(repository => repository.GetRingfence(42))
                .Returns(CreateRingfence(42, ringfenceDivisions));
        }

        var result = CreateSubject().RemoveItemFromRingfence(42, "ASSET-7");

        result.Should().BeOfType<NotFoundResult>();
        _repository.Verify(repository => repository.GetRingfenceItems(It.IsAny<int>()), Times.Never);
        _repository.Verify(repository => repository.DeleteRingfenceItem(It.IsAny<RingfenceItem>()), Times.Never);
    }

    [Fact]
    public void RemoveItem_AllowsCollaboratorToRemoveHistoricalItem_WithoutReadingAsset()
    {
        SetIdentity(" uk, IE ");
        var ringfence = CreateRingfence(42, "FR,UK", owner: "owner@example.com");
        var item = CreateRingfenceItem(7, 42, "ASSET-7", "historical@example.com");
        _repository.Setup(repository => repository.GetRingfence(42)).Returns(ringfence);
        _repository.Setup(repository => repository.GetRingfenceItems(42))
            .Returns(new[] { item }.AsQueryable());

        var result = CreateSubject().RemoveItemFromRingfence(42, "ASSET-7");

        result.Should().BeOfType<NoContentResult>();
        _repository.Verify(repository => repository.GetAsset(It.IsAny<string>()), Times.Never);
        _repository.Verify(repository => repository.DeleteRingfenceItem(item), Times.Once);
    }

    [Fact]
    public void RemoveItem_ReturnsNotFoundWithoutDeleting_WhenItemIsMissingOrDifferentlyCased()
    {
        SetIdentity("UK");
        _repository.Setup(repository => repository.GetRingfence(42))
            .Returns(CreateRingfence(42, "UK"));
        _repository.Setup(repository => repository.GetRingfenceItems(42))
            .Returns(new[] { CreateRingfenceItem(7, 42, "ASSET-7") }.AsQueryable());

        var result = CreateSubject().RemoveItemFromRingfence(42, "asset-7");

        result.Should().BeOfType<NotFoundResult>();
        _repository.Verify(repository => repository.DeleteRingfenceItem(It.IsAny<RingfenceItem>()), Times.Never);
    }

    [Theory]
    [InlineData("title")]
    [InlineData("warehouse")]
    [InlineData("dates")]
    [InlineData("past")]
    public void CreateRingfence_RejectsInvalidBusinessFieldsWithoutWriting(string invalidField)
    {
        SetIdentity("UK");
        var request = CreateRequest();
        request = invalidField switch
        {
            "title" => request with { Title = "  " },
            "warehouse" => request with { Warehouse = "  " },
            "dates" => request with
            {
                FromDate = new DateTime(2026, 10, 2),
                ToDate = new DateTime(2026, 10, 1),
            },
            "past" => request with
            {
                FromDate = new DateTime(2026, 8, 23),
                ToDate = new DateTime(2026, 8, 24),
            },
            _ => throw new ArgumentOutOfRangeException(nameof(invalidField), invalidField, null),
        };

        var result = CreateSubject().CreateRingfence(request);

        var problem = GetValidationProblem(result);
        problem.Errors.Keys.Should().Contain(invalidField switch
        {
            "title" => "title",
            "warehouse" => "warehouse",
            "dates" => "fromDate",
            "past" => "fromDate",
            _ => throw new ArgumentOutOfRangeException(nameof(invalidField), invalidField, null),
        });
        _repository.Verify(repository => repository.CreateRingfence(
            It.IsAny<IUserIdentity>(), It.IsAny<Ringfence>()), Times.Never);
    }

    [Fact]
    public void CreateRingfence_ReturnsConflictForAnExistingNameIgnoringCaseAndWhitespace()
    {
        SetIdentity("UK");
        var existing = CreateRingfence(1, "UK");
        existing.Title = "Existing reserve";
        SetRingfences(existing);

        var result = CreateSubject().CreateRingfence(CreateRequest() with { Title = " existing reserve " });

        var problem = GetValidationProblem(result, StatusCodes.Status409Conflict);
        problem.Title.Should().Be("Ringfence name already exists.");
        problem.Errors["title"].Should().Equal("A ringfence with this name already exists.");
        _repository.Verify(repository => repository.CreateRingfence(
            It.IsAny<IUserIdentity>(), It.IsAny<Ringfence>()), Times.Never);
    }

    [Fact]
    public void UpdateRingfence_AllowsHistoricalMetadataEditsButPreventsRewritingHistoricalDates()
    {
        SetIdentity("UK");
        var historical = CreateRingfence(42, "UK", new DateTime(2026, 7, 1));
        historical.ToDate = new DateTime(2026, 7, 31);
        _repository.Setup(repository => repository.GetRingfence(42)).Returns(historical);
        _repository.Setup(repository => repository.UpdateRingfence(_identity.Object, historical))
            .Returns(historical);

        var metadataResult = CreateSubject().UpdateRingfence(
            42,
            UpdateRequest() with
            {
                FromDate = historical.FromDate,
                ToDate = historical.ToDate,
                Title = "Corrected historical reserve",
            });

        GetOkRingfenceResponse(metadataResult).Title.Should().Be("Corrected historical reserve");

        var invalidResult = CreateSubject().UpdateRingfence(
            42,
            UpdateRequest() with
            {
                FromDate = new DateTime(2026, 7, 2),
                ToDate = historical.ToDate,
            });

        var invalidProblem = GetValidationProblem(invalidResult);
        invalidProblem.Errors["fromDate"]
            .Should().Equal("Ringfence start date cannot be changed to a date in the past.");
        _repository.Verify(repository => repository.UpdateRingfence(_identity.Object, historical), Times.Once);
    }

    [Fact]
    public void UpdateRingfence_ReturnsConflictForAnotherRingfenceName()
    {
        SetIdentity("UK");
        var ringfence = CreateRingfence(42, "UK");
        var existing = CreateRingfence(99, "UK");
        existing.Title = "Existing reserve";
        SetRingfences(ringfence, existing);
        _repository.Setup(repository => repository.GetRingfence(42)).Returns(ringfence);

        var result = CreateSubject().UpdateRingfence(
            42,
            UpdateRequest() with { Title = "existing reserve" });

        GetValidationProblem(result, StatusCodes.Status409Conflict)
            .Errors["title"]
            .Should().Equal("A ringfence with this name already exists.");
        AssertOriginalRingfence(ringfence, "UK");
        _repository.Verify(repository => repository.UpdateRingfence(
            It.IsAny<IUserIdentity>(), It.IsAny<Ringfence>()), Times.Never);
    }

    private RingfenceController CreateSubject() => new(_repository.Object, _identity.Object, _timeProvider);

    private IActionResult Invoke(string action) => action switch
    {
        "list" => CreateSubject().GetRingfences(),
        "detail" => CreateSubject().GetRingfence(42),
        "create" => CreateSubject().CreateRingfence(CreateRequest()),
        "update" => CreateSubject().UpdateRingfence(42, UpdateRequest()),
        "delete" => CreateSubject().DeleteRingfence(42),
        "add-item" => CreateSubject().AddItemToRingfence(42, new AddRingfenceItemRequest { AssetId = "ASSET-7" }),
        "remove-item" => CreateSubject().RemoveItemFromRingfence(42, "ASSET-7"),
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown Ringfence action."),
    };

    private void SetMissingIdentity()
    {
        _identity.Setup(identity => identity.GetIdentity()).Returns((User?)null!);
    }

    private void SetIdentity(string? divisions, bool isSuperAdmin = false)
    {
        _identity.Setup(identity => identity.GetIdentity()).Returns(new User
        {
            LoginName = "planner@example.com",
            FullName = "Fleet Planner",
            Division = divisions!,
            IsSuperAdmin = isSuperAdmin,
            DateFormat = "dd/MM/yyyy",
        });
        SetupValidReferenceData();
    }

    private void SetupValidReferenceData()
    {
        _repository.Setup(repository => repository.GetWarehouses(It.IsAny<string[]>()))
            .Returns((string[] selectedDivisions) =>
                (IList<WarehouseItem>)selectedDivisions
                    .SelectMany(division => new[]
                    {
                        CreateWarehouse(division, "ED1"),
                        CreateWarehouse(division, "ED2"),
                        CreateWarehouse(division, "ED3"),
                        CreateWarehouse(division, "ED0"),
                    })
                    .ToList());
        _repository.Setup(repository => repository.GetUsers()).Returns(() =>
            new[]
            {
                CreateUser("planner@example.com", "UK,IE,FR"),
                CreateUser("owner@example.com", "UK,IE,FR"),
            }.AsQueryable());
    }

    private void SetRingfences(params Ringfence[] ringfences)
    {
        _repository.Setup(repository => repository.GetRingfences()).Returns(ringfences.AsQueryable());
    }

    private void VerifyNoItemCreated()
    {
        _repository.Verify(repository => repository.AddAssetToRingfence(
            It.IsAny<IUserIdentity>(), It.IsAny<Ringfence>(), It.IsAny<string>()), Times.Never);
    }

    private static JsonElement GetJson(IActionResult result)
    {
        var value = result.Should().BeOfType<OkObjectResult>().Subject.Value;
        value.Should().NotBeNull();
        return JsonSerializer.SerializeToElement(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private static List<RingfenceListItemResponse> GetListResponse(IActionResult result)
    {
        var value = result.Should().BeOfType<OkObjectResult>().Subject.Value;
        return value.Should().BeOfType<List<RingfenceListItemResponse>>().Subject;
    }

    private static RingfenceDetailResponse GetDetailResponse(IActionResult result)
    {
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        return ok.Value.Should().BeOfType<RingfenceDetailResponse>().Subject;
    }

    private static RingfenceResponse GetCreatedRingfenceResponse(IActionResult result, int expectedId)
    {
        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.StatusCode.Should().Be(201);
        created.ActionName.Should().Be(nameof(RingfenceController.GetRingfence));
        created.RouteValues.Should().NotBeNull();
        created.RouteValues!["id"].Should().Be(expectedId);
        return created.Value.Should().BeOfType<RingfenceResponse>().Subject;
    }

    private static RingfenceResponse GetOkRingfenceResponse(IActionResult result)
    {
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        return ok.Value.Should().BeOfType<RingfenceResponse>().Subject;
    }

    private static RingfenceItemResponse GetOkRingfenceItemResponse(IActionResult result)
    {
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        return ok.Value.Should().BeOfType<RingfenceItemResponse>().Subject;
    }

    private static RingfenceItemBatchResponse GetBatchResponse(IActionResult result)
    {
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        return ok.Value.Should().BeOfType<RingfenceItemBatchResponse>().Subject;
    }

    private static string GetBadRequestMessage(IActionResult result)
    {
        return GetValidationProblem(result).Detail!;
    }

    private static ValidationProblemDetails GetValidationProblem(
        IActionResult result,
        int expectedStatusCode = StatusCodes.Status400BadRequest)
    {
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(expectedStatusCode);
        var problem = objectResult.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        problem.Status.Should().Be(expectedStatusCode);
        problem.Extensions["message"].Should().Be(problem.Detail);
        return problem;
    }

    private static void AssertRingfenceResponseMatches(RingfenceResponse actual, Ringfence expected)
    {
        actual.Id.Should().Be(expected.Id);
        actual.FromDate.Should().Be(expected.FromDate);
        actual.ToDate.Should().Be(expected.ToDate);
        actual.Title.Should().Be(expected.Title);
        actual.LastUpdatedBy.Should().Be(expected.LastUpdatedBy);
        actual.LastUpdatedDate.Should().Be(expected.LastUpdatedDate);
        actual.Divisions.Should().Be(expected.Divisions);
        actual.Owner.Should().Be(expected.Owner);
        actual.Warehouse.Should().Be(expected.Warehouse);
        actual.CreatedAt.Should().Be(expected.CreatedAt);
        actual.CreatedBy.Should().Be(expected.CreatedBy);
    }

    private static void AssertRingfenceItemResponseMatches(RingfenceItemResponse actual, RingfenceItem expected)
    {
        actual.Id.Should().Be(expected.Id);
        actual.RingfenceId.Should().Be(expected.RingfenceId);
        actual.AssetId.Should().Be(expected.AssetId);
        actual.LastUpdatedBy.Should().Be(expected.LastUpdatedBy);
        actual.LastUpdatedDate.Should().Be(expected.LastUpdatedDate);
        actual.CreatedAt.Should().Be(expected.CreatedAt);
        actual.CreatedBy.Should().Be(expected.CreatedBy);
    }

    private static string SerializeWeb(RingfenceDetailResponse response) => JsonSerializer.Serialize(
        response,
        new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static CreateRingfenceRequest CreateRequest(string divisions = "UK") => new()
    {
        Title = "New ringfence",
        FromDate = new DateTime(2026, 9, 1),
        ToDate = new DateTime(2026, 9, 30),
        Divisions = divisions,
        Warehouse = "ED0",
        Owner = "owner@example.com",
    };

    private static UpdateRingfenceRequest UpdateRequest(string divisions = "UK") => new()
    {
        Title = "Updated ringfence",
        FromDate = new DateTime(2026, 10, 1),
        ToDate = new DateTime(2026, 10, 31),
        Divisions = divisions,
        Warehouse = "ED0",
        Owner = "owner@example.com",
    };

    private static Ringfence CreateRingfence(
        int id,
        string? divisions,
        DateTime? fromDate = null,
        string? owner = "owner@example.com") => new("creator@example.com")
    {
        Id = id,
        Title = $"Ringfence {id}",
        FromDate = fromDate ?? new DateTime(2026, 1, 1),
        ToDate = new DateTime(2026, 12, 31),
        Divisions = divisions!,
        Warehouse = "ED1",
        Owner = owner,
        CreatedAt = new DateTimeOffset(2025, 12, 1, 12, 0, 0, TimeSpan.Zero),
    };

    private static Ringfence CreateFullyPopulatedRingfence() => new("creator@example.com")
    {
        Id = 42,
        FromDate = new DateTime(2026, 1, 1, 8, 30, 0, DateTimeKind.Utc),
        ToDate = new DateTime(2026, 12, 31, 17, 45, 0, DateTimeKind.Utc),
        Title = "Annual fleet ringfence",
        LastUpdatedBy = "editor@example.com",
        LastUpdatedDate = new DateTime(2026, 8, 14, 12, 34, 56, DateTimeKind.Utc),
        Divisions = "UK,IE",
        Owner = "owner@example.com",
        Warehouse = "ED1",
        CreatedAt = new DateTimeOffset(2025, 12, 1, 12, 0, 0, TimeSpan.FromHours(1)),
    };

    private static Ringfence CreateNullAndDefaultRingfence() => new(null!)
    {
        Title = null!,
        Divisions = null!,
    };

    private static RingfenceItem CreateRingfenceItem(
        int id,
        int ringfenceId,
        string assetId,
        string createdBy = "creator@example.com") => new(createdBy)
    {
        Id = id,
        RingfenceId = ringfenceId,
        AssetId = assetId,
        CreatedAt = new DateTimeOffset(2025, 12, 2, 12, 0, 0, TimeSpan.Zero),
    };

    private static RingfenceItem CreateFullyPopulatedRingfenceItem() => new("item.creator@example.com")
    {
        Id = 7,
        RingfenceId = 42,
        AssetId = "ASSET-7",
        LastUpdatedBy = "item.editor@example.com",
        LastUpdatedDate = new DateTime(2026, 8, 15, 13, 35, 57, DateTimeKind.Utc),
        CreatedAt = new DateTimeOffset(2025, 12, 2, 12, 0, 0, TimeSpan.Zero),
    };

    private static RingfenceItem CreateNullAndDefaultRingfenceItem() => new(null!)
    {
        AssetId = null!,
    };

    private static Asset CreateAsset(string id, string? division) => new()
    {
        Id = id,
        IndividualItemNumber = id,
        Division = division,
        Status = "AVAILABLE",
        Warehouse = "ED1",
    };

    private static WarehouseItem CreateWarehouse(string divisionCode, string warehouseCode) => new()
    {
        WarehouseCode = warehouseCode,
        Warehouse = warehouseCode,
        DivisionCode = divisionCode,
        FacilityCode = $"{divisionCode}-FAC",
        Facility = $"{divisionCode} facility",
        CountryCode = divisionCode,
        Country = divisionCode,
    };

    private static User CreateUser(string loginName, string divisions) => new()
    {
        LoginName = loginName,
        FullName = loginName,
        Division = divisions,
        DateFormat = "dd/MM/yyyy",
    };

    private void SetupBatchCandidate(Ringfence ringfence, string assetId)
    {
        _repository.Setup(repository => repository.GetRingfence(42)).Returns(ringfence);
        _repository.Setup(repository => repository.GetAssets())
            .Returns(new[] { CreateAsset(assetId, "UK") }.AsQueryable());
        _repository.Setup(repository => repository.GetRingfenceItems(42))
            .Returns(Enumerable.Empty<RingfenceItem>().AsQueryable());
    }

    private static void AssertOriginalRingfence(Ringfence ringfence, string? divisions)
    {
        ringfence.Title.Should().Be("Ringfence 42");
        ringfence.FromDate.Should().Be(new DateTime(2026, 1, 1));
        ringfence.ToDate.Should().Be(new DateTime(2026, 12, 31));
        ringfence.Divisions.Should().Be(divisions);
        ringfence.Warehouse.Should().Be("ED1");
        ringfence.Owner.Should().Be("owner@example.com");
        ringfence.CreatedBy.Should().Be("creator@example.com");
    }
}
