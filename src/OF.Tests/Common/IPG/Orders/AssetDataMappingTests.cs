using FluentAssertions;
using OF.Common.Infrastructure.IPG.Orders.Models.RAA.AssetSync;
using OF.Data.Database;

namespace OF.Tests.Common.IPG.Orders;

public class AssetDataMappingTests
{
    // ──────────────────────────────────────────────────────────────────────
    // AgreementNumber — was previously cleared by ION syncs that omit it
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void WhenIncomingAssetDataHasNoAgreementNumber_ExistingAgreementNumberIsPreserved()
    {
        // Arrange
        var existingAsset = new Asset { Id = "ASSET001", AgreementNumber = "AGR-12345" };
        var incomingData = new AssetData { IndividualItemNumber = "ASSET001", AgreementNumber = null };

        // Act
        var result = incomingData.ToEntity(existingAsset);

        // Assert
        result.AgreementNumber.Should().Be("AGR-12345");
    }

    [Fact]
    public void WhenIncomingAssetDataHasEmptyAgreementNumber_ExistingAgreementNumberIsPreserved()
    {
        // Arrange
        var existingAsset = new Asset { Id = "ASSET001", AgreementNumber = "AGR-12345" };
        var incomingData = new AssetData { IndividualItemNumber = "ASSET001", AgreementNumber = string.Empty };

        // Act
        var result = incomingData.ToEntity(existingAsset);

        // Assert
        result.AgreementNumber.Should().Be("AGR-12345");
    }

    [Fact]
    public void WhenIncomingAssetDataHasAgreementNumber_ExistingValueIsOverwritten()
    {
        // Arrange
        var existingAsset = new Asset { Id = "ASSET001", AgreementNumber = "AGR-OLD" };
        var incomingData = new AssetData { IndividualItemNumber = "ASSET001", AgreementNumber = "AGR-NEW" };

        // Act
        var result = incomingData.ToEntity(existingAsset);

        // Assert
        result.AgreementNumber.Should().Be("AGR-NEW");
    }

    [Fact]
    public void WhenNewAsset_AndIncomingDataHasAgreementNumber_AgreementNumberIsSet()
    {
        // Arrange
        var incomingData = new AssetData { IndividualItemNumber = "ASSET002", AgreementNumber = "AGR-12345" };

        // Act
        var result = incomingData.ToEntity(null);

        // Assert
        result.AgreementNumber.Should().Be("AGR-12345");
    }

    [Fact]
    public void WhenNewAsset_AndIncomingDataHasNoAgreementNumber_AgreementNumberIsNull()
    {
        // Arrange
        var incomingData = new AssetData { IndividualItemNumber = "ASSET003", AgreementNumber = null };

        // Act
        var result = incomingData.ToEntity(null);

        // Assert
        result.AgreementNumber.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────────────
    // Other fields still use KeepOriginalOrAssignIfPopulated (regression)
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void WhenIncomingAssetDataHasNoWarehouse_ExistingWarehouseIsPreserved()
    {
        var existingAsset = new Asset { Id = "ASSET001", Warehouse = "Doncaster" };
        var incomingData = new AssetData { IndividualItemNumber = "ASSET001", Warehouse = null };

        var result = incomingData.ToEntity(existingAsset);

        result.Warehouse.Should().Be("Doncaster");
    }

    [Fact]
    public void WhenIncomingAssetDataHasWarehouse_WarehouseIsUpdated()
    {
        var existingAsset = new Asset { Id = "ASSET001", Warehouse = "Doncaster" };
        var incomingData = new AssetData { IndividualItemNumber = "ASSET001", Warehouse = "Aberdeen" };

        var result = incomingData.ToEntity(existingAsset);

        result.Warehouse.Should().Be("Aberdeen");
    }
}
