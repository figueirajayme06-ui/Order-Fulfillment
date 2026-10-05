using System.ComponentModel.DataAnnotations.Schema;

namespace OF.Common.Infrastructure.MDP.Models
{
    public class ConfiguredProduct
    {
        public ConfiguredProduct()
        {
            Attributes = new List<ConfigurationField>();
            Rules = new List<ConfigurationRule>();
        }

        public string? Id { get; set; }

        public string? Name { get; set; }

        public string? ProductCode { get; set; }

        public string? GenericCode { get; set; }

        [NotMapped]
        public bool HasSelectionRules => Rules?.Any(r => r.RuleType == "Selection") ?? false;

        [NotMapped]
        public bool HasValidationRules => Rules?.Any(r => r.RuleType == "Validation") ?? false;

        [NotMapped]
        public IList<ConfigurationField> Attributes { get; set; }

        [NotMapped]
        public IList<ConfigurationRule> Rules { get; set; }
    }
}
