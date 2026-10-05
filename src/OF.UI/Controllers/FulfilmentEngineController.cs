using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OF.Common;
using OF.Common.Utils;
using OF.Data.Database;
using OF.UI.Database;
using OF.UI.Engine;
using OF.UI.Helpers;
using OF.UI.Identity;
using OF.UI.Models;

namespace OF.UI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class FulfilmentEngineController : ControllerBase
    {
        private readonly IDataRepository _repository;
        private readonly IUserIdentity _userIdentity;
        private readonly ILogger _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IFulfilmentEngine _engine;

        public FulfilmentEngineController(IDataRepository repository, IUserIdentity userIdentity, IFulfilmentEngine engine, ILogger<FulfilmentEngineController> logger, IHttpContextAccessor httpContextAccessor)
        {
            _repository = repository;
            _userIdentity = userIdentity;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _engine = engine;
        }

        [HttpPost]
        [Route("Satisfy")]
        public ActionResult<FulfilmentResponse> Satisfy([FromBody] FulfilmentRequest request)
        {
            var response = _engine.SatisfyLine(request);
            return new OkObjectResult(response);
        }

        [HttpPost]
        [Route("Stock")]
        public ActionResult<FulfilmentResponse> StockLevel([FromBody] StockRequest request)
        {
            var id = _userIdentity.GetIdentity();
            string[] divs = null;

            // Get the division cookies
            var divCookies = _httpContextAccessor.HttpContext.Request.Cookies["user_divisions"];
            if (!string.IsNullOrWhiteSpace(divCookies))
            {
                divs = divCookies.Split(","); // Look in the request cookie
            }

            if ((divs == null || divs.Length == 0) && id != null)
            {
                divs = id.Division.Split(","); // Look in the user profile
            }

            var response = _engine.GetStock(_userIdentity, request, divs);
            return new OkObjectResult(response);
        }

        [HttpPost]
        [Route("Reserve")]
        public ActionResult<ReserveResponse> Reserve([FromBody] ReserveRequest request)
        {
            var id = _userIdentity.GetIdentity();

            if (id == null)
            {
                return new BadRequestResult();
            }

            var response = _engine.Reserve(request);
            return new OkObjectResult(response);
        }

        [HttpPost]
        [Route("BulkAction")]
        public ActionResult<ReserveResponse> BulkAction(BulkActionRequest request)
        {
            var id = _userIdentity.GetIdentity();

            if (id == null)
            {
                return new BadRequestResult();
            }

            var response = _engine.BulkAction(request);
            return new OkObjectResult(response);
        }

        [HttpPost]
        [Route("CreateRingfence")]
        public ActionResult<CreateRingfenceResponse> CreateRingfence([FromBody] CreateRingfenceRequest request)
        {
            try
            {
                var id = _userIdentity.GetIdentity();

                if (id == null)
                {
                    return new BadRequestResult();
                }

                var existing = _repository.GetRingfences().Where(r => r.Title == request.Name).FirstOrDefault();
                if (existing != null)
                {
                    return ErrorResult<CreateRingfenceResponse>
                        ("A ringfence with this name already exists");
                }

                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return ErrorResult<CreateRingfenceResponse>
                        ("Ringfence name cannot be empty");
                }

                if (request.StartDate.Date < DateTime.Now.Date || request.EndDate.Date < DateTime.Now.Date)
                {
                    return ErrorResult<CreateRingfenceResponse>
                        ("Ringfence cannot be in the past");
                }

                if (request.StartDate.Date > request.EndDate.Date)
                {
                    return ErrorResult<CreateRingfenceResponse>
                        ("The ringfence start date cannot be after the end date");
                }

                if (string.IsNullOrWhiteSpace(request.Owner))
                {
                    return ErrorResult<CreateRingfenceResponse>
                        ("Ringfence owner cannot be empty");
                }

                if (string.IsNullOrWhiteSpace(request.Warehouse))
                {
                    return ErrorResult<CreateRingfenceResponse>
                        ("Ringfence warehouse cannot be empty");
                }

                if (string.IsNullOrWhiteSpace(request.Division))
                {
                    return ErrorResult<CreateRingfenceResponse>
                        ("Ringfence division cannot be empty");
                }

                if (!_userIdentity.GetIdentity().DivisionValidForUser(request.Division))
                {
                    return ErrorResult<CreateRingfenceResponse>
                        ("Ringfence division not valid for user");
                }

                var ringfence = new Ringfence(_userIdentity.GetIdentity().LoginName)
                {
                    Divisions = request.Division,
                    Title = request.Name,
                    FromDate = request.StartDate,
                    ToDate = request.EndDate,
                    Owner = request.Owner,
                    Warehouse = request.Warehouse
                };
                var result = _repository.CreateRingfence(_userIdentity, ringfence);

                return new OkObjectResult(new CreateRingfenceResponse()
                {
                    IsSuccess = true,
                    ErrorMessage = string.Empty
                });
            }
            catch (Exception ex)
            {
                return new OkObjectResult(new CreateRingfenceResponse()
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        [HttpPost]
        [Route("EditRingfence")]
        public ActionResult<EditRingfenceResponse> EditRingfence([FromBody] EditRingfenceRequest request)
        {
            try
            {
                var id = _userIdentity.GetIdentity();

                if (id == null)
                {
                    return new BadRequestResult();
                }

                var existing = _repository.GetRingfences().Where(r => r.Title == request.Name && r.Id != request.Id).FirstOrDefault();
                if (existing != null)
                {
                    return ErrorResult<EditRingfenceResponse>
                        ("A ringfence with this name already exists");
                }

                var ringfence = _repository.GetRingfence(request.Id);

                if (ringfence == null)
                {
                    return ErrorResult<EditRingfenceResponse>
                        ("Ringfence not found");
                }

                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return ErrorResult<EditRingfenceResponse>
                        ("Ringfence name cannot be empty");
                }

                if (request.StartDate.Date < DateTime.Now.Date || request.EndDate.Date < DateTime.Now.Date)
                {
                    return ErrorResult<EditRingfenceResponse>
                        ("Ringfence cannot be in the past");
                }

                if (request.StartDate.Date > request.EndDate.Date)
                {
                    return ErrorResult<EditRingfenceResponse>
                        ("The ringfence start date cannot be after the end date");
                }

                if (string.IsNullOrWhiteSpace(request.Warehouse))
                {
                    return ErrorResult<EditRingfenceResponse>
                        ("Ringfence warehouse cannot be empty");
                }

                if (string.IsNullOrWhiteSpace(request.Division))
                {
                    return ErrorResult<EditRingfenceResponse>
                        ("Ringfence division cannot be empty");
                }

                if (!_userIdentity.GetIdentity().DivisionValidForUser(request.Division))
                {
                    return ErrorResult<EditRingfenceResponse>
                        ("Ringfence division not valid for user");
                }

                ringfence.Title = request.Name;
                ringfence.FromDate = request.StartDate;
                ringfence.ToDate = request.EndDate;
                ringfence.Divisions = request.Division;
                ringfence.Owner = request.Owner;
                ringfence.Warehouse = request.Warehouse;

                var result = _repository.UpdateRingfence(_userIdentity, ringfence);

                return new OkObjectResult(new EditRingfenceResponse()
                {
                    IsSuccess = true,
                    ErrorMessage = String.Empty
                });
            }
            catch (Exception ex)
            {
                return new OkObjectResult(new EditRingfenceResponse()
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        [HttpDelete]
        [Route("DeleteRingfence")]
        public ActionResult<DeleteRingfenceResponse> DeleteRingfence([FromBody] DeleteRingfenceRequest request)
        {
            try
            {
                var id = _userIdentity.GetIdentity();

                if (id == null)
                {
                    return new BadRequestResult();
                }

                var existing = _repository.GetRingfences().Where(r => r.Id == request.Id).FirstOrDefault();
                if (existing != null)
                {
                    _repository.DeleteRingfence(existing);
                }

                return new OkObjectResult(new CreateRingfenceResponse()
                {
                    IsSuccess = true,
                    ErrorMessage = String.Empty
                });
            }
            catch (Exception ex)
            {
                return new OkObjectResult(new CreateRingfenceResponse()
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        [HttpGet]
        [Route("GetRingfences")]
        public ActionResult<GetRingfencesResponse> GetRingfences()
        {
            try
            {
                var id = _userIdentity.GetIdentity();

                if (id == null)
                {
                    return new BadRequestResult();
                }

                var ringfences = _repository.GetRingfences();
                var targetDivs = _userIdentity.GetIdentity().Division.Split(",");
                var items = new List<OF.Data.Database.Ringfence>();

                foreach (var ringfence in ringfences)
                {
                    if (ringfence.Divisions.Split(",").Intersect(targetDivs).Any())
                    {
                        items.Add(ringfence);
                    }
                }

                return new OkObjectResult(new GetRingfencesResponse()
                {
                    IsSuccess = true,
                    ErrorMessage = String.Empty,
                    Ringfences = items.ToArray()
                });
            }
            catch (Exception ex)
            {
                return new OkObjectResult(new GetRingfencesResponse()
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        [HttpPost]
        [Route("Ringfence")]
        public ActionResult<RingfenceResponse> Ringfence([FromBody] RingfenceRequest request)
        {
            try
            {
                var id = _userIdentity.GetIdentity();

                if (id == null)
                {
                    return new BadRequestResult();
                }

                var ringfence = _repository.GetRingfences().Where(r => r.Id == request.RingfenceId).FirstOrDefault();
                if (ringfence == null)
                {
                    return new OkObjectResult(new RingfenceResponse()
                    {
                        IsSuccess = false,
                        ErrorMessage = "Ringfence not found"
                    });
                }

                foreach (var assetId in request.AssetIds)
                {
                    _repository.AddAssetToRingfence(_userIdentity, ringfence, assetId);
                }

                return new OkObjectResult(new CreateRingfenceResponse()
                {
                    IsSuccess = true,
                    ErrorMessage = String.Empty
                });
            }
            catch (Exception ex)
            {
                return new OkObjectResult(new CreateRingfenceResponse()
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        [HttpPost]
        [Route("RingfenceOverlap")]
        public async Task<ActionResult> RingfenceOverlap([FromBody] RingfenceRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var id = _userIdentity.GetIdentity();

                if (id == null)
                {
                    return new BadRequestResult();
                }

                var ringfence = await _repository.GetRingfences().FirstOrDefaultAsync(r => r.Id == request.RingfenceId, cancellationToken);
                if (ringfence == null)
                {
                    return ErrorResult<RingfenceOverlap>("Ringfence not found");
                }

                return new OkObjectResult(new RingfenceOverlap
                {
                    OverlappingRingfences = await _repository.GetOverlappingRingfenceDetailsAsync(
                        ringfence.Id,
                        request.AssetIds.ToList(),
                        ringfence.FromDate,
                        ringfence.ToDate,
                        cancellationToken
                    ),
                    IsSuccess = true,
                    ErrorMessage = string.Empty
                });

            }
            catch (Exception ex)
            {
                return new OkObjectResult(new RingfenceOverlap
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                });
            }
        }



        [HttpGet]
        [Route("GetWarehousesByDivision/{division}")]
        public IEnumerable<WarehouseItem> GetWarehousesByDivision(string division)
        {
            if (_userIdentity.GetIdentity().DivisionValidForUser(division))
            {
                IEnumerable<WarehouseItem> warehouseItems = _repository
                    .GetWarehouses(new[] { division })
                    .Where(w => !w.WarehouseCode.IsExcludedWarehouse());
                return warehouseItems;
            }

            return Enumerable.Empty<WarehouseItem>();
        }

        [HttpGet]
        [Route("GetWarehouses")]
        public ActionResult GetWarehouses()
        {
            try
            {
                var id = _userIdentity.GetIdentity();
                 
                if (id == null)
                {
                    return new BadRequestResult();
                }

                string[] divisions = id.GetDivisionsForUser(_httpContextAccessor.HttpContext.Request.Cookies);

                var items = _repository.GetWarehouses(divisions);
                items = items.Where(w => !w.WarehouseCode.IsExcludedWarehouse()).ToArray();

                return new OkObjectResult(new 
                {
                    IsSuccess = true,
                    ErrorMessage = String.Empty,
                    Warehouses = items.ToArray()
                });
            }
            catch (Exception ex)
            {
                return new OkObjectResult(new 
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        [HttpGet]
        [Route("GetRingfenceAssets")]
        public ActionResult<GetRingfenceAssetsResponse> GetRingfenceAssets(int ringfenceId)
        {
            try
            {
                var id = _userIdentity.GetIdentity();

                if (id == null)
                {
                    return new BadRequestResult();
                }

                var items = _repository.GetAssetsForRingfence(ringfenceId);

                return new OkObjectResult(new GetRingfenceAssetsResponse()
                {
                    IsSuccess = true,
                    ErrorMessage = String.Empty,
                    Assets = items.ToArray()
                });
            }
            catch (Exception ex)
            {
                return new OkObjectResult(new GetRingfenceAssetsResponse()
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        [HttpDelete]
        [Route("RemoveRingfenceAsset")]
        public ActionResult<RemoveRingfenceAssetResponse> RemoveRingfenceAsset([FromBody] RemoveRingfenceAssetRequest request)
        {
            try
            {
                var id = _userIdentity.GetIdentity();

                if (id == null)
                {
                    return new BadRequestResult();
                }

                var existing = _repository.GetRingfenceItems(request.RingfenceId).Where(r => r.AssetId == request.AssetId).FirstOrDefault();
                if (existing != null)
                {
                    _repository.DeleteRingfenceItem(existing);
                }

                return new OkObjectResult(new RemoveRingfenceAssetResponse()
                {
                    IsSuccess = true,
                    ErrorMessage = String.Empty
                });
            }
            catch (Exception ex)
            {
                return new OkObjectResult(new RemoveRingfenceAssetResponse()
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        [HttpDelete]
        [Route("DeleteLine")]
        public ActionResult<DeleteLineResponse> DeleteLine([FromBody] DeleteLineRequest request)
        {
            try
            {
                var id = _userIdentity.GetIdentity();

                if (id == null)
                {
                    return new BadRequestResult();
                }

                var existing = _repository.GetLine(request.LineId);
                if (existing != null && existing.HeaderId.HasValue)
                {
                    var header = _repository.GetHeader(existing.HeaderId.Value);

                    var otherLines = _repository.GetLines(existing.HeaderId.Value).Where(l => l.Id != request.LineId).ToArray();
                    if (otherLines.Length == 0)
                    {
                        return new OkObjectResult(new DeleteLineResponse()
                        {
                            LineId = request.LineId,
                            IsSuccess = false,
                            ErrorMessage = "You cannot delete the last line on an order"
                        });
                    }

                    _repository.DeleteLine(_userIdentity, existing);

                    // Recalculate status
                    _engine.RecalculateStatusForHeader(header, id.LoginName);
                }
                else
                {
                    return new OkObjectResult(new DeleteLineResponse()
                    {
                        IsActivatable = false,
                        LineId = request.LineId,
                        IsSuccess = false,
                        ErrorMessage = "Line not found"
                    });
                }

                var newHeader = _repository.GetHeaderWithLines(existing.HeaderId.Value);

                return new OkObjectResult(new DeleteLineResponse()
                {
                    IsActivatable = newHeader.IsActivatable(),
                    LineId = request.LineId,
                    IsSuccess = true,
                    ErrorMessage = String.Empty,
                    HeaderStatus = (newHeader?.FulfilmentStatus ?? (int)FulfilmentStatus.Unfulfilled)
                });
            }
            catch (Exception ex)
            {
                return new OkObjectResult(new DeleteLineResponse()
                {
                    IsActivatable = false,
                    LineId = request.LineId,
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        public string CalcNextAgreementLineIndex(Line line)
        {
            var children = _repository.GetAllLines(line!.HeaderId!.Value).Where(l => l.HeaderId == line.HeaderId && l.AgreementLineNumber.StartsWith(line.AgreementLineNumber+".")).ToArray();
            return _engine.CalcNextAgreementLineIndex(line, children);
        }

        [HttpPut]
        [Route("AddLine")]
        public ActionResult<AddLineResponse> AddLine(AddLineRequest request)
        {
            try
            {
                var id = _userIdentity.GetIdentity();

                if (id == null)
                {
                    return new BadRequestResult();
                }

                var refLine = _repository.GetLine(request.LineId);

                var line = new Line();
                line.HeaderId = refLine.HeaderId;
                line.OrderLineNumber = refLine.OrderLineNumber;
                line.AgreementLineType = refLine.AgreementLineType;
                line.Status = refLine.Status;
                line.ValidFromDate = refLine.ValidFromDate;
                line.ValidToDate = refLine.ValidToDate;
                line.DeliveryDate = refLine.DeliveryDate;
                line.TerminationDate = refLine.TerminationDate;
                line.CollectionDate = refLine.CollectionDate;
                line.Division = refLine.Division;
                line.Warehouse = refLine.Warehouse;
                line.Facility = refLine.Facility;

                if (request.ByGeneric)
                {
                    var generic = _repository.GetGeneric(request.GenericId);
                    line.GenericItemNumber = generic.GenericCode;
                    line.ItemNumber = generic.GenericCode;
                }
                else
                {
                    line.ItemNumber = request.ItemNumber;
                }

                line.Quantity = request.Quantity;
                line.FulfilmentStatus = (int)FulfilmentStatus.Unfulfilled;
                line.QuantityFulfilled = 0;
                line.RequiresFulfilment = true;
                line.OrderSource = Constants.IPG.OrderSource;
                line.PackageGroupNumber = refLine.PackageGroupNumber;
                line.AgreementLineNumber = CalcNextAgreementLineIndex(refLine);
                line.AgreementLineIndex = refLine.AgreementLineIndex;
                line.OrderLineIndex = refLine.OrderLineIndex;
                line.QuoteLineIndex = refLine.QuoteLineIndex;
                line.Attributes = request.Attributes;
                line.OrderSource = refLine.OrderSource;
                line.AgreementNumbersOnly = refLine.AgreementNumbersOnly;
                line.QuotePublicId = refLine.QuotePublicId;
                line.QuotePublicIdNumbersOnly = refLine.QuotePublicIdNumbersOnly;
                line.NumberOfShifts = refLine.NumberOfShifts;
                line.RateType = refLine.RateType;
                line.ChangeSequence = refLine.ChangeSequence;

                _repository.AddLine(_userIdentity, line);

                // Recalculate status
                _engine.RecalculateStatusForLineAndHeader(line, _userIdentity.GetIdentity().LoginName);

                Header? newHeader = null;

                if (refLine.HeaderId != null)
                {
                    newHeader = _repository.GetHeader(refLine.HeaderId.Value);
                }

                return new AddLineResponse()
                {
                    LineId = line.Id,
                    RootAgreementLineNumber = refLine.AgreementLineNumber!,
                    AgreementLineNumber = line.AgreementLineNumber,
                    ItemNumber = line.ItemNumber,
                    Quantity = (int)line.Quantity,
                    IsSuccess = true,
                    HeaderStatus = (newHeader?.FulfilmentStatus ?? (int)FulfilmentStatus.Unfulfilled)
                };
            }
            catch (Exception ex)
            {
                return new AddLineResponse()
                {
                    IsSuccess = false,
                    ErrorMessage = ex.GetBaseException().Message
                };
            }
        }


        private static ActionResult ErrorResult<T>(string message)
            where T : IResponse, new()
        {
            return new OkObjectResult(new T()
            {
                IsSuccess = false,
                ErrorMessage = message
            });
        }

    }
}
