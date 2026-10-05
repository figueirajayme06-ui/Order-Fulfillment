using FluentAssertions;
using OF.Common;

namespace OF.Tests.Common;

public class WarehouseConstantsTests
{
    [Theory]
    [InlineData("EA0")]
    [InlineData("gb0")]
    [InlineData(" HK0 ")]
    public void IsExcluded_RecognizesKnownInactiveWarehouses(string warehouseCode)
    {
        Constants.Warehouses.IsExcluded(warehouseCode).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ED0")]
    public void IsExcluded_KeepsActiveOrMissingWarehouseCodes(string? warehouseCode)
    {
        Constants.Warehouses.IsExcluded(warehouseCode).Should().BeFalse();
    }
}
