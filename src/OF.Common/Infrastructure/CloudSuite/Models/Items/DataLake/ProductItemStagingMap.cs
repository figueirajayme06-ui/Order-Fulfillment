using CsvHelper.Configuration;
using OF.Data.Database;

namespace OF.Common.Infrastructure.CloudSuite.Models.Items.DataLake
{
    public class ProductItemStagingMap : ClassMap<ProductItemsStaging>
    {
        public ProductItemStagingMap()
        {
            Map(m => m.Warehouse).Name("WHLO");
            Map(m => m.ItemNumber).Name("ITNO");
            Map(m => m.StockQuantity).Name("STQT");
            Map(m => m.AllocatedQuantity).Name("ALQT");
            Map(m => m.DefaultLocation).Name("WHSL");
            Map(m => m.Facility).Name("FACI");
            Map(m => m.Division).Name("DIVI");
            Map(m => m.Status).Name("STAT");
        }
    }
}
