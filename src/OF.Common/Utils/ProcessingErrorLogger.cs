using Microsoft.Extensions.Logging;
using OF.Data;
using OF.Data.Database;

namespace OF.Common.Utils
{
    /// <summary>
    /// Simple utility for logging processing errors to the database.
    /// Can be used by any process that needs to track errors.
    /// </summary>
    public static class ProcessingErrorLogger
    {
        /// <summary>
        /// Logs an error to the ProcessingErrors table without throwing exceptions.
        /// </summary>
        /// <param name="dbContext">Database context to use for logging</param>
        /// <param name="logger">Logger for diagnostics</param>
        /// <param name="processName">Name of the process (e.g., "QuoteSync")</param>
        /// <param name="processId">Optional process instance ID</param>
        /// <param name="recordId">ID of the record being processed</param>
        /// <param name="recordType">Type of record (e.g., "Opportunity", "Quote")</param>
        /// <param name="errorMessage">Brief error message</param>
        /// <param name="errorDetails">Detailed error information</param>
        public static async Task LogErrorAsync(
            ApplicationDbContext dbContext,
            ILogger logger,
            string processName,
            string? processId,
            string? recordId,
            string? recordType,
            string errorMessage,
            string? errorDetails = null)
        {
            try
            {
                var error = new ProcessingError
                {
                    ProcessName = processName,
                    ProcessId = processId,
                    RecordId = recordId,
                    RecordType = recordType,
                    ErrorMessage = errorMessage,
                    ErrorDetails = errorDetails,
                    ProcessedAt = DateTime.UtcNow,
                    CreatedBy = processName
                };

                dbContext.ProcessingErrors.Add(error);
                await dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Don't throw here to avoid breaking the main process
                logger.LogError(ex, "Failed to log processing error to database for {ProcessName}", processName);
            }
        }

        /// <summary>
        /// Logs an error from an exception to the ProcessingErrors table.
        /// </summary>
        /// <param name="dbContext">Database context to use for logging</param>
        /// <param name="logger">Logger for diagnostics</param>
        /// <param name="processName">Name of the process</param>
        /// <param name="processId">Optional process instance ID</param>
        /// <param name="recordId">ID of the record being processed</param>
        /// <param name="recordType">Type of record</param>
        /// <param name="exception">The exception that occurred</param>
        public static async Task LogErrorAsync(
            ApplicationDbContext dbContext,
            ILogger logger,
            string processName,
            string? processId,
            string? recordId,
            string? recordType,
            Exception exception)
        {
            await LogErrorAsync(
                dbContext,
                logger,
                processName,
                processId,
                recordId,
                recordType,
                exception.Message,
                exception.ToString());
        }
    }
}
