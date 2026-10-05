namespace OF.UI.Models
{
    public class FulfilmentResponse
    {
        public int LineId { get; set; }

        public string AgreementLineNumber { get; set; }

        public string Division { get; set; }

        public bool IsChild { get; set; }

        public string GenericCode { get; set; }

        public string ItemNumber { get; set; }

        public bool IsSerialized { get; set; }

        public string ItemDescription { get; set; }

        public AlternativeOption[] AlternativeOptions { get; set; }

        public string[] Attributes { get; set; }

        public string[] DisplayAttributes { get; set; }

        public string Warehouse { get; set; } 

        public bool IsSuccess { get; set; }

        public string ErrorMessage { get; set; }

        public int ProductFamilyId { get; set; }

        public int ProductLineId { get; set; }

    }
}
