namespace OF.UI.Models
{
    public class OrderSummaryFooterModel
    {
        public required OrderSummaryModel OrderSumary { get; set; }

        public int PageIndex { get; set; }

        public int PageTotal { get; set; }
    }
}
