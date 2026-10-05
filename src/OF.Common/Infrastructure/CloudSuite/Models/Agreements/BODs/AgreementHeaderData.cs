using System.ComponentModel.DataAnnotations;
using System.Xml.Serialization;

namespace OF.Common.Infrastructure.CloudSuite.Models.Agreements.BODs
{
    public class AgreementHeaderData
    {
        [Required]
        [XmlElement("agreementNumber", IsNullable = false)]
        public required string AgreementNumber { get; set; }

        [Required]
        [XmlElement("facility", IsNullable = false)]
        public required string Facility { get; set; }

        [Required]
        [XmlElement("division", IsNullable = false)]
        public required string Division { get; set; }

        [Required]
        [XmlElement("agreementCustomer", IsNullable = false)]
        public required string AgreementCustomer { get; set; }

        [Required]
        [XmlElement("customerName", IsNullable = false)]
        public required string CustomerName { get; set; }

        [Required]
        [XmlElement("customerSiteAddressLine1", IsNullable = false)]
        public required string CustomerSiteAddressLine1 { get; set; }

        [Required]
        [XmlElement("customerSiteAddress", IsNullable = false)]
        public required string CustomerSiteAddress { get; set; }

        [Required]
        [XmlElement("agreementHighestStatus", IsNullable = false)]
        public required string AgreementHighestStatus { get; set; }

        [XmlElement("orderSource", IsNullable = false)]
        public string? OrderSource { get; set; }

        [Required]
        [XmlElement("changeSequence", IsNullable = false)]
        public required string ChangeSequence { get; set; }

        [XmlElement("orderRecordId")]
        public string? OrderRecordId { get; set; }

        [XmlElement("quoteRecordId")]
        public string? QuoteRecordId { get; set; }

        [XmlElement("proposalNumber")]
        public string? QuotePublicId { get; set; }

        [XmlElement("rentalDepot")]
        public string? RentalDepot { get; set; }

        [Required]
        [XmlElement("agreementNumberId", IsNullable = false)]
        public required string AgreementNumberId { get; set; }
    }
}
