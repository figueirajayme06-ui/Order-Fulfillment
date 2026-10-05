using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OF.Common;
using OF.Common.Infrastructure.IPG.Orders;
using OF.Common.Infrastructure.Storage;
using OF.Data;
using OF.Data.Database;
using System.Diagnostics.CodeAnalysis;
using static OF.Common.Enums;

namespace OF.Api.UseCases.Agreements
{
    public record ActivateHeaderRequest(
        BinaryData Content,
        DateTimeOffset SentTimestamp,
        IReadOnlyDictionary<string, object>? Properties);

    public class ActivateHeaderHandler
    {
        private readonly ApplicationDbContext dbContext;
        private readonly OrderManagementService orderIntegration;
        private readonly IActivateHeaderQueueClient headerQueue;
        private readonly TimeProvider timeProvider;
        private readonly ILogger logger;

        public ActivateHeaderHandler(
            ApplicationDbContext dbContext,
            OrderManagementService orderIntegration,
            IActivateHeaderQueueClient headerQueue,
            TimeProvider timeProvider,
            ILogger logger)
        {
            this.dbContext = dbContext;
            this.orderIntegration = orderIntegration;
            this.headerQueue = headerQueue;
            this.timeProvider = timeProvider;
            this.logger = logger;
        }

        public async Task Handle(ActivateHeaderRequest request, CancellationToken cancellationToken = default)
        {
            var content = request.Content.ToObjectFromJson<ActivateHeaderContent>()!;

            logger.LogInformation($"Activating header now {content.HeaderId}.");

            var header = await dbContext.Headers
                                .Include(i => i.Lines)
                                .FirstAsync(i => i.Id == content.HeaderId, cancellationToken);

            // If this is already activated then, mark activate and clear errors

            if (header.IsActivated)
            {
                logger.LogInformation($"Agreement {header.AgreementNumber} has already been activated.");
                header.ActivationStatus = (int)ActivationStatus.Activated;
                header.ActivationErrors = null;
                header.LastUpdatedDate = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            // If this is not ready to be activated as not fulfilled, then log as such and mark for retry once fulfilled

            if (header.FulfilmentStatus == (int)FulfilmentStatus.Unfulfilled || header.FulfilmentStatus == (int)FulfilmentStatus.PartiallyFulfilled)
            {
                var issue = $"{DateTime.UtcNow.ToString("yyyyMMddHHmmss")}: Fail[ActivateHeader]: Header is at fulfilment status {((FulfilmentStatus)header.FulfilmentStatus)}. Please retry once fully fulfilled;";
                header.ActivationStatus = (int)ActivationStatus.Failed;
                header.ActivationErrors = issue;
                header.LastUpdatedDate = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            // Mark as failed if the lines which required fulfilment have any errors

            var lines = header.Lines.Where(i => i.IsDeleted == false && i.RequiresFulfilment == true);
            var linesWithErrors = lines.Where(i => i.ActivationStatus == (int)ActivationStatus.Failed);

            if (linesWithErrors.Any())
            {
                var issue = $"Agreement header {header.AgreementNumber} cannot be activated as some of the lines have failed. Please retry once all lines are resolved.";
                logger.LogInformation(issue);
                header.ActivationStatus = (int)ActivationStatus.Failed;
                header.ActivationErrors = issue;
                header.LastUpdatedDate = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            // Mark as failed if any new lines have been added that have not yet been activated

            var linesNotRequestedToBeActivate = lines.Where(i => i.ActivationStatus == (int)ActivationStatus.TODO);

            if (linesNotRequestedToBeActivate.Any())
            {
                var issue = $"Agreement header {header.AgreementNumber} cannot be activated as some of the lines have not yet requested to be activate. Please retry once fully fulfilled.";
                logger.LogInformation(issue);
                header.ActivationStatus = (int)ActivationStatus.Failed;
                header.ActivationErrors = issue;
                header.LastUpdatedDate = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            // Reschedule back on the queue if lines are still in flight

            var linesPendingActivation = lines.Where(i => i.ActivationStatus == (int)ActivationStatus.Requested).ToList();

            if (linesPendingActivation.Any())
            {
                var agreementLines = string.Join(", ", linesPendingActivation.Select(x => x.AgreementLineNumber));

                if (ShouldRetry(request, out var propertiesToSend))
                {
                    logger.LogInformation($"Agreement header {header.AgreementNumber} still has lines {agreementLines} pending activation, requeuing.");
                    header.ActivationStatus = (int)ActivationStatus.Requested;
                    header.ActivationErrors = null;
                    header.LastUpdatedDate = DateTime.UtcNow;
                    await headerQueue.QueueActivation(content.HeaderId, propertiesToSend, cancellationToken);
                }
                else
                {
                    logger.LogInformation($"Agreement header {header.AgreementNumber} could not be activated in time! Activation stalled with pending lines [{agreementLines}].");
                    header.ActivationStatus = (int)ActivationStatus.Failed;
                    header.ActivationErrors = "Activation failed: unable to activate line";
                    header.ActivationErrors += linesPendingActivation.Count > 1 ? $"s [{agreementLines}]" : $" [{agreementLines}]";
                    header.ActivationErrors += ". Please retry once all lines are resolved.";
                    header.LastUpdatedDate = DateTime.UtcNow;

                    foreach (var line in linesPendingActivation)
                    {
                        line.ActivationStatus = (int)ActivationStatus.Failed;
                        line.ActivationErrors = "Activation failed: activation wasn't done in time!";
                        line.LastUpdatedDate = DateTime.UtcNow;
                    }
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            // Actually request activation if it all looks good
            logger.LogInformation($"Agreement header {header.AgreementNumber} is good to go, requesting activation.");
            header.ActivationStatus = (int)ActivationStatus.Requested;
            header.ActivationErrors = null;
            header.LastUpdatedDate = DateTime.UtcNow;
            await orderIntegration.ActivateOrder(header);
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        private bool ShouldRetry(
            ActivateHeaderRequest request,
            [NotNullWhen(true)] out IDictionary<string, object>? propertiesToSend)
        {
            const string timestampKey = Constants.Message.Properties.ActivationTimestampKey;
            const string retryCounterKey = Constants.Message.Properties.ActivationRetryCounterKey;
            var graceTime = TimeSpan.FromMinutes(5);
            const int maxAttempts = 4;
            propertiesToSend = null;
            var now = timeProvider.GetUtcNow();

            if (request.Properties?.TryGetValue(timestampKey, out DateTimeOffset sentTime) != true ||
                sentTime > now)
            {
                sentTime = request.SentTimestamp;
                logger.LogInformation($"No valid activation timestamp associated with key [{timestampKey}] found on the message, defaulting to the message sent timestamp {sentTime:O}.");
            }

            if (now - sentTime < graceTime)
            {
                propertiesToSend = new Dictionary<string, object>()
                {
                    { timestampKey, sentTime! }
                };
                return true;
            }

            if (request.Properties?.TryGetValue(retryCounterKey, out int retried) != true ||
                retried < 0)
            {
                retried = 0;
                logger.LogInformation($"No valid activation retry counter associated with key [{retryCounterKey}] found on the message, defaulting to {retried}.");
            }

            var retrying = ++retried < maxAttempts;

            if (retrying)
            {
                propertiesToSend = new Dictionary<string, object>()
                {
                    { timestampKey, sentTime },
                    { retryCounterKey, retried }
                };
                logger.LogInformation($"Pending activation grace period of {graceTime:c} exceeded, retrying {retried}/{maxAttempts - 1}.");
            }
            else
            {
                logger.LogError($"Activation timed out after a grace period of {graceTime:c} and after reaching a maximum of {maxAttempts - 1} retries without success!");
            }

            return retrying;
        }

        private class ActivateHeaderContent
        {
            public int HeaderId { get; set; }
        }
    }
}
