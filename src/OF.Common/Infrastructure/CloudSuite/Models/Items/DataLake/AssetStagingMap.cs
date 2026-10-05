using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using OF.Data.Database;
using System.Globalization;

namespace OF.Common.Infrastructure.CloudSuite.Models.Items.DataLake
{
    public class CustomDateTimeConverter : ITypeConverter
    {
#pragma warning disable CS8767 // Nullability of reference types in type of parameter doesn't match implicitly implemented member (possibly because of nullability attributes).

        public object? ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length < 8)
            {
                return null;
            }

            if (DateTime.TryParse(text, out DateTime dateValue))
            {
                return dateValue;
            }

            if (DateTime.TryParseExact(text.Substring(0, 8), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out dateValue))
            {
                return dateValue;
            }

            return null;
        }

        public string ConvertToString(object value, IWriterRow row, MemberMapData memberMapData)
        {
            return ((DateTime)value).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        }

#pragma warning restore CS8767 // Nullability of reference types in type of parameter doesn't match implicitly implemented member (possibly because of nullability attributes).
    }

    public class AssetStagingMap : ClassMap<AssetsStaging>
    {
        public AssetStagingMap()
        {
            Map(m => m.Id).Name("Id");
            Map(m => m.IndividualItemNumber).Name("IndividualItemNumber");
            Map(m => m.ItemNumber).Name("ItemNumber");
            Map(m => m.Warehouse).Name("Warehouse");
            Map(m => m.WarehouseLocation).Name("WarehouseLocation");
            Map(m => m.Status).Name("Status");
            Map(m => m.StatusCode).Name("StatusCode");
            Map(m => m.Facility).Name("Facility");
            Map(m => m.ServiceCenter).Name("ServiceCenter");
            Map(m => m.OwnerServiceCenter).Name("OwnerServiceCenter");
            Map(m => m.Division).Name("Division");
            Map(m => m.Description).Name("Description");
            Map(m => m.TelemetryStatus).Name("TelemetryStatus");
            Map(m => m.ManufacturerName).Name("ManufacturerName");
            Map(m => m.AgreementNumber).Name("AgreementNumber");
            Map(m => m.CustomerNumber).Name("CustomerNumber");
            Map(m => m.CustomerName).Name("CustomerName");
            Map(m => m.AgreementLineValidToDate).Name("AgreementLineValidToDate").TypeConverter<CustomDateTimeConverter>();
            Map(m => m.AgreementLineValidFromDate).Name("AgreementLineValidFromDate").TypeConverter<CustomDateTimeConverter>();
            Map(m => m.TerminationDate).Name("TerminationDate").TypeConverter<CustomDateTimeConverter>();
            Map(m => m.DeliveryDate).Name("DeliveryDate").TypeConverter<CustomDateTimeConverter>();
            Map(m => m.CollectionDate).Name("CollectionDate").TypeConverter<CustomDateTimeConverter>();
            Map(m => m.ShipAddress1).Name("ShipAddress1");
            Map(m => m.ShipAddress3).Name("ShipAddress3");
            Map(m => m.IonlastModified).Name("IONLastModified").TypeConverter<CustomDateTimeConverter>();
            Map(m => m.Remark).Name("Remark");
            Map(m => m.RunHours).Name("RunHours");
        }
    }
}
