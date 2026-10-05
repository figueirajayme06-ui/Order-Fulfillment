using OF.Common.Infrastructure.CloudSuite.Models.Agreements.BODs.Common;
using OF.Common.Infrastructure.CloudSuite.Models.Agreements.BODs.Exceptions;
using OF.Common.Utils;
using System.ComponentModel.DataAnnotations;
using System.Xml.Serialization;

namespace OF.Common.Infrastructure.CloudSuite.Models.Agreements.BODs
{
    public class DetailLines
    {
        [XmlElement("agreementNumber", IsNullable = false)]
        public required string AgreementNumber { get; set; }

        [XmlElement("agreementLineNumber", IsNullable = false)]
        public string? AgreementLineNumber { get; set; }

        [XmlElement("orderItemRecordId", IsNullable = false)]
        public string? OrderItemRecordId { get; set; }

        [XmlElement("CorrelationId", IsNullable = false)]
        public string? CorrelationId { get; set; }

        [XmlElement("correlationId", IsNullable = false)]
        public string? LoweredCorrelationId { get; set; }

        [XmlElement("technicalErrorMessage", IsNullable = false)]
        public string? TechnicalErrorMessage { get; set; }
    }

    public class AGKRentalAgreementLines
    {
        [XmlElement(ElementName = "detailLines")]
        public DetailLines? DetailLines { get; set; }

        [XmlElement(ElementName = "UserArea")]
        public UserArea? UserArea { get; set; }
    }

    public class AcknowledgeLineDataArea : BaseValidatable
    {
        public required Acknowledge Acknowledge { get; set; }

        [Required]
        public required AGKRentalAgreementLines? AGKRentalAgreementLines { get; set; }

        public void Validate()
        {
            var errors = new List<ValidationResult>();

            var agreementNumber = GetAgreementNumber();
            if (!agreementNumber.IsTOrAAgreement())
            {
                errors.Add(new ValidationResult($"AgreementNumber 'agreementNumber' does not start with T or A."));
            }

            if (Acknowledge.ResponseCriteria.ResponseExpression.ActionCode == null)
            {
                errors.Add(new ValidationResult($"The ActionCode field is required."));
            }

            if (errors.Any())
            {
                throw new BodValidationException(nameof(AcknowledgeLineDataArea), errors.Select(i => i.ErrorMessage!).Distinct());
            }
        }

        public string? GetAgreementNumber() => AGKRentalAgreementLines?.DetailLines?.AgreementNumber ?? GetValue("agreementNumber");

        public string? GetCorrelationId()
        {
            string? correlationId = null;

            if (!string.IsNullOrWhiteSpace(AGKRentalAgreementLines?.DetailLines?.CorrelationId))
            {
                correlationId = AGKRentalAgreementLines?.DetailLines?.CorrelationId;
            }

            if (string.IsNullOrWhiteSpace(correlationId) && !string.IsNullOrWhiteSpace(AGKRentalAgreementLines?.DetailLines?.LoweredCorrelationId))
            {
                correlationId = AGKRentalAgreementLines?.DetailLines?.LoweredCorrelationId;
            }

            if (string.IsNullOrWhiteSpace(correlationId))
            {
                correlationId = GetValue("CorrelationId");
            }

            return correlationId;
        }

        public string? GetOrderItemRecordId() => AGKRentalAgreementLines?.DetailLines?.OrderItemRecordId ?? GetValue("orderItemRecordId");

        public string? GetTechnicalErrorMessage()
        {
            var technicalErrorMessage = AGKRentalAgreementLines?.DetailLines?.TechnicalErrorMessage;

            return string.IsNullOrWhiteSpace(technicalErrorMessage)
                ? GetValue("technicalErrorMessage")
                : technicalErrorMessage;
        }

        public int GetLineNumber()
        {
            if (int.TryParse(AGKRentalAgreementLines?.DetailLines?.AgreementLineNumber, out int lineNumber))
            {
                return lineNumber;
            }

            int.TryParse(GetValue("agreementLineNumber"), out lineNumber);
            return lineNumber;
        }

        public bool RetryCreateDueToRaceConditionWhereHeaderIsAlreadyABeforeLine()
        {
            var error = GetTechnicalErrorMessage();

            if (error?.ToUpper()?.Contains("XAD0001") == true)
            {
                return true;
            }

            return false;
        }

        public string? GetAgreementLineNumber() => $"{GetAgreementNumber()}-{GetLineNumber()}";

        private string? GetValue(string name)
        {
            return AGKRentalAgreementLines?.UserArea?.Properties?.FirstOrDefault(i => i.NameValue?.Name?.ToLower() == name.ToLower())?.NameValue?.Value;
        }
    }
}
