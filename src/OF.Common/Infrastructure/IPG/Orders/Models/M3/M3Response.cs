using Newtonsoft.Json;

namespace OF.Common.Infrastructure.IPG.Orders.Models.M3;

public class M3Response
{
    public string Program { get; set; } = string.Empty;

    public string Transaction { get; set; } = string.Empty;

    [JsonProperty("MIRecord")]
    public List<M3Record> Records { get; set; } = new List<M3Record>();
}

public class M3Record
{
    public string RowIndex { get; set; } = string.Empty;

    public List<M3NameValuePair> NameValue { get; set; } = new List<M3NameValuePair>();
}

public class M3NameValuePair
{
    public string Name { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}
