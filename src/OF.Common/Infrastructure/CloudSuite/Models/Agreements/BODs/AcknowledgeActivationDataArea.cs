using OF.Common.Infrastructure.CloudSuite.Models.Agreements.BODs.Common;
using OF.Common.Infrastructure.CloudSuite.Models.Agreements.BODs.Exceptions;
using OF.Common.Utils;
using System.ComponentModel.DataAnnotations;
using System.Xml.Serialization;

namespace OF.Common.Infrastructure.CloudSuite.Models.Agreements.BODs
{

    public class AGKRentalAgreementActivation
    {
        [Required]
        [XmlElement("agreementNumber", IsNullable = false)]
        public required string AgreementNumber { get; set; }

        [XmlElement("reasonCodeCreated", IsNullable = false)]
        public string? ReasonCodeCreated { get; set; }

        [XmlElement("orderRecordId", IsNullable = false)]
        public string? OrderRecordId { get; set; }

        [XmlElement("correlationId", IsNullable = false)]
        public string? CorrelationId { get; set; }

        [XmlElement("orderStatus", IsNullable = false)]
        public string? OrderStatus { get; set; }

        [XmlElement("technicalErrorMessage", IsNullable = false)]
        public string? TechnicalErrorMessage { get; set; }

        [XmlElement(ElementName = "UserArea")]
        public UserArea? UserArea { get; set; }
    }

    public class AcknowledgeActivationDataArea : BaseValidatable
    {
        public required Acknowledge Acknowledge { get; set; }

        [Required]
        public required AGKRentalAgreementActivation AGKRentalAgreementActivation { get; set; }

        public void Validate()
        {
            var errors = new List<ValidationResult>();

            Validate(AGKRentalAgreementActivation, out IList<ValidationResult> areaErrors);

            errors.AddRange(areaErrors);

            if (!AGKRentalAgreementActivation.AgreementNumber.IsTOrAAgreement())
            {
                errors.Add(new ValidationResult($"AgreementNumber '{AGKRentalAgreementActivation.AgreementNumber}' does not start with T or A."));
            }

            if (Acknowledge.ResponseCriteria.ResponseExpression.ActionCode == null)
            {
                errors.Add(new ValidationResult($"The ActionCode field is required."));
            }

            if (errors.Any())
            {
                throw new BodValidationException(nameof(AcknowledgeActivationDataArea), errors.Select(i => i.ErrorMessage!).Distinct());
            }
        }

        public string? GetTechnicalErrorMessage()
        {
            var technicalErrorMessage = AGKRentalAgreementActivation?.TechnicalErrorMessage;

            return string.IsNullOrWhiteSpace(technicalErrorMessage)
                ? GetValue("technicalErrorMessage")
                : technicalErrorMessage;
        }

        private string? GetValue(string name)
        {
            return AGKRentalAgreementActivation?.UserArea?.Properties?.FirstOrDefault(i => i.NameValue?.Name?.ToLower() == name.ToLower())?.NameValue?.Value;
        }
    }
}
