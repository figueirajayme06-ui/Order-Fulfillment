using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using OF.Common.Infrastructure.OF;
using OF.Data.Database;
using OF.UI.Database;
using OF.UI.Identity;
using OF.UI.Models;
using OF.WebApp.Controllers;

namespace OF.Tests.WebApp.Controllers;

public class FulfilmentControllerTests
{
    private readonly Mock<IDataRepository> _repository = new();
    private readonly Mock<IUserIdentity> _identity = new();
    private readonly Mock<ICoreFulfilmentEngine> _engine = new();

    [Fact]
    public void ReserveNonSerializedStock_UsesTheEnteredStockQuantityDirectly()
    {
        SetIdentity("UK");
        var line = CreateLine();
        var stock = CreateStock(stockQuantity: 20, allocatedQuantity: 2);
        stock.Reservations.Add(new StockReservation
        {
            Quantity = 4,
            ValidFromDate = new DateTime(2026, 8, 5),
            ValidToDate = new DateTime(2026, 8, 8),
        });
        SetupAccessibleLine(line, stock);
        Reservation? saved = null;
        _repository.Setup(repository => repository.CreateReservation(_identity.Object, It.IsAny<Reservation>()))
            .Callback<IUserIdentity, Reservation>((_, reservation) => saved = reservation)
            .Returns<IUserIdentity, Reservation>((_, reservation) =>
            {
                reservation.Id = 91;
                return reservation;
            });

        var result = CreateSubject().ReserveNonSerializedStock(new ReserveNonSerializedStockRequest(
            line.Id, "SUB-ITEM", "EM0", 3));

        var created = result.Should().BeOfType<CreatedResult>().Subject;
        created.StatusCode.Should().Be(201);
        created.Value.Should().BeEquivalentTo(new ReserveNonSerializedStockResponse(
            91, "SUB-ITEM", "EM0", 3, 3));
        saved.Should().BeEquivalentTo(new Reservation
        {
            Id = 91,
            AssetId = "SUB-ITEM",
            ItemNumber = "SUB-ITEM",
            Warehouse = "EM0",
            LineId = line.Id,
            Quantity = 3,
            EffectiveQuantity = 3,
        });
        _engine.Verify(engine => engine.RecalculateStatusForLineAndHeader(line, "planner@example.com"), Times.Once);
    }

    [Fact]
    public void ReserveNonSerializedStock_ReturnsConflictWhenPeriodAvailabilityChanged()
    {
        SetIdentity("UK");
        var line = CreateLine();
        var stock = CreateStock(stockQuantity: 10, allocatedQuantity: 0);
        stock.Reservations.Add(new StockReservation
        {
            Quantity = 6,
            ValidFromDate = line.ValidFromDate,
            ValidToDate = line.ValidToDate,
        });
        SetupAccessibleLine(line, stock);

        var result = CreateSubject().ReserveNonSerializedStock(new ReserveNonSerializedStockRequest(
            line.Id, "SUB-ITEM", "EM0", 5));

        var conflict = result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflict.Value.Should().BeEquivalentTo(new StockReservationErrorResponse(
            "Only 4 stock units are available for the selected period.", 4));
        VerifyNoMutation();
    }

    [Fact]
    public void ReserveNonSerializedStock_ReturnsNotFoundForAnInaccessibleAgreement()
    {
        SetIdentity("UK");
        _repository.Setup(repository => repository.GetHeaderForLineId(7)).Returns(new Header
        {
            Id = 42,
            Division = "FR",
            Facility = "PAR",
            OrderSource = "NOF",
        });

        var result = CreateSubject().ReserveNonSerializedStock(new ReserveNonSerializedStockRequest(
            7, "SUB-ITEM", "EM0", 1));

        result.Should().BeOfType<NotFoundResult>();
        _repository.Verify(repository => repository.GetLine(It.IsAny<int>()), Times.Never);
        VerifyNoMutation();
    }

    private FulfilmentController CreateSubject() => new(
        _repository.Object,
        _identity.Object,
        _engine.Object);

    private void SetupAccessibleLine(Line line, NonSerializedQueryResult stock)
    {
        _repository.Setup(repository => repository.GetHeaderForLineId(line.Id)).Returns(new Header
        {
            Id = 42,
            Division = "UK",
            Facility = "LON",
            OrderSource = "NOF",
        });
        _repository.Setup(repository => repository.GetLine(line.Id)).Returns(line);
        _repository.Setup(repository => repository.GetNonSerializedStock(
                line.Id,
                It.Is<string[]>(divisions => divisions.SequenceEqual(new[] { "UK" })),
                "EM0",
                It.IsAny<string[]>()))
            .Returns([stock]);
    }

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

    private void VerifyNoMutation()
    {
        _repository.Verify(repository => repository.CreateReservation(
            It.IsAny<IUserIdentity>(), It.IsAny<Reservation>()), Times.Never);
        _engine.Verify(engine => engine.RecalculateStatusForLineAndHeader(
            It.IsAny<Line>(), It.IsAny<string?>()), Times.Never);
    }

    private static Line CreateLine() => new()
    {
        Id = 7,
        HeaderId = 42,
        Quantity = 5,
        ValidFromDate = new DateTime(2026, 8, 1),
        ValidToDate = new DateTime(2026, 8, 31),
        Attributes = "Voltage:400",
        RequiresFulfilment = true,
    };

    private static NonSerializedQueryResult CreateStock(
        decimal stockQuantity,
        decimal allocatedQuantity) => new()
    {
        Generic = new CpqGeneric(),
        Asset = new ProductItem
        {
            Warehouse = "EM0",
            ItemNumber = "SUB-ITEM",
            StockQuantity = stockQuantity,
            AllocatedQuantity = allocatedQuantity,
            Facility = "LON",
            Division = "UK",
            Status = "Available",
        },
        Reservations = new List<StockReservation>(),
        WarehouseName = "London",
    };
}
