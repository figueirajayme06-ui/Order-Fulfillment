namespace OF.UI.Controllers
{
    public partial class ChangeOrderController
    {
        public class DocumentRequest
        {
            public string? RecipientEmail { get; set; }
            public bool RequireApproval { get; set; }
            public string? AdditionalText { get; set; }
        }
    }
}
