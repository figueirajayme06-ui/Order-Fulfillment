using Microsoft.AspNetCore.Mvc;
using OF.Data.Database;
using OF.UI.Database;
using OF.UI.Identity;
using OF.WebApp.Features.Agreements;
using OF.WebApp.Features.Authorization;

namespace OF.WebApp.Controllers;

[ApiController]
[Route("api/agreements/{headerId:int}/equipment")]
public sealed class AgreementEquipmentController : ControllerBase
{
    private readonly IDataRepository _repository;
    private readonly IAgreementEquipmentRepository _equipmentRepository;
    private readonly IUserIdentity _userIdentity;

    public AgreementEquipmentController(
        IDataRepository repository,
        IAgreementEquipmentRepository equipmentRepository,
        IUserIdentity userIdentity)
    {
        _repository = repository;
        _equipmentRepository = equipmentRepository;
        _userIdentity = userIdentity;
    }

    [HttpGet("catalog")]
    public IActionResult GetCatalog(int headerId)
    {
        var access = GetAccessibleHeader(headerId);
        if (access.Failure != null)
        {
            return access.Failure;
        }

        if (!AgreementEquipmentEligibility.CanChange(access.Header!))
        {
            return HeaderNotEligibleProblem();
        }

        var catalog = _equipmentRepository.GetCatalog(access.Header!.Division);
        return Ok(new AgreementEquipmentCatalogResponse
        {
            ProductLines = catalog.ProductLines
                .Select(line => new AgreementEquipmentProductLineResponse
                {
                    Id = line.Id,
                    Description = line.Description,
                    FamilyDescription = line.FamilyDescription,
                })
                .ToArray(),
            Generics = catalog.Generics.Select(MapGeneric).ToArray(),
        });
    }

    [HttpGet("catalog/generics/{genericId:int}")]
    public IActionResult GetGenericCatalog(
        int headerId,
        int genericId,
        [FromQuery(Name = "attributes")] string[]? rawAttributes = null)
    {
        var access = GetAccessibleHeader(headerId);
        if (access.Failure != null)
        {
            return access.Failure;
        }

        if (!AgreementEquipmentEligibility.CanChange(access.Header!))
        {
            return HeaderNotEligibleProblem();
        }

        var attributes = ExpandQueryAttributes(rawAttributes);
        var result = _equipmentRepository.GetGenericCatalog(access.Header!.Division, genericId, attributes);
        if (result.Failure == AgreementEquipmentFailure.InvalidGeneric)
        {
            return NotFound();
        }

        if (result.Failure == AgreementEquipmentFailure.InvalidAttributes)
        {
            return ValidationProblem(
                "Equipment catalog validation failed.",
                "attributes",
                result.Error ?? "One or more attributes are invalid.");
        }

        return Ok(new AgreementEquipmentGenericCatalogResponse
        {
            Generic = MapGeneric(result.Generic!),
            Attributes = result.AttributeGroups
                .Select(group => new AgreementEquipmentAttributeResponse
                {
                    Name = group.Name,
                    Values = group.Values,
                })
                .ToArray(),
            Items = result.Items
                .Select(item => new AgreementEquipmentItemResponse
                {
                    ItemNumber = item.ItemNumber,
                    Description = item.Description,
                    GenericId = item.GenericId,
                })
                .ToArray(),
        });
    }

    [HttpPost]
    [DenyReadOnly]
    public IActionResult Create(int headerId, [FromBody] CreateAgreementEquipmentRequest request)
    {
        var access = GetAccessibleHeader(headerId);
        if (access.Failure != null)
        {
            return access.Failure;
        }

        if (!AgreementEquipmentEligibility.CanChange(access.Header!))
        {
            return HeaderNotEligibleProblem();
        }

        if (request.Quantity is < 1 or > 1000)
        {
            return ValidationProblem(
                "Equipment validation failed.",
                "quantity",
                "Quantity must be between 1 and 1000.");
        }

        var result = _equipmentRepository.Create(
            _userIdentity,
            headerId,
            access.Header!.Division,
            new AgreementEquipmentCreateCommand(
                request.ParentLineId,
                request.GenericId,
                request.ItemNumber,
                request.Attributes ?? [],
                request.Quantity));

        var failure = MapCreateFailure(result);
        if (failure != null)
        {
            return failure;
        }

        var line = result.Line!;
        var response = new AgreementEquipmentCreatedResponse
        {
            LineId = line.Id,
            ParentLineId = request.ParentLineId,
            AgreementLineNumber = line.AgreementLineNumber!,
            ItemNumber = line.ItemNumber!,
            GenericItemNumber = line.GenericItemNumber!,
            Attributes = line.Attributes,
            Quantity = (int)line.Quantity,
            HeaderStatus = result.HeaderStatus,
        };

        return Created($"/api/agreements/{headerId}", response);
    }

    [HttpDelete("{lineId:int}")]
    [DenyReadOnly]
    public IActionResult Delete(int headerId, int lineId)
    {
        var access = GetAccessibleHeader(headerId);
        if (access.Failure != null)
        {
            return access.Failure;
        }

        if (!AgreementLineDeletionEligibility.CanDelete(access.Header!))
        {
            return ValidationProblem("Equipment line cannot be removed.", "agreement", "Lines can only be removed from a temporary agreement awaiting activation.", StatusCodes.Status409Conflict);
        }

        var result = _equipmentRepository.Delete(
            _userIdentity,
            headerId,
            access.Header!.Division,
            lineId);

        return result.Failure switch
        {
            AgreementEquipmentFailure.None => NoContent(),
            AgreementEquipmentFailure.HeaderNotFound => NotFound(),
            AgreementEquipmentFailure.LineNotFound => NotFound(),
            AgreementEquipmentFailure.HeaderNotEligible => ValidationProblem(
                "Equipment line cannot be removed.",
                "agreement",
                "Lines can only be removed from a temporary agreement awaiting activation.",
                StatusCodes.Status409Conflict),
            AgreementEquipmentFailure.LineNotDeletable => ValidationProblem(
                "Equipment line cannot be removed.",
                "lineId",
                "Only a pending locally added equipment line can be removed.",
                StatusCodes.Status409Conflict),
            AgreementEquipmentFailure.ReservationsExist => ValidationProblem(
                "Equipment line cannot be removed.",
                "lineId",
                "Remove the line's reservations before removing the equipment line.",
                StatusCodes.Status409Conflict),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private (Header? Header, IActionResult? Failure) GetAccessibleHeader(int headerId)
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null)
        {
            return (null, Unauthorized());
        }

        var header = _repository.GetHeader(headerId);
        if (header == null || !AgreementDivisionAccess.CanAccess(identity, header.Division))
        {
            return (null, NotFound());
        }

        return (header, null);
    }

    private IActionResult? MapCreateFailure(AgreementEquipmentCreateResult result) => result.Failure switch
    {
        AgreementEquipmentFailure.None => null,
        AgreementEquipmentFailure.HeaderNotFound => NotFound(),
        AgreementEquipmentFailure.HeaderNotEligible => HeaderNotEligibleProblem(),
        AgreementEquipmentFailure.ParentNotFound => NotFound(),
        AgreementEquipmentFailure.ParentNotEligible => ValidationProblem(
            "Equipment line changed.",
            "parentLineId",
            "Equipment can only be added beneath a stable root fulfilment line.",
            StatusCodes.Status409Conflict),
        AgreementEquipmentFailure.InvalidQuantity => ValidationProblem(
            "Equipment validation failed.",
            "quantity",
            "Quantity must be between 1 and 1000."),
        AgreementEquipmentFailure.InvalidGeneric => NotFound(),
        AgreementEquipmentFailure.InvalidAttributes => ValidationProblem(
            "Equipment validation failed.",
            "attributes",
            result.Error ?? "One or more attributes are invalid."),
        AgreementEquipmentFailure.InvalidItem => ValidationProblem(
            "Equipment validation failed.",
            "itemNumber",
            "The selected item is not active, available, or compatible with the selected generic and attributes."),
        _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
    };

    private ObjectResult HeaderNotEligibleProblem() => ValidationProblem(
        "Agreement cannot be changed.",
        "agreement",
        "Equipment can only be changed on a stable temporary or activated agreement.",
        StatusCodes.Status409Conflict);

    private ObjectResult ValidationProblem(
        string title,
        string field,
        string message,
        int statusCode = StatusCodes.Status400BadRequest)
    {
        var problem = new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            [field] = [message],
        })
        {
            Status = statusCode,
            Title = title,
            Detail = message,
        };
        problem.Extensions["message"] = message;

        var response = new ObjectResult(problem)
        {
            StatusCode = statusCode,
        };
        response.ContentTypes.Add("application/problem+json");
        return response;
    }

    private static string[] ExpandQueryAttributes(IEnumerable<string>? attributes) =>
        attributes?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .SelectMany(value => value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToArray()
        ?? [];

    private static AgreementEquipmentGenericResponse MapGeneric(AgreementEquipmentGenericData generic) => new()
    {
        Id = generic.Id,
        ProductLineId = generic.ProductLineId,
        Code = generic.Code,
        Description = generic.Description,
    };
}

public sealed record CreateAgreementEquipmentRequest
{
    public int ParentLineId { get; init; }
    public int GenericId { get; init; }
    public string? ItemNumber { get; init; }
    public string[]? Attributes { get; init; }
    public int Quantity { get; init; }
}

public sealed class AgreementEquipmentCatalogResponse
{
    public required IReadOnlyList<AgreementEquipmentProductLineResponse> ProductLines { get; init; }
    public required IReadOnlyList<AgreementEquipmentGenericResponse> Generics { get; init; }
}

public sealed class AgreementEquipmentProductLineResponse
{
    public int Id { get; init; }
    public required string Description { get; init; }
    public required string FamilyDescription { get; init; }
}

public sealed class AgreementEquipmentGenericResponse
{
    public int Id { get; init; }
    public int ProductLineId { get; init; }
    public required string Code { get; init; }
    public required string Description { get; init; }
}

public sealed class AgreementEquipmentGenericCatalogResponse
{
    public required AgreementEquipmentGenericResponse Generic { get; init; }
    public required IReadOnlyList<AgreementEquipmentAttributeResponse> Attributes { get; init; }
    public required IReadOnlyList<AgreementEquipmentItemResponse> Items { get; init; }
}

public sealed class AgreementEquipmentAttributeResponse
{
    public required string Name { get; init; }
    public required IReadOnlyList<string> Values { get; init; }
}

public sealed class AgreementEquipmentItemResponse
{
    public required string ItemNumber { get; init; }
    public required string Description { get; init; }
    public int GenericId { get; init; }
}

public sealed class AgreementEquipmentCreatedResponse
{
    public int LineId { get; init; }
    public int ParentLineId { get; init; }
    public required string AgreementLineNumber { get; init; }
    public required string ItemNumber { get; init; }
    public required string GenericItemNumber { get; init; }
    public string? Attributes { get; init; }
    public int Quantity { get; init; }
    public int HeaderStatus { get; init; }
}
