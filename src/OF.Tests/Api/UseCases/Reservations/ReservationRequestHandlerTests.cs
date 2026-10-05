using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OF.Api.UseCases.Reservations;
using OF.Data;
using OF.Data.Database;
using OF.Tests.Common;

namespace OF.Tests.Api.UseCases.Reservations;

[Collection("DatabaseCollection")]
public class ReservationRequestHandlerTests : CommonDBTest
{
    private readonly Mock<ILogger<ReservationRequestHandler>> _mockLogger;
    private readonly ApplicationDbContext _dbContext;
    private readonly ReservationRequestHandler _sut;

    public ReservationRequestHandlerTests(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
        _mockLogger = new Mock<ILogger<ReservationRequestHandler>>();
        _dbContext = new ApplicationDbContext(dbContextOptions);
        _sut = new ReservationRequestHandler(_dbContext, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithValidReservationId_ReturnsReservationDto()
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
            LineId = 1,
            AssetId = "ASSET001",
            ItemNumber = "ITEM001",
            Quantity = 2,
            Warehouse = "WH001",
            LastUpdatedBy = "TestUser",
            LastUpdatedDate = DateTime.UtcNow,
            IsDepotFulfilled = false,
            IsRehire = false,
            EffectiveQuantity = 2.0,
            IsConfirmed = false
        };

        await _dbContext.Headers.AddAsync(testHeader);
        await _dbContext.Lines.AddAsync(testLine);
        await _dbContext.Reservations.AddAsync(testReservation);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.Handle(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
        Assert.Equal("ASSET001", result.AssetId);
        Assert.Equal("ITEM001", result.ItemNumber);
        Assert.Equal(2, result.Quantity);
        Assert.Equal(1, result.LineId);
        Assert.Equal("Test Attributes for Line", result.Attributes);
    }

    [Fact]
    public async Task Handle_WithNonExistentReservationId_ReturnsNull()
    {
        // Arrange
        var nonExistentId = 9999;

        // Act
        var result = await _sut.Handle(nonExistentId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_WithNullId_ReturnsNull()
    {
        // Act
        var result = await _sut.Handle(null);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_WithMultipleReservations_ReturnsCorrectOne()
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
            Quantity = 3,
            Division = "200",
            Facility = "USG",
            OrderSource = "ORF",
            Warehouse = "WH002"
        };

        var testReservation1 = new Reservation
        {
            Id = 1,
            LineId = 1,
            AssetId = "ASSET001",
            ItemNumber = "ITEM001",
            Quantity = 1,
            Warehouse = "WH001",
            LastUpdatedBy = "TestUser1",
            LastUpdatedDate = DateTime.UtcNow.AddDays(-1),
            IsDepotFulfilled = false,
            IsRehire = false,
            EffectiveQuantity = 1.0,
            IsConfirmed = false
        };

        var testReservation2 = new Reservation
        {
            Id = 2,
            LineId = 2,
            AssetId = "ASSET002",
            ItemNumber = "ITEM002",
            Quantity = 3,
            Warehouse = "WH002",
            LastUpdatedBy = "TestUser2",
            LastUpdatedDate = DateTime.UtcNow,
            IsDepotFulfilled = true,
            IsRehire = false,
            EffectiveQuantity = 3.0,
            IsConfirmed = true
        };

        await _dbContext.Headers.AddAsync(testHeader);
        await _dbContext.Lines.AddAsync(testLine1);
        await _dbContext.Lines.AddAsync(testLine2);
        await _dbContext.Reservations.AddAsync(testReservation1);
        await _dbContext.Reservations.AddAsync(testReservation2);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.Handle(2);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Id);
        Assert.Equal("ASSET002", result.AssetId);
        Assert.Equal("ITEM002", result.ItemNumber);
        Assert.Equal(3, result.Quantity);
        Assert.Equal(2, result.LineId);
        Assert.Equal("Line 2 Attributes", result.Attributes);
    }

    [Fact]
    public async Task Handle_WithReservationButNoLine_ReturnsNull()
    {
        // Arrange
        var testReservation = new Reservation
        {
            Id = 1,
            LineId = 999, // Non-existent line
            AssetId = "ASSET001",
            ItemNumber = "ITEM001",
            Quantity = 1,
            Warehouse = "WH001",
            LastUpdatedBy = "TestUser",
            LastUpdatedDate = DateTime.UtcNow,
            IsDepotFulfilled = false,
            IsRehire = false,
            EffectiveQuantity = 1.0,
            IsConfirmed = false
        };

        await _dbContext.Reservations.AddAsync(testReservation);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.Handle(1);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_WithRehireReservation_ReturnsCorrectData()
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
            Division = "200",
            Facility = "USG",
            OrderSource = "ORF",
            Warehouse = "REHIRE_WH"
        };

        var testReservation = new Reservation
        {
            Id = 1,
            LineId = 1,
            AssetId = "REHIRE_ASSET",
            ItemNumber = "REHIRE_ITEM",
            Quantity = 1,
            Warehouse = "WH001",
            LastUpdatedBy = "RehireUser",
            LastUpdatedDate = DateTime.UtcNow,
            IsDepotFulfilled = false,
            IsRehire = true,
            EffectiveQuantity = 1.0,
            IsConfirmed = false
        };

        await _dbContext.Headers.AddAsync(testHeader);
        await _dbContext.Lines.AddAsync(testLine);
        await _dbContext.Reservations.AddAsync(testReservation);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.Handle(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
        Assert.Equal("REHIRE_ASSET", result.AssetId);
        Assert.Equal("REHIRE_ITEM", result.ItemNumber);
        Assert.Equal(1, result.Quantity);
        Assert.Equal(1, result.LineId);
        Assert.Equal("Rehire Attributes", result.Attributes);
    }

    [Fact]
    public async Task Handle_WithDepotFulfilledReservation_ReturnsCorrectData()
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
            Division = "200",
            Facility = "USG",
            OrderSource = "ORF",
            Warehouse = "DEPOT_WH"
        };

        var testReservation = new Reservation
        {
            Id = 1,
            LineId = 1,
            AssetId = "DEPOTFULFIL",
            ItemNumber = "DEPOT_ITEM",
            Quantity = 5,
            Warehouse = "DEPOT_WH",
            LastUpdatedBy = "DepotUser",
            LastUpdatedDate = DateTime.UtcNow,
            IsDepotFulfilled = true,
            IsRehire = false,
            EffectiveQuantity = 5.0,
            IsConfirmed = true
        };

        await _dbContext.Headers.AddAsync(testHeader);
        await _dbContext.Lines.AddAsync(testLine);
        await _dbContext.Reservations.AddAsync(testReservation);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.Handle(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
        Assert.Equal("DEPOTFULFIL", result.AssetId);
        Assert.Equal("DEPOT_ITEM", result.ItemNumber);
        Assert.Equal(5, result.Quantity);
        Assert.Equal(1, result.LineId);
        Assert.Equal("Depot Fulfil Attributes", result.Attributes);
    }

    [Fact]
    public async Task Handle_WithEmptyAttributes_ReturnsNullAttributes()
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
            Attributes = null, // No attributes
            Quantity = 1,
            Division = "200",
            Facility = "USG",
            OrderSource = "ORF",
            Warehouse = "EMPTY_WH"
        };

        var testReservation = new Reservation
        {
            Id = 1,
            LineId = 1,
            AssetId = "EMPTY_ASSET",
            ItemNumber = "EMPTY_ITEM",
            Quantity = 1,
            Warehouse = "WH001",
            LastUpdatedBy = "EmptyUser",
            LastUpdatedDate = DateTime.UtcNow,
            IsDepotFulfilled = false,
            IsRehire = false,
            EffectiveQuantity = 1.0,
            IsConfirmed = false
        };

        await _dbContext.Headers.AddAsync(testHeader);
        await _dbContext.Lines.AddAsync(testLine);
        await _dbContext.Reservations.AddAsync(testReservation);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.Handle(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
        Assert.Equal("EMPTY_ASSET", result.AssetId);
        Assert.Equal("EMPTY_ITEM", result.ItemNumber);
        Assert.Equal(1, result.Quantity);
        Assert.Equal(1, result.LineId);
        Assert.Null(result.Attributes);
    }

}
