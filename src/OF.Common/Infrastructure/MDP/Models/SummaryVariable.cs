using System.ComponentModel.DataAnnotations.Schema;

namespace OF.Common.Infrastructure.MDP.Models
{
    public class SummaryVariable
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public string? AggregateField { get; set; }

        public string? AggregateFunction { get; set; }

        public string? CombineWith { get; set; }

        public string? CompositeOperator { get; set; }

        public string? ConstraintField { get; set; }

        public string? FilterField { get; set; }

        public string? FilterValue { get; set; }

        public string? Operator { get; set; }

        public string? Scope { get; set; }

        public string? TargetObject { get; set; }

        public string? ValueElement { get; set; }
    }
}
