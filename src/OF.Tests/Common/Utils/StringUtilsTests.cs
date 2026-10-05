using OF.Common.Infrastructure.CloudSuite.Models.Agreements.BODs;
using OF.Common.Infrastructure.CloudSuite.Models.Agreements.BODs.Common;
using OF.Common.Utils;
using Xunit;

namespace OF.Tests.Common.Utils
{
    public class StringUtilsTests
    {
        private readonly SyncAGKRentalOrderLine bod = new SyncAGKRentalOrderLine
        {
            DataArea = new AgreementLineDataArea
            {
                Sync = new Sync
                {
                    ActionCriteria = new ActionCriteria
                    {
                        ActionExpression = new ActionExpression
                        {
                            ActionCode = ""
                        }
                    }
                },
                AGKRentalOrderLine = new AGKRentalOrderLine
                {
                    AgreementNumberId = "",
                    AgreementLineId = "",
                    AgreementLineNumber = 1,
                    AgreementNumber = "",
                    AgreementLines = new AgreementLineData
                    {
                        AgreementLineStatus = "",
                        Division = "",
                        Facility = "",
                        ItemNumber = "DefaultItem",
                        GenericItemNumber = "DefaultGenericItem",
                        FromWarehouse = "DefaultWarehouse",
                        OrderedQuantity = 10,
                        LotNumber = "DefaultLotNumber"
                    }
                }
            }
        };

        [Theory]
        [InlineData("Item to Pick:[DB0063FXC143] [XDSC465]")]
        [InlineData("Qty: x3;Item to Pick:[DB0063FXC143] [XDSC465]")]
        [InlineData("Qty: x3;Item to Pick:[DB0063FXC143] [XDSC465];Voltage Range:Low Voltage")]
        [InlineData("Item to Pick:[DB0063FXC143] [XDSC465];Voltage Range:Low Voltage")]
        public void Get_Serialized_Items_From_Item_Attributes(string input)
        {
            var result = input.GetItemsFromAttributeText(bod);

            Assert.NotNull(result);
            Assert.Equal("DB0063FXC143", result.ItemNumber);
            Assert.Equal("XDSC465", result.LotNumber);
            Assert.Equal("DefaultWarehouse", result.Warehouse);
            Assert.False(result.IsRehire);
            Assert.False(result.IsDepotFulfil);
            Assert.True(result.ContainsAllocation);
        }

        [Theory]
        [InlineData("Item to Pick:[DB0063FXC143] x13.5", 13.5)]
        [InlineData("Qty: x3;Item to Pick:[DB0063FXC143] x13.5", 13.5)]
        [InlineData("Qty: x3;Item to Pick:[DB0063FXC143] x13.5;Voltage Range:Low Voltage", 13.5)]
        [InlineData("Qty: x3;Item to Pick:[DB0063FXC143] x13;Voltage Range:Low Voltage", 13.0)]
        [InlineData("Item to Pick:[DB0063FXC143] x13.5;Voltage Range:Low Voltage", 13.5)]
        public void Get_NonSerialized_Items_From_Item_Attributes(string input, float quantity)
        {
            var result = input.GetItemsFromAttributeText(bod);

            Assert.NotNull(result);
            Assert.Equal("DB0063FXC143", result.ItemNumber);
            Assert.Equal("DB0063FXC143", result.LotNumber);
            Assert.Equal("DefaultWarehouse", result.Warehouse);
            Assert.Equal(quantity, result.Quantity);
            Assert.False(result.IsRehire);
            Assert.False(result.IsDepotFulfil);
            Assert.True(result.ContainsAllocation);
        }

        [Theory]
        [InlineData("Depot fulfills from:[ED0] [Dumbarton] Qty : x12.5", 12.5)]
        [InlineData("Qty: x3;Depot fulfills from:[ED0] [Dumbarton] Qty : x12.5", 12.5)]
        [InlineData("Qty: x3;Depot fulfills from:[ED0] [Dumbarton] Qty : x12", 12.0)]
        [InlineData("Qty: x3;Depot fulfills from:[ED0] [Dumbarton] Qty : x12.5;Voltage Range:Low Voltage", 12.5)]
        [InlineData("Depot fulfills from:[ED0] [Dumbarton] Qty : x12.5;Voltage Range:Low Voltage", 12.5)]
        public void Get_DepotFulfil_From_Item_Attributes(string input, float quantity)
        {
            var result = input.GetItemsFromAttributeText(bod);

            Assert.NotNull(result);
            Assert.True(result.IsDepotFulfil);
            Assert.Equal("ED0", result.Warehouse);
            Assert.Equal(quantity, result.Quantity);
            Assert.Equal("DefaultItem", result.ItemNumber);
            Assert.Equal("DefaultLotNumber", result.LotNumber);
            Assert.False(result.IsRehire);
            Assert.True(result.ContainsAllocation);
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData("", null)]
        [InlineData(" ", null)]
        [InlineData("Some unrelated input text", null)]
        [InlineData("Another unrelated text", null)]
        [InlineData("Yet another unrelated text", null)]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("Some unrelated input text")]
        [InlineData("Another unrelated text")]
        [InlineData("Yet another unrelated text")]
        public void Get_Default_Values_From_Item_Attributes(string? input, string defaultLotNumber = "DefaultLotNumber")
        {
            bod.DataArea.AGKRentalOrderLine.AgreementLines.LotNumber = defaultLotNumber;

            var result = input.GetItemsFromAttributeText(bod);

            Assert.NotNull(result);
            Assert.Equal("DefaultItem", result.ItemNumber);
            Assert.Equal(defaultLotNumber == null ? "DefaultItem" : "DefaultLotNumber", result.LotNumber);
            Assert.Equal("DefaultWarehouse", result.Warehouse);
            Assert.Equal(10, result.Quantity);
            Assert.False(result.IsRehire);
            Assert.False(result.IsDepotFulfil);
            Assert.False(result.ContainsAllocation);
        }
    }
}
