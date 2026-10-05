using AutoFixture;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OF.Common.Extensions;
using OF.Data;
using OF.Data.Database;

namespace OF.Tests.Common.Extensions
{
    public class LineExtensionsTests
    {
        private readonly Fixture _fixture = new();
        private readonly Mock<ILogger> _mockLogger = new();

        [Fact]
        public async Task MarkAsDeletedAndRemoveReservationsAsync_WithMultipleReservations_ShouldDeleteReservationsAndMarkLineAsDeleted()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            await using var context = new ApplicationDbContext(options);

            var quote = _fixture.Create<Quote>();
            var line = _fixture.Build<Line>()
                .With(l => l.QuoteId, quote.Id)
                .With(l => l.IsDeleted, false)
                .Create();

            var reservations = _fixture.Build<Reservation>()
                .With(r => r.LineId, line.Id)
                .CreateMany(3)
                .ToList();

            context.Quotes.Add(quote);
            context.Lines.Add(line);
            context.Reservations.AddRange(reservations);
            await context.SaveChangesAsync();

            var originalAgreementLineNumber = line.AgreementLineNumber;

            // Act
            var result = await line.MarkAsDeletedAndRemoveReservationsAsync(context, _mockLogger.Object);

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

            var quote = _fixture.Create<Quote>();
            var line = _fixture.Build<Line>()
                .With(l => l.QuoteId, quote.Id)
                .With(l => l.IsDeleted, false)
                .Create();

            context.Quotes.Add(quote);
            context.Lines.Add(line);
            await context.SaveChangesAsync();

            var originalAgreementLineNumber = line.AgreementLineNumber;

            // Act
            var result = await line.MarkAsDeletedAndRemoveReservationsAsync(context, _mockLogger.Object);

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
    }
}
