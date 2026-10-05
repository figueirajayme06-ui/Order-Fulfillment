using System.ComponentModel.DataAnnotations.Schema;

namespace OF.Common.Infrastructure.MDP.Models
{
    public class ConfigurationRule
    {
        public ConfigurationRule()
        {
        }

        public string? Id { get; set; }

        public string Product { get; set; }

        public string? Name { get; set; }

        public string? RuleType { get; set; }

        public string? RuleEvaluationEvent { get; set; }

        [NotMapped]
        public string? ProductRuleId { get; set; }

        [NotMapped]
        public ProductRule? ProductRule { get; set; }

    }

    public class ConfigurationRuleCache
    {
        public ConfigurationRuleCache()
        {
            Cache = new Dictionary<string, List<ConfigurationRule>>();
        }

        public Dictionary<string, List<ConfigurationRule>> Cache { get; set; }
    }
}
