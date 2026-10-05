using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OF.Common.Infrastructure.CloudSuite;
using OF.Data.Database;
using System.Data;
using Refresh = OF.Common.Constants.DataRefresh.NonSerialised;

namespace OF.Data.ProductItems.UseCases
{
    public class GetProductItemsHandler
    {
        private readonly ICloudSuiteService cloudsuiteService;
        private readonly ApplicationDbContext dbContext;
        private readonly ILogger<GetProductItemsHandler> logger;

        public GetProductItemsHandler(ICloudSuiteService cloudsuiteService, ApplicationDbContext dbContext, ILogger<GetProductItemsHandler> logger)
        {
            this.cloudsuiteService = cloudsuiteService;
            this.dbContext = dbContext;
            this.logger = logger;
        }

        public async Task Handle()
        {
            logger.LogInformation($"Getting product items from ION Data lake.");

            // Step 1: Fetch item numbers from CPQ_Item and ProductItemsSyncList
            var cpqItemNumbers = await dbContext.CpqItems
                .Where(x => x.ItemNumber != null)
                .Select(x => x.ItemNumber)
                .ToListAsync();
            var syncListNumbers = await dbContext.ProductItemsSyncList
                .Where(x => x.IsActive && x.ItemNumber != null)
                .Select(x => x.ItemNumber)
                .ToListAsync();

            // Step 2: Combine and deduplicate
            var allItemNumbers = cpqItemNumbers
                .Concat(syncListNumbers)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();

            if (!allItemNumbers.Any())
            {
                logger.LogWarning("No item numbers found for product item sync. Aborting sync.");
                return;
            }

            logger.LogInformation($"Found {allItemNumbers.Count} unique item numbers for sync.");

            // Step 3: Generate dynamic SQL
            var dynamicSql = GenerateDynamicProductDetailsQuery(allItemNumbers);

            var entities = await cloudsuiteService.RunDataLakeQuery<ProductItemsStaging>(dynamicSql);

            using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadUncommitted);

            try
            {
                logger.LogInformation($"Truncating the staging table.");
                await dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE [dbo].[ProductItems_Staging]");
                logger.LogInformation($"Bulk Insert {entities.Count} entities in staging table.");
                await dbContext.BulkInsertAsync(entities);
                logger.LogDebug($"Inserted {entities.Count} entities to staging table.");
                logger.LogDebug($"Swapping primary partitions on [ProductItems] and [ProductItems_Staging]");
                await dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE [dbo].[ProductItems]");
                await dbContext.Database.ExecuteSqlRawAsync("ALTER TABLE [dbo].[ProductItems_Staging] SWITCH PARTITION 1 TO [dbo].[ProductItems]");
                logger.LogDebug($"Data loaded and partitions swapped");

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

        private string GenerateDynamicProductDetailsQuery(List<string> itemNumbers)
        {
            var sanitizedItems = itemNumbers
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(SanitizeItemNumber)
                .ToList();

            var inClause = string.Join(",", sanitizedItems.Select(item => $"'{item}'"));

            return $@"select WHLO, ITNO, STQT, ALQT, AVAL, WHSL, FACI, DIVI, STAT from default.mitbal mb
                    where mb.CONO = '1'
                    and mb.ITNO IN
	                    (select ITNO from default.MITMAS mm
	                    where mm.ITNO in ({inClause})
		                    and mm.STAT = 20 --active in MITMAS, so stocked item
		                    and mm.INDI <> 2 --not lot controlled asset, so hide those
	                    )
                    and mb.STAT = '20' --active in that warehouse
                    and (mb.WHLO like '%0' or mb.WHLO like '%5') --DJ said only 0 and 5
                    order by ITNO, WHLO";
        }

        private string SanitizeItemNumber(string itemNumber)
        {
            if (string.IsNullOrWhiteSpace(itemNumber))
                return string.Empty;

            return itemNumber.Trim()
                .Replace("'", "''")
                .Replace(";", "")
                .Replace("--", "")
                .Replace("/*", "")
                .Replace("*/", "");
        }
    }
}
