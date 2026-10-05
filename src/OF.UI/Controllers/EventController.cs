using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OF.UI.Database;
using OF.UI.Identity;
using OF.UI.Models;

namespace OF.UI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class EventController : ControllerBase
    {
        private readonly IDataRepository _repository;
        private readonly IUserIdentity _userIdentity;
        private readonly ILogger _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public EventController(IDataRepository repository, IUserIdentity userIdentity, ILogger<EventController> logger, IHttpContextAccessor httpContextAccessor)
        {
            _repository = repository;
            _userIdentity = userIdentity;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        [HttpPost]
        [Route("Events")]
        public ActionResult<EventResponse> Events([FromBody] EventRequest request)
        {
            var id = _userIdentity.GetIdentity();

            if (id == null)
            {
                return new BadRequestResult();
            }

            var divs = String.Empty;

            // Get the division cookies
            var divCookies = _httpContextAccessor.HttpContext.Request.Cookies["user_divisions"];
            if (!string.IsNullOrWhiteSpace(divCookies))
            {
                divs = divCookies; // Look in the request cookie
            }

            if (string.IsNullOrEmpty(divs) && id != null)
            {
                divs = id.Division; // Look in the user profile
            }

            var filterDivs = divs.Replace(" ", "").Split(',');

            // We ignore the request end date to compensate for daylight saving issues on the client browser
            var endDate = request.StartDate.Date.AddMonths(3).AddDays(-1);
            var events = _repository.GetEventsForDivisionsAndRange(request.StartDate, endDate, filterDivs);

            var response = new EventResponse() { Events = new Dictionary<string, List<Event>>() };
            foreach (var e in events)
            {
                if (e.StartDate.HasValue && e.EndDate.HasValue)
                {
                    if (!response.Events.ContainsKey(e.AssetId))
                    {
                        response.Events.Add(e.AssetId, new List<Event>());
                    }
                    response.Events[e.AssetId].Add(e);
                }
            }

            return new OkObjectResult(response);
        }
    }
}
