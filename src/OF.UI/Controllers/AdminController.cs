using Infragistics.Web.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using OF.Common.Infrastructure.Features;
using OF.Data.Database;
using OF.UI.Database;
using OF.UI.Identity;
using OF.UI.Shared.Controllers;

namespace OF.UI.Controllers
{
    [Authorize]
    public class AdminController : CommonController
    {
        private readonly IDataRepository _repository;
        private IUserIdentity _userIdentity;
        public readonly ILogger _logger;
        public readonly IHttpContextAccessor _httpContextAccessor;
        private readonly FeatureProvider featureProvider;

        public AdminController(IDataRepository repository, IUserIdentity userIdentity, ILogger<AdminController> logger, IHttpContextAccessor httpContextAccessor, FeatureProvider featureProvider) : base(repository)
        {
            _repository = repository;
            _userIdentity = userIdentity;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            this.featureProvider = featureProvider;
        }

        void EnableSyncIO()
        {
            if (HttpContext != null)
            {
                var syncIOFeature = HttpContext.Features.Get<IHttpBodyControlFeature>();
                if (syncIOFeature != null)
                {
                    syncIOFeature.AllowSynchronousIO = true;
                }
            }
        }


        [GridDataSourceAction]
        public ActionResult GetUsers()
        {
            var id = _userIdentity.GetIdentity();

            if (id == null || !id.IsAdmin)
            {
                return new BadRequestResult();
            }

            var divs = String.Empty;

            EnableSyncIO();

            var users = _repository.GetUsers().OrderBy(x => x.LoginName).AsQueryable();
            return View(users);
        }


        public ActionResult UpdateUsers()
        {
            try
            {
                var id = _userIdentity.GetIdentity();

                if (id != null && id.IsAdmin)
                {

                    GridModel m = new GridModel();
                    List<Transaction<User>> transactions = m.LoadTransactions<User>(_httpContextAccessor.HttpContext.Request.Form["ig_transactions"]);

                    foreach (Transaction<User> t in transactions)
                    {
                        var pk = t.rowId;

                        switch (t.type)
                        {
                            case "newrow":
                                var newUser = new User();
                                newUser.LoginName = pk;
                                newUser.FullName = t.row.FullName;
                                newUser.Division = t.row.Division;
                                newUser.IsAdmin = t.row.IsAdmin;
                                newUser.DateFormat = t.row.DateFormat;
                                newUser.Language = t.row.Language;
                                if (featureProvider.RolesEnabled)
                                {
                                    newUser.Roles = t.row.Roles;
                                }
                                _repository.AddUser(newUser);
                                break;

                            case "deleterow":
                                var toDelete = _repository.GetUser(pk);
                                if (toDelete != null)
                                {
                                    _repository.DeleteUser(toDelete);
                                }
                                break;

                            case "row":
                                var toUpdate = _repository.GetUser(pk);
                                if (toUpdate != null)
                                {
                                    // We never update the name
                                    toUpdate.Division = t.row.Division;
                                    toUpdate.IsAdmin = t.row.IsAdmin;
                                    toUpdate.FullName = t.row.FullName;
                                    toUpdate.DateFormat = t.row.DateFormat;
                                    toUpdate.Language = t.row.Language;
                                    if (featureProvider.RolesEnabled)
                                    {
                                        toUpdate.Roles = t.row.Roles;
                                    }
                                    _repository.UpdateUser(toUpdate);
                                }
                                break;
                        }
                    }

                    Dictionary<string, bool> response = new Dictionary<string, bool>();
                    response.Add("Success", true);
                    JsonResult result = new JsonResult(response);
                    return result;
                }
                else
                {
                    Dictionary<string, bool> response = new Dictionary<string, bool>();
                    response.Add("Success", false);
                    JsonResult result = new JsonResult(response);
                    return result;
                }
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "UpdateUsers");
                Dictionary<string, bool> response = new Dictionary<string, bool>();
                response.Add("Success", false);
                JsonResult result = new JsonResult(response);
                return result;
            }
        }

        public IActionResult Users()
        {
            var id = _userIdentity.GetIdentity();

            if (id == null || !id.IsAdmin)
            {
                return new BadRequestResult();
            }

            return View();
        }
    }
}
