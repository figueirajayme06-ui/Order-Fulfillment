using OF.Common.Utils;
using OF.Data.Database;
using OF.Tests.Data.Test.BODs.Agreements;
using OF.Common.Infrastructure.CloudSuite.Models.Agreements.BODs;
using OF.Common.Infrastructure.CloudSuite.Models.Agreements.BODs.Exceptions;

namespace OF.Tests.Common.Models.Agreements
{
    public class AgreementHeaderDataTests
    {
        [Fact]
        public void Parse_Header_Message_To_Database_ToEntity()
        {
            SyncAGKRentalOrderHeader? header = AgreementBOD.Sync.ParseToBODResponse<SyncAGKRentalOrderHeader>();
            Assert.NotNull(header);
            Assert.Equal("Q-438254", header.DataArea.AGKRentalOrderHeader.AgreementHeaders.QuotePublicId);
            Assert.Equal("8017a000003GAerAAG", header.DataArea.AGKRentalOrderHeader.AgreementHeaders.OrderRecordId);
            Assert.Equal("A712565", header.DataArea.AGKRentalOrderHeader.AgreementHeaders.AgreementNumber);
            Assert.Equal("US00103535",header.DataArea.AGKRentalOrderHeader.AgreementHeaders.AgreementCustomer);
            Assert.Equal("NAM05Apr24180655", header.DataArea.AGKRentalOrderHeader.AgreementHeaders.CustomerName);
            Assert.Equal("1320 Bank Ave", header.DataArea.AGKRentalOrderHeader.AgreementHeaders.CustomerSiteAddressLine1);
            Assert.Equal("900000", header.DataArea.AGKRentalOrderHeader.AgreementHeaders.CustomerSiteAddress);
            Assert.Equal("20", header.DataArea.AGKRentalOrderHeader.AgreementHeaders.AgreementHighestStatus);
            Assert.Equal("USG", header.DataArea.AGKRentalOrderHeader.AgreementHeaders.Facility);
            Assert.Equal("200", header.DataArea.AGKRentalOrderHeader.AgreementHeaders.Division);
            Assert.Equal("1713417422608|5", header.DataArea.AGKRentalOrderHeader.AgreementHeaders.ChangeSequence);
            Assert.Equal("CPQ", header.DataArea.AGKRentalOrderHeader.AgreementHeaders.OrderSource);

            Header entity = header.DataArea.ToHeaderEntity();
            Assert.NotNull(entity);
            Assert.Equal(entity.QuotePublicId, header.DataArea.AGKRentalOrderHeader.AgreementHeaders.QuotePublicId);
            Assert.Equal(entity.AgreementNumber, header.DataArea.AGKRentalOrderHeader.AgreementHeaders.AgreementNumber);
            Assert.Equal(entity.CustomerNumber, header.DataArea.AGKRentalOrderHeader.AgreementHeaders.AgreementCustomer);
            Assert.Equal(entity.CustomerName, header.DataArea.AGKRentalOrderHeader.AgreementHeaders.CustomerName);
            Assert.Equal(entity.CustomerAddress, header.DataArea.AGKRentalOrderHeader.AgreementHeaders.CustomerSiteAddressLine1);
            Assert.Equal(entity.CustomerAddressCode, header.DataArea.AGKRentalOrderHeader.AgreementHeaders.CustomerSiteAddress);
            Assert.Equal(entity.Status, header.DataArea.AGKRentalOrderHeader.AgreementHeaders.AgreementHighestStatus);
            Assert.Equal(entity.Facility, header.DataArea.AGKRentalOrderHeader.AgreementHeaders.Facility);
            Assert.Equal(entity.Division, header.DataArea.AGKRentalOrderHeader.AgreementHeaders.Division);
            Assert.Equal(1713417422608, entity.ChangeSequence);
            Assert.Equal(entity.OrderSource, header.DataArea.AGKRentalOrderHeader.AgreementHeaders.OrderSource);
        }

        [Fact]
        public void Parse_Header_Message_To_Database_ToEntity_Throws_When_Empty_Agreement()
        {
            var xml = AgreementBOD.Sync.Replace("<agreementNumber>A712565</agreementNumber>", "");
            SyncAGKRentalOrderHeader? line = xml.ParseToBODResponse<SyncAGKRentalOrderHeader>();
            var exception = Assert.Throws<BodValidationException>(() => _ = line.DataArea.ToHeaderEntity());
            Assert.Contains("The AgreementNumber field is required", exception.Message);
        }

        [Fact]
        public void Parse_Header_Message_To_Database_ToEntity_Throws_When_Agreement_Is_Not_A_Or_T()
        {
            var xml = AgreementBOD.Sync.Replace("<agreementNumber>A712565</agreementNumber>", "<agreementNumber>P700497</agreementNumber>");
            SyncAGKRentalOrderHeader? line = xml.ParseToBODResponse<SyncAGKRentalOrderHeader>();
            var exception = Assert.Throws<BodValidationException>(() => _ = line.DataArea.ToHeaderEntity());
            Assert.Contains("AgreementNumber 'P700497' does not start with T or A.", exception.Message);
        }

        [Fact]
        public void Parse_Header_Message_To_Database_ToEntity_Throws_When_Empty_ChangeSequence()
        {
            var xml = AgreementBOD.Sync.Replace("<changeSequence>1713417422608|5</changeSequence>", "");
            SyncAGKRentalOrderHeader? line = xml.ParseToBODResponse<SyncAGKRentalOrderHeader>();
            var exception = Assert.Throws<BodValidationException>(() => _ = line.DataArea.ToHeaderEntity());
            Assert.Contains("ChangeSequence does not contain a long date part", exception.Message);
        }

        [Fact]
        public void Parse_Header_Message_To_Database_ToEntity_Throws_When_Empty_Division()
        {
            var xml = AgreementBOD.Sync.Replace("<division>200</division>", "");
            SyncAGKRentalOrderHeader? line = xml.ParseToBODResponse<SyncAGKRentalOrderHeader>();
            var exception = Assert.Throws<BodValidationException>(() => _ = line.DataArea.ToHeaderEntity());
            Assert.Contains("The Division field is required", exception.Message);
        }

        [Fact]
        public void Parse_Header_Message_To_Database_ToEntity_Throws_When_Empty_Facility()
        {
            var xml = AgreementBOD.Sync.Replace("<facility>USG</facility>", "");
            SyncAGKRentalOrderHeader? line = xml.ParseToBODResponse<SyncAGKRentalOrderHeader>();
            var exception = Assert.Throws<BodValidationException>(() => _ = line.DataArea.ToHeaderEntity());
            Assert.Contains("The Facility field is required", exception.Message);
        }
    }
}