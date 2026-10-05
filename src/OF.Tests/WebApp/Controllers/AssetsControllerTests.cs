using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using OF.Data;
using OF.Data.Database;
using OF.UI.Database;
using OF.UI.Identity;
using OF.UI.Models;
using OF.WebApp.Controllers;
using OF.WebApp.Features.Assets;

namespace OF.Tests.WebApp.Controllers;

public class AssetsControllerTests
{
    private readonly Mock<IDataRepository> _repository = new();
    private readonly Mock<IUserIdentity> _identity = new();
    private readonly Mock<IAssetEnrichmentService> _assetEnrichmentService = new();

    [Fact]
    public void GetAssets_ReturnsUnauthorizedWithoutReadingAssets_WhenIdentityIsMissing()
    {
        _identity.Setup(identity => identity.GetIdentity()).Returns((User?)null!);

        var result = CreateSubject().GetAssets();

        result.Should().BeOfType<UnauthorizedResult>();
        _repository.Verify(repository => repository.GetAssets(), Times.Never);
        _repository.Verify(repository => repository.GetAssetItems(It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public void GetAssets_UsesAssignedDivisionsIgnoringCaseWhitespaceAndDuplicates()
    {
        SetIdentity(" uk, IE,uk ");
        SetAssetList(
            CreateAsset("A-UK", "UK"),
            CreateAsset("A-IE", "ie"),
            CreateAsset("A-FR", "FR"),
            CreateAsset("A-NONE", null));

        var result = CreateSubject().GetAssets();

        GetAssetIds(result).Should().Equal("A-IE", "A-UK");
    }

    [Fact]
    public void GetAssets_ExplicitDivisionsCanOnlyNarrowAssignedDivisions()
    {
        SetIdentity("UK, IE");
        SetAssetList(
            CreateAsset("A-UK", "UK"),
            CreateAsset("A-IE", "IE"),
            CreateAsset("A-FR", "FR"));

        var result = CreateSubject().GetAssets(division: " ie, FR ");

        GetAssetIds(result).Should().Equal("A-IE");
    }

    [Fact]
    public void GetAssets_ReturnsEmpty_WhenNormalUserHasNoAssignedDivisions()
    {
        SetIdentity(" , ");
        SetAssetList(CreateAsset("A-UK", "UK"));

        var result = CreateSubject().GetAssets(division: "UK");

        GetAssetIds(result).Should().BeEmpty();
    }

    [Fact]
    public void GetAssets_ReturnsEmpty_WhenExplicitDivisionsDoNotIntersectAssignedDivisions()
    {
        SetIdentity("UK");
        SetAssetList(CreateAsset("A-UK", "UK"), CreateAsset("A-FR", "FR"));

        var result = CreateSubject().GetAssets(division: "FR");

        GetAssetIds(result).Should().BeEmpty();
    }

    [Fact]
    public void GetAssets_PreservesCommaOnlyDivisionDelimiterContract()
    {
        SetIdentity("UK,IE");
        SetAssetList(CreateAsset("A-UK", "UK"), CreateAsset("A-IE", "IE"));

        var result = CreateSubject().GetAssets(division: "UK;IE");

        GetAssetIds(result).Should().BeEmpty();
    }

    [Fact]
    public void GetAssets_ReturnsAllDivisionsForSuperAdmin_WhenNoDivisionIsRequested()
    {
        SetIdentity(null, isSuperAdmin: true);
        SetAssetList(
            CreateAsset("A-UK", "UK"),
            CreateAsset("A-FR", "FR"),
            CreateAsset("A-NONE", null));

        var result = CreateSubject().GetAssets();

        GetAssetIds(result).Should().Equal("A-FR", "A-NONE", "A-UK");
    }

    [Fact]
    public void GetAssets_UsesRequestedDivisionsForSuperAdminIgnoringCaseAndWhitespace()
    {
        SetIdentity(null, isSuperAdmin: true);
        SetAssetList(CreateAsset("A-UK", "UK"), CreateAsset("A-IE", "ie"), CreateAsset("A-FR", "FR"));

        var result = CreateSubject().GetAssets(division: " ie, uk ");

        GetAssetIds(result).Should().Equal("A-IE", "A-UK");
    }

    [Fact]
    public void GetAssets_PreservesSubstringWarehouseAndItemNumberFiltersByDefault()
    {
        SetIdentity("UK");
        SetAssetList(
            CreateAsset("A-EXACT", "UK", "ED1", "ITEM-10"),
            CreateAsset("B-WAREHOUSE-SUFFIX", "UK", "ED10", "ITEM-10"),
            CreateAsset("C-ITEM-SUFFIX", "UK", "ED1", "ITEM-100"),
            CreateAsset("D-OTHER", "UK", "GL1", "OTHER"));

        var result = CreateSubject().GetAssets(warehouse: "ed1", itemNumber: "item-10");

        GetAssetIds(result).Should().Equal("A-EXACT", "B-WAREHOUSE-SUFFIX", "C-ITEM-SUFFIX");
    }

    [Fact]
    public void GetAssets_MatchesWarehouseLocationForTheDefaultWarehouseFilter()
    {
        SetIdentity("UK");
        var warehouseMatch = CreateAsset("A-WAREHOUSE", "UK", "ED1");
        var locationMatch = CreateAsset("B-LOCATION", "UK", "MAN");
        locationMatch.WarehouseLocation = "Yard Alpha";
        var excluded = CreateAsset("C-OTHER", "UK", "GLA");
        excluded.WarehouseLocation = "Yard Beta";
        SetAssetList(warehouseMatch, locationMatch, excluded);

        var result = CreateSubject().GetAssets(warehouse: "alpha");

        GetAssetIds(result).Should().Equal("B-LOCATION");
    }

    [Fact]
    public void GetAssets_UsesCaseInsensitiveTrimmedWarehouseAndItemNumberEquality_WhenExactMatchIsRequested()
    {
        SetIdentity("UK");
        SetAssetList(
            CreateAsset("A-EXACT", "UK", "Ed1", "Item-10"),
            CreateAsset("B-WAREHOUSE-SUFFIX", "UK", "ED10", "ITEM-10"),
            CreateAsset("C-ITEM-SUFFIX", "UK", "ED1", "ITEM-100"));

        var result = CreateSubject().GetAssets(
            warehouse: " ed1 ",
            itemNumber: " item-10 ",
            exactMatch: true);

        GetAssetIds(result).Should().Equal("A-EXACT");
    }

    [Theory]
    [InlineData("repair", "A-STATUS")]
    [InlineData("yard alpha", "B-LOCATION")]
    [InlineData("service depot", "C-FACILITY")]
    [InlineData("diesel generator", "D-DESCRIPTION")]
    public void GetAssets_BroadSearchMatchesVisibleAssetContext(string search, string expectedAssetId)
    {
        SetIdentity("UK");
        var statusAsset = CreateAsset("A-STATUS", "UK");
        statusAsset.Status = "Repair";
        var locationAsset = CreateAsset("B-LOCATION", "UK");
        locationAsset.WarehouseLocation = "Yard Alpha";
        var facilityAsset = CreateAsset("C-FACILITY", "UK");
        facilityAsset.Facility = "Service Depot";
        var descriptionAsset = CreateAsset("D-DESCRIPTION", "UK");
        descriptionAsset.Description = "Diesel Generator";
        SetAssetList(statusAsset, locationAsset, facilityAsset, descriptionAsset);

        var result = CreateSubject().GetAssets(search: search);

        GetAssetIds(result).Should().Equal(expectedAssetId);
    }

    [Fact]
    public void GetAssets_FiltersByAnyRequestedStatus()
    {
        SetIdentity("UK");
        var available = CreateAsset("A-AVAILABLE", "UK");
        available.Status = "Available";
        var repair = CreateAsset("B-REPAIR", "UK");
        repair.Status = "Repair";
        var service = CreateAsset("C-SERVICE", "UK");
        service.Status = "Service";
        SetAssetList(available, repair, service);

        var result = CreateSubject().GetAssets(statuses: " Available, Repair ");

        GetAssetIds(result).Should().Equal("A-AVAILABLE", "B-REPAIR");
    }

    [Fact]
    public void GetAssets_PreservesSingularStatusFilter()
    {
        SetIdentity("UK");
        var available = CreateAsset("A-AVAILABLE", "UK");
        available.Status = "Available";
        var repair = CreateAsset("B-REPAIR", "UK");
        repair.Status = "Repair";
        SetAssetList(available, repair);

        var result = CreateSubject().GetAssets(status: "Repair");

        GetAssetIds(result).Should().Equal("B-REPAIR");
    }

    [Fact]
    public void GetAssets_SerializesLegacyGridFieldsAndNoteCountsToWebJsonContract()
    {
        SetIdentity(null, isSuperAdmin: true);
        var completeAsset = new Asset
        {
            Id = "A-FULL",
            IndividualItemNumber = "SERIAL-1",
            ItemNumber = "ITEM-1",
            Status = "Available",
            Warehouse = "ED1",
            Division = "UK",
            Facility = "FAC1",
            EstimatedReadyDate = new DateTime(2026, 5, 1),
            AgreementNumber = "AGR-1",
            CustomerName = "Customer Ltd",
            CustomerNumber = "C-1",
            DeliveryDate = new DateTime(2026, 1, 2),
            AgreementLineValidFromDate = new DateTime(2026, 1, 3),
            AgreementLineValidToDate = new DateTime(2026, 12, 30),
            Description = "Diesel generator",
            WarehouseLocation = "Yard A",
            CollectionDate = new DateTime(2026, 7, 1),
            TerminationDate = new DateTime(2026, 6, 30),
            TelemetryStatus = "Online",
            ProductGroup = "Power",
            ProductCategory = "Generator",
            RunHours = 123.5,
            UsSizeRating = "500 kVA",
            Remark = "Requires inspection",
        };
        var nullAsset = new Asset
        {
            Id = "Z-NULL",
            IndividualItemNumber = "SERIAL-NULL",
        };
        SetAssetSources(
            new[] { nullAsset, completeAsset },
            new[]
            {
                new VwAssetItem
                {
                    Id = completeAsset.Id,
                    WarehouseName = "Edinburgh",
                    DaysOffHire = 12,
                },
                new VwAssetItem
                {
                    Id = nullAsset.Id,
                    WarehouseName = string.Empty,
                },
            });
        _repository.Setup(repository => repository.GetNotes("asset")).Returns(new[]
        {
            new Note
            {
                Id = 1,
                ParentId = completeAsset.Id,
                NoteType = "asset",
                Note1 = "Inspection complete",
                LastUpdatedBy = "planner@example.com",
                LastUpdatedDate = new DateTime(2026, 1, 7),
            },
        }.AsQueryable());

        var result = CreateSubject().GetAssets();

        var response = GetAssetResponses(result);
        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        json.Should().Be(
            "[{\"id\":\"A-FULL\",\"individualItemNumber\":\"SERIAL-1\",\"itemNumber\":\"ITEM-1\"," +
            "\"status\":\"Available\",\"warehouse\":\"ED1\",\"division\":\"UK\",\"facility\":\"FAC1\"," +
            "\"estimatedReadyDate\":\"2026-05-01T00:00:00\",\"agreementNumber\":\"AGR-1\"," +
            "\"customerName\":\"Customer Ltd\",\"deliveryDate\":\"2026-01-02T00:00:00\"," +
            "\"agreementLineValidFromDate\":\"2026-01-03T00:00:00\"," +
            "\"agreementLineValidToDate\":\"2026-12-30T00:00:00\",\"description\":\"Diesel generator\"," +
            "\"warehouseLocation\":\"Yard A\",\"collectionDate\":\"2026-07-01T00:00:00\"," +
            "\"terminationDate\":\"2026-06-30T00:00:00\",\"daysOffHire\":12,\"warehouseName\":\"Edinburgh\"," +
            "\"customerNumber\":\"C-1\",\"productGroup\":\"Power\",\"productCategory\":\"Generator\"," +
            "\"runHours\":123.5,\"size\":\"500 kVA\",\"telemetryStatus\":\"Online\"," +
            "\"remark\":\"Requires inspection\",\"noteCount\":1}," +
            "{\"id\":\"Z-NULL\",\"individualItemNumber\":\"SERIAL-NULL\",\"itemNumber\":null," +
            "\"status\":null,\"warehouse\":null,\"division\":null,\"facility\":null," +
            "\"estimatedReadyDate\":null,\"agreementNumber\":null,\"customerName\":null,\"deliveryDate\":null," +
            "\"agreementLineValidFromDate\":null,\"agreementLineValidToDate\":null,\"description\":null," +
            "\"warehouseLocation\":null,\"collectionDate\":null,\"terminationDate\":null,\"daysOffHire\":null," +
            "\"warehouseName\":\"\",\"customerNumber\":null,\"productGroup\":null,\"productCategory\":null," +
            "\"runHours\":null,\"size\":null,\"telemetryStatus\":null,\"remark\":null,\"noteCount\":0}]");
        using var document = JsonDocument.Parse(json);
        document.RootElement[0].EnumerateObject().Should().HaveCount(27);
        document.RootElement[0].GetProperty("telemetryStatus").GetString().Should().Be("Online");
        document.RootElement[0].GetProperty("noteCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public void GetAssets_PreservesLeftJoinAndOrdersBeforeApplyingTake()
    {
        SetIdentity("UK");
        var assetA = CreateAsset("A-MISSING-VIEW", "UK");
        var assetB = CreateAsset("B-MISSING-VIEW", "UK");
        var assetC = CreateAsset("C-OUTSIDE-TAKE", "UK");
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var context = new ApplicationDbContext(options);
        context.Assets.AddRange(assetC, assetA, assetB);
        context.SaveChanges();
        context.ChangeTracker.Clear();
        _repository.Setup(repository => repository.GetAssets()).Returns(context.Assets);
        _repository.Setup(repository => repository.GetAssetItems(true)).Returns(context.VwAssetItems);

        var result = CreateSubject().GetAssets(take: 2);

        var response = GetAssetResponses(result);
        response.Select(asset => asset.Id).Should().Equal("A-MISSING-VIEW", "B-MISSING-VIEW");
        response.Select(asset => asset.DaysOffHire).Should().OnlyContain(days => days == null);
        context.ChangeTracker.Entries<Asset>().Should().BeEmpty("the list should project directly to response rows");
        _repository.Verify(repository => repository.GetAssets(), Times.Once);
        _repository.Verify(repository => repository.GetAssetItems(true), Times.Once);
    }

    [Fact]
    public void GetAsset_ReturnsUnauthorizedWithoutReadingAsset_WhenIdentityIsMissing()
    {
        _identity.Setup(identity => identity.GetIdentity()).Returns((User?)null!);

        var result = CreateSubject().GetAsset("A-1");

        result.Should().BeOfType<UnauthorizedResult>();
        _repository.Verify(repository => repository.GetAsset(It.IsAny<string>()), Times.Never);
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public void GetAsset_ReturnsNotFound_WhenAssetIsMissing()
    {
        SetIdentity("UK");
        _repository.Setup(repository => repository.GetAsset("A-MISSING")).Returns((Asset)null!);

        var result = CreateSubject().GetAsset("A-MISSING");

        result.Should().BeOfType<NotFoundResult>();
        _repository.Verify(repository => repository.GetAsset("A-MISSING"), Times.Once);
        _repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" , ")]
    public void GetAsset_ReturnsNotFound_WhenNormalUserHasNoAssignedDivisions(string? assignedDivisions)
    {
        SetIdentity(assignedDivisions);
        _repository.Setup(repository => repository.GetAsset("A-UK")).Returns(CreateAsset("A-UK", "UK"));

        var result = CreateSubject().GetAsset("A-UK");

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void GetAsset_ReturnsNotFound_WhenAssetIsOutsideAssignedDivisions()
    {
        SetIdentity("UK");
        _repository.Setup(repository => repository.GetAsset("A-FR")).Returns(CreateAsset("A-FR", "FR"));

        var result = CreateSubject().GetAsset("A-FR");

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void GetAsset_ReturnsExactNamedResponse_WhenDivisionMatchesIgnoringCaseAndWhitespace()
    {
        SetIdentity(" uk, IE ");
        var asset = new Asset
        {
            Id = "A-UK",
            IndividualItemNumber = "SERIAL-1",
            StatusCode = "AVL",
            Warehouse = "ED1",
            ShipAddress1 = "1 High Street",
            AgreementNumber = "AGR-1",
            DeliveryDate = new DateTime(2026, 1, 2),
            AgreementLineValidToDate = new DateTime(2026, 12, 30),
            TerminationDate = new DateTime(2026, 12, 31),
            CustomerName = "Customer Ltd",
            TelemetryStatus = "Online",
            ServiceCenter = "SC1",
            Description = "Diesel generator",
            Status = "Available",
            ManufacturerName = "Maker",
            OwnerServiceCenter = "OSC1",
            CustomerNumber = "CUST-1",
            ItemNumber = "ITEM-1",
            ShipAddress3 = "Edinburgh",
            Facility = "FAC1",
            IonlastModified = new DateTime(2026, 1, 4, 5, 6, 7),
            WarehouseLocation = "Yard A",
            Division = "UK",
            Remark = "Inspection complete",
            ProductGroup = "Power",
            ProductCategory = "Generator",
            InternationalSizeRating = "Large",
            UsSizeRating = "10",
            RunHours = 123.5,
            EstimatedReadyDate = new DateTime(2026, 2, 1),
            AgreementLineValidFromDate = new DateTime(2026, 1, 1),
            CollectionDate = new DateTime(2026, 2, 14),
        };
        _repository.Setup(repository => repository.GetAsset("A-UK")).Returns(asset);

        var result = CreateSubject().GetAsset("A-UK");

        var response = GetAssetDetailResponse(result);
        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        json.Should().Be(
            "{\"id\":\"A-UK\",\"individualItemNumber\":\"SERIAL-1\",\"statusCode\":\"AVL\"," +
            "\"warehouse\":\"ED1\",\"shipAddress1\":\"1 High Street\",\"agreementNumber\":\"AGR-1\"," +
            "\"deliveryDate\":\"2026-01-02T00:00:00\",\"agreementLineValidToDate\":\"2026-12-30T00:00:00\"," +
            "\"terminationDate\":\"2026-12-31T00:00:00\",\"customerName\":\"Customer Ltd\"," +
            "\"telemetryStatus\":\"Online\",\"serviceCenter\":\"SC1\",\"description\":\"Diesel generator\"," +
            "\"status\":\"Available\",\"manufacturerName\":\"Maker\",\"ownerServiceCenter\":\"OSC1\"," +
            "\"customerNumber\":\"CUST-1\",\"itemNumber\":\"ITEM-1\",\"shipAddress3\":\"Edinburgh\"," +
            "\"facility\":\"FAC1\",\"ionlastModified\":\"2026-01-04T05:06:07\"," +
            "\"warehouseLocation\":\"Yard A\",\"division\":\"UK\",\"remark\":\"Inspection complete\"," +
            "\"productGroup\":\"Power\",\"productCategory\":\"Generator\"," +
            "\"internationalSizeRating\":\"Large\",\"usSizeRating\":\"10\",\"runHours\":123.5," +
            "\"estimatedReadyDate\":\"2026-02-01T00:00:00\"," +
            "\"agreementLineValidFromDate\":\"2026-01-01T00:00:00\"," +
            "\"collectionDate\":\"2026-02-14T00:00:00\"}");
        using var document = JsonDocument.Parse(json);
        document.RootElement.EnumerateObject().Should().HaveCount(32);
        _repository.Verify(repository => repository.GetAsset("A-UK"), Times.Once);
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public void GetAsset_ReturnsExactAllNullOptionalFieldsForSuperAdmin_WhenAssetHasNoDivision()
    {
        SetIdentity(null, isSuperAdmin: true);
        var asset = new Asset
        {
            Id = "A-NONE",
            IndividualItemNumber = "SERIAL-NONE",
        };
        _repository.Setup(repository => repository.GetAsset("A-NONE")).Returns(asset);

        var result = CreateSubject().GetAsset("A-NONE");

        var response = GetAssetDetailResponse(result);
        JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .Should().Be(
                "{\"id\":\"A-NONE\",\"individualItemNumber\":\"SERIAL-NONE\",\"statusCode\":null," +
                "\"warehouse\":null,\"shipAddress1\":null,\"agreementNumber\":null,\"deliveryDate\":null," +
                "\"agreementLineValidToDate\":null,\"terminationDate\":null,\"customerName\":null," +
                "\"telemetryStatus\":null,\"serviceCenter\":null,\"description\":null,\"status\":null," +
                "\"manufacturerName\":null,\"ownerServiceCenter\":null,\"customerNumber\":null," +
                "\"itemNumber\":null,\"shipAddress3\":null,\"facility\":null,\"ionlastModified\":null," +
                "\"warehouseLocation\":null,\"division\":null,\"remark\":null,\"productGroup\":null," +
                "\"productCategory\":null,\"internationalSizeRating\":null,\"usSizeRating\":null," +
                "\"runHours\":null,\"estimatedReadyDate\":null,\"agreementLineValidFromDate\":null," +
                "\"collectionDate\":null}");
        _repository.Verify(repository => repository.GetAsset("A-NONE"), Times.Once);
        _repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null, "UK")]
    [InlineData("", "UK")]
    [InlineData("UK", "FR")]
    public void GetAssetProfile_ReturnsNotFoundWithoutReadingSchedule_WhenAssetIsInaccessible(
        string? assignedDivisions,
        string assetDivision)
    {
        SetIdentity(assignedDivisions);
        _repository.Setup(repository => repository.GetAssets())
            .Returns(new[] { CreateAsset("A-1", assetDivision) }.AsQueryable());

        var result = CreateSubject().GetAssetProfile(
            "A-1",
            new DateTime(2026, 1, 1),
            new DateTime(2026, 1, 31));

        result.Should().BeOfType<NotFoundResult>();
        _repository.Verify(repository => repository.GetAssetScheduleReservations(
            It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Never);
        _repository.Verify(repository => repository.GetRingfencesForAsset(
            It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public void GetAssetProfile_ReturnsProfile_WhenDivisionMatchesIgnoringCaseAndWhitespace()
    {
        SetIdentity(" uk, IE ");
        SetProfileAsset(CreateAsset("A-UK", "UK"));

        var result = CreateSubject().GetAssetProfile(
            "A-UK",
            new DateTime(2026, 1, 1),
            new DateTime(2026, 1, 31));

        result.Should().BeOfType<OkObjectResult>();
        VerifyProfileSources("A-UK");
    }

    [Fact]
    public void GetAssetProfile_ReturnsProfileForSuperAdmin_WhenAssetHasNoDivision()
    {
        SetIdentity(null, isSuperAdmin: true);
        SetProfileAsset(CreateAsset("A-NONE", null));

        var result = CreateSubject().GetAssetProfile(
            "A-NONE",
            new DateTime(2026, 1, 1),
            new DateTime(2026, 1, 31));

        result.Should().BeOfType<OkObjectResult>();
        VerifyProfileSources("A-NONE");
    }

    [Fact]
    public async Task GetAssetEnrichment_AuthorizesAssetAndUsesIndividualItemNumber()
    {
        SetIdentity("UK");
        var asset = CreateAsset("A-UK", "UK");
        asset.IndividualItemNumber = "PLANT-123";
        _repository.Setup(repository => repository.GetAsset("A-UK")).Returns(asset);
        var enrichment = new AssetEnrichmentResponse(null, [], [], []);
        _assetEnrichmentService
            .Setup(service => service.GetAsync("PLANT-123", 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(enrichment);

        var result = await CreateSubject().GetAssetEnrichment("A-UK");

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(enrichment);
        _assetEnrichmentService.Verify(
            service => service.GetAsync("PLANT-123", 20, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAssetEnrichment_DoesNotQueryMdpForInaccessibleAsset()
    {
        SetIdentity("UK");
        _repository.Setup(repository => repository.GetAsset("A-FR")).Returns(CreateAsset("A-FR", "FR"));

        var result = await CreateSubject().GetAssetEnrichment("A-FR");

        result.Should().BeOfType<NotFoundResult>();
        _assetEnrichmentService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetAssetEnrichment_ReturnsServiceUnavailableWithoutBreakingCoreProfile()
    {
        SetIdentity("UK");
        _repository.Setup(repository => repository.GetAsset("A-UK")).Returns(CreateAsset("A-UK", "UK"));
        _assetEnrichmentService
            .Setup(service => service.GetAsync(It.IsAny<string>(), 20, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AssetEnrichmentUnavailableException("Unavailable", new TimeoutException()));

        var result = await CreateSubject().GetAssetEnrichment("A-UK");

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(503);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public async Task GetAssetEnrichment_RejectsUnboundedServiceLimits(int serviceLimit)
    {
        SetIdentity("UK");
        _repository.Setup(repository => repository.GetAsset("A-UK")).Returns(CreateAsset("A-UK", "UK"));

        var result = await CreateSubject().GetAssetEnrichment("A-UK", serviceLimit);

        result.Should().BeOfType<BadRequestObjectResult>();
        _assetEnrichmentService.VerifyNoOtherCalls();
    }

    private AssetsController CreateSubject() => new(
        _repository.Object,
        _identity.Object,
        _assetEnrichmentService.Object,
        Microsoft.Extensions.Logging.Abstractions.NullLogger<AssetsController>.Instance);

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
    }

    private void SetAssetList(params Asset[] assets)
    {
        SetAssetSources(
            assets,
            assets.Select(asset => new VwAssetItem
            {
                Id = asset.Id,
                Division = asset.Division,
                WarehouseName = asset.Warehouse ?? string.Empty,
            }).ToArray());
    }

    private void SetAssetSources(Asset[] assets, VwAssetItem[] assetItems)
    {
        _repository.Setup(repository => repository.GetAssets()).Returns(assets.AsQueryable());
        _repository.Setup(repository => repository.GetAssetItems(true)).Returns(assetItems.AsQueryable());
    }

    private void SetProfileAsset(Asset asset)
    {
        _repository.Setup(repository => repository.GetAssets()).Returns(new[] { asset }.AsQueryable());
        _repository.Setup(repository => repository.GetAssetScheduleReservations(
            asset.Id,
            new DateTime(2026, 1, 1),
            new DateTime(2026, 1, 31))).Returns(new List<AssetScheduleReservation>());
        _repository.Setup(repository => repository.GetRingfencesForAsset(
            asset.Id,
            new DateTime(2026, 1, 1),
            new DateTime(2026, 1, 31))).Returns(new List<Ringfence>());
    }

    private void VerifyProfileSources(string assetId)
    {
        _repository.Verify(repository => repository.GetAssetScheduleReservations(
            assetId,
            new DateTime(2026, 1, 1),
            new DateTime(2026, 1, 31)), Times.Once);
        _repository.Verify(repository => repository.GetRingfencesForAsset(
            assetId,
            new DateTime(2026, 1, 1),
            new DateTime(2026, 1, 31)), Times.Once);
    }

    private static IReadOnlyList<string> GetAssetIds(IActionResult result)
    {
        return GetAssetResponses(result)
            .Select(asset => asset.Id)
            .ToList();
    }

    private static IReadOnlyList<AssetListItemResponse> GetAssetResponses(IActionResult result)
    {
        return result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<List<AssetListItemResponse>>().Subject;
    }

    private static AssetDetailResponse GetAssetDetailResponse(IActionResult result)
    {
        return result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<AssetDetailResponse>().Subject;
    }

    private static Asset CreateAsset(
        string id,
        string? division,
        string warehouse = "ED1",
        string? itemNumber = null)
    {
        return new Asset
        {
            Id = id,
            IndividualItemNumber = id,
            ItemNumber = itemNumber,
            Division = division,
            Status = "AVAILABLE",
            Warehouse = warehouse,
        };
    }
}
