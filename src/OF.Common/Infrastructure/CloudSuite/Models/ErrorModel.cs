using CsvHelper.Configuration.Attributes;

namespace OF.Common.Infrastructure.CloudSuite.Models
{
    public class ErrorModel
    {
        [Name("MessageCode")]
        public string? MessageCode { get; set; }

        [Name("MessageCategory")]
        public string? MessageCategory { get; set; }

        [Name("MessageType")]
        public string MessageType { get; set; } = ""!;

        [Name("Message")]
        public string Message { get; set; } = ""!;

        [Name("LocalizedMessage")]
        public string? LocalizedMessage { get; set; }

        [Name("SuggestedLocalizedText")]
        public string SuggestedLocalizedText { get; set; } = ""!;

        [Name("MessageTime")]
        public DateTime? MessageTime { get; set; }

        [Name("QueryId")]
        public string? QueryId { get; set; }

        [Name("DataObjectId")]
        public string? DataObjectId { get; set; }

        [Name("UserTableName")]
        public string? UserTableName { get; set; }

        [Name("Line")]
        public int? Line { get; set; }

        [Name("Position")]
        public int? Position { get; set; }
    }
}
