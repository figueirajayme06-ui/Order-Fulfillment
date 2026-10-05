using AutoFixture;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OF.Common;
using OF.Common.Extensions;
using OF.Data;
using OF.Data.Database;

namespace OF.Tests.Common.Extensions
{
    public class LineExtensionsTests
    {
        private readonly Fixture _fixture = new();
        private readonly Mock<ILogger> _mockLogger = new();

        private Line BuildLine(int? headerId = null)
        {
            return _fixture.Build<Line>()
                .Without(l => l.Id)
                .With(l => l.HeaderId, headerId)
                .With(l => l.IsDeleted, false)
                .With(l => l.Division, "200")
                .With(l => l.Facility, "FAC1")
                .With(l => l.Warehouse, "WH1")
                .With(l => l.OrderSource, "SF")
                .With(l => l.ValidFromDate, DateTime.UtcNow.AddDays(-7))
                .With(l => l.ValidToDate, DateTime.UtcNow.AddDays(30))
                .Without(l => l.Header)
                .Without(l => l.ChangeOrderLines)
                .Create();
        }

        private Header BuildHeader(string status)
        {
            return _fixture.Build<Header>()
                .Without(h => h.Id)
                .With(h => h.Status, status)
                .With(h => h.AgreementNumber, "T12345")
                .With(h => h.Division, "200")
                .With(h => h.Facility, "FAC1")
                .With(h => h.OrderSource, "SF")
                .Without(h => h.Lines)
                .Without(h => h.ChangeOrderHeaders)
                .Without(h => h.ChangeOrders)
                .Create();
        }

        [Fact]
        public async Task MarkAsDeletedAndRemoveReservationsAsync_WithMultipleReservations_ShouldDeleteReservationsAndMarkLineAsDeleted()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            await using var context = new ApplicationDbContext(options);

            var line = BuildLine();
            context.Lines.Add(line);
            await context.SaveChangesAsync();

            var reservations = _fixture.Build<Reservation>()
                .With(r => r.LineId, line.Id)
                .CreateMany(3)
                .ToList();

            context.Reservations.AddRange(reservations);
            await context.SaveChangesAsync();

            var originalAgreementLineNumber = line.AgreementLineNumber;

            // Act
            var result = await line.MarkAsDeletedAndRemoveReservationsAsync(context, _mockLogger.Object);
            await context.SaveChangesAsync(); // Persist deletions as the caller would

            // Assert
            result.Should().Be(3);
            line.IsDeleted.Should().BeTrue();
            line.AgreementLineNumber.Should().NotBe(originalAgreementLineNumber);
            line.AgreementLineNumber.Should().StartWith(originalAgreementLineNumber + "_");

            var remainingReservations = await context.Reservations
                .Where(r => r.LineId == line.Id)
                .ToListAsync();
            remainingReservations.Should().BeEmpty();

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Marked line") && v.ToString()!.Contains("as deleted and removed 3 associated reservations")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task MarkAsDeletedAndRemoveReservationsAsync_WithNoReservations_ShouldMarkLineAsDeletedAndReturnZero()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            await using var context = new ApplicationDbContext(options);

            var line = BuildLine();
            context.Lines.Add(line);
            await context.SaveChangesAsync();

            var originalAgreementLineNumber = line.AgreementLineNumber;

            // Act
            var result = await line.MarkAsDeletedAndRemoveReservationsAsync(context, _mockLogger.Object);
            await context.SaveChangesAsync(); // Persist deletions as the caller would

            // Assert
            result.Should().Be(0);
            line.IsDeleted.Should().BeTrue();
            line.AgreementLineNumber.Should().NotBe(originalAgreementLineNumber);
            line.AgreementLineNumber.Should().StartWith(originalAgreementLineNumber + "_");

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Marked line") && v.ToString()!.Contains("as deleted and removed 0 associated reservations")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Theory]
        [InlineData(Constants.HeaderStatus.Terminated)]
        [InlineData(Constants.HeaderStatus.Invoiced)]
        [InlineData(Constants.HeaderStatus.Completed)]
        public async Task MarkAsDeletedAndRemoveReservationsAsync_WithHistoricalHeader_ShouldPreserveReservations(string headerStatus)
        {
            // Arrange
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            await using var context = new ApplicationDbContext(options);

            var header = BuildHeader(headerStatus);
            context.Headers.Add(header);
            await context.SaveChangesAsync();

            var line = BuildLine(headerId: header.Id);
            context.Lines.Add(line);
            await context.SaveChangesAsync();

            var reservations = _fixture.Build<Reservation>()
                .With(r => r.LineId, line.Id)
                .CreateMany(3)
                .ToList();

            context.Reservations.AddRange(reservations);
            await context.SaveChangesAsync();

            var originalAgreementLineNumber = line.AgreementLineNumber;

            // Act
            var result = await line.MarkAsDeletedAndRemoveReservationsAsync(context, _mockLogger.Object);
            await context.SaveChangesAsync(); // Persist deletions as the caller would

            // Assert
            result.Should().Be(0, "reservations should be preserved for historical orders");
            line.IsDeleted.Should().BeTrue();
            line.AgreementLineNumber.Should().StartWith(originalAgreementLineNumber + "_");

            var remainingReservations = await context.Reservations
                .Where(r => r.LineId == line.Id)
                .ToListAsync();
            remainingReservations.Should().HaveCount(3, "all reservations should be preserved for historical orders");

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Preserving reservations for historical order")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Theory]
        [InlineData(Constants.HeaderStatus.Created)]
        [InlineData(Constants.HeaderStatus.OnHire)]
        [InlineData(Constants.HeaderStatus.LineCreated)]
        public async Task MarkAsDeletedAndRemoveReservationsAsync_WithNonHistoricalHeader_ShouldRemoveReservations(string headerStatus)
        {
            // Arrange
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            await using var context = new ApplicationDbContext(options);

            var header = BuildHeader(headerStatus);
            context.Headers.Add(header);
            await context.SaveChangesAsync();

            var line = BuildLine(headerId: header.Id);
            context.Lines.Add(line);
            await context.SaveChangesAsync();

            var reservations = _fixture.Build<Reservation>()
                .With(r => r.LineId, line.Id)
                .CreateMany(2)
                .ToList();

            context.Reservations.AddRange(reservations);
            await context.SaveChangesAsync();

            // Act
            var result = await line.MarkAsDeletedAndRemoveReservationsAsync(context, _mockLogger.Object);
            await context.SaveChangesAsync(); // Persist deletions as the caller would

            // Assert
            result.Should().Be(2);
            line.IsDeleted.Should().BeTrue();

            var remainingReservations = await context.Reservations
                .Where(r => r.LineId == line.Id)
                .ToListAsync();
            remainingReservations.Should().BeEmpty("reservations should be removed for non-historical orders");
        }

        [Fact]
        public async Task MarkAsDeletedAndRemoveReservationsAsync_WithNoHeader_ShouldRemoveReservations()
        {
            // Arrange - line with no HeaderId (e.g. quote line)
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            await using var context = new ApplicationDbContext(options);

            var line = BuildLine(headerId: null);
            context.Lines.Add(line);
            await context.SaveChangesAsync();

            var reservations = _fixture.Build<Reservation>()
                .With(r => r.LineId, line.Id)
                .CreateMany(2)
                .ToList();

            context.Reservations.AddRange(reservations);
            await context.SaveChangesAsync();

            // Act
            var result = await line.MarkAsDeletedAndRemoveReservationsAsync(context, _mockLogger.Object);
            await context.SaveChangesAsync(); // Persist deletions as the caller would

            // Assert
            result.Should().Be(2, "reservations should be removed when there is no header");
            line.IsDeleted.Should().BeTrue();

            var remainingReservations = await context.Reservations
                .Where(r => r.LineId == line.Id)
                .ToListAsync();
            remainingReservations.Should().BeEmpty();
        }
    }
}
