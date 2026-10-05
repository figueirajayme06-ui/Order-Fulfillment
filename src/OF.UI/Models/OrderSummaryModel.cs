using OF.Common.Infrastructure.IPG.Orders.Models.Quotes;
using OF.Data.Database;

namespace OF.UI.Models
{
    public class OrderSummaryModel
    {
        public Header? Header { get; set; }

        public Opportunity? Opportunity { get; set; }

        public string AgreementNumber => Header?.AgreementNumber ?? Opportunity?.AgreementNumber;

        public string CustomerName => Header?.CustomerName ?? Opportunity?.Account?.Name!;

        public string CustomerNumber => Header?.CustomerNumber ?? Opportunity?.Account?.M3CustomerNumber;

        public DateTime? DeliveryDate => Opportunity?.Quote.DeliveryDate ?? Header?.Lines?.Min(i => i.DeliveryDate) ?? Opportunity?.Quote?.OnHireDate;

        public string? DeliverySlot => string.Join(" - ", (new string?[] { Opportunity?.Quote?.DeliverySlotStartTime?.Substring(0,5), Opportunity?.Quote?.DeliverySlotEndTime?.Substring(0, 5) }).Where(i => !string.IsNullOrWhiteSpace(i)));

        public DateTime? MinOnHire => Header?.Lines?.Min(i => i.ValidFromDate) ?? Opportunity?.Quote?.OnHireDate ?? Opportunity?.Quote?.OnHireDate;

        public string ShippingAddress => Opportunity?.Quote?.Order?.ShippingAddress ?? Header?.CustomerAddress ?? Opportunity?.Address?.Street;

        public bool HasSiteContact => new string? [] { SiteContactName, SiteContactEmail, SiteContactPhone }.Any(i => !string.IsNullOrWhiteSpace(i));

        public string? SiteContactName => Opportunity?.Quote?.SiteContactName;

        public string? SiteContactEmail => Opportunity?.Quote?.SiteContactEmail;

        public string? SiteContactPhone => Opportunity?.Quote?.SiteContactMobile;

        public bool HasProposalContact => new string?[] { ProposalContactName, ProposalContactEmail }.Any(i => !string.IsNullOrWhiteSpace(i));

        public string? ProposalContactName => Opportunity?.Quote?.ProposalContactName;

        public string? ProposalContactEmail => Opportunity?.Quote?.ProposalContactEmail;

        public string? ProposalContactPhone => null; // TODO: Get from SF

        public bool HasOwner => new string?[] { OwnerName }.Any(i => !string.IsNullOrWhiteSpace(i));

        public string? OwnerName => Opportunity?.Quote?.Order?.OwnerName;

        public bool ARMRequired => Opportunity?.Quote?.ARMRequired ?? false;

        public string? ARMContactHours => Opportunity?.Quote?.ARMContactHours;

        public string? FreightMethod => Opportunity?.Quote?.FreightMethod;

        public string? FreightDetails => Opportunity?.Quote?.FreightDetails;

        public string? FuelServiceSelected => Opportunity?.Quote?.FuelServiceSelected;

        public string? FuelServiceDetails => Opportunity?.Quote?.FuelServiceDetails;

        public string? OperationalRequirementsDetails => Opportunity?.Quote?.OperationalRequirementsDetails;

        public string? SiteSpecificRequirements => Opportunity?.Quote?.SiteSpecificRequirements;

        public string? OrderNotes => Opportunity?.Quote?.OrderNotes ?? Opportunity?.Quote?.QuoteNotes;

        public bool AggrekoTechRequired => Opportunity?.Quote?.AggrekoTechRequired ?? false;

        public string? TechnicianRequirementDetails => Opportunity?.Quote?.TechnicianRequirementDetails;

        public string? TechnicalRequirements => Opportunity?.Quote?.TechnicalRequirements;

        public string? OverviewOfServices => Opportunity?.Quote?.OverviewOfServices;

        public IList<OrderSummaryLineSectionModel> Equipment => GetFromHeaders();

        public IList<OrderSummaryLineChargeModel> OneTimeCharges => GetCharges("One Time");

        public IList<OrderSummaryLineChargeModel> VariableCharges => GetCharges("Variable");

        public IList<Reservation>? Reservations { get; set; }

        public IList<WarehouseItem>? Warehouses { get; set; }

        public IList<OpportunityQuoteLine>? Lines { get; set; }

        public IList<Note>? Notes { get; set; }

        private IList<OrderSummaryLineSectionModel>? GetFromHeaders()
        {
            if (Header?.Lines?.Any() != true)
            {
                return new List<OrderSummaryLineSectionModel>();
            }

            IList<OrderSummaryLineModel> equipment = new List<OrderSummaryLineModel>();

            foreach (var line in Header?.Lines?.OrderBy(i => i.AgreementLineIndex).Where(i => !i.IsDeleted))
            {
                var reservation = Reservations?.FirstOrDefault(i => i.LineId == line.Id);
                string warehouseCode = reservation?.Warehouse ?? line.Warehouse;
                string warehouse = Warehouses?.FirstOrDefault(i => i.WarehouseCode == warehouseCode)?.Warehouse ?? warehouseCode;
                var quoteLine = Lines?.FirstOrDefault(i => i.ProductCode == line.GenericItemNumber);

                var item = new OrderSummaryLineModel()
                {
                    Warehouse = warehouse,
                    Actions = reservation?.Notes,
                    Asset = reservation?.AssetId != reservation?.ItemNumber ? reservation?.AssetId : null,
                    Attributes = line.LocalizedAttributes ?? line.Attributes ?? quoteLine?.LocalizedAttributesAsText ?? quoteLine?.SelectedAttributesAsText,
                    Description = line.DescriptionWithAttributes ?? quoteLine?.DescriptionWithAttributes,
                    GenericCode = line.GenericItemNumber,
                    ItemNumber = (reservation?.IsDepotFulfilled == true ? "DEPOT" : (reservation?.IsRehire == true ? "REHIRE" : reservation?.ItemNumber)) ?? line.ItemNumber,
                    LineNumber = line.AgreementLineIndex,
                    OnHireDate = line.ValidFromDate,
                    OffHireDate = line.ValidToDate,
                    Quantity = reservation?.Quantity ?? (int)line.Quantity
                };

                equipment.Add(item);
            }

            return equipment.GroupBy(i => i.Warehouse).Select(i => new OrderSummaryLineSectionModel()
            {
                Warehouse = i.Key,
                Lines = i.ToList()
            }).ToList();
        }

        private IList<OrderSummaryLineChargeModel>? GetCharges(string chargeType)
        {
            if (Lines == null || Lines?.Any() == false)
            {
                return new List<OrderSummaryLineChargeModel>();
            }

            var exclusions = new string[] { "FUEL OUT/IN", "MTR-14" };
            var lines = Lines?.Where(i => i?.ProposalSection?.ToUpper() == chargeType.ToUpper() && !exclusions.Contains(i.ProductCode?.ToUpper())).ToList();

            IList<OrderSummaryLineChargeModel> charges = new List<OrderSummaryLineChargeModel>();

            foreach (var line in lines)
            {
                var item = new OrderSummaryLineChargeModel()
                {
                    Description = line.DescriptionWithAttributes,
                    ItemNumber = line.ProductCode,
                    Quantity = line.Quantity
                };

                charges.Add(item);
            }

            return charges;
        }
    }

    public class OrderSummaryLineSectionModel
    {
        public string Warehouse { get; set; }

        public IList<OrderSummaryLineModel>? Lines { get; set; }
    }

    public class OrderSummaryLineModel
    {
        public string Warehouse { get; set; }

        public int? LineNumber { get; set; }

        public string? Asset { get; set; }

        public string? ItemNumber { get; set; }

        public string? GenericCode { get; set; }

        public string? Description { get; set; }

        public decimal Quantity { get; set; }

        public string? Attributes { get; set; }

        public DateTime OnHireDate { get; set; }

        public DateTime OffHireDate { get; set; }

        public string? Actions { get; set; }
    }

    public class OrderSummaryLineChargeModel
    {
        public string? ItemNumber { get; set; }

        public string? Description { get; set; }

        public string? Quantity { get; set; }
    }
}