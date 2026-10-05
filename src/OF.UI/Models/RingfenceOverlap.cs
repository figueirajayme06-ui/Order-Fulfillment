using OF.Data.Database;
using OF.UI.Models;

public class RingfenceOverlap : IResponse
{
    public bool IsSuccess { get; set; }
    public string ErrorMessage { get; set; }
    public IReadOnlyList<RingfenceAssestDetails> OverlappingRingfences { get; set; }
}