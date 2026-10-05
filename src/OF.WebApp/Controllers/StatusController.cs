using Microsoft.AspNetCore.Mvc;
using OF.UI.Database;
using OF.UI.Identity;

namespace OF.WebApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StatusController : ControllerBase
{
    private readonly IDataRepository _repository;
    private readonly IUserIdentity _userIdentity;

    public StatusController(IDataRepository repository, IUserIdentity userIdentity)
    {
        _repository = repository;
        _userIdentity = userIdentity;
    }

    [HttpGet("refreshes")]
    public IActionResult GetRefreshes()
    {
        if (_userIdentity.GetIdentity() == null)
        {
            return Unauthorized();
        }

        var refreshes = _repository.GetRefreshes()
            .Select(refresh => new
            {
                refresh.Key,
                refresh.Description,
                refresh.LastSuccessfulRunUtc,
            })
            .ToList();

        return Ok(refreshes);
    }
}
