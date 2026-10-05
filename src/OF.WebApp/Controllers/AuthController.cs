using Microsoft.AspNetCore.Mvc;
using OF.Data.Enums;
using OF.UI.Database;
using OF.UI.Identity;
using OF.WebApp.Features.Authorization;

namespace OF.WebApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUserIdentity _userIdentity;
    private readonly IDataRepository _repository;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IUserIdentity userIdentity,
        IDataRepository repository,
        TimeProvider timeProvider,
        ILogger<AuthController> logger)
    {
        _userIdentity = userIdentity;
        _repository = repository;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    [HttpGet("me")]
    public IActionResult GetCurrentUser()
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null)
        {
            if (User?.Identity?.IsAuthenticated == true)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "NOF access required",
                    Detail = "The authenticated account has not been configured for Order Fulfillment.",
                    Extensions = { ["code"] = "user_not_provisioned" },
                });
            }

            return Unauthorized();
        }

        try
        {
            _repository.UpdateLastLoginAtUtc(identity.LoginName, _timeProvider.GetUtcNow());
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unable to record Last NOF access for user {LoginName}. Trace identifier: {TraceIdentifier}",
                identity.LoginName,
                HttpContext.TraceIdentifier);
        }

        return Ok(new CurrentUserResponse
        {
            LoginName = identity.LoginName,
            DisplayName = identity.FullName ?? identity.LoginName,
            Division = identity.Division ?? string.Empty,
            IsAdmin = identity.IsAdmin,
            IsSuperAdmin = identity.IsSuperAdmin,
            IsReadOnly = ReadOnlyAccess.IsReadOnly(identity),
            Language = MapLanguageCode((Languages)identity.Language),
        });
    }

    private static string MapLanguageCode(Languages language) => language switch
    {
        Languages.German => "de",
        Languages.French => "fr",
        Languages.Spanish => "es",
        Languages.Italian => "it",
        _ => "en",
    };
}

public sealed class CurrentUserResponse
{
    public string LoginName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Division { get; init; } = string.Empty;
    public bool IsAdmin { get; init; }
    public bool IsSuperAdmin { get; init; }
    public bool IsReadOnly { get; init; }
    public string Language { get; init; } = string.Empty;
}
