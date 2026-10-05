using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OF.Api;
using OF.Api.UseCases.Reservations;
using OF.Data;
using OF.Data.Database;
using System;
using System.Threading.Tasks;
using Xunit;

namespace OF.Tests.Api.UseCases.Reservations;
public class ReservationFunctionsIntegrationTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly Mock<ILogger<ReservationRequestHandler>> _mockLogger;
    private readonly ReservationRequestHandler _handler;

    public ReservationFunctionsIntegrationTests()
    {
        // Set up in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _mockLogger = new Mock<ILogger<ReservationRequestHandler>>();
        _handler = new ReservationRequestHandler(_dbContext, _mockLogger.Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task Handler_WithValidReservationId_ReturnsReservationDto()
    {
        // Arrange
        var testHeader = new Header
        {
            Id = 1,
            AgreementNumber = "TEST001",
            CustomerNumber = "CUST001",
            Division = "200",
            Facility = "USG",
            OrderSource = "ORF",
            FulfilmentStatus = 1
        };

        var testLine = new Line
        {
            Id = 1,
            HeaderId = 1,
            ItemNumber = "ITEM001",
            Attributes = "Test Attributes for Line",
            Quantity = 3,
            GenericItemNumber = "GEN001",
            Division = "200",
            Facility = "USG",
            OrderSource = "ORF",
            Warehouse = "WH001"
        };

        var testReservation = new Reservation
        {
            Id = 1,
            AssetId = "ASSET001",
            LineId = 1,
            ItemNumber = "ITEM001",
            Quantity = 3,
            Warehouse = "WH001"
        };

        _dbContext.Headers.Add(testHeader);
        _dbContext.Lines.Add(testLine);
        _dbContext.Reservations.Add(testReservation);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
        Assert.Equal("ASSET001", result.AssetId);
        Assert.Equal("ITEM001", result.ItemNumber);
        Assert.Equal(3, result.Quantity);
        Assert.Equal(1, result.LineId);
        Assert.Equal("Test Attributes for Line", result.Attributes);
    }

    [Fact]
    public async Task Handler_WithNonExistentId_ReturnsNull()
    {
        // Act
        var result = await _handler.Handle(999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handler_WithMultipleReservations_ReturnsCorrectOne()
    {
        // Arrange
        var testHeader = new Header
        {
            Id = 1,
            AgreementNumber = "TEST001",
            CustomerNumber = "CUST001",
            Division = "200",
            Facility = "USG",
            OrderSource = "ORF",
            FulfilmentStatus = 1
        };

        var testLine1 = new Line
        {
            Id = 1,
            HeaderId = 1,
            ItemNumber = "ITEM001",
            Attributes = "Line 1 Attributes",
            Quantity = 2,
            GenericItemNumber = "GEN001",
            Division = "200",
            Facility = "USG",
            OrderSource = "ORF",
            Warehouse = "WH001"
        };

        var testLine2 = new Line
        {
            Id = 2,
            HeaderId = 1,
            ItemNumber = "ITEM002",
            Attributes = "Line 2 Attributes",
            Quantity = 4,
            GenericItemNumber = "GEN002",
            Division = "200",
            Facility = "USG",
            OrderSource = "ORF",
            Warehouse = "WH002"
        };

        var testReservation1 = new Reservation
        {
            Id = 1,
            AssetId = "ASSET001",
            LineId = 1,
            ItemNumber = "ITEM001",
            Quantity = 2,
            Warehouse = "WH001"
        };

        var testReservation2 = new Reservation
        {
            Id = 2,
            AssetId = "ASSET002",
            LineId = 2,
            ItemNumber = "ITEM002",
            Quantity = 4,
            Warehouse = "WH002"
        };

        _dbContext.Headers.Add(testHeader);
        _dbContext.Lines.AddRange(testLine1, testLine2);
        _dbContext.Reservations.AddRange(testReservation1, testReservation2);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(2);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Id);
        Assert.Equal("ASSET002", result.AssetId);
        Assert.Equal("ITEM002", result.ItemNumber);
        Assert.Equal(4, result.Quantity);
        Assert.Equal(2, result.LineId);
        Assert.Equal("Line 2 Attributes", result.Attributes);
    }

    [Fact]
    public async Task Handler_WithRehireReservation_ReturnsCorrectData()
    {
        // Arrange
        var testHeader = new Header
        {
            Id = 1,
            AgreementNumber = "REHIRE001",
            CustomerNumber = "CUST001",
            Division = "200",
            Facility = "USG",
            OrderSource = "ORF",
            FulfilmentStatus = 1
        };

        var testLine = new Line
        {
            Id = 1,
            HeaderId = 1,
            ItemNumber = "REHIRE_ITEM",
            Attributes = "Rehire Attributes",
            Quantity = 1,
            GenericItemNumber = "REHIRE_GEN",
            Division = "200",
            Facility = "USG",
            OrderSource = "ORF",
            Warehouse = "REHIRE_WH"
        };

        var testReservation = new Reservation
        {
            Id = 1,
            AssetId = "REHIRE_ASSET",
            LineId = 1,
            ItemNumber = "REHIRE_ITEM",
            Quantity = 1,
            Warehouse = "REHIRE_WH"
        };

        _dbContext.Headers.Add(testHeader);
        _dbContext.Lines.Add(testLine);
        _dbContext.Reservations.Add(testReservation);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("REHIRE_ASSET", result.AssetId);
        Assert.Equal("REHIRE_ITEM", result.ItemNumber);
        Assert.Equal(1, result.Quantity);
        Assert.Equal(1, result.LineId);
        Assert.Equal("Rehire Attributes", result.Attributes);
    }

    [Fact]
    public async Task Handler_WithDepotFulfilledReservation_ReturnsCorrectData()
    {
        // Arrange
        var testHeader = new Header
        {
            Id = 1,
            AgreementNumber = "DEPOT001",
            CustomerNumber = "CUST001",
            Division = "200",
            Facility = "USG",
            OrderSource = "ORF",
            FulfilmentStatus = 1
        };

        var testLine = new Line
        {
            Id = 1,
            HeaderId = 1,
            ItemNumber = "DEPOT_ITEM",
            Attributes = "Depot Fulfil Attributes",
            Quantity = 5,
            GenericItemNumber = "DEPOT_GEN",
            Division = "200",
            Facility = "USG",
            OrderSource = "ORF",
            Warehouse = "DEPOT_WH"
        };

        var testReservation = new Reservation
        {
            Id = 1,
            AssetId = "DEPOT_ASSET",
            LineId = 1,
            ItemNumber = "DEPOT_ITEM",
            Quantity = 5,
            Warehouse = "DEPOT_WH"
        };

        _dbContext.Headers.Add(testHeader);
        _dbContext.Lines.Add(testLine);
        _dbContext.Reservations.Add(testReservation);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("DEPOT_ASSET", result.AssetId);
        Assert.Equal("DEPOT_ITEM", result.ItemNumber);
        Assert.Equal(5, result.Quantity);
        Assert.Equal(1, result.LineId);
        Assert.Equal("Depot Fulfil Attributes", result.Attributes);
    }

    [Fact]
    public async Task Handler_WithEmptyAttributes_ReturnsNullAttributes()
    {
        // Arrange
        var testHeader = new Header
        {
            Id = 1,
            AgreementNumber = "EMPTY001",
            CustomerNumber = "CUST001",
            Division = "200",
            Facility = "USG",
            OrderSource = "ORF",
            FulfilmentStatus = 1
        };

        var testLine = new Line
        {
            Id = 1,
            HeaderId = 1,
            ItemNumber = "EMPTY_ITEM",
            Attributes = null, // Empty attributes
            Quantity = 1,
            GenericItemNumber = "EMPTY_GEN",
            Division = "200",
            Facility = "USG",
            OrderSource = "ORF",
            Warehouse = "EMPTY_WH"
        };

        var testReservation = new Reservation
        {
            Id = 1,
            AssetId = "EMPTY_ASSET",
            LineId = 1,
            ItemNumber = "EMPTY_ITEM",
            Quantity = 1,
            Warehouse = "EMPTY_WH"
        };

        _dbContext.Headers.Add(testHeader);
        _dbContext.Lines.Add(testLine);
        _dbContext.Reservations.Add(testReservation);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("EMPTY_ASSET", result.AssetId);
        Assert.Equal("EMPTY_ITEM", result.ItemNumber);
        Assert.Equal(1, result.Quantity);
        Assert.Equal(1, result.LineId);
        Assert.Null(result.Attributes);
    }
}
