using OF.Common.Infrastructure.CloudSuite.Models.Agreements.BODs.Common;
using OF.Data.Database;
using System.ComponentModel.DataAnnotations;

namespace OF.Common.Infrastructure.CloudSuite.Models.Agreements.DataLake
{
    public class IONAgreementData : BaseValidatable
    {
        [Required]
        public required string Warehouse { get; set; }

        [Required]
        public required string Division { get; set; }

        [Required]
        public required string Facility { get; set; }

        public string? ProposalNumber { get; set; }

        public string? CustomerSiteAccount { get; set; }

        public string? CustomerSiteAddress { get; set; }

        [Required]
        public required string CustomerAccount { get; set; }

        public string? CustomersOrderRef { get; set; }

        [Required]
        public required string Agreement { get; set; }

        [Required]
        public required string AgreementHighestStatus { get; set; }

        public string? AgreementLowestStatus { get; set; }

        public string? RentalAgreementAmendment { get; set; }

        public string? QuoteID { get; set; }

        public string? OrderID { get; set; }

        public string? Source { get; set; }

        public string? BaseLineID { get; set; }

        public string? OrderItemNumber { get; set; }

        public string? LinkedLine { get; set; }

        [Required]
        public required string CustomerName { get; set; }

        public string? CustomerAddress1 { get; set; }

        public string? CustomerAddress2 { get; set; }

        public string? CustomerAddress3 { get; set; }

        public string? CustomerAddress4 { get; set; }

        public Header ToHeaderEntity(Header? existing = null)
        {
            if (existing == null)
            {
                existing = new Header();
            }

            existing.QuotePublicId = ProposalNumber;
            existing.AgreementNumber = Agreement;
            existing.AgreementNumbersOnly = Agreement.Length > 0 ? Agreement.Substring(1) : Agreement;
            existing.Status = AgreementHighestStatus;
            existing.CustomerName = CustomerName;
            existing.CustomerNumber = CustomerAccount;
            existing.CustomerAddress = string.Join(",", new[] { CustomerAddress1, CustomerAddress2, CustomerAddress3, CustomerAddress4 }.Where(i => !string.IsNullOrWhiteSpace(i)));
            existing.CustomerAddressCode = CustomerSiteAccount;
            existing.Facility = Facility;
            existing.Division = Division;
            existing.OrderSource = "ION";
            existing.ChangeSequence = 0;
            existing.IsDeleted = false;
            existing.QuotePublicIdNumbersOnly = ProposalNumber?.Length >= 2 ? ProposalNumber.Substring(2) : ProposalNumber;
            existing.RentalDepot = Warehouse;

            return existing;
        }
    }
}
