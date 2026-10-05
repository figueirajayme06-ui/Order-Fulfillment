using System.ComponentModel.DataAnnotations.Schema;

namespace OF.Common.Infrastructure.MDP.Models
{
    public class ProductRule
    {
        public ProductRule()
        {
            ErrorConditions = new List<ErrorCondition>();
            ProductActions = new List<ProductAction>();
        }

        public string Id { get; set; }

        [NotMapped]
        public bool Active { get; set; }

        public string? AdvancedCondition { get; set; }

        public string? ConditionsMet { get; set; }

        [NotMapped]
        public string? ErrorMessage { get; set; }

        public string? EvaluationEvent { get; set; }

        [NotMapped]
        public decimal? EvaluationOrder { get; set; } = 0;

        [NotMapped]
        public IList<ErrorCondition> ErrorConditions { get; set; }

        [NotMapped]
        public IList<ProductAction> ProductActions { get; set; }

        [NotMapped]
        public string? ExecutableFormula { get; set; }
    }
}
