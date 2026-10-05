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

public class AvailabilityControllerTests
{
    private readonly Mock<IDataRepository> _repository = new();
    private readonly Mock<IUserIdentity> _identity = new();
    private readonly Mock<ILogger<AvailabilityController>> _logger = new();

    [Fact]
    public async Task GetAvailabilitySummary_ReturnsUnauthorizedWithoutReadingRepository_WhenIdentityIsMissing()
    {
        _identity.Setup(identity => identity.GetIdentity()).Returns((User?)null!);

        var result = await CreateSubject().GetAvailabilitySummary("GEN-1");

        result.Should().BeOfType<UnauthorizedResult>();
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetAvailabilitySummary_ForwardsRequiredAndOptionalQueryValues()
    {
        var startDate = new DateTime(2026, 8, 1);
        var endDate = new DateTime(2026, 8, 31);
        SetIdentity("UK,IE");
        _repository.Setup(repository => repository.GetAvailabilitySummaryAsync(
                "GEN-1",
                "voltage=400 V",
                startDate,
                endDate,
                "IE",
                "ITEM-1",
                73))
            .ReturnsAsync([new AvailabilitySummaryResult()]);

        var result = await CreateSubject().GetAvailabilitySummary(
            "GEN-1",
            "voltage=400 V",
            startDate,
            endDate,
            "ie",
            "ITEM-1",
            73);

        GetResponses(result).Should().ContainSingle();
        _repository.VerifyAll();
    }

    [Fact]
    public async Task GetAvailabilitySummary_MapsFullAndDefaultResultsToExactWebJsonContract()
    {
        SetIdentity("UK");
        _repository.Setup(repository => repository.GetAvailabilitySummaryAsync(
                "GEN-1", "", null, null, "UK", null, null))
            .ReturnsAsync(
            [
                new AvailabilitySummaryResult
                {
                    WarehouseCode = "WH-1",
                    Warehouse = "London",
                    GenericCode = "GEN-1",
                    GenericDescription = "Generator",
                    ItemNumber = "ITEM-1",
                    DescriptionIntl = "Diesel generator",
                    Facility = "London East",
                    DivisionCode = "UK",
                    DivisionName = "United Kingdom",
                    Available = 7,
                    Count = 9,
                    GenericOnly = true,
                    ReservationMode = "quantity",
                    SubstitutionReason = "RELATED",
                },
                new AvailabilitySummaryResult(),
            ]);

        var result = await CreateSubject().GetAvailabilitySummary("GEN-1");

        var responses = GetResponses(result);
        SerializeWeb(responses).Should().Be(
            "[{\"warehouseCode\":\"WH-1\",\"warehouse\":\"London\",\"genericCode\":\"GEN-1\"," +
            "\"genericDescription\":\"Generator\",\"itemNumber\":\"ITEM-1\"," +
            "\"descriptionIntl\":\"Diesel generator\",\"facility\":\"London East\"," +
            "\"divisionCode\":\"UK\",\"divisionName\":\"United Kingdom\"," +
            "\"available\":7,\"count\":9,\"genericOnly\":true," +
            "\"reservationMode\":\"quantity\",\"substitutionReason\":\"RELATED\"}," +
            "{\"warehouseCode\":\"\",\"warehouse\":\"\",\"genericCode\":\"\"," +
            "\"genericDescription\":\"\",\"itemNumber\":\"\",\"descriptionIntl\":\"\"," +
            "\"facility\":\"\",\"divisionCode\":\"\",\"divisionName\":\"\"," +
            "\"available\":0,\"count\":0,\"genericOnly\":false," +
            "\"reservationMode\":\"asset\",\"substitutionReason\":null}]");
    }

    [Fact]
    public async Task GetAvailabilitySummary_FillsMissingLocationMetadataFromWarehouseLookup()
    {
        SetIdentity("UK,IE");
        _repository.Setup(repository => repository.GetAvailabilitySummaryAsync(
                "GEN-1", "", null, null, "UK,IE", null, null))
            .ReturnsAsync(
            [
                new AvailabilitySummaryResult
                {
                    WarehouseCode = "wh-1",
                    ItemNumber = "ITEM-1",
                },
                new AvailabilitySummaryResult
                {
                    WarehouseCode = "WH-2",
                    ItemNumber = "ITEM-2",
                    Facility = "Procedure facility",
                    DivisionCode = "PROCEDURE",
                    DivisionName = "Procedure division",
                },
                new AvailabilitySummaryResult
                {
                    WarehouseCode = "WH-3",
                    ItemNumber = "ITEM-3",
                },
            ]);
        _repository.Setup(repository => repository.GetWarehouses(
                It.Is<string[]>(divisions => divisions.SequenceEqual(new[] { "UK", "IE" }))))
            .Returns(
            [
                new WarehouseItem
                {
                    WarehouseCode = "WH-1",
                    DivisionCode = "UK",
                    Country = "United Kingdom",
                    Facility = "London East",
                    FacilityCode = "LON",
                },
                new WarehouseItem
                {
                    WarehouseCode = "WH-2",
                    DivisionCode = "UK",
                    Country = "United Kingdom",
                    Facility = "Lookup facility",
                    FacilityCode = "LOOKUP",
                },
                new WarehouseItem
                {
                    WarehouseCode = "WH-3",
                    DivisionCode = "IE",
                    Country = "Ireland",
                    Facility = " ",
                    FacilityCode = "DUB",
                },
            ]);

        var result = await CreateSubject().GetAvailabilitySummary("GEN-1", division: "uk,ie");

        var responses = GetResponses(result);
        responses[0].Should().BeEquivalentTo(new
        {
            Facility = "LON",
            DivisionCode = "UK",
            DivisionName = "United Kingdom",
        });
        responses[1].Should().BeEquivalentTo(new
        {
            Facility = "Procedure facility",
            DivisionCode = "PROCEDURE",
            DivisionName = "Procedure division",
        });
        responses[2].Should().BeEquivalentTo(new
        {
            Facility = "DUB",
            DivisionCode = "IE",
            DivisionName = "Ireland",
        });
        _repository.VerifyAll();
    }

    [Fact]
    public async Task GetAvailabilitySummary_ReturnsOriginalRows_WhenWarehouseMetadataFallbackFails()
    {
        SetIdentity("UK");
        _repository.Setup(repository => repository.GetAvailabilitySummaryAsync(
                "GEN-1", "", null, null, "UK", null, null))
            .ReturnsAsync(
            [
                new AvailabilitySummaryResult
                {
                    WarehouseCode = "WH-1",
                    ItemNumber = "ITEM-1",
                },
            ]);
        _repository.Setup(repository => repository.GetWarehouses(
                It.Is<string[]>(divisions => divisions.SequenceEqual(new[] { "UK" }))))
            .Throws(new InvalidOperationException("warehouse lookup unavailable"));

        var result = await CreateSubject().GetAvailabilitySummary("GEN-1");

        var response = GetResponses(result).Should().ContainSingle().Which;
        response.WarehouseCode.Should().Be("WH-1");
        response.Facility.Should().BeEmpty();
        response.DivisionCode.Should().BeEmpty();
        response.DivisionName.Should().BeEmpty();
        _repository.VerifyAll();
    }

    [Fact]
    public async Task GetAvailabilitySummary_DoesNotLoadWarehouses_WhenLocationMetadataIsComplete()
    {
        SetIdentity("UK");
        _repository.Setup(repository => repository.GetAvailabilitySummaryAsync(
                "GEN-1", "", null, null, "UK", null, null))
            .ReturnsAsync(
            [
                new AvailabilitySummaryResult
                {
                    WarehouseCode = "WH-1",
                    Facility = "London East",
                    DivisionCode = "UK",
                    DivisionName = "United Kingdom",
                },
            ]);

        var result = await CreateSubject().GetAvailabilitySummary("GEN-1");

        GetResponses(result).Should().ContainSingle();
        _repository.Verify(repository => repository.GetWarehouses(It.IsAny<string[]>()), Times.Never);
    }

    [Fact]
    public async Task GetAvailabilitySummary_DoesNotLoadWarehouses_WhenNoAvailabilityRowsExist()
    {
        SetIdentity("UK");
        _repository.Setup(repository => repository.GetAvailabilitySummaryAsync(
                "GEN-1", "", null, null, "UK", null, null))
            .ReturnsAsync([]);

        var result = await CreateSubject().GetAvailabilitySummary("GEN-1");

        GetResponses(result).Should().BeEmpty();
        _repository.Verify(repository => repository.GetWarehouses(It.IsAny<string[]>()), Times.Never);
    }

    [Fact]
    public async Task GetAvailabilitySummary_UsesFirstAssignedDivision_WhenRequestOmitsDivision()
    {
        SetIdentity("uk,IE");
        _repository.Setup(repository => repository.GetAvailabilitySummaryAsync(
                "GEN-1", "", null, null, "UK", null, null))
            .ReturnsAsync([]);

        var result = await CreateSubject().GetAvailabilitySummary("GEN-1");

        result.Should().BeOfType<OkObjectResult>();
        _repository.VerifyAll();
    }

    [Fact]
    public async Task GetAvailabilitySummary_AllowsExplicitAssignedDivisionIgnoringCase()
    {
        SetIdentity("UK,IE");
        _repository.Setup(repository => repository.GetAvailabilitySummaryAsync(
                "GEN-1", "", null, null, "IE", null, null))
            .ReturnsAsync([]);

        var result = await CreateSubject().GetAvailabilitySummary("GEN-1", division: "ie");

        result.Should().BeOfType<OkObjectResult>();
        _repository.VerifyAll();
    }

    [Fact]
    public async Task GetAvailabilitySummary_IntersectsAndPreservesMultipleExplicitDivisions()
    {
        SetIdentity("UK,IE");
        _repository.Setup(repository => repository.GetAvailabilitySummaryAsync(
                "GEN-1", "", null, null, "IE,UK", null, null))
            .ReturnsAsync([]);

        var result = await CreateSubject().GetAvailabilitySummary("GEN-1", division: "ie,FR,uk");

        result.Should().BeOfType<OkObjectResult>();
        _repository.VerifyAll();
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("UK", "FR")]
    public async Task GetAvailabilitySummary_ReturnsEmptyWithoutRepositoryCall_WhenNoAllowedDivision(
        string assignedDivisions,
        string? requestedDivision)
    {
        SetIdentity(assignedDivisions);

        var result = await CreateSubject().GetAvailabilitySummary("GEN-1", division: requestedDivision);

        var responses = result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<AvailabilitySummaryResponse[]>().Subject;
        responses.Should().BeEmpty();
        SerializeWeb(responses).Should().Be("[]");
        _repository.Verify(repository => repository.GetAvailabilitySummaryAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int?>()),
            Times.Never);
    }

    [Fact]
    public async Task GetAvailabilitySummary_AllowsExplicitDivision_ForSuperAdmin()
    {
        SetIdentity("", isSuperAdmin: true);
        _repository.Setup(repository => repository.GetAvailabilitySummaryAsync(
                "GEN-1", "", null, null, "FR", null, null))
            .ReturnsAsync([]);

        var result = await CreateSubject().GetAvailabilitySummary("GEN-1", division: "fr");

        result.Should().BeOfType<OkObjectResult>();
        _repository.VerifyAll();
    }

    [Fact]
    public async Task GetAvailabilitySummary_UsesUnrestrictedRepositoryQuery_ForSuperAdminWithoutSelection()
    {
        SetIdentity("", isSuperAdmin: true);
        _repository.Setup(repository => repository.GetWarehouseDivisionCodes())
            .Returns([" uk ", "FR", "UK", ""]);
        _repository.Setup(repository => repository.GetAvailabilitySummaryAsync(
                "GEN-1", "", null, null, "UK,FR", null, null))
            .ReturnsAsync([]);

        var result = await CreateSubject().GetAvailabilitySummary("GEN-1");

        result.Should().BeOfType<OkObjectResult>();
        _repository.VerifyAll();
    }

    [Fact]
    public async Task GetAvailabilitySummary_ReturnsEmpty_ForSuperAdminWhenNoDivisionsExist()
    {
        SetIdentity("", isSuperAdmin: true);
        _repository.Setup(repository => repository.GetWarehouseDivisionCodes()).Returns([]);

        var result = await CreateSubject().GetAvailabilitySummary("GEN-1");

        var responses = result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<AvailabilitySummaryResponse[]>().Subject;
        responses.Should().BeEmpty();
        SerializeWeb(responses).Should().Be("[]");
        _repository.Verify(repository => repository.GetAvailabilitySummaryAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int?>()),
            Times.Never);
    }

    [Fact]
    public async Task GetAvailabilitySummary_ReturnsEmpty_ForExplicitDelimiterOnlySelection()
    {
        SetIdentity("UK,IE", isSuperAdmin: true);

        var result = await CreateSubject().GetAvailabilitySummary("GEN-1", division: " , ");

        var responses = result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<AvailabilitySummaryResponse[]>().Subject;
        responses.Should().BeEmpty();
        SerializeWeb(responses).Should().Be("[]");
        _repository.Verify(repository => repository.GetAvailabilitySummaryAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int?>()),
            Times.Never);
    }

    private AvailabilityController CreateSubject() => new(_repository.Object, _identity.Object, _logger.Object);

    private static IReadOnlyList<AvailabilitySummaryResponse> GetResponses(IActionResult result)
    {
        return result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeAssignableTo<IEnumerable<AvailabilitySummaryResponse>>().Subject
            .ToList();
    }

    private static string SerializeWeb<T>(T value) => JsonSerializer.Serialize(
        value,
        new JsonSerializerOptions(JsonSerializerDefaults.Web));

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
}
