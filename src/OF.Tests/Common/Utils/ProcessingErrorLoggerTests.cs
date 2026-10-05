using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OF.Common.Utils;
using OF.Data;
using OF.Tests.AutoFixture;

namespace OF.Tests.Common.Utils
{
    [Collection("DatabaseCollection")]
    public class ProcessingErrorLoggerTests
    {
        private readonly DatabaseFixture _fixture;

        public ProcessingErrorLoggerTests(DatabaseFixture fixture)
        {
            _fixture = fixture;
        }

        [Theory]
        [AutoData]
        public async Task LogErrorAsync_ShouldLogErrorToDatabase(
            string processName,
            string processId,
            string recordId,
            string recordType,
            string errorMessage,
            string errorDetails)
        {
            // Arrange
            var dbContextFactory = _fixture.SetupDbContext(nameof(LogErrorAsync_ShouldLogErrorToDatabase));
            using var context = await dbContextFactory.CreateDbContextAsync();
            var logger = new Mock<ILogger>();

            // Act
            await ProcessingErrorLogger.LogErrorAsync(
                context,
                logger.Object,
                processName,
                processId,
                recordId,
                recordType,
                errorMessage,
                errorDetails);

            // Assert
            var savedError = await context.ProcessingErrors.FirstOrDefaultAsync();
            Assert.NotNull(savedError);
            Assert.Equal(processName, savedError.ProcessName);
            Assert.Equal(processId, savedError.ProcessId);
            Assert.Equal(recordId, savedError.RecordId);
            Assert.Equal(recordType, savedError.RecordType);
            Assert.Equal(errorMessage, savedError.ErrorMessage);
            Assert.Equal(errorDetails, savedError.ErrorDetails);
        }

        [Theory]
        [AutoData]
        public async Task LogErrorAsync_WithException_ShouldLogErrorToDatabase(
            string processName,
            string processId,
            string recordId,
            string recordType)
        {
            // Arrange
            var dbContextFactory = _fixture.SetupDbContext(nameof(LogErrorAsync_WithException_ShouldLogErrorToDatabase));
            using var context = await dbContextFactory.CreateDbContextAsync();
            var logger = new Mock<ILogger>();
            var exception = new InvalidOperationException("Test exception");

            // Act
            await ProcessingErrorLogger.LogErrorAsync(
                context,
                logger.Object,
                processName,
                processId,
                recordId,
                recordType,
                exception);

            // Assert
            var savedError = await context.ProcessingErrors.FirstOrDefaultAsync();
            Assert.NotNull(savedError);
            Assert.Equal(processName, savedError.ProcessName);
            Assert.Equal(processId, savedError.ProcessId);
            Assert.Equal(recordId, savedError.RecordId);
            Assert.Equal(recordType, savedError.RecordType);
            Assert.Equal("Test exception", savedError.ErrorMessage);
            Assert.Contains("InvalidOperationException", savedError.ErrorDetails);
        }

        [Fact]
        public async Task LogErrorAsync_WhenDatabaseFails_ShouldNotThrow()
        {
            // Arrange
            var mockContext = new Mock<ApplicationDbContext>();
            var logger = new Mock<ILogger>();
            mockContext.Setup(x => x.SaveChangesAsync(default))
                .ThrowsAsync(new InvalidOperationException("Database error"));

            // Act & Assert - Should not throw
            await ProcessingErrorLogger.LogErrorAsync(
                mockContext.Object,
                logger.Object,
                "QuoteSync",
                "test-id",
                "record-1",
                "Opportunity",
                "Test error",
                "Test details");
        }
    }
}
