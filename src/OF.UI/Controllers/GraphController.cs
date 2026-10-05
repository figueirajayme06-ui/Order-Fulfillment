using Azure.Identity;
using Infragistics.Web.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using System.Net;

namespace OF.UI.Controllers
{
    [Route("/api/[controller]")]
    [ApiController]
    [Authorize]
    public class GraphController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        // App-ony auth token credential
        private ClientSecretCredential? _clientSecretCredential;
        // Client configured with app-only authentication
        private GraphServiceClient? _appClient;
        private IHttpContextAccessor _httpContextAccessor;

        public GraphController(IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
        {
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
        }

        void InitializeGraphForAppOnlyAuth(IConfiguration configuration)
        {
            if (_clientSecretCredential == null)
            {
                _clientSecretCredential = new ClientSecretCredential(
                    configuration["GraphClientTenantId"], configuration["GraphClientId"], configuration["GraphClientSecret"]);
            }

            if (_appClient == null)
            {
                _appClient = new GraphServiceClient(_clientSecretCredential,
                    // Use the default scope, which will request the scopes
                    // configured on the app registration
                    new[] { "https://graph.microsoft.com/.default" });
            }
        }

        async Task<UserCollectionResponse?> GetUsersAsync(string query)
        {
            // Ensure client isn't null
            _ = _appClient ??
                throw new System.NullReferenceException("Graph has not been initialized for app-only auth");

            query = query.Trim().TrimEnd(';');
            var search = String.Empty;
            var queryParts = query.Split(';');
            for (var p = 0; p < queryParts.Length; p++)
            {
                var part = queryParts[p];
                search = search + "\"displayName:" + part.Trim() + "\"";
                if (p != queryParts.Length - 1) search = search + " OR ";
            }

            return await _appClient.Users.GetAsync((config) =>
            {
                config.Headers.Add("ConsistencyLevel", "eventual");
                config.QueryParameters.Search = search;
                config.QueryParameters.Orderby = new string[] { "displayName" };
                config.QueryParameters.Select = new string[] { "displayName", "mail", "userPrincipalName" };
                config.QueryParameters.Top = 25;
            });
        }

        [HttpGet]
        [Route("People")]
        [ComboDataSourceAction]
        public ActionResult<IQueryable<Models.Graph.Person>> GetUsers(string? filter)
        {
            var results = new List<Models.Graph.Person>();


            if (string.IsNullOrEmpty(filter))
            {
                var q = _httpContextAccessor.HttpContext.Request.Query["filter(displayName)"];

                if (q.Count > 0)
                {
                    var parts = q[0].Split('(');
                    filter = parts[parts.Length - 1].TrimEnd(')');
                }
                else
                {
                    var q2 = _httpContextAccessor.HttpContext.Request.Query["filter(displayName:string)"];
                    if (q2.Count > 0)
                    {
                        var parts = q2[0].Split('(');
                        filter = parts[parts.Length - 1].TrimEnd(')');
                    }
                }
            }

            if (!string.IsNullOrEmpty(filter))
            {

                InitializeGraphForAppOnlyAuth(_configuration);
                var items = GetUsersAsync(WebUtility.UrlDecode(filter)).Result;

                if (items != null && items.Value != null)
                {
                    foreach (var p in items.Value)
                    {
                        results.Add(new Models.Graph.Person()
                        {
                            UserPrincipalName = p.UserPrincipalName!,
                            Mail = p.Mail!,
                            DisplayName = p.DisplayName!
                        });
                    }
                }
            }

            return new ObjectResult(results);

        }
    }
}
