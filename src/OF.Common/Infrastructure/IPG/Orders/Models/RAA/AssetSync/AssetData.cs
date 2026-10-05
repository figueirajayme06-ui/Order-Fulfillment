using Newtonsoft.Json;
using OF.Common.Utils;
using OF.Data.Database;

namespace OF.Common.Infrastructure.IPG.Orders.Models.RAA.AssetSync
{
    public class AssetData
    {
        [JsonProperty("ID")]
        public string? ID { get; set; }

        [JsonProperty("IndItemNumber")]
        public string? IndividualItemNumber { get; set; }

        [JsonProperty("ItemNumber")]
        public string? ItemNumber { get; set; }

        [JsonProperty("Warehouse")]
        public string? Warehouse { get; set; }

        [JsonProperty("Status")]
        public string? Status { get; set; }

        [JsonProperty("UnknownLocation")]
        public string? UnknownLocation { get; set; }

        [JsonProperty("StatusCode")]
        public string? StatusCode { get; set; }

        [JsonProperty("Facility")]
        public string? Facility { get; set; }

        [JsonProperty("Owner")]
        public string? Owner { get; set; }

        [JsonProperty("Description")]
        public string? Description { get; set; }

        [JsonProperty("TelemetryEnabled")]
        public string? TelemetryEnabled { get; set; }

        [JsonProperty("Manufacturer")]
        public string? Manufacturer { get; set; }

        [JsonProperty("FuelNumber")]
        public string? FuelNumber { get; set; }

        [JsonProperty("FuelNumberGALS")]
        public string? FuelNumberGALS { get; set; }

        [JsonProperty("AgreementNumber")]
        public string? AgreementNumber { get; set; }

        [JsonProperty("CurrentLineNumber")]
        public string? CurrentLineNumber { get; set; }

        [JsonProperty("Container")]
        public string? Container { get; set; }

        [JsonProperty("CustomerNumber")]
        public string? CustomerNumber { get; set; }

        [JsonProperty("CustomerName")]
        public string? CustomerName { get; set; }

        [JsonProperty("AgreementOffHireExpected")]
        public string? AgreementOffHireExpected { get; set; }

        [JsonProperty("AgreementOnHireExpected")]
        public string? AgreementOnHireExpected { get; set; }

        [JsonProperty("AgreementTerminationDate")]
        public string? AgreementTerminationDate { get; set; }

        [JsonProperty("LotNumber")]
        public string? LotNumber { get; set; }

        [JsonProperty("AgreementDeliveryDate")]
        public string? AgreementDeliveryDate { get; set; }

        [JsonProperty("ShipAddress")]
        public string? ShipAddress { get; set; }

        [JsonProperty("ShipAddress2")]
        public string? ShipAddress2 { get; set; }

        [JsonProperty("ShipAddress3")]
        public string? ShipAddress3 { get; set; }

        [JsonProperty("ShipAddress4")]
        public string? ShipAddress4 { get; set; }

        [JsonProperty("Salesperson")]
        public string? Salesperson { get; set; }

        [JsonProperty("Division")]
        public string? Division { get; set; }

        [JsonProperty("last_modified_DateTime\r\n")]
        public string? LastModified { get; set; }
    }

    public static class AssetDataMapping
    {
        public static Asset ToEntity(this AssetData assetData, Asset? asset)
        {
            if (asset == null)
            {
                asset = new Asset();
            }

            T KeepOriginalOrAssignIfPopulated<T>(T original, Func<T> newValue)
            {
                T value = newValue();

                if (value == null || string.IsNullOrWhiteSpace(value?.ToString()))
                {
                    return original;
                }

                return newValue();
            };

            asset.Id = KeepOriginalOrAssignIfPopulated(asset.Id, () => assetData.IndividualItemNumber!);
            asset.IndividualItemNumber = KeepOriginalOrAssignIfPopulated(asset.IndividualItemNumber, () => assetData.IndividualItemNumber!);
            asset.StatusCode = KeepOriginalOrAssignIfPopulated(asset.StatusCode, () => assetData?.StatusCode);
            asset.Warehouse = KeepOriginalOrAssignIfPopulated(asset.Warehouse, () => assetData?.Warehouse);
            asset.ShipAddress1 = KeepOriginalOrAssignIfPopulated(asset.ShipAddress1, () => assetData?.ShipAddress);
            asset.AgreementNumber = KeepOriginalOrAssignIfPopulated(asset.AgreementNumber, () => assetData?.AgreementNumber);
            asset.DeliveryDate = KeepOriginalOrAssignIfPopulated(null, () => assetData?.AgreementDeliveryDate.ParseIonExact());
            asset.AgreementLineValidFromDate = KeepOriginalOrAssignIfPopulated(null, () => assetData?.AgreementOnHireExpected.ParseIonExact());
            asset.AgreementLineValidToDate = KeepOriginalOrAssignIfPopulated(null, () => assetData?.AgreementOffHireExpected.ParseIonExact());
            asset.TerminationDate = KeepOriginalOrAssignIfPopulated(null, () => assetData?.AgreementTerminationDate.ParseIonExact());
            asset.CustomerName = KeepOriginalOrAssignIfPopulated(asset.CustomerName, () => assetData?.CustomerName);
            asset.TelemetryStatus = KeepOriginalOrAssignIfPopulated(asset.TelemetryStatus, () => assetData?.TelemetryEnabled);
            asset.ServiceCenter = KeepOriginalOrAssignIfPopulated(asset.ServiceCenter, () => assetData?.Owner);
            asset.Description = KeepOriginalOrAssignIfPopulated(asset.Description, () => assetData?.Description);
            asset.Status = KeepOriginalOrAssignIfPopulated(asset.Status, () => assetData?.Status);
            asset.ManufacturerName = KeepOriginalOrAssignIfPopulated(asset.ManufacturerName, () => assetData?.Manufacturer);
            asset.OwnerServiceCenter = KeepOriginalOrAssignIfPopulated(asset.OwnerServiceCenter, () => assetData?.Owner);
            asset.CustomerNumber = KeepOriginalOrAssignIfPopulated(asset.CustomerNumber, () => assetData?.CustomerNumber);
            asset.ItemNumber = KeepOriginalOrAssignIfPopulated(asset.ItemNumber, () => assetData?.ItemNumber);
            asset.ShipAddress3 = KeepOriginalOrAssignIfPopulated(asset.ShipAddress3, () => assetData?.ShipAddress3);
            asset.IonlastModified = DateTime.UtcNow; // This is essentially the LMTS
            asset.Facility = KeepOriginalOrAssignIfPopulated(asset.Facility, () => assetData?.Facility);
            asset.Division = KeepOriginalOrAssignIfPopulated(asset.Division, () => assetData?.Division);

            return asset;
        }
    }
}