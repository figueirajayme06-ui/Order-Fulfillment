namespace OF.UI.Models
{
    public class ReserveResponse
    {
        public bool IsSuccess { get; set; }

        public string ErrorMessage { get; set; }

        public List<int> SiblingVictims { get; set; }
        public int HeaderStatus { get; internal set; }
        public bool IsActivatable { get; internal set; }
    }
}
