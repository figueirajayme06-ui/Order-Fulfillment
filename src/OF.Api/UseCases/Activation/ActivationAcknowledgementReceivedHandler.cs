using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OF.Common.Infrastructure.CloudSuite.Models.Agreements.BODs;
using OF.Common.Infrastructure.CloudSuite.Models.Agreements.BODs.Exceptions;
using OF.Common.Utils;
using OF.Data;
using static OF.Common.Enums;

namespace OF.Api.UseCases.Activation
{
    public class ActivationAcknowledgementReceivedHandler : IBodHandler
    {
        private readonly ApplicationDbContext dbContext;
        private readonly ILogger logger;

        public ActivationAcknowledgementReceivedHandler(ApplicationDbContext dbContext, ILogger logger)
        {
            this.dbContext = dbContext;
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
                return bod?.DataArea.AGKRentalAgreementActivation.AgreementNumber.Substring(1) ?? string.Empty;
            }
            catch
            {
                // Swallow exception for logging purposes
                return string.Empty;
            }
        }

        public async Task Handle(string body)
        {
            using var _ = logger.BeginScope(new {Action = nameof(ActivationAcknowledgementReceivedHandler), SessionId = GetSessionId(body) });
            logger.LogInformation($"AcknowledgementReceivedHandler fired for message.");
            logger.LogInformation(body);

            AcknowledgeAGKRentalAgreementActivation? bod = null;

            try
            {
                bod = GetBodFromMessage(body);

                if (bod == null)
                {
                    logger.LogWarning("Acknowledgement BOD is malformed and contains no DataArea.");
                    return;
                }

                string agreementNumber = bod.DataArea.AGKRentalAgreementActivation.AgreementNumber;
                var header = await dbContext.Headers.Include(i => i.Lines).FirstOrDefaultAsync(h => h.AgreementNumbersOnly == agreementNumber.Substring(1));

                if (header == null)
                {
                    logger.LogWarning($"No header found for acknowledged agreement {agreementNumber}");
                    return;
                }

                if (header.ActivationStatus == (int)ActivationStatus.Activated)
                {
                    logger.LogWarning($"Header has already been activated for agreement {agreementNumber}");
                    header.ActivationErrors = null;
                    return;
                }

                try
                {
                    if (bod.DataArea.Acknowledge.ResponseCriteria.ResponseExpression.ActionCode.ToUpper() == "REJECTED")
                    {
                        var error = bod.DataArea.GetTechnicalErrorMessage() ?? "Error in processing in M3, please check for activation error";

                        header.ActivationStatus = (int)ActivationStatus.Failed;
                        header.LastUpdatedDate = DateTime.UtcNow;
                        var issue = $"{DateTime.UtcNow.ToString("yyyyMMddHHmmss")}: Fail[Acknowledge]: {error};";
                        header.ActivationErrors = issue;
                    }
                    else
                    {
                        header.ActivationStatus = (int)ActivationStatus.Activated;
                        header.ActivationErrors = null;
                        header.LastUpdatedDate = DateTime.UtcNow;
                    }

                    await dbContext.SaveChangesAsync();
                }
                catch (Exception e)
                {
                    var issue = $"{DateTime.UtcNow.ToString("yyyyMMddHHmmss")}: Fail[Acknowledge]: {e.GetBaseException().Message};";
                    header.ActivationErrors = issue;

                    await dbContext.SaveChangesAsync();

                    throw;
                }
            }
            catch (BodValidationException exception)
            {
                logger.LogError("Bod validation error for Acknowledgement exception", exception);
                logger.LogInformation("Body Error: " + body);
            }
            catch (Exception exception)
            {
                logger.LogError("Error while deserializing BOD for Acknowledgement message.", exception);
                logger.LogInformation("Body Error: " + body);
            }
        }

        private static AcknowledgeAGKRentalAgreementActivation? GetBodFromMessage(string body)
        {
            if (body?.ToLower()?.Contains("<dataarea>") != true)
            {
                return null;
            }

            AcknowledgeAGKRentalAgreementActivation bod = body.ParseToBODResponse<AcknowledgeAGKRentalAgreementActivation>();
            bod.DataArea.Validate();
            return bod;
        }
    }
}
