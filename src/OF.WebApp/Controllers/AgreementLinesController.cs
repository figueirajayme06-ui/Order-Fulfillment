using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OF.UI.Database;
using OF.UI.Identity;
using OF.WebApp.Features.Agreements;
using OF.WebApp.Features.Authorization;

namespace OF.WebApp.Controllers;

[ApiController]
[Route("api/agreements/{headerId:int}/lines")]
public sealed class AgreementLinesController(
    IDataRepository repository,
    IAgreementLineDeletionRepository deletionRepository,
    IUserIdentity userIdentity) : ControllerBase
{
    [HttpDelete("{lineId:int}")]
    [DenyReadOnly]
    public IActionResult Delete(int headerId, int lineId)
    {
        var identity = userIdentity.GetIdentity();
        if (identity == null) return Unauthorized();
        if (ReadOnlyAccess.HasReadOnlyRole(identity.Roles)) return Forbid();
        var header = repository.GetHeader(headerId);
        if (header == null || header.IsDeleted || !AgreementDivisionAccess.CanAccess(identity, header.Division)) return NotFound();
        if (!AgreementLineDeletionEligibility.CanDelete(header))
            return ConflictProblem("header_not_eligible", "Lines can only be deleted from a temporary agreement awaiting activation.");

        try
        {
            var result = deletionRepository.Delete(userIdentity, headerId, header.Division, lineId);
            return result.Failure switch
            {
                AgreementLineDeletionFailure.None => Ok(new { result.LineId, result.RemovedReservationCount, result.HeaderStatus }),
                AgreementLineDeletionFailure.NotFound => NotFound(),
                AgreementLineDeletionFailure.HeaderNotEligible => ConflictProblem("header_not_eligible", "The agreement state changed. Refresh before deleting a line."),
                AgreementLineDeletionFailure.LineNotEligible => ConflictProblem("line_not_eligible", "Only a line awaiting activation can be deleted."),
                AgreementLineDeletionFailure.LastLine => ConflictProblem("last_line", "The last line of an agreement cannot be deleted."),
                AgreementLineDeletionFailure.HasChildren => ConflictProblem("has_children", "Delete this line's child lines first."),
                AgreementLineDeletionFailure.ConfirmedReservations => ConflictProblem("confirmed_reservations", "This line has confirmed reservations and cannot be deleted."),
                _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
            };
        }
        catch (DbUpdateConcurrencyException)
        {
            return ConflictProblem("concurrent_change", "The agreement changed. Refresh and try again.");
        }
        catch (SqlException exception) when (exception.Number is 1205 or 1222 or 51000)
        {
            return ConflictProblem("concurrent_change", "The agreement is being changed. Refresh and try again.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException sql && sql.Number is 1205 or 1222)
        {
            return ConflictProblem("concurrent_change", "The agreement is being changed. Refresh and try again.");
        }
    }

    private static ObjectResult ConflictProblem(string code, string message)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Line cannot be deleted.",
            Detail = message,
        };
        problem.Extensions["code"] = code;
        problem.Extensions["message"] = message;
        var response = new ObjectResult(problem) { StatusCode = StatusCodes.Status409Conflict };
        response.ContentTypes.Add("application/problem+json");
        return response;
    }
}
