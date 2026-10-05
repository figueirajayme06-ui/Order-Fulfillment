using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using OF.Data.Database;
using OF.UI.Database;
using OF.UI.Identity;
using OF.WebApp.Controllers;
using static OF.Common.Enums;

namespace OF.Tests.WebApp.Controllers;

public class AgreementEquipmentControllerTests
{
    private readonly Mock<IDataRepository> _repository = new();
    private readonly Mock<IAgreementEquipmentRepository> _equipmentRepository = new();
    private readonly Mock<IUserIdentity> _identity = new();

    [Theory]
    [InlineData("catalog")]
    [InlineData("generic")]
    [InlineData("create")]
    [InlineData("delete")]
    public void Endpoints_ReturnUnauthorizedBeforeReadingAgreement_WhenIdentityIsMissing(string action)
    {
        _identity.Setup(identity => identity.GetIdentity()).Returns((User?)null!);

        var result = Invoke(action);

        result.Should().BeOfType<UnauthorizedResult>();
        _repository.Verify(repository => repository.GetHeader(It.IsAny<int>()), Times.Never);
        VerifyNoEquipmentCalls();
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(true, "FR")]
    [InlineData(true, "")]
    public void Endpoints_ReturnNotFound_WhenAgreementIsMissingOrOutsideCallerDivision(
        bool headerExists,
        string? headerDivision)
    {
        SetIdentity("UK");
        if (headerExists)
        {
            _repository.Setup(repository => repository.GetHeader(42))
                .Returns(CreateHeader("T100", (int)ActivationStatus.TODO, headerDivision!));
        }

        var result = CreateSubject().GetCatalog(42);

        result.Should().BeOfType<NotFoundResult>();
        VerifyNoEquipmentCalls();
    }

    [Theory]
    [InlineData("Q100", 0, false)]
    [InlineData("T100", 1, false)]
    [InlineData("T100", 2, false)]
    [InlineData("T100", 3, false)]
    [InlineData("A100", 0, false)]
    [InlineData("A100", 1, false)]
    [InlineData("A100", 2, false)]
    [InlineData("T100", 0, true)]
    public void Catalog_ReturnsConflict_ForQuoteUnstableOrDeletedAgreement(
        string agreementNumber,
        int activationStatus,
        bool isDeleted)
    {
        SetAccessibleHeader(agreementNumber, activationStatus, isDeleted);

        var result = CreateSubject().GetCatalog(42);

        GetValidationProblem(result, StatusCodes.Status409Conflict)
            .Errors.Should().ContainKey("agreement");
        _equipmentRepository.Verify(repository => repository.GetCatalog(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData("T100", 0)]
    [InlineData("A100", 3)]
    public void Catalog_ReturnsAvailableProductLinesAndGenerics_ForStableAgreement(
        string agreementNumber,
        int activationStatus)
    {
        SetAccessibleHeader(agreementNumber, activationStatus);
        _equipmentRepository.Setup(repository => repository.GetCatalog("UK"))
            .Returns(new AgreementEquipmentCatalogData(
                [new AgreementEquipmentProductLineData(10, "Generators", "Power")],
                [new AgreementEquipmentGenericData(20, 10, "GEN60", "Generator 60")]));

        var result = CreateSubject().GetCatalog(42);

        var response = result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<AgreementEquipmentCatalogResponse>().Subject;
        response.ProductLines.Should().ContainSingle().Which.FamilyDescription.Should().Be("Power");
        response.Generics.Should().ContainSingle().Which.Code.Should().Be("GEN60");
    }

    [Fact]
    public void GenericCatalog_AcceptsSemicolonJoinedAttributes_AndReturnsAttributesShape()
    {
        SetAccessibleHeader("T100", (int)ActivationStatus.TODO);
        _equipmentRepository.Setup(repository => repository.GetGenericCatalog(
                "UK",
                20,
                It.Is<IReadOnlyCollection<string>>(attributes =>
                    attributes.SequenceEqual(new[] { "Voltage:240V", "Phase:1" }))))
            .Returns(AgreementEquipmentGenericCatalogResult.Success(
                new AgreementEquipmentGenericData(20, 10, "GEN60", "Generator 60"),
                [new AgreementEquipmentAttributeGroupData("Voltage", ["240V", "415V"])],
                [new AgreementEquipmentItemData("GEN60-240", "Generator 60 240V", 20)]));

        var result = CreateSubject().GetGenericCatalog(
            42,
            20,
            ["Voltage:240V;Phase:1"]);

        var response = result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<AgreementEquipmentGenericCatalogResponse>().Subject;
        response.Attributes.Should().ContainSingle().Which.Name.Should().Be("Voltage");
        response.Items.Should().ContainSingle().Which.ItemNumber.Should().Be("GEN60-240");
    }

    [Fact]
    public void GenericCatalog_TreatsEmptyQueryValueAsNoAttributeFilter()
    {
        SetAccessibleHeader("T100", (int)ActivationStatus.TODO);
        _equipmentRepository.Setup(repository => repository.GetGenericCatalog(
                "UK",
                20,
                It.Is<IReadOnlyCollection<string>>(attributes => attributes.Count == 0)))
            .Returns(AgreementEquipmentGenericCatalogResult.Success(
                new AgreementEquipmentGenericData(20, 10, "GEN60", "Generator 60"),
                [],
                []));

        var result = CreateSubject().GetGenericCatalog(42, 20, [null!]);

        result.Should().BeOfType<OkObjectResult>();
        _equipmentRepository.VerifyAll();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    public void Create_RejectsInvalidQuantityBeforeMutation(int quantity)
    {
        SetAccessibleHeader("T100", (int)ActivationStatus.TODO);

        var result = CreateSubject().Create(42, CreateRequest() with { Quantity = quantity });

        GetValidationProblem(result).Errors.Should().ContainKey("quantity");
        _equipmentRepository.Verify(repository => repository.Create(
            It.IsAny<IUserIdentity>(),
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<AgreementEquipmentCreateCommand>()), Times.Never);
    }

    [Theory]
    [InlineData(AgreementEquipmentFailure.InvalidAttributes, "attributes")]
    [InlineData(AgreementEquipmentFailure.InvalidItem, "itemNumber")]
    public void Create_ReturnsFieldProblem_ForInvalidCatalogOrParentInput(
        AgreementEquipmentFailure failure,
        string expectedField)
    {
        SetAccessibleHeader("A100", (int)ActivationStatus.Activated);
        _equipmentRepository.Setup(repository => repository.Create(
                _identity.Object,
                42,
                "UK",
                It.IsAny<AgreementEquipmentCreateCommand>()))
            .Returns(new AgreementEquipmentCreateResult(failure, "invalid selection", null, 0));

        var result = CreateSubject().Create(42, CreateRequest());

        GetValidationProblem(result).Errors.Should().ContainKey(expectedField);
    }

    [Theory]
    [InlineData(AgreementEquipmentFailure.ParentNotFound)]
    [InlineData(AgreementEquipmentFailure.InvalidGeneric)]
    public void Create_ReturnsNotFound_ForForeignParentOrUnavailableGeneric(AgreementEquipmentFailure failure)
    {
        SetAccessibleHeader("A100", (int)ActivationStatus.Activated);
        _equipmentRepository.Setup(repository => repository.Create(
                _identity.Object,
                42,
                "UK",
                It.IsAny<AgreementEquipmentCreateCommand>()))
            .Returns(new AgreementEquipmentCreateResult(failure, null, null, 0));

        var result = CreateSubject().Create(42, CreateRequest());

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void Create_ReturnsConflict_WhenParentIsNoLongerEligible()
    {
        SetAccessibleHeader("A100", (int)ActivationStatus.Activated);
        _equipmentRepository.Setup(repository => repository.Create(
                _identity.Object,
                42,
                "UK",
                It.IsAny<AgreementEquipmentCreateCommand>()))
            .Returns(AgreementEquipmentCreateResult.ParentNotEligible());

        var result = CreateSubject().Create(42, CreateRequest());

        GetValidationProblem(result, StatusCodes.Status409Conflict)
            .Errors.Should().ContainKey("parentLineId");
    }

    [Fact]
    public void Create_ReturnsCreatedLineSummary()
    {
        SetAccessibleHeader("A100", (int)ActivationStatus.Activated);
        var line = new Line
        {
            Id = 77,
            AgreementLineNumber = "A100-1.1",
            ItemNumber = "GEN60-240",
            GenericItemNumber = "GEN60",
            Attributes = "Voltage:240V",
            Quantity = 2,
        };
        _equipmentRepository.Setup(repository => repository.Create(
                _identity.Object,
                42,
                "UK",
                It.Is<AgreementEquipmentCreateCommand>(command =>
                    command.ParentLineId == 7
                    && command.GenericId == 20
                    && command.ItemNumber == "GEN60-240"
                    && command.Quantity == 2)))
            .Returns(AgreementEquipmentCreateResult.Success(line, (int)FulfilmentStatus.PartiallyFulfilled));

        var result = CreateSubject().Create(42, CreateRequest());

        var created = result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be("/api/agreements/42");
        var response = created.Value.Should().BeOfType<AgreementEquipmentCreatedResponse>().Subject;
        response.LineId.Should().Be(77);
        response.ParentLineId.Should().Be(7);
        response.GenericItemNumber.Should().Be("GEN60");
        response.HeaderStatus.Should().Be((int)FulfilmentStatus.PartiallyFulfilled);
    }

    [Theory]
    [InlineData(AgreementEquipmentFailure.LineNotDeletable)]
    [InlineData(AgreementEquipmentFailure.ReservationsExist)]
    public void Delete_ReturnsConflictForDeleteSafeguards(AgreementEquipmentFailure failure)
    {
        SetAccessibleHeader("T100", (int)ActivationStatus.TODO);
        _equipmentRepository.Setup(repository => repository.Delete(_identity.Object, 42, "UK", 77))
            .Returns(new AgreementEquipmentDeleteResult(failure));

        var result = CreateSubject().Delete(42, 77);

        GetValidationProblem(result, StatusCodes.Status409Conflict)
            .Errors.Should().ContainKey("lineId");
    }

    [Fact]
    public void Delete_RejectsActivatedAAgreementWithoutRemovingEquipment()
    {
        SetAccessibleHeader("A100", (int)ActivationStatus.Activated);
        GetValidationProblem(CreateSubject().Delete(42, 77), StatusCodes.Status409Conflict)
            .Errors.Should().ContainKey("agreement");
        _equipmentRepository.Verify(repository => repository.Delete(
            It.IsAny<IUserIdentity>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }
    private AgreementEquipmentController CreateSubject() =>
        new(_repository.Object, _equipmentRepository.Object, _identity.Object);

    private IActionResult Invoke(string action) => action switch
    {
        "catalog" => CreateSubject().GetCatalog(42),
        "generic" => CreateSubject().GetGenericCatalog(42, 20),
        "create" => CreateSubject().Create(42, CreateRequest()),
        "delete" => CreateSubject().Delete(42, 77),
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, null),
    };

    private void SetIdentity(string divisions)
    {
        _identity.Setup(identity => identity.GetIdentity()).Returns(new User
        {
            LoginName = "planner@example.com",
            FullName = "Fleet Planner",
            Division = divisions,
            DateFormat = "dd/MM/yyyy",
        });
    }

    private void SetAccessibleHeader(string agreementNumber, int activationStatus, bool isDeleted = false)
    {
        SetIdentity("UK");
        _repository.Setup(repository => repository.GetHeader(42))
            .Returns(CreateHeader(agreementNumber, activationStatus, "UK", isDeleted));
    }

    private static Header CreateHeader(
        string agreementNumber,
        int activationStatus,
        string division,
        bool isDeleted = false) => new()
    {
        Id = 42,
        AgreementNumber = agreementNumber,
        ActivationStatus = activationStatus,
        Division = division,
        Facility = "UK-FAC",
        OrderSource = "IPG",
        IsDeleted = isDeleted,
    };

    private static CreateAgreementEquipmentRequest CreateRequest() => new()
    {
        ParentLineId = 7,
        GenericId = 20,
        ItemNumber = "GEN60-240",
        Attributes = ["Voltage:240V"],
        Quantity = 2,
    };

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

    private void VerifyNoEquipmentCalls()
    {
        _equipmentRepository.VerifyNoOtherCalls();
    }
}
