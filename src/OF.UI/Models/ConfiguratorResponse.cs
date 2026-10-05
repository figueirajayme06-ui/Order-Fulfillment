public class ConfiguratorResponse
{
    public CustomAttributes CustomAttributes { get; set; }
    public Dictionary<string, object> ConfigurationAttributes { get; set; }
    public Product Product { get; set; }
    public ConfigurationQuote Quote { get; set; }
}
