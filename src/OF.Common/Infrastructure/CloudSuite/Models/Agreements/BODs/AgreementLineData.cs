using OF.Data.Database;
using System.ComponentModel.DataAnnotations;
using System.Xml.Serialization;

namespace OF.Common.Infrastructure.CloudSuite.Models.Agreements.BODs
{
    public class AgreementLineData
    {
        [Range(0, int.MaxValue)]
        [XmlElement("agreementLineNumber")]
        public int? AgreementLineNumber { get; set; }

        [XmlElement("agreementLineId")]
        public string? AgreementLineId { get; set; }

        [XmlElement("lotNumber")]
        public string? LotNumber { get; set; }

        [XmlElement("deliveryDate")]
        public DateTime? DeliveryDate { get; set; }

        [Required]
        [XmlElement("validToDate")]
        public DateTime? ValidToDate { get; set; }

        [Required]
        [XmlElement("validFromDate")]
        public DateTime? ValidFromDate { get; set; }

        [XmlElement("terminationDate")]
        public DateTime? TerminationDate { get; set; }

        [XmlElement("collectionDate")]
        public DateTime? CollectionDate { get; set; }

        [Required]
        [Range(0, float.MaxValue)]
        [XmlElement("orderedQuantity", IsNullable = false)]
        public required float OrderedQuantity { get; set; }

        [Required]
        [XmlElement("agreementLineStatus", IsNullable = false)]
        public required string AgreementLineStatus { get; set; }

        [XmlElement("agreementLineType")]
        public string? AgreementLineType { get; set; }

        [XmlElement("itemDescription")]
        public string? ItemDescription { get; set; }

        [Required]
        [XmlElement("itemNumber", IsNullable = false)]
        public required string ItemNumber { get; set; }

        [XmlElement("TextIdentityDeliveryText")]
        public TextLines? TextIdentityDeliveryText { get; set; }

        [XmlElement("packageGroupNumber")]
        public string? PackageGroupNumber { get; set; }

        [XmlElement("genericItemNumber")]
        public string? GenericItemNumber { get; set; }

        [Required]
        [XmlElement("fromWarehouse", IsNullable = false)]
        public required string FromWarehouse { get; set; }

        [Required]
        [XmlElement("facility", IsNullable = false)]
        public required string Facility { get; set; }

        [Required]
        [XmlElement("division", IsNullable = false)]
        public required string Division { get; set; }

        [XmlElement("orderSource", IsNullable = false)]
        public string? OrderSource { get; set; }

        [XmlElement("changeSequence")]
        public string? ChangeSequence { get; set; }

        [XmlElement("orderItemRecordId")]
        public string? OrderLineRecordId { get; set; }

        [XmlElement("rateType")]
        public string? RateType { get; set; }

        [XmlElement("numberOfShifts")]
        public string? NumberOfShifts { get; set; }

        [XmlElement("deliveryOrderLineHighestStatus", IsNullable = false)]
        public string? AgreementLineStatusDelivery { get; set; }

        public string GenericOrItemNumber => GenericItemNumber ?? ItemNumber;

        [XmlElement("siteAddressLine1")]
        public string? SiteAddressLine1 { get; set; }

        [XmlElement("siteAddressLine3")]
        public string? SiteAddressLine3 { get; set; }

        [XmlElement("siteAddressLine4")]
        public string? SiteAddressLine4 { get; set; }

        [XmlElement("customerSiteAddress")]
        public string? CustomerSiteAddress { get; set; }
    }

    public class TextLines
    {
        [XmlElement("text")]
        public string? Text { get; set; }
    }
}
