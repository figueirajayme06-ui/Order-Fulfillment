namespace OF.Common.Infrastructure.CloudSuite
{
    public class CloudSuiteSettings
    {
        public required string AuthBaseUrl { get; set; }
        public required string CSBaseUrl { get; set; }
        public required string EnvUrl { get; set; }
        public required string ClientID { get; set; }
        public required string ClientSecret { get; set; }
        public required string Username { get; set; }
        public required string Password { get; set; }
        public required string CSAuthUrl { get; set; }
    }
}
