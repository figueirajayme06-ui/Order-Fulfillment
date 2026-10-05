namespace OF.UI.Models
{
    public class AddLineResponse
    {
        public int LineId { get; set; }
        public string RootAgreementLineNumber { get; set; }
        public string AgreementLineNumber { get; set; }
        public string ItemNumber { get; set; }
        public int Quantity { get; set; }
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
        public int HeaderStatus { get; internal set; }
    }
}
