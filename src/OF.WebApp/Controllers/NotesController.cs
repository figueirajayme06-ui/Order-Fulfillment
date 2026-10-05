using Microsoft.AspNetCore.Mvc;
using OF.Data.Database;
using OF.UI.Database;
using OF.UI.Identity;
using OF.WebApp.Features.Agreements;
using OF.WebApp.Features.Authorization;
using OF.WebApp.Features.Divisions;

namespace OF.WebApp.Controllers;

[ApiController]
[Route("api/notes")]
public sealed class NotesController : ControllerBase
{
    private const int MaximumNoteLength = 10_000;
    private const string AssetNoteType = "asset";
    private const string AgreementNoteType = "agreement";

    private readonly IDataRepository _repository;
    private readonly IUserIdentity _userIdentity;

    public NotesController(IDataRepository repository, IUserIdentity userIdentity)
    {
        _repository = repository;
        _userIdentity = userIdentity;
    }

    [HttpGet("assets/{assetId}")]
    public IActionResult GetAssetNotes(string assetId)
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null)
        {
            return Unauthorized();
        }

        var asset = _repository.GetAsset(assetId);
        if (asset == null || !DivisionAccess.CanAccess(identity, asset.Division))
        {
            return NotFound();
        }

        return Ok(GetNotes(AssetNoteType, assetId));
    }

    [HttpPost("assets/{assetId}")]
    [DenyReadOnly]
    public IActionResult CreateAssetNote(string assetId, [FromBody] SaveNoteRequest request)
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null)
        {
            return Unauthorized();
        }

        var asset = _repository.GetAsset(assetId);
        if (asset == null || !DivisionAccess.CanAccess(identity, asset.Division))
        {
            return NotFound();
        }

        return CreateNote(AssetNoteType, assetId, request);
    }

    [HttpPut("assets/{assetId}/{noteId:int}")]
    [DenyReadOnly]
    public IActionResult UpdateAssetNote(string assetId, int noteId, [FromBody] SaveNoteRequest request)
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null)
        {
            return Unauthorized();
        }

        var asset = _repository.GetAsset(assetId);
        if (asset == null || !DivisionAccess.CanAccess(identity, asset.Division))
        {
            return NotFound();
        }

        return UpdateNote(AssetNoteType, assetId, noteId, request);
    }

    [HttpGet("agreements/{headerId:int}")]
    public IActionResult GetAgreementNotes(int headerId)
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null)
        {
            return Unauthorized();
        }

        var header = _repository.GetHeader(headerId);
        if (header == null || !AgreementDivisionAccess.CanAccess(identity, header.Division))
        {
            return NotFound();
        }

        return Ok(GetNotes(AgreementNoteType, headerId.ToString()));
    }

    [HttpPost("agreements/{headerId:int}")]
    [DenyReadOnly]
    public IActionResult CreateAgreementNote(int headerId, [FromBody] SaveNoteRequest request)
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null)
        {
            return Unauthorized();
        }

        var header = _repository.GetHeader(headerId);
        if (header == null || !AgreementDivisionAccess.CanAccess(identity, header.Division))
        {
            return NotFound();
        }

        return CreateNote(AgreementNoteType, headerId.ToString(), request);
    }

    [HttpPut("agreements/{headerId:int}/{noteId:int}")]
    [DenyReadOnly]
    public IActionResult UpdateAgreementNote(int headerId, int noteId, [FromBody] SaveNoteRequest request)
    {
        var identity = _userIdentity.GetIdentity();
        if (identity == null)
        {
            return Unauthorized();
        }

        var header = _repository.GetHeader(headerId);
        if (header == null || !AgreementDivisionAccess.CanAccess(identity, header.Division))
        {
            return NotFound();
        }

        return UpdateNote(AgreementNoteType, headerId.ToString(), noteId, request);
    }

    private IActionResult CreateNote(string noteType, string parentId, SaveNoteRequest request)
    {
        var validationError = Validate(request, out var noteText);
        if (validationError != null)
        {
            return validationError;
        }

        var saved = _repository.CreateNote(
            identity: _userIdentity,
            new Note { ParentId = parentId, NoteType = noteType, Note1 = noteText });
        return Ok(ToResponse(saved));
    }

    private IActionResult UpdateNote(string noteType, string parentId, int noteId, SaveNoteRequest request)
    {
        var note = _repository.GetNote(noteId);
        if (note == null || note.NoteType != noteType || note.ParentId != parentId)
        {
            return NotFound();
        }

        var validationError = Validate(request, out var noteText);
        if (validationError != null)
        {
            return validationError;
        }

        note.Note1 = noteText;
        return Ok(ToResponse(_repository.UpdateNote(_userIdentity, note)));
    }

    private IActionResult? Validate(SaveNoteRequest request, out string noteText)
    {
        noteText = request.Notes ?? string.Empty;
        return noteText.Length > MaximumNoteLength
            ? BadRequest(new { message = $"Notes must be {MaximumNoteLength:N0} characters or fewer." })
            : null;
    }

    private IReadOnlyList<NoteResponse> GetNotes(string noteType, string parentId) => _repository.GetNotes(noteType, parentId)
        .OrderByDescending(note => note.LastUpdatedDate)
        .ThenByDescending(note => note.Id)
        .AsEnumerable()
        .Select(ToResponse)
        .ToList();

    private static NoteResponse ToResponse(Note note) => new()
    {
        Id = note.Id,
        Notes = note.Note1 ?? string.Empty,
        LastUpdatedBy = note.LastUpdatedBy,
        LastUpdatedDate = note.LastUpdatedDate,
    };
}

public sealed class SaveNoteRequest
{
    public string? Notes { get; init; }
}

public sealed class NoteResponse
{
    public int Id { get; init; }
    public string Notes { get; init; } = string.Empty;
    public string? LastUpdatedBy { get; init; }
    public DateTime? LastUpdatedDate { get; init; }
}
