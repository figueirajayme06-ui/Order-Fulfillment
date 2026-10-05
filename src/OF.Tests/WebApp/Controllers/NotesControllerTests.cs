using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using OF.Data.Database;
using OF.UI.Database;
using OF.UI.Identity;
using OF.WebApp.Controllers;

namespace OF.Tests.WebApp.Controllers;

public class NotesControllerTests
{
    private readonly Mock<IDataRepository> _repository = new();
    private readonly Mock<IUserIdentity> _identity = new();

    [Fact]
    public void GetAssetNotes_ReturnsUnauthorizedWithoutReadingTheAsset_WhenIdentityIsMissing()
    {
        _identity.Setup(identity => identity.GetIdentity()).Returns((User?)null!);

        var result = CreateSubject().GetAssetNotes("ASSET-1");

        result.Should().BeOfType<UnauthorizedResult>();
        _repository.Verify(repository => repository.GetAsset(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void CreateAgreementNote_ReturnsUnauthorizedWithoutReadingTheAgreement_WhenIdentityIsMissing()
    {
        _identity.Setup(identity => identity.GetIdentity()).Returns((User?)null!);

        var result = CreateSubject().CreateAgreementNote(42, new SaveNoteRequest { Notes = "Private" });

        result.Should().BeOfType<UnauthorizedResult>();
        _repository.Verify(repository => repository.GetHeader(It.IsAny<int>()), Times.Never);
        _repository.Verify(repository => repository.CreateNote(It.IsAny<IUserIdentity>(), It.IsAny<Note>()), Times.Never);
    }

    [Fact]
    public void GetAssetNotes_ReturnsAllNotesNewestFirst_ForAnAccessibleAsset()
    {
        SetIdentity("UK");
        _repository.Setup(repository => repository.GetAsset("ASSET-1")).Returns(CreateAsset("ASSET-1", "UK"));
        _repository.Setup(repository => repository.GetNotes("asset", "ASSET-1")).Returns(new[]
        {
            new Note { Id = 1, ParentId = "ASSET-1", NoteType = "asset", Note1 = "Older", LastUpdatedDate = new DateTime(2026, 1, 1) },
            new Note { Id = 2, ParentId = "ASSET-1", NoteType = "asset", Note1 = "Current", LastUpdatedBy = "planner@example.com", LastUpdatedDate = new DateTime(2026, 2, 1) },
        }.AsQueryable());

        var response = GetResponses(CreateSubject().GetAssetNotes("ASSET-1"));

        response.Select(note => note.Id).Should().Equal(2, 1);
        response[0].Notes.Should().Be("Current");
        response[0].LastUpdatedBy.Should().Be("planner@example.com");
    }

    [Fact]
    public void CreateAssetNote_AllowsAReadOnlyAuthenticatedUserWithAssetAccess()
    {
        SetIdentity("UK", roles: "ReadOnly");
        _repository.Setup(repository => repository.GetAsset("ASSET-1")).Returns(CreateAsset("ASSET-1", "UK"));
        _repository.Setup(repository => repository.CreateNote(_identity.Object, It.IsAny<Note>()))
            .Returns((IUserIdentity _, Note note) => note);

        var response = GetResponse(CreateSubject().CreateAssetNote("ASSET-1", new SaveNoteRequest { Notes = "Call before collection" }));

        response.Notes.Should().Be("Call before collection");
        _repository.Verify(repository => repository.CreateNote(_identity.Object, It.Is<Note>(note =>
            note.ParentId == "ASSET-1" && note.NoteType == "asset" && note.Note1 == "Call before collection")), Times.Once);
    }

    [Fact]
    public void UpdateAgreementNote_UpdatesTheRequestedNote_ForAnAccessibleAgreement()
    {
        SetIdentity("UK", roles: "ReadOnly");
        _repository.Setup(repository => repository.GetHeader(42)).Returns(CreateHeader(42, "UK"));
        var existing = new Note
        {
            Id = 12,
            ParentId = "42",
            NoteType = "agreement",
            Note1 = "Old text",
            LastUpdatedBy = "another.user@example.com",
        };
        _repository.Setup(repository => repository.GetNote(12)).Returns(existing);
        _repository.Setup(repository => repository.UpdateNote(_identity.Object, existing)).Returns(existing);

        var response = GetResponse(CreateSubject().UpdateAgreementNote(42, 12, new SaveNoteRequest { Notes = "" }));

        response.Notes.Should().BeEmpty();
        _repository.Verify(repository => repository.UpdateNote(_identity.Object, existing), Times.Once);
    }

    [Fact]
    public void UpdateAgreementNote_ReturnsNotFound_WhenTheNoteBelongsToAnotherRecord()
    {
        SetIdentity("UK");
        _repository.Setup(repository => repository.GetHeader(42)).Returns(CreateHeader(42, "UK"));
        _repository.Setup(repository => repository.GetNote(12))
            .Returns(new Note { Id = 12, ParentId = "43", NoteType = "agreement", Note1 = "Other agreement" });

        var result = CreateSubject().UpdateAgreementNote(42, 12, new SaveNoteRequest { Notes = "Changed" });

        result.Should().BeOfType<NotFoundResult>();
        _repository.Verify(repository => repository.UpdateNote(It.IsAny<IUserIdentity>(), It.IsAny<Note>()), Times.Never);
    }

    [Fact]
    public void GetAgreementNotes_ReturnsNotFound_WhenTheAgreementIsOutsideTheUsersDivision()
    {
        SetIdentity("UK");
        _repository.Setup(repository => repository.GetHeader(42)).Returns(CreateHeader(42, "FR"));

        var result = CreateSubject().GetAgreementNotes(42);

        result.Should().BeOfType<NotFoundResult>();
        _repository.Verify(repository => repository.GetNotes(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void CreateAgreementNote_RejectsNotesLongerThanTheContractLimit()
    {
        SetIdentity("UK");
        _repository.Setup(repository => repository.GetHeader(42)).Returns(CreateHeader(42, "UK"));

        var result = CreateSubject().CreateAgreementNote(42, new SaveNoteRequest { Notes = new string('x', 10_001) });

        result.Should().BeOfType<BadRequestObjectResult>();
        _repository.Verify(repository => repository.CreateNote(It.IsAny<IUserIdentity>(), It.IsAny<Note>()), Times.Never);
    }

    private NotesController CreateSubject() => new(_repository.Object, _identity.Object);

    private void SetIdentity(string division, string? roles = null)
    {
        _identity.Setup(identity => identity.GetIdentity()).Returns(new User
        {
            LoginName = "planner@example.com",
            FullName = "Planner",
            Division = division,
            Roles = roles,
            DateFormat = "dd/MM/yyyy",
        });
    }

    private static Asset CreateAsset(string id, string division) => new()
    {
        Id = id,
        IndividualItemNumber = id,
        Division = division,
    };

    private static Header CreateHeader(int id, string division) => new()
    {
        Id = id,
        Division = division,
        OrderSource = "NOF",
        Facility = "FAC",
    };

    private static IReadOnlyList<NoteResponse> GetResponses(IActionResult result) => result
        .Should().BeOfType<OkObjectResult>().Subject.Value
        .Should().BeAssignableTo<IReadOnlyList<NoteResponse>>().Subject;

    private static NoteResponse GetResponse(IActionResult result) => result.Should().BeOfType<OkObjectResult>().Subject.Value
        .Should().BeOfType<NoteResponse>().Subject;
}
