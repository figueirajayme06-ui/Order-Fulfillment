using OF.Common.Utils;
using OF.Tests.Data.Test.BODs.Agreements;
using OF.Common.Infrastructure.CloudSuite.Models.Agreements.BODs;
using OF.Common.Infrastructure.CloudSuite.Models.Agreements.BODs.Exceptions;

namespace OF.Tests.Common.Models.Acknowledgement
{
    public class AcknowledgementDataTests
    {
        [Fact]
        public void Parse_Acknowledge_Message_To_Model()
        {
            AcknowledgeAGKRentalAgreementActivation? bod = AgreementBOD.Activation.ParseToBODResponse<AcknowledgeAGKRentalAgreementActivation>();
            Assert.NotNull(bod);
            Assert.Equal("A712565", bod.DataArea.AGKRentalAgreementActivation.AgreementNumber);
            Assert.Equal("C09", bod.DataArea.AGKRentalAgreementActivation.ReasonCodeCreated);
            Assert.Equal("8017a000003GAerAAG", bod.DataArea.AGKRentalAgreementActivation.OrderRecordId);
            Assert.Equal("f0ec62d9-92a3-4ea1-9ea6-3b8fa773fc46", bod.DataArea.AGKRentalAgreementActivation.CorrelationId);
            Assert.Equal("190", bod.DataArea.AGKRentalAgreementActivation.OrderStatus);
            Assert.Equal("Accepted", bod.DataArea.Acknowledge.ResponseCriteria.ResponseExpression.ActionCode);
        }

        [Fact]
        public void Parse_Acknowledge_Validate_Throws_When_Empty_ActionCode()
        {
            var xml = AgreementBOD.Activation.Replace("actionCode=\"Accepted\"", "");
            AcknowledgeAGKRentalAgreementActivation? line = xml.ParseToBODResponse<AcknowledgeAGKRentalAgreementActivation>();
            var exception = Assert.Throws<BodValidationException>(() => line.DataArea.Validate());
            Assert.Contains("The ActionCode field is required", exception.Message);
        }

        [Fact]
        public void Parse_Acknowledge_Validate_Throws_When_Agreement_Is_Not_A_Or_T()
        {
            var xml = AgreementBOD.Activation.Replace("<agreementNumber>A712565</agreementNumber>", "<agreementNumber>P700550</agreementNumber>");
            AcknowledgeAGKRentalAgreementActivation? line = xml.ParseToBODResponse<AcknowledgeAGKRentalAgreementActivation>();
            var exception = Assert.Throws<BodValidationException>(() => line.DataArea.Validate());
            Assert.Contains("AgreementNumber 'P700550' does not start with T or A.", exception.Message);
        }

        [Fact]
        public void Parse_Acknowledge_Validate_Throws_When_Agreement_Line_Number_Empty()
        {
            var xml = AgreementBOD.Activation.Replace("<agreementNumber>A712565</agreementNumber>", "");
            AcknowledgeAGKRentalAgreementActivation? line = xml.ParseToBODResponse<AcknowledgeAGKRentalAgreementActivation>();
            var exception = Assert.Throws<BodValidationException>(() => line.DataArea.Validate());
            Assert.Contains("The AgreementNumber field is required", exception.Message);
        }

        [Fact]
        public void Parse_Acknowledge_Rejected_Gets_Technical_Error_From_UserArea()
        {
            AcknowledgeAGKRentalAgreementActivation bod = AgreementBOD.ActivationFailed.ParseToBODResponse<AcknowledgeAGKRentalAgreementActivation>();
            var error = bod.DataArea.GetTechnicalErrorMessage();
            Assert.NotNull(error);
            Assert.Contains("Reason code - created agreement 13 does not exist", error);
        }

        [Fact]
        public void Parse_Acknowledge_Rejected_Gets_Technical_Error_From_Direct_Element()
        {
            AcknowledgeAGKRentalAgreementActivation bod = AgreementBOD.ActivationFailedDirectError.ParseToBODResponse<AcknowledgeAGKRentalAgreementActivation>();
            var error = bod.DataArea.GetTechnicalErrorMessage();
            Assert.NotNull(error);
            Assert.Contains("Customer is blocked", error);
        }
    }
}
