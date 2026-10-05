namespace OF.UI.Models
{
    public class GetRingfencesResponse
    {
        public OF.Data.Database.Ringfence[] Ringfences { get; set; }

        public bool IsSuccess { get; set; }

        public string ErrorMessage { get; set; }
    }
}
