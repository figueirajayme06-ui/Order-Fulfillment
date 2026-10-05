using System.Xml.Serialization;

namespace OF.Common.Infrastructure.CloudSuite.Models.Agreements.BODs.Common
{
    public class Property
    {
        [XmlElement("NameValue", IsNullable = false)]
        public required NameValue NameValue { get; set; }

    }
}
