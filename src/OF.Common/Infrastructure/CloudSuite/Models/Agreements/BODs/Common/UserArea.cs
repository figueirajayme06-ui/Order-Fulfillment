using System.Xml.Serialization;

namespace OF.Common.Infrastructure.CloudSuite.Models.Agreements.BODs.Common
{
    public class UserArea
    {
        [XmlElement(ElementName = "Property")]
        public List<Property>? Properties { get; set; }
    }
}
