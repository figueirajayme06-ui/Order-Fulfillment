namespace OF.Common.Infrastructure.IPG.Orders.Models.Quotes
{
    public class QuoteLineData : SfObjectBase
    {
        public required string QuoteLineId { get; set; }

        public required int QuoteLineIndex { get; set; }

        public DateTime OnHireDate { get; set; }

        public DateTime OffHireDate { get; set; }

        public int Quantity { get; set; }

        public string? Attributes { get; set; }

        public string? LocalizedAttributes { get; set; }

        public string? CPQGroupName { get; set; }

        public string? GenericCode { get; set; }

        public int? LineTypeId { get; set; }

        public string? NumberOfShifts { get; set; }

        public DateTime? CollectionDate { get; set; }

        public string? DescriptionWithAttributes { get; set; }

        public string? ItemDescription { get; set; }
    }

    public class CustomerData : SfObjectBase
    {
        public string? CustomerNumber { get; set; }

        public string? Name { get; set; }
    }

    public class WarehouseData : SfObjectBase
    {
        public string? Name { get; set; }

        public string? Division { get; set; }

        public string? Facility { get; set; }
    }

    public class ContactData : SfObjectBase
    {
        public string? Name { get; set; }

        public string? Email { get; set; }

        public string? Phone { get; set; }
    }

    public partial class QuoteData : SfObjectBase
    {
        public string? OpportunityName { get; set; }
        public CustomerData? Customer { get; set; }

        public ContactData? Contact { get; set; }

        public string? StageName { get; set; }

        public WarehouseData? SourceWarehouse { get; set; }

        public IList<QuoteLineData> Lines { get; set; } = new List<QuoteLineData>();

        public DateTime OnHireDate { get; set; }

        public DateTime OffHireDate { get; set; }

        public string? OpportunityRecordId { get; set; }

        public string? QuoteRecordId { get; set; }

        public string? QuotePublicId { get; set; }

        public string? QuoteName { get; set; }

        public string? Address1 { get; set; }

        public string? OverviewOfServices { get; set; }

        public float Probability { get; set; }

        public string? RateType { get; set; }

        public DateTime? CollectionDate { get; set; }

        public DateTime? DeliveryDate { get; set; }
    }
}
