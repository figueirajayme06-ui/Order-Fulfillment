using System.ComponentModel.DataAnnotations;
using System.Xml.Serialization;

namespace OF.Common.Infrastructure.CloudSuite.Models.Agreements.BODs.Common
{
    public class Sync
    {
        public required ActionCriteria ActionCriteria { get; set; }
    }

    public class ActionCriteria
    {
        public required ActionExpression ActionExpression { get; set; }
    }

    public class ActionExpression
    {
        [Required]
        [XmlAttribute(AttributeName = "actionCode")]
        public required string ActionCode { get; set; }
    }
}
