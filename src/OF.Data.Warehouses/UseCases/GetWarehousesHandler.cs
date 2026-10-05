using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OF.Common.Infrastructure.CloudSuite.SQL;
using OF.Common.Infrastructure.MDP;
using System.Data;
using Refresh = OF.Common.Constants.DataRefresh.Warehouses;

namespace OF.Data.Warehouses.UseCases
{
    public class GetWarehousesHandler
    {
        private readonly ApplicationDbContext dbContext;
        private readonly MDPDbContext mdpContext;
        private readonly ILogger<GetWarehousesHandler> logger;

        public GetWarehousesHandler(ApplicationDbContext dbContext, MDPDbContext mdpContext, ILogger<GetWarehousesHandler> logger)
        {
            this.dbContext = dbContext;
            this.mdpContext = mdpContext;
            this.logger = logger;
        }

        public async Task Handle()
        {
            logger.LogInformation($"Getting Warehouses from MDP.");

            using var transaction = dbContext.Database.BeginTransaction(IsolationLevel.ReadUncommitted);

            try
            {
                var warehouseItems = await mdpContext.OrganisationalHierarchy.Where(p => p.WarehouseCode != "UNKNOWN").ToListAsync();
                await dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE [dbo].[WarehouseItemsStaging]");
                logger.LogInformation($"Bulk Insert {warehouseItems.Count} entities in WarehouseItemsStaging table.");
                await dbContext.BulkInsertAsync(warehouseItems);
                logger.LogDebug($"Merging");
                await dbContext.Database.ExecuteSqlRawAsync(IONSQL.MergeWarehouses);

                await dbContext.DataRefreshStamp(Refresh.Key, Refresh.Description);
                await dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception exception)
            {
                logger.LogError(exception, $"Error persisting warehouse data.");
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
