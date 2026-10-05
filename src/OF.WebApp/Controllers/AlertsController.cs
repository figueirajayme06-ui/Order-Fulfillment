using Microsoft.AspNetCore.Mvc;
using OF.UI.Database;
using OF.UI.Identity;

namespace OF.WebApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AlertsController : ControllerBase
{
    private readonly IDataRepository _repository;
    private readonly IUserIdentity _userIdentity;

    public AlertsController(IDataRepository repository, IUserIdentity userIdentity)
    {
        _repository = repository;
        _userIdentity = userIdentity;
    }

    [HttpGet]
    public IActionResult GetAlerts()
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null)
        {
            return Unauthorized();
        }

        var alerts = new List<object>();
        var staleAlertIds = new List<int>();
        var items = _repository.GetAlertsForUser(identity.LoginName)
            .Where(alert => !alert.Acknowledged)
            .OrderByDescending(alert => alert.Id);

        foreach (var alert in items)
        {
            var line = _repository.GetLine(alert.LineId);
            if (line == null || line.IsDeleted)
            {
                staleAlertIds.Add(alert.Id);
                continue;
            }

            if (alert.Text.StartsWith("Reservation clash:", StringComparison.Ordinal))
            {
                if (_repository.HasReservationsForLine(alert.LineId) || !line.RequiresFulfilment)
                {
                    staleAlertIds.Add(alert.Id);
                    continue;
                }
            }

            if (line.HeaderId == null)
            {
                staleAlertIds.Add(alert.Id);
                continue;
            }

            var header = _repository.GetHeader(line.HeaderId.Value);
            if (header == null)
            {
                staleAlertIds.Add(alert.Id);
                continue;
            }

            alerts.Add(new
            {
                alert.Id,
                alert.Text,
                alert.LineId,
                HeaderId = header.Id,
            });
        }

        if (staleAlertIds.Count > 0)
        {
            _repository.AcknowledgeAlerts(staleAlertIds.ToArray());
        }

        return Ok(alerts);
    }
}
