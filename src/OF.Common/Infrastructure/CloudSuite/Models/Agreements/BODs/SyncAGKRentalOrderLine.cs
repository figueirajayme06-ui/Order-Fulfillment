using System.Xml.Serialization;

namespace OF.Common.Infrastructure.CloudSuite.Models.Agreements.BODs
{
    [XmlRoot("SyncAGKRentalOrderLine", Namespace = "http://schema.infor.com/InforOAGIS/2")]
    public class SyncAGKRentalOrderLine
    {
        public required AgreementLineDataArea DataArea { get; set; }
    }
}
