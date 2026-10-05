using OF.Common.Utils;
using OF.Data.Database;
using OF.Tests.Data.Test.BODs.AgreementLines;
using OF.Common.Infrastructure.CloudSuite.Models.Agreements.BODs;
using OF.Common.Infrastructure.CloudSuite.Models.Agreements.BODs.Exceptions;

namespace OF.Tests.Common.Models.AgreementLines
{
    public class AgreementLineDataTests
    {
        [SkippableTheory]
        [InlineData("FSLLABOUR")]
        [InlineData("FUEL OUT/IN")]
        [InlineData("MTR123")]
        [InlineData("XX123")]
        [InlineData("XH123")]
        [InlineData("TX123")]
        [InlineData("BD123")]
        [InlineData("BF123")]
        [InlineData("PF123")]
        [InlineData("SERV123")]
        [InlineData("YDEF OUT/IN")]
        public void Parse_Line_Message_To_Database_ToEntity_Has_RequiresFulfilled_False_When_In_Exclusions(string itemNumber)
        {
            var bod = string.Format(AgreementLineBOD.SyncDeliveredExcludedLine, itemNumber);
            SyncAGKRentalOrderLine? line = bod.ParseToBODResponse<SyncAGKRentalOrderLine>();
            Line entity = line.DataArea.ToLineEntity();
            Assert.NotNull(entity);
            Assert.False(entity.RequiresFulfilment);
        }

        [SkippableTheory]
        [InlineData("XAB123")]
        [InlineData("XGN1245566")]
        public void Parse_Line_Message_To_Database_ToEntity_Has_RequiresFulfilled_True_When_Not_In_Exclusions(string itemNumber)
        {
            var bod = string.Format(AgreementLineBOD.SyncDeliveredExcludedLine, itemNumber);
            SyncAGKRentalOrderLine? line = bod.ParseToBODResponse<SyncAGKRentalOrderLine>();
            Line entity = line.DataArea.ToLineEntity();
            Assert.NotNull(entity);
            Assert.True(entity.RequiresFulfilment);
        }

        [Fact]
        public void Parse_Line_Message_To_Database_ToEntity()
        {
            SyncAGKRentalOrderLine? line = AgreementLineBOD.Sync.ParseToBODResponse<SyncAGKRentalOrderLine>();
            Assert.NotNull(line);
            Assert.Equal("A712565", line.DataArea.AGKRentalOrderLine.AgreementNumber);
            Assert.Equal(3.0, line.DataArea.AGKRentalOrderLine.GetAgreementLineNumber());
            Assert.Equal("A712565-3", line.DataArea.AGKRentalOrderLine.GetAgreementLineId());
            Assert.Equal("8027a000007OnNjAAK", line.DataArea.AGKRentalOrderLine.AgreementLines.OrderLineRecordId);
            Assert.Equal(DateTime.Parse("2024-04-22T00:00:00Z").ToUniversalTime(), line.DataArea.AGKRentalOrderLine.AgreementLines.DeliveryDate);
            Assert.Equal(DateTime.Parse("2024-04-23T00:00:00Z").ToUniversalTime(), line.DataArea.AGKRentalOrderLine.AgreementLines.ValidFromDate);
            Assert.Equal(DateTime.Parse("2024-04-26T00:00:00Z").ToUniversalTime(), line.DataArea.AGKRentalOrderLine.AgreementLines.ValidToDate);
            Assert.Equal(DateTime.Parse("2024-04-25T00:00:00Z").ToUniversalTime(), line.DataArea.AGKRentalOrderLine.AgreementLines.TerminationDate);
            Assert.Equal(DateTime.Parse("2024-04-26T00:00:00Z").ToUniversalTime(), line.DataArea.AGKRentalOrderLine.AgreementLines.CollectionDate);
            Assert.Equal("20", line.DataArea.AGKRentalOrderLine.AgreementLines.AgreementLineStatus);
            Assert.Equal(1.0, line.DataArea.AGKRentalOrderLine.AgreementLines.OrderedQuantity);
            Assert.Equal("ABC", line.DataArea.AGKRentalOrderLine.AgreementLines.PackageGroupNumber);
            Assert.Equal("Additional information:Environmental Fee (Recurring) — 5.00%", line.DataArea.AGKRentalOrderLine.AgreementLines.TextIdentityDeliveryText?.Text);
            Assert.Equal("BD0", line.DataArea.AGKRentalOrderLine.AgreementLines.FromWarehouse);
            Assert.Equal("USG", line.DataArea.AGKRentalOrderLine.AgreementLines.Facility);
            Assert.Equal("200", line.DataArea.AGKRentalOrderLine.AgreementLines.Division);
            Assert.Equal("1713417424448|6", line.DataArea.AGKRentalOrderLine.AgreementLines.ChangeSequence);
            Assert.Equal("CPQ", line.DataArea.AGKRentalOrderLine.AgreementLines.OrderSource);
            Line entity = line.DataArea.ToLineEntity();
            Assert.NotNull(entity);
            Assert.Equal(entity.AgreementLineIndex, line.DataArea.AGKRentalOrderLine.GetAgreementLineNumber());
            Assert.Equal(entity.AgreementLineNumber, line.DataArea.AGKRentalOrderLine.GetAgreementLineId());
            Assert.Equal(entity.ItemNumber, line.DataArea.AGKRentalOrderLine.AgreementLines.ItemNumber);
            Assert.Equal(entity.AgreementLineType, line.DataArea.AGKRentalOrderLine.AgreementLines.AgreementLineType);
            Assert.Equal(entity.Status, line.DataArea.AGKRentalOrderLine.AgreementLines.AgreementLineStatus);
            Assert.Equal(entity.Quantity, line.DataArea.AGKRentalOrderLine.AgreementLines.OrderedQuantity);
            Assert.Equal(entity.PackageGroupNumber, line.DataArea.AGKRentalOrderLine.AgreementLines.PackageGroupNumber);
            Assert.Null(entity.Attributes);
            Assert.Equal(entity.DeliveryDate, line.DataArea.AGKRentalOrderLine.AgreementLines!.DeliveryDate);
            Assert.Equal(entity.ValidFromDate, line.DataArea.AGKRentalOrderLine.AgreementLines.ValidFromDate);
            Assert.Equal(entity.ValidToDate, line.DataArea.AGKRentalOrderLine.AgreementLines.ValidToDate);
            Assert.Equal(entity.TerminationDate, line.DataArea.AGKRentalOrderLine.AgreementLines.TerminationDate);
            Assert.Equal(entity.Warehouse, line.DataArea.AGKRentalOrderLine.AgreementLines.FromWarehouse);
            Assert.Equal(entity.Facility, line.DataArea.AGKRentalOrderLine.AgreementLines.Facility);
            Assert.Equal(entity.Division, line.DataArea.AGKRentalOrderLine.AgreementLines.Division);
            Assert.Equal(entity.OrderSource, line.DataArea.AGKRentalOrderLine.AgreementLines.OrderSource);
            Assert.Equal(1713417424448, entity.ChangeSequence);
        }

        [Fact]
        public void Parse_Line_Message_To_Database_ToEntity_Throws_When_Empty_Agreement()
        {
            var xml = AgreementLineBOD.Sync.Replace("<agreementNumber>A712565</agreementNumber>", "");
            SyncAGKRentalOrderLine? line = xml.ParseToBODResponse<SyncAGKRentalOrderLine>();
            var exception = Assert.Throws<BodValidationException>(() => _ = line.DataArea.ToLineEntity());
            Assert.Contains("The AgreementNumber field is required", exception.Message);
        }

        [Fact]
        public void Parse_Line_Message_To_Database_ToEntity_Throws_When_Agreement_Is_Not_A_Or_T()
        {
            var xml = AgreementLineBOD.Sync.Replace("<agreementNumber>A712565</agreementNumber>", "<agreementNumber>P700550</agreementNumber>");
            SyncAGKRentalOrderLine? line = xml.ParseToBODResponse<SyncAGKRentalOrderLine>();
            var exception = Assert.Throws<BodValidationException>(() => _ = line.DataArea.ToLineEntity());
            Assert.Contains("AgreementNumber 'P700550' does not start with T or A.", exception.Message);
        }

        [Fact]
        public void Parse_Line_Message_To_Database_ToEntity_Throws_When_Agreement_Line_Number_Empty()
        {
            var xml = AgreementLineBOD.Sync.Replace("<agreementLineId>A712565-3</agreementLineId>", "");
            SyncAGKRentalOrderLine? line = xml.ParseToBODResponse<SyncAGKRentalOrderLine>();
            var exception = Assert.Throws<BodValidationException>(() => _ = line.DataArea.ToLineEntity());
            Assert.Contains("The AgreementLineId field is required", exception.Message);
        }

        [Fact]
        public void Parse_Line_Message_To_Database_ToEntity_Throws_When_Empty_ChangeSequence()
        {
            var xml = AgreementLineBOD.Sync.Replace("<changeSequence>1713417424448|6</changeSequence>", "");
            SyncAGKRentalOrderLine? line = xml.ParseToBODResponse<SyncAGKRentalOrderLine>();
            var exception = Assert.Throws<BodValidationException>(() => _ = line.DataArea.ToLineEntity());
            Assert.Contains("ChangeSequence does not contain a long date part", exception.Message);
        }

        [Fact]
        public void Parse_Line_Message_To_Database_ToEntity_Throws_When_Empty_Division()
        {
            var xml = AgreementLineBOD.Sync.Replace("<division>200</division>", "");
            SyncAGKRentalOrderLine? line = xml.ParseToBODResponse<SyncAGKRentalOrderLine>();
            var exception = Assert.Throws<BodValidationException>(() => _ = line.DataArea.ToLineEntity());
            Assert.Contains("The Division field is required", exception.Message);
        }

        [Fact]
        public void Parse_Line_Message_To_Database_ToEntity_Throws_When_Empty_Facility()
        {
            var xml = AgreementLineBOD.Sync.Replace("<facility>USG</facility>", "");
            SyncAGKRentalOrderLine? line = xml.ParseToBODResponse<SyncAGKRentalOrderLine>();
            var exception = Assert.Throws<BodValidationException>(() => _ = line.DataArea.ToLineEntity());
            Assert.Contains("The Facility field is required", exception.Message);
        }

        [Fact]
        public void Parse_Line_Message_To_Database_ToEntity_Throws_When_Empty_Warehouse()
        {
            var xml = AgreementLineBOD.Sync.Replace("<fromWarehouse>BD0</fromWarehouse>", "");
            SyncAGKRentalOrderLine? line = xml.ParseToBODResponse<SyncAGKRentalOrderLine>();
            var exception = Assert.Throws<BodValidationException>(() => _ = line.DataArea.ToLineEntity());
            Assert.Contains("The FromWarehouse field is required", exception.Message);
        }

        [Fact]
        public void Parse_Line_Message_To_Database_ToEntity_Throws_When_Empty_ItemNumber()
        {
            var xml = AgreementLineBOD.Sync.Replace("<itemNumber>XDH51005R</itemNumber>", "");
            SyncAGKRentalOrderLine? line = xml.ParseToBODResponse<SyncAGKRentalOrderLine>();
            var exception = Assert.Throws<BodValidationException>(() => _ = line.DataArea.ToLineEntity());
            Assert.Contains("The ItemNumber field is required", exception.Message);
        }

        [Fact]
        public void Parse_Line_Message_To_Database_ToEntity_Throws_When_Empty_ValidFromDate()
        {
            var xml = AgreementLineBOD.Sync.Replace("<validFromDate>2024-04-23T00:00:00Z</validFromDate>", "");
            SyncAGKRentalOrderLine? line = xml.ParseToBODResponse<SyncAGKRentalOrderLine>();
            var exception = Assert.Throws<BodValidationException>(() => _ = line.DataArea.ToLineEntity());
            Assert.Contains("The ValidFromDate field is required", exception.Message);
        }

        [Fact]
        public void Parse_Line_Message_To_Database_ToEntity_Throws_When_Empty_ValidToDate()
        {
            var xml = AgreementLineBOD.Sync.Replace("<validToDate>2024-04-26T00:00:00Z</validToDate>", "");
            SyncAGKRentalOrderLine? line = xml.ParseToBODResponse<SyncAGKRentalOrderLine>();
            var exception = Assert.Throws<BodValidationException>(() => _ = line.DataArea.ToLineEntity());
            Assert.Contains("The ValidToDate field is required", exception.Message);
        }

        [Fact]
        public void Parse_Line_Message_To_Database_ToEntity_Does_Not_Set_Attributes_From_BOD_Text()
        {
            var dutchText = "Lengte (m):60;Kabeltype:Standaard/kabelschoenen";
            var xml = AgreementLineBOD.Sync.Replace(
                "Additional information:Environmental Fee (Recurring) — 5.00%",
                dutchText);
            SyncAGKRentalOrderLine? line = xml.ParseToBODResponse<SyncAGKRentalOrderLine>();
            Assert.Equal(dutchText, line.DataArea.AGKRentalOrderLine.AgreementLines.TextIdentityDeliveryText?.Text);
            Line entity = line.DataArea.ToLineEntity();
            Assert.Null(entity.Attributes);
        }
    }
}
