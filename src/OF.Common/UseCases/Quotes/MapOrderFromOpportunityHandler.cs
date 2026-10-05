using System.Globalization;
using Microsoft.Extensions.Logging;
using OF.Common.Infrastructure.IPG.Orders;
using OF.Common.Infrastructure.IPG.Orders.Models.Quotes;
using OF.Common.Utils;

namespace OF.Common.UseCases.Quotes
{
    public class MapOrderFromOpportunityHandler
    {
        private readonly GetQuoteLinesHandler getQuoteLinesHandler;

        public MapOrderFromOpportunityHandler(IOrderManagementIntegration salesforceService, ILogger logger)
        {
            this.getQuoteLinesHandler = new GetQuoteLinesHandler(salesforceService, logger);
        }

        public async Task<QuoteData?> Handle(Opportunity opportunity)
        {
            var opportunityQuoteLines = await getQuoteLinesHandler.Handle(opportunity);
            return MapOpportunityAndLinesToOrderHeader(opportunity, opportunityQuoteLines);
        }

        private QuoteData MapOpportunityAndLinesToOrderHeader(Opportunity opportunity, IEnumerable<OpportunityQuoteLine> quoteLines)
        {
            float probability = opportunity.EffectiveProbability != null
                ? (float)opportunity.EffectiveProbability.Value
                : float.TryParse(opportunity.Probability, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                    ? parsed
                    : 0;

            QuoteData order = new QuoteData
            {
                OpportunityName = opportunity.OpportunityName,
                StageName = opportunity.StageName,
                QuoteRecordId = opportunity.QuoteNumber,
                QuotePublicId = opportunity.Quote.Name,
                OpportunityRecordId = opportunity.Id,
                SourceWarehouse = new WarehouseData
                {
                    Name = opportunity.Quote!.Warehouse?.M3Id,
                    Division = opportunity.Quote!.Warehouse?.M3DivisionId,
                    Facility = opportunity.Quote!.Warehouse?.M3FacilityId
                },
                Contact = new ContactData
                {
                    Name = opportunity.Quote?.Contact?.Name,
                    Email = opportunity.Quote?.Contact?.Email,
                    Phone = opportunity.Quote?.Contact?.Phone
                },
                OverviewOfServices = opportunity.Quote!.OverviewOfServices,
                Probability = probability,
                QuoteName = opportunity.Quote.Name,
                OnHireDate = opportunity.Quote.OnHireDate!.Value,
                OffHireDate = opportunity.Quote.OffHireDate!.Value,
                DeliveryDate = opportunity.Quote.DeliveryDate,
                Address1 = opportunity.Address?.Street,
                RateType = opportunity.Quote.RateType,
                CollectionDate = opportunity.Quote.CollectionDate,
                Customer = new CustomerData()
                {
                    CustomerNumber = opportunity.Account.M3CustomerNumber,
                    Name = opportunity.Account.Name
                }
            };

            foreach (OpportunityQuoteLine quoteLine in quoteLines)
            {
                AddQuoteLineToOrder(order, quoteLine);
            }

            return order;
        }

        private void AddQuoteLineToOrder(QuoteData order, OpportunityQuoteLine quoteLine)
        {
            if (order.Lines == null)
            {
                order.Lines = new List<QuoteLineData>();
            }

            QuoteLineData orderLine = new QuoteLineData()
            {
                QuoteLineId = quoteLine.Id,
                QuoteLineIndex = Convert.ToInt32(quoteLine.LineId),
                DescriptionWithAttributes = quoteLine.DescriptionWithAttributes,
                ItemDescription = quoteLine.ItemDescription,
                LineTypeId = quoteLine.LineTypeId.ConvertIntFromDecimalString(),
                GenericCode = quoteLine.ProductCode,
                Quantity = quoteLine.Quantity.ConvertIntFromDecimalString(),
                CPQGroupName = quoteLine.QuoteGroup?.Name,
                NumberOfShifts = quoteLine.NumberOfShifts,
                Attributes = quoteLine.SelectedAttributesAsText,
                LocalizedAttributes = quoteLine.LocalizedAttributesAsText,
                OffHireDate = quoteLine.OffHireDate ?? order.OffHireDate,
                OnHireDate = quoteLine.OnHireDate ?? order.OnHireDate,
                CollectionDate = order.CollectionDate
            };

            order.Lines.Add(orderLine);
        }
    }
}
