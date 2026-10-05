namespace OF.UI.Models
{
    public class CreateRingfenceResponse : IResponse
    {
        public bool IsSuccess { get; set; }

        public string ErrorMessage { get; set; }
    }
}
