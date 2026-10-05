using FluentAssertions;
using Moq;
using OF.Data.Database;
using OF.UI.Database;
using OF.UI.Engine;
using OF.UI.Identity;
using OF.UI.Models;
using Xunit.Abstractions;

namespace OF.Tests.UI.Engine;
public class FulfilmentEngineTests
{
    private IDataRepository _repo;
    private IUserIdentity _identity;
    private readonly IFulfilmentEngine _sut;
    private readonly ITestOutputHelper _output;
    private const int ValidLineId = 1;
    private const string ValidItem = "Valid Item";

    public FulfilmentEngineTests(ITestOutputHelper helper)
    {
        _output = helper;
        _repo = Mock.Of<IDataRepository>();
        _identity = Mock.Of<IUserIdentity>();

        var admin = new User
        {
            FullName = "Test User",
            LoginName = "testuser@aggreko.com",
            IsAdmin = true
        };

        Mock.Get(_identity).Setup(x => x.GetIdentity()).Returns(admin);

        _sut = new FulfilmentEngine(_repo, _identity);
    }

    [Theory]
    [InlineData("Description", "Description", "Description")]
    [InlineData("LineDescription", "GenericDescription", "LineDescription  (GenericDescription)")]
    [InlineData("", "GenericDescription", "GenericDescription")]
    [InlineData(" ", "GenericDescription", "GenericDescription")]
    [InlineData(null, "GenericDescription", "GenericDescription")]
    [InlineData(null, null, "Unknown")]
    [InlineData("LineDescription", null, "LineDescription  (Unknown)")]
    public void GivenLine_WhenCallingSatisfyLine_ItemDescriptionIsCorrect(string? lineDescription, string genericDescription, string expected)
    {
        // Arrange
        SetupRepo(lineDescription, genericDescription);

        // Act
        var response = _sut.SatisfyLine(new FulfilmentRequest { LineId = ValidLineId });

        // Assert
        response.ItemDescription.Should().Be(expected);
    }

    private void SetupRepo(string? lineDescription, string genericDescription)
    {
        var line = new Line()
        {
            ItemDescription = lineDescription,
            Id = ValidLineId,
            ItemNumber = ValidItem,
            AgreementLineNumber = "A1234358"
        };

        var item = new CpqItem
        {
            ItemNumber = "ItemNumber",
            GenericId = 100
        };

        var generic = new CpqGeneric
        {
            GenericDescription = genericDescription,
            LineId = 1
        };

        var productLine = new CpqLine
        {
            FamilyId = 1,
        };

        Mock.Get(_repo).Setup(x => x.GetLine(ValidLineId)).Returns(line);
        Mock.Get(_repo).Setup(x => x.GetItem(line.ItemNumber)).Returns(item);
        Mock.Get(_repo).Setup(x => x.GetGeneric(item.GenericId)).Returns(generic);
        Mock.Get(_repo).Setup(x => x.GetProductLine(generic.LineId)).Returns(productLine);
    }

    [Theory]
    [InlineData("RemovedStock")]
    [InlineData("Scrap")]
    [InlineData("Sold")]
    public void Reserve_WhenAssetIsDeleted_ReturnsFailure(string assetStatus)
    {
        // Arrange
        var asset = new Asset { Id = "ASSET001", Status = assetStatus };
        Mock.Get(_repo).Setup(x => x.GetAsset("ASSET001")).Returns(asset);

        var request = new ReserveRequest
        {
            IsDelete = false,
            AssetId = "ASSET001",
            LineId = ValidLineId,
            ItemNumber = ValidItem,
            Warehouse = "WH1",
            IsSerialized = true,
            IsRehire = false,
            IsDepotFulfiled = false,
            Multiple = 1
        };

        // Act
        var response = _sut.Reserve(request);

        // Assert
        response.IsSuccess.Should().BeFalse();
        response.ErrorMessage.Should().Contain(assetStatus);
        response.ErrorMessage.Should().Contain("ASSET001");
    }

    [Fact]
    public void Reserve_WhenAssetIsActive_DoesNotBlockReservation()
    {
        // Arrange
        var asset = new Asset { Id = "ASSET002", Status = "OnHire" };
        var line = new Line
        {
            Id = ValidLineId,
            ItemNumber = ValidItem,
            Quantity = 1,
            AgreementLineNumber = "A1234358",
            HeaderId = 1,
            ValidFromDate = DateTime.Now,
            ValidToDate = DateTime.Now.AddDays(30)
        };
        var header = new Header { Id = 1, FulfilmentStatus = 0, AgreementNumber = "A1234567" };

        Mock.Get(_repo).Setup(x => x.GetAsset("ASSET002")).Returns(asset);
        Mock.Get(_repo).Setup(x => x.GetLine(ValidLineId)).Returns(line);
        Mock.Get(_repo).Setup(x => x.GetReservationSumForLine(ValidLineId)).Returns(0);
        Mock.Get(_repo).Setup(x => x.GetOverlappingReservations(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>())).Returns(Array.Empty<Reservation>());
        Mock.Get(_repo).Setup(x => x.CreateReservation(It.IsAny<IUserIdentity>(), It.IsAny<Reservation>())).Returns(new Reservation());
        Mock.Get(_repo).Setup(x => x.GetHeaderForLineId(ValidLineId)).Returns(header);
        Mock.Get(_repo).Setup(x => x.GetHeader(1)).Returns(header);
        Mock.Get(_repo).Setup(x => x.GetNonServiceLines(1)).Returns(new List<Line> { line }.AsQueryable());
        Mock.Get(_repo).Setup(x => x.DeleteAlertsForLine(ValidLineId));

        var request = new ReserveRequest
        {
            IsDelete = false,
            AssetId = "ASSET002",
            LineId = ValidLineId,
            ItemNumber = ValidItem,
            Warehouse = "WH1",
            IsSerialized = true,
            IsRehire = false,
            IsDepotFulfiled = false,
            Multiple = 1
        };

        // Act
        var response = _sut.Reserve(request);

        // Assert
        _output.WriteLine($"Reserve response: IsSuccess={response.IsSuccess}, Error={response.ErrorMessage}");
        response.IsSuccess.Should().BeTrue($"Reserve failed with: {response.ErrorMessage}");
    }

    [Fact]
    public void Reserve_WhenDeletingReservationForDeletedAsset_Succeeds()
    {
        // Arrange
        var reservation = new Reservation { Id = 99, AssetId = "ASSET001", LineId = ValidLineId };
        var line = new Line
        {
            Id = ValidLineId,
            ItemNumber = ValidItem,
            Quantity = 1,
            AgreementLineNumber = "A1234358",
            HeaderId = 1,
            ActivationStatus = 0
        };
        var header = new Header { Id = 1, FulfilmentStatus = 0, AgreementNumber = "A1234567" };

        Mock.Get(_repo).Setup(x => x.GetReservation(99)).Returns(reservation);
        Mock.Get(_repo).Setup(x => x.DeleteReservation(99)).Returns(reservation);
        Mock.Get(_repo).Setup(x => x.GetLine(ValidLineId)).Returns(line);
        Mock.Get(_repo).Setup(x => x.GetHeaderForLineId(ValidLineId)).Returns(header);
        Mock.Get(_repo).Setup(x => x.GetReservationSumForLine(ValidLineId)).Returns(0);
        Mock.Get(_repo).Setup(x => x.GetHeader(1)).Returns(header);
        Mock.Get(_repo).Setup(x => x.GetNonServiceLines(1)).Returns(new List<Line> { line }.AsQueryable());

        var request = new ReserveRequest
        {
            IsDelete = true,
            ReservationId = 99,
            LineId = ValidLineId
        };

        // Act
        var response = _sut.Reserve(request);

        // Assert
        response.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Reserve_WhenRehireWithDeletedAsset_IsAllowed()
    {
        // Arrange - Rehire requests should not be blocked even if asset is deleted
        var line = new Line
        {
            Id = ValidLineId,
            ItemNumber = ValidItem,
            Quantity = 1,
            AgreementLineNumber = "A1234358",
            HeaderId = 1,
            ValidFromDate = DateTime.Now,
            ValidToDate = DateTime.Now.AddDays(30)
        };
        var header = new Header { Id = 1, FulfilmentStatus = 0, AgreementNumber = "A1234567" };

        Mock.Get(_repo).Setup(x => x.GetLine(ValidLineId)).Returns(line);
        Mock.Get(_repo).Setup(x => x.GetReservationSumForLine(ValidLineId)).Returns(0);
        Mock.Get(_repo).Setup(x => x.CreateReservation(It.IsAny<IUserIdentity>(), It.IsAny<Reservation>())).Returns(new Reservation());
        Mock.Get(_repo).Setup(x => x.GetHeaderForLineId(ValidLineId)).Returns(header);
        Mock.Get(_repo).Setup(x => x.GetHeader(1)).Returns(header);
        Mock.Get(_repo).Setup(x => x.GetNonServiceLines(1)).Returns(new List<Line> { line }.AsQueryable());
        Mock.Get(_repo).Setup(x => x.DeleteAlertsForLine(ValidLineId));

        var request = new ReserveRequest
        {
            IsDelete = false,
            AssetId = "REHIRE_ITEM",
            LineId = ValidLineId,
            ItemNumber = "REHIRE_ITEM",
            Warehouse = "WH1",
            IsSerialized = false,
            IsRehire = true,
            IsDepotFulfiled = false,
            Multiple = 1,
            Quantity = 1
        };

        // Act
        var response = _sut.Reserve(request);

        // Assert
        response.IsSuccess.Should().BeTrue();
    }
}
