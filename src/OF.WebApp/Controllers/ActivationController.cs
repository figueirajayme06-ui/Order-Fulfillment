using Microsoft.AspNetCore.Mvc;
using OF.Common.Infrastructure.Storage;
using OF.Data.Database;
using OF.UI.Database;
using OF.UI.Identity;
using OF.WebApp.Features.Agreements;
using OF.WebApp.Features.Authorization;

namespace OF.WebApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ActivationController : ControllerBase
{
    private readonly IDataRepository _repository;
    private readonly IUserIdentity _userIdentity;
    private readonly ActivateAgreementQueueClient _activateQueue;

    public ActivationController(
        IDataRepository repository,
        IUserIdentity userIdentity,
        ActivateAgreementQueueClient activateQueue)
    {
        _repository = repository;
        _userIdentity = userIdentity;
        _activateQueue = activateQueue;
    }

    [HttpPost("{headerId:int}")]
    [DenyReadOnly]
    public async Task<IActionResult> Activate(int headerId)
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null)
        {
            return Unauthorized();
        }

        if (!CanAccessAgreement(identity, headerId))
        {
            return NotFound();
        }

        try
        {
            await _repository.SetHeaderForActivation(headerId, _userIdentity);
            await _activateQueue.QueueActivation(headerId);
        }
        catch (Exception ex)
        {
            return StatusCode(503, new ActivationUnavailableResponse
            {
                Type = "Activation",
                HeaderId = headerId,
                Error = $"Activation queued locally but Service Bus unavailable: {ex.Message}",
            });
        }

        return Ok(new ActivationResponse
        {
            Type = "Activation",
            HeaderId = headerId,
        });
    }

    [HttpPost("{headerId:int}/cancel")]
    [DenyReadOnly]
    public IActionResult CancelActivation(int headerId)
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null)
        {
            return Unauthorized();
        }

        if (!CanAccessAgreement(identity, headerId))
        {
            return NotFound();
        }

        _repository.TimeoutSublineActivation(headerId, _userIdentity);
        return Ok(new ActivationResponse
        {
            Type = "CancelActivation",
            HeaderId = headerId,
        });
    }

    private bool CanAccessAgreement(User identity, int headerId)
    {
        var header = _repository.GetHeader(headerId);
        return header != null && AgreementDivisionAccess.CanAccess(identity, header.Division);
    }
}

public sealed class ActivationResponse
{
    public string Type { get; init; } = string.Empty;
    public int HeaderId { get; init; }
}

public sealed class ActivationUnavailableResponse
{
    public string Type { get; init; } = string.Empty;
    public int HeaderId { get; init; }
    public string Error { get; init; } = string.Empty;
}
