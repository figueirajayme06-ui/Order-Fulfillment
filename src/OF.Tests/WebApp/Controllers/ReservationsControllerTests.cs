using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using OF.Common.Infrastructure.OF;
using OF.Data.Database;
using OF.UI.Database;
using OF.UI.Identity;
using OF.WebApp.Controllers;

namespace OF.Tests.WebApp.Controllers;

public class ReservationsControllerTests
{
    private readonly Mock<IDataRepository> _repository = new();
    private readonly Mock<IUserIdentity> _identity = new();
    private readonly Mock<ICoreFulfilmentEngine> _engine = new();

    [Fact]
    public void GetReservationsForHeader_ReturnsUnauthorizedWithoutReadingRepositories_WhenIdentityIsMissing()
    {
        SetMissingIdentity();

        var result = CreateSubject().GetReservationsForHeader(42);

        result.Should().BeOfType<UnauthorizedResult>();
        _repository.Verify(repository => repository.GetHeader(It.IsAny<int>()), Times.Never);
        _repository.Verify(repository => repository.GetReservationsForHeader(It.IsAny<int>()), Times.Never);
    }

    [Theory]
    [InlineData("UK", null)]
    [InlineData("UK", "FR")]
    [InlineData("", "UK")]
    public void GetReservationsForHeader_ReturnsNotFoundWithoutReadingReservations_WhenParentIsMissingOrInaccessible(
        string callerDivisions,
        string? parentDivision)
    {
        SetIdentity(callerDivisions);
        if (parentDivision != null)
        {
            _repository.Setup(repository => repository.GetHeader(42)).Returns(CreateHeader(parentDivision));
        }

        var result = CreateSubject().GetReservationsForHeader(42);

        result.Should().BeOfType<NotFoundResult>();
        _repository.Verify(repository => repository.GetReservationsForHeader(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void GetReservationsForHeader_ReturnsExactReservations_WhenDivisionMatchesIgnoringCaseAndWhitespace()
    {
        SetIdentity(" uk, IE ");
        var reservations = new[] { CreateReservation(id: 11), CreateReservation(id: 12) };
        _repository.Setup(repository => repository.GetHeader(42)).Returns(CreateHeader("UK"));
        _repository.Setup(repository => repository.GetReservationsForHeader(42)).Returns(reservations.AsQueryable());

        var result = CreateSubject().GetReservationsForHeader(42);

        var returned = GetOkReservations(result);
        returned.Should().HaveCount(2);
        AssertResponseMatches(returned[0], reservations[0]);
        AssertResponseMatches(returned[1], reservations[1]);
        _repository.Verify(repository => repository.GetAsset(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void GetReservationsForHeader_AllowsSuperAdminAcrossDivisions()
    {
        SetIdentity(null, isSuperAdmin: true);
        _repository.Setup(repository => repository.GetHeader(42)).Returns(CreateHeader("FR"));
        _repository.Setup(repository => repository.GetReservationsForHeader(42))
            .Returns(Array.Empty<Reservation>().AsQueryable());

        var result = CreateSubject().GetReservationsForHeader(42);

        GetOkReservations(result).Should().BeEmpty();
        _repository.Verify(repository => repository.GetReservationsForHeader(42), Times.Once);
        _repository.Verify(repository => repository.GetAsset(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void GetReservation_ReturnsUnauthorizedWithoutReadingRepositories_WhenIdentityIsMissing()
    {
        SetMissingIdentity();

        var result = CreateSubject().GetReservation(11);

        result.Should().BeOfType<UnauthorizedResult>();
        _repository.Verify(repository => repository.GetReservation(It.IsAny<int>()), Times.Never);
        _repository.Verify(repository => repository.GetHeaderForLineId(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void GetReservation_ReturnsNotFoundWithoutReadingParent_WhenReservationIsMissing()
    {
        SetIdentity("UK");
        _repository.Setup(repository => repository.GetReservation(11)).Returns((Reservation?)null!);

        var result = CreateSubject().GetReservation(11);

        result.Should().BeOfType<NotFoundResult>();
        _repository.Verify(repository => repository.GetHeaderForLineId(It.IsAny<int>()), Times.Never);
    }

    [Theory]
    [InlineData("UK", null)]
    [InlineData("UK", "FR")]
    [InlineData("", "UK")]
    public void GetReservation_ReturnsNotFound_WhenParentIsMissingOrInaccessible(
        string callerDivisions,
        string? parentDivision)
    {
        SetIdentity(callerDivisions);
        var reservation = CreateReservation();
        _repository.Setup(repository => repository.GetReservation(11)).Returns(reservation);
        if (parentDivision != null)
        {
            _repository.Setup(repository => repository.GetHeaderForLineId(reservation.LineId))
                .Returns(CreateHeader(parentDivision));
        }

        var result = CreateSubject().GetReservation(11);

        result.Should().BeOfType<NotFoundResult>();
        VerifyNoReservationMutations();
    }

    [Fact]
    public void GetReservation_ReturnsExactReservation_WhenDivisionMatchesIgnoringCaseAndWhitespace()
    {
        SetIdentity(" uk, IE ");
        var reservation = CreateReservation();
        _repository.Setup(repository => repository.GetReservation(11)).Returns(reservation);
        _repository.Setup(repository => repository.GetHeaderForLineId(reservation.LineId))
            .Returns(CreateHeader("UK"));

        var result = CreateSubject().GetReservation(11);

        AssertResponseMatches(GetOkReservation(result), reservation);
        _repository.Verify(repository => repository.GetAsset(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void GetReservation_AllowsSuperAdminAcrossDivisions()
    {
        SetIdentity(null, isSuperAdmin: true);
        var reservation = CreateReservation();
        _repository.Setup(repository => repository.GetReservation(11)).Returns(reservation);
        _repository.Setup(repository => repository.GetHeaderForLineId(reservation.LineId))
            .Returns(CreateHeader("FR"));

        var result = CreateSubject().GetReservation(11);

        AssertResponseMatches(GetOkReservation(result), reservation);
        _repository.Verify(repository => repository.GetAsset(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void GetReservation_SerializesFullyPopulatedCurrentWebJsonContract()
    {
        SetIdentity("UK");
        var reservation = CreateFullyPopulatedReservation();
        _repository.Setup(repository => repository.GetReservation(reservation.Id)).Returns(reservation);
        _repository.Setup(repository => repository.GetHeaderForLineId(reservation.LineId))
            .Returns(CreateHeader("UK"));

        var response = GetOkReservation(CreateSubject().GetReservation(reservation.Id));

        SerializeWeb(response).Should().Be(
            """{"isIndividualItem":true,"id":73,"assetId":"ASSET-7","notes":"Keep together","itemNumber":"ITEM-7","quantity":3,"warehouse":"ED1","lineId":7,"lastUpdatedBy":"creator@example.com","lastUpdatedDate":"2026-08-14T12:34:56Z","isDepotFulfilled":true,"isRehire":true,"effectiveQuantity":2.5,"isConfirmed":true,"actualAssetId":"ACTUAL-7","actualItemNumber":"ACTUAL-ITEM-7","actualQuantity":2.25}""");
    }

    [Fact]
    public void GetReservation_PreservesNullsInCurrentWebJsonContract()
    {
        SetIdentity("UK");
        var reservation = CreateNullReservation();
        _repository.Setup(repository => repository.GetReservation(reservation.Id)).Returns(reservation);
        _repository.Setup(repository => repository.GetHeaderForLineId(reservation.LineId))
            .Returns(CreateHeader("UK"));

        var response = GetOkReservation(CreateSubject().GetReservation(reservation.Id));

        SerializeWeb(response).Should().Be(
            """{"isIndividualItem":false,"id":74,"assetId":"ITEM-8","notes":null,"itemNumber":"ITEM-8","quantity":0,"warehouse":"ED2","lineId":8,"lastUpdatedBy":null,"lastUpdatedDate":null,"isDepotFulfilled":false,"isRehire":false,"effectiveQuantity":0,"isConfirmed":false,"actualAssetId":null,"actualItemNumber":null,"actualQuantity":null}""");
    }

    [Fact]
    public void CreateReservation_ReturnsUnauthorizedWithoutReadingRepositories_WhenIdentityIsMissing()
    {
        SetMissingIdentity();

        var result = CreateSubject().CreateReservation(CreateRequest());

        result.Should().BeOfType<UnauthorizedResult>();
        _repository.Verify(repository => repository.GetHeaderForLineId(It.IsAny<int>()), Times.Never);
        _repository.Verify(repository => repository.GetAsset(It.IsAny<string>()), Times.Never);
        _repository.Verify(repository => repository.CreateReservation(
            It.IsAny<IUserIdentity>(), It.IsAny<Reservation>()), Times.Never);
    }

    [Theory]
    [InlineData("UK", null)]
    [InlineData("UK", "FR")]
    [InlineData("", "UK")]
    public void CreateReservation_ReturnsNotFoundWithoutCreating_WhenParentIsMissingOrInaccessible(
        string callerDivisions,
        string? parentDivision)
    {
        SetIdentity(callerDivisions);
        var request = CreateRequest();
        if (parentDivision != null)
        {
            _repository.Setup(repository => repository.GetHeaderForLineId(request.LineId))
                .Returns(CreateHeader(parentDivision));
        }

        var result = CreateSubject().CreateReservation(request);

        result.Should().BeOfType<NotFoundResult>();
        _repository.Verify(repository => repository.GetAsset(It.IsAny<string>()), Times.Never);
        _repository.Verify(repository => repository.CreateReservation(
            It.IsAny<IUserIdentity>(), It.IsAny<Reservation>()), Times.Never);
    }

    [Fact]
    public void CreateReservation_MapsExactRequestAndReturnsCreatedAtGetReservation_WhenAgreementAndAssetAreAccessible()
    {
        SetIdentity(" uk, IE ");
        var request = CreateRequest();
        var line = CreateLine(request.LineId);
        Reservation? passedReservation = null;
        var created = CreateReservation(id: 73, lineId: request.LineId);
        _repository.Setup(repository => repository.GetHeaderForLineId(request.LineId)).Returns(CreateHeader("UK"));
        _repository.Setup(repository => repository.GetAsset(request.AssetId))
            .Returns(CreateAsset(request.AssetId, "ie"));
        _repository.Setup(repository => repository.GetLine(request.LineId)).Returns(line);
        _repository.Setup(repository => repository.CreateReservation(_identity.Object, It.IsAny<Reservation>()))
            .Callback<IUserIdentity, Reservation>((_, reservation) => passedReservation = reservation)
            .Returns(created);

        var result = CreateSubject().CreateReservation(request);

        passedReservation.Should().BeEquivalentTo(new Reservation
        {
            AssetId = "ASSET-7",
            LineId = 7,
            ItemNumber = "ITEM-7",
            Quantity = 3,
            EffectiveQuantity = 3,
            Warehouse = "ED1",
            Notes = "Keep together",
            IsConfirmed = true,
            IsDepotFulfilled = true,
            IsRehire = true,
        });
        AssertResponseMatches(GetCreatedReservation(result, 73), created);
        _repository.Verify(repository => repository.GetAsset(request.AssetId), Times.Once);
        _engine.Verify(engine => engine.RecalculateStatusForLineAndHeader(
            line,
            "planner@example.com"), Times.Once);
    }

    [Fact]
    public void CreateReservation_AllowsSuperAdminWhenIndividualAssetExistsRegardlessOfDivision()
    {
        SetIdentity(null, isSuperAdmin: true);
        var request = CreateRequest();
        var line = CreateLine(request.LineId);
        var created = CreateReservation(id: 73, lineId: request.LineId);
        _repository.Setup(repository => repository.GetHeaderForLineId(request.LineId)).Returns(CreateHeader("FR"));
        _repository.Setup(repository => repository.GetAsset(request.AssetId))
            .Returns(CreateAsset(request.AssetId, null));
        _repository.Setup(repository => repository.GetLine(request.LineId)).Returns(line);
        _repository.Setup(repository => repository.CreateReservation(_identity.Object, It.IsAny<Reservation>()))
            .Returns(created);

        var result = CreateSubject().CreateReservation(request);

        AssertResponseMatches(GetCreatedReservation(result, 73), created);
        _repository.Verify(repository => repository.GetAsset(request.AssetId), Times.Once);
        _repository.Verify(repository => repository.CreateReservation(
            _identity.Object, It.IsAny<Reservation>()), Times.Once);
        _engine.Verify(engine => engine.RecalculateStatusForLineAndHeader(
            line,
            "planner@example.com"), Times.Once);
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(true, "FR")]
    [InlineData(true, null)]
    public void CreateReservation_ReturnsNotFoundWithoutCreating_WhenIndividualAssetIsMissingOrInaccessible(
        bool assetExists,
        string? assetDivision)
    {
        SetIdentity("UK");
        var request = CreateRequest();
        _repository.Setup(repository => repository.GetHeaderForLineId(request.LineId)).Returns(CreateHeader("UK"));
        if (assetExists)
        {
            _repository.Setup(repository => repository.GetAsset(request.AssetId))
                .Returns(CreateAsset(request.AssetId, assetDivision));
        }

        var result = CreateSubject().CreateReservation(request);

        result.Should().BeOfType<NotFoundResult>();
        _repository.Verify(repository => repository.GetAsset(request.AssetId), Times.Once);
        _repository.Verify(repository => repository.CreateReservation(
            It.IsAny<IUserIdentity>(), It.IsAny<Reservation>()), Times.Never);
    }

    [Fact]
    public void CreateReservation_ReturnsNotFoundWithoutCreating_WhenSuperAdminIndividualAssetIsMissing()
    {
        SetIdentity(null, isSuperAdmin: true);
        var request = CreateRequest();
        _repository.Setup(repository => repository.GetHeaderForLineId(request.LineId)).Returns(CreateHeader("FR"));

        var result = CreateSubject().CreateReservation(request);

        result.Should().BeOfType<NotFoundResult>();
        _repository.Verify(repository => repository.GetAsset(request.AssetId), Times.Once);
        _repository.Verify(repository => repository.CreateReservation(
            It.IsAny<IUserIdentity>(), It.IsAny<Reservation>()), Times.Never);
    }

    [Theory]
    [InlineData("ITEM-7", false, false)]
    [InlineData("DEPOTFULFIL", true, false)]
    [InlineData("REHIRE-7", false, true)]
    public void CreateReservation_AllowsMissingLegacyItemNumberReservation_AfterCheckingForAnAsset(
        string itemNumber,
        bool isDepotFulfilled,
        bool isRehire)
    {
        SetIdentity("UK");
        var request = CreateRequest() with
        {
            AssetId = itemNumber,
            ItemNumber = itemNumber,
            IsDepotFulfilled = isDepotFulfilled,
            IsRehire = isRehire,
        };
        var line = CreateLine(request.LineId);
        var created = CreateReservation(id: 73, lineId: request.LineId);
        _repository.Setup(repository => repository.GetHeaderForLineId(request.LineId)).Returns(CreateHeader("UK"));
        _repository.Setup(repository => repository.GetLine(request.LineId)).Returns(line);
        _repository.Setup(repository => repository.CreateReservation(_identity.Object, It.IsAny<Reservation>()))
            .Returns(created);

        var result = CreateSubject().CreateReservation(request);

        AssertResponseMatches(GetCreatedReservation(result, 73), created);
        _repository.Verify(repository => repository.GetAsset(itemNumber), Times.Once);
        _repository.Verify(repository => repository.CreateReservation(
            _identity.Object, It.IsAny<Reservation>()), Times.Once);
        _engine.Verify(engine => engine.RecalculateStatusForLineAndHeader(
            line,
            "planner@example.com"), Times.Once);
    }

    [Theory]
    [InlineData("ITEM-7", false, false)]
    [InlineData("DEPOTFULFIL", true, false)]
    [InlineData("REHIRE-7", false, true)]
    public void CreateReservation_AllowsEqualIdentifiers_WhenExistingAssetIsAccessible(
        string itemNumber,
        bool isDepotFulfilled,
        bool isRehire)
    {
        SetIdentity(" uk, IE ");
        var request = CreateRequest() with
        {
            AssetId = itemNumber,
            ItemNumber = itemNumber,
            IsDepotFulfilled = isDepotFulfilled,
            IsRehire = isRehire,
        };
        var line = CreateLine(request.LineId);
        var created = CreateReservation(id: 73, lineId: request.LineId);
        _repository.Setup(repository => repository.GetHeaderForLineId(request.LineId)).Returns(CreateHeader("UK"));
        _repository.Setup(repository => repository.GetAsset(itemNumber)).Returns(CreateAsset(itemNumber, "ie"));
        _repository.Setup(repository => repository.GetLine(request.LineId)).Returns(line);
        _repository.Setup(repository => repository.CreateReservation(_identity.Object, It.IsAny<Reservation>()))
            .Returns(created);

        var result = CreateSubject().CreateReservation(request);

        AssertResponseMatches(GetCreatedReservation(result, 73), created);
        _repository.Verify(repository => repository.GetAsset(itemNumber), Times.Once);
        _repository.Verify(repository => repository.CreateReservation(
            _identity.Object, It.IsAny<Reservation>()), Times.Once);
        _engine.Verify(engine => engine.RecalculateStatusForLineAndHeader(
            line,
            "planner@example.com"), Times.Once);
    }

    [Theory]
    [InlineData("ITEM-7", false, false)]
    [InlineData("DEPOTFULFIL", true, false)]
    [InlineData("REHIRE-7", false, true)]
    public void CreateReservation_ReturnsNotFound_WhenExistingEqualIdentifierAssetIsOutOfScopeRegardlessOfFlags(
        string itemNumber,
        bool isDepotFulfilled,
        bool isRehire)
    {
        SetIdentity("UK");
        var request = CreateRequest() with
        {
            AssetId = itemNumber,
            ItemNumber = itemNumber,
            IsDepotFulfilled = isDepotFulfilled,
            IsRehire = isRehire,
        };
        _repository.Setup(repository => repository.GetHeaderForLineId(request.LineId)).Returns(CreateHeader("UK"));
        _repository.Setup(repository => repository.GetAsset(itemNumber)).Returns(CreateAsset(itemNumber, "FR"));

        var result = CreateSubject().CreateReservation(request);

        result.Should().BeOfType<NotFoundResult>();
        _repository.Verify(repository => repository.GetAsset(itemNumber), Times.Once);
        _repository.Verify(repository => repository.CreateReservation(
            It.IsAny<IUserIdentity>(), It.IsAny<Reservation>()), Times.Never);
    }

    [Fact]
    public void CreateReservation_ReturnsNotFoundWithoutCreating_WhenAffectedLineCannotBeLoaded()
    {
        SetIdentity("UK");
        var request = CreateRequest();
        _repository.Setup(repository => repository.GetHeaderForLineId(request.LineId)).Returns(CreateHeader("UK"));
        _repository.Setup(repository => repository.GetAsset(request.AssetId)).Returns(CreateAsset(request.AssetId, "UK"));

        var result = CreateSubject().CreateReservation(request);

        result.Should().BeOfType<NotFoundResult>();
        _repository.Verify(repository => repository.GetLine(request.LineId), Times.Once);
        _repository.Verify(repository => repository.CreateReservation(
            It.IsAny<IUserIdentity>(), It.IsAny<Reservation>()), Times.Never);
        _engine.Verify(engine => engine.RecalculateStatusForLineAndHeader(
            It.IsAny<Line>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public void DeleteReservation_ReturnsUnauthorizedWithoutReadingRepositories_WhenIdentityIsMissing()
    {
        SetMissingIdentity();

        var result = CreateSubject().DeleteReservation(11);

        result.Should().BeOfType<UnauthorizedResult>();
        _repository.Verify(repository => repository.GetReservation(It.IsAny<int>()), Times.Never);
        _repository.Verify(repository => repository.GetHeaderForLineId(It.IsAny<int>()), Times.Never);
        _repository.Verify(repository => repository.DeleteReservation(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void DeleteReservation_ReturnsNotFoundWithoutReadingParentOrDeleting_WhenReservationIsMissing()
    {
        SetIdentity("UK");
        _repository.Setup(repository => repository.GetReservation(11)).Returns((Reservation?)null!);

        var result = CreateSubject().DeleteReservation(11);

        result.Should().BeOfType<NotFoundResult>();
        _repository.Verify(repository => repository.GetHeaderForLineId(It.IsAny<int>()), Times.Never);
        _repository.Verify(repository => repository.DeleteReservation(It.IsAny<int>()), Times.Never);
    }

    [Theory]
    [InlineData("UK", null)]
    [InlineData("UK", "FR")]
    [InlineData("", "UK")]
    public void DeleteReservation_ReturnsNotFoundWithoutDeleting_WhenParentIsMissingOrInaccessible(
        string callerDivisions,
        string? parentDivision)
    {
        SetIdentity(callerDivisions);
        var reservation = CreateReservation();
        _repository.Setup(repository => repository.GetReservation(11)).Returns(reservation);
        if (parentDivision != null)
        {
            _repository.Setup(repository => repository.GetHeaderForLineId(reservation.LineId))
                .Returns(CreateHeader(parentDivision));
        }

        var result = CreateSubject().DeleteReservation(11);

        result.Should().BeOfType<NotFoundResult>();
        _repository.Verify(repository => repository.DeleteReservation(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void DeleteReservation_ReturnsNotFoundWithoutDeleting_WhenAffectedLineCannotBeLoaded()
    {
        SetIdentity("UK");
        var reservation = CreateReservation();
        _repository.Setup(repository => repository.GetReservation(reservation.Id)).Returns(reservation);
        _repository.Setup(repository => repository.GetHeaderForLineId(reservation.LineId))
            .Returns(CreateHeader("UK"));

        var result = CreateSubject().DeleteReservation(reservation.Id);

        result.Should().BeOfType<NotFoundResult>();
        _repository.Verify(repository => repository.GetLine(reservation.LineId), Times.Once);
        _repository.Verify(repository => repository.DeleteReservation(It.IsAny<int>()), Times.Never);
        _engine.Verify(engine => engine.RecalculateStatusForLineAndHeader(
            It.IsAny<Line>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public void DeleteReservation_AllowsSharedAgreementUserRegardlessOfReservationLastUpdatedBy()
    {
        SetIdentity(" uk, IE ");
        var reservation = CreateReservation(lastUpdatedBy: "another.user@example.com");
        var line = CreateLine(reservation.LineId);
        _repository.Setup(repository => repository.GetReservation(11)).Returns(reservation);
        _repository.Setup(repository => repository.GetHeaderForLineId(reservation.LineId))
            .Returns(CreateHeader("UK"));
        _repository.Setup(repository => repository.GetLine(reservation.LineId)).Returns(line);
        _repository.Setup(repository => repository.DeleteReservation(11)).Returns(reservation);

        var result = CreateSubject().DeleteReservation(11);

        result.Should().BeOfType<NoContentResult>();
        _repository.Verify(repository => repository.GetAsset(It.IsAny<string>()), Times.Never);
        _repository.Verify(repository => repository.DeleteReservation(11), Times.Once);
        _engine.Verify(engine => engine.RecalculateStatusForLineAndHeader(
            line,
            "planner@example.com"), Times.Once);
    }

    [Fact]
    public void DeleteReservation_AllowsSuperAdminAcrossDivisions()
    {
        SetIdentity(null, isSuperAdmin: true);
        var reservation = CreateReservation();
        var line = CreateLine(reservation.LineId);
        _repository.Setup(repository => repository.GetReservation(11)).Returns(reservation);
        _repository.Setup(repository => repository.GetHeaderForLineId(reservation.LineId))
            .Returns(CreateHeader("FR"));
        _repository.Setup(repository => repository.GetLine(reservation.LineId)).Returns(line);
        _repository.Setup(repository => repository.DeleteReservation(11)).Returns(reservation);

        var result = CreateSubject().DeleteReservation(11);

        result.Should().BeOfType<NoContentResult>();
        _repository.Verify(repository => repository.GetAsset(It.IsAny<string>()), Times.Never);
        _repository.Verify(repository => repository.DeleteReservation(11), Times.Once);
        _engine.Verify(engine => engine.RecalculateStatusForLineAndHeader(
            line,
            "planner@example.com"), Times.Once);
    }

    [Fact]
    public void DeleteReservation_ReturnsNotFound_WhenAuthorizedReservationDisappearsBeforeDelete()
    {
        SetIdentity("UK");
        var reservation = CreateReservation();
        var line = CreateLine(reservation.LineId);
        _repository.Setup(repository => repository.GetReservation(11)).Returns(reservation);
        _repository.Setup(repository => repository.GetHeaderForLineId(reservation.LineId))
            .Returns(CreateHeader("UK"));
        _repository.Setup(repository => repository.GetLine(reservation.LineId)).Returns(line);
        _repository.Setup(repository => repository.DeleteReservation(11)).Returns((Reservation?)null);

        var result = CreateSubject().DeleteReservation(11);

        result.Should().BeOfType<NotFoundResult>();
        _repository.Verify(repository => repository.GetAsset(It.IsAny<string>()), Times.Never);
        _repository.Verify(repository => repository.DeleteReservation(11), Times.Once);
        _engine.Verify(engine => engine.RecalculateStatusForLineAndHeader(
            It.IsAny<Line>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public void ReservationLifecycle_RecalculatesLineAndHeaderAfterEachPersistedMutation()
    {
        SetIdentity("UK");
        var header = CreateHeader("UK");
        header.FulfilmentStatus = (int)OF.Data.Database.FulfilmentStatus.Unfulfilled;
        var line = CreateLine(7);
        line.AgreementLineNumber = "A731595-1";
        line.Quantity = 1;
        line.FulfilmentStatus = (int)OF.Data.Database.FulfilmentStatus.Unfulfilled;
        var request = CreateRequest() with { Quantity = 1 };
        Reservation? persistedReservation = null;
        double reservationTotal = 0;

        _repository.Setup(repository => repository.GetHeaderForLineId(line.Id)).Returns(header);
        _repository.Setup(repository => repository.GetAsset(request.AssetId))
            .Returns(CreateAsset(request.AssetId, "UK"));
        _repository.Setup(repository => repository.GetLine(line.Id)).Returns(line);
        _repository.Setup(repository => repository.CreateReservation(_identity.Object, It.IsAny<Reservation>()))
            .Callback<IUserIdentity, Reservation>((_, reservation) =>
            {
                persistedReservation = reservation;
                reservationTotal = reservation.Quantity;
            })
            .Returns<IUserIdentity, Reservation>((_, reservation) =>
            {
                reservation.Id = 73;
                return reservation;
            });
        _repository.Setup(repository => repository.GetReservationSumForLine(line.Id))
            .Returns(() => reservationTotal);
        _repository.Setup(repository => repository.UpdateLine(line, "planner@example.com")).Returns(line);
        _repository.Setup(repository => repository.GetNonServiceLines(header.Id))
            .Returns(new[] { line }.AsQueryable());
        _repository.Setup(repository => repository.GetHeader(header.Id)).Returns(header);
        _repository.Setup(repository => repository.UpdateHeader(header, "planner@example.com")).Returns(header);
        _repository.Setup(repository => repository.GetReservation(73))
            .Returns(() => persistedReservation!);
        _repository.Setup(repository => repository.DeleteReservation(73))
            .Callback(() => reservationTotal = 0)
            .Returns(() => persistedReservation!);
        var subject = new ReservationsController(
            _repository.Object,
            _identity.Object,
            new CoreFulfilmentEngine(_repository.Object));

        var createResult = subject.CreateReservation(request);

        GetCreatedReservation(createResult, 73);
        line.QuantityFulfilled.Should().Be(1);
        line.FulfilmentStatus.Should().Be((int)OF.Data.Database.FulfilmentStatus.FullyFulfiled);
        header.FulfilmentStatus.Should().Be((int)OF.Data.Database.FulfilmentStatus.FullyFulfiled);

        var deleteResult = subject.DeleteReservation(73);

        deleteResult.Should().BeOfType<NoContentResult>();
        line.QuantityFulfilled.Should().Be(0);
        line.FulfilmentStatus.Should().Be((int)OF.Data.Database.FulfilmentStatus.Unfulfilled);
        header.FulfilmentStatus.Should().Be((int)OF.Data.Database.FulfilmentStatus.Unfulfilled);
    }

    private ReservationsController CreateSubject() => new(
        _repository.Object,
        _identity.Object,
        _engine.Object);

    private static IReadOnlyList<ReservationResponse> GetOkReservations(IActionResult result)
    {
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        return ok.Value.Should().BeOfType<List<ReservationResponse>>().Subject;
    }

    private static ReservationResponse GetOkReservation(IActionResult result)
    {
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        return ok.Value.Should().BeOfType<ReservationResponse>().Subject;
    }

    private static ReservationResponse GetCreatedReservation(IActionResult result, int expectedId)
    {
        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.StatusCode.Should().Be(201);
        created.ActionName.Should().Be(nameof(ReservationsController.GetReservation));
        created.RouteValues.Should().ContainKey("id").WhoseValue.Should().Be(expectedId);
        return created.Value.Should().BeOfType<ReservationResponse>().Subject;
    }

    private static void AssertResponseMatches(ReservationResponse actual, Reservation expected)
    {
        actual.Id.Should().Be(expected.Id);
        actual.AssetId.Should().Be(expected.AssetId);
        actual.Notes.Should().Be(expected.Notes);
        actual.ItemNumber.Should().Be(expected.ItemNumber);
        actual.Quantity.Should().Be(expected.Quantity);
        actual.Warehouse.Should().Be(expected.Warehouse);
        actual.LineId.Should().Be(expected.LineId);
        actual.LastUpdatedBy.Should().Be(expected.LastUpdatedBy);
        actual.LastUpdatedDate.Should().Be(expected.LastUpdatedDate);
        actual.IsDepotFulfilled.Should().Be(expected.IsDepotFulfilled);
        actual.IsRehire.Should().Be(expected.IsRehire);
        actual.EffectiveQuantity.Should().Be(expected.EffectiveQuantity);
        actual.IsConfirmed.Should().Be(expected.IsConfirmed);
        actual.ActualAssetId.Should().Be(expected.ActualAssetId);
        actual.ActualItemNumber.Should().Be(expected.ActualItemNumber);
        actual.ActualQuantity.Should().Be(expected.ActualQuantity);
        actual.IsIndividualItem.Should().Be(expected.IsIndividualItem);
    }

    private static string SerializeWeb(ReservationResponse response) => JsonSerializer.Serialize(
        response,
        new JsonSerializerOptions(JsonSerializerDefaults.Web));

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
    }

    private void VerifyNoReservationMutations()
    {
        _repository.Verify(repository => repository.CreateReservation(
            It.IsAny<IUserIdentity>(), It.IsAny<Reservation>()), Times.Never);
        _repository.Verify(repository => repository.DeleteReservation(It.IsAny<int>()), Times.Never);
        _engine.Verify(engine => engine.RecalculateStatusForLineAndHeader(
            It.IsAny<Line>(), It.IsAny<string?>()), Times.Never);
    }

    private static Header CreateHeader(string division) => new()
    {
        Id = 42,
        Division = division,
        OrderSource = "NOF",
        Facility = "FAC1",
    };

    private static Asset CreateAsset(string id, string? division) => new()
    {
        Id = id,
        IndividualItemNumber = id,
        Division = division,
    };

    private static Line CreateLine(int id) => new()
    {
        Id = id,
        HeaderId = 42,
        Quantity = 3,
        RequiresFulfilment = true,
    };

    private static Reservation CreateReservation(
        int id = 11,
        int lineId = 7,
        string lastUpdatedBy = "creator@example.com") => new()
    {
        Id = id,
        AssetId = "ASSET-7",
        LineId = lineId,
        ItemNumber = "ITEM-7",
        Quantity = 3,
        Warehouse = "ED1",
        LastUpdatedBy = lastUpdatedBy,
    };

    private static Reservation CreateFullyPopulatedReservation() => new()
    {
        Id = 73,
        AssetId = "ASSET-7",
        Notes = "Keep together",
        ItemNumber = "ITEM-7",
        Quantity = 3,
        Warehouse = "ED1",
        LineId = 7,
        LastUpdatedBy = "creator@example.com",
        LastUpdatedDate = new DateTime(2026, 8, 14, 12, 34, 56, DateTimeKind.Utc),
        IsDepotFulfilled = true,
        IsRehire = true,
        EffectiveQuantity = 2.5,
        IsConfirmed = true,
        ActualAssetId = "ACTUAL-7",
        ActualItemNumber = "ACTUAL-ITEM-7",
        ActualQuantity = 2.25,
    };

    private static Reservation CreateNullReservation() => new()
    {
        Id = 74,
        AssetId = "ITEM-8",
        Notes = null,
        ItemNumber = "ITEM-8",
        Quantity = 0,
        Warehouse = "ED2",
        LineId = 8,
        LastUpdatedBy = null,
        LastUpdatedDate = null,
        IsDepotFulfilled = false,
        IsRehire = false,
        EffectiveQuantity = 0,
        IsConfirmed = false,
        ActualAssetId = null,
        ActualItemNumber = null,
        ActualQuantity = null,
    };

    private static CreateReservationRequest CreateRequest() => new()
    {
        AssetId = "ASSET-7",
        LineId = 7,
        ItemNumber = "ITEM-7",
        Quantity = 3,
        Warehouse = "ED1",
        Notes = "Keep together",
        IsConfirmed = true,
        IsDepotFulfilled = true,
        IsRehire = true,
    };
}
