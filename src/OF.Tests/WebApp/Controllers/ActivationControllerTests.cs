using System.Text.Json;
using Azure.Messaging.ServiceBus;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using OF.Common;
using OF.Common.Infrastructure.Storage;
using OF.Data.Database;
using OF.UI.Database;
using OF.UI.Identity;
using OF.WebApp.Controllers;

namespace OF.Tests.WebApp.Controllers;

public class ActivationControllerTests
{
    private readonly Mock<IDataRepository> _repository = new();
    private readonly Mock<IUserIdentity> _identity = new();
    private readonly Mock<ServiceBusClient> _serviceBus = new();
    private readonly Mock<ServiceBusSender> _sender = new();
    private readonly ActivateAgreementQueueClient _queueClient;

    public ActivationControllerTests()
    {
        _serviceBus.Setup(client => client.CreateSender(Constants.Queues.ActivateAgreement))
            .Returns(_sender.Object);
        _queueClient = new ActivateAgreementQueueClient(_serviceBus.Object);
    }

    [Theory]
    [InlineData("activate")]
    [InlineData("cancel")]
    public async Task ActivationActions_ReturnUnauthorizedWithoutReadingAgreement_WhenIdentityIsMissing(string action)
    {
        SetMissingIdentity();

        var result = await Invoke(action, 42);

        result.Should().BeOfType<UnauthorizedResult>();
        _repository.Verify(repository => repository.GetHeader(It.IsAny<int>()), Times.Never);
        VerifyNoStateOrQueueCalls();
    }

    [Theory]
    [InlineData("activate", "UK", null)]
    [InlineData("activate", "UK", "FR")]
    [InlineData("activate", "", "UK")]
    [InlineData("cancel", "UK", null)]
    [InlineData("cancel", "UK", "FR")]
    [InlineData("cancel", "", "UK")]
    public async Task ActivationActions_ReturnNotFoundWithoutStateOrQueueCalls_WhenAgreementIsMissingOrInaccessible(
        string action,
        string callerDivisions,
        string? agreementDivision)
    {
        SetIdentity(callerDivisions);
        if (agreementDivision != null)
        {
            _repository.Setup(repository => repository.GetHeader(42)).Returns(CreateHeader(agreementDivision));
        }

        var result = await Invoke(action, 42);

        result.Should().BeOfType<NotFoundResult>();
        _repository.Verify(repository => repository.GetHeader(42), Times.Once);
        VerifyNoStateOrQueueCalls();
    }

    [Fact]
    public async Task Activate_PersistsLocalStateBeforeQueueing_AndReturnsExactResponse_WhenDivisionMatches()
    {
        SetIdentity(" uk, IE ");
        _repository.Setup(repository => repository.GetHeader(42)).Returns(CreateHeader("UK"));
        var calls = new List<string>();
        ServiceBusMessage? queuedMessage = null;
        _repository.Setup(repository => repository.SetHeaderForActivation(42, _identity.Object))
            .Returns(() =>
            {
                calls.Add("state");
                return Task.CompletedTask;
            });
        _sender.Setup(sender => sender.SendMessageAsync(
                It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ServiceBusMessage, CancellationToken>((message, _) =>
            {
                calls.Add("queue");
                queuedMessage = message;
            })
            .Returns(Task.CompletedTask);

        var result = await CreateSubject().Activate(42);

        calls.Should().Equal("state", "queue");
        queuedMessage.Should().NotBeNull();
        queuedMessage!.Body.ToString().Should().Be("{\"HeaderId\":42}");
        GetResponseValue(result, 200).Should().BeOfType<ActivationResponse>();
        SerializeResponse(result, 200).Should().Be("{\"type\":\"Activation\",\"headerId\":42}");
        _repository.Verify(repository => repository.SetHeaderForActivation(42, _identity.Object), Times.Once);
        _sender.Verify(sender => sender.SendMessageAsync(
            It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void CancelActivation_UpdatesStateAndReturnsExactResponse_WhenDivisionMatches()
    {
        SetIdentity(" uk, IE ");
        _repository.Setup(repository => repository.GetHeader(42)).Returns(CreateHeader("UK"));

        var result = CreateSubject().CancelActivation(42);

        GetResponseValue(result, 200).Should().BeOfType<ActivationResponse>();
        SerializeResponse(result, 200).Should().Be("{\"type\":\"CancelActivation\",\"headerId\":42}");
        _repository.Verify(repository => repository.TimeoutSublineActivation(42, _identity.Object), Times.Once);
        _repository.Verify(repository => repository.SetHeaderForActivation(
            It.IsAny<int>(), It.IsAny<IUserIdentity>()), Times.Never);
        VerifyQueueNotCalled();
    }

    [Theory]
    [InlineData("activate")]
    [InlineData("cancel")]
    public async Task ActivationActions_AllowSuperAdminAccess_WhenAgreementHasNoDivision(string action)
    {
        SetIdentity(null, isSuperAdmin: true);
        _repository.Setup(repository => repository.GetHeader(42)).Returns(CreateHeader(null));
        SetupSuccessfulStateAndQueue();

        var result = await Invoke(action, 42);

        result.Should().BeOfType<OkObjectResult>();
        _repository.Verify(repository => repository.GetHeader(42), Times.Once);
        if (action == "activate")
        {
            _repository.Verify(repository => repository.SetHeaderForActivation(42, _identity.Object), Times.Once);
            _sender.Verify(sender => sender.SendMessageAsync(
                It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        }
        else
        {
            _repository.Verify(repository => repository.TimeoutSublineActivation(42, _identity.Object), Times.Once);
            VerifyQueueNotCalled();
        }
    }

    [Fact]
    public async Task Activate_ReturnsServiceUnavailableAfterPersistingLocalState_WhenQueueThrows()
    {
        SetIdentity("UK");
        _repository.Setup(repository => repository.GetHeader(42)).Returns(CreateHeader("UK"));
        var calls = new List<string>();
        _repository.Setup(repository => repository.SetHeaderForActivation(42, _identity.Object))
            .Returns(() =>
            {
                calls.Add("state");
                return Task.CompletedTask;
            });
        _sender.Setup(sender => sender.SendMessageAsync(
                It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                calls.Add("queue");
                return Task.FromException(new InvalidOperationException("offline"));
            });

        var result = await CreateSubject().Activate(42);

        calls.Should().Equal("state", "queue");
        GetResponseValue(result, 503).Should().BeOfType<ActivationUnavailableResponse>();
        SerializeResponse(result, 503).Should().Be(
            "{\"type\":\"Activation\",\"headerId\":42,\"error\":\"Activation queued locally but Service Bus unavailable: offline\"}");
        _repository.Verify(repository => repository.SetHeaderForActivation(42, _identity.Object), Times.Once);
        _sender.Verify(sender => sender.SendMessageAsync(
            It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("activate", -1)]
    [InlineData("cancel", 999)]
    public async Task ActivationActions_PreserveCurrentBehaviorForAccessibleAgreementsWithOddStatuses(
        string action,
        int activationStatus)
    {
        SetIdentity("UK");
        _repository.Setup(repository => repository.GetHeader(42))
            .Returns(CreateHeader("UK", activationStatus));
        SetupSuccessfulStateAndQueue();

        var result = await Invoke(action, 42);

        result.Should().BeOfType<OkObjectResult>();
        if (action == "activate")
        {
            _repository.Verify(repository => repository.SetHeaderForActivation(42, _identity.Object), Times.Once);
        }
        else
        {
            _repository.Verify(repository => repository.TimeoutSublineActivation(42, _identity.Object), Times.Once);
        }
    }

    private ActivationController CreateSubject() => new(_repository.Object, _identity.Object, _queueClient);

    private async Task<IActionResult> Invoke(string action, int headerId) => action switch
    {
        "activate" => await CreateSubject().Activate(headerId),
        "cancel" => CreateSubject().CancelActivation(headerId),
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown activation action."),
    };

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

    private void SetupSuccessfulStateAndQueue()
    {
        _repository.Setup(repository => repository.SetHeaderForActivation(
                It.IsAny<int>(), It.IsAny<IUserIdentity>()))
            .Returns(Task.CompletedTask);
        _sender.Setup(sender => sender.SendMessageAsync(
                It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private void VerifyNoStateOrQueueCalls()
    {
        _repository.Verify(repository => repository.SetHeaderForActivation(
            It.IsAny<int>(), It.IsAny<IUserIdentity>()), Times.Never);
        _repository.Verify(repository => repository.TimeoutSublineActivation(
            It.IsAny<int>(), It.IsAny<IUserIdentity>()), Times.Never);
        VerifyQueueNotCalled();
    }

    private void VerifyQueueNotCalled()
    {
        _sender.Verify(sender => sender.SendMessageAsync(
            It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Header CreateHeader(string? division, int activationStatus = 0) => new()
    {
        Id = 42,
        Division = division!,
        ActivationStatus = activationStatus,
        OrderSource = "NOF",
        Facility = "FAC1",
    };

    private static string SerializeResponse(IActionResult result, int expectedStatus)
    {
        return JsonSerializer.Serialize(
            GetResponseValue(result, expectedStatus),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private static object GetResponseValue(IActionResult result, int expectedStatus)
    {
        var objectResult = result.Should().BeAssignableTo<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(expectedStatus);
        objectResult.Value.Should().NotBeNull();
        return objectResult.Value!;
    }
}
