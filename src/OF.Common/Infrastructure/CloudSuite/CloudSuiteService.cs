using System.Net.Http.Headers;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using CsvHelper.Configuration;
using CsvHelper;
using System.Globalization;
using OF.Common.Infrastructure.CloudSuite.Models.Agreements.DataLake;
using OF.Common.Infrastructure.CloudSuite.Models.Items.DataLake;
using OF.Common.Infrastructure.CloudSuite.Models;

namespace OF.Common.Infrastructure.CloudSuite
{
    public interface ICloudSuiteService
    {
        Task<IList<T>> RunDataLakeQuery<T>(string query) where T : class;
    }

    public class CloudSuiteService : ICloudSuiteService
    {
        private readonly HttpClient authClient;
        private readonly HttpClient apiClient;
        private readonly CloudSuiteSettings settings;
        private readonly ILogger<CloudSuiteService> logger;

        public CloudSuiteService(CloudSuiteSettings settings, IHttpClientFactory httpClientFactory, ILogger<CloudSuiteService> logger)
        {
            this.settings = settings;
            this.logger = logger;

            authClient = httpClientFactory.CreateClient();
            authClient.DefaultRequestHeaders.Accept.Clear();
            authClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            authClient.BaseAddress = new Uri(settings.AuthBaseUrl);

            apiClient = httpClientFactory.CreateClient();
            apiClient.DefaultRequestHeaders.Accept.Clear();
            apiClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            apiClient.BaseAddress = new Uri(settings.CSBaseUrl);
            apiClient.Timeout = TimeSpan.FromSeconds(300);
        }

        private async Task<string> Authenticate()
        {
            var clientId = settings.ClientID;
            var clientSecret = settings.ClientSecret;
            var username = settings.Username;
            var password = settings.Password;
            var authUrl = "/" + settings.EnvUrl + "/" + settings.CSAuthUrl;

            logger.LogInformation("Authenticating to CloudSuite {0}", authUrl);

            var body = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "password"),
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", clientSecret),
                new KeyValuePair<string, string>("username", username),
                new KeyValuePair<string, string>("password", password)
            });

            var request = new HttpRequestMessage(HttpMethod.Post, authUrl) { Content = body };
            var response = await authClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            logger.LogInformation("Authentication success");

            var responseBody = await response.Content.ReadAsStringAsync();
            var json = JsonConvert.DeserializeObject<CloudSuiteBearerToken>(responseBody);

            return json!.AccessToken;
        }

        private async Task<string> SubmitQuery(string accessToken, string query)
        {
            string url = "/" + settings.EnvUrl + "/DATAFABRIC/compass/v2/jobs";

            logger.LogInformation("Sending query {0}", url);

            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(query, System.Text.Encoding.UTF8, "text/plain")
            };

            request.Headers.Add("Authorization", "Bearer " + accessToken);

            var response = await apiClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var responseBody = await response.Content.ReadAsStringAsync();
            var json = JsonConvert.DeserializeObject<CloudSuiteQuerySubmitResults>(responseBody);
            return json!.QueryId;
        }

        private async Task<bool> IsQueryFinished(string id, string accessToken)
        {
            string url = "/" + settings.EnvUrl + "/DATAFABRIC/compass/v2/jobs/" + id + "/status";

            logger.LogInformation("Checking if query is finished");

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Authorization", "Bearer " + accessToken);
            var response = await apiClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return response.StatusCode.ToString() == "Created";
        }

        private async Task<IList<T>> GetQueryResult<T>(string id, string accessToken) where T : class
        {
            const int take = 50000;
            int count = 0;

            List<T> results = new();

            bool getMoreResults = true;
            while (getMoreResults)
            {
                string url = $"/{settings.EnvUrl}/DATAFABRIC/compass/v2/jobs/{id}/result?limit={take}&offset={take * count}";

                logger.LogInformation("Getting query result");

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Authorization", "Bearer " + accessToken);
                request.Headers.Accept.Clear();
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/csv"));
                request.Headers.AcceptEncoding.Clear();
                request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("identity"));
                var response = await apiClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                var csvData = await response.Content.ReadAsStringAsync();

                try
                {
                    using (var reader = new StringReader(csvData))
                    using (var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true }))
                    {
                        csv.Context.RegisterClassMap<IONAgreementLineDataMap>();
                        csv.Context.RegisterClassMap<IONAgreementDataMap>();
                        csv.Context.RegisterClassMap<ProductItemStagingMap>();
                        csv.Context.RegisterClassMap<AssetStagingMap>();

                        var paged = csv.GetRecords<T>().ToList();
                        if (!paged.Any())
                        {
                            break;
                        }
                        results.AddRange(paged);
                        count++;
                    }
                }
                catch (HeaderValidationException)
                {
                    using (var reader = new StringReader(csvData))
                    using (var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true }))
                    {
                        var errors = csv.GetRecords<ErrorModel>().ToList();

                        if (errors.Any())
                        {
                            logger.LogError($"ION {errors[0].MessageType}: {errors[0].SuggestedLocalizedText}.");
                        }
                    }

                    throw;
                }
            }

            return results;
        }

        public async Task<IList<T>> RunDataLakeQuery<T>(string query) where T : class
        {
            var accessToken = await Authenticate();
            var queryId = await SubmitQuery(accessToken, query);

            while (!await IsQueryFinished(queryId, accessToken))
            {
                Thread.Sleep(10000);
            }

            logger.LogInformation("Query is finished, getting results.");

            return await GetQueryResult<T>(queryId, accessToken);
        }
    }
}
