using System.ComponentModel.DataAnnotations.Schema;

namespace OF.Common.Infrastructure.MDP.Models
{
    public class ErrorCondition
    {
        public string? Id { get; set; }

        public string? Rule { get; set; }

        public string? FilterType { get; set; }

        public string? FilterValue { get; set; }

        public string? FilterVariable { get; set; }

        public string? Index { get; set; }

        public decimal? IndexValue => string.IsNullOrWhiteSpace(Index) ? 0 : decimal.Parse(Index);

        public string? Operator { get; set; }

        public string? TestedAttribute { get; set; }

        public string? TestedField { get; set; }

        public string? TestedObject { get; set; }

        public string? TestedVariable { get; set; }

        [NotMapped]
        public SummaryVariable? SummaryVariable { get; set; }
    }
}
