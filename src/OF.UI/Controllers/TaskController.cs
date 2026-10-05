using OF.UI.Database;
using OF.UI.Identity;
using OF.UI.Models;
using Infragistics.Web.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OF.Data.Database;
using OF.Common;

namespace OF.UI.Controllers
{
    [Route("/api/[controller]")]
    [ApiController]
    [Authorize]
    public class TaskController : ControllerBase
    {
        private readonly IDataRepository _repository;
        private readonly IUserIdentity _userIdentity;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public TaskController(IDataRepository repository, IUserIdentity userIdentity, IHttpContextAccessor httpContextAccessor)
        { 
            _repository = repository;
            _userIdentity = userIdentity;
            _httpContextAccessor = httpContextAccessor;
        }

        [HttpGet]
        [Route("Divisions")]
        public ActionResult<IEnumerable<Division>> GetDivisions()
        {
            var headerDivs = _repository.GetHeaders()
                .Select(h => h.Division)
                .Where(d => !string.IsNullOrEmpty(d));

            var assetDivs = _repository.GetAssets()
                .Select(a => a.Division)
                .Where(d => !string.IsNullOrEmpty(d));

            return new ObjectResult(headerDivs
                .Concat(assetDivs)
                .Distinct()
                .OrderBy(d => d)
                .Select(d => new Division { Code = d }));
        }

        [HttpGet]
        [Route("Roles")]
        public ActionResult<IEnumerable<Division>> GetRoles()
        {
            return new ObjectResult(
                new object []
                {
                    new { code = Constants.Roles.ChangeOrder },
                    new { code = Constants.Roles.ChangeApproval },
                    new { code = Constants.Roles.NewFrontendPreview }
                });
        }

        [HttpGet]
        [Route("Users")]
        public ActionResult<IEnumerable<User>> GetUsers()
        {
            return new ObjectResult(_repository.GetUsers().OrderBy(b => b.FullName));
        }

        [HttpGet]
        [Route("UsersByDivision/{division}")]
        public ActionResult<IEnumerable<User>> GetUsersByDivision(string division)
        {
            if (_userIdentity.GetIdentity().DivisionValidForUser(division))
            {
                IEnumerable<User> users = 
                    _repository
                        .GetUsers()
                        .AsEnumerable()
                        .Where(x => x.Division.Split(',').Contains(division))
                        .OrderBy(b => b.FullName);

                return new ObjectResult(users);
            }

            return new ObjectResult(Enumerable.Empty<User>());
        }

        [HttpGet]
        [Route("Alerts")]
        public ActionResult<IEnumerable<Alert>> GetAlerts()
        {
            var result = new List<Alert>();
            var staleAlertIds = new List<int>();
            var items = _repository.GetAlertsForUser(_userIdentity.GetIdentity().LoginName).Where(a => !a.Acknowledged).OrderByDescending(a => a.Id).ToArray();

            foreach (var a in items)
            {
                var line = _repository.GetLine(a.LineId);

                if (line == null || line.IsDeleted)
                {
                    staleAlertIds.Add(a.Id);
                    continue;
                }

                if (a.Text.StartsWith("Reservation clash:", StringComparison.Ordinal))
                {
                    if (_repository.HasReservationsForLine(a.LineId))
                    {
                        staleAlertIds.Add(a.Id);
                        continue;
                    }

                    if (!line.RequiresFulfilment)
                    {
                        staleAlertIds.Add(a.Id);
                        continue;
                    }
                }

                if (line.HeaderId != null)
                {
                    var header = _repository.GetHeader(line.HeaderId.Value);
                    if (header != null)
                    {
                        a.HeaderId = header.Id;
                        result.Add(a);
                    }
                    else
                    {
                        staleAlertIds.Add(a.Id);
                    }
                }
            }

            if (staleAlertIds.Count > 0)
            {
                _repository.AcknowledgeAlerts(staleAlertIds.ToArray());
            }

            return new OkObjectResult(result.ToArray());
        }

        [HttpGet]
        [Route("Agreement")]
        public ActionResult<VwHeader> GetAgreement(int id)
        {
            return new ObjectResult(_repository.GetAgreement(id));
        }
    }
}
