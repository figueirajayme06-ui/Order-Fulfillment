using Infragistics.Web.Mvc;
using OF.Common.Infrastructure.OF;
using OF.Data.Database;
using OF.UI.Database;
using OF.UI.Helpers;
using OF.UI.Identity;
using OF.UI.Models;
using System.Globalization;
using System.Text;
using static OF.Common.Enums;

namespace OF.UI.Engine
{
    public class FulfilmentEngine : CoreFulfilmentEngine, IFulfilmentEngine
    {
        private readonly IDataRepository _repository;
        private readonly IUserIdentity _identity;

        public FulfilmentEngine(IDataRepository repository, IUserIdentity identity) : base(repository)
        {
            _repository = repository;
            _identity = identity;
        }

        AlternativeOption[] GetAlternativeOptionsForGeneric(string genericCode)
        {
            var result = new List<AlternativeOption>();
            result.AddRange(_repository.GetAlternativeOptions(genericCode));
            return result.ToArray();
        }

        public FulfilmentResponse SatisfyLine(FulfilmentRequest request)
        {
            try
            {
                var response = new FulfilmentResponse();
                CpqGeneric rootGeneric;

                // Get the line
                var line = _repository.GetLine(request.LineId);
                if (line == null)
                {
                    response.IsSuccess = false;
                    response.ErrorMessage = "Line not found.";
                    return response;
                }

                // Services
                var service = _repository.GetService(line.ItemNumber);
                if (service != null)
                {
                    response.IsSuccess = false;
                    response.ErrorMessage = "This line is a service and does not require fulfilment.";
                    return response;
                }

                // Generics
                var specific = _repository.GetItem(line.ItemNumber);
                if (specific != null)
                {
                    response.ItemNumber = specific.ItemNumber;
                    var specificGeneric = _repository.GetGeneric(specific.GenericId);

                    if (specificGeneric == null)
                    {
                        response.IsSuccess = false;
                        response.ErrorMessage = "Provided specific does not match a generic.";
                        return response;
                    }

                    rootGeneric = specificGeneric;
                    response.GenericCode = specificGeneric.GenericCode;
                    response.IsSerialized = specificGeneric.M3Type == "Serialized";
                    response.ItemDescription = specificGeneric.GenericDescription == null ? "Unknown" : specificGeneric.GenericDescription;
                }
                else
                {
                    // Get the product name from the database
                    var generic = _repository.GetGeneric(line.ItemNumber);
                    if (generic == null)
                    {
                        response.IsSuccess = false;
                        response.ErrorMessage = "Generic/item not found.";
                        return response;
                    }

                    rootGeneric = generic;
                    response.GenericCode = generic.GenericCode;
                    response.IsSerialized = generic.M3Type == "Serialized";
                    response.ItemDescription = generic.GenericDescription == null ? "Unknown" : generic.GenericDescription;
                }

                // Include the line description
                if (!string.IsNullOrWhiteSpace(line.ItemDescription) &&
                    !response.ItemDescription.Equals(line.ItemDescription, StringComparison.InvariantCultureIgnoreCase))
                {
                    response.ItemDescription = $"{line.ItemDescription}  ({response.ItemDescription})";
                }

                // Alternatives
                response.AlternativeOptions = GetAlternativeOptionsForGeneric(response.GenericCode);

                // Attributes
                if (line.Attributes != null)
                {
                    var attributes = new List<string>();
                    var displayAttributes = new List<string>();
                    var consideredAttributes = _repository.GetFulfilmentAttributes();
                    var attributeNames = consideredAttributes.Select(i => i.AttributeName.Normalize(NormalizationForm.FormC)).Distinct().ToList();

                    var parts = line.Attributes.Split(';');
                    var localizedParts = line.LocalizedAttributes?.Split(';');

                    for (int i = 0; i < parts.Length; i++)
                    {
                        var p = parts[i];
                        var nameValue = p.Split(':');
                        if (nameValue.Count() == 2)
                        {
                            var attributeName = nameValue[0].Trim().Normalize(NormalizationForm.FormC);
                            var culture = CultureInfo.InvariantCulture;
                            var comparisons = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

                            if (attributeNames.Any(name => string.Compare(name.Normalize(NormalizationForm.FormC), attributeName, culture, comparisons) == 0))
                            {
                                attributes.Add(p);
                                var displayPart = (localizedParts != null && i < localizedParts.Length)
                                    ? localizedParts[i]
                                    : p;
                                displayAttributes.Add(displayPart);
                            }
                        }
                    }

                    response.Attributes = attributes.ToArray();
                    response.DisplayAttributes = displayAttributes.ToArray();
                }
                else
                {
                    response.Attributes = new string[0];
                    response.DisplayAttributes = new string[0];
                }

                var productLine = _repository.GetProductLine(rootGeneric.LineId);

                if (line.AgreementLineNumber == null || productLine == null)
                {
                    throw new Exception("Agreement line number or product line not found.");
                }

                response.AgreementLineNumber = line.AgreementLineNumber;
                response.ProductLineId = rootGeneric.LineId;
                response.ProductFamilyId = productLine.FamilyId;
                response.IsChild = line.AgreementLineNumber.Contains(".");
                response.LineId = line.Id;
                response.Warehouse = line.Warehouse;
                response.IsSuccess = true;
                response.ErrorMessage = String.Empty;
                response.Division = line.Division;
                return response;
            }
            catch (Exception ex)
            {
                return new FulfilmentResponse()
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        const int SERVICE_DAYS = 3;

        string FormatDateRange(DateTime startDate, DateTime endDate, string dateFormat)
        {
            if (startDate == endDate) return startDate.ToString(dateFormat);
            return startDate.ToString(dateFormat) + " - " + endDate.ToString(dateFormat);
        }

        Bar[][] GenerateSerializedBars(SerializedQueryResult[] items, int lineId, int daysInBar, DateTime startDate, DateTime endDate, string dateFormat)
        {
            List<Bar[]> barArray = new List<Bar[]>();

            foreach (var item in items)
            {
                List<Bar> bars = new List<Bar>();

                // Reference dates
                if (item.Asset.Status.ToUpper() == "ONHIRE")
                {

                    if (String.IsNullOrEmpty(item.Asset.AgreementNumber))
                    {
                        bars.Add(new Bar()
                        {
                            Start = 0,
                            End = daysInBar - 1,
                            CssClass = "table-danger",
                            Text = "On Hold",
                            ShortText = String.Empty,
                            Quantity = 0
                        });
                    }
                    else
                    {
                        var startBarDate = item.Asset.DeliveryDate.HasValue ? item.Asset.DeliveryDate.Value.Date : DateTime.MinValue;
                        var endBarDate = item.Asset.TerminationDate.HasValue ? item.Asset.TerminationDate.Value.Date : (item.Asset.AgreementLineValidToDate.HasValue ? item.Asset.AgreementLineValidToDate.Value.Date : DateTime.MaxValue);                 
                        if (endBarDate <= DateTime.Now.Date) endBarDate = DateTime.Now.Date; // If the end date is in the past, make it today
                        if (startBarDate < endDate && startDate < endBarDate) // Do the ranges overlap?
                        {
                            bars.Add(new Bar()
                            {
                                Start = (startBarDate - startDate).Days,
                                End = (endBarDate - startDate).Days,
                                CssClass = "table-danger",
                                Text = FormatDateRange(startBarDate, endBarDate, dateFormat) + " On Hire for " + item.Asset.AgreementNumber + " (" + item.Asset.CustomerName + ")",
                                ShortText = String.Empty,
                                Quantity = 0
                            });
                        }

                        if (startBarDate < endDate && startDate < endBarDate) // Do the ranges overlap?
                        {
                            if (endBarDate < DateTime.Now.Date) // Overdue
                            {
                                endBarDate = DateTime.Now.Date;
                                bars.Add(new Bar()
                                {
                                    Start = (startBarDate - startDate).Days,
                                    End = (endBarDate - startDate).Days,
                                    CssClass = "table-warning",
                                    Text = FormatDateRange(startBarDate, endBarDate, dateFormat) + "Overdue: On Hire for " + item.Asset.AgreementNumber + " (" + item.Asset.CustomerName + ")",
                                    ShortText = String.Empty,
                                    Quantity = 0
                                });
                            }
                        }
                    }
                }

                if (item.Asset.Status.ToUpper() == "ASSESS" || item.Asset.Status.ToUpper() == "SERVICE" || item.Asset.Status.ToUpper() == "INSERVICE")
                {
                    var startBarDate = item.Asset.IonlastModified.HasValue ? item.Asset.IonlastModified.Value : DateTime.Now;

                    var endBarDate = startBarDate.AddDays(SERVICE_DAYS);
                    // Do we have an estimated ready date?
                    if (item.Asset.EstimatedReadyDate.HasValue)
                    {
                        if (item.Asset.EstimatedReadyDate.Value > startBarDate) { 
                            endBarDate = item.Asset.EstimatedReadyDate.Value;
                        }
                    }

                    if (startBarDate < endDate && startDate < endBarDate) // Do the ranges overlap?
                    {
                        bars.Add(new Bar()
                        {
                            Start = (startBarDate - startDate).Days,
                            End = (endBarDate - startDate).Days,
                            CssClass = "table-warning",
                            Text = FormatDateRange(startBarDate, endBarDate, dateFormat) + " Service",
                            ShortText = String.Empty,
                            Quantity = 0
                        });
                    }
                }

                if (item.Asset.Status.ToUpper() == "REPAIR")
                {
                    var startBarDate = item.Asset.IonlastModified.HasValue ? item.Asset.IonlastModified.Value : DateTime.Now;
                    var endBarDate = endDate; // No end in sight?
                    // But, do we have an estimated ready date?
                    if (item.Asset.EstimatedReadyDate.HasValue)
                    {
                        if (item.Asset.EstimatedReadyDate.Value > startBarDate)
                        {
                            endBarDate = item.Asset.EstimatedReadyDate.Value;
                        }
                    }

                    if (startBarDate < endDate && startDate < endBarDate) // Do the ranges overlap?
                    {
                        bars.Add(new Bar()
                        {
                            Start = (startBarDate - startDate).Days,
                            End = (endBarDate - startDate).Days,
                            CssClass = "table-warning",
                            Text = FormatDateRange(startBarDate, endBarDate, dateFormat) + " Major Repair",
                            ShortText = String.Empty,
                            Quantity = 0
                        });
                    }
                }

                if (item.Asset.Status.ToUpper() == "COLLECTION" || item.Asset.Status.ToUpper() == "IN TRANSIT")
                {
                    var startBarDate = item.Asset.IonlastModified.HasValue ? item.Asset.IonlastModified.Value : DateTime.Now;
                    var endBarDate = endDate; // No end in sight
                    if (startBarDate < endDate && startDate < endBarDate) // Do the ranges overlap?
                    {
                        bars.Add(new Bar()
                        {
                            Start = (startBarDate - startDate).Days,
                            End = (endBarDate - startDate).Days,
                            CssClass = "table-warning",
                            Text = FormatDateRange(startBarDate, endBarDate, dateFormat) + " Collection/Transit",
                            ShortText = String.Empty,
                            Quantity = 0
                        });
                    }
                }

                // Reservations
                if (item.Reservations != null)
                {
                    foreach (var r in item.Reservations.Where(r => r.IsConfirmed == false))
                    {
                        var startBarDate = r.DeliveryDate.HasValue ? r.DeliveryDate.Value.Date : (r.ValidFromDate.HasValue ? r.ValidFromDate.Value.Date : DateTime.MinValue);
                        var endBarDate = r.TerminationDate.HasValue ? r.TerminationDate.Value.Date : (r.ValidToDate.HasValue ? r.ValidToDate.Value.Date : DateTime.MaxValue);

                        if (startBarDate < endDate && startDate < endBarDate) // Do the ranges overlap?
                        {
                            var startOffset = (startBarDate - startDate).Days;
                            var endOffset = (endBarDate - startDate).Days;

                            if (r.CustomerName == "RINGFENCE")
                            {
                                bars.Add(new Bar()
                                {
                                    Start = startOffset,
                                    End = endOffset,
                                    CssClass = "table-danger",
                                    Text = FormatDateRange(startBarDate, endBarDate, dateFormat) + " Ringfenced:  " + (r.Notes.Length > 30 ? r.Notes.Substring(0,30) : r.Notes),
                                    ShortText = String.Empty,
                                    Quantity = 0
                                });
                            }
                            else
                            {
                                if (lineId == r.LineId)
                                {
                                    bars.Add(new Bar()
                                    {
                                        Start = startOffset,
                                        End = endOffset,
                                        CssClass = "table-info",
                                        Text = FormatDateRange(startBarDate, endBarDate, dateFormat) + " Reserved for this line " + r.AgreementNumber + " (" + r.CustomerName + ")",
                                        ShortText = String.Empty,
                                        Quantity = 0
                                    });
                                }
                                else
                                {
                                    bars.Add(new Bar()
                                    {
                                        Start = startOffset,
                                        End = endOffset,
                                        CssClass = "table-danger",
                                        Text = FormatDateRange(startBarDate, endBarDate, dateFormat) + " Reserved for " + r.AgreementNumber + " (" + r.CustomerName + ")",
                                        ShortText = String.Empty,
                                        Quantity = 0
                                    });
                                }
                            }
                        }
                    }
                }

                barArray.Add(bars.ToArray());
            }

            return barArray.ToArray();
        }

        string GetStockLevelBarColor(double stockLevel, double minStockLevel)
        {
            if (stockLevel < minStockLevel) return "table-danger";
            if (stockLevel < minStockLevel * 1.3) return "table-warning";
            return "table-success";
        }

        Bar[][] GenerateNonSerializedBars(NonSerializedQueryResult[] items, int daysInBar, DateTime startDate, DateTime endDate, double qtyRequired, string dateFormat)
        {
            List<Bar[]> barArray = new List<Bar[]>();

            foreach (var item in items)
            {
                List<Bar> bars = new List<Bar>();
                List<StockLevel> stockLevels = new List<StockLevel>();

                // For each day
                // Count = StockQuantity - AllocatedQuantity - ReservedQuantity
                // Reserved Quantity = count of reservations that cover the day
                for (var d=0; d<daysInBar; d++)
                {
                    var qty = item.Asset.StockQuantity - item.Asset.AllocatedQuantity;
                    var refDate = startDate.AddDays(d);
                    var reserved = item.Reservations.Where(i => i.IsConfirmed == false).Where(r => (r.DeliveryDate.HasValue ? r.DeliveryDate.Value.Date : (r.ValidFromDate.HasValue ? r.ValidFromDate.Value.Date : DateTime.MinValue)) <= refDate && (r.TerminationDate.HasValue ? r.TerminationDate.Value.Date : (r.ValidToDate.HasValue ? r.ValidToDate.Value.Date : DateTime.MaxValue)) >= refDate).Sum(r => r.Quantity);
                    qty = qty - reserved;
                    stockLevels.Add(new StockLevel() {  Stock = qty, Date = refDate});
                }

                // Now divide into bars
                Bar bar = null;
                DateTime startBarDate = startDate;
                DateTime endBarDate = startDate;

                for(var d=0; d<daysInBar; d++)
                {
                    endBarDate = startDate.AddDays(d);

                    if (bar == null || stockLevels[d].Stock != stockLevels[d-1].Stock)
                    {
                        if (bar != null)
                        {
                            bar.End = d-1; // End previous bar
                        }

                        bar = new Bar();
                        bar.Start = d;
                        bar.End = d;
                        bar.Quantity = (int)stockLevels[d].Stock;
                        bar.CssClass = GetStockLevelBarColor((double)stockLevels[d].Stock, qtyRequired);
                        bar.Text = FormatDateRange(startBarDate, endBarDate, dateFormat) + " " + stockLevels[d].Stock.ToString() + " available";
                        bar.ShortText = stockLevels[d].Stock.ToString();
                        bars.Add(bar);
                        startBarDate = startDate.AddDays(d);
                    }
                    else
                    {
                        bar.Quantity = (int)stockLevels[d].Stock;
                        bar.Text = FormatDateRange(startBarDate, endBarDate, dateFormat) + " " + stockLevels[d].Stock.ToString() + " available";
                        bar.ShortText = stockLevels[d].Stock.ToString();
                        bar.End = d; // Move current bar forward
                    }
                }
                
                barArray.Add(bars.ToArray());
            }

            return barArray.ToArray();
        }


        public StockResponse GetStock(IUserIdentity userIdentity, StockRequest request, string[] divisions)
        {
            try
            {
                var response = new StockResponse();
                var dateFormat = "dd/MM/yyyy";

                if (userIdentity != null && userIdentity.GetIdentity() != null)
                {
                    dateFormat = userIdentity.GetIdentity().DateFormat;
                }

                // Get the line
                var line = _repository.GetLine(request.LineId);
                if (line == null)
                {
                    response.IsSuccess = false;
                    response.ErrorMessage = "Line not found";
                    return response;
                }

                response.LineId = line.Id;
                response.Quantity = line.Quantity;
                response.StartDate = line.ValidFromDate;
                response.FulfilmentStatus = (FulfilmentStatus)line.FulfilmentStatus;
                if (line.DeliveryDate.HasValue && line.DeliveryDate.Value.Date < line.ValidFromDate.Date) response.StartDate = line.DeliveryDate.Value.Date;
                response.EndDate = line.ValidToDate;
                response.StartDateString = response.StartDate.ToString(dateFormat);
                response.EndDateString = response.EndDate.ToString(dateFormat);
                int daysInBar = (response.EndDate - response.StartDate).Days + 1;
                response.Cols = daysInBar;

                response.IsSerialized = request.IsSerialized;
                bool haveItems = false;
                if (request.IsSerialized)
                {
                    response.SerializedItems = _repository.GetSerializedStock(line.Id, divisions, request.Warehouse, request.Attributes)
                        .OrderBy(s => s.Asset.Warehouse == request.Warehouse ? 0 : 1) // Put the warehouse we're in at the top
                        .ThenBy(s => s.Asset.Warehouse) // Then sort by warehouse
                        .ThenBy(s => s.SubstitutionReason == null ? 0 : 1) // Put items with no substitution reason at the top
                        .ThenBy(s => s.Asset.ItemNumber) // Then sort by item
                        .ToArray();
                    response.Bars = GenerateSerializedBars(response.SerializedItems, line.Id, daysInBar, response.StartDate, response.EndDate, dateFormat);
                    haveItems = response.SerializedItems.Length > 0;
                }
                else
                {
                    response.NonSerializedItems = _repository.GetNonSerializedStock(line.Id, divisions, request.Warehouse, request.Attributes)
                        .OrderBy(s => s.Asset.Warehouse == request.Warehouse ? 0 : 1) // Put the warehouse we're in at the top
                        .ThenBy(s => s.Asset.Warehouse) // Then sort by warehouse
                        .ThenBy(s => s.SubstitutionReason == null ? 0 : 1) // Put items with no substitution reason at the top
                        .ThenBy(s => s.Asset.ItemNumber) // Then sort by item
                        .ToArray();
                    response.Bars = GenerateNonSerializedBars(response.NonSerializedItems, daysInBar, response.StartDate, response.EndDate, line.Quantity, dateFormat);
                    haveItems = response.NonSerializedItems.Length > 0;
                }

                response.OtherReservations = _repository.GetOtherReservationsForLine(line.Id);

                response.IsSuccess = true;
                response.ErrorMessage = String.Empty;
                return response;
            }
            catch (Exception ex)
            {
                return new StockResponse()
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                };
            }
        }
        
        public ReserveResponse Reserve(ReserveRequest request)
        {
            try
            {
                List<int> siblingVictims = new List<int>();

                var lineId = request.LineId;

                if (request.IsDelete)
                {
                    var reservation = _repository.GetReservation(request.ReservationId);
                    if (reservation != null)
                    {
                        _repository.DeleteReservation(request.ReservationId);
                        var line = _repository.GetLine(reservation.LineId); 
                        RecalculateStatusForLineAndHeader(line, _identity.GetIdentity().LoginName);
                        lineId = reservation?.LineId ?? 0;

                        // If we delete a reservation we need to reenable the activation button

                        if (line.ActivationStatus > 0)
                        {
                            _repository.SetLineActivationStatus(line, ActivationStatus.TODO, _identity);
                        }
                    }
                }
                else
                {
                    if (request.Multiple == 0) request.Multiple = 1; // Sanity check multiples

                    if (!request.IsRehire && !request.IsDepotFulfiled && !string.IsNullOrEmpty(request.AssetId))
                    {
                        var asset = _repository.GetAsset(request.AssetId);
                        if (asset != null && (asset.Status == "RemovedStock" || asset.Status == "Scrap" || asset.Status == "Sold"))
                        {
                            return new ReserveResponse()
                            {
                                IsSuccess = false,
                                ErrorMessage = $"Asset {request.AssetId} has been marked as '{asset.Status}' and cannot be allocated."
                            };
                        }
                    }

                    var reservation = new Reservation()
                    {
                        AssetId = request.AssetId,
                        LineId = request.LineId,
                        ItemNumber = request.ItemNumber,
                        Warehouse = request.Warehouse,
                        Quantity = 1,
                        EffectiveQuantity = 1 / (double)request.Multiple,
                        IsRehire = request.IsRehire,
                        IsDepotFulfilled = request.IsDepotFulfiled
                    };

                    var line = _repository.GetLine(request.LineId);
                    var required = ((int)(line.Quantity - _repository.GetReservationSumForLine(line.Id)))*request.Multiple;
                    var toAllocate = required;

                    if (request.IsRehire || request.IsDepotFulfiled)
                    {
                        if (request.Quantity == 0)
                        {
                            reservation.Quantity = required;
                            reservation.EffectiveQuantity = required;
                        }
                        else
                        {
                            reservation.Quantity = request.Quantity;
                            reservation.EffectiveQuantity = request.Quantity;
                        }
                    }
                    else if (!request.IsSerialized)
                    {
                        reservation.Quantity = request.Quantity;
                        reservation.EffectiveQuantity = required/ (double)request.Multiple;
                    }
                    else
                    {
                        var startBarDate = line.DeliveryDate?.Date ?? line.ValidFromDate.Date;
                        var endBarDate = line.TerminationDate?.Date ?? line.ValidToDate.Date;

                        // Check for overlapping reservations
                        var overlapping = _repository.GetOverlappingReservations(request.AssetId, startBarDate, endBarDate);

                        foreach (var o in overlapping)
                        {
                            var victimLine = _repository.GetLine(o.LineId);
                            if (victimLine != null)
                            {
                                if (victimLine.HeaderId.HasValue && victimLine.LastUpdatedBy != null)
                                {
                                    var victimHeader = _repository.GetHeader(victimLine.HeaderId.Value);

                                    var alert = new Alert()
                                    {
                                        AffectedUser = victimLine.LastUpdatedBy,
                                        LineId = o.LineId,
                                        Acknowledged = false,
                                        Text = "Reservation clash: " + victimHeader.AgreementNumber + " (" + victimLine.Id.ToString() + ")"
                                    };
                                    _repository.AddAlert(_identity, alert);
                                }

                                if (victimLine.HeaderId == line.HeaderId && !siblingVictims.Contains(victimLine.Id))
                                {
                                    siblingVictims.Add(victimLine.Id);
                                }
                            }

                            // If we have a reservation clash on a previously reserved line, delete it and mark as needing fulfilled

                            var deletedReservation = _repository.DeleteReservation(o.Id);

                            if (victimLine != null && (int)victimLine.ActivationStatus > 0 && deletedReservation != null)
                            {
                                _repository.SetLineActivationStatus(line, ActivationStatus.TODO, _identity);
                            }

                            RecalculateStatusForLineAndHeader(victimLine, _identity.GetIdentity().LoginName);
                        }
                    }

                    if (reservation.Quantity > 0)
                    {
                        _repository.CreateReservation(_identity, reservation);
                        RecalculateStatusForLineAndHeader(line, _identity.GetIdentity().LoginName);
                       _repository.DeleteAlertsForLine(line.Id);
                    }
                }

                var header = _repository.GetHeaderForLineId(lineId);

                return new ReserveResponse()
                {
                    IsActivatable = header.IsActivatable(),
                    IsSuccess = true,
                    SiblingVictims = siblingVictims,
                    HeaderStatus = (header?.FulfilmentStatus ?? (int)FulfilmentStatus.Unfulfilled)
                };
            }
            catch (Exception ex)
            {
                return new ReserveResponse()
                {
                    IsActivatable = false,
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public BulkActionResponse BulkAction(BulkActionRequest request)
        {
            try
            {
                var lines = _repository.GetLines(request.HeaderId).ToArray();

                if (request.IsDepotFulfil)
                {
                    foreach (var line in lines)
                    {
                        if (request.Items.Contains(line.Id))
                        {
                            if (request.All || (line.FulfilmentStatus != (int)FulfilmentStatus.FullyFulfiled && line.FulfilmentStatus != (int)FulfilmentStatus.OverFulfilled))
                            {
                                if (request.All) _repository.DeleteReservationsForLine(line.Id);

                                var reserveRequest = new ReserveRequest()
                                {
                                    AssetId = "DEPOTFULFIL",
                                    IsDelete = false,
                                    IsDepotFulfiled = true,
                                    IsRehire = false,
                                    IsSerialized = false,
                                    ItemNumber = "DEPOTFULFIL",
                                    LineId = line.Id,
                                    MinAvailableInPeriod = 0,
                                    Warehouse = request.Warehouse,
                                    Multiple = 1
                                };
                                Reserve(reserveRequest);
                            }
                        }
                    }
                }

                if (request.IsRehire)
                {
                    foreach (var line in lines)
                    {
                        if (request.Items.Contains(line.Id))
                        {
                            if (request.All || (line.FulfilmentStatus != (int)FulfilmentStatus.FullyFulfiled && line.FulfilmentStatus != (int)FulfilmentStatus.OverFulfilled))
                            {
                                CpqGeneric matchedGeneric = null;
                                var specific = _repository.GetItem(line.ItemNumber);
                                if (specific != null) { 
                                    matchedGeneric = _repository.GetGeneric(specific.GenericId);
                                }
                                else
                                {
                                    matchedGeneric = _repository.GetGeneric(line.ItemNumber);
                                }

                                if (matchedGeneric != null)
                                {
                                    var alternatives = GetAlternativeOptionsForGeneric(matchedGeneric.GenericCode);
                                    if (alternatives.Length > 0)
                                    {
                                        // Only delete existing reservations if we can actually rehire
                                        if (request.All) _repository.DeleteReservationsForLine(line.Id);

                                        var reserveRequest = new ReserveRequest()
                                        {
                                            AssetId = alternatives[0].ItemNumber,
                                            IsDelete = false,
                                            IsDepotFulfiled = false,
                                            IsRehire = true,
                                            IsSerialized = false,
                                            ItemNumber = alternatives[0].ItemNumber,
                                            LineId = line.Id,
                                            MinAvailableInPeriod = 0,
                                            Warehouse = request.Warehouse,
                                            Multiple = 1
                                        };
                                        Reserve(reserveRequest);
                                    }
                                    else
                                    {
                                        return new BulkActionResponse()
                                        {
                                            IsSuccess = false,
                                            ErrorMessage = "No rehire options available for " + line.ItemNumber
                                        };
                                    }
                                }
                                else
                                {
                                    return new BulkActionResponse()
                                    {
                                        IsSuccess = false,
                                        ErrorMessage = "Generic not found for " + line.ItemNumber
                                    };
                                }
                            }
                        }
                    }
                }

                var header = _repository.GetHeader(request.HeaderId);

                return new BulkActionResponse()
                {
                    IsSuccess = true,
                    HeaderStatus = (header?.FulfilmentStatus ?? (int)FulfilmentStatus.Unfulfilled)
                };
            }
            catch(Exception ex)
            {
                return new BulkActionResponse()
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                };
            }
        }
    }
}
