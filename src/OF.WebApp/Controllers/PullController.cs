using Microsoft.AspNetCore.Mvc;
using OF.Common.Infrastructure.Storage;
using OF.UI.Identity;
using OF.WebApp.Features.Authorization;

namespace OF.WebApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PullController : ControllerBase
{
    private readonly UpdateAgreementQueueClient _updateAgreementQueue;
    private readonly UpsertQuoteQueueClient _upsertQuoteQueue;
    private readonly IUserIdentity _userIdentity;

    public PullController(
        UpdateAgreementQueueClient updateAgreementQueue,
        UpsertQuoteQueueClient upsertQuoteQueue,
        IUserIdentity userIdentity)
    {
        _updateAgreementQueue = updateAgreementQueue;
        _upsertQuoteQueue = upsertQuoteQueue;
        _userIdentity = userIdentity;
    }

    [HttpPost("{number}")]
    [DenyReadOnly]
    public async Task<IActionResult> Pull(string number)
    {
        if (_userIdentity.GetIdentity() == null)
        {
            return Unauthorized();
        }

        var normalizedNumber = number.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(normalizedNumber))
        {
            return BadRequest(new { error = "Enter a quote or agreement number." });
        }

        if (normalizedNumber.StartsWith('Q'))
        {
            await _upsertQuoteQueue.QueueQuote(normalizedNumber);
            return Ok(new { type = "Quote", number = normalizedNumber });
        }

        if (normalizedNumber.StartsWith('A') || normalizedNumber.StartsWith('T'))
        {
            await _updateAgreementQueue.QueueAgreement(normalizedNumber);
            return Ok(new { type = "Agreement", number = normalizedNumber });
        }

        return BadRequest(new { error = "Quote numbers must start with Q; agreement numbers must start with A or T." });
    }
}
