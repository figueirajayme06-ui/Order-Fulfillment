using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OF.UI.Database;
using OF.UI.Identity;
using OF.UI.Shared.Controllers;
using OF.UI.ViewModels.Ringfence;

namespace OF.UI.Controllers
{
    [Authorize]
    public class RingfenceController : CommonController
    {
        private readonly IDataRepository _repository;
        private readonly IUserIdentity _userIdentity;
        private readonly ILogger _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public RingfenceController(IDataRepository repository, IUserIdentity userIdentity, ILogger<RingfenceController> logger, IHttpContextAccessor httpContextAccessor) : base(repository)
        {
            _repository = repository;
            _userIdentity = userIdentity;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public IActionResult Index(int headerId)
        {
            ViewData["HideDivisions"] = true;

            // Require a user
            if (_userIdentity.GetIdentity() == null)
            {
                return Unauthorized();
            }

            var ringfenceModel = new RingfenceViewModel();
            ringfenceModel.Ringfences = new List<OF.Data.Database.Ringfence>();
            var ringfences = _repository.GetRingfences();
            var targetDivs = _userIdentity.GetIdentity().Division.Split(",");
            foreach (var ringfence in ringfences)
            {
                if (ringfence.Divisions.Split(",").Intersect(targetDivs).Any())
                {
                    ringfenceModel.Ringfences.Add(ringfence);
                }
            }  

            return View(ringfenceModel);
        }

    }
}
