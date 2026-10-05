using System.Xml.Serialization;

namespace OF.Common.Infrastructure.CloudSuite.Models.Agreements.BODs
{
    [XmlRoot("AcknowledgeAGKRentalAgreementActivation", Namespace = "http://schema.infor.com/InforOAGIS/2")]
    public class AcknowledgeAGKRentalAgreementActivation
    {
        public required AcknowledgeActivationDataArea DataArea { get; set; }
    }
}
