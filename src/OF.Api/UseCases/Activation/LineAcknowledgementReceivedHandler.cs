using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OF.Common.Infrastructure.CloudSuite.Models.Agreements.BODs;
using OF.Common.Infrastructure.CloudSuite.Models.Agreements.BODs.Exceptions;
using OF.Common.Infrastructure.IPG.Orders;
using OF.Common.Utils;
using OF.Data;
using OF.Data.Database;
using static OF.Common.Enums;

namespace OF.Api.UseCases.Activation
{
    public class LineAcknowledgementReceivedHandler : IBodHandler
    {
        private readonly ApplicationDbContext dbContext;
        private readonly OrderManagementService orderIntegration;
        private readonly ILogger logger;

        public LineAcknowledgementReceivedHandler(ApplicationDbContext dbContext, OrderManagementService orderIntegration, ILogger logger)
        {
            this.dbContext = dbContext;
            this.orderIntegration = orderIntegration;
            this.logger = logger;
        }

        public static string GetSessionId(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return string.Empty;
            }

            try
            {
                var bod = GetBodFromMessage(body);
                return bod?.DataArea?.GetAgreementNumber()?.Substring(1) ?? string.Empty;
            }
            catch
            {
                // Swallow exception for logging purposes
                return string.Empty;
            }
        }

        public async Task Handle(string body)
        {
            using var _ = logger.BeginScope(new { Action = nameof(LineAcknowledgementReceivedHandler), SessionId = GetSessionId(body) });
            logger.LogInformation($"=== LineAcknowledgementReceivedHandler STARTED ===");
            logger.LogInformation($"LineAcknowledgementReceivedHandler fired for message.");
            logger.LogInformation(body);

            AcknowledgeAGKRentalAgreementLines? bod = null;

            try
            {
                bod = GetBodFromMessage(body);

                if (bod == null)
                {
                    logger.LogWarning("Acknowledgement line BOD is malformed and contains no DataArea.");
                    return;
                }

                string? agreementLineNumbersOnly = bod.DataArea.GetAgreementLineNumber()?.Substring(1);
                Line? line = null;

                if (agreementLineNumbersOnly == null)
                {
                    logger.LogWarning("No agreement Line number found in the message.");
                }
                else
                {
                    line = await dbContext.Lines.Include(i => i.Header).FirstOrDefaultAsync(h => h.AgreementLineNumber == "T" + agreementLineNumbersOnly || h.AgreementLineNumber == "A" + agreementLineNumbersOnly);
                }

                var correlationId = bod.DataArea.GetCorrelationId();

                if (line == null && !string.IsNullOrWhiteSpace(correlationId))
                {
                    line = await dbContext.Lines.Include(i => i.Header).FirstOrDefaultAsync(h => h.ActivationInstanceId == correlationId);
                }

                if (line == null)
                { 
                    logger.LogWarning($"No Line found for acknowledged agreement line (T,A){agreementLineNumbersOnly}");
                    return;
                }

                if (line.ActivationStatus == (int)ActivationStatus.Activated)
                {
                    logger.LogWarning($"Line has already been activated for agreement {agreementLineNumbersOnly}");
                    line.ActivationErrors = null;
                    return;
                }

                try
                {
                    if (bod.DataArea.Acknowledge.ResponseCriteria.ResponseExpression.ActionCode.ToUpper() == "REJECTED")
                    {
                        await RetryOrLogAsFailed(agreementLineNumbersOnly, bod, line);
                    }
                    else
                    {
                        line.AgreementLineNumber = bod.DataArea.GetAgreementLineNumber();
                        line.AgreementNumbersOnly = bod.DataArea.GetAgreementNumber()?.Substring(1);
                        line.ActivationStatus = (int)ActivationStatus.Activated;
                        line.ActivationErrors = null;
                        line.LastUpdatedDate = DateTime.UtcNow;
                    }

                    await dbContext.SaveChangesAsync();
                }
                catch (Exception e)
                {
                    var issue = $"{DateTime.UtcNow.ToString("yyyyMMddHHmmss")}: Fail[LineAcknowledge]: {e.GetBaseException().Message};";
                    line.ActivationErrors = issue;

                    await dbContext.SaveChangesAsync();

                    throw;
                }
            }
            catch (BodValidationException exception)
            {
                logger.LogError("Bod validation error for Line Acknowledgement exception", exception);
                logger.LogInformation("Body Error: " + body);
            }
            catch (Exception exception)
            {
                logger.LogError("Error while deserializing BOD for Line Acknowledgement message.", exception);
                logger.LogInformation("Body Error: " + body);
            }
        }

        private async Task RetryOrLogAsFailed(string? agreementLineNumbersOnly, AcknowledgeAGKRentalAgreementLines bod, Line line)
        {
            var error = bod.DataArea.GetTechnicalErrorMessage() ?? "Error in processing in M3, please check for line activation error";

            if (bod.DataArea.RetryCreateDueToRaceConditionWhereHeaderIsAlreadyABeforeLine()) // Code XAD00001
            {
                logger.LogInformation("Retrying activation as got error: " + error);

                var reservation = dbContext.Reservations.First(r => r.LineId == line.Id);

                line.ActivationStatus = (int)ActivationStatus.Requested;
                line.LastUpdatedDate = DateTime.UtcNow;

                try
                {
                    await orderIntegration.CreateLine(line.Header!, line, reservation);
                }
                catch (Exception e)
                {
                    logger.LogError($"Error while attempting retry on creating line {agreementLineNumbersOnly}.", e);
                    line.ActivationStatus = (int)ActivationStatus.Failed;
                    line.LastUpdatedDate = DateTime.UtcNow;
                    error = e.GetBaseException().Message;
                }

                var issue = $"{DateTime.UtcNow.ToString("yyyyMMddHHmmss")}: Fail[LineAcknowledgeRetrying]: {error};";
                line.ActivationErrors = issue;
            }
            else
            {
                line.ActivationStatus = (int)ActivationStatus.Failed;
                line.LastUpdatedDate = DateTime.UtcNow;
                var issue = $"{DateTime.UtcNow.ToString("yyyyMMddHHmmss")}: Fail[LineAcknowledge]: {error};";
                line.ActivationErrors = issue;
            }
        }

        private static AcknowledgeAGKRentalAgreementLines? GetBodFromMessage(string body)
        {
            if (body?.ToLower()?.Contains("<dataarea>") != true)
            {
                return null;
            }

            AcknowledgeAGKRentalAgreementLines bod = body.ParseToBODResponse<AcknowledgeAGKRentalAgreementLines>();
            bod.DataArea.Validate();
            return bod;
        }
    }
}
