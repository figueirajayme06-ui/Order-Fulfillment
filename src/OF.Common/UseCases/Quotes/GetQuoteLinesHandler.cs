using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OF.Common.Infrastructure.IPG.Orders;
using OF.Common.Infrastructure.IPG.Orders.Models.Quotes;
using OF.Common.Infrastructure.IPG.Orders.Models.Quotes.Response;
using OF.Common.Infrastructure.IPG.Orders.SOQL;

namespace OF.Common.UseCases.Quotes;
public class GetQuoteLinesHandler
{
    private readonly IOrderManagementIntegration salesforceService;
    private readonly ILogger logger;

    public GetQuoteLinesHandler(IOrderManagementIntegration salesforceService, ILogger logger)
    {
        this.salesforceService = salesforceService;
        this.logger = logger;
    }

    public async Task<IEnumerable<OpportunityQuoteLine>> Handle(Opportunity opportunity)
    {
        if (opportunity == null)
        {
            logger.LogWarning("Opportunity is null.");
            return Enumerable.Empty<OpportunityQuoteLine>();
        }

        logger.LogInformation("Getting order from opportunity {Id}", opportunity.Id);

        var soql = string.Format(OFSOQL.GetQuoteLines, opportunity.QuoteNumber, string.Empty);

        var response = await salesforceService.QueryRaw<OpportunityQuoteLine>(soql);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Error getting Salesforce data: {Body}.", body);
            return Enumerable.Empty<OpportunityQuoteLine>();
        }

        var content = JsonConvert.DeserializeObject<SOQLResponse<OpportunityQuoteLine>>(body);

        if (content?.TotalSize <= 0)
        {
            logger.LogWarning("No lines for opportunity {Id} found.", opportunity.Id);
            return Enumerable.Empty<OpportunityQuoteLine>();
        }

        logger.LogInformation("Retrieved {TotalSize} quotes from Salesforce", content!.TotalSize);

        return content.Records.GroupBy(r => r.LineId).Select(g => g.First());
    }
}
