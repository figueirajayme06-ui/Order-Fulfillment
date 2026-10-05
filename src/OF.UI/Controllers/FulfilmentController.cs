using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using OF.Common.Infrastructure.IPG.Pricing;
using OF.Common.Infrastructure.IPG.Pricing.Models;
using OF.Common.Infrastructure.MDP.Models;
using OF.Common.Utils;
using OF.UI.Database;
using OF.UI.Identity;
using OF.UI.Shared.Controllers;
using OF.UI.ViewModels.Asset;
using System.Data;

namespace OF.UI.Controllers
{
    [Authorize]
    public class FulfilmentController : CommonController
    {
        private readonly IDataRepository _repository;
        private readonly IUserIdentity _userIdentity;
        private readonly IConfiguration _configuration;
        private readonly IPricingApi _pricingApi;

        public FulfilmentController(IDataRepository repository, IUserIdentity userIdentity, IConfiguration configuration, IPricingApi pricingApi) : base(repository)
        {
            _repository = repository;
            _userIdentity = userIdentity;
            _configuration = configuration;
            _pricingApi = pricingApi;
        }

        public IActionResult Index(int headerId)
        {
            // Require a user
            if (_userIdentity.GetIdentity() == null)
            {
                return Unauthorized();
            }

            var header = _repository.GetHeader(headerId);
            if (header == null)
            {
                return new NotFoundResult();
            }

            var services = _repository.GetServices().ToArray();

            var allLines = _repository
            .GetLines(headerId)
            .ToArray(); // materialise to make the next bit easier on LINQ

            
            var lines = allLines
            .Where(line => !line.ItemNumber.IsExcludedLine() && !services.Any(s => s.ProductCode == line.ItemNumber));

            var minDelivery = lines.Any() ? lines.Min(l => l.DeliveryDate) : null;
            var minValid = lines.Any() ? lines.Min(l => l.ValidFromDate) : DateTime.Now;

            var reservationText = new Dictionary<int, string>(); 
            var reservationWarehouseText = new Dictionary<int, string>();
            var reservationDeletedAssetWarnings = new Dictionary<int, string>();
            var reservations = _repository.GetReservationsForHeader(headerId).ToList();
            var reservedAssetIds = reservations
                .Where(r => !string.IsNullOrEmpty(r.AssetId) && r.AssetId != "DEPOTFULFIL")
                .Select(r => r.AssetId)
                .Distinct()
                .ToList();
            var deletedStatuses = new HashSet<string> { "RemovedStock", "Scrap", "Sold" };
            var deletedAssets = new Dictionary<string, string>();
            
            foreach (var assetId in reservedAssetIds)
            {
                var asset = _repository.GetAsset(assetId);
                if (asset != null && !string.IsNullOrEmpty(asset.Status) && deletedStatuses.Contains(asset.Status))
                {
                    deletedAssets[assetId] = asset.Status;
                }
            }

            foreach (var l in lines)
            {
                var text = string.Join(",", reservations.Where(r => r.LineId == l.Id).Select(r => string.IsNullOrEmpty(r.AssetId) ? r.ItemNumber : r.AssetId).Distinct());
                var warehouses = string.Join(",", reservations.Where(r => r.LineId == l.Id).Select(r => r.Warehouse).Distinct());
                reservationText[l.Id] = text;
                reservationWarehouseText[l.Id] = warehouses;

                // Check if any reservations for this line reference a deleted asset
                var deletedForLine = reservations
                    .Where(r => r.LineId == l.Id && !string.IsNullOrEmpty(r.AssetId) && deletedAssets.ContainsKey(r.AssetId))
                    .Select(r => r.AssetId + " (" + deletedAssets[r.AssetId] + ")")
                    .ToList();
                reservationDeletedAssetWarnings[l.Id] = deletedForLine.Any() ? string.Join(", ", deletedForLine) : string.Empty;
            }

            // Build the model
            var model = new FulfilmentViewModel
            {
                Header = header,
                Lines = lines.ToArray(),
                PackageGroups = lines.Select(l => string.IsNullOrEmpty(l.PackageGroupNumber) ? "1" : l.PackageGroupNumber).Distinct().OrderBy(s => s).ToArray(),
                DateFrom = minDelivery > minValid ? minValid : minDelivery,
                DateTo = lines.Any() ? lines?.Max(l => l.ValidToDate) : null,
                Services = services,
                ReservationText = reservationText,
                ReservationWarehouseText = reservationWarehouseText,
                ReservationDeletedAssetWarnings = reservationDeletedAssetWarnings
            };

            return View(model);
        }

        [Route("api/[controller]/FulfilmentInfo")]
        [HttpPost]
        public ActionResult<FulfilmentInfo[]> GetFulfilmentInfo(string genericCode, DateTime startDate, DateTime endDate, string division, [FromBody] string[] attributes)
        {
            IList<FulfilmentInfo> fulfilmentInfoList = new List<FulfilmentInfo>();

            try
            {
                var fulfilmentConnectionString = _configuration["SqlConnection"];
                using (var conn = new SqlConnection(fulfilmentConnectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand("CPQAvailabilitySummary"))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@GenericCode", genericCode);
                        cmd.Parameters.AddWithValue("@Attributes", String.Join(';', attributes));
                        cmd.Parameters.AddWithValue("@StartDate", startDate);
                        cmd.Parameters.AddWithValue("@EndDate", endDate);
                        cmd.Parameters.AddWithValue("@Division", division);
                        cmd.Connection = conn;
                        using (var rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                var fulfilmentInfo = new FulfilmentInfo();
                                fulfilmentInfo.WarehouseCode = rdr.GetString(0);
                                fulfilmentInfo.Warehouse = rdr.GetString(1);
                                fulfilmentInfo.ItemNumber = rdr.GetString(2);
                                fulfilmentInfo.DescriptionIntl = rdr.GetString(3);
                                fulfilmentInfo.Available = rdr.GetInt32(4);
                                fulfilmentInfo.Count = rdr.GetInt32(5);
                                fulfilmentInfoList.Add(fulfilmentInfo);
                            }
                        }
                    }
                }

                return Ok(fulfilmentInfoList.ToArray());
            }
            catch (Exception ex)
            {
                // Log the error (optional logging)
                // _logger.LogError(ex, "Error occurred while querying Salesforce");

                // Return an error response
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [Route("api/[controller]/Price")]
        [HttpPost]
        public async Task<ActionResult<CalculationResult>> GetPrice(string json)
        {
            var pricingQuote = JsonConvert.DeserializeObject<PricingQuote>(json)!;
            var request = new CalculationRequest
            {
                Id = Guid.NewGuid().ToString(),
                Quote = pricingQuote
            };

            var result = await _pricingApi.CalculateAsync(request);
            return Ok(result);
        }
    }
}
