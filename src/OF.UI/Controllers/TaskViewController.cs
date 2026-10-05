using Infragistics.Web.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Text.Json;
using OF.Data.Database;
using OF.UI.Database;
using OF.UI.Grid;
using OF.UI.Identity;
using OF.UI.Models;
using OF.UI.Models.ViewPersistence;
using OF.UI.Shared.Controllers;

namespace OF.UI.Controllers
{
    [Authorize]
    public class TaskViewController : CommonController
    {
        private readonly IDataRepository _repository;
        private readonly IUserIdentity _userIdentity;
        private readonly IGridFactory _gridFactory;
        private readonly IRemoteHandlers _remoteHandlers;
        private readonly ILogger _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public TaskViewController(IDataRepository repository, IUserIdentity userIdentity, IGridFactory gridFactory, IRemoteHandlers remoteHandlers, ILogger<TaskViewController> logger, IHttpContextAccessor httpContextAccessor) : base(repository)
        {
            _repository = repository;
            _userIdentity = userIdentity;
            _logger = logger;
            _gridFactory = gridFactory;
            _remoteHandlers = remoteHandlers;
            _httpContextAccessor = httpContextAccessor;
        }

        void GetDatesFromCookies(out DateTime startDate, out DateTime endDate)
        {
            startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1); // First day of the month

            // Get the date range filter
            var dateCookies = _httpContextAccessor.HttpContext.Request.Cookies["date_range"];
            if (dateCookies != null)
            {
                if (DateTime.TryParse(dateCookies, null, System.Globalization.DateTimeStyles.None, out startDate))
                {
                    startDate = DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
                }
            }

            endDate = startDate.AddMonths(3).AddDays(-1); // Last day of three months time
        }

        void EnableSyncIO()
        {
            var syncIOFeature = HttpContext.Features.Get<IHttpBodyControlFeature>();
            if (syncIOFeature != null)
            {
                syncIOFeature.AllowSynchronousIO = true;
            }
        }

        IQueryable<T> ApplyFiltersAndSorting<T>(GridModel model, IQueryable<T> data) where T : IGridData
        {
            var id = _userIdentity.GetIdentity();
            if (id == null)
            {
                return data;
            }

            _logger.LogInformation("Got user identity");

            var divs = String.Empty;

            // Get the division cookies
            var divCookies = _httpContextAccessor.HttpContext.Request.Cookies["user_divisions"];
            if (!string.IsNullOrWhiteSpace(divCookies))
            {
                divs = divCookies; // Look in the request cookie

                _logger.LogInformation("Getting divisions from cookie");
            }

            if (string.IsNullOrEmpty(divs) && id != null)
            {
                divs = id.Division; // Look in the user profile

                _logger.LogInformation("Getting divisions from user");
            }

            if (!string.IsNullOrEmpty(divs))
            {
                var filterDivs = divs.Replace(" ", "").Split(',');
                data = data.Where(x => filterDivs.Contains(x.Division));

                _logger.LogInformation("Got divisions [{Divisions}]", divs);
            }
            else
            {
                _logger.LogInformation("Not filtering by divisions");
            }

            // Apply filtering
            var filterExprs = Request.Query.Keys.Where(x => x.Contains("filter"));

            _logger.LogInformation("Applying [{FilterExpressionsCount}] filter expressions [{FilterExpressions}]", filterExprs.Count(), string.Join(',', filterExprs));

            if (filterExprs.Count() != 0)
            {
                data = (IQueryable<T>)_remoteHandlers.ApplyFiltering<T>(Request.Query, data, model);

                _logger.LogInformation("Applied filter expressions to queryable");
            }

            // Apply sorting
            var sortExprs = Request.Query.Keys.Where(x => x.Contains("sort"));

            _logger.LogInformation("Applying [{SortExpressionsCount}] sort expressions [{SortExpressions}]", sortExprs.Count(), string.Join(',', sortExprs));

            if (sortExprs.Count() != 0)
            {
                data = (IQueryable<T>)_remoteHandlers.ApplySorting(Request.Query, data, model);

                _logger.LogInformation("Applied sorting expressions to queryable");
            }

            return data;
        }

        [GridDataSourceAction]
        public ActionResult GetTasks()
        {
            _logger.LogInformation("Beginning to get Agreements");

            // Require a user
            if (_userIdentity.GetIdentity() == null)
            {
                return Unauthorized();
            }

            _logger.LogInformation("User is valid");

            EnableSyncIO();

            var gridModel = _gridFactory.CreateTaskModel(null, null, false, null);

            _logger.LogInformation("Created grid model");

            var showFulfilled = _httpContextAccessor.HttpContext.Request.Cookies["showFulfilled"] == "true";
            var agreements = _repository.GetAgreements(showFulfilled);

            _logger.LogInformation("Got queryable agreements with showFulfilled [{ShowFulfilled}]", showFulfilled);

            var data = ApplyFiltersAndSorting(gridModel, agreements);

            _logger.LogInformation("Applied filters and sorting");

            return View(data);
        }

        [GridDataSourceAction]
        public ActionResult GetAssets()
        {
            _logger.LogInformation("Beginning to get Assets");

            // Require a user
            if (_userIdentity.GetIdentity() == null)
            {
                return Unauthorized();
            }

            _logger.LogInformation("User is valid");

            EnableSyncIO();

            var gridModel = _gridFactory.CreateAssetModel(null, null, false, null);

            _logger.LogInformation("Created grid model");

            var assets = _repository.GetAssetItems();


            _logger.LogInformation("Got queryable assets");

            var data = ApplyFiltersAndSorting(gridModel, assets);

            _logger.LogInformation("Applied filters and sorting");

            return View(data);
        }

        public IActionResult Index(int viewid, string? textFilter, int assetView, int cloneid)
        {
            View? view = null;
            PersistedView? persistedView = null;

            // Require a user
            if (_userIdentity.GetIdentity() == null)
            {
                return Unauthorized();
            }

            if (viewid > 0 || cloneid > 0)
            {
                view = _repository.GetView(viewid > 0 ? viewid : cloneid);

                if (cloneid > 0)
                {
                    view.Id = 0;
                    view.Name = "Copy of " + view.Name;
                }

                if (view != null)
                {
                    persistedView = JsonConvert.DeserializeObject<PersistedView>(view.ViewJson);
                }
                else
                {
                    return new NotFoundResult();
                }
            }

            DateTime startDate;
            DateTime endDate;

            GetDatesFromCookies(out startDate, out endDate);

            var assetViewResolved = view != null ? view.AssetView : (assetView==1);
            var isGantt = view == null ? false : view.GanttView;
            
            var parameters = new Dictionary<string, string>(); // Future use
            
            return View(new GridContainer()
            {
                Grid = assetViewResolved ? _gridFactory.CreateAssetModel(persistedView, textFilter, isGantt, parameters) : _gridFactory.CreateTaskModel(persistedView, textFilter, isGantt, parameters),
                View = view,
                StartDate = startDate,
                EndDate = endDate,
                IsAssetView = assetViewResolved,
                IsGanttView = isGantt
            });
        }
    }
}
