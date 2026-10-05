using System;

namespace OF.UI.Models;

public class RingfenceAssestDetails
{
    public int RingfenceId { get; set; }
    public IReadOnlyList<string> AssetIds { get; set; }
    public string Title { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string? Owner { get; set; }
}
