using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OF.Common.Infrastructure.CloudSuite;
using OF.Common.Infrastructure.CloudSuite.SQL;
using OF.Common.Infrastructure.MDP;
using OF.Data.Database;
using System.Data;
using Refresh = OF.Common.Constants.DataRefresh.Assets;

namespace OF.Data.Assets.UseCases
{
    public class GetAssetsHandler
    {
        private readonly ICloudSuiteService cloudsuiteService;
        private readonly ApplicationDbContext dbContext;
        private readonly MDPDbContext mdpContext;
        private readonly ILogger<GetAssetsHandler> logger;

        public GetAssetsHandler(ICloudSuiteService cloudsuiteService, ApplicationDbContext dbContext, MDPDbContext mdpContext, ILogger<GetAssetsHandler> logger)
        {
            this.cloudsuiteService = cloudsuiteService;
            this.dbContext = dbContext;
            this.mdpContext = mdpContext;
            this.logger = logger;
        }

        public async Task Handle()
        {
            logger.LogInformation($"Getting assets from ION Data lake.");
            var entities = await cloudsuiteService.RunDataLakeQuery<AssetsStaging>(IONSQL.GetAssets);
            var groupedEntities = entities.GroupBy(i => i.Id).Select(i => i.FirstOrDefault()).Where(i => i != null).Cast<AssetsStaging>().ToList();

            using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadUncommitted);

            try
            {
                logger.LogInformation($"Truncating the staging table.");
                await dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE [dbo].[AssetsStaging]");
                
                logger.LogInformation($"Bulk Insert {entities.Count} entities in staging table.");
                await dbContext.BulkInsertAsync(groupedEntities);
                
                logger.LogDebug($"Merging assets - this may take several minutes...");
                // Set extended timeout for the merge operation
                dbContext.Database.SetCommandTimeout(600); // 10 minutes
                await dbContext.Database.ExecuteSqlRawAsync(IONSQL.MergeAssets);
                
                // Continue with product hierarchies...
                var productHierarchies = await mdpContext.ProductHierarchy.Where(p => p.IndividualItemNumber != "Not Found" && p.IndividualItemNumber != "UNKNOWN").ToListAsync();
                await dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE [dbo].[ProductHierarchy_Staging]");
                logger.LogInformation($"Bulk Insert {productHierarchies.Count} entities in ProductHierarchy_Staging table.");
                await dbContext.BulkInsertAsync(productHierarchies);
                
                logger.LogDebug($"Merging hierarchy - this may take several minutes...");
                // Set timeout for hierarchy merge as well
                dbContext.Database.SetCommandTimeout(600);
                await dbContext.Database.ExecuteSqlRawAsync(IONSQL.MergeHierarchy);

                await dbContext.DataRefreshStamp(Refresh.Key, Refresh.Description);
                await dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception exception)
            {
                logger.LogError(exception, $"Error persisting data.");
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
