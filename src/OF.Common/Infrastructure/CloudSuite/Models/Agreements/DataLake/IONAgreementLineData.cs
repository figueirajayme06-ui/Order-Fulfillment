using OF.Common.Infrastructure.CloudSuite.Models.Agreements.BODs.Common;
using OF.Common.Utils;
using OF.Data.Database;
using System.ComponentModel.DataAnnotations;
using System.Data.SqlTypes;

namespace OF.Common.Infrastructure.CloudSuite.Models.Agreements.DataLake
{
    public class IONAgreementLineData : BaseValidatable
    {
        [Required]
        public required string AgreementNumber { get; set; }

        [Required]
        public required float LineNumber { get; set; }

        public string AgreementLineNumber => AgreementNumber + "-" + LineNumber;

        public string AgreementLineNumbersOnly => AgreementLineNumber.Substring(1);

        public string? ProposalNumber { get; set; }

        public string? LineSuffix { get; set; }

        public string? Company { get; set; }

        [Required]
        public required string Division { get; set; }

        [Required]
        public required string Facility { get; set; }

        [Required]
        public required string ItemNumber { get; set; }

        [Required]
        public required string LineType { get; set; }

        [Required]
        public required string AgreementLineStatus { get; set; }

        public string? CustomerOrderNumber { get; set; }

        public string? CustomerSite { get; set; }

        public string? AddressNumber { get; set; }

        [Required]
        public required string FromWarehouse { get; set; }

        public string? DeliveryOrderNumber { get; set; }

        public string? DeliveryOrderLineNumber { get; set; }

        public string? CollectionOrderNumber { get; set; }

        public string? CollectionOrderLineNumber { get; set; }

        public string? NumberOfShifts { get; set; }

        [Required]
        public required float OrderedQuantity { get; set; }

        public string? DeliveryDate { get; set; }

        public string? CollectionDateReturnDate { get; set; }

        public string? ShipAddress1 { get; set; }

        public string? ShipAddress2 { get; set; }

        public string? ShipAddress3 { get; set; }

        public string? ShipAddress4 { get; set; }

        public string? TelephoneNumber { get; set; }

        public string? TerminationDate { get; set; }

        public string? ToWarehouse { get; set; }

        public string? AgreementLineTextId { get; set; }

        public string? SubstituteFlag { get; set; }

        [Required]
        public required string AgreementFromDate { get; set; }

        public string? AgreementToDate { get; set; }

        public string? NumberOfDaysOfHire { get; set; }

        public string? NumberOfUsedDaysOnHire { get; set; }

        public string? TextIdentityDeliveryOrderText { get; set; }

        public string? TextIdentityCollectionText { get; set; }

        public string? TextIdentityPOText { get; set; }

        public string? TextIdentityPRText { get; set; }

        public string? DeliveryNumber { get; set; }

        public string? DeliveryLine { get; set; }

        public string? ReturnNumber { get; set; }

        public string? ReturnLine { get; set; }

        public string? LotNumber { get; set; }

        public string? SubstituteItem { get; set; }

        public string? GenericItem { get; set; }

        public string? DeliveryWindowStartDate { get; set; }

        public string? DeliveryWindowStartTime { get; set; }

        public string? CollectionWindowStartDate { get; set; }

        public string? CollectionWindowStartTime { get; set; }

        public string? DeliveryWindowEnd { get; set; }

        public string? CollectionWindowEnd { get; set; }

        public string? PackageNumber { get; set; }

        public string? PackageSortLine { get; set; }

        public string? QuoteLineId { get; set; }

        public string? OrderLineId { get; set; }

        public string? Source { get; set; }

        public string? RateType { get; set; }

        public Line ToLineEntity(Line? existing = null)
        {
            if (existing == null)
            {
                existing = new Line();
            }

            existing.AgreementLineNumber = AgreementLineNumber;
            existing.AgreementLineIndex = (int)LineNumber;
            existing.AgreementNumbersOnly = AgreementNumber.Length > 0 ? AgreementNumber.Substring(1) : AgreementNumber;
            existing.AgreementLineType = LineType;
            existing.ItemNumber = ItemNumber;
            existing.GenericItemNumber = !string.IsNullOrWhiteSpace(GenericItem) ? GenericItem : ItemNumber;
            existing.Status = AgreementLineStatus;
            existing.Quantity = OrderedQuantity;
            existing.DeliveryDate = DeliveryDate.ParseIonExact();
            existing.ValidToDate = AgreementToDate.ParseIonExact() ?? SqlDateTime.MinValue.Value;
            existing.ValidFromDate = AgreementFromDate.ParseIonExact()!.Value;
            existing.TerminationDate = TerminationDate.ParseIonExact();
            existing.Warehouse = FromWarehouse;
            existing.Facility = Facility;
            existing.Division = Division;
            existing.OrderSource = "ION";
            existing.ChangeSequence = 0;
            existing.QuotePublicId = ProposalNumber;
            existing.QuotePublicIdNumbersOnly = ProposalNumber?.Length >= 2 ? ProposalNumber.Substring(2) : ProposalNumber;
            existing.NumberOfShifts = NumberOfShifts;
            existing.RateType = RateType;
            existing.CollectionDate = CollectionDateReturnDate.ParseIonExact();
            existing.RequiresFulfilment = !ItemNumber.IsExcludedLine();
            return existing;
        }
    }
}
