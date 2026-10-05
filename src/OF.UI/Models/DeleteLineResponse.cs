namespace OF.UI.Models
{
    public class DeleteLineResponse
    { 
        public int LineId { get; set; }
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
        public int HeaderStatus { get; internal set; }
        public bool IsActivatable { get; set; } = false;
    }
}
