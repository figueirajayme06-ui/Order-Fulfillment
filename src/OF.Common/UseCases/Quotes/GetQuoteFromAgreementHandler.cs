using Microsoft.Extensions.Logging;
using OF.Common.Infrastructure.IPG.Orders;
using OF.Common.Infrastructure.IPG.Orders.Models.Quotes;
using OF.Common.Infrastructure.IPG.Orders.SOQL;

namespace OF.Common.UseCases.Quotes
{
    public class GetQuoteFromAgreementHandler
    {
        private IOrderManagementIntegration salesforceService;
        private readonly ILogger logger;

        public GetQuoteFromAgreementHandler(IOrderManagementIntegration salesforceService, ILogger logger)
        {
            this.salesforceService = salesforceService;
            this.logger = logger;
        }

        public async Task<(Opportunity?, IList<OpportunityQuoteLine>?)> Handle(string agreementNumber, string quoteId)
        {
            logger.LogInformation($"Importing Quotes Probability");

            var response = await salesforceService.Query<Opportunity>(string.Format(OFSOQL.GetQuoteById, agreementNumber, quoteId));

            logger.LogInformation($"Retrieved Quote {response!.TotalSize} Quotefrom Salesforce");

            Opportunity? opportunity = response.Records?.FirstOrDefault();
            IList<OpportunityQuoteLine>? lines = null;

            if (opportunity != null)
            {
                var soql = string.Format(OFSOQL.GetQuoteLines, opportunity.Quote.Id, string.Empty);
                var lineResponse = await salesforceService.Query<OpportunityQuoteLine>(soql);
                lines = lineResponse.Records?.ToList();
            }

            return (opportunity, lines);
        }
    }
}
