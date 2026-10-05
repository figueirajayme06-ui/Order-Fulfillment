using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using OF.Data.Database;
using OF.UI.Database;
using OF.UI.Identity;
using OF.UI.Models;
using OF.WebApp.Controllers;

namespace OF.Tests.WebApp.Controllers;

public class EventControllerTests
{
    private readonly Mock<IDataRepository> _repository = new();
    private readonly Mock<IUserIdentity> _identity = new();
    private readonly Mock<ILogger<EventController>> _logger = new();

    [Fact]
    public void Events_ReturnsUnauthorizedWithoutRepositoryReads_WhenIdentityIsMissing()
    {
        _identity.Setup(identity => identity.GetIdentity()).Returns((User?)null!);

        var result = CreateSubject().Events(CreateRequest());

        result.Should().BeOfType<UnauthorizedResult>();
        _repository.Verify(repository => repository.GetAssets(), Times.Never);
        _repository.Verify(repository => repository.GetEventsForDivisionsAndRange(
            It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string[]>()), Times.Never);
    }

    [Fact]
    public void Events_UsesAllAssignedDivisions_WhenNormalUserOmitsSelection()
    {
        SetIdentity(" uk,IE,UK ");
        SetAssets(CreateAsset("A-UK", "uk"), CreateAsset("A-IE", "IE"));
        SetEvents([CreateEvent("A-UK")], "UK", "IE");

        var result = CreateSubject().Events(CreateRequest());

        GetResponse(result).Events.Keys.Should().Equal("A-UK");
        VerifyEventDivisions("UK", "IE");
    }

    [Fact]
    public void Events_ExplicitSelectionCanOnlyNarrowAssignedDivisions()
    {
        SetIdentity("UK,IE");
        SetAssets(CreateAsset("A-IE", "ie"), CreateAsset("A-FR", "FR"));
        SetEvents([CreateEvent("A-IE")], "IE");

        var result = CreateSubject().Events(CreateRequest(divisions: "ie;FR"));

        GetResponse(result).Events.Keys.Should().Equal("A-IE");
        VerifyEventDivisions("IE");
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("UK", "FR")]
    [InlineData("UK", ";")]
    public void Events_ReturnsEmptyWithoutReadingEvents_WhenNoAllowedDivision(
        string assignedDivisions,
        string? requestedDivisions)
    {
        SetIdentity(assignedDivisions);

        var result = CreateSubject().Events(CreateRequest(divisions: requestedDivisions));

        GetResponse(result).Events.Should().BeEmpty();
        _repository.Verify(repository => repository.GetEventsForDivisionsAndRange(
            It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string[]>()), Times.Never);
    }

    [Fact]
    public void Events_DiscardsRepositoryEventsForAssetsOutsideEffectiveDivisions()
    {
        SetIdentity("UK");
        SetAssets(CreateAsset("A-UK", "UK"), CreateAsset("A-FR", "FR"));
        SetEvents([CreateEvent("A-UK"), CreateEvent("A-FR")], "UK");

        var result = CreateSubject().Events(CreateRequest(divisions: "UK", assetIds: ["A-UK", "A-FR"]));

        var response = GetResponse(result);
        response.Events.Keys.Should().Equal("A-UK");
        response.Events.Should().NotContainKey("A-FR");
    }

    [Fact]
    public void Events_AllowsAllAssetDivisions_ForSuperAdminWithoutSelection()
    {
        SetIdentity("", isSuperAdmin: true);
        SetAssets(CreateAsset("A-UK", "UK"), CreateAsset("A-FR", "fr"));
        SetEvents([CreateEvent("A-UK"), CreateEvent("A-FR")], "UK", "FR");

        var result = CreateSubject().Events(CreateRequest());

        GetResponse(result).Events.Keys.Should().BeEquivalentTo("A-UK", "A-FR");
        VerifyEventDivisions("UK", "FR");
    }

    [Fact]
    public void Events_NormalizesStartDateAndDefaultsToAThreeMonthInclusiveRange()
    {
        SetIdentity("UK");
        SetAssets();
        SetEmptyEvents(new DateTime(2026, 1, 31), new DateTime(2026, 4, 29));

        var result = CreateSubject().Events(new AssetTimelineEventsRequest
        {
            StartDate = new DateTime(2026, 1, 31, 14, 30, 0),
            Divisions = "UK",
        });

        GetResponse(result).Events.Should().BeEmpty();
        _repository.Verify(repository => repository.GetEventsForDivisionsAndRange(
            new DateTime(2026, 1, 31),
            new DateTime(2026, 4, 29),
            It.Is<string[]>(divisions => divisions.SequenceEqual(new[] { "UK" }))), Times.Once);
    }

    [Fact]
    public void Events_NormalizesBothDatesWhenTheEndDateIsSupplied()
    {
        SetIdentity("UK");
        SetAssets();
        SetEmptyEvents(new DateTime(2026, 2, 3), new DateTime(2026, 2, 9));

        var result = CreateSubject().Events(new AssetTimelineEventsRequest
        {
            StartDate = new DateTime(2026, 2, 3, 14, 30, 0),
            EndDate = new DateTime(2026, 2, 9, 23, 59, 59),
            Divisions = "UK",
        });

        GetResponse(result).Events.Should().BeEmpty();
        _repository.Verify(repository => repository.GetEventsForDivisionsAndRange(
            new DateTime(2026, 2, 3),
            new DateTime(2026, 2, 9),
            It.Is<string[]>(divisions => divisions.SequenceEqual(new[] { "UK" }))), Times.Once);
    }

    [Fact]
    public void Events_TrimsAndGroupsAssetIdsInEncounterOrderWhilePreservingEventObjectsAndExactJson()
    {
        SetIdentity("UK");
        SetAssets(CreateAsset("asset-b", "UK"), CreateAsset("asset-a", "UK"));
        var first = new Event
        {
            AssetId = " asset-b ",
            EventType = "ONHIRE",
            StartDate = new DateTime(2026, 1, 2),
            EndDate = new DateTime(2026, 1, 3),
            Title = "First",
            CssClass = "onhire_event",
        };
        var second = new Event
        {
            AssetId = "asset-a",
            EventType = "COLLECTION",
            StartDate = new DateTime(2026, 1, 6),
            EndDate = new DateTime(2026, 1, 7),
            Title = "Second",
            CssClass = "collection_event",
        };
        var third = new Event
        {
            AssetId = "ASSET-B",
            StartDate = new DateTime(2026, 1, 4),
            EndDate = new DateTime(2026, 1, 5),
        };
        var missingEndDate = new Event
        {
            AssetId = "asset-a",
            StartDate = new DateTime(2026, 1, 8),
        };
        var blankAssetId = new Event
        {
            AssetId = " ",
            StartDate = new DateTime(2026, 1, 8),
            EndDate = new DateTime(2026, 1, 9),
        };
        SetEvents([first, second, third, missingEndDate, blankAssetId], "UK");

        var response = GetResponse(CreateSubject().Events(CreateRequest(divisions: "UK")));

        response.Events.Keys.Should().Equal("asset-b", "asset-a");
        response.Events["ASSET-B"].Should().Equal(first, third);
        response.Events["ASSET-A"].Should().Equal(second);
        response.Events["ASSET-B"][0].Should().BeSameAs(first);
        response.Events["ASSET-B"][1].Should().BeSameAs(third);
        first.AssetId.Should().Be("asset-b");
        third.AssetId.Should().Be("ASSET-B");

        JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)).Should().Be(
            "{\"events\":{\"asset-b\":[{\"assetId\":\"asset-b\",\"eventType\":\"ONHIRE\"," +
            "\"startDate\":\"2026-01-02T00:00:00\",\"endDate\":\"2026-01-03T00:00:00\"," +
            "\"title\":\"First\",\"cssClass\":\"onhire_event\"},{\"assetId\":\"ASSET-B\"," +
            "\"eventType\":null,\"startDate\":\"2026-01-04T00:00:00\"," +
            "\"endDate\":\"2026-01-05T00:00:00\",\"title\":null,\"cssClass\":null}]," +
            "\"asset-a\":[{\"assetId\":\"asset-a\",\"eventType\":\"COLLECTION\"," +
            "\"startDate\":\"2026-01-06T00:00:00\",\"endDate\":\"2026-01-07T00:00:00\"," +
            "\"title\":\"Second\",\"cssClass\":\"collection_event\"}]}}");
    }

    private EventController CreateSubject() => new(_repository.Object, _identity.Object, _logger.Object);

    private void SetIdentity(string divisions, bool isSuperAdmin = false)
    {
        _identity.Setup(identity => identity.GetIdentity()).Returns(new User
        {
            LoginName = "planner@example.com",
            FullName = "Fleet Planner",
            Division = divisions,
            IsSuperAdmin = isSuperAdmin,
            DateFormat = "dd/MM/yyyy",
        });
    }

    private void SetAssets(params Asset[] assets)
    {
        _repository.Setup(repository => repository.GetAssets()).Returns(assets.AsQueryable());
    }

    private void SetEvents(IList<Event> events, params string[] expectedDivisions)
    {
        _repository.Setup(repository => repository.GetEventsForDivisionsAndRange(
                new DateTime(2026, 1, 1),
                new DateTime(2026, 1, 31),
                It.Is<string[]>(divisions => divisions.SequenceEqual(expectedDivisions))))
            .Returns(events);
    }

    private void SetEmptyEvents(DateTime startDate, DateTime endDate)
    {
        _repository.Setup(repository => repository.GetEventsForDivisionsAndRange(
                startDate,
                endDate,
                It.Is<string[]>(divisions => divisions.SequenceEqual(new[] { "UK" }))))
            .Returns(Array.Empty<Event>());
    }

    private void VerifyEventDivisions(params string[] expectedDivisions)
    {
        _repository.Verify(repository => repository.GetEventsForDivisionsAndRange(
            new DateTime(2026, 1, 1),
            new DateTime(2026, 1, 31),
            It.Is<string[]>(divisions => divisions.SequenceEqual(expectedDivisions))), Times.Once);
    }

    private static AssetTimelineEventsRequest CreateRequest(
        string? divisions = null,
        string[]? assetIds = null) => new()
    {
        StartDate = new DateTime(2026, 1, 1),
        EndDate = new DateTime(2026, 1, 31),
        Divisions = divisions,
        AssetIds = assetIds,
    };

    private static AssetTimelineEventsResponse GetResponse(IActionResult result) =>
        result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<AssetTimelineEventsResponse>().Subject;

    private static Asset CreateAsset(string id, string division) => new()
    {
        Id = id,
        Division = division,
    };

    private static Event CreateEvent(string assetId) => new()
    {
        AssetId = assetId,
        EventType = "ONHIRE",
        StartDate = new DateTime(2026, 1, 2),
        EndDate = new DateTime(2026, 1, 3),
        Title = "Agreement",
        CssClass = "onhire_event",
    };
}
