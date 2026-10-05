
namespace OF.Api.UseCases.Agreements.Models
{
    public static class AcknowledgementResponseX
    {
        public static bool IsValid(this AcknowledgementResponse? model) => model != null && model.EntityId > 0;
    }

    public class AcknowledgementResponse
    {
        public int EntityId { get; set; }

        public int Status { get; set; }

        public bool IsLine { get; set; }
    }
}