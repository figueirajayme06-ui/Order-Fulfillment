using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using OF.Api.UseCases.Assets;
using OF.Data;
using System.Net;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using System.Diagnostics.CodeAnalysis;
using OF.Common.Infrastructure.IPG.Orders.Models.RAA.AssetSync;

namespace OF.Api
{
    [ExcludeFromCodeCoverage]
    public class AssetMessageFunctions
    {
        private readonly ILogger<AssetReceivedHandler> logger;
        private readonly ApplicationDbContext dbContext;

        public AssetMessageFunctions(ILogger<AssetReceivedHandler> logger, ApplicationDbContext dbContext)
        {
            this.logger = logger;
            this.dbContext = dbContext;
        }

        [OpenApiOperation(operationId: "UpsertAsset", tags: new[] { "asset" }, Summary = "Insert or update an asset.", Description = "Insert or update an asset.")]
        [OpenApiSecurity("function_auth", SecuritySchemeType.ApiKey, In = OpenApiSecurityLocationType.Header, Name = "x-functions-key")]
        [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(AssetData), Required = true, Description = "The asset data from ION enriched skeletal asset.")]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "Asset details inserted or updated")]
        [Function(nameof(AssetsApi))]
        public async Task<HttpResponseData> AssetsApi(
         [HttpTrigger(AuthorizationLevel.Function, "POST", Route = "AssetsApi")] HttpRequestData req)
        {
            var message = await new StreamReader(req.Body).ReadToEndAsync();

            logger.LogInformation($"{nameof(AssetsApi)} handler invoked: {message}");

            var assetHandler = new AssetReceivedHandler(dbContext, logger);
            await assetHandler.Handle(message);

            return req.CreateResponse(HttpStatusCode.NoContent);
        }
    }
}
