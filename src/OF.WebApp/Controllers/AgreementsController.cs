using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OF.UI.Database;
using OF.UI.Identity;
using OF.WebApp.Features.Agreements;

namespace OF.WebApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Route("api/orders")]
public class AgreementsController : ControllerBase
{
    private const string OrderTypeQuote = "quote";
    private const string OrderTypeTemporaryAgreement = "temporaryAgreement";
    private const string OrderTypeAgreement = "agreement";

    private readonly IDataRepository _repository;
    private readonly IUserIdentity _userIdentity;

    public AgreementsController(IDataRepository repository, IUserIdentity userIdentity)
    {
        _repository = repository;
        _userIdentity = userIdentity;
    }

    [HttpGet]
    public IActionResult GetAgreements(
        [FromQuery] bool hideFulfilled = false,
        [FromQuery] bool showHistorical = false,
        [FromQuery] string? division = null,
        [FromQuery] string? search = null,
        [FromQuery] string? customerName = null,
        [FromQuery] string? agreementNumber = null,
        [FromQuery] string? warehouse = null,
        [FromQuery] DateTime? onHireDateFrom = null,
        [FromQuery] DateTime? onHireDateTo = null,
        [FromQuery] DateTime? offHireDateFrom = null,
        [FromQuery] DateTime? offHireDateTo = null,
        [FromQuery] DateTime? deliveryDateFrom = null,
        [FromQuery] DateTime? deliveryDateTo = null,
        [FromQuery] DateTime? validFromDate = null,
        [FromQuery] DateTime? validToDate = null,
        [FromQuery] DateTime? terminationDateFrom = null,
        [FromQuery] DateTime? terminationDateTo = null,
        [FromQuery] DateTime? collectionDateFrom = null,
        [FromQuery] DateTime? collectionDateTo = null,
        [FromQuery] string? customerAddress = null,
        [FromQuery] string? lastUpdatedByName = null,
        [FromQuery] string? orderType = null,
        [FromQuery] int? status = null,
        [FromQuery] int? take = null,
        [FromQuery] string? orderTypes = null,
        [FromQuery] string? statuses = null)
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null)
        {
            return Unauthorized();
        }

        var agreements = _repository.GetAgreements(!hideFulfilled, showHistorical);

        // Division filtering
        var requestedDivisions = !string.IsNullOrWhiteSpace(division)
            ? AgreementDivisionAccess.ParseDivisions(division)
            : null;
        var identityDivisions = AgreementDivisionAccess.ParseDivisions(identity.Division);

        if (identity.IsSuperAdmin)
        {
            if (requestedDivisions != null && requestedDivisions.Length > 0)
            {
                // If a division is explicitly selected in the UI, apply it for super admins.
                agreements = agreements.Where(a => requestedDivisions.Contains(a.Division.ToUpper()));
            }
        }
        else
        {
            if (requestedDivisions != null && requestedDivisions.Length > 0)
            {
                var allowedRequestedDivisions = requestedDivisions
                    .Where(identityDivisions.Contains)
                    .ToArray();

                agreements = agreements.Where(a => allowedRequestedDivisions.Contains(a.Division.ToUpper()));
            }
            else
            {
                agreements = agreements.Where(a => identityDivisions.Contains(a.Division.ToUpper()));
            }
        }

        // Search filtering (broad text search)
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            agreements = agreements.Where(a =>
                (a.AgreementNumber != null && a.AgreementNumber.ToLower().Contains(term)) ||
                (a.CustomerName != null && a.CustomerName.ToLower().Contains(term)) ||
                (a.CustomerNumber != null && a.CustomerNumber.ToLower().Contains(term)) ||
                (a.OpportunityName != null && a.OpportunityName.ToLower().Contains(term)) ||
                (a.LastUpdatedByName != null && a.LastUpdatedByName.ToLower().Contains(term)));
        }

        // Advanced field-level filters
        if (!string.IsNullOrWhiteSpace(customerName))
        {
            var term = customerName.Trim().ToLower();
            agreements = agreements.Where(a => a.CustomerName != null && a.CustomerName.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(agreementNumber))
        {
            var term = agreementNumber.Trim().ToLower();
            agreements = agreements.Where(a => a.AgreementNumber != null && a.AgreementNumber.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(warehouse))
        {
            var term = warehouse.Trim().ToLower();
            agreements = agreements.Where(a => a.Warehouse != null && a.Warehouse.ToLower().Contains(term));
        }

        var selectedOrderTypes = SplitCsv(!string.IsNullOrWhiteSpace(orderTypes) ? orderTypes : orderType)
            .Where(value => value is OrderTypeQuote or OrderTypeTemporaryAgreement or OrderTypeAgreement)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (selectedOrderTypes.Count > 0)
        {
            var includeQuotes = selectedOrderTypes.Contains(OrderTypeQuote);
            var includeTemporaryAgreements = selectedOrderTypes.Contains(OrderTypeTemporaryAgreement);
            var includeAgreements = selectedOrderTypes.Contains(OrderTypeAgreement);
            agreements = agreements.Where(a =>
                a.AgreementNumber != null
                && ((includeQuotes && a.AgreementNumber.StartsWith("Q"))
                    || (includeTemporaryAgreements && a.AgreementNumber.StartsWith("T"))
                    || (includeAgreements && a.AgreementNumber.StartsWith("A"))));
        }

        if (onHireDateFrom.HasValue)
        {
            agreements = agreements.Where(a => a.OnHireDate >= onHireDateFrom.Value);
        }

        if (onHireDateTo.HasValue)
        {
            agreements = agreements.Where(a => a.OnHireDate <= onHireDateTo.Value);
        }

        if (offHireDateFrom.HasValue)
        {
            agreements = agreements.Where(a => a.OffHireDate >= offHireDateFrom.Value);
        }

        if (offHireDateTo.HasValue)
        {
            agreements = agreements.Where(a => a.OffHireDate <= offHireDateTo.Value);
        }

        if (deliveryDateFrom.HasValue)
        {
            agreements = agreements.Where(a => a.DeliveryDate >= deliveryDateFrom.Value);
        }

        if (deliveryDateTo.HasValue)
        {
            agreements = agreements.Where(a => a.DeliveryDate <= deliveryDateTo.Value);
        }

        if (validFromDate.HasValue)
        {
            agreements = agreements.Where(a => a.ValidFromDate >= validFromDate.Value);
        }

        if (validToDate.HasValue)
        {
            agreements = agreements.Where(a => a.ValidToDate <= validToDate.Value);
        }

        if (terminationDateFrom.HasValue)
        {
            agreements = agreements.Where(a => a.TerminationDate >= terminationDateFrom.Value);
        }

        if (terminationDateTo.HasValue)
        {
            agreements = agreements.Where(a => a.TerminationDate <= terminationDateTo.Value);
        }

        if (collectionDateFrom.HasValue)
        {
            agreements = agreements.Where(a => a.CollectionDate >= collectionDateFrom.Value);
        }

        if (collectionDateTo.HasValue)
        {
            agreements = agreements.Where(a => a.CollectionDate <= collectionDateTo.Value);
        }

        if (!string.IsNullOrWhiteSpace(customerAddress))
        {
            var term = customerAddress.Trim().ToLower();
            agreements = agreements.Where(a => a.CustomerAddress != null && a.CustomerAddress.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(lastUpdatedByName))
        {
            var term = lastUpdatedByName.Trim().ToLower();
            agreements = agreements.Where(a => a.LastUpdatedByName != null && a.LastUpdatedByName.ToLower().Contains(term));
        }

        var selectedStatuses = !string.IsNullOrWhiteSpace(statuses)
            ? SplitCsv(statuses).Select(value => int.TryParse(value, out var parsed) ? (int?)parsed : null)
                .Where(value => value.HasValue)
                .Select(value => value!.Value)
                .Distinct()
                .ToArray()
            : status.HasValue ? [status.Value] : [];
        if (selectedStatuses.Length > 0)
        {
            agreements = agreements.Where(a => selectedStatuses.Contains(a.FulfilmentStatus));
        }

        IQueryable<OF.Data.Database.VwHeader> query = agreements.OrderByDescending(a => a.OnHireDate);

        if (take.HasValue && take.Value > 0)
        {
            query = query.Take(take.Value);
        }

        var agreementRows = query.ToList();
        var agreementIds = agreementRows.Select(a => a.Id.ToString()).ToArray();
        var noteCounts = agreementIds.Length == 0
            ? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            : (_repository.GetNotes("agreement") ?? Enumerable.Empty<OF.Data.Database.Note>().AsQueryable())
                .Where(note => agreementIds.Contains(note.ParentId))
                .GroupBy(note => note.ParentId)
                .Select(group => new { ParentId = group.Key, Count = group.Count() })
                .ToDictionary(entry => entry.ParentId, entry => entry.Count, StringComparer.OrdinalIgnoreCase);

        var results = agreementRows
            .Select(a => new AgreementListItemResponse
            {
                Id = a.Id,
                AgreementNumber = a.AgreementNumber,
                CustomerName = a.CustomerName,
                CustomerNumber = a.CustomerNumber,
                Division = a.Division,
                Warehouse = a.Warehouse,
                FulfilmentStatus = a.FulfilmentStatus,
                OnHireDate = a.OnHireDate,
                OffHireDate = a.OffHireDate,
                IsDeleted = a.IsDeleted,
                OrderSource = a.OrderSource,
                LineCount = a.LineCount,
                DeliveryDate = a.DeliveryDate,
                ValidFromDate = a.ValidFromDate,
                ValidToDate = a.ValidToDate,
                TerminationDate = a.TerminationDate,
                CollectionDate = a.CollectionDate,
                CustomerAddress = a.CustomerAddress,
                LastUpdatedByName = a.LastUpdatedByName,
                OpportunityName = a.OpportunityName,
                FromDate = a.FromDate,
                ToDate = a.ToDate,
                LastUpdatedDate = a.LastUpdatedDate,
                OpportunityStage = a.OpportunityStage,
                Probability = a.Probability,
                MinFulfilmentStatus = a.MinFulfilmentStatus,
                MaxFulfilmentStatus = a.MaxFulfilmentStatus,
                NoteCount = noteCounts.GetValueOrDefault(a.Id.ToString()),
            })
            .ToList();

        return Ok(results);
    }

    private static string[] SplitCsv(string? value) =>
        value?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];

    [HttpGet("{headerId:int}")]
    public IActionResult GetAgreement(int headerId)
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null)
        {
            return Unauthorized();
        }

        var header = _repository.GetHeader(headerId);
        if (header == null || !AgreementDivisionAccess.CanAccess(identity, header.Division))
        {
            return NotFound();
        }

        var lines = _repository.GetLines(headerId).ToList();

        return Ok(AgreementDetailResponseMapper.Map(header, lines));
    }
}
