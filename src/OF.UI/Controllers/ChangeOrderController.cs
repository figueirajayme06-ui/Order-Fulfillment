using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using OF.Data.Database;
using OF.UI.Database;
using OF.UI.Identity;
using OF.UI.Models;
using OF.UI.ViewModels.ChangeOrders;
using OF.Common;
using OF.UI.Shared.Controllers;

namespace OF.UI.Controllers
{
    [Authorize(Roles = $"{Constants.Roles.ChangeOrder},{Constants.Roles.ChangeApproval}")]
    public partial class ChangeOrderController : CommonController
    {
        private readonly IDataRepository _repository;
        private readonly IUserIdentity _userIdentity;

        public record HeaderFields(
            DateTime? onHire,
            DateTime? offHire,
            string? rateType,
            string? jobAic,
            string? projectCode,
            string? poNumber,
            string? poAmount,
            int? rentalDuration,
            int? minimumRental,
            int? daysInWeek,
            int? weeksInMonth,
            decimal? priceBase,
            decimal? targetCustomerAmount,
            decimal? priceAdjustment);

        public record LineFields(
            int? lineId,
            string generic,
            float quantity,
            string attributes,
            DateTime? delivery,
            DateTime onHire,
            DateTime offHire,
            DateTime? termination,
            DateTime? collection);

        public ChangeOrderController(IDataRepository repository, IUserIdentity userIdentity) : base(repository)
        {
            _repository = repository;
            _userIdentity = userIdentity;
        }

        public IActionResult List(int headerId)
        {
            var header = _repository.GetHeaderWithChanges(headerId);

            var model = new ChangeOrdersListViewModel
            {
                Header = header!,
                Success = false
            };

            return View(model);
        }

        [HttpPost]
        public IActionResult List(CreateChangeOrderRequest request)
        {
            var header = _repository.GetHeaderWithChanges(request.HeaderId);

            var activeChanges = header?.ChangeOrders?.Any(c => c.IsActive) ?? false;
            if (!activeChanges)
            {
                ChangeOrder changeOrder = new ChangeOrder
                {
                    HeaderId = request.HeaderId,
                    Status = (int)ChangeStatus.Pending,
                    CreatedBy = _userIdentity.GetIdentity().LoginName,
                    CreatedDate = DateTime.UtcNow,
                };

                changeOrder = _repository.AddChangeOrder(changeOrder);

                return RedirectToAction("Details", new { headerId = request.HeaderId, changeId = changeOrder.Id });
            }

            ModelState.AddModelError("HeaderId", "There is already an active change order for this header.");

            var model = new ChangeOrdersListViewModel
            {
                Header = header!,
                Success = true
            };

            return View(model);
        }

        public IActionResult Details(int headerId, int changeId)
        {
            var user = _userIdentity.GetIdentity();

            var header = _repository.GetHeaderWithChanges(headerId);
            if (header == null)
            {
                return new NotFoundResult();
            }

            var services = _repository.GetServices().ToArray();

            var allLines = _repository
            .GetAllLinesForChangeOrder(headerId)
            .ToArray();

            var changeOrder = header.ChangeOrders?.FirstOrDefault(i => i.Id == changeId);
            var LinesAndChanges = allLines.OrderBy(i => i.AgreementLineNumber)?.Select((l, index) =>
                new ChangeOrderLineWithChange
                {
                    Line = l,
                    ChangeOrderLine = changeOrder?.ChangeOrderLines?.FirstOrDefault(c => c.LineId != null && c.LineId == l.Id),
                    Index = index
                })
                .ToList();
            var changesWithLines = LinesAndChanges.Where(i => i.ChangeOrderLine != null).Select(i => i.ChangeOrderLine).ToArray();
            var changesWithoutLines = changeOrder?.ChangeOrderLines?.Where(i => i.LineId == null).ToArray();

            if (changesWithoutLines != null)
            {
                int startingIndex = LinesAndChanges.Count;

                var additionalItems = changesWithoutLines.Select((change, i) => new ChangeOrderLineWithChange
                {
                    Line = null,
                    ChangeOrderLine = change,
                    Index = startingIndex + i
                });
                LinesAndChanges.AddRange(additionalItems);
            }

            // Build the model
            var model = new ChangeOrdersViewModel
            {
                Header = header,
                Lines = LinesAndChanges,
                Services = services,
                ChangeOrder = changeOrder
            };

            return View(model);
        }        
        
        [HttpPost]
        public IActionResult EditHeader(int headerId, int changeId, string tab, [FromForm] EditHeaderRequest request)
        {
            var header = _repository.GetHeaderWithChanges(headerId);
            var change = header?.ChangeOrders?.FirstOrDefault(i => i.Id == changeId);

            if (change == null)
            {
                return Json(new { success = false, message = "Change order not found" });
            }

            if (!change.IsEditable)
            {
                return Json(new { success = false, message = $"Change is not longer editable, status: {((ChangeStatus)change.Status).ToString()}" });
            }

            var user = _userIdentity.GetIdentity();
            var onHireDate = DateTime.ParseExact(request.OnHireDate, user.DateFormat, null);
            var offHireDate = DateTime.ParseExact(request.OffHireDate, user.DateFormat, null);

            var changeHeader = change.ChangeOrderHeader;

            var original = new HeaderFields(header.OnHireDate, header.OffHireDate, changeHeader?.RateType, changeHeader?.JobAic, changeHeader?.ProjectCode, changeHeader?.Ponumber, changeHeader?.Poamount, changeHeader?.RentalDuration, changeHeader?.MinimumRental, changeHeader?.DaysInWeek, changeHeader?.WeeksInMonth, changeHeader?.PriceBase, changeHeader?.TargetCustomerAmount, changeHeader?.PriceAdjustment);

            int? rentalDuration = null;
            if (int.TryParse(request.RentalDuration, out int outRentalDuration)) rentalDuration = outRentalDuration;

            int? minimumRental = null;
            if (int.TryParse(request.MinimumRental, out int outMinimumRental)) minimumRental = outMinimumRental;

            int? daysInWeek = null;
            if (int.TryParse(request.DaysInWeek, out int outDaysInWeek)) daysInWeek = outDaysInWeek;

            int? weeksInMonth = null;
            if (int.TryParse(request.WeeksInMonth, out int outWeeksInMonth)) weeksInMonth = outWeeksInMonth;

            decimal? priceBase = null;
            if (decimal.TryParse(request.PriceBase, out decimal outPriceBase)) priceBase = outPriceBase;

            decimal? targetCustomerAmount = null;
            if (decimal.TryParse(request.TargetCustomerAmount, out decimal outTargetCustomerAmount)) targetCustomerAmount = outTargetCustomerAmount;

            decimal? priceAdjustment = null;
            if (decimal.TryParse(request.PriceAdjustment, out decimal outPriceAdjustment)) priceAdjustment = outPriceAdjustment;

            var posted = new HeaderFields(onHireDate, offHireDate, request.RateType, request.JobAic, request.ProjectCode, request.Ponumber, request.Poamount, rentalDuration, minimumRental, daysInWeek, weeksInMonth, priceBase, targetCustomerAmount, priceAdjustment);
            var isEqual = original.Equals(posted);

            // Check if OnHire or OffHire dates have changed
            bool datesChanged = header.OnHireDate != onHireDate || header.OffHireDate != offHireDate;

            if (changeHeader == null)
            {
                if (!isEqual)
                {
                    changeHeader = new ChangeOrderHeader()
                    {
                        HeaderId = headerId,
                        Status = (int)ChangeStatus.Pending,
                        ChangeOrderId = changeId
                    };

                    changeHeader.OnHireDate = onHireDate;
                    changeHeader.OffHireDate = offHireDate;
                    changeHeader.RateType = request.RateType;
                    changeHeader.JobAic = request.JobAic;
                    changeHeader.ProjectCode = request.ProjectCode;
                    changeHeader.Ponumber = request.Ponumber;
                    changeHeader.Poamount = request.Poamount;
                    changeHeader.RentalDuration = rentalDuration;
                    changeHeader.MinimumRental = minimumRental;
                    changeHeader.DaysInWeek = daysInWeek;
                    changeHeader.WeeksInMonth = weeksInMonth;
                    changeHeader.PriceBase = priceBase;
                    changeHeader.TargetCustomerAmount = targetCustomerAmount;
                    changeHeader.PriceAdjustment = priceAdjustment;

                    _repository.UpsertChangeOrderHeader(changeHeader, add: true);
                }
            }
            else
            {
                if (!isEqual)
                {
                    change.ChangeOrderHeader.OnHireDate = onHireDate;
                    change.ChangeOrderHeader.OffHireDate = offHireDate;
                    change.ChangeOrderHeader.RateType = request.RateType;
                    change.ChangeOrderHeader.JobAic = request.JobAic;
                    change.ChangeOrderHeader.ProjectCode = request.ProjectCode;
                    change.ChangeOrderHeader.Ponumber = request.Ponumber;
                    change.ChangeOrderHeader.Poamount = request.Poamount;
                    change.ChangeOrderHeader.RentalDuration = rentalDuration;
                    change.ChangeOrderHeader.MinimumRental = minimumRental;
                    change.ChangeOrderHeader.DaysInWeek = daysInWeek;
                    change.ChangeOrderHeader.WeeksInMonth = weeksInMonth;
                    change.ChangeOrderHeader.PriceBase = priceBase;
                    change.ChangeOrderHeader.TargetCustomerAmount = targetCustomerAmount;
                    change.ChangeOrderHeader.PriceAdjustment = priceAdjustment;

                    _repository.UpsertChangeOrderHeader(changeHeader, add: false);
                }
                else
                {
                    _repository.DeleteChangeOrderHeader(change.ChangeOrderHeader.ChangeOrderId);
                }
            }            
            
            if (datesChanged)
            {
                if (change?.ChangeOrderLines?.Any() == true)
                {
                    foreach (var changeOrderLine in change.ChangeOrderLines.Where(i => i.LineId == null))
                    {
                        changeOrderLine.OnHireDate = onHireDate;
                        changeOrderLine.OffHireDate = offHireDate;

                        _repository.UpsertChangeOrderLine(changeOrderLine);
                    }
                }
                
                if (header?.Lines != null && header.Lines.Any())
                {
                    foreach (var headerLine in header.Lines)
                    {
                        var newChangeLine = change?.ChangeOrderLines?.FirstOrDefault(i => i.LineId == headerLine.Id);

                        if (newChangeLine == null)
                        {
                            newChangeLine = new ChangeOrderLine()
                            {
                                ChangeOrderId = changeId,
                                LineId = headerLine.Id,
                                Delete = false
                            };
                        }

                        newChangeLine.DeliveryDate = headerLine.DeliveryDate;
                        newChangeLine.OnHireDate = onHireDate;
                        newChangeLine.OffHireDate = offHireDate;
                        newChangeLine.TerminationDate = headerLine.TerminationDate;
                        newChangeLine.CollectionDate = headerLine.CollectionDate;
                        newChangeLine.Warehouse = headerLine.Warehouse ?? string.Empty;
                        newChangeLine.GenericItemNumber = headerLine.GenericItemNumber;
                        newChangeLine.ItemNumber = headerLine.ItemNumber;
                        newChangeLine.Attributes = headerLine.Attributes;
                        newChangeLine.Quantity = headerLine.Quantity;
                        
                        _repository.UpsertChangeOrderLine(newChangeLine);
                    }
                }
            }

            return RedirectToAction("Details", new { headerId, changeId, tab });
        }        
        
        [HttpPost]
        public IActionResult EditLine(int headerId, int changeOrderId, string tab, [FromForm] EditLinesRequest request)
        {
            var header = _repository.GetHeaderWithChanges(headerId);
            var change = header?.ChangeOrders?.FirstOrDefault(i => i.Id == changeOrderId);

            if (change == null)
            {
                return Json(new { success = false, message = "Change order not found" });
            }

            if (!change.IsEditable)
            {
                return Json(new { success = false, message = $"Change is not longer editable, status: {((ChangeStatus)change.Status).ToString()}" });
            }

            var user = _userIdentity.GetIdentity();

            var lineDto = request;
            var delete = lineDto.Delete;
            int? lineId = !string.IsNullOrEmpty(lineDto.LineId) ? int.Parse(lineDto.LineId) : null;
            int? changeLineId = !string.IsNullOrEmpty(lineDto.ChangeId) ? int.Parse(lineDto.ChangeId) : null;
            var line = header.Lines.FirstOrDefault(i => i.Id == lineId);

            var generic = lineDto.GenericItemNumber;
            var quantity = float.Parse(lineDto.Quantity);
            var attributes = lineDto.Attributes?.Trim();
            var deliveryDate = !string.IsNullOrEmpty(lineDto.DeliveryDate) ? DateTime.ParseExact(lineDto.DeliveryDate, user.DateFormat, null) : (DateTime?)null;
            var onHireDate = DateTime.ParseExact(lineDto.OnHireDate, user.DateFormat, null);
            var offHireDate = DateTime.ParseExact(lineDto.OffHireDate, user.DateFormat, null);
            var terminationDate = !string.IsNullOrEmpty(lineDto.TerminationDate) ? DateTime.ParseExact(lineDto.TerminationDate, user.DateFormat, null) : (DateTime?)null;
            var collectionDate = !string.IsNullOrEmpty(lineDto.CollectionDate) ? DateTime.ParseExact(lineDto.CollectionDate, user.DateFormat, null) : (DateTime?)null;

            var original = new LineFields(
                line?.Id,
                line?.GenericItemNumber,
                line?.Quantity ?? 0,
                line?.Attributes,
                line?.DeliveryDate,
                line?.ValidFromDate ?? DateTime.UtcNow,
                line?.ValidToDate ?? DateTime.UtcNow,
                line?.TerminationDate,
                line?.CollectionDate);
            var posted = new LineFields(
                lineId,
                generic,
                quantity,
                attributes,
                deliveryDate,
                onHireDate,
                offHireDate,
                terminationDate,
                collectionDate);
            var isEqual = original.Equals(posted);

            var changeLine = change.ChangeOrderLines.FirstOrDefault(i => i.Id == changeLineId);

            // If we don't already have a change orderLine
            if (changeLine == null)
            {               
                // If its equals to the original line just ignore it, if its flagging as the main lines is to be deleted we need to do that
                if (isEqual && !delete)
                {
                    return RedirectToAction("Details", new { headerId, changeId = changeOrderId, tab });
                }

                // Make a new one if there are different fields
                changeLine = new ChangeOrderLine()
                {
                    LineId = lineId,
                    Status = (int)ChangeStatus.Pending,
                    ChangeOrderId = changeOrderId
                };
            }
            else // If a change line already exists
            {                
                // Also, if this is a change line not related to a line, delete it
                if (!changeLine.LineId.HasValue && delete)
                {
                    _repository.DeleteChangeOrderLine(changeLine.Id);
                    return RedirectToAction("Details", new { headerId, changeId = changeOrderId, tab });
                }

                // And its equal to the original, delete it
                if (isEqual && !delete)
                {
                    _repository.DeleteChangeOrderLine(changeLine.Id);
                    return RedirectToAction("Details", new { headerId, changeId = changeOrderId, tab });
                }
            }

            decimal.TryParse(request.Price, out decimal price);

            changeLine.GenericItemNumber = generic;
            changeLine.Attributes = attributes;
            changeLine.Quantity = quantity;
            changeLine.DeliveryDate = deliveryDate;
            changeLine.OnHireDate = onHireDate;
            changeLine.OffHireDate = offHireDate;
            changeLine.TerminationDate = terminationDate;
            changeLine.CollectionDate = collectionDate;
            changeLine.Delete = delete;
            changeLine.Price = price;           
            
            _repository.UpsertChangeOrderLine(changeLine);

            return RedirectToAction("Details", new { headerId, changeId = changeOrderId, tab });
        }

        [HttpPost]
        public IActionResult EditAddress(int headerId, int changeId, int addressType, string tab, [FromForm] EditAddressRequest request)
        {
            var header = _repository.GetHeaderWithChanges(headerId);
            var change = header?.ChangeOrders?.FirstOrDefault(i => i.Id == changeId);

            if (change == null)
            {
                return Json(new { success = false, message = "Change order not found" });
            }

            if (!change.IsEditable)
            {
                return Json(new { success = false, message = $"Change is not longer editable, status: {((ChangeStatus)change.Status).ToString()}" });
            }

            var isInvoiceAddress = addressType == (int)AddressType.Invoice;
            var existingAddress = isInvoiceAddress ? change.InvoiceAddress : change.SiteAddress;

            var changeOrderAddress = existingAddress ?? new ChangeOrderAddress
            {
                AddressType = addressType
            };

            changeOrderAddress.AddressName = request.AddressName;
            changeOrderAddress.Street = request.Street;
            changeOrderAddress.City = request.City;
            changeOrderAddress.StateOrProvince = request.StateOrProvince;
            changeOrderAddress.ZipOrPostalCode = request.ZipOrPostalCode;
            changeOrderAddress.Country = request.Country;
            changeOrderAddress.M3number = request.M3number;
            changeOrderAddress.SalesforceId = request.SalesforceId;

            var savedAddress = _repository.UpsertChangeOrderAddress(changeOrderAddress, existingAddress == null);

            if (isInvoiceAddress)
            {
                change.InvoiceAddressId = savedAddress.Id;
            }
            else
            {
                change.SiteAddressId = savedAddress.Id;
            }

            _repository.UpdateChangeOrder(change);

            return RedirectToAction("Details", new { headerId, changeId, tab });
        }

        [HttpPost]
        public IActionResult EditContact(int headerId, int changeId, int contactType, string tab, [FromForm] EditContactRequest request)
        {
            var header = _repository.GetHeaderWithChanges(headerId);
            var change = header?.ChangeOrders?.FirstOrDefault(i => i.Id == changeId);

            if (change == null)
            {
                return Json(new { success = false, message = "Change order not found" });
            }

            if (!change.IsEditable)
            {
                return Json(new { success = false, message = $"Change is not longer editable, status: {((ChangeStatus)change.Status).ToString()}" });
            }

            ChangeOrderContact existingContact = null;

            switch (contactType)
            {
                case (int)ContactType.Primary:
                    existingContact = change.PrimaryContact;
                    break;
                case (int)ContactType.Billing:
                    existingContact = change.BillingContact;
                    break;
                case (int)ContactType.ARM:
                    existingContact = change.Armcontact;
                    break;
                case (int)ContactType.Site:
                    existingContact = change.SiteContact;
                    break;
            }

            var changeOrderContact = existingContact ?? new ChangeOrderContact
            {
                ContactType = contactType
            };


            changeOrderContact.Title = request.Title;
            changeOrderContact.FirstName = request.FirstName;
            changeOrderContact.LastName = request.LastName;
            changeOrderContact.Phone = request.Phone;
            changeOrderContact.Mobile = request.Mobile;
            changeOrderContact.Email = request.Email;
            changeOrderContact.M3number = request.M3number;
            changeOrderContact.SalesforceId = request.SalesforceId;

            var savedContact = _repository.UpsertChangeOrderContact(changeOrderContact, existingContact == null);

            switch (contactType)
            {
                case (int)ContactType.Primary:
                    change.PrimaryContactId = savedContact.Id;
                    break;
                case (int)ContactType.Billing:
                    change.BillingContactId = savedContact.Id;
                    break;
                case (int)ContactType.ARM:
                    change.ArmcontactId = savedContact.Id;
                    break;
                case (int)ContactType.Site:
                    change.SiteContactId = savedContact.Id;
                    break;
            }

            _repository.UpdateChangeOrder(change);

            return RedirectToAction("Details", new { headerId, changeId, tab });
        }

        [HttpPost]
        public IActionResult SendOrDelete(int headerId, int changeId, string tab, [FromForm] SendOrDeleteRequest request)
        {
            var user = _userIdentity.GetIdentity();

            var header = _repository.GetHeaderWithChanges(headerId);
            if (header == null)
            {
                return new NotFoundResult();
            }

            var change = header?.ChangeOrders?.FirstOrDefault(i => i.Id == changeId);
            if (change.Status != (int)ChangeStatus.Pending)
            {
                return Json(new { success = false, message = $"Change cannot be sent for approval, status: {((ChangeStatus)change.Status).ToString()}" });
            }

            if (!request.Status)
            {
                if (header?.CurrentChangeOrder?.ChangeOrderLines != null)
                {
                    foreach (var line in header.CurrentChangeOrder.ChangeOrderLines)
                    {
                        _repository.DeleteChangeOrderLine(line.Id);
                    }
                }

                if (header?.CurrentChangeOrder?.ChangeOrderHeader != null)
                {
                    _repository.DeleteChangeOrderHeader(header.CurrentChangeOrder.ChangeOrderHeader.ChangeOrderId);
                }

                if (header?.CurrentChangeOrder != null)
                {
                    _repository.DeleteChangeOrder(header.CurrentChangeOrder.Id);
                }

                return RedirectToAction("List", new { headerId });
            }
            else
            {
                if (header?.CurrentChangeOrder?.ChangeOrderHeader != null)
                {
                    header.CurrentChangeOrder.ChangeOrderHeader.Status = (int)ChangeStatus.Requested;
                    _repository.UpsertChangeOrderHeader(header.CurrentChangeOrder.ChangeOrderHeader, add: false);
                }

                if (header?.CurrentChangeOrder?.ChangeOrderLines != null)
                {
                    foreach (var line in header.CurrentChangeOrder.ChangeOrderLines)
                    {
                        line.Status = (int)ChangeStatus.Requested;
                        _repository.UpsertChangeOrderLine(line);
                    }
                }

                if (header?.CurrentChangeOrder != null)
                {
                    header.CurrentChangeOrder.Status = (int)ChangeStatus.Requested;
                    _repository.UpdateChangeOrder(header.CurrentChangeOrder);
                }

                // TODO(ChangeOrders): Send notification to someone, maybe?
            }


            if (!string.IsNullOrWhiteSpace(request.Comment))
            {
                _repository.AddChangeOrderComment(changeId, request.Comment, user.LoginName);
            }

            return RedirectToAction("Details", new { headerId, changeId, tab });
        }        
        
        [HttpPost]
        public IActionResult ApproveOrReject(int headerId, int changeId, string tab, [FromForm] ApproveOrRejectRequest request)
        {
            var user = _userIdentity.GetIdentity();

            var header = _repository.GetHeaderWithChanges(headerId);
            if (header == null)
            {
                return new NotFoundResult();
            }

            var change = header?.ChangeOrders?.FirstOrDefault(i => i.Id == changeId);
            if (change.Status != (int)ChangeStatus.Requested)
            {
                return Json(new { success = false, message = $"Change cannot be approved or rejected, status: {((ChangeStatus)change.Status).ToString()}" });
            }

            if (header?.CurrentChangeOrder?.ChangeOrderHeader != null)
            {
                header.CurrentChangeOrder.ChangeOrderHeader.Status = (int)request.Status;
                _repository.UpsertChangeOrderHeader(header.CurrentChangeOrder.ChangeOrderHeader, add: false);
            }

            if (header?.CurrentChangeOrder?.ChangeOrderLines != null)
            {
                foreach (var line in header.CurrentChangeOrder.ChangeOrderLines)
                {
                    line.Status = (int)request.Status;
                    _repository.UpsertChangeOrderLine(line);
                }
            }

            if (change != null)
            {
                change.Status = (int)request.Status;

                if (change.Status == (int)ChangeStatus.Rejected)
                {
                    change.RejectedBy = user.LoginName;
                    change.RejectedDate = DateTime.UtcNow;
                }
                else if (change.Status == (int)ChangeStatus.Approved)
                {
                    change.ApprovedBy = user.LoginName;
                    change.ApprovedDate = DateTime.UtcNow;
                }

                _repository.UpdateChangeOrder(change);
            }

            if (!string.IsNullOrWhiteSpace(request.Comment))
            {
                _repository.AddChangeOrderComment(changeId, request.Comment, user.LoginName);
            }

            return RedirectToAction("Details", new { headerId, changeId, tab });
        }        
        
        [HttpPost]
        public IActionResult AddComment(int headerId, int changeId, string tab, [FromForm] AddCommentRequest request)
        {
            var user = _userIdentity.GetIdentity();

            var header = _repository.GetHeaderWithChanges(headerId);
            if (header == null)
            {
                return new NotFoundResult();
            }

            var change = header?.ChangeOrders?.FirstOrDefault(i => i.Id == changeId);

            if (!string.IsNullOrWhiteSpace(request.Comment))
            {
                _repository.AddChangeOrderComment(changeId, request.Comment, user.LoginName);
            }

            return RedirectToAction("Details", new { headerId, changeId, tab });
        }        
        
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var action = context.RouteData?.Values?.FirstOrDefault(i => i.Key == "action");

            ViewData["HideDivisions"] = true;
            if (action?.Value?.ToString()?.ToLower() != "list")
            {
                var value = context.ActionArguments?.FirstOrDefault(i => i.Key == "headerId");
                ViewData["BackUrl"] = "/ChangeOrder/List?headerId=" + value?.Value;
            }

            base.OnActionExecuting(context);
        }

        [HttpPost]
        public IActionResult SendDocument(int headerId, int changeId, DocumentRequest model, string tab = "document")
        {
            return RedirectToAction("Details", new { headerId, changeId, tab });
        }
    }
}
