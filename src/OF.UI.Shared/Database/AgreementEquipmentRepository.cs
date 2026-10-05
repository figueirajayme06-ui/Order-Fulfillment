using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using OF.Data;
using OF.Data.Database;
using OF.UI.Identity;
using static OF.Common.Enums;

namespace OF.UI.Database;

public interface IAgreementEquipmentRepository
{
    AgreementEquipmentCatalogData GetCatalog(string division);

    AgreementEquipmentGenericCatalogResult GetGenericCatalog(
        string division,
        int genericId,
        IReadOnlyCollection<string> attributes);

    AgreementEquipmentCreateResult Create(
        IUserIdentity userIdentity,
        int headerId,
        string expectedDivision,
        AgreementEquipmentCreateCommand command);

    AgreementEquipmentDeleteResult Delete(
        IUserIdentity userIdentity,
        int headerId,
        string expectedDivision,
        int lineId);
}

public sealed class AgreementEquipmentRepository : IAgreementEquipmentRepository
{
    private const int FamPurposeId = 4;
    private const int MaxStoredAttributesLength = 2000;

    private readonly ApplicationDbContext _context;
    private readonly TimeProvider _timeProvider;

    public AgreementEquipmentRepository(ApplicationDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public AgreementEquipmentCatalogData GetCatalog(string division)
    {
        var generics = GetAvailableActiveGenerics(division);
        var productLineIds = generics.Select(generic => generic.LineId).Distinct().ToArray();

        var productLines = _context.CpqLines
            .AsNoTracking()
            .Where(line => productLineIds.Contains(line.Id))
            .OrderBy(line => line.LineDescription)
            .Select(line => new AgreementEquipmentProductLineData(
                line.Id,
                line.LineDescription ?? string.Empty,
                line.Family.FamilyDescription ?? string.Empty))
            .ToArray();

        return new AgreementEquipmentCatalogData(
            productLines,
            generics
                .OrderBy(generic => generic.GenericDescription)
                .ThenBy(generic => generic.GenericCode)
                .Select(generic => new AgreementEquipmentGenericData(
                    generic.Id,
                    generic.LineId,
                    generic.GenericCode,
                    generic.GenericDescription ?? string.Empty))
                .ToArray());
    }

    public AgreementEquipmentGenericCatalogResult GetGenericCatalog(
        string division,
        int genericId,
        IReadOnlyCollection<string> attributes)
    {
        if (!AgreementEquipmentAttributeSelection.TryParse(attributes, out var selections, out var parseError))
        {
            return AgreementEquipmentGenericCatalogResult.InvalidAttributes(parseError);
        }

        var generic = GetAvailableActiveGenerics(division)
            .SingleOrDefault(candidate => candidate.Id == genericId);
        if (generic == null)
        {
            return AgreementEquipmentGenericCatalogResult.GenericNotFound();
        }

        var items = GetAvailableActiveItems(division, genericId);
        var attributesById = GetFilterAttributes(generic.LineId);
        var groups = BuildAttributeGroups(items, attributesById);

        if (!TryValidateSelections(selections, groups, out var canonicalSelections, out var validationError))
        {
            return AgreementEquipmentGenericCatalogResult.InvalidAttributes(validationError);
        }

        var matchingItems = items
            .Where(item => MatchesAllSelections(item, canonicalSelections, attributesById))
            .OrderBy(item => item.ItemNumber)
            .Select(item => new AgreementEquipmentItemData(
                item.ItemNumber,
                item.DescriptionIntl,
                item.GenericId))
            .ToArray();

        return AgreementEquipmentGenericCatalogResult.Success(
            new AgreementEquipmentGenericData(
                generic.Id,
                generic.LineId,
                generic.GenericCode,
                generic.GenericDescription ?? string.Empty),
            groups,
            matchingItems);
    }

    public AgreementEquipmentCreateResult Create(
        IUserIdentity userIdentity,
        int headerId,
        string expectedDivision,
        AgreementEquipmentCreateCommand command)
    {
        using var transaction = BeginMutationTransaction();
        AcquireLineNumberLock(transaction, headerId);

        var header = GetFreshHeader(headerId);
        if (header == null || !DivisionsMatch(header.Division, expectedDivision))
        {
            return AgreementEquipmentCreateResult.HeaderNotFound();
        }

        if (!AgreementEquipmentEligibility.CanChange(header))
        {
            return AgreementEquipmentCreateResult.HeaderNotEligible();
        }

        var parent = _context.Lines.SingleOrDefault(line => line.Id == command.ParentLineId);
        if (parent == null || parent.HeaderId != headerId)
        {
            return AgreementEquipmentCreateResult.ParentNotFound();
        }

        if (!AgreementEquipmentEligibility.IsEligibleParent(header, parent))
        {
            return AgreementEquipmentCreateResult.ParentNotEligible();
        }

        if (command.Quantity is < 1 or > 1000)
        {
            return AgreementEquipmentCreateResult.InvalidQuantity();
        }

        if (!AgreementEquipmentAttributeSelection.TryParse(command.Attributes, out var selections, out var parseError))
        {
            return AgreementEquipmentCreateResult.InvalidAttributes(parseError);
        }

        var generic = GetAvailableActiveGenerics(header.Division)
            .SingleOrDefault(candidate => candidate.Id == command.GenericId);
        if (generic == null)
        {
            return AgreementEquipmentCreateResult.InvalidGeneric();
        }

        var availableItems = GetAvailableActiveItems(header.Division, generic.Id);
        var attributesById = GetFilterAttributes(generic.LineId);
        var groups = BuildAttributeGroups(availableItems, attributesById);
        if (!TryValidateSelections(selections, groups, out var canonicalSelections, out var validationError))
        {
            return AgreementEquipmentCreateResult.InvalidAttributes(validationError);
        }

        CpqItem? exactItem = null;
        if (!string.IsNullOrWhiteSpace(command.ItemNumber))
        {
            var requestedItemNumber = command.ItemNumber.Trim();
            exactItem = availableItems.SingleOrDefault(item =>
                string.Equals(item.ItemNumber, requestedItemNumber, StringComparison.OrdinalIgnoreCase));

            if (exactItem == null || !MatchesAllSelections(exactItem, canonicalSelections, attributesById))
            {
                return AgreementEquipmentCreateResult.InvalidItem();
            }
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var loginName = userIdentity.GetIdentity()?.LoginName;
        var attributesText = AgreementEquipmentAttributeSelection.ToStorageString(canonicalSelections);
        var line = CreateLine(parent, generic, exactItem, command.Quantity, attributesText, now, loginName);

        line.AgreementLineNumber = AllocateNextLineNumber(parent);
        _context.Lines.Add(line);
        RecalculateHeaderStatus(header, line);
        header.LastUpdatedDate = now;
        header.LastUpdatedBy = loginName;

        _context.SaveChanges();
        transaction?.Commit();

        return AgreementEquipmentCreateResult.Success(line, header.FulfilmentStatus);
    }

    public AgreementEquipmentDeleteResult Delete(
        IUserIdentity userIdentity,
        int headerId,
        string expectedDivision,
        int lineId)
    {
        using var transaction = BeginMutationTransaction();
        AcquireLineNumberLock(transaction, headerId);

        var header = GetFreshHeader(headerId);
        if (header == null || !DivisionsMatch(header.Division, expectedDivision))
        {
            return AgreementEquipmentDeleteResult.HeaderNotFound();
        }

        if (!AgreementLineDeletionEligibility.CanDelete(header))
        {
            return AgreementEquipmentDeleteResult.HeaderNotEligible();
        }

        var line = _context.Lines.SingleOrDefault(candidate => candidate.Id == lineId);
        if (line == null || line.HeaderId != headerId)
        {
            return AgreementEquipmentDeleteResult.LineNotFound();
        }

        _context.Entry(line).Reload();
        if (line.HeaderId != headerId)
        {
            return AgreementEquipmentDeleteResult.LineNotFound();
        }
        if (!AgreementEquipmentEligibility.IsDeletableLocalLine(line)
            || _context.Lines.Count(candidate => candidate.HeaderId == headerId && !candidate.IsDeleted) <= 1
            || _context.Lines.Any(candidate => candidate.HeaderId == headerId && !candidate.IsDeleted
                && candidate.Id != line.Id && candidate.AgreementLineNumber != null
                && candidate.AgreementLineNumber.StartsWith(line.AgreementLineNumber + ".")))
        {
            return AgreementEquipmentDeleteResult.LineNotDeletable();
        }

        if (_context.Reservations.Any(reservation => reservation.LineId == lineId))
        {
            return AgreementEquipmentDeleteResult.ReservationsExist();
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var loginName = userIdentity.GetIdentity()?.LoginName;
        line.IsDeleted = true;
        line.LastUpdatedDate = now;
        line.LastUpdatedBy = loginName;
        RecalculateHeaderStatus(header);
        header.LastUpdatedDate = now;
        header.LastUpdatedBy = loginName;

        _context.SaveChanges();
        transaction?.Commit();

        return AgreementEquipmentDeleteResult.Success();
    }

    private CpqGeneric[] GetAvailableActiveGenerics(string division)
    {
        var itemNumbers = GetAvailableItemNumbers(division);

        return _context.CpqGenerics
            .AsNoTracking()
            .Where(generic =>
                generic.Active == true
                && !generic.Deleted
                && _context.CpqItems.Any(item =>
                    item.GenericId == generic.Id
                    && item.Active == true
                    && !item.Deleted
                    && itemNumbers.Contains(item.ItemNumber)))
            .ToArray();
    }

    private CpqItem[] GetAvailableActiveItems(string division, int genericId)
    {
        var itemNumbers = GetAvailableItemNumbers(division);

        return _context.CpqItems
            .AsNoTracking()
            .Include(item => item.CpqItemAttributeValues)
            .ThenInclude(value => value.Attribute)
            .Where(item =>
                item.GenericId == genericId
                && item.Active == true
                && !item.Deleted
                && itemNumbers.Contains(item.ItemNumber))
            .ToArray();
    }

    private IQueryable<string> GetAvailableItemNumbers(string division)
    {
        var normalizedDivision = division.Trim().ToUpper();
        var assetItemNumbers = _context.Assets
            .Where(asset =>
                asset.Division != null
                && asset.ItemNumber != null
                && asset.Division.Trim().ToUpper() == normalizedDivision)
            .Select(asset => asset.ItemNumber!);
        var stockItemNumbers = _context.ProductItems
            .Where(item => item.Division.Trim().ToUpper() == normalizedDivision)
            .Select(item => item.ItemNumber);

        return assetItemNumbers.Concat(stockItemNumbers).Distinct();
    }

    private Dictionary<int, CpqAttribute> GetFilterAttributes(int productLineId) =>
        _context.CpqLineAttributePurposes
            .AsNoTracking()
            .Include(purpose => purpose.Attribute)
            .Where(purpose =>
                purpose.LineId == productLineId
                && purpose.PurposeId == FamPurposeId
                && purpose.Active.ToUpper() == "TRUE")
            .Select(purpose => purpose.Attribute)
            .Distinct()
            .ToDictionary(attribute => attribute.Id);

    private static AgreementEquipmentAttributeGroupData[] BuildAttributeGroups(
        IReadOnlyCollection<CpqItem> items,
        IReadOnlyDictionary<int, CpqAttribute> attributesById)
    {
        return items
            .SelectMany(item => item.CpqItemAttributeValues)
            .Where(value => attributesById.ContainsKey(value.AttributeId))
            .SelectMany(value => ExpandAttributeValues(attributesById[value.AttributeId], value.Value))
            .GroupBy(value => value.Name, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key)
            .Select(group => new AgreementEquipmentAttributeGroupData(
                group.Key,
                group.Select(value => value.Value)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                    .ToArray()))
            .ToArray();
    }

    private static IEnumerable<(string Name, string Value)> ExpandAttributeValues(
        CpqAttribute attribute,
        string storedValue)
    {
        var name = attribute.AttributeName?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            yield break;
        }

        var values = string.Equals(attribute.DataType, "Multi", StringComparison.OrdinalIgnoreCase)
            ? storedValue.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [storedValue.Trim()];

        foreach (var value in values.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            yield return (name, value);
        }
    }

    private static bool TryValidateSelections(
        IReadOnlyCollection<AgreementEquipmentAttributeSelection> selections,
        IReadOnlyCollection<AgreementEquipmentAttributeGroupData> groups,
        out IReadOnlyList<AgreementEquipmentAttributeSelection> canonicalSelections,
        out string error)
    {
        var canonical = new List<AgreementEquipmentAttributeSelection>();
        foreach (var selection in selections)
        {
            var group = groups.SingleOrDefault(candidate =>
                string.Equals(candidate.Name, selection.Name, StringComparison.OrdinalIgnoreCase));
            var canonicalValue = group?.Values.FirstOrDefault(value =>
                string.Equals(value, selection.Value, StringComparison.OrdinalIgnoreCase));
            if (group == null || canonicalValue == null)
            {
                canonicalSelections = [];
                error = $"Attribute '{selection.Name}:{selection.Value}' is not available for this generic and division.";
                return false;
            }

            canonical.Add(new AgreementEquipmentAttributeSelection(group.Name, canonicalValue));
        }

        var stored = AgreementEquipmentAttributeSelection.ToStorageString(canonical);
        if (stored.Length > MaxStoredAttributesLength)
        {
            canonicalSelections = [];
            error = $"The selected attributes cannot exceed {MaxStoredAttributesLength} characters.";
            return false;
        }

        canonicalSelections = canonical;
        error = string.Empty;
        return true;
    }

    private static bool MatchesAllSelections(
        CpqItem item,
        IReadOnlyCollection<AgreementEquipmentAttributeSelection> selections,
        IReadOnlyDictionary<int, CpqAttribute> attributesById)
    {
        return selections.All(selection => item.CpqItemAttributeValues.Any(itemValue =>
        {
            if (!attributesById.TryGetValue(itemValue.AttributeId, out var attribute)
                || !string.Equals(attribute.AttributeName?.Trim(), selection.Name, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.Equals(attribute.DataType, "Number", StringComparison.OrdinalIgnoreCase))
            {
                return decimal.TryParse(itemValue.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var itemNumber)
                    && decimal.TryParse(selection.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var requestedNumber)
                    && itemNumber >= requestedNumber;
            }

            if (string.Equals(attribute.DataType, "Multi", StringComparison.OrdinalIgnoreCase))
            {
                return itemValue.Value
                    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Contains(selection.Value, StringComparer.OrdinalIgnoreCase);
            }

            return string.Equals(itemValue.Value.Trim(), selection.Value, StringComparison.OrdinalIgnoreCase);
        }));
    }

    private static Line CreateLine(
        Line parent,
        CpqGeneric generic,
        CpqItem? exactItem,
        int quantity,
        string attributes,
        DateTime now,
        string? loginName) => new()
    {
        HeaderId = parent.HeaderId,
        OrderLineNumber = parent.OrderLineNumber,
        ItemNumber = exactItem?.ItemNumber ?? generic.GenericCode,
        GenericItemNumber = generic.GenericCode,
        ItemDescription = exactItem?.DescriptionIntl ?? generic.GenericDescription,
        DeliveryDate = parent.DeliveryDate,
        ValidToDate = parent.ValidToDate,
        TerminationDate = parent.TerminationDate,
        CollectionDate = parent.CollectionDate,
        Quantity = quantity,
        AgreementLineType = parent.AgreementLineType,
        Warehouse = parent.Warehouse,
        Status = parent.Status,
        Division = parent.Division,
        PackageGroupNumber = parent.PackageGroupNumber,
        Attributes = string.IsNullOrEmpty(attributes) ? null : attributes,
        ValidFromDate = parent.ValidFromDate,
        ChangeSequence = parent.ChangeSequence,
        IsDeleted = false,
        FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled,
        QuantityFulfilled = 0,
        LastUpdatedBy = loginName,
        LastUpdatedDate = now,
        AgreementLineIndex = parent.AgreementLineIndex,
        Facility = parent.Facility,
        OrderLineIndex = parent.OrderLineIndex,
        QuoteLineIndex = parent.QuoteLineIndex,
        QuoteLineNumber = parent.QuoteLineNumber,
        OrderSource = parent.OrderSource,
        AgreementNumbersOnly = parent.AgreementNumbersOnly,
        QuotePublicId = parent.QuotePublicId,
        QuotePublicIdNumbersOnly = parent.QuotePublicIdNumbersOnly,
        NumberOfShifts = parent.NumberOfShifts,
        RateType = parent.RateType,
        ActivationStatus = (int)ActivationStatus.TODO,
        RequiresFulfilment = true,
    };

    private string AllocateNextLineNumber(Line parent)
    {
        var prefix = parent.AgreementLineNumber + ".";
        var suffixes = _context.Lines
            .Where(line => line.HeaderId == parent.HeaderId && line.AgreementLineNumber!.StartsWith(prefix))
            .Select(line => line.AgreementLineNumber!)
            .AsEnumerable()
            .Select(lineNumber => lineNumber[prefix.Length..])
            .Where(suffix => !suffix.Contains('.'))
            .Select(suffix => int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : 0);

        var next = suffixes.DefaultIfEmpty(0).Max() + 1;
        return prefix + next.ToString(CultureInfo.InvariantCulture);
    }

    private void RecalculateHeaderStatus(Header header, Line? pendingLine = null)
    {
        var serviceItemNumbers = _context.CpqServices
            .AsNoTracking()
            .Select(service => service.ProductCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var lines = _context.Lines
            .Where(line => line.HeaderId == header.Id && !line.IsDeleted && line.RequiresFulfilment && line.AgreementLineNumber != null)
            .AsEnumerable()
            .Where(line =>
                !line.IsDeleted
                && line.RequiresFulfilment
                && line.AgreementLineNumber != null
                && (line.ItemNumber == null || !serviceItemNumbers.Contains(line.ItemNumber)))
            .ToList();
        if (pendingLine != null
            && (pendingLine.ItemNumber == null || !serviceItemNumbers.Contains(pendingLine.ItemNumber)))
        {
            lines.Add(pendingLine);
        }

        var nonQuoteLines = lines
            .Where(line => !line.AgreementLineNumber!.StartsWith("Q", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var fulfilmentLines = nonQuoteLines.Count > 0 ? nonQuoteLines : lines;

        var status = FulfilmentStatus.Unfulfilled;
        if (fulfilmentLines.Count > 0)
        {
            if (fulfilmentLines.Any(line => line.FulfilmentStatus != (int)FulfilmentStatus.Unfulfilled))
            {
                status = FulfilmentStatus.PartiallyFulfilled;
            }

            if (fulfilmentLines.All(line => line.FulfilmentStatus == (int)FulfilmentStatus.FullyFulfiled))
            {
                status = FulfilmentStatus.FullyFulfiled;
            }
        }

        header.FulfilmentStatus = (int)status;
    }

    private IDbContextTransaction? BeginMutationTransaction() =>
        _context.Database.IsRelational()
            ? _context.Database.BeginTransaction(IsolationLevel.Serializable)
            : null;

    private Header? GetFreshHeader(int headerId)
    {
        var tracked = _context.Headers.Local.SingleOrDefault(header => header.Id == headerId);
        if (tracked != null)
        {
            _context.Entry(tracked).Reload();
            return tracked;
        }

        return _context.Headers.SingleOrDefault(header => header.Id == headerId);
    }

    private void AcquireLineNumberLock(IDbContextTransaction? transaction, int headerId)
    {
        if (transaction == null
            || !string.Equals(
                _context.Database.ProviderName,
                "Microsoft.EntityFrameworkCore.SqlServer",
                StringComparison.Ordinal))
        {
            return;
        }

        var resource = $"OF:AgreementMutation:{headerId}";
        _context.Database.ExecuteSqlInterpolated($@"
DECLARE @lockResult int;
EXEC @lockResult = sys.sp_getapplock
    @Resource={resource},
    @LockMode='Exclusive',
    @LockOwner='Transaction',
    @LockTimeout=10000;
IF @lockResult < 0
    THROW 51000, 'Could not acquire the agreement equipment mutation lock.', 1;");
    }

    private static bool DivisionsMatch(string left, string right) =>
        string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
}

public static class AgreementEquipmentEligibility
{
    public static bool CanChange(Header header)
    {
        if (header.IsDeleted || string.IsNullOrWhiteSpace(header.AgreementNumber))
        {
            return false;
        }

        return (header.AgreementNumber.StartsWith("T", StringComparison.OrdinalIgnoreCase)
                && header.ActivationStatus == (int)ActivationStatus.TODO)
            || (header.AgreementNumber.StartsWith("A", StringComparison.OrdinalIgnoreCase)
                && header.ActivationStatus == (int)ActivationStatus.Activated);
    }

    public static bool IsEligibleParent(Header header, Line line) =>
        !line.IsDeleted
        && line.RequiresFulfilment
        && !string.IsNullOrWhiteSpace(line.AgreementLineNumber)
        && !line.IsSubline
        && !string.IsNullOrWhiteSpace(line.Division)
        && string.Equals(line.Division.Trim(), header.Division.Trim(), StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(line.Warehouse)
        && !string.IsNullOrWhiteSpace(line.Facility)
        && !string.IsNullOrWhiteSpace(line.AgreementLineType)
        && !string.IsNullOrWhiteSpace(line.RateType)
        && !string.IsNullOrWhiteSpace(line.NumberOfShifts)
        && line.DeliveryDate.HasValue
        && line.ValidFromDate > DateTime.MinValue
        && line.ValidToDate >= line.ValidFromDate
        && ((header.AgreementNumber!.StartsWith("T", StringComparison.OrdinalIgnoreCase)
                && line.ActivationStatus == (int)ActivationStatus.TODO)
            || (header.AgreementNumber.StartsWith("A", StringComparison.OrdinalIgnoreCase)
                && line.ActivationStatus == (int)ActivationStatus.Activated));

    public static bool IsDeletableLocalLine(Line line) =>
        !line.IsDeleted
        && line.RequiresFulfilment
        && line.IsSubline
        && line.ActivationStatus == (int)ActivationStatus.TODO
        && string.IsNullOrWhiteSpace(line.ActivationInstanceId);
}

public sealed record AgreementEquipmentCreateCommand(
    int ParentLineId,
    int GenericId,
    string? ItemNumber,
    IReadOnlyCollection<string> Attributes,
    int Quantity);

public sealed record AgreementEquipmentCatalogData(
    IReadOnlyList<AgreementEquipmentProductLineData> ProductLines,
    IReadOnlyList<AgreementEquipmentGenericData> Generics);

public sealed record AgreementEquipmentProductLineData(
    int Id,
    string Description,
    string FamilyDescription);

public sealed record AgreementEquipmentGenericData(
    int Id,
    int ProductLineId,
    string Code,
    string Description);

public sealed record AgreementEquipmentAttributeGroupData(
    string Name,
    IReadOnlyList<string> Values);

public sealed record AgreementEquipmentItemData(
    string ItemNumber,
    string Description,
    int GenericId);

public enum AgreementEquipmentFailure
{
    None,
    HeaderNotFound,
    HeaderNotEligible,
    ParentNotFound,
    ParentNotEligible,
    InvalidQuantity,
    InvalidGeneric,
    InvalidAttributes,
    InvalidItem,
    LineNotFound,
    LineNotDeletable,
    ReservationsExist,
}

public sealed record AgreementEquipmentGenericCatalogResult(
    AgreementEquipmentFailure Failure,
    string? Error,
    AgreementEquipmentGenericData? Generic,
    IReadOnlyList<AgreementEquipmentAttributeGroupData> AttributeGroups,
    IReadOnlyList<AgreementEquipmentItemData> Items)
{
    public static AgreementEquipmentGenericCatalogResult Success(
        AgreementEquipmentGenericData generic,
        IReadOnlyList<AgreementEquipmentAttributeGroupData> attributeGroups,
        IReadOnlyList<AgreementEquipmentItemData> items) =>
        new(AgreementEquipmentFailure.None, null, generic, attributeGroups, items);

    public static AgreementEquipmentGenericCatalogResult GenericNotFound() =>
        new(AgreementEquipmentFailure.InvalidGeneric, null, null, [], []);

    public static AgreementEquipmentGenericCatalogResult InvalidAttributes(string error) =>
        new(AgreementEquipmentFailure.InvalidAttributes, error, null, [], []);
}

public sealed record AgreementEquipmentCreateResult(
    AgreementEquipmentFailure Failure,
    string? Error,
    Line? Line,
    int HeaderStatus)
{
    public static AgreementEquipmentCreateResult Success(Line line, int headerStatus) =>
        new(AgreementEquipmentFailure.None, null, line, headerStatus);

    public static AgreementEquipmentCreateResult HeaderNotFound() => FailureResult(AgreementEquipmentFailure.HeaderNotFound);
    public static AgreementEquipmentCreateResult HeaderNotEligible() => FailureResult(AgreementEquipmentFailure.HeaderNotEligible);
    public static AgreementEquipmentCreateResult ParentNotFound() => FailureResult(AgreementEquipmentFailure.ParentNotFound);
    public static AgreementEquipmentCreateResult ParentNotEligible() => FailureResult(AgreementEquipmentFailure.ParentNotEligible);
    public static AgreementEquipmentCreateResult InvalidQuantity() => FailureResult(AgreementEquipmentFailure.InvalidQuantity);
    public static AgreementEquipmentCreateResult InvalidGeneric() => FailureResult(AgreementEquipmentFailure.InvalidGeneric);
    public static AgreementEquipmentCreateResult InvalidAttributes(string error) => FailureResult(AgreementEquipmentFailure.InvalidAttributes, error);
    public static AgreementEquipmentCreateResult InvalidItem() => FailureResult(AgreementEquipmentFailure.InvalidItem);

    private static AgreementEquipmentCreateResult FailureResult(AgreementEquipmentFailure failure, string? error = null) =>
        new(failure, error, null, 0);
}

public sealed record AgreementEquipmentDeleteResult(AgreementEquipmentFailure Failure)
{
    public static AgreementEquipmentDeleteResult Success() => new(AgreementEquipmentFailure.None);
    public static AgreementEquipmentDeleteResult HeaderNotFound() => new(AgreementEquipmentFailure.HeaderNotFound);
    public static AgreementEquipmentDeleteResult HeaderNotEligible() => new(AgreementEquipmentFailure.HeaderNotEligible);
    public static AgreementEquipmentDeleteResult LineNotFound() => new(AgreementEquipmentFailure.LineNotFound);
    public static AgreementEquipmentDeleteResult LineNotDeletable() => new(AgreementEquipmentFailure.LineNotDeletable);
    public static AgreementEquipmentDeleteResult ReservationsExist() => new(AgreementEquipmentFailure.ReservationsExist);
}

public sealed record AgreementEquipmentAttributeSelection(string Name, string Value)
{
    public static bool TryParse(
        IReadOnlyCollection<string>? values,
        out IReadOnlyList<AgreementEquipmentAttributeSelection> selections,
        out string error)
    {
        var parsed = new List<AgreementEquipmentAttributeSelection>();
        var selectedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawValue in values ?? [])
        {
            var value = rawValue?.Trim();
            var separator = value?.IndexOf(':') ?? -1;
            if (separator <= 0
                || separator == value!.Length - 1
                || value.Contains(';'))
            {
                selections = [];
                error = "Attributes must use the 'name:value' format.";
                return false;
            }

            var selection = new AgreementEquipmentAttributeSelection(
                value[..separator].Trim(),
                value[(separator + 1)..].Trim());
            if (selection.Name.Length == 0 || selection.Value.Length == 0)
            {
                selections = [];
                error = "Attributes must use the 'name:value' format.";
                return false;
            }

            if (!selectedNames.Add(selection.Name))
            {
                selections = [];
                error = $"Only one value can be selected for attribute '{selection.Name}'.";
                return false;
            }

            parsed.Add(selection);
        }

        selections = parsed;
        error = string.Empty;
        return true;
    }

    public static string ToStorageString(IEnumerable<AgreementEquipmentAttributeSelection> selections) =>
        string.Join(';', selections.Select(selection => $"{selection.Name}:{selection.Value}"));
}
