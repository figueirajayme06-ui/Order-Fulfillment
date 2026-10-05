using OF.Common.Infrastructure.OF;
using OF.Data.Database;
using OF.UI.Identity;
using OF.UI.Models;
using static OF.Common.Enums;

namespace OF.UI.Database
{
    public interface IDataRepository : ICoreDataRepository
    {
        // Change

        ChangeOrder AddChangeOrder(ChangeOrder changeOrder);
        ChangeOrderComment AddChangeOrderComment(int changeOrderId, string comment, string loginName);

        void DeleteChangeOrderHeader(int changeOrderHeaderId);

        void DeleteChangeOrderLine(int changeOrderLineId);

        void DeleteChangeOrder(int changeOrderId);

        // User
        void DeleteUser(User user);

        User AddUser(User user);

        User UpdateUser(User user);

        User GetUser(string loginName);

        Task<User?> GetUserAsync(string loginName);

        IQueryable<User> GetUsers();

        void UpdateLastLoginAtUtc(string loginName, DateTimeOffset accessedAtUtc);

        // View
        void DeleteView(View view);

        View AddView(View view);

        View AddViewWithRecipients(View view, IReadOnlyCollection<string> recipientLoginNames);

        View UpdateView(View view);

        View UpdateViewWithRecipients(View view, IReadOnlyCollection<string> recipientLoginNames);

        View GetView(int id);

        View GetViewWithRecipients(int id);

        IQueryable<View> GetViews();

        IQueryable<View> GetViewsWithRecipients();

        // Asset
        Asset GetAsset(string id);

        IQueryable<Asset> GetAssets();

        IQueryable<VwAssetItem> GetAssetItems(bool includeAllStatuses = false);

        IList<AssetScheduleReservation> GetAssetScheduleReservations(string assetId, DateTime startDate, DateTime endDate);

        // Header
        IQueryable<VwHeader> GetAgreements(bool showFulfilled, bool showHistorical = true);

        Header? GetAgreement(string agreementNumber);

        IQueryable<Header> GetHeaders();

        Header? GetHeaderForLineId(int id);

        Header UpdateHeader(IUserIdentity identity, Header header);

        HeaderDeletionAudit DeleteHeaderAndReservationsFromId(int headerId, string? deletedBy = null);

        void TimeoutSublineActivation(int headerId, IUserIdentity user);

        Task SetHeaderForActivation(int headerId, IUserIdentity identity);

        // Line
        Line GetLine(int id);

        IQueryable<Line> GetAllLines(int headerId);

        IQueryable<Line> GetAllLinesForChangeOrder(int headerId);

        IQueryable<Line> GetLines(int headerId);

        Line UpdateLine(IUserIdentity identity, Line line);

        Line DeleteLine(IUserIdentity identity, Line line);

        Line AddLine(IUserIdentity identity, Line line);

        void SetLineActivationStatus(Line line, ActivationStatus status, IUserIdentity identity);

        // Products
        CpqLine GetProductLine(int id);

        CpqGeneric GetGeneric(string genericCode);

        CpqGeneric GetGeneric(int id);

        CpqItem GetItem(string itemNumber);

        // Rehire
        AlternativeOption[] GetAlternativeOptions(string genericCode);

        // Items
        CpqItem[] GetItems();

        // Services
        CpqService GetService(string productCode);

        CpqService[] GetServices();

        // Attributes
        VwAllAttribute[] GetFulfilmentAttributes();

        // Stock
        IList<SerializedQueryResult> GetSerializedStock(int lineId, string[] divisions, string warehouse, string[] attributes);

        IList<NonSerializedQueryResult> GetNonSerializedStock(int lineId, string[] divisions, string warehouse, string[] attributes);

        // Availability
        Task<List<AvailabilitySummaryResult>> GetAvailabilitySummaryAsync(
            string genericCode,
            string attributes,
            DateTime? startDate,
            DateTime? endDate,
            string division,
            string? itemNumber,
            int? lineId = null);

        // Reservations
        Reservation GetReservation(int reservationId);

        Reservation? DeleteReservation(int reservationId);

        Reservation[] GetOverlappingReservations(string assetId, DateTime startDate, DateTime endDate);

        Reservation CreateReservation(IUserIdentity identity, Reservation reservation);

        Reservation[] GetOtherReservationsForLine(int lineId);

        void DeleteReservationsForHeader(int headerId);

        void DeleteReservationsForLine(int lineId);

        IQueryable<Reservation> GetReservationsForHeader(int headerId);

        bool HasReservationsForLine(int lineId);

        // Alerts
        Alert[] GetAlertsForUser(string loginName);

        Alert AddAlert(IUserIdentity identity, Alert alert);

        Alert UpdateAlert(IUserIdentity identity, Alert alert);

        void DeleteAlertsForLine(int lineId);

        void AcknowledgeAlerts(int[] alertIds);

        // Events
        IList<Event> GetEventsForDivisionsAndRange(DateTime startDate, DateTime endDate, string[] divisions); 

        // Products
        IQueryable<CpqFamily> GetProductFamilies();

        IQueryable<CpqLine> GetProductLines();

        IQueryable<CpqGeneric> GetGenerics();

        IQueryable<CpqGeneric> GetActiveGenerics(string division);

        IQueryable<CpqItem> GetActiveItems(string division);

        IEnumerable<CpqItem> GetItemsForGenericAndAttributes(string division, int genericId, string attributes);

        IQueryable<ProductAttribute> GetProductAttributes(int genericId);

        // Ringfences
        Ringfence CreateRingfence(IUserIdentity identity, Ringfence ringfence);

        Ringfence UpdateRingfence(IUserIdentity identity, Ringfence ringfence);

        void DeleteRingfence(Ringfence ringfence);

        Ringfence GetRingfence(int id);

        IQueryable<Ringfence> GetRingfences();

        IList<Ringfence> GetRingfencesForAsset(string assetId, DateTime startDate, DateTime endDate);

        RingfenceItem CreateRingfenceItem(IUserIdentity identity, RingfenceItem ringfenceItem);

        RingfenceItem UpdateRingfenceItem(IUserIdentity identity, RingfenceItem ringfenceItem);

        void DeleteRingfenceItem(RingfenceItem ringfenceItem);

        IQueryable<RingfenceItem> GetRingfenceItems(int ringfenceId);

        IReadOnlyDictionary<int, int> GetRingfenceItemCounts(IReadOnlyCollection<int> ringfenceIds);

        RingfenceItem AddAssetToRingfence(IUserIdentity identity, Ringfence ringfence, string assetId);

        IQueryable<VwAssetItem> GetAssetsForRingfence(int ringfenceId);

        Task<IReadOnlyList<RingfenceAssestDetails>> GetOverlappingRingfenceDetailsAsync(int ringfenceId, IReadOnlyList<string> assetIds, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken);

        // Agreements
        VwHeader GetAgreement(int id);

        // Notes
        Note GetNote(int id);
        Note CreateNote(IUserIdentity identity, Note note);
        void DeleteNote(IUserIdentity identity, Note note);
        Note UpdateNote(IUserIdentity identity, Note note);
        IQueryable<Note> GetNotes(string noteType, string key);
        IQueryable<Note> GetNotes(string noteType);

        // Warehouse
        string[] GetWarehouseDivisionCodes();

        IList<WarehouseItem> GetWarehouses(string[] divisions);

        //Data Refreshes
        IList<DataRefresh> GetRefreshes();

        //Change Orders
        ChangeOrder UpdateChangeOrder(ChangeOrder change);
        ChangeOrderHeader UpsertChangeOrderHeader(ChangeOrderHeader changeHeader, bool add = false);
        ChangeOrderLine UpsertChangeOrderLine(ChangeOrderLine changeLine);
        IList<ChangeOrderLine> UpsertChangeOrderLines(IList<ChangeOrderLine> changeLines);

        ChangeOrderAddress UpsertChangeOrderAddress(ChangeOrderAddress changeOrderAddress, bool add = false);
        ChangeOrderContact UpsertChangeOrderContact(ChangeOrderContact changeOrderContact, bool add = false);
    }
}
