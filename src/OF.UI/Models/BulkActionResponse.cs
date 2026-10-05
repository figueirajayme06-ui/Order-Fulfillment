namespace OF.UI.Models
{
    public class BulkActionResponse
    {
        public bool IsSuccess { get; set; }

        public string ErrorMessage { get; set; }
        public object HeaderStatus { get; internal set; }
    }
}
