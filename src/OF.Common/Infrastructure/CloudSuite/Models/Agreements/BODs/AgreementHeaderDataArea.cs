using OF.Common.Infrastructure.CloudSuite.Models.Agreements.BODs.Common;
using OF.Common.Infrastructure.CloudSuite.Models.Agreements.BODs.Exceptions;
using OF.Common.Utils;
using OF.Data.Database;
using System.ComponentModel.DataAnnotations;

namespace OF.Common.Infrastructure.CloudSuite.Models.Agreements.BODs
{
    public class AGKRentalOrderHeader
    {
        [Required]
        public required AgreementHeaderData AgreementHeaders { get; set; }
    }

    public class AgreementHeaderDataArea : BaseValidatable
    {
        public required Sync Sync { get; set; }

        [Required]
        public required AGKRentalOrderHeader AGKRentalOrderHeader { get; set; }

        public long Validate(Header? entity)
        {
            var errors = new List<ValidationResult>();

            Validate(AGKRentalOrderHeader, out IList<ValidationResult> areaErrors);
            Validate(AGKRentalOrderHeader.AgreementHeaders, out IList<ValidationResult> headerErrors);

            errors.AddRange(areaErrors);
            errors.AddRange(headerErrors);

            var changeSequenceDatePart = AGKRentalOrderHeader.AgreementHeaders.ChangeSequence?.Split("|")[0];
            if (!long.TryParse(changeSequenceDatePart, out var changeSequenceDate))
            {
                errors.Add(new ValidationResult("ChangeSequence does not contain a long date part"));
            }

            long existingChangeSequence = entity?.ChangeSequence ?? 0;
            if (changeSequenceDate < existingChangeSequence)
            {
                errors.Add(new ValidationResult($"AgreementNumber '{AGKRentalOrderHeader.AgreementHeaders.AgreementNumber}' Change sequence is greater than previously processed."));
            }

            if (!AGKRentalOrderHeader.AgreementHeaders.AgreementNumber.IsTOrAAgreement())
            {
                errors.Add(new ValidationResult($"AgreementNumber '{AGKRentalOrderHeader.AgreementHeaders.AgreementNumber}' does not start with T or A."));
            }

            if (errors.Any())
            {
                throw new BodValidationException(nameof(AgreementHeaderDataArea), errors.Select(i => i.ErrorMessage!).Distinct());
            }

            return changeSequenceDate;
        }

        public Header ToHeaderEntity(Header? entity = null)
        {
            long changeSequenceDate = Validate(entity);

            if (entity == null)
            {
                entity = new Header();
            }

            entity.QuoteNumber ??= !string.IsNullOrWhiteSpace(AGKRentalOrderHeader.AgreementHeaders.QuoteRecordId) ? AGKRentalOrderHeader.AgreementHeaders.QuoteRecordId : null;
            entity.OrderNumber ??= !string.IsNullOrWhiteSpace(AGKRentalOrderHeader.AgreementHeaders.OrderRecordId) ? AGKRentalOrderHeader.AgreementHeaders.OrderRecordId : null;
            entity.AgreementNumber = AGKRentalOrderHeader.AgreementHeaders.AgreementNumber;
            entity.AgreementNumbersOnly = AGKRentalOrderHeader.AgreementHeaders.AgreementNumber.Substring(1);
            entity.Status = AGKRentalOrderHeader.AgreementHeaders.AgreementHighestStatus;
            entity.CustomerName = AGKRentalOrderHeader.AgreementHeaders.CustomerName;
            entity.CustomerNumber = AGKRentalOrderHeader.AgreementHeaders.AgreementCustomer;
            entity.CustomerAddress ??= AGKRentalOrderHeader.AgreementHeaders.CustomerSiteAddressLine1;
            entity.CustomerAddressCode ??= AGKRentalOrderHeader.AgreementHeaders.CustomerSiteAddress;
            entity.Facility = AGKRentalOrderHeader.AgreementHeaders.Facility;
            entity.Division = AGKRentalOrderHeader.AgreementHeaders.Division;
            entity.OrderSource = AGKRentalOrderHeader.AgreementHeaders.OrderSource ?? "SF";
            entity.ChangeSequence = changeSequenceDate;
            entity.QuotePublicId ??= AGKRentalOrderHeader.AgreementHeaders.QuotePublicId;
            entity.QuotePublicIdNumbersOnly ??= AGKRentalOrderHeader.AgreementHeaders.QuotePublicId?.Substring(2);
            entity.RentalDepot = AGKRentalOrderHeader.AgreementHeaders.RentalDepot;

            return entity;
        }
    }
}
