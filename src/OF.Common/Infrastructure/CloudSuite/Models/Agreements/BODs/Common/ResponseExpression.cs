using System.Xml.Serialization;

namespace OF.Common.Infrastructure.CloudSuite.Models.Agreements.BODs.Common
{
    public class ResponseExpression
    {
        [XmlAttribute(AttributeName = "actionCode")]
        public required string ActionCode { get; set; }
    }
}
