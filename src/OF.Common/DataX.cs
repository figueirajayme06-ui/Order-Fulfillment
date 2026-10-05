using OF.Data.Database;

namespace OF.Common;

public static class DataX
{
    /// <summary>
    /// Produce the line item picking fulfillment attribute from the reservation
    /// </summary>
    public static string GetItemToPickAttributeMessage(this Reservation reservation)
    {
        var itemToPick = reservation.IsIndividualItem
            ? $"{Constants.M3LineText.ItemToPickText}:[{reservation.ItemNumber}] [{reservation.AssetId}]"
            : $"{Constants.M3LineText.ItemToPickText}:[{reservation.ItemNumber}] x{reservation.Quantity}";
        return itemToPick;
    }

    /// <summary>
    /// Produce the line depot fulfillment attribute from the reservation
    /// </summary>
    public static string GetDepotFulfilledAttributeMessage(this Reservation reservation, string? fulfillingDepotName)
    {
        if (string.IsNullOrWhiteSpace(fulfillingDepotName))
        {
            fulfillingDepotName = "Unknown";
        }

        var itemToPick = $"{Constants.M3LineText.DepotFulfillesFrom}:[{reservation.Warehouse}] [{fulfillingDepotName}] {Constants.M3LineText.DepotQuantity}: x{reservation.Quantity}";
        return itemToPick;
    }
}
