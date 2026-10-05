using System.Xml.Serialization;

namespace OF.Common.Infrastructure.CloudSuite.Models.Agreements.BODs
{
    [XmlRoot("AcknowledgeAGKRentalAgreementLines", Namespace = "http://schema.infor.com/InforOAGIS/2")]
    public class AcknowledgeAGKRentalAgreementLines
    {
        public required AcknowledgeLineDataArea DataArea { get; set; }
    }
}
