using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using OF.Common;
using OF.Common.Infrastructure.OF;
using OF.Data;
using OF.Data.Database;
using OF.UI.Identity;
using OF.UI.Models;
using System.Data;
using System.Linq;
using static OF.Common.Enums;

namespace OF.UI.Database
{
    public class DataRepository : CoreDataRepository, IDataRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<DataRepository>? _logger;

        public DataRepository(
            ApplicationDbContext context,
            IMemoryCache cache,
            TimeProvider timeProvider,
            ILogger<DataRepository>? logger = null) : base(context)
        {
            _context = context;
            _cache = cache;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public ChangeOrder AddChangeOrder(ChangeOrder changeOrder)
        {
            _context.ChangeOrders.Add(changeOrder);
            _context.SaveChanges();
            return changeOrder;
        }

        public User AddUser(User user)
        {
            _context.Users.Add(user);
            _context.SaveChanges();
            return user;
        }

        public View AddView(View view)
        {
            _context.Views.Add(view);
            _context.SaveChanges();
            return view;
        }

        public View AddViewWithRecipients(View view, IReadOnlyCollection<string> recipientLoginNames)
        {
            foreach (var recipientLoginName in NormalizeRecipientLoginNames(recipientLoginNames))
            {
                view.ViewRecipients.Add(new ViewRecipient
                {
                    RecipientLoginName = recipientLoginName,
                });
            }

            _context.Views.Add(view);
            _context.SaveChanges();
            return view;
        }

        public void DeleteUser(User user)
        {
            _context.Users.Remove(user);
            _context.SaveChanges();
        }

        public void DeleteView(View view)
        {
            _context.Views.Remove(view);
            _context.SaveChanges();
        }

        public Asset GetAsset(string id)
        {
            return _context.Assets.FirstOrDefault(a => a.Id == id);
        }

        public IQueryable<Asset> GetAssets()
        {
            return _context.Assets;
        }

        public IQueryable<VwAssetItem> GetAssetItems(bool includeAllStatuses = false)
        {
            var assetItems = _context.VwAssetItems.AsQueryable();

            return includeAllStatuses
                ? assetItems
                : assetItems.Where(a => a.Status != "RemovedStock" && a.Status != "Scrap" && a.Status != "Sold");
        }

        public IList<AssetScheduleReservation> GetAssetScheduleReservations(string assetId, DateTime startDate, DateTime endDate)
        {
            return (
                from reservation in _context.Reservations
                join line in _context.Lines on reservation.LineId equals line.Id
                join header in _context.Headers on line.HeaderId equals header.Id into headers
                from header in headers.DefaultIfEmpty()
                let reservationStart = line.DeliveryDate ?? line.ValidFromDate
                let reservationEnd = line.CollectionDate ?? line.TerminationDate ?? line.ValidToDate
                where reservation.AssetId == assetId
                    && !line.IsDeleted
                    && reservationStart <= endDate
                    && startDate <= reservationEnd
                orderby reservationStart, reservationEnd
                select new AssetScheduleReservation
                {
                    ReservationId = reservation.Id,
                    LineId = line.Id,
                    HeaderId = line.HeaderId,
                    AgreementNumber = header != null ? header.AgreementNumber : null,
                    CustomerName = header != null ? header.CustomerName : null,
                    Warehouse = reservation.Warehouse ?? line.Warehouse,
                    StartDate = reservationStart,
                    EndDate = reservationEnd,
                    IsConfirmed = reservation.IsConfirmed,
                }
            ).ToList();
        }

        public Header? GetHeaderForLineId(int id)
        {
            return _context.Lines.Include(i => i.Header).ThenInclude(i => i.Lines).FirstOrDefault(l => l.Id == id)?.Header;
        }

        public IQueryable<Header> GetHeaders()
        {
            return _context.Headers.Where(h => !h.IsDeleted);
        }

        public IQueryable<VwHeader> GetAgreements(bool showFulfilled, bool showHistorical = true)
        {
            var cutoffDate = _timeProvider.GetUtcNow().UtcDateTime.Date.AddDays(-30);

            return _context.VwHeaders
                .AsNoTracking()
                .Where(a =>
                    !a.IsDeleted
                    && (showFulfilled || a.FulfilmentStatus != (int)FulfilmentStatus.FullyFulfiled)
                    && (showHistorical || a.OffHireDate == null || a.OffHireDate >= cutoffDate));
        }

        public Header UpdateHeader(IUserIdentity identity, Header header)
        {
            return UpdateHeader(header, identity.GetIdentity().LoginName);
        }

        public Line GetLine(int id)
        {
            return _context.Lines.FirstOrDefault(l => l.Id == id);
        }

        public IQueryable<Line> GetAllLines(int headerId)
        {
            return _context.Lines.Where(l => l.HeaderId == headerId && l.RequiresFulfilment);
        }

        public IQueryable<Line> GetAllLinesForChangeOrder(int headerId)
        {
            return _context.Lines.Where(l => l.HeaderId == headerId);
        }

        public IQueryable<Line> GetLines(int headerId)
        {
            return _context.Lines.Where(l => l.HeaderId == headerId && !l.IsDeleted && l.AgreementLineNumber != null && l.RequiresFulfilment);
        }

        public Line AddLine(IUserIdentity identity, Line line)
        {
            line.LastUpdatedDate = DateTime.UtcNow;
            line.LastUpdatedBy = identity.GetIdentity().LoginName;
            _context.Lines.Add(line);
            _context.SaveChanges();
            return line;
        }

        public Line UpdateLine(IUserIdentity identity, Line line)
        {
            line.LastUpdatedDate = DateTime.UtcNow;
            line.LastUpdatedBy = identity.GetIdentity().LoginName;
            _context.Lines.Update(line);
            _context.SaveChanges();
            return line;
        }

        public Line DeleteLine(IUserIdentity identity, Line line)
        {
            // Remove reservations
            var ringfences = _context.Reservations.Where(r => r.LineId == line.Id).ToArray();
            foreach (var ringfence in ringfences)
            {
                _context.Reservations.Remove(ringfence);
            }

            // Update the line - soft delete
            line.LastUpdatedDate = DateTime.UtcNow;
            line.LastUpdatedBy = identity.GetIdentity().LoginName;
            line.IsDeleted = true;
            _context.Lines.Update(line);
            _context.SaveChanges();
            return line;
        }

        public User GetUser(string loginName)
        {
            return _context.Users.FirstOrDefault(u => u.LoginName == loginName);
        }

        public Task<User?> GetUserAsync(string loginName)
        {
            return _context.Users.FirstOrDefaultAsync(u => u.LoginName == loginName);
        }

        public IQueryable<User> GetUsers()
        {
            return _context.Users;
        }

        public void UpdateLastLoginAtUtc(string loginName, DateTimeOffset accessedAtUtc)
        {
            var utcTimestamp = accessedAtUtc.UtcDateTime;
            _context.Users
                .Where(user =>
                    user.LoginName == loginName
                    && (user.LastLoginAtUtc == null || user.LastLoginAtUtc < utcTimestamp))
                .ExecuteUpdate(setters => setters.SetProperty(user => user.LastLoginAtUtc, utcTimestamp));
        }

        public View GetView(int id)
        {
            return _context.Views.FirstOrDefault(v => v.Id == id);
        }

        public View GetViewWithRecipients(int id)
        {
            return _context.Views
                .Include(view => view.ViewRecipients)
                .ThenInclude(recipient => recipient.Recipient)
                .FirstOrDefault(view => view.Id == id);
        }

        public IQueryable<View> GetViews()
        {
            return _context.Views;
        }

        public IQueryable<View> GetViewsWithRecipients()
        {
            return _context.Views
                .Include(view => view.ViewRecipients)
                .ThenInclude(recipient => recipient.Recipient);
        }

        public User UpdateUser(User user)
        {
            _context.Users.Update(user);
            _context.SaveChanges();
            return user;
        }

        public View UpdateView(View view)
        {
            _context.Views.Update(view);
            _context.SaveChanges();
            return view;
        }

        public View UpdateViewWithRecipients(View view, IReadOnlyCollection<string> recipientLoginNames)
        {
            using var transaction = _context.Database.IsRelational()
                ? _context.Database.BeginTransaction(IsolationLevel.Serializable)
                : null;

            if (transaction != null)
            {
                _ = _context.Views
                    .FromSqlInterpolated($"SELECT * FROM [dbo].[Views] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {view.Id}")
                    .AsNoTracking()
                    .Select(candidate => candidate.Id)
                    .Single();
            }

            var requestedRecipients = NormalizeRecipientLoginNames(recipientLoginNames)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (_context.Entry(view).State == EntityState.Detached)
            {
                _context.Views.Update(view);
            }

            var existingRecipients = _context.ViewRecipients
                .Where(recipient => recipient.ViewId == view.Id)
                .ToList();

            foreach (var existingRecipient in existingRecipients)
            {
                if (!requestedRecipients.Remove(existingRecipient.RecipientLoginName))
                {
                    view.ViewRecipients.Remove(existingRecipient);
                    _context.ViewRecipients.Remove(existingRecipient);
                }
            }

            foreach (var recipientLoginName in requestedRecipients)
            {
                view.ViewRecipients.Add(new ViewRecipient
                {
                    ViewId = view.Id,
                    RecipientLoginName = recipientLoginName,
                });
            }

            _context.SaveChanges();
            transaction?.Commit();
            return view;
        }

        private static string[] NormalizeRecipientLoginNames(IEnumerable<string> recipientLoginNames)
        {
            return recipientLoginNames
                .Select(loginName => loginName.Trim())
                .Where(loginName => loginName.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public CpqGeneric GetGeneric(string genericCode)
        {
            return _context.CpqGenerics.FirstOrDefault(p => p.GenericCode == genericCode);
        }

        public CpqGeneric GetGeneric(int id)
        {
            return _context.CpqGenerics.FirstOrDefault(p => p.Id == id);
        }

        public CpqItem GetItem(string itemNumber)
        {
            return _context.CpqItems.FirstOrDefault(p => p.ItemNumber == itemNumber);
        }

        public AlternativeOption[] GetAlternativeOptions(string genericCode)
        {
            return _context.CpqGenerics.Join(_context.CpqGenerics, g => g.LineId, g2 => g2.LineId, (g, g2) => new { g, g2 })
                .Join(_context.CpqItems, g3 => g3.g2.Id, i => i.GenericId, (g3, i) => new { g3, i })
                .Where(g4 => g4.g3.g2.Rehire == "Yes" && g4.g3.g.GenericCode == genericCode && g4.g3.g.Active.HasValue && g4.g3.g.Active.Value && !g4.g3.g.Deleted)
                .Select(g5 => new AlternativeOption
                {
                    ItemNumber = g5.i.ItemNumber,
                    Description = g5.i.ItemNumber + ": " + g5.i.DescriptionIntl
                }).ToArray();
        }

        public CpqItem[] GetItems()
        {
            return _context.CpqItems.Where(x => !x.Deleted && x.Active.HasValue && x.Active.Value).ToArray();
        }

        public CpqService GetService(string productCode)
        {
            return _context.CpqServices.FirstOrDefault(p => p.ProductCode == productCode);
        }

        public CpqService[] GetServices()
        {
            return _context.CpqServices.AsNoTracking().ToArray();
        }

        public VwAllAttribute[] GetFulfilmentAttributes()
        {
            var cachekey = nameof(GetFulfilmentAttributes);
            _cache.TryGetValue(cachekey, out VwAllAttribute[] attributes);

            if (attributes == null || attributes.Length == 0)
            {
                attributes = _context.VwAllAttributes.ToArray();
                _cache.Set(cachekey, attributes, TimeSpan.FromMinutes(30));
            }

            return attributes;
        }

        string? FormatSubstitutionReason(string? reason)
        {
            if (reason == null) return reason;
            if (reason.ToUpper().Contains("MULTIPLE")) return "MULTIPLE";
            if (reason.ToUpper().Contains("UP")) return "UP";
            if (reason.Equals("Related Substitute", StringComparison.OrdinalIgnoreCase)) return "RELATED";
            return null;
        }

        public IList<SerializedQueryResult> GetSerializedStock(int lineId, string[] divisions, string warehouse, string[] attributes)
        {
            var conn = _context.Database.GetDbConnection(); // We didn't open it, so don't close it 
            var command = (SqlCommand)conn.CreateCommand();
            if (command.Connection.State != ConnectionState.Open)
            {
                command.Connection.Open();
            }
            command.CommandText = "FulfilSerialized";
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@lineId", lineId);

            if (attributes.Length > 0)
            {
                command.Parameters.AddWithValue("@attributes", String.Join(';', attributes));
            }
            else
            {
                command.Parameters.AddWithValue("@attributes", String.Empty);
            }

            if (divisions.Length > 0)
            {
                command.Parameters.AddWithValue("@divisions", String.Join(';', divisions));
            }
            else
            {
                command.Parameters.AddWithValue("@divisions", String.Empty);
            }

            var result = new List<SerializedQueryResult>();
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var item = new SerializedQueryResult();
                    item.Generic = new CpqGeneric();
                    item.Asset = new Asset();
                    item.Reservations = new List<StockReservation>();

                    if (!reader.IsDBNull(0)) item.Generic.Id = reader.GetInt32(0);
                    if (!reader.IsDBNull(1)) item.Generic.LineId = reader.GetInt32(1);
                    if (!reader.IsDBNull(2)) item.Generic.GenericCode = reader.GetString(2);
                    if (!reader.IsDBNull(3)) item.Generic.GenericDescription = reader.GetString(3);
                    if (!reader.IsDBNull(4)) item.Generic.RentalTermDays = reader.GetInt32(4);
                    if (!reader.IsDBNull(5)) item.Generic.UomIntl = reader.GetString(5);
                    if (!reader.IsDBNull(6)) item.Generic.RatingIntl = reader.GetString(6);
                    if (!reader.IsDBNull(7)) item.Generic.UomUs = reader.GetString(7);
                    if (!reader.IsDBNull(8)) item.Generic.RatingUs = reader.GetString(8);
                    if (!reader.IsDBNull(9)) item.Generic.Rehire = reader.GetString(9);
                    if (!reader.IsDBNull(10)) item.Generic.CableSizeAwg = reader.GetString(10);
                    if (!reader.IsDBNull(11)) item.Generic.CableSizeMm = reader.GetString(11);
                    if (!reader.IsDBNull(12)) item.Generic.AmperageLimit = reader.GetString(12);
                    if (!reader.IsDBNull(13)) item.Generic.Conductors = reader.GetString(13);
                    if (!reader.IsDBNull(14)) item.Generic.IsFuel = reader.GetString(14);
                    if (!reader.IsDBNull(15)) item.Generic.IsMeter = reader.GetString(15);
                    if (!reader.IsDBNull(16)) item.Generic.M3Type = reader.GetString(16);
                    if (!reader.IsDBNull(17)) item.Generic.CableM3itemNumber = reader.GetString(17);
                    if (!reader.IsDBNull(18)) item.Generic.CpqSequence = reader.GetInt32(18);
                    if (!reader.IsDBNull(19)) item.Generic.VerCol = reader.GetFieldValue<byte[]>(19);
                    if (!reader.IsDBNull(20)) item.Generic.CableType = reader.GetString(20);
                    if (!reader.IsDBNull(21)) item.Generic.ShiftFactor = reader.GetString(21);
                    if (!reader.IsDBNull(22)) item.Generic.ConfigurationType = reader.GetString(22);

                    if (!reader.IsDBNull(23)) item.SubstitutionReason = FormatSubstitutionReason(reader.GetString(23));
                    if (!reader.IsDBNull(24)) item.Asset.Id = reader.GetString(24);
                    if (!reader.IsDBNull(25)) item.Asset.IndividualItemNumber = reader.GetString(25);
                    if (!reader.IsDBNull(26)) item.Asset.StatusCode = reader.GetString(26);
                    if (!reader.IsDBNull(27)) item.Asset.Warehouse = reader.GetString(27);
                    if (!reader.IsDBNull(28)) item.Asset.ShipAddress1 = reader.GetString(28);
                    if (!reader.IsDBNull(29)) item.Asset.AgreementNumber = reader.GetString(29);
                    if (!reader.IsDBNull(30)) item.Asset.DeliveryDate = reader.GetDateTime(30);
                    if (!reader.IsDBNull(31)) item.Asset.AgreementLineValidToDate = reader.GetDateTime(31);
                    if (!reader.IsDBNull(32)) item.Asset.TerminationDate = reader.GetDateTime(32);
                    if (!reader.IsDBNull(33)) item.Asset.CustomerName = reader.GetString(33);
                    if (!reader.IsDBNull(34)) item.Asset.TelemetryStatus = reader.GetString(34);
                    if (!reader.IsDBNull(35)) item.Asset.ServiceCenter = reader.GetString(35);
                    if (!reader.IsDBNull(36)) item.Asset.Description = reader.GetString(36);
                    if (!reader.IsDBNull(37)) item.Asset.Status = reader.GetString(37);
                    if (!reader.IsDBNull(38)) item.Asset.ManufacturerName = reader.GetString(38);
                    if (!reader.IsDBNull(39)) item.Asset.OwnerServiceCenter = reader.GetString(39);
                    if (!reader.IsDBNull(40)) item.Asset.CustomerNumber = reader.GetString(40);
                    if (!reader.IsDBNull(41)) item.Asset.ItemNumber = reader.GetString(41);
                    if (!reader.IsDBNull(42)) item.Asset.ShipAddress3 = reader.GetString(42);
                    if (!reader.IsDBNull(43)) item.Asset.Facility = reader.GetString(43);
                    if (!reader.IsDBNull(44)) item.Asset.WarehouseLocation = reader.GetString(44);
                    if (!reader.IsDBNull(45)) item.Asset.Division = reader.GetString(45);
                    if (!reader.IsDBNull(66)) item.Asset.IonlastModified = reader.GetDateTime(66);

                    var reservation = new StockReservation();
                    if (!reader.IsDBNull(46)) reservation.CustomerName = reader.GetString(46);
                    if (!reader.IsDBNull(47)) reservation.CustomerNumber = reader.GetString(47);
                    if (!reader.IsDBNull(48)) reservation.AgreementNumber = reader.GetString(48);
                    if (!reader.IsDBNull(49)) reservation.DeliveryDate = reader.GetDateTime(49);
                    if (!reader.IsDBNull(50)) reservation.ValidFromDate = reader.GetDateTime(50);
                    if (!reader.IsDBNull(51)) reservation.ValidToDate = reader.GetDateTime(51);
                    if (!reader.IsDBNull(52)) reservation.TerminationDate = reader.GetDateTime(52);
                    if (!reader.IsDBNull(53)) reservation.AgreementLineNumber = reader.GetString(53);
                    if (!reader.IsDBNull(54)) reservation.Notes = reader.GetString(54);
                    if (!reader.IsDBNull(55)) reservation.LineId = reader.GetInt32(55);
                    if (!reader.IsDBNull(56)) reservation.ReservationId = reader.GetInt32(56);

                    if (!reader.IsDBNull(57)) item.WarehouseName = reader.GetString(57);

                    if (item.SubstitutionReason == "MULTIPLE")
                    {
                        item.SubstitutionMultiple = reader.GetInt32(58);
                    }

                    var ringFence = new StockReservation();

                    if (!reader.IsDBNull(59)) ringFence.ReservationId = reader.GetInt32(59);
                    ringFence.CustomerName = "RINGFENCE";
                    ringFence.CustomerNumber = "RINGFENCE";
                    ringFence.AgreementNumber = "RINGFENCE";
                    if (!reader.IsDBNull(60)) ringFence.DeliveryDate = reader.GetDateTime(60);
                    ringFence.ValidFromDate = ringFence.DeliveryDate;
                    if (!reader.IsDBNull(61)) ringFence.TerminationDate = reader.GetDateTime(61);
                    if (!reader.IsDBNull(62)) ringFence.Notes = reader.GetString(62);
                    if (!reader.IsDBNull(63)) item.Asset.EstimatedReadyDate = reader.GetDateTime(63);
                    if (!reader.IsDBNull(64)) item.NoteCount = reader.GetInt32(64);

                    if (!reader.IsDBNull(65)) reservation.IsConfirmed = reader.GetBoolean(65);

                    if (reservation.ReservationId > 0 || ringFence.ReservationId > 0)
                    {
                        if (reservation.ReservationId > 0)
                        {
                            // Merge or add
                            var existing = result.FirstOrDefault(r => r.Asset.Id == item.Asset.Id);
                            if (existing != null)
                            {
                                if (existing.Reservations.FirstOrDefault(r => r.ReservationId == reservation.ReservationId && r.CustomerName != "RINGFENCE") == null)
                                {
                                    existing.Reservations.Add(reservation);
                                }
                            }
                            else
                            {
                                item.Reservations.Add(reservation);
                                result.Add(item);
                            }
                        }

                        if (ringFence.ReservationId > 0)
                        {
                            // Merge or add
                            var existing = result.FirstOrDefault(r => r.Asset.Id == item.Asset.Id);
                            if (existing != null)
                            {
                                if (existing.Reservations.FirstOrDefault(r => r.ReservationId == ringFence.ReservationId && r.CustomerName == "RINGFENCE") == null)
                                {
                                    existing.Reservations.Add(ringFence);
                                }
                            }
                            else
                            {
                                item.Reservations.Add(ringFence);
                                result.Add(item);
                            }
                        }
                    }
                    else
                    {
                        result.Add(item);
                    }
                }
            }

            return result;
        }

        public async Task SetHeaderForActivation(int headerId, IUserIdentity identity)
        {
            var header = await _context.Headers.Include(i => i.Lines).FirstOrDefaultAsync(i => i.Id == headerId);

            if (header == null)
            {
                throw new Exception($"Header with id {headerId} not found");
            }

            var loginName = identity.GetIdentity().LoginName;

            if (header.ActivationStatus != (int)ActivationStatus.Activated)
            {
                header.ActivationErrors = null;
                header.ActivationInstanceId = null;
                header.ActivationStatus = (int)ActivationStatus.Requested;
                header.LastUpdatedDate = DateTime.UtcNow;
                header.LastUpdatedBy = loginName;
            }

            if (header.Lines != null)
            {
                foreach (var line in header.Lines.Where(i => i.ActivationStatus != (int)ActivationStatus.Activated))
                {
                    line.ActivationErrors = null;
                    line.ActivationInstanceId = null;
                    line.ActivationStatus = (int)ActivationStatus.Requested;
                    line.LastUpdatedDate = DateTime.UtcNow;
                    line.LastUpdatedBy = loginName;
                }
            }

            await _context.SaveChangesAsync();
        }

        public IList<NonSerializedQueryResult> GetNonSerializedStock(int lineId, string[] divisions, string warehouse, string[] attributes)
        {
            var conn = _context.Database.GetDbConnection(); // We didn't open it, so don't close it
            var command = (SqlCommand)conn.CreateCommand();
            if (command.Connection.State != ConnectionState.Open)
            {
                command.Connection.Open();
            }
            command.CommandText = "FulfilNonSerialized";
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@lineId", lineId);

            if (attributes.Length > 0)
            {
                command.Parameters.AddWithValue("@attributes", String.Join(';', attributes));
            }
            else
            {
                command.Parameters.AddWithValue("@attributes", String.Empty);
            }

            if (divisions.Length > 0)
            {
                command.Parameters.AddWithValue("@divisions", String.Join(';', divisions));
            }
            else
            {
                command.Parameters.AddWithValue("@divisions", String.Empty);
            }

            var result = new List<NonSerializedQueryResult>();
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var item = new NonSerializedQueryResult();
                    item.Generic = new CpqGeneric();
                    item.Reservations = new List<StockReservation>();

                    if (!reader.IsDBNull(0)) item.Generic.Id = reader.GetInt32(0);
                    if (!reader.IsDBNull(1)) item.Generic.LineId = reader.GetInt32(1);
                    if (!reader.IsDBNull(2)) item.Generic.GenericCode = reader.GetString(2);
                    if (!reader.IsDBNull(3)) item.Generic.GenericDescription = reader.GetString(3);
                    if (!reader.IsDBNull(4)) item.Generic.RentalTermDays = reader.GetInt32(4);
                    if (!reader.IsDBNull(5)) item.Generic.UomIntl = reader.GetString(5);
                    if (!reader.IsDBNull(6)) item.Generic.RatingIntl = reader.GetString(6);
                    if (!reader.IsDBNull(7)) item.Generic.UomUs = reader.GetString(7);
                    if (!reader.IsDBNull(8)) item.Generic.RatingUs = reader.GetString(8);
                    if (!reader.IsDBNull(9)) item.Generic.Rehire = reader.GetString(9);
                    if (!reader.IsDBNull(10)) item.Generic.CableSizeAwg = reader.GetString(10);
                    if (!reader.IsDBNull(11)) item.Generic.CableSizeMm = reader.GetString(11);
                    if (!reader.IsDBNull(12)) item.Generic.AmperageLimit = reader.GetString(12);
                    if (!reader.IsDBNull(13)) item.Generic.Conductors = reader.GetString(13);
                    if (!reader.IsDBNull(14)) item.Generic.IsFuel = reader.GetString(14);
                    if (!reader.IsDBNull(15)) item.Generic.IsMeter = reader.GetString(15);
                    if (!reader.IsDBNull(16)) item.Generic.M3Type = reader.GetString(16);
                    if (!reader.IsDBNull(17)) item.Generic.CableM3itemNumber = reader.GetString(17);
                    if (!reader.IsDBNull(18)) item.Generic.CpqSequence = reader.GetInt32(18);
                    if (!reader.IsDBNull(19)) item.Generic.VerCol = reader.GetFieldValue<byte[]>(19);
                    if (!reader.IsDBNull(20)) item.Generic.CableType = reader.GetString(20);
                    if (!reader.IsDBNull(21)) item.Generic.ShiftFactor = reader.GetString(21);
                    if (!reader.IsDBNull(22)) item.Generic.ConfigurationType = reader.GetString(22);

                    if (!reader.IsDBNull(23)) item.SubstitutionReason = FormatSubstitutionReason(reader.GetString(23));

                    item.Asset = new ProductItem()
                    {
                        Warehouse = reader.GetString(24),
                        ItemNumber = reader.GetString(25),
                        StockQuantity = reader.GetDecimal(26),
                        AllocatedQuantity = reader.GetDecimal(27),
                        DefaultLocation = reader.IsDBNull(28) ? null : reader.GetString(28),
                        Facility = reader.GetString(29),
                        Division = reader.GetString(30),
                        Status = reader.GetString(31),
                        Description = reader.GetString(32)
                    };

                    var reservation = new StockReservation();
                    if (!reader.IsDBNull(33)) reservation.CustomerName = reader.GetString(33);
                    if (!reader.IsDBNull(34)) reservation.CustomerNumber = reader.GetString(34);
                    if (!reader.IsDBNull(35)) reservation.AgreementNumber = reader.GetString(35);
                    if (!reader.IsDBNull(36)) reservation.DeliveryDate = reader.GetDateTime(36);
                    if (!reader.IsDBNull(37)) reservation.ValidFromDate = reader.GetDateTime(37);
                    if (!reader.IsDBNull(38)) reservation.ValidToDate = reader.GetDateTime(38);
                    if (!reader.IsDBNull(39)) reservation.TerminationDate = reader.GetDateTime(39);
                    if (!reader.IsDBNull(40)) reservation.AgreementLineNumber = reader.GetString(40);
                    if (!reader.IsDBNull(41)) reservation.Quantity = reader.GetInt32(41);
                    if (!reader.IsDBNull(42)) reservation.Notes = reader.GetString(42);
                    if (!reader.IsDBNull(43)) reservation.LineId = reader.GetInt32(43);
                    if (!reader.IsDBNull(44)) reservation.ReservationId = reader.GetInt32(44);

                    if (!reader.IsDBNull(45)) item.WarehouseName = reader.GetString(45);

                    if (item.SubstitutionReason == "MULTIPLE")
                    {
                        item.SubstitutionMultiple = reader.GetInt32(46);
                    }

                    if (!reader.IsDBNull(47)) reservation.IsConfirmed = reader.GetBoolean(47);

                    if (!String.IsNullOrEmpty(reservation.AgreementNumber))
                    {
                        // Merge or add
                        var existing = result.FirstOrDefault(r => r.Asset.ItemNumber == item.Asset.ItemNumber && r.Asset.Warehouse == item.Asset.Warehouse);
                        if (existing != null)
                        {
                            if (existing.Reservations.FirstOrDefault(r => r.ReservationId == reservation.ReservationId) == null)
                            {
                                existing.Reservations.Add(reservation);
                            }
                        }
                        else
                        {
                            item.Reservations.Add(reservation);
                            result.Add(item);
                        }
                    }
                    else
                    {
                        result.Add(item);
                    }
                }
            }
            return result;
        }

        public async Task<List<AvailabilitySummaryResult>> GetAvailabilitySummaryAsync(
            string genericCode,
            string attributes,
            DateTime? startDate,
            DateTime? endDate,
            string division,
            string? itemNumber,
            int? lineId = null)
        {
            var conn = _context.Database.GetDbConnection();
            var command = (SqlCommand)conn.CreateCommand();
            if (command.Connection!.State != ConnectionState.Open)
            {
                await command.Connection.OpenAsync();
            }
            command.CommandText = "dbo.GetFulfilmentAvailabilitySummary";
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add("@genericCode", SqlDbType.NVarChar, 255).Value = genericCode;
            command.Parameters.Add("@attributes", SqlDbType.NVarChar, -1).Value = attributes ?? string.Empty;
            command.Parameters.Add("@startDate", SqlDbType.DateTime2).Value = (object?)startDate ?? DBNull.Value;
            command.Parameters.Add("@endDate", SqlDbType.DateTime2).Value = (object?)endDate ?? DBNull.Value;
            command.Parameters.Add("@divisions", SqlDbType.NVarChar, -1).Value = division ?? string.Empty;
            command.Parameters.Add("@itemNumber", SqlDbType.NVarChar, 255).Value = (object?)itemNumber ?? DBNull.Value;
            command.Parameters.Add("@lineId", SqlDbType.Int).Value = (object?)lineId ?? DBNull.Value;

            var results = new List<AvailabilitySummaryResult>();
            using (var reader = await command.ExecuteReaderAsync())
            {
                var warehouseCode = reader.GetOrdinal("WarehouseCode");
                var warehouse = reader.GetOrdinal("Warehouse");
                var resultGenericCode = reader.GetOrdinal("GenericCode");
                var genericDescription = reader.GetOrdinal("GenericDescription");
                var resultItemNumber = reader.GetOrdinal("ItemNumber");
                var descriptionIntl = reader.GetOrdinal("DescriptionIntl");
                var facility = reader.GetOrdinal("Facility");
                var divisionCode = reader.GetOrdinal("DivisionCode");
                var divisionName = reader.GetOrdinal("DivisionName");
                var available = reader.GetOrdinal("Available");
                var count = reader.GetOrdinal("Count");
                var genericOnly = reader.GetOrdinal("GenericOnly");
                var reservationMode = reader.GetOrdinal("ReservationMode");
                var substitutionReason = reader.GetOrdinal("SubstitutionReason");

                while (await reader.ReadAsync())
                {
                    results.Add(new AvailabilitySummaryResult
                    {
                        WarehouseCode = reader.IsDBNull(warehouseCode) ? string.Empty : reader.GetString(warehouseCode),
                        Warehouse = reader.IsDBNull(warehouse) ? string.Empty : reader.GetString(warehouse),
                        GenericCode = reader.IsDBNull(resultGenericCode) ? string.Empty : reader.GetString(resultGenericCode),
                        GenericDescription = reader.IsDBNull(genericDescription) ? string.Empty : reader.GetString(genericDescription),
                        ItemNumber = reader.IsDBNull(resultItemNumber) ? string.Empty : reader.GetString(resultItemNumber),
                        DescriptionIntl = reader.IsDBNull(descriptionIntl) ? string.Empty : reader.GetString(descriptionIntl),
                        Facility = reader.IsDBNull(facility) ? string.Empty : reader.GetString(facility),
                        DivisionCode = reader.IsDBNull(divisionCode) ? string.Empty : reader.GetString(divisionCode),
                        DivisionName = reader.IsDBNull(divisionName) ? string.Empty : reader.GetString(divisionName),
                        Available = reader.IsDBNull(available) ? 0 : reader.GetInt32(available),
                        Count = reader.IsDBNull(count) ? 0 : reader.GetInt32(count),
                        GenericOnly = !reader.IsDBNull(genericOnly) && reader.GetBoolean(genericOnly),
                        ReservationMode = reader.IsDBNull(reservationMode) ? "asset" : reader.GetString(reservationMode),
                        SubstitutionReason = reader.IsDBNull(substitutionReason) ? null : reader.GetString(substitutionReason),
                    });
                }
            }
            return results;
        }

        public Reservation GetReservation(int reservationId)
        {
            return _context.Reservations.FirstOrDefault(r => r.Id == reservationId);
        }

        public Reservation? DeleteReservation(int reservationId)
        {
            var reservation = _context.Reservations.FirstOrDefault(r => r.Id == reservationId);
            if (reservation != null)
            {
                _context.Reservations.Remove(reservation);
                _context.SaveChanges();
            }

            return reservation;
        }

        public Reservation[] GetOverlappingReservations(string assetId, DateTime startDate, DateTime endDate)
        {
            var reservations =
                from reservation in _context.Reservations
                where reservation.AssetId == assetId
                join line in _context.Lines on reservation.LineId equals line.Id
                where (line.DeliveryDate ?? line.ValidFromDate) <= endDate &&
                    startDate <= (line.TerminationDate ?? line.ValidToDate) // Are the dates overlapping?
                select reservation;
            return reservations.ToArray();
        }

        public Reservation[] GetOtherReservationsForLine(int lineId)
        {
            return _context.Reservations.Where(r => r.LineId == lineId &&
                (r.IsRehire || r.IsDepotFulfilled || (r.IsConfirmed && r.AssetId != null)))
                .Where(i => !i.IsConfirmed || (i.IsConfirmed && !i.IsRehire && !i.IsDepotFulfilled))
                .ToArray();
        }

        public Reservation CreateReservation(IUserIdentity identity, Reservation reservation)
        {
            reservation.LastUpdatedDate = DateTime.UtcNow;
            reservation.LastUpdatedBy = identity.GetIdentity().LoginName;
            _context.Reservations.Add(reservation);
            _context.SaveChanges();
            return reservation;
        }

        public IQueryable<Reservation> GetReservationsForHeader(int headerId)
        {
            return _context.Reservations.FromSql($"SELECT r.* FROM Reservations r INNER JOIN Lines l ON r.LineId=l.Id WHERE l.HeaderId={headerId}");
        }

        public bool HasReservationsForLine(int lineId)
        {
            return _context.Reservations.Any(r => r.LineId == lineId);
        }

        public void TimeoutSublineActivation(int headerId, IUserIdentity user)
        {
            var header = _context.Headers.Include(i => i.Lines).First(l => l.Id == headerId);
            var loginName = user.GetIdentity().LoginName;

            header.LastUpdatedDate = DateTime.UtcNow;
            header.LastUpdatedBy = loginName;

            foreach (var line in header?.Lines ?? new List<Line>())
            {
                if (line.IsDeleted)
                {
                    continue;
                }

                if (line.IsSubline)
                {
                    continue;
                }

                if (line.ActivationStatus == (int)ActivationStatus.Requested)
                {
                    line.ActivationStatus = (int)ActivationStatus.Failed;
                    line.ActivationErrors = $"Line activation request cancellation by {loginName} ({user.GetIdentity().FullName}) @ {DateTime.UtcNow.ToLongDateString()} UTC.";
                    line.LastUpdatedDate = DateTime.UtcNow;
                    line.LastUpdatedBy = loginName;
                }
            }

            _context.SaveChanges();
        }

        public HeaderDeletionAudit DeleteHeaderAndReservationsFromId(int headerId, string? deletedBy = null)
        {
            var header = _context.Headers
                .Include(i => i.Lines)
                .First(l => l.Id == headerId);

            var lineIds = (header.Lines ?? new List<Line>())
                .Select(l => l.Id)
                .ToArray();

            var reservationsDeleted = lineIds.Length == 0
                ? 0
                : _context.Reservations.Count(r => lineIds.Contains(r.LineId));

            DeleteReservationsForHeader(headerId);

            var deletedAtUtc = DateTime.UtcNow;
            var wasHeaderAlreadyDeleted = header.IsDeleted;
            var activeLinesMarkedDeleted = 0;
            var alreadyDeletedLines = 0;

            header.IsDeleted = true;
            header.LastUpdatedDate = deletedAtUtc;

            if (!string.IsNullOrWhiteSpace(deletedBy))
            {
                header.LastUpdatedBy = deletedBy;
            }

            foreach (var line in header.Lines ?? new List<Line>())
            {
                if (line.IsDeleted)
                {
                    alreadyDeletedLines++;
                }
                else
                {
                    activeLinesMarkedDeleted++;
                }

                line.IsDeleted = true;
                line.LastUpdatedDate = deletedAtUtc;

                if (!string.IsNullOrWhiteSpace(deletedBy))
                {
                    line.LastUpdatedBy = deletedBy;
                }
            }

            _context.SaveChanges();

            var audit = new HeaderDeletionAudit
            {
                HeaderId = header.Id,
                QuotePublicId = header.QuotePublicId,
                WasHeaderAlreadyDeleted = wasHeaderAlreadyDeleted,
                ActiveLinesMarkedDeleted = activeLinesMarkedDeleted,
                AlreadyDeletedLines = alreadyDeletedLines,
                ReservationsDeleted = reservationsDeleted,
                DeletedBy = deletedBy,
                DeletedAtUtc = deletedAtUtc,
            };

            _logger?.LogWarning(
                "Header deletion audit: HeaderId={HeaderId}, Quote={QuotePublicId}, DeletedBy={DeletedBy}, HeaderWasDeleted={WasHeaderAlreadyDeleted}, ActiveLinesMarkedDeleted={ActiveLinesMarkedDeleted}, AlreadyDeletedLines={AlreadyDeletedLines}, ReservationsDeleted={ReservationsDeleted}, DeletedAtUtc={DeletedAtUtc}",
                audit.HeaderId,
                audit.QuotePublicId,
                audit.DeletedBy,
                audit.WasHeaderAlreadyDeleted,
                audit.ActiveLinesMarkedDeleted,
                audit.AlreadyDeletedLines,
                audit.ReservationsDeleted,
                audit.DeletedAtUtc);

            return audit;
        }

        public void DeleteReservationsForHeader(int headerId)
        {
            var lines = _context.Lines.Where(l => l.HeaderId == headerId).ToArray();
            foreach (var line in lines)
            {
                var reservations = _context.Reservations.Where(r => r.LineId == line.Id).ToArray();
                foreach (var reservation in reservations)
                {
                    _context.Reservations.Remove(reservation);
                }
            }
            _context.SaveChanges();
        }

        public void DeleteReservationsForLine(int lineId)
        {
            var reservations = _context.Reservations.Where(r => r.LineId == lineId).ToArray();
            foreach (var reservation in reservations)
            {
                _context.Reservations.Remove(reservation);
            }
            _context.SaveChanges();
        }

        public Alert[] GetAlertsForUser(string loginName)
        {
            return _context.Alerts.Where(a => a.AffectedUser == loginName).ToArray();
        }

        public Alert AddAlert(IUserIdentity identity, Alert alert)
        {
            alert.LastUpdatedDate = DateTime.UtcNow;
            alert.LastUpdatedBy = identity.GetIdentity().LoginName;
            _context.Alerts.Add(alert);
            _context.SaveChanges();
            return alert;
        }

        public Alert UpdateAlert(IUserIdentity identity, Alert alert)
        {
            alert.LastUpdatedDate = DateTime.UtcNow;
            alert.LastUpdatedBy = identity.GetIdentity().LoginName;
            _context.SaveChanges();
            return alert;
        }

        public void DeleteAlertsForLine(int lineId)
        {
            var alerts = _context.Alerts.Where(a => a.LineId == lineId).ToArray();
            foreach (var alert in alerts)
            {
                alert.Acknowledged = true;
            }
            _context.SaveChanges();
        }

        public void AcknowledgeAlerts(int[] alertIds)
        {
            if (alertIds.Length == 0)
            {
                return;
            }
            var alerts = _context.Alerts.Where(a => alertIds.Contains(a.Id)).ToArray();
            foreach (var alert in alerts)
            {
                alert.Acknowledged = true;
            }
            _context.SaveChanges();
        }

        public IList<Event> GetEventsForDivisionsAndRange(DateTime startDate, DateTime endDate, string[] divisions)
        {
            var conn = _context.Database.GetDbConnection(); // We didn't open it, so don't close it
            var command = (SqlCommand)conn.CreateCommand();
            if (command.Connection.State != ConnectionState.Open)
            {
                command.Connection.Open();
            }
            command.CommandText = "GenerateEvents";
            command.CommandType = System.Data.CommandType.StoredProcedure;

            command.Parameters.AddWithValue("@startDate", startDate.Date);
            command.Parameters.AddWithValue("@endDate", endDate.Date);

            if (divisions.Length > 0)
            {
                command.Parameters.AddWithValue("@divisions", String.Join(';', divisions));
            }
            else
            {
                command.Parameters.AddWithValue("@divisions", String.Empty);
            }

            var result = new List<Event>();
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var item = new Event();
                    if (!reader.IsDBNull(0)) item.AssetId = reader.GetString(0);
                    if (!reader.IsDBNull(1)) item.EventType = reader.GetString(1);
                    if (!reader.IsDBNull(2)) item.Title = reader.GetString(2);
                    if (!reader.IsDBNull(3)) item.CssClass = reader.GetString(3);
                    if (!reader.IsDBNull(4)) item.StartDate = reader.GetDateTime(4).Date;
                    if (!reader.IsDBNull(5)) item.EndDate = reader.GetDateTime(5).Date;

                    result.Add(item);
                }
            }

            return result;
        }

        public IQueryable<CpqFamily> GetProductFamilies()
        {
            return _context.CpqFamilies.OrderBy(f => f.FamilyDescription);
        }

        public IQueryable<CpqLine> GetProductLines()
        {
            return _context.CpqLines.Include("Family").OrderBy(l => l.LineDescription);
        }

        public IQueryable<CpqGeneric> GetGenerics()
        {
            return _context.CpqGenerics.Where(g => !g.Deleted && g.Active.HasValue && g.Active.Value);
        }

        public IQueryable<CpqGeneric> GetActiveGenerics(string division)
        {
            /*
            SELECT DISTINCT g.*
            FROM cpq_generic g
            INNER JOIN cpq_item i ON i.genericid=g.id
            LEFT JOIN Assets a ON a.itemnumber=i.itemnumber
            LEFT JOIN ProductItems p ON p.itemnumber=i.itemnumber
            WHERE i.Deleted=0 AND (p.division=@division OR a.division=@division)
            */
            return _context.CpqGenerics.FromSql($"SELECT DISTINCT g.* FROM cpq_generic g INNER JOIN cpq_item i ON i.genericid=g.id LEFT JOIN Assets a ON a.itemnumber=i.itemnumber LEFT JOIN ProductItems p ON p.itemnumber=i.itemnumber WHERE i.Deleted=0 AND (p.division={division} OR a.division={division})");
        }

        public IQueryable<CpqItem> GetActiveItems(string division)
        {
            /*
                SELECT DISTINCT i.*
                FROM cpq_item i
                LEFT JOIN Assets a ON a.itemnumber=i.itemnumber
                LEFT JOIN ProductItems p ON p.itemnumber=i.itemnumber
                WHERE i.Deleted=0 AND (p.division=@division OR a.division=@division)
            */
            return _context.CpqItems.FromSql($"SELECT DISTINCT i.* FROM cpq_item i LEFT JOIN Assets a ON a.itemnumber=i.itemnumber LEFT JOIN ProductItems p ON p.itemnumber=i.itemnumber WHERE i.Deleted=0 AND (p.division={division} OR a.division={division})");
        }

        public IEnumerable<CpqItem> GetItemsForGenericAndAttributes(string division, int genericId, string attributes)
        {
            return _context.CpqItems.FromSql($"EXEC GetItemsForGenericAndAttributes {genericId},{attributes},{division}").AsEnumerable();

        }

        public IQueryable<ProductAttribute> GetProductAttributes(int genericId)
        {
            var conn = _context.Database.GetDbConnection(); // We didn't open it, so don't close it
            var command = (SqlCommand)conn.CreateCommand();
            if (command.Connection.State != ConnectionState.Open)
            {
                command.Connection.Open();
            }
            command.CommandText = "GetProductAttributesForGeneric";
            command.CommandType = System.Data.CommandType.StoredProcedure;

            command.Parameters.AddWithValue("@genericId", genericId);


            var result = new List<ProductAttribute>();
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var item = new ProductAttribute();
                    if (!reader.IsDBNull(0)) item.Name = reader.GetString(0);
                    if (!reader.IsDBNull(1)) item.Value = reader.GetString(1);

                    result.Add(item);
                }
            }

            return result.AsQueryable();
        }

        public CpqLine GetProductLine(int id)
        {
            return _context.CpqLines.FirstOrDefault(p => p.Id == id);
        }

        private void TouchRingfence(IUserIdentity identity, Ringfence ringfence)
        {
            ringfence.LastUpdatedDate = _timeProvider.GetUtcNow().UtcDateTime;
            ringfence.LastUpdatedBy = identity.GetIdentity().LoginName;
        }

        public Ringfence CreateRingfence(IUserIdentity identity, Ringfence ringfence)
        {
            TouchRingfence(identity, ringfence);
            _context.Ringfences.Add(ringfence);
            _context.SaveChanges();
            return ringfence;
        }

        public Ringfence UpdateRingfence(IUserIdentity identity, Ringfence ringfence)
        {
            TouchRingfence(identity, ringfence);
            _context.Ringfences.Update(ringfence);
            _context.SaveChanges();
            return ringfence;
        }

        public void DeleteRingfence(Ringfence ringfence)
        {
            var items = _context.RingfenceItems.Where(r => r.RingfenceId == ringfence.Id).ToArray();
            foreach (var item in items)
            {
                _context.RingfenceItems.Remove(item);
            }
            _context.Ringfences.Remove(ringfence);
            _context.SaveChanges();
        }

        public Ringfence GetRingfence(int id)
        {
            return _context.Ringfences.FirstOrDefault(r => r.Id == id);
        }

        public IQueryable<Ringfence> GetRingfences()
        {
            return _context.Ringfences;
        }

        public IList<Ringfence> GetRingfencesForAsset(string assetId, DateTime startDate, DateTime endDate)
        {
            return (
                from item in _context.RingfenceItems
                join ringfence in _context.Ringfences on item.RingfenceId equals ringfence.Id
                where item.AssetId == assetId
                    && ringfence.FromDate <= endDate
                    && startDate <= ringfence.ToDate
                orderby ringfence.FromDate, ringfence.ToDate
                select ringfence
            ).ToList();
        }

        private void TouchRingfenceItem(IUserIdentity identity, RingfenceItem ringfenceItem)
        {
            ringfenceItem.LastUpdatedDate = _timeProvider.GetUtcNow().UtcDateTime;
            ringfenceItem.LastUpdatedBy = identity.GetIdentity().LoginName;
        }

        public RingfenceItem CreateRingfenceItem(IUserIdentity identity, RingfenceItem ringfenceItem)
        {
            TouchRingfenceItem(identity, ringfenceItem);
            _context.RingfenceItems.Add(ringfenceItem);
            _context.SaveChanges();
            return ringfenceItem;
        }

        public RingfenceItem UpdateRingfenceItem(IUserIdentity identity, RingfenceItem ringfenceItem)
        {
            TouchRingfenceItem(identity, ringfenceItem);
            _context.RingfenceItems.Update(ringfenceItem);
            _context.SaveChanges();
            return ringfenceItem;
        }

        public void DeleteRingfenceItem(RingfenceItem ringfenceItem)
        {
            _context.RingfenceItems.Remove(ringfenceItem);
            _context.SaveChanges();
        }

        public IQueryable<RingfenceItem> GetRingfenceItems(int ringfenceId)
        {
            return _context.RingfenceItems.Where(r => r.RingfenceId == ringfenceId);
        }

        public IReadOnlyDictionary<int, int> GetRingfenceItemCounts(IReadOnlyCollection<int> ringfenceIds)
        {
            if (ringfenceIds.Count == 0)
            {
                return new Dictionary<int, int>();
            }

            return _context.RingfenceItems
                .Where(item => item.RingfenceId.HasValue && ringfenceIds.Contains(item.RingfenceId.Value))
                .GroupBy(item => item.RingfenceId!.Value)
                .ToDictionary(group => group.Key, group => group.Count());
        }

        public RingfenceItem AddAssetToRingfence(IUserIdentity identity, Ringfence ringfence, string assetId)
        {
            var existing = _context.RingfenceItems.FirstOrDefault(r => r.RingfenceId == ringfence.Id && r.AssetId == assetId);
            if (existing != null)
            {
                return existing;
            }

            var ringfenceItem = new RingfenceItem(identity.GetIdentity().LoginName)
            {
                AssetId = assetId,
                RingfenceId = ringfence.Id
            };
            TouchRingfenceItem(identity, ringfenceItem);
            _context.RingfenceItems.Add(ringfenceItem);
            _context.SaveChanges();

            return ringfenceItem;

        }

        public IQueryable<VwAssetItem> GetAssetsForRingfence(int ringfenceId)
        {
            var items = from asset in _context.VwAssetItems
                        join ringfenceItem in _context.RingfenceItems on asset.Id equals ringfenceItem.AssetId
                        join ringfence in _context.Ringfences on ringfenceItem.RingfenceId equals ringfence.Id
                        where ringfence.Id == ringfenceId
                        select asset;
            return items.OrderBy(a => a.Id);
        }

        public VwHeader GetAgreement(int id)
        {
            return _context.VwHeaders.FirstOrDefault(a => a.Id == id);
        }

        public Header? GetAgreement(string agreementNumber)
        {
            return _context.Headers.Include(i => i.Lines).FirstOrDefault(a => a.AgreementNumber == agreementNumber);
        }

        // Notes
        public ChangeOrderContact UpsertChangeOrderContact(ChangeOrderContact changeOrderContact, bool add = false)
        {
            if (add)
            {
                _context.ChangeOrderContacts.Add(changeOrderContact);
            }
            else
            {
                _context.ChangeOrderContacts.Update(changeOrderContact);
            }

            _context.SaveChanges();

            return changeOrderContact;
        }

        public Note GetNote(int id)
        {
            return _context.Notes.FirstOrDefault(n => n.Id == id);
        }

        public Note CreateNote(IUserIdentity identity, Note note)
        {
            note.LastUpdatedDate = DateTime.UtcNow;
            note.LastUpdatedBy = identity.GetIdentity().LoginName;
            _context.Notes.Add(note);
            _context.SaveChanges();
            return note;
        }

        public void DeleteNote(IUserIdentity identity, Note note)
        {
            _context.Notes.Remove(note);
            _context.SaveChanges();
        }

        public void SetLineActivationStatus(Line line, ActivationStatus status, IUserIdentity identity)
        {
            line.ActivationStatus = (int)status;
            line.LastUpdatedDate = DateTime.UtcNow;
            line.LastUpdatedBy = identity.GetIdentity().LoginName;
            _context.SaveChanges();
        }

        public Note UpdateNote(IUserIdentity identity, Note note)
        {
            note.LastUpdatedDate = DateTime.UtcNow;
            note.LastUpdatedBy = identity.GetIdentity().LoginName;
            _context.Notes.Update(note);
            _context.SaveChanges();
            return note;
        }

        public IQueryable<Note> GetNotes(string noteType, string key)
        {
            return _context.Notes.Where(n => n.NoteType == noteType && n.ParentId == key);
        }

        public IQueryable<Note> GetNotes(string noteType)
        {
            return _context.Notes.Where(n => n.NoteType == noteType);
        }

        public IList<DataRefresh> GetRefreshes()
        {
            var refreshes = _context.DataRefreshes.OrderBy(i => i.Key).ToList();

            var cpqLastRun = _context.CpqLogs
                .Where(l => l.RunType == "Last2Hours" && !l.IsError)
                .OrderByDescending(l => l.RunOn)
                .Select(l => (DateTime?)l.RunOn)
                .FirstOrDefault();

            refreshes.Add(new DataRefresh
            {
                Key = "CPQ",
                Description = "CPQ data from Dataverse via PCatSync. Every 30 minutes.",
                LastSuccessfulRunUtc = cpqLastRun
            });

            return refreshes.OrderBy(i => i.Key).ToList();
        }

        public string[] GetWarehouseDivisionCodes()
        {
            return _context.WarehouseItems
                .Select(item => item.DivisionCode)
                .Distinct()
                .ToArray();
        }

        public IList<WarehouseItem> GetWarehouses(string[] divisions)
        {
            var warehouses = _context.WarehouseItems
                .Where(i => divisions.Contains(i.DivisionCode) && !i.WarehouseCode.StartsWith("zz") && !i.Warehouse.StartsWith("zz"))
                .OrderBy(i => i.WarehouseCode)
                .ToList();
            warehouses = warehouses
                .Where(i => !Constants.Warehouses.IsExcluded(i.WarehouseCode))
                .ToList();
            return warehouses;
        }

        public ChangeOrderHeader UpsertChangeOrderHeader(ChangeOrderHeader changeHeader, bool add = false)
        {
            if (add)
            {
                _context.ChangeOrderHeaders.Add(changeHeader);
            }
            else
            {
                _context.ChangeOrderHeaders.Update(changeHeader);
            }

            _context.SaveChanges();

            return changeHeader;
        }

        public void DeleteChangeOrderHeader(int changeOrderHeaderId)
        {
            _context.ChangeOrderHeaders.Where(i => i.ChangeOrderId == changeOrderHeaderId).ExecuteDelete();
            _context.SaveChanges();
        }

        public void DeleteChangeOrder(int changeOrderId)
        {
            _context.ChangeOrders.Where(i => i.Id == changeOrderId).ExecuteDelete();
            _context.SaveChanges();
        }

        public void DeleteChangeOrderLine(int changeOrderLineId)
        {
            _context.ChangeOrderLines.Where(i => i.Id == changeOrderLineId).ExecuteDelete();
            _context.SaveChanges();
        }

        public ChangeOrder UpdateChangeOrder(ChangeOrder change)
        {
            _context.ChangeOrders.Update(change);

            _context.SaveChanges();

            return change;
        }

        public ChangeOrderLine UpsertChangeOrderLine(ChangeOrderLine changeLine)
        {
            if (changeLine.Id == 0)
            {
                _context.ChangeOrderLines.Add(changeLine);
            }
            else
            {
                _context.ChangeOrderLines.Update(changeLine);
            }

            _context.SaveChanges();

            return changeLine;
        }


        public ChangeOrderComment AddChangeOrderComment(int changeOrderId, string comment, string loginName)
        {
            var entity = new ChangeOrderComment()
            {
                ChangeOrderId = changeOrderId,
                Comment = comment,
                CreatedBy = loginName,
                CreatedDate = DateTime.UtcNow
            };

            _context.ChangeOrderComments.Add(entity);
            _context.SaveChanges();

            return entity;
        }

        public IList<ChangeOrderLine> UpsertChangeOrderLines(IList<ChangeOrderLine> changeLines)
        {
            foreach (var changeLine in changeLines)
            {
                if (changeLine.Id == 0)
                {
                    _context.ChangeOrderLines.Add(changeLine);
                }
                else
                {
                    _context.ChangeOrderLines.Update(changeLine);
                }
            }

            _context.SaveChanges();

            return changeLines;
        }

        public async Task<IReadOnlyList<RingfenceAssestDetails>> GetOverlappingRingfenceDetailsAsync(int ringfenceId, IReadOnlyList<string> assetIds, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken)
        {
            var query = from item in _context.Set<RingfenceItem>()
                        join ringfence in _context.Set<Ringfence>() on item.RingfenceId equals ringfence.Id
                        where item.RingfenceId != ringfenceId
                              && assetIds.Contains(item.AssetId)
                              && ringfence.FromDate < toDate
                              && fromDate < ringfence.ToDate
                        select new RingfenceAssestDetails
                        {
                            RingfenceId = ringfence.Id,
                            AssetIds = new List<string> { item.AssetId },
                            Title = ringfence.Title,
                            FromDate = ringfence.FromDate,
                            ToDate = ringfence.ToDate,
                            Owner = ringfence.Owner
                        };

            return await query.AsNoTracking().ToListAsync(cancellationToken);
        }

        public ChangeOrderAddress UpsertChangeOrderAddress(ChangeOrderAddress changeOrderAddress, bool add = false)
        {
            if (add)
            {
                _context.ChangeOrderAddresses.Add(changeOrderAddress);
            }
            else
            {
                _context.ChangeOrderAddresses.Update(changeOrderAddress);
            }

            _context.SaveChanges();

            return changeOrderAddress;
        }
    }
}
