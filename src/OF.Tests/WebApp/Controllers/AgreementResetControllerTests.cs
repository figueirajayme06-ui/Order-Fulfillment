using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using OF.Data.Database;
using OF.UI.Database;
using OF.UI.Identity;
using OF.WebApp.Controllers;

namespace OF.Tests.WebApp.Controllers;

public class AgreementResetControllerTests
{
    private readonly Mock<IDataRepository> repository = new();
    private readonly Mock<IAgreementResetRepository> reset = new();
    private readonly Mock<IUserIdentity> identity = new();

    [Fact]
    public void Unfulfil_RequiresIdentityBeforeReadingData()
    {
        Subject().Unfulfil(42).Should().BeOfType<UnauthorizedResult>();
        repository.VerifyNoOtherCalls();
        reset.VerifyNoOtherCalls();
    }

    [Fact]
    public void Unfulfil_DeniesReadOnlyEvenWhenCalledDirectly()
    {
        identity.Setup(service => service.GetIdentity()).Returns(new User { Roles = "ChangeOrder,readonly", Division = "UK" });
        Subject().Unfulfil(42).Should().BeOfType<ForbidResult>();
        reset.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("FR")]
    [InlineData("")]
    public void Unfulfil_HidesMissingAndInaccessibleAgreement(string? division)
    {
        SetIdentity();
        if (division != null)
            repository.Setup(service => service.GetHeader(42)).Returns(new Header { Division = division });
        Subject().Unfulfil(42).Should().BeOfType<NotFoundResult>();
        reset.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(AgreementResetFailure.ActivationConflict, "activation_conflict")]
    [InlineData(AgreementResetFailure.ConfirmedReservations, "confirmed_reservations")]
    [InlineData(AgreementResetFailure.ConcurrentChange, "agreement_changed")]
    public void Unfulfil_ReturnsExplicitConflict(AgreementResetFailure failure, string code)
    {
        SetupAccess();
        reset.Setup(service => service.Unfulfil(42, "UK", "planner")).Returns(new AgreementResetResult(failure));
        var result = Subject().Unfulfil(42).Should().BeOfType<ObjectResult>().Subject;
        result.StatusCode.Should().Be(409);
        result.ContentTypes.Should().Contain("application/problem+json");
        result.Value.Should().BeOfType<ProblemDetails>().Subject.Extensions["code"].Should().Be(code);
    }

    [Fact]
    public void Unfulfil_ReturnsCompleteOperationCountsAndStatus()
    {
        SetupAccess();
        reset.Setup(service => service.Unfulfil(42, "UK", "planner")).Returns(new AgreementResetResult(AgreementResetFailure.None, 8, 100, 0));
        Subject().Unfulfil(42).Should().BeOfType<OkObjectResult>().Subject.Value.Should().Be(new AgreementResetResponse(8, 100, 0));
    }

    [Fact]
    public void Unfulfil_ReturnsNotFoundWhenPersistedDivisionChanges()
    {
        SetupAccess();
        reset.Setup(service => service.Unfulfil(42, "UK", "planner")).Returns(new AgreementResetResult(AgreementResetFailure.NotFound));
        Subject().Unfulfil(42).Should().BeOfType<NotFoundResult>();
    }

    private void SetupAccess()
    {
        SetIdentity();
        repository.Setup(service => service.GetHeader(42)).Returns(new Header { Id = 42, Division = "UK" });
    }
    private void SetIdentity() => identity.Setup(service => service.GetIdentity()).Returns(new User { LoginName = "planner", Division = "UK" });
    private AgreementResetController Subject() => new(repository.Object, reset.Object, identity.Object);
}
