using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using OF.Common.Infrastructure.OF;
using OF.Data.Database;
using OF.UI.Database;
using OF.UI.Identity;
using OF.WebApp.Controllers;
using AlternativeOption = OF.UI.Models.AlternativeOption;

namespace OF.Tests.WebApp.Controllers;

public class BulkActionsControllerTests
{
    private readonly Mock<IDataRepository> _repository = new();
    private readonly Mock<IUserIdentity> _identity = new();
    private readonly Mock<ICoreFulfilmentEngine> _engine = new();

    [Fact]
    public void DepotFulfil_ReturnsUnauthorizedWithoutReadingAgreementOrLines_WhenIdentityIsMissing()
    {
        _identity.Setup(identity => identity.GetIdentity()).Returns((User?)null!);

        var result = CreateSubject().DepotFulfil(CreateRequest());

        result.Should().BeOfType<UnauthorizedResult>();
        _repository.Verify(repository => repository.GetHeader(It.IsAny<int>()), Times.Never);
        VerifyNoBulkWork();
    }

    [Fact]
    public void Rehire_ReturnsUnauthorizedWithoutReadingAgreementOrLines_WhenIdentityIsMissing()
    {
        _identity.Setup(identity => identity.GetIdentity()).Returns((User?)null!);

        var result = CreateSubject().Rehire(CreateRequest());

        result.Should().BeOfType<UnauthorizedResult>();
        _repository.Verify(repository => repository.GetHeader(It.IsAny<int>()), Times.Never);
        VerifyNoBulkWork();
    }

    [Theory]
    [InlineData("depot", "UK", null)]
    [InlineData("depot", "UK", "FR")]
    [InlineData("depot", "", "UK")]
    [InlineData("rehire", "UK", null)]
    [InlineData("rehire", "UK", "FR")]
    [InlineData("rehire", "", "UK")]
    public void BulkActions_ReturnNotFoundWithoutDownstreamWork_WhenAgreementIsMissingOrInaccessible(
        string action,
        string callerDivisions,
        string? agreementDivision)
    {
        SetIdentity(callerDivisions);
        if (agreementDivision != null)
        {
            _repository.Setup(repository => repository.GetHeader(42)).Returns(CreateHeader(agreementDivision));
        }

        var result = Invoke(action, CreateRequest());

        result.Should().BeOfType<NotFoundResult>();
        _repository.Verify(repository => repository.GetHeader(42), Times.Once);
        VerifyNoBulkWork();
    }

    [Theory]
    [InlineData("depot", " uk, IE ", false, "UK")]
    [InlineData("rehire", " uk, IE ", false, "UK")]
    [InlineData("depot", null, true, null)]
    [InlineData("rehire", null, true, null)]
    public void BulkActions_AllowCaseInsensitiveAgreementAccessAndUnrestrictedSuperAdminAccess(
        string action,
        string? callerDivisions,
        bool isSuperAdmin,
        string? agreementDivision)
    {
        SetIdentity(callerDivisions, isSuperAdmin);
        _repository.Setup(repository => repository.GetHeader(42)).Returns(CreateHeader(agreementDivision));
        _repository.Setup(repository => repository.GetLines(42)).Returns(Array.Empty<Line>().AsQueryable());

        var result = Invoke(action, CreateRequest(lineIds: []));

        GetProcessed(result).Should().Be(0);
        _repository.Verify(repository => repository.GetLines(42), Times.Once);
        VerifyNoReservationSideEffects();
    }

    [Theory]
    [InlineData("depot")]
    [InlineData("rehire")]
    public void BulkActions_ProcessReturnedSelectedLineOnce_WhenIdsContainDuplicatesAndUnknownValues(string action)
    {
        SetIdentity();
        SetAccessibleAgreement();
        var selected = CreateLine(7, OF.Data.Database.FulfilmentStatus.Unfulfilled, "ED1");
        var notSelected = CreateLine(8, OF.Data.Database.FulfilmentStatus.Unfulfilled, "ED2");
        _repository.Setup(repository => repository.GetLines(42))
            .Returns(new[] { selected, notSelected }.AsQueryable());
        if (action == "rehire")
        {
            var generic = CreateGeneric(70, "GEN-7");
            _repository.Setup(repository => repository.GetGeneric(selected.ItemNumber!)).Returns(generic);
            _repository.Setup(repository => repository.GetAlternativeOptions(generic.GenericCode))
                .Returns([CreateAlternative("REHIRE-7")]);
        }
        _repository.Setup(repository => repository.CreateReservation(_identity.Object, It.IsAny<Reservation>()))
            .Returns<IUserIdentity, Reservation>((_, reservation) => reservation);

        var result = Invoke(action, CreateRequest(lineIds: [999, 7, 7]));

        GetProcessed(result).Should().Be(1);
        _repository.Verify(repository => repository.CreateReservation(
            _identity.Object,
            It.Is<Reservation>(reservation => reservation.LineId == selected.Id)), Times.Once);
        _engine.Verify(engine => engine.RecalculateStatusForLineAndHeader(selected, "planner@example.com"), Times.Once);
        _engine.Verify(engine => engine.RecalculateStatusForLineAndHeader(notSelected, It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public void DepotFulfil_ProcessesOnlySelectedUnfulfilledLines()
    {
        SetIdentity();
        SetAccessibleAgreement();
        var selected = CreateLine(7, OF.Data.Database.FulfilmentStatus.Unfulfilled, "ED1");
        selected.Quantity = 4;
        var alreadyFulfilled = CreateLine(8, OF.Data.Database.FulfilmentStatus.FullyFulfiled, "ED2");
        var notSelected = CreateLine(9, OF.Data.Database.FulfilmentStatus.Unfulfilled, "ED3");
        _repository.Setup(repository => repository.GetLines(42))
            .Returns(new[] { selected, alreadyFulfilled, notSelected }.AsQueryable());
        _repository.Setup(repository => repository.CreateReservation(_identity.Object, It.IsAny<Reservation>()))
            .Returns<IUserIdentity, Reservation>((_, reservation) => reservation);

        var result = CreateSubject().DepotFulfil(CreateRequest(
            lineIds: [7, 8],
            warehouse: "OVERRIDE",
            includeAlreadyFulfilled: false));

        GetProcessed(result).Should().Be(1);
        _repository.Verify(repository => repository.CreateReservation(
            _identity.Object,
            It.Is<Reservation>(reservation =>
                reservation.AssetId == "DEPOTFULFIL"
                && reservation.ItemNumber == "DEPOTFULFIL"
                && reservation.LineId == 7
                && reservation.Warehouse == "OVERRIDE"
                && reservation.Quantity == 4
                && reservation.EffectiveQuantity == 4
                && reservation.IsDepotFulfilled)), Times.Once);
        _repository.Verify(repository => repository.DeleteReservationsForLine(It.IsAny<int>()), Times.Never);
        _engine.Verify(engine => engine.RecalculateStatusForLineAndHeader(selected, "planner@example.com"), Times.Once);
        _engine.Verify(engine => engine.RecalculateStatusForLineAndHeader(alreadyFulfilled, It.IsAny<string?>()), Times.Never);
        _engine.Verify(engine => engine.RecalculateStatusForLineAndHeader(notSelected, It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public void DepotFulfil_DoesNotProcessPartiallyFulfilledLines()
    {
        SetIdentity();
        SetAccessibleAgreement();
        var line = CreateLine(7, OF.Data.Database.FulfilmentStatus.PartiallyFulfilled, "ED1");
        line.Quantity = 5;
        _repository.Setup(repository => repository.GetLines(42)).Returns(new[] { line }.AsQueryable());
        var result = CreateSubject().DepotFulfil(CreateRequest(lineIds: [line.Id]));

        GetProcessed(result).Should().Be(0);
        VerifyNoReservationSideEffects();
    }

    [Fact]
    public void DepotFulfil_DoesNotReplaceFulfilledLines_WhenReplacementIsRequested()
    {
        SetIdentity();
        SetAccessibleAgreement();
        var line = CreateLine(7, OF.Data.Database.FulfilmentStatus.FullyFulfiled, "ED1");
        line.Quantity = 6;
        _repository.Setup(repository => repository.GetLines(42)).Returns(new[] { line }.AsQueryable());

        var result = CreateSubject().DepotFulfil(CreateRequest(
            lineIds: [7],
            includeAlreadyFulfilled: true));

        GetProcessed(result).Should().Be(0);
        VerifyNoReservationSideEffects();
    }

    [Fact]
    public void Rehire_ResolvesFirstAlternativeBeforeDeletingAndRecalculating_WhenReplacementIsRequested()
    {
        SetIdentity();
        SetAccessibleAgreement();
        var line = CreateLine(7, OF.Data.Database.FulfilmentStatus.FullyFulfiled, "ED1");
        var specific = new CpqItem { ItemNumber = line.ItemNumber!, GenericId = 70 };
        var generic = CreateGeneric(70, "GEN-7");
        var calls = new List<string>();
        _repository.Setup(repository => repository.GetLines(42)).Returns(new[] { line }.AsQueryable());
        _repository.Setup(repository => repository.GetItem(line.ItemNumber!))
            .Callback(() => calls.Add("item"))
            .Returns(specific);
        _repository.Setup(repository => repository.GetGeneric(specific.GenericId))
            .Callback(() => calls.Add("generic"))
            .Returns(generic);
        _repository.Setup(repository => repository.GetAlternativeOptions(generic.GenericCode))
            .Callback(() => calls.Add("alternatives"))
            .Returns([CreateAlternative("REHIRE-1"), CreateAlternative("REHIRE-2")]);
        _repository.Setup(repository => repository.DeleteReservationsForLine(line.Id))
            .Callback(() => calls.Add("delete"));
        _repository.Setup(repository => repository.CreateReservation(_identity.Object, It.IsAny<Reservation>()))
            .Callback(() => calls.Add("create"))
            .Returns<IUserIdentity, Reservation>((_, reservation) => reservation);
        _engine.Setup(engine => engine.RecalculateStatusForLineAndHeader(line, "planner@example.com"))
            .Callback(() => calls.Add("recalculate"));

        var result = CreateSubject().Rehire(CreateRequest(
            lineIds: [line.Id],
            includeAlreadyFulfilled: true));

        GetProcessed(result).Should().Be(1);
        calls.Should().Equal("item", "generic", "alternatives", "delete", "create", "recalculate");
        _repository.Verify(repository => repository.CreateReservation(
            _identity.Object,
            It.Is<Reservation>(reservation =>
                reservation.AssetId == "REHIRE-1"
                && reservation.ItemNumber == "REHIRE-1"
                && reservation.LineId == line.Id
                && reservation.Warehouse == line.Warehouse
                && reservation.Quantity == 1
                && reservation.EffectiveQuantity == 1
                && reservation.IsRehire)), Times.Once);
    }

    [Fact]
    public void Rehire_SkipsMissingGenericAndAlternativesWithoutDeleting_ThenContinuesWithLaterLines()
    {
        SetIdentity();
        SetAccessibleAgreement();
        var missingGeneric = CreateLine(7, OF.Data.Database.FulfilmentStatus.Unfulfilled, "ED1");
        var missingAlternatives = CreateLine(8, OF.Data.Database.FulfilmentStatus.Unfulfilled, "ED2");
        var valid = CreateLine(9, OF.Data.Database.FulfilmentStatus.Unfulfilled, "ED3");
        var genericWithoutAlternatives = CreateGeneric(80, "GEN-8");
        var validGeneric = CreateGeneric(90, "GEN-9");
        _repository.Setup(repository => repository.GetLines(42))
            .Returns(new[] { missingGeneric, missingAlternatives, valid }.AsQueryable());
        _repository.Setup(repository => repository.GetItem(missingAlternatives.ItemNumber!))
            .Returns(new CpqItem { ItemNumber = missingAlternatives.ItemNumber!, GenericId = genericWithoutAlternatives.Id });
        _repository.Setup(repository => repository.GetGeneric(genericWithoutAlternatives.Id))
            .Returns(genericWithoutAlternatives);
        _repository.Setup(repository => repository.GetAlternativeOptions(genericWithoutAlternatives.GenericCode))
            .Returns([]);
        _repository.Setup(repository => repository.GetGeneric(valid.ItemNumber!)).Returns(validGeneric);
        _repository.Setup(repository => repository.GetAlternativeOptions(validGeneric.GenericCode))
            .Returns([CreateAlternative("REHIRE-9")]);
        _repository.Setup(repository => repository.CreateReservation(_identity.Object, It.IsAny<Reservation>()))
            .Returns<IUserIdentity, Reservation>((_, reservation) => reservation);

        var result = CreateSubject().Rehire(CreateRequest(
            lineIds: [missingGeneric.Id, missingAlternatives.Id, valid.Id],
            includeAlreadyFulfilled: true));

        GetProcessed(result).Should().Be(1);
        _repository.Verify(repository => repository.DeleteReservationsForLine(missingGeneric.Id), Times.Never);
        _repository.Verify(repository => repository.DeleteReservationsForLine(missingAlternatives.Id), Times.Never);
        _repository.Verify(repository => repository.DeleteReservationsForLine(valid.Id), Times.Once);
        _repository.Verify(repository => repository.CreateReservation(
            _identity.Object,
            It.Is<Reservation>(reservation => reservation.LineId == valid.Id && reservation.AssetId == "REHIRE-9")), Times.Once);
        _engine.Verify(engine => engine.RecalculateStatusForLineAndHeader(valid, "planner@example.com"), Times.Once);
    }

    private BulkActionsController CreateSubject() => new(_repository.Object, _identity.Object, _engine.Object);

    private IActionResult Invoke(string action, BulkActionRequest request) => action switch
    {
        "depot" => CreateSubject().DepotFulfil(request),
        "rehire" => CreateSubject().Rehire(request),
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown bulk action."),
    };

    private void SetIdentity(string? divisions = "UK", bool isSuperAdmin = false)
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

    private void SetAccessibleAgreement()
    {
        _repository.Setup(repository => repository.GetHeader(42)).Returns(CreateHeader("UK"));
    }

    private void VerifyNoBulkWork()
    {
        _repository.Verify(repository => repository.GetLines(It.IsAny<int>()), Times.Never);
        _repository.Verify(repository => repository.GetItem(It.IsAny<string>()), Times.Never);
        _repository.Verify(repository => repository.GetGeneric(It.IsAny<int>()), Times.Never);
        _repository.Verify(repository => repository.GetGeneric(It.IsAny<string>()), Times.Never);
        _repository.Verify(repository => repository.GetAlternativeOptions(It.IsAny<string>()), Times.Never);
        VerifyNoReservationSideEffects();
    }

    private void VerifyNoReservationSideEffects()
    {
        _repository.Verify(repository => repository.DeleteReservationsForLine(It.IsAny<int>()), Times.Never);
        _repository.Verify(repository => repository.CreateReservation(
            It.IsAny<IUserIdentity>(), It.IsAny<Reservation>()), Times.Never);
        _engine.Verify(engine => engine.RecalculateStatusForLineAndHeader(
            It.IsAny<Line>(), It.IsAny<string?>()), Times.Never);
    }

    private static BulkActionRequest CreateRequest(
        int[]? lineIds = null,
        string? warehouse = null,
        bool includeAlreadyFulfilled = false)
    {
        return new BulkActionRequest
        {
            HeaderId = 42,
            LineIds = lineIds ?? [7],
            Warehouse = warehouse,
            IncludeAlreadyFulfilled = includeAlreadyFulfilled,
        };
    }

    private static Header CreateHeader(string? division) => new()
    {
        Id = 42,
        Division = division!,
        OrderSource = "NOF",
        Facility = "FAC1",
    };

    private static Line CreateLine(int id, OF.Data.Database.FulfilmentStatus status, string warehouse)
    {
        return new Line
        {
            Id = id,
            HeaderId = 42,
            ItemNumber = $"ITEM-{id}",
            Quantity = 1,
            FulfilmentStatus = (int)status,
            Warehouse = warehouse,
            Division = "UK",
            Facility = "FAC1",
            OrderSource = "NOF",
        };
    }

    private static CpqGeneric CreateGeneric(int id, string code) => new()
    {
        Id = id,
        GenericCode = code,
    };

    private static AlternativeOption CreateAlternative(string itemNumber) => new()
    {
        ItemNumber = itemNumber,
        Description = itemNumber,
    };

    private static int GetProcessed(IActionResult result)
    {
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        var response = ok.Value.Should().BeOfType<BulkActionProcessedResponse>().Subject;
        JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .Should().Be($"{{\"processed\":{response.Processed}}}");
        return response.Processed;
    }
}
