using CsvHelper.Configuration;

namespace OF.Common.Infrastructure.CloudSuite.Models.Agreements.DataLake
{
    public class IONAgreementDataMap : ClassMap<IONAgreementData>
    {
        public IONAgreementDataMap()
        {
            Map(m => m.Warehouse).Name("DPOT");
            Map(m => m.Facility).Name("FACI");
            Map(m => m.Division).Name("DIVI");
            Map(m => m.ProposalNumber).Name("AYRF");
            Map(m => m.CustomerSiteAccount).Name("SAID");
            Map(m => m.CustomerName).Name("ACNM");
            Map(m => m.CustomerAddress1).Name("ADR1");
            Map(m => m.CustomerAddress2).Name("ADR2");
            Map(m => m.CustomerAddress3).Name("ADR3");
            Map(m => m.CustomerAddress4).Name("ADR4");
            Map(m => m.CustomerAccount).Name("AGCN");
            Map(m => m.CustomersOrderRef).Name("CUOR");
            Map(m => m.Agreement).Name("AGNB");
            Map(m => m.AgreementHighestStatus).Name("ASTH");
            Map(m => m.AgreementLowestStatus).Name("ASTL");
            Map(m => m.RentalAgreementAmendment).Name("CFJ7");
            Map(m => m.QuoteID).Name("UCA2");
            Map(m => m.OrderID).Name("UCA3");
            Map(m => m.Source).Name("UCA4");
            Map(m => m.BaseLineID).Name("UCA6");
            Map(m => m.OrderItemNumber).Name("UDN1");
            Map(m => m.LinkedLine).Name("UDN2");
        }
    }
}
