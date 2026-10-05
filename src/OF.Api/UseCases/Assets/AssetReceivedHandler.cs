using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OF.Common.Infrastructure.IPG.Orders.Models.RAA.AssetSync;
using OF.Data;
using OF.Data.Database;

namespace OF.Api.UseCases.Assets
{
    public class AssetReceivedHandler
    {
        private ApplicationDbContext dbContext;
        private readonly ILogger<AssetReceivedHandler> logger;

        public AssetReceivedHandler(ApplicationDbContext dbContext, ILogger<AssetReceivedHandler> logger)
        {
            this.dbContext = dbContext;
            this.logger = logger;
        }

        public async Task Handle(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                throw new ArgumentNullException(nameof(body));
            }

            var assetData = JsonConvert.DeserializeObject<AssetData>(body);

            if (string.IsNullOrWhiteSpace(assetData?.IndividualItemNumber))
            {
                logger.LogWarning("Asset data from BOD does not have an IndividualItemNumber");
                return;
            }

            Asset? asset = await dbContext.Assets.FirstOrDefaultAsync(i => i.Id == assetData.ID);
            var entity = assetData!.ToEntity(asset);

            if (dbContext.Entry(entity).State == EntityState.Detached)
            {
                dbContext.Assets.Add(entity);
            }

            await dbContext.SaveChangesAsync();
        }
    }
}
