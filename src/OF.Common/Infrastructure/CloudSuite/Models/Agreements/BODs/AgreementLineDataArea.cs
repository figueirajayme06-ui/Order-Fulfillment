using Microsoft.Extensions.Logging;
using OF.Common.Infrastructure.CloudSuite.Models.Agreements.BODs.Common;
using OF.Common.Infrastructure.CloudSuite.Models.Agreements.BODs.Exceptions;
using OF.Common.Utils;
using OF.Data.Database;
using System.ComponentModel.DataAnnotations;
using System.Xml.Serialization;
using static OF.Common.Constants;

namespace OF.Common.Infrastructure.CloudSuite.Models.Agreements.BODs
{
    public class AGKRentalOrderLine
    {
        [Required]
        [XmlElement("agreementNumber", IsNullable = false)]
        public required string AgreementNumber { get; set; }

        [Required]
        [Range(0, int.MaxValue)]
        [XmlElement("agreementLineNumber", IsNullable = false)]
        public required int AgreementLineNumber { get; set; }

        [Required]
        [XmlElement("agreementLineId", IsNullable = false)]
        public required string AgreementLineId { get; set; }

        [Required]
        [XmlElement("AgreementLines", IsNullable = false)]
        public required AgreementLineData AgreementLines { get; set; }

        [Required]
        [XmlElement("agreementNumberId", IsNullable = false)]
        public required string AgreementNumberId { get; set; }

        public int GetAgreementLineNumber()
        {
            if (AgreementLineNumber > 0)
            {
                return AgreementLineNumber;
            }

            return AgreementLines.AgreementLineNumber ?? 0;
        }

        public string GetAgreementLineId()
        {
            if (!string.IsNullOrWhiteSpace(AgreementLineId))
            {
                return AgreementLineId;
            }

            if (!string.IsNullOrWhiteSpace(AgreementLines.AgreementLineId))
            {
                return AgreementLines.AgreementLineId;
            }

            return $"{AgreementNumber}-{GetAgreementLineNumber()}";
        }

        public DateTime GetDateFromChangeSequence()
        {
            if (!string.IsNullOrWhiteSpace(AgreementLines.ChangeSequence))
            {
                if (long.TryParse(AgreementLines.ChangeSequence.Split("|")[0], out long changeSequence))
                {
                    if (changeSequence > 0)
                    {
                        var dateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(changeSequence);
                        return dateTimeOffset.DateTime;
                    }
                }
            }

            return DateTime.UtcNow;
        }

        public string GetAgreementAddress()
        {
            var lines = new List<string?>
            {
                AgreementLines.SiteAddressLine1,
                AgreementLines.SiteAddressLine3,
                AgreementLines.SiteAddressLine4
            };

            return string.Join(", ", lines.Where(i => !string.IsNullOrWhiteSpace(i)).ToArray());
        }
    }

    public class AgreementLineDataArea : BaseValidatable
    {
        public required Sync Sync { get; set; }

        [Required]
        public required AGKRentalOrderLine AGKRentalOrderLine { get; set; }

        public long Validate(Line? entity)
        {
            var errors = new List<ValidationResult>();

            Validate(AGKRentalOrderLine, out IList<ValidationResult> areaErrors);
            Validate(AGKRentalOrderLine.AgreementLines, out IList<ValidationResult> lineErrors);

            errors.AddRange(areaErrors);
            errors.AddRange(lineErrors);

            var changeSequenceDatePart = AGKRentalOrderLine.AgreementLines.ChangeSequence?.Split("|")[0];
            if (!long.TryParse(changeSequenceDatePart, out var changeSequenceDate))
            {
                errors.Add(new ValidationResult("ChangeSequence does not contain a long date part"));
            }

            long existingChangeSequence = entity?.ChangeSequence ?? 0;
            if (changeSequenceDate < existingChangeSequence)
            {
                errors.Add(new ValidationResult("ChangeSequence as older than previously processed"));
            }

            if (!AGKRentalOrderLine.AgreementNumber.IsTOrAAgreement())
            {
                errors.Add(new ValidationResult($"AgreementNumber '{AGKRentalOrderLine.AgreementNumber}' does not start with T or A."));
            }

            if (errors.Any())
            {
                throw new BodValidationException(nameof(AgreementLineDataArea), errors.Select(i => i.ErrorMessage!).Distinct());
            }

            return changeSequenceDate;
        }

        public bool ShouldConfirmReservation
        {
            get
            {
                int.TryParse(AGKRentalOrderLine.AgreementLines.AgreementLineStatus, out int status);
                int.TryParse(AGKRentalOrderLine.AgreementLines.AgreementLineStatusDelivery, out int statusDelivery);

                return (status == M3LineStatus.OnHire || status == M3LineStatus.Terminated || status == M3LineStatus.Invoiced || status == M3LineStatus.Closed) && statusDelivery == M3LineDeliveryStatus.Delivered;
            }
        }

        public Line ToLineEntity(Line? entity = null, ILogger? logger = null)
        {
            long changeSequenceDate = Validate(entity);

            if (entity == null)
            {
                entity = new Line();
            }

            entity.OrderLineNumber = !string.IsNullOrWhiteSpace(AGKRentalOrderLine.AgreementLines.OrderLineRecordId) ? AGKRentalOrderLine.AgreementLines.OrderLineRecordId : null;
            entity.AgreementNumbersOnly = AGKRentalOrderLine.AgreementNumber.Substring(1);
            entity.AgreementLineNumber = AGKRentalOrderLine.GetAgreementLineId();
            entity.AgreementLineIndex = AGKRentalOrderLine.GetAgreementLineNumber();
            entity.AgreementLineType = AGKRentalOrderLine.AgreementLines.AgreementLineType;
            entity.ItemNumber = AGKRentalOrderLine.AgreementLines.ItemNumber;
            entity.GenericItemNumber = AGKRentalOrderLine.AgreementLines.GenericItemNumber ?? AGKRentalOrderLine.AgreementLines.ItemNumber;
            entity.Status = AGKRentalOrderLine.AgreementLines.AgreementLineStatus;
            entity.Quantity = AGKRentalOrderLine.AgreementLines.OrderedQuantity;
            entity.PackageGroupNumber = !string.IsNullOrWhiteSpace(AGKRentalOrderLine.AgreementLines.PackageGroupNumber) ? AGKRentalOrderLine.AgreementLines.PackageGroupNumber : entity.PackageGroupNumber;
            entity.DeliveryDate = AGKRentalOrderLine.AgreementLines.DeliveryDate;
            entity.ValidFromDate = AGKRentalOrderLine.AgreementLines.ValidFromDate!.Value;
            entity.ValidToDate = AGKRentalOrderLine.AgreementLines.ValidToDate!.Value;
            entity.TerminationDate = AGKRentalOrderLine.AgreementLines.TerminationDate;
            entity.CollectionDate = AGKRentalOrderLine.AgreementLines.CollectionDate;
            entity.Warehouse = AGKRentalOrderLine.AgreementLines.FromWarehouse;
            entity.Facility = AGKRentalOrderLine.AgreementLines.Facility;
            entity.Division = AGKRentalOrderLine.AgreementLines.Division;
            entity.OrderSource = AGKRentalOrderLine.AgreementLines.OrderSource ?? "SF";
            entity.ChangeSequence = changeSequenceDate;
            entity.RateType = AGKRentalOrderLine.AgreementLines.RateType;
            entity.NumberOfShifts = AGKRentalOrderLine.AgreementLines.NumberOfShifts;
            entity.ItemDescription = AGKRentalOrderLine.AgreementLines.ItemDescription;
            entity.RequiresFulfilment = !AGKRentalOrderLine.AgreementLines.ItemNumber.IsExcludedLine();

            entity.NormalizeItemDescription(logger);

            return entity;
        }
    }
}
