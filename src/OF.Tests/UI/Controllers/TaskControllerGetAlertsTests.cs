using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using OF.Data.Database;
using OF.UI.Controllers;
using OF.UI.Database;
using OF.UI.Identity;

namespace OF.Tests.UI.Controllers;

public class TaskControllerGetAlertsTests
{
    private readonly Mock<IDataRepository> _repository;
    private readonly Mock<IUserIdentity> _identity;
    private readonly Mock<IHttpContextAccessor> _httpContextAccessor;
    private readonly TaskController _sut;
    private const string TestUser = "testuser@aggreko.com";

    public TaskControllerGetAlertsTests()
    {
        _repository = new Mock<IDataRepository>();
        _identity = new Mock<IUserIdentity>();
        _httpContextAccessor = new Mock<IHttpContextAccessor>();

        _identity.Setup(x => x.GetIdentity()).Returns(new User
        {
            LoginName = TestUser,
            FullName = "Test User",
            Division = "UK",
            IsAdmin = true,
            DateFormat = "dd/MM/yyyy"
        });

        _sut = new TaskController(_repository.Object, _identity.Object, _httpContextAccessor.Object);
    }

    [Fact]
    public void GetAlerts_WhenLineIsNull_AcknowledgesAlert()
    {
        // Arrange
        var alert = CreateClashAlert(1, 100);
        _repository.Setup(x => x.GetAlertsForUser(TestUser)).Returns(new[] { alert });
        _repository.Setup(x => x.GetLine(100)).Returns((Line)null!);

        // Act
        var result = _sut.GetAlerts();

        // Assert
        var okResult = result.Result as OkObjectResult;
        var alerts = okResult!.Value as Alert[];
        alerts.Should().BeEmpty();
        _repository.Verify(x => x.AcknowledgeAlerts(It.Is<int[]>(ids => ids.Contains(1))), Times.Once);
    }

    [Fact]
    public void GetAlerts_WhenLineIsDeleted_AcknowledgesAlert()
    {
        // Arrange
        var alert = CreateClashAlert(1, 100);
        var line = new Line { Id = 100, HeaderId = 1, IsDeleted = true, RequiresFulfilment = true };
        _repository.Setup(x => x.GetAlertsForUser(TestUser)).Returns(new[] { alert });
        _repository.Setup(x => x.GetLine(100)).Returns(line);

        // Act
        var result = _sut.GetAlerts();

        // Assert
        var okResult = result.Result as OkObjectResult;
        var alerts = okResult!.Value as Alert[];
        alerts.Should().BeEmpty();
        _repository.Verify(x => x.AcknowledgeAlerts(It.Is<int[]>(ids => ids.Contains(1))), Times.Once);
    }

    [Fact]
    public void GetAlerts_WhenClashAlertAndLineHasReservations_AcknowledgesAlert()
    {
        // Arrange
        var alert = CreateClashAlert(1, 100);
        var line = new Line { Id = 100, HeaderId = 1, IsDeleted = false, RequiresFulfilment = true };
        _repository.Setup(x => x.GetAlertsForUser(TestUser)).Returns(new[] { alert });
        _repository.Setup(x => x.GetLine(100)).Returns(line);
        _repository.Setup(x => x.HasReservationsForLine(100)).Returns(true);

        // Act
        var result = _sut.GetAlerts();

        // Assert
        var okResult = result.Result as OkObjectResult;
        var alerts = okResult!.Value as Alert[];
        alerts.Should().BeEmpty();
        _repository.Verify(x => x.AcknowledgeAlerts(It.Is<int[]>(ids => ids.Contains(1))), Times.Once);
    }

    [Fact]
    public void GetAlerts_WhenClashAlertAndLineDoesNotRequireFulfilment_AcknowledgesAlert()
    {
        // Arrange
        var alert = CreateClashAlert(1, 100);
        var line = new Line { Id = 100, HeaderId = 1, IsDeleted = false, RequiresFulfilment = false };
        _repository.Setup(x => x.GetAlertsForUser(TestUser)).Returns(new[] { alert });
        _repository.Setup(x => x.GetLine(100)).Returns(line);
        _repository.Setup(x => x.HasReservationsForLine(100)).Returns(false);

        // Act
        var result = _sut.GetAlerts();

        // Assert
        var okResult = result.Result as OkObjectResult;
        var alerts = okResult!.Value as Alert[];
        alerts.Should().BeEmpty();
        _repository.Verify(x => x.AcknowledgeAlerts(It.Is<int[]>(ids => ids.Contains(1))), Times.Once);
    }

    [Fact]
    public void GetAlerts_WhenClashAlertStillValid_ReturnsAlert()
    {
        // Arrange
        var alert = CreateClashAlert(1, 100);
        var line = new Line { Id = 100, HeaderId = 1, IsDeleted = false, RequiresFulfilment = true };
        var header = new Header { Id = 1, Division = "UK", OrderSource = "NOF", Facility = "FAC1" };
        _repository.Setup(x => x.GetAlertsForUser(TestUser)).Returns(new[] { alert });
        _repository.Setup(x => x.GetLine(100)).Returns(line);
        _repository.Setup(x => x.HasReservationsForLine(100)).Returns(false);
        _repository.Setup(x => x.GetHeader(1)).Returns(header);

        // Act
        var result = _sut.GetAlerts();

        // Assert
        var okResult = result.Result as OkObjectResult;
        var alerts = okResult!.Value as Alert[];
        alerts.Should().HaveCount(1);
        alerts![0].HeaderId.Should().Be(1);
        _repository.Verify(x => x.AcknowledgeAlerts(It.IsAny<int[]>()), Times.Never);
    }

    [Fact]
    public void GetAlerts_WhenHeaderNoLongerExists_AcknowledgesAlert()
    {
        // Arrange
        var alert = CreateClashAlert(1, 100);
        var line = new Line { Id = 100, HeaderId = 1, IsDeleted = false, RequiresFulfilment = true };
        _repository.Setup(x => x.GetAlertsForUser(TestUser)).Returns(new[] { alert });
        _repository.Setup(x => x.GetLine(100)).Returns(line);
        _repository.Setup(x => x.HasReservationsForLine(100)).Returns(false);
        _repository.Setup(x => x.GetHeader(1)).Returns((Header?)null);

        // Act
        var result = _sut.GetAlerts();

        // Assert
        var okResult = result.Result as OkObjectResult;
        var alerts = okResult!.Value as Alert[];
        alerts.Should().BeEmpty();
        _repository.Verify(x => x.AcknowledgeAlerts(It.Is<int[]>(ids => ids.Contains(1))), Times.Once);
    }

    [Fact]
    public void GetAlerts_WhenNonClashAlertAndLineExists_ReturnsAlert()
    {
        // Arrange
        var alert = new Alert { Id = 1, LineId = 100, Text = "Some other alert", AffectedUser = TestUser, Acknowledged = false };
        var line = new Line { Id = 100, HeaderId = 1, IsDeleted = false, RequiresFulfilment = false };
        var header = new Header { Id = 1, Division = "UK", OrderSource = "NOF", Facility = "FAC1" };
        _repository.Setup(x => x.GetAlertsForUser(TestUser)).Returns(new[] { alert });
        _repository.Setup(x => x.GetLine(100)).Returns(line);
        _repository.Setup(x => x.GetHeader(1)).Returns(header);

        // Act
        var result = _sut.GetAlerts();

        // Assert
        var okResult = result.Result as OkObjectResult;
        var alerts = okResult!.Value as Alert[];
        alerts.Should().HaveCount(1);
        _repository.Verify(x => x.HasReservationsForLine(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void GetAlerts_WithMultipleAlerts_AcknowledgesOnlyStaleOnes()
    {
        // Arrange
        var staleAlert = CreateClashAlert(1, 100); // line deleted
        var validAlert = CreateClashAlert(2, 200); // still valid

        var deletedLine = new Line { Id = 100, HeaderId = 1, IsDeleted = true, RequiresFulfilment = true };
        var validLine = new Line { Id = 200, HeaderId = 2, IsDeleted = false, RequiresFulfilment = true };
        var header = new Header { Id = 2, Division = "UK", OrderSource = "NOF", Facility = "FAC1" };

        _repository.Setup(x => x.GetAlertsForUser(TestUser)).Returns(new[] { staleAlert, validAlert });
        _repository.Setup(x => x.GetLine(100)).Returns(deletedLine);
        _repository.Setup(x => x.GetLine(200)).Returns(validLine);
        _repository.Setup(x => x.HasReservationsForLine(200)).Returns(false);
        _repository.Setup(x => x.GetHeader(2)).Returns(header);

        // Act
        var result = _sut.GetAlerts();

        // Assert
        var okResult = result.Result as OkObjectResult;
        var alerts = okResult!.Value as Alert[];
        alerts.Should().HaveCount(1);
        alerts![0].Id.Should().Be(2);
        _repository.Verify(x => x.AcknowledgeAlerts(It.Is<int[]>(ids => ids.Length == 1 && ids[0] == 1)), Times.Once);
    }

    [Fact]
    public void GetAlerts_WhenNoStaleAlerts_DoesNotCallAcknowledge()
    {
        // Arrange
        var alert = CreateClashAlert(1, 100);
        var line = new Line { Id = 100, HeaderId = 1, IsDeleted = false, RequiresFulfilment = true };
        var header = new Header { Id = 1, Division = "UK", OrderSource = "NOF", Facility = "FAC1" };
        _repository.Setup(x => x.GetAlertsForUser(TestUser)).Returns(new[] { alert });
        _repository.Setup(x => x.GetLine(100)).Returns(line);
        _repository.Setup(x => x.HasReservationsForLine(100)).Returns(false);
        _repository.Setup(x => x.GetHeader(1)).Returns(header);

        // Act
        _sut.GetAlerts();

        // Assert
        _repository.Verify(x => x.AcknowledgeAlerts(It.IsAny<int[]>()), Times.Never);
    }

    private static Alert CreateClashAlert(int id, int lineId)
    {
        return new Alert
        {
            Id = id,
            LineId = lineId,
            Text = "Reservation clash: A123456 line was displaced",
            AffectedUser = TestUser,
            Acknowledged = false
        };
    }
}
