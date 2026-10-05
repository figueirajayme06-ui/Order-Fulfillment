using System.Globalization;
using Microsoft.Extensions.Logging;
using OF.Data;
using OF.Data.Database;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using OF.Common;
using OF.Common.Infrastructure.CloudSuite.Models.Agreements.DataLake;
using OF.Common.Infrastructure.OF;
using OF.Common.Infrastructure.CloudSuite;
using OF.Common.Infrastructure.IPG.Orders.SOQL;
using OF.Common.Infrastructure.CloudSuite.SQL;
using OF.Common.Utils;
using OF.Common.Infrastructure.IPG.Orders;
using OF.Common.Infrastructure.IPG.Orders.Models.Quotes.Response;
using OF.Common.Infrastructure.IPG.Orders.Models.RAA.Agreementrs;
using OF.Common.Infrastructure.CloudSuite.Models.Agreements.BODs;

namespace OF.Api.UseCases.Agreements
{
    public class UpdateByAgreementNumberRequest
    {
        public required string AgreementNumber { get; set; }

        public string AgreementNumbersOnly => AgreementNumber.Substring(1);
    }

    public class UpdateByAgreementNumberHandler
    {
        private readonly ICloudSuiteService cloudsuiteService;
        private readonly IOrderManagementIntegration salesforceService;
        private readonly ApplicationDbContext dbContext;
        private readonly ICoreFulfilmentEngine fulfilmentEngine;
        private readonly ILogger logger;

        public UpdateByAgreementNumberHandler(
            ICloudSuiteService cloudsuiteService,
            IOrderManagementIntegration salesforceService,
            ApplicationDbContext dbContext,
            ICoreFulfilmentEngine fulfilmentEngine,
            ILogger logger)
        {
            this.cloudsuiteService = cloudsuiteService;
            this.salesforceService = salesforceService;
            this.dbContext = dbContext;
            this.fulfilmentEngine = fulfilmentEngine;
            this.logger = logger;
        }

        public async Task Handle(string body)
        {
            UpdateByAgreementNumberRequest request = JsonConvert.DeserializeObject<UpdateByAgreementNumberRequest>(body)!;

            logger.LogInformation($"UpdateByAgreementNumber fired for message.");

            try
            {
                var header = await GetHeader(request);

                if (header == null)
                {
                    logger.LogWarning($"No agreement found for agreement number (T or A): {request.AgreementNumbersOnly}");
                    return;
                }

                await GetLines(header);


                await dbContext.SaveChangesAsync();

                // If we have for some reason deleted a line cause of the message or added a new one then update the header status

                if (header != null)
                {
                    fulfilmentEngine.RecalculateStatusForHeader(header);
                }

            }
            catch (Exception exception)
            {
                logger.LogError(exception, $"Error UPDATING agreement data for number .{request.AgreementNumbersOnly}");
                throw;
            }
        }

        private async Task<Header?> GetHeader(UpdateByAgreementNumberRequest request)
        {
            string agreementsQuery = string.Format(IONSQL.GetAgreementByAGNB, request.AgreementNumbersOnly);
            IList<IONAgreementData> headers = await cloudsuiteService.RunDataLakeQuery<IONAgreementData>(agreementsQuery);

            if (!headers.Any())
            {
                logger.LogWarning($"No agreement found for agreement number (T or A): {request.AgreementNumbersOnly}");
                return null;
            }

            var agreement = headers.First();

            Header? header = await dbContext.Headers.Include(i => i.Lines).FirstOrDefaultAsync(i => i.AgreementNumbersOnly == request.AgreementNumbersOnly);

            if (header == null && !string.IsNullOrWhiteSpace(agreement.ProposalNumber))
            {
                header = await dbContext.Headers.Include(i => i.Lines).FirstOrDefaultAsync(i => i.QuotePublicId == agreement.ProposalNumber);
            }

            Order? order = null;

            if (!string.IsNullOrWhiteSpace(agreement.ProposalNumber))
            {
                string soql = string.Format(OFSOQL.GetOrderByQuotePublicId, agreement.ProposalNumber);
                SOQLResponse<Order> soqlResponse = await salesforceService.Query<Order>(soql);
                order = soqlResponse?.Records?.FirstOrDefault();
            }

            header = agreement.ToHeaderEntity(header);

            if (order != null)
            {
                float? probability = order.Quote?.Opportunity?.EffectiveProbability != null
                    ? (float)order.Quote.Opportunity.EffectiveProbability.Value
                    : float.TryParse(order.Quote?.Opportunity?.Probability, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                        ? parsed
                        : null;

                header.QuoteNumber = order.QuoteId;
                header.QuotePublicId = order.Quote?.Name;
                header.QuotePublicIdNumbersOnly = order.Quote?.Name?.Substring(2);
                header.OrderNumber = order.Id;
                header.OnHireDate ??= order.OnHireDate;
                header.OffHireDate ??= order.OffHireDate;
                header.OpportunityNumber ??= order.Quote?.OpportunityId;
                header.OpportunityName ??= order.Quote?.Opportunity?.Name;
                header.OpportunityStage = order.Quote?.Opportunity?.OpportunityStageName ?? header.OpportunityStage;
                header.OverviewOfService ??= order.Quote?.OverviewOfServices;
                if (probability.HasValue)
                    header.Probability = probability.Value;
                header.ArmcontactName ??= order.Quote?.Contact?.Name;
                header.ArmcontactEmail ??= order.Quote?.Contact?.Email;
                header.ArmcontactPhone ??= order.Quote?.Contact?.Phone;
                header.CustomerAddress ??= order.ShippingAddress;
            }

            if (header.Id == 0)
            {
                dbContext.Headers.Add(header);
            }

            return header;
        }

        private async Task GetLines(Header header)
        {
            var excludes = string.Join(" AND ", Constants.Lines.Excludes.Select(i => $"ITNO NOT LIKE '{i}%'"));
            var agreementsLinesQuery = string.Format(IONSQL.GetAgreementLinesByAGNB, header.AgreementNumbersOnly, excludes);
            var lines = await cloudsuiteService.RunDataLakeQuery<IONAgreementLineData>(agreementsLinesQuery);

            if (!lines.Any())
            {
                logger.LogWarning($"No lines found for agreement number (T or A): {header.AgreementNumbersOnly}");
            }

            IList<OrderItem>? orderLines = null;

            if (!string.IsNullOrWhiteSpace(header.QuotePublicId))
            {
                string soql = string.Format(OFSOQL.GetOrderLinesByQuotePublicId, header.QuotePublicId);
                SOQLResponse<OrderItem> soqlResponse = await salesforceService.Query<OrderItem>(soql);
                orderLines = soqlResponse?.Records?.ToList();
            }

            foreach (var line in lines)
            {
                // Check if the line already exists in the header

                var existing = header.Lines.FirstOrDefault(i => (i.AgreementLineNumber == "A" + line.AgreementLineNumbersOnly || i.AgreementLineNumber == "T" + line.AgreementLineNumbersOnly));

                // If not check if the line already exists in the database to be attached

                if (existing == null)
                {
                    existing = dbContext.Lines.FirstOrDefault(i => (i.AgreementLineNumber == "A" + line.AgreementLineNumbersOnly || i.AgreementLineNumber == "T" + line.AgreementLineNumbersOnly) && i.HeaderId == null);

                    if (existing != null)
                    {
                        header.Lines.Add(existing);
                    }
                }

                // If not check if the line already exists in the header by generic item number

                if (existing == null)
                {
                    existing = header.Lines.FirstOrDefault(i => i.GenericItemNumber == line.GenericItem && i.ItemNumber == line.GenericItem && string.IsNullOrWhiteSpace(i.AgreementNumbersOnly));
                }

                bool toBeAdded = existing == null;

                existing = line.ToLineEntity(existing);

                if (existing.Id == 0)
                {
                    dbContext.Lines.Add(existing);
                }

                OrderItem? orderItem = orderLines?.FirstOrDefault(i => (i.QuoteLine.GenericItemNumber == (line.GenericItem ?? line.ItemNumber)));

                if (orderItem != null)
                {
                    existing.QuoteLineNumber = orderItem.QuoteLineId;
                    existing.QuoteLineIndex = Convert.ToInt32(orderItem.QuoteLine?.LineId ?? 0);
                    existing.OrderLineNumber = orderItem.Id;
                    existing.OrderLineIndex = Convert.ToInt32(orderItem.OrderLineIndex ?? 0);
                    existing.PackageGroupNumber ??= orderItem.QuoteLine?.Group?.Name;
                    existing.QuotePublicId ??= orderItem.QuoteLine?.Quote?.Name;
                    existing.QuotePublicIdNumbersOnly ??= !string.IsNullOrWhiteSpace(orderItem.QuoteLine?.Quote?.Name) ? orderItem.QuoteLine.Quote?.Name?.Substring(2) : null;
                    existing.GenericItemNumber = !string.IsNullOrWhiteSpace(orderItem.QuoteLine?.GenericItemNumber) ? orderItem.QuoteLine.GenericItemNumber : line.ItemNumber;
                    existing.Attributes ??= orderItem.QuoteLine?.SelectedAttributesAsText;
                    existing.DescriptionWithAttributes ??= orderItem.QuoteLine?.DescriptionWithAttributes;
                    existing.ItemDescription ??= orderItem.QuoteLine?.ItemDescription;

                    existing.NormalizeItemDescription(logger);
                }

                if (toBeAdded)
                {
                    header.Lines.Add(existing);
                }
            }
        }
    }
}
