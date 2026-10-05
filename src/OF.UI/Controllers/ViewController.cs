using OF.UI.Database;
using OF.UI.Identity;
using OF.UI.Models.ViewPersistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using OF.Data.Database;

namespace OF.UI.Controllers
{
    [Route("/api/[controller]")]
    [ApiController]
    [Authorize]
    public class ViewController : ControllerBase
    {
        private readonly IDataRepository _repository;
        private readonly IUserIdentity _identity;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ViewController(IDataRepository repository, IUserIdentity identity, IHttpContextAccessor httpContextAccessor)
        { 
            _repository = repository; 
            _identity = identity;
            _httpContextAccessor = httpContextAccessor;
        }

        [HttpDelete]
        public ActionResult DeleteView(int id)
        {
            var user = _identity.GetIdentity();

            if (user != null)
            {
                if (id > 0)
                {
                    var existing = _repository.GetView(id);
                    if (existing != null)
                    {
                        if (user.IsAdmin || user.LoginName == existing.Owner)
                        {
                            _repository.DeleteView(existing);
                            return new OkResult();
                        }
                    }
                }
            }

            return new BadRequestResult();
        }

        [HttpPut]
        public ActionResult SaveView(int id, string name, int forEveryone, bool ganttView, bool assetView, [FromBody] Models.ViewPersistence.PersistedView view)
        {
            var user = _identity.GetIdentity();

            if (user != null)
            {
                if (forEveryone>0 && !user.IsAdmin)
                {
                    return new BadRequestResult();
                }

                if (id > 0)
                {
                    var existing = _repository.GetView(id);
                    if (existing != null)
                    {
                        existing.Owner = user.LoginName;
                        existing.Name = name;
                        existing.ForEveryone = forEveryone;
                        existing.GanttView = ganttView;
                        existing.AssetView = assetView;
                        existing.ViewJson = JsonConvert.SerializeObject(view);

                        if (forEveryone == 2)
                        {
                            existing.ForDivisions = user.Division;
                        }
                        else
                        {
                            existing.ForDivisions = null;
                        }

                        _repository.UpdateView(existing);
                        return new OkObjectResult(existing);
                    }
                    else
                    {
                        return new NotFoundResult();
                    }
                }
                else
                {
                    // New view
                    var newView = new View();
                    newView.Name = name;
                    newView.ForEveryone = forEveryone;
                    newView.Owner = user.LoginName;
                    newView.GanttView = ganttView;
                    newView.AssetView = assetView;
                    newView.ViewJson = JsonConvert.SerializeObject(view);

                    if (forEveryone == 2)
                    {
                        newView.ForDivisions = user.Division;
                    }
                    else
                    {
                        newView.ForDivisions = null;
                    }

                    _repository.AddView(newView);
                    return new OkObjectResult(newView);
                }
            }
            else
            {
                return new BadRequestResult();
            }
        }

        string GetViewType(int viewType)
        {
            switch (viewType)
            {
                case 0:
                    return "Personal Views";
                case 1:
                    return "Global Views";
                case 2:
                    return "Divisional Views";
                default:
                    return "Unknown";
            }
        }

        [HttpGet]
        public ActionResult<IEnumerable<ViewUIEntry>> GetAll()
        {
            var user = _identity.GetIdentity();
            IList<View> views;
            var divs = String.Empty;

            // Get the division cookies
            var divCookies = _httpContextAccessor.HttpContext.Request.Cookies["user_divisions"];
            if (!string.IsNullOrWhiteSpace(divCookies))
            {
                divs = divCookies; // Look in the request cookie
            }

            if (string.IsNullOrEmpty(divs) && user != null)
            {
                divs = user.Division; // Look in the user profile
            }

            if (user == null)
            {
                views = _repository.GetViews()
                    .Where(b => b.ForEveryone == 1).ToList();
            }
            else
            {
                var source = _repository.GetViews().Where(v => (v.ForEveryone > 0) || v.Owner == user.LoginName).ToList();

                views = new List<View>();

                var defaultView = _repository.GetViews().FirstOrDefault(v => v.Id == 1);
                if (defaultView != null)
                {
                    views.Add(defaultView);
                }

                if (!String.IsNullOrEmpty(divs))
                {
                    var targetDivs = divs.Split(',');

                    foreach (var v in source)
                    {
                        if (v.Id != 1)
                        {
                            if (v.ForEveryone == 2)
                            {
                                if (!String.IsNullOrEmpty(v.ForDivisions))
                                {
                                    var viewDivs = v.ForDivisions.Split(',');
                                    if (viewDivs.Intersect(targetDivs).Any())
                                    {
                                        views.Add(v);
                                    }
                                }
                                else
                                {
                                    views.Add(v);
                                }
                            }
                            else
                            {
                                views.Add(v);
                            }
                        }
                    }
                }
            }
            return new OkObjectResult(views.OrderBy(v => v.Name).Select(v => new ViewUIEntry { Id = v.Id, Name = v.Name, DisplaySection = GetViewType(v.ForEveryone) }));
        }

    }
}
