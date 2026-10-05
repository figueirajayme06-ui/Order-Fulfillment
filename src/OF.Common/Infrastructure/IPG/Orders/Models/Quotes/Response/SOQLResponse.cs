using Newtonsoft.Json;
using OF.Common.Infrastructure.IPG.Orders.Models.Quotes;

namespace OF.Common.Infrastructure.IPG.Orders.Models.Quotes.Response
{
    public class SOQLResponse<T> where T : SfObjectBase
    {
        [JsonProperty("totalSize")]
        public int TotalSize { get; set; }

        [JsonProperty("done")]
        public bool Done { get; set; }

        [JsonProperty("records")]
        public required IEnumerable<T> Records { get; set; }
    }
}
