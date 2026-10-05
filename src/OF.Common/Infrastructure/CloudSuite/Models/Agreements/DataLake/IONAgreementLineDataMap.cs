using CsvHelper.Configuration;

namespace OF.Common.Infrastructure.CloudSuite.Models.Agreements.DataLake
{
    public class IONAgreementLineDataMap : ClassMap<IONAgreementLineData>
    {
        public IONAgreementLineDataMap()
        {
            Map(m => m.AgreementNumber).Name("AGNB");
            Map(m => m.LineNumber).Name("PONR");
            Map(m => m.LineSuffix).Name("POSX");
            Map(m => m.Company).Name("CONO");
            Map(m => m.Division).Name("DIVI");
            Map(m => m.Facility).Name("FACI");
            Map(m => m.ItemNumber).Name("ITNO");
            Map(m => m.LineType).Name("LTYP");
            Map(m => m.AgreementLineStatus).Name("ASTH");
            Map(m => m.CustomerOrderNumber).Name("ORNO");
            Map(m => m.CustomerSite).Name("CUPL");
            Map(m => m.AddressNumber).Name("SAID");
            Map(m => m.FromWarehouse).Name("FWHL");
            Map(m => m.DeliveryOrderNumber).Name("DOND");
            Map(m => m.DeliveryOrderLineNumber).Name("DOLD");
            Map(m => m.CollectionOrderNumber).Name("DONR");
            Map(m => m.CollectionOrderLineNumber).Name("DOLR");
            Map(m => m.NumberOfShifts).Name("ANOS");
            Map(m => m.OrderedQuantity).Name("ORQT");
            Map(m => m.DeliveryDate).Name("DLDT");
            Map(m => m.CollectionDateReturnDate).Name("COLD");
            Map(m => m.ShipAddress1).Name("SAD1");
            Map(m => m.ShipAddress2).Name("SAD2");
            Map(m => m.ShipAddress3).Name("SAD3");
            Map(m => m.ShipAddress4).Name("SAD4");
            Map(m => m.TelephoneNumber).Name("SPHN");
            Map(m => m.TerminationDate).Name("TEDA");
            Map(m => m.ToWarehouse).Name("TWHL");
            Map(m => m.AgreementLineTextId).Name("TXID");
            Map(m => m.SubstituteFlag).Name("DURT");
            Map(m => m.AgreementFromDate).Name("FVDT");
            Map(m => m.AgreementToDate).Name("LVDT");
            Map(m => m.QuoteLineId).Name("UCA2");
            Map(m => m.OrderLineId).Name("UCA3");
            Map(m => m.Source).Name("UCA4");
            Map(m => m.NumberOfUsedDaysOnHire).Name("UDAY");
            Map(m => m.TextIdentityDeliveryOrderText).Name("DETX");
            Map(m => m.TextIdentityCollectionText).Name("COTX");
            Map(m => m.TextIdentityPOText).Name("POTX");
            Map(m => m.TextIdentityPRText).Name("PRTX");
            Map(m => m.DeliveryNumber).Name("DOND");
            Map(m => m.DeliveryLine).Name("DOLD");
            Map(m => m.ReturnNumber).Name("DONR");
            Map(m => m.ReturnLine).Name("DOLR");
            Map(m => m.LotNumber).Name("BANO");
            Map(m => m.SubstituteItem).Name("ILIT");
            Map(m => m.GenericItem).Name("GEIT");
            Map(m => m.DeliveryWindowStartDate).Name("DETH");
            Map(m => m.DeliveryWindowStartTime).Name("DETM");
            Map(m => m.CollectionWindowStartDate).Name("COTH");
            Map(m => m.CollectionWindowStartTime).Name("CLTM");
            Map(m => m.DeliveryWindowEnd).Name("CFJ4");
            Map(m => m.CollectionWindowEnd).Name("CFJ9");
            Map(m => m.PackageNumber).Name("CFJ6");
            Map(m => m.PackageSortLine).Name("CFJ8");
            Map(m => m.ProposalNumber).Name("AYRF");
            Map(m => m.RateType).Name("CCAP");
        }
    }
}
