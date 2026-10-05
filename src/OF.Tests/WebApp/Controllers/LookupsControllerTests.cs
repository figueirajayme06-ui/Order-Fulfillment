using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using OF.Data.Database;
using OF.UI.Database;
using OF.UI.Identity;
using OF.WebApp.Controllers;
using System.Text.Json;

namespace OF.Tests.WebApp.Controllers;

public class LookupsControllerTests
{
    private readonly Mock<IDataRepository> _repository = new();
    private readonly Mock<IUserIdentity> _identity = new();

    [Fact]
    public void GetDivisions_ReturnsUnauthorizedWithoutReadingRepositories_WhenIdentityIsMissing()
    {
        _identity.Setup(identity => identity.GetIdentity()).Returns((User?)null!);

        var result = CreateSubject().GetDivisions();

        result.Should().BeOfType<UnauthorizedResult>();
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public void GetDivisions_UsesTrimmedCallerDivisionsAndReturnsGroupedSortedLookups()
    {
        SetIdentity(" UK, IE ");
        _repository.Setup(repository => repository.GetWarehouses(It.IsAny<string[]>())).Returns(new[]
        {
            CreateWarehouse("UK", "United Kingdom", "UK-1"),
            CreateWarehouse("IE", "Ireland", "IE-1"),
            CreateWarehouse("UK", "Ignored duplicate country", "UK-2"),
        });

        var result = CreateSubject().GetDivisions();

        var divisions = GetOkDivisions(result);
        divisions.Select(division => (division.Code, division.Name)).Should().Equal(
            ("IE", "Ireland"),
            ("UK", "United Kingdom"));
        _repository.Verify(repository => repository.GetWarehouses(
            It.Is<string[]>(values => values.SequenceEqual(new[] { "UK", "IE" }))), Times.Once);
        _repository.Verify(repository => repository.GetWarehouseDivisionCodes(), Times.Never);
    }

    [Fact]
    public void GetDivisions_UsesConfiguredWarehouseDivisions_ForSuperAdmin()
    {
        SetIdentity("IGNORED", isSuperAdmin: true);
        _repository.Setup(repository => repository.GetWarehouseDivisionCodes()).Returns(["UK", "FR"]);
        _repository.Setup(repository => repository.GetWarehouses(It.IsAny<string[]>())).Returns(new[]
        {
            CreateWarehouse("UK", "United Kingdom", "UK-1"),
            CreateWarehouse("FR", "France", "FR-1"),
        });

        var result = CreateSubject().GetDivisions();

        GetOkDivisions(result).Select(division => division.Code).Should().Equal("FR", "UK");
        _repository.Verify(repository => repository.GetWarehouseDivisionCodes(), Times.Once);
        _repository.Verify(repository => repository.GetWarehouses(
            It.Is<string[]>(values => values.SequenceEqual(new[] { "UK", "FR" }))), Times.Once);
    }

    [Fact]
    public void GetDivisions_DisplaysDivision200AsUsa()
    {
        SetIdentity("200");
        _repository.Setup(repository => repository.GetWarehouses(It.IsAny<string[]>())).Returns(new[]
        {
            CreateWarehouse("200", "Legacy country name", "US-1"),
        });

        var result = CreateSubject().GetDivisions();

        GetOkDivisions(result).Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Code = "200",
            Name = "USA",
        });
    }

    [Fact]
    public void GetDivisions_SerializesToExactTwoPropertyWebJsonContract()
    {
        SetIdentity("UK");
        _repository.Setup(repository => repository.GetWarehouses(It.IsAny<string[]>())).Returns(new[]
        {
            CreateWarehouse("UK", "United Kingdom", "UK-1"),
        });
        var divisions = GetOkDivisions(CreateSubject().GetDivisions());

        var json = JsonSerializer.Serialize(
            divisions,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        json.Should().Be("""[{"code":"UK","name":"United Kingdom"}]""");
    }

    [Fact]
    public void GetWarehouses_ReturnsUnauthorizedWithoutReadingRepository_WhenIdentityIsMissing()
    {
        _identity.Setup(identity => identity.GetIdentity()).Returns((User?)null!);

        var result = CreateSubject().GetWarehouses("UK");

        result.Should().BeOfType<UnauthorizedResult>();
        _repository.Verify(repository => repository.GetWarehouses(It.IsAny<string[]>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetWarehouses_ReturnsBadRequestWithoutReadingRepository_WhenDivisionIsBlank(string? division)
    {
        SetIdentity("UK");

        var result = CreateSubject().GetWarehouses(division);

        result.Should().BeOfType<BadRequestObjectResult>();
        _repository.Verify(repository => repository.GetWarehouses(It.IsAny<string[]>()), Times.Never);
    }

    [Fact]
    public void GetWarehouses_ReturnsEmptyWithoutReadingRepository_WhenNoRequestedDivisionIsAuthorized()
    {
        SetIdentity("UK");

        var result = CreateSubject().GetWarehouses("FR");

        GetOkWarehouses(result).Should().BeEmpty();
        _repository.Verify(repository => repository.GetWarehouses(It.IsAny<string[]>()), Times.Never);
    }

    [Fact]
    public void GetWarehouses_ReturnsTrimmedDeduplicatedSortedRows_ForAuthorizedDivisionsOnly()
    {
        SetIdentity(" UK, IE ");
        _repository.Setup(repository => repository.GetWarehouses(It.IsAny<string[]>())).Returns(new[]
        {
            CreateWarehouse("FR", "France", "FR1"),
            new WarehouseItem
            {
                WarehouseCode = " UK1 ",
                Warehouse = " London ",
                DivisionCode = " uk ",
                FacilityCode = " UKC ",
                Facility = "ignored facility",
                CountryCode = "UK",
                Country = " United Kingdom ",
            },
            new WarehouseItem
            {
                WarehouseCode = " IE1 ",
                Warehouse = "   ",
                DivisionCode = "IE",
                FacilityCode = "   ",
                Facility = " DUB ",
                CountryCode = "IE",
                Country = " Ireland ",
            },
            new WarehouseItem
            {
                WarehouseCode = "uk1",
                Warehouse = "duplicate",
                DivisionCode = "UK",
                FacilityCode = "UKZ",
                Facility = "duplicate facility",
                CountryCode = "UK",
                Country = "United Kingdom",
            },
            new WarehouseItem
            {
                WarehouseCode = "   ",
                Warehouse = "invalid",
                DivisionCode = "UK",
                FacilityCode = "UKC",
                Facility = "UK central",
                CountryCode = "UK",
                Country = "United Kingdom",
            },
        });

        var result = CreateSubject().GetWarehouses("ie, FR, uk, IE");

        GetOkWarehouses(result).Select(warehouse => new
        {
            warehouse.WarehouseCode,
            warehouse.Warehouse,
            warehouse.Facility,
            warehouse.DivisionCode,
            warehouse.DivisionName,
        }).Should().Equal(
            new
            {
                WarehouseCode = "IE1",
                Warehouse = "IE1",
                Facility = "DUB",
                DivisionCode = "IE",
                DivisionName = "Ireland",
            },
            new
            {
                WarehouseCode = "UK1",
                Warehouse = "London",
                Facility = "UKC",
                DivisionCode = "uk",
                DivisionName = "United Kingdom",
            });
        _repository.Verify(repository => repository.GetWarehouses(
            It.Is<string[]>(values => values.SequenceEqual(new[] { "IE", "UK" }))), Times.Once);
    }

    [Fact]
    public void GetWarehouses_AllowsAnExplicitDivision_ForSuperAdmin()
    {
        SetIdentity("IGNORED", isSuperAdmin: true);
        _repository.Setup(repository => repository.GetWarehouses(It.IsAny<string[]>())).Returns(new[]
        {
            CreateWarehouse("FR", "France", "FR1"),
        });

        var result = CreateSubject().GetWarehouses(" fr ");

        GetOkWarehouses(result).Select(warehouse => warehouse.DivisionCode).Should().Equal("FR");
        _repository.Verify(repository => repository.GetWarehouses(
            It.Is<string[]>(values => values.SequenceEqual(new[] { "FR" }))), Times.Once);
    }

    [Fact]
    public void GetWarehouses_DisplaysDivision200AsUsa()
    {
        SetIdentity("200");
        _repository.Setup(repository => repository.GetWarehouses(It.IsAny<string[]>())).Returns(new[]
        {
            CreateWarehouse("200", "Legacy country name", "US1"),
        });

        var result = CreateSubject().GetWarehouses("200");

        GetOkWarehouses(result).Should().ContainSingle().Which.DivisionName.Should().Be("USA");
    }

    [Fact]
    public void GetWarehouses_SerializesFacilityNameInWebJsonContract()
    {
        SetIdentity("UK");
        _repository.Setup(repository => repository.GetWarehouses(It.IsAny<string[]>())).Returns(new[]
        {
            CreateWarehouse("UK", "United Kingdom", "UK1"),
        });
        var warehouses = GetOkWarehouses(CreateSubject().GetWarehouses("UK"));

        var json = JsonSerializer.Serialize(
            warehouses,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        json.Should().Be(
            """[{"warehouseCode":"UK1","warehouse":"UK1","facility":"UK1-FAC","facilityName":"UK1 facility","divisionCode":"UK","divisionName":"United Kingdom"}]""");
    }

    [Fact]
    public void GetWarehouses_PropagatesUnexpectedRepositoryErrors()
    {
        SetIdentity("UK");
        _repository.Setup(repository => repository.GetWarehouses(It.IsAny<string[]>()))
            .Throws(new InvalidOperationException("warehouse lookup failed"));

        var action = () => CreateSubject().GetWarehouses("UK");

        action.Should().Throw<InvalidOperationException>().WithMessage("warehouse lookup failed");
    }

    [Fact]
    public void GetUsers_ReturnsUnauthorizedWithoutReadingRepository_WhenIdentityIsMissing()
    {
        _identity.Setup(identity => identity.GetIdentity()).Returns((User?)null!);

        var result = CreateSubject().GetUsers("UK");

        result.Should().BeOfType<UnauthorizedResult>();
        _repository.Verify(repository => repository.GetUsers(), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetUsers_ReturnsBadRequestWithoutReadingRepository_WhenDivisionIsBlank(string? division)
    {
        SetIdentity("UK");

        var result = CreateSubject().GetUsers(division);

        result.Should().BeOfType<BadRequestObjectResult>();
        _repository.Verify(repository => repository.GetUsers(), Times.Never);
    }

    [Fact]
    public void GetUsers_ReturnsEmptyWithoutReadingRepository_WhenNoRequestedDivisionIsAuthorized()
    {
        SetIdentity("UK");

        var result = CreateSubject().GetUsers("FR");

        GetOkUsers(result).Should().BeEmpty();
        _repository.Verify(repository => repository.GetUsers(), Times.Never);
    }

    [Fact]
    public void GetUsers_ReturnsTrimmedSortedUsersAssignedToAnyAuthorizedSelectedDivision()
    {
        SetIdentity(" UK, IE ");
        _repository.Setup(repository => repository.GetUsers()).Returns(new[]
        {
            CreateUser("zoe@example.com", " Zoe Planner ", " UK, FR "),
            CreateUser("anna@example.com", "Anna Planner", " ie "),
            CreateUser("fr@example.com", "French Planner", "FR"),
            CreateUser("fallback@example.com", "   ", "UK"),
            CreateUser("mixed@example.com", "Mixed Planner", "fr, UK"),
        }.AsQueryable());

        var result = CreateSubject().GetUsers(" ie, FR, uk, IE ");

        GetOkUsers(result).Select(user => (user.LoginName, user.FullName)).Should().Equal(
            ("anna@example.com", "Anna Planner"),
            ("fallback@example.com", "fallback@example.com"),
            ("mixed@example.com", "Mixed Planner"),
            ("zoe@example.com", "Zoe Planner"));
        _repository.Verify(repository => repository.GetUsers(), Times.Once);
    }

    [Fact]
    public void GetUsers_AllowsAnExplicitDivision_ForSuperAdmin()
    {
        SetIdentity("IGNORED", isSuperAdmin: true);
        _repository.Setup(repository => repository.GetUsers()).Returns(new[]
        {
            CreateUser("fr@example.com", "French Planner", "FR"),
            CreateUser("uk@example.com", "UK Planner", "UK"),
        }.AsQueryable());

        var result = CreateSubject().GetUsers(" fr ");

        GetOkUsers(result).Select(user => user.LoginName).Should().Equal("fr@example.com");
        _repository.Verify(repository => repository.GetUsers(), Times.Once);
    }

    [Fact]
    public void GetUsers_SerializesToExactTwoPropertyWebJsonContract()
    {
        SetIdentity("UK");
        _repository.Setup(repository => repository.GetUsers()).Returns(new[]
        {
            CreateUser("planner@example.com", "Fleet Planner", "UK"),
        }.AsQueryable());

        var users = GetOkUsers(CreateSubject().GetUsers("UK"));

        var json = JsonSerializer.Serialize(
            users,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        json.Should().Be("""[{"loginName":"planner@example.com","fullName":"Fleet Planner"}]""");
    }

    private LookupsController CreateSubject() => new(_repository.Object, _identity.Object);

    private void SetIdentity(string division, bool isSuperAdmin = false)
    {
        _identity.Setup(identity => identity.GetIdentity()).Returns(new User
        {
            LoginName = "planner@example.com",
            FullName = "Fleet Planner",
            Division = division,
            IsSuperAdmin = isSuperAdmin,
            DateFormat = "dd/MM/yyyy",
        });
    }

    private static IReadOnlyList<DivisionLookupResponse> GetOkDivisions(IActionResult result)
    {
        return result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeAssignableTo<IEnumerable<DivisionLookupResponse>>().Subject
            .ToList();
    }

    private static IReadOnlyList<WarehouseLookupResponse> GetOkWarehouses(IActionResult result)
    {
        return result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeAssignableTo<IEnumerable<WarehouseLookupResponse>>().Subject
            .ToList();
    }

    private static IReadOnlyList<UserLookupResponse> GetOkUsers(IActionResult result)
    {
        return result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeAssignableTo<IEnumerable<UserLookupResponse>>().Subject
            .ToList();
    }

    private static WarehouseItem CreateWarehouse(string divisionCode, string country, string warehouseCode)
    {
        return new WarehouseItem
        {
            WarehouseCode = warehouseCode,
            Warehouse = warehouseCode,
            DivisionCode = divisionCode,
            FacilityCode = $"{warehouseCode}-FAC",
            Facility = $"{warehouseCode} facility",
            CountryCode = divisionCode,
            Country = country,
        };
    }

    private static User CreateUser(string loginName, string fullName, string division)
    {
        return new User
        {
            LoginName = loginName,
            FullName = fullName,
            Division = division,
            DateFormat = "dd/MM/yyyy",
        };
    }
}
