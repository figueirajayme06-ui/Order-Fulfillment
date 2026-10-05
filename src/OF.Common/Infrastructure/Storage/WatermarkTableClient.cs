using Azure.Data.Tables;

namespace OF.Common.Infrastructure.Storage
{
    public interface IWatermarkTableClient
    {
        Task<DateTime> GetAsync(string key);

        Task UpsertStampAsync(string key);
    }

    public class WatermarkTableClient : IWatermarkTableClient
    {
        public static string TableName = "Watermark";

        private readonly TableServiceClient tableServiceClient;

        public WatermarkTableClient(TableServiceClient tableServiceClient)
        {
            this.tableServiceClient = tableServiceClient;
        }

        public async Task UpsertStampAsync(string key)
        {
            TableClient tableClient = tableServiceClient.GetTableClient(TableName);

            var entity = new TableEntity(TableName, key)
            {
                { "LastSuccess", DateTime.UtcNow }
            };

            await tableClient.UpsertEntityAsync(entity);
        }

        public async Task<DateTime> GetAsync(string key)
        {
            TableClient tableClient = tableServiceClient.GetTableClient(TableName);
            var entity = await tableClient.GetEntityIfExistsAsync<TableEntity>(TableName, key);

            if (entity.HasValue && entity.Value!.ContainsKey("LastSuccess"))
            {
                if (DateTime.TryParse(entity.Value["LastSuccess"].ToString(), out DateTime date))
                {
                    return date;
                }
            }

            return DateTime.UtcNow.AddYears(-1);
        }
    }
}
