using Microsoft.EntityFrameworkCore;

namespace OF.Data.Database
{
    [Keyless]
    public class MergeOutput
    {
        public string RecordId { get; set; } = ""!;

        public string MergeType { get; set; } = ""!;
    }
}
