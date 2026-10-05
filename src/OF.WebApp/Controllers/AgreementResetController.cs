using Microsoft.AspNetCore.Mvc;
using OF.UI.Database;
using OF.UI.Identity;
using OF.WebApp.Features.Agreements;
using OF.WebApp.Features.Authorization;

namespace OF.WebApp.Controllers;

[ApiController]
[Route("api/agreements/{headerId:int}/unfulfil")]
public sealed class AgreementResetController(
    IDataRepository repository,
    IAgreementResetRepository resetRepository,
    IUserIdentity userIdentity) : ControllerBase
{
    [HttpPost]
    [DenyReadOnly]
    public IActionResult Unfulfil(int headerId)
    {
        var identity = userIdentity.GetIdentity();
        if (identity == null)
            return Unauthorized();
        if (ReadOnlyAccess.HasReadOnlyRole(identity.Roles))
            return Forbid();

        var header = repository.GetHeader(headerId);
        if (header == null || !AgreementDivisionAccess.CanAccess(identity, header.Division))
            return NotFound();

        var result = resetRepository.Unfulfil(headerId, header.Division, identity.LoginName);
        return result.Failure switch
        {
            AgreementResetFailure.None => Ok(new AgreementResetResponse(result.LinesReset, result.ReservationsRemoved, result.HeaderStatus)),
            AgreementResetFailure.NotFound => NotFound(),
            AgreementResetFailure.ActivationConflict => ConflictProblem("activation_conflict",
                "This agreement has activation work in progress or external activation state. Its fulfilment cannot be reset."),
            AgreementResetFailure.ConfirmedReservations => ConflictProblem("confirmed_reservations",
                "This agreement has confirmed reservations or external fulfilment data. Its fulfilment cannot be reset."),
            AgreementResetFailure.ConcurrentChange => ConflictProblem("agreement_changed",
                "The agreement changed while resetting fulfilment. Refresh and try again."),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private ObjectResult ConflictProblem(string code, string message)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Agreement fulfilment cannot be reset.",
            Detail = message,
        };
        problem.Extensions["code"] = code;
        problem.Extensions["message"] = message;
        var response = new ObjectResult(problem) { StatusCode = StatusCodes.Status409Conflict };
        response.ContentTypes.Add("application/problem+json");
        return response;
    }
}

public sealed record AgreementResetResponse(int LinesReset, int ReservationsRemoved, int HeaderStatus);
