using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OF.Common.Infrastructure.IPG.Orders;
using OF.Common.Infrastructure.IPG.Orders.Models.Quotes;
using OF.Common.UseCases.Quotes;
using OF.Data;
using OF.Data.Database;
using OF.UI.Models;

namespace OF.UI.Controllers
{
    [Authorize]
    public class OrderSummarySheetController : Controller
    {
        private readonly ApplicationDbContext dbContext;
        private readonly GetQuoteFromAgreementHandler getQuoteFromAgreement;
        private readonly IConfiguration configuration;

        public OrderSummarySheetController(ApplicationDbContext dbContext, IOrderManagementIntegration orderManagementService, IConfiguration configuration, ILogger<OrderSummarySheetController> logger)
        {
            this.dbContext = dbContext;
            this.getQuoteFromAgreement = new GetQuoteFromAgreementHandler(orderManagementService, logger);
            this.configuration = configuration;
        }

        [Route("report/{agreementNumber}/display")]
        public async Task<IActionResult> Index(string agreementNumber, string? quoteId = null)
        {
            Header header;
            if (agreementNumber?.ToUpper()?.StartsWith("Q") == true)
            {
                header = await dbContext.Headers
                .Include(i => i.Lines)
                .SingleOrDefaultAsync(i => i.AgreementNumber == agreementNumber);
            }
            else
            {
                header = await dbContext.Headers
                .Include(i => i.Lines)
                .SingleOrDefaultAsync(i => i.AgreementNumbersOnly == agreementNumber.Substring(1));
            }

            // if the header isnt in our DB, redirect to the original OrderSummarySheet in the old OF 

            if (header == null)
            {
                var originalOssUrl = $"{configuration["OriginalOFUrl"]}{Request.Path}{Request.QueryString}";
                return Redirect(originalOssUrl);
            }

            var lineIds = header?.Lines?.Select(i => i.Id)?.Distinct()?.ToList();

            IList<Reservation>? reservations = null;
            IList<WarehouseItem>? warehouses = null;

            if (lineIds?.Any() == true)
            {
                reservations = await dbContext.Reservations
                .Where(i => lineIds.Contains(i.LineId))
                .ToListAsync();

                var warehouseCodes = header.Lines.Select(i => i.Warehouse).Distinct().ToList();

                if (reservations.Any())
                {
                    warehouseCodes.AddRange(reservations.Select(i => i.Warehouse).Distinct().ToList());
                }

                warehouses = await dbContext.WarehouseItems.Where(i => warehouseCodes.Contains(i.WarehouseCode)).ToListAsync();
            }

            quoteId = quoteId ?? header?.QuotePublicId;

            (Opportunity? opportunity, IList<OpportunityQuoteLine>? lines) oppo = (null, null);

            if (quoteId != null)
            {
                oppo = await getQuoteFromAgreement.Handle(agreementNumber, quoteId ?? header?.QuotePublicId);
            }

            IList<Note>? notes = null;

            if (header != null)
            {
                notes = await dbContext.Notes.Where(i => i.ParentId == header.Id.ToString() && i.NoteType == "agreement").ToListAsync();
            }

            var model = new OrderSummaryModel()
            {
                Header = header,
                Opportunity = oppo.opportunity,
                Lines = oppo.lines,
                Reservations = reservations,
                Warehouses = warehouses,
                Notes = notes
            };

            return View("Index", model);
        }
    }
}
