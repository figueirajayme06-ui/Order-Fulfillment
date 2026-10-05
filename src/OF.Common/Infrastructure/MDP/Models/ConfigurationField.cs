using System.ComponentModel.DataAnnotations.Schema;

namespace OF.Common.Infrastructure.MDP.Models
{
    public class ConfigurationField
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public string? DisplayOrder { get; set; }

        public string? ColumnOrder { get; set; }

        public bool Hidden { get; set; }

        public string? Position { get; set; }

        public bool Required { get; set; }

        public string? TargetField { get; set; }

        public bool AppliedImmediately { get; set; }

        public string? ShownValues { get; set; }

        public string? HiddenValues { get; set; }

        public bool ApplyToProductOptions { get; set; }

        [NotMapped]
        public string? ProductCode { get; set; }

        [NotMapped]
        public SObjectFieldMetadata? FieldMetadata { get; set; }
    }
}