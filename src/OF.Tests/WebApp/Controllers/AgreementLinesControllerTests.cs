using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using OF.Data.Database;
using OF.UI.Database;
using OF.UI.Identity;
using OF.WebApp.Controllers;

namespace OF.Tests.WebApp.Controllers;

public class AgreementLinesControllerTests
{
    private readonly Mock<IDataRepository> repository = new();
    private readonly Mock<IAgreementLineDeletionRepository> deletion = new();
    private readonly Mock<IUserIdentity> identity = new();

    [Fact]
    public void Delete_RejectsMissingAndReadOnlyIdentities()
    {
        Subject().Delete(1, 11).Should().BeOfType<UnauthorizedResult>();
        identity.Setup(candidate => candidate.GetIdentity()).Returns(new User { Roles = "Admin,ReadOnly", Division = "UK" });
        Subject().Delete(1, 11).Should().BeOfType<ForbidResult>();
        deletion.VerifyNoOtherCalls();
    }

    [Fact]
    public void Delete_HidesInaccessibleAndMissingAgreements()
    {
        Arrange();
        repository.Setup(candidate => candidate.GetHeader(1)).Returns((Header?)null!);
        Subject().Delete(1, 11).Should().BeOfType<NotFoundResult>();
        Arrange(division: "US");
        Subject().Delete(1, 11).Should().BeOfType<NotFoundResult>();
        deletion.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("A100", 3)]
    [InlineData("A100", 0)]
    [InlineData("Q100", 0)]
    [InlineData("X100", 0)]
    [InlineData("T100", 1)]
    [InlineData("T100", 2)]
    [InlineData("T100", 3)]
    public void Delete_RejectsIneligiblePersistedHeader(string number, int state)
    {
        Arrange(number, state);
        Subject().Delete(1, 11).Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(409);
        deletion.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(AgreementLineDeletionFailure.LastLine, "last_line")]
    [InlineData(AgreementLineDeletionFailure.HasChildren, "has_children")]
    [InlineData(AgreementLineDeletionFailure.LineNotEligible, "line_not_eligible")]
    [InlineData(AgreementLineDeletionFailure.HeaderNotEligible, "header_not_eligible")]
    [InlineData(AgreementLineDeletionFailure.ConfirmedReservations, "confirmed_reservations")]
    public void Delete_ReturnsCodedConflicts(AgreementLineDeletionFailure failure, string code)
    {
        Arrange();
        deletion.Setup(candidate => candidate.Delete(identity.Object, 1, "UK", 11)).Returns(new AgreementLineDeletionResult(failure));
        var response = Subject().Delete(1, 11).Should().BeOfType<ObjectResult>().Subject;
        response.StatusCode.Should().Be(409);
        response.Value.Should().BeOfType<ProblemDetails>().Which.Extensions["code"].Should().Be(code);
    }

    [Fact]
    public void Delete_ReturnsNotFoundForForeignLineAndSuccessForDeletedLine()
    {
        Arrange();
        deletion.Setup(candidate => candidate.Delete(identity.Object, 1, "UK", 11)).Returns(new AgreementLineDeletionResult(AgreementLineDeletionFailure.NotFound));
        Subject().Delete(1, 11).Should().BeOfType<NotFoundResult>();
        deletion.Setup(candidate => candidate.Delete(identity.Object, 1, "UK", 11)).Returns(new AgreementLineDeletionResult(AgreementLineDeletionFailure.None, 11, 3, 0));
        Subject().Delete(1, 11).Should().BeOfType<OkObjectResult>();
    }

    private AgreementLinesController Subject() => new(repository.Object, deletion.Object, identity.Object);

    private void Arrange(string number = "T100", int state = 0, string division = "UK")
    {
        identity.Setup(candidate => candidate.GetIdentity()).Returns(new User { LoginName = "planner", Division = "UK" });
        repository.Setup(candidate => candidate.GetHeader(1)).Returns(new Header
        {
            Id = 1, AgreementNumber = number, ActivationStatus = state, Division = division, Facility = "UK1", OrderSource = "IPG",
        });
    }
}
