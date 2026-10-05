using System.Xml.Serialization;

namespace OF.Common.Infrastructure.CloudSuite.Models.Agreements.BODs
{
    [XmlRoot("SyncAGKRentalOrderHeader", Namespace = "http://schema.infor.com/InforOAGIS/2")]
    public class SyncAGKRentalOrderHeader
    {
        public required AgreementHeaderDataArea DataArea { get; set; }
    }
}
