using System.Xml.Serialization;

namespace OF.Common.Infrastructure.CloudSuite.Models.Agreements.BODs.Common
{
    public class NameValue
    {
        [XmlAttribute(AttributeName = "name")]
        public required string Name { get; set; }

        [XmlText]
        public string? Value { get; set; }
    }
}
