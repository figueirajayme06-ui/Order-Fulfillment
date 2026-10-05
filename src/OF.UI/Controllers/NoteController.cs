using OF.UI.Database;
using OF.UI.Identity;
using OF.UI.Models;
using Infragistics.Web.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OF.Data.Database;

namespace OF.UI.Controllers
{
    [Route("/api/[controller]")]
    [ApiController]
    [Authorize]
    public class NoteController : ControllerBase
    {
        private readonly IDataRepository _repository;
        private readonly IUserIdentity _userIdentity;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public NoteController(IDataRepository repository, IUserIdentity userIdentity, IHttpContextAccessor httpContextAccessor)
        { 
            _repository = repository;
            _userIdentity = userIdentity;
            _httpContextAccessor = httpContextAccessor;
        }


        [HttpGet]
        [Route("GetNotes")]
        public ActionResult GetNotes(string noteType, string key)
        {
            //Join to the users table to get the user's full name
            return new ObjectResult(_repository.GetNotes(noteType, key).Join(_repository.GetUsers(), n => n.LastUpdatedBy, u => u.LoginName, (n, u) => new
            {
                Note = n,
                User = u
            }));
        }

        [HttpGet]
        [Route("GetNoteCount")]
        public ActionResult GetNoteCount(string noteType, string key)
        {
            return new ObjectResult(_repository.GetNotes(noteType, key).Count());
        }

        [HttpPost]
        [Route("EditNote")]
        public ActionResult<EditNoteResponse> DeleteNote([FromBody] EditNoteRequest request)
        {
            try
            {
                var id = _userIdentity.GetIdentity();

                if (id == null)
                {
                    return new BadRequestResult();
                }

                var note = _repository.GetNote(request.Id);
                if (note == null)
                {
                    return new NotFoundResult();
                }

                note.Note1 = request.Note;

                _repository.UpdateNote(_userIdentity, note);

                return new OkObjectResult(new EditNoteResponse()
                {
                    Id = note.Id,
                    IsSuccess = true,
                    ErrorMessage = String.Empty
                });
            }
            catch (Exception ex)
            {
                return new OkObjectResult(new EditNoteResponse()
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        [HttpDelete]
        [Route("DeleteNote")]
        public ActionResult<DeleteNoteResponse> DeleteNote([FromBody] DeleteNoteRequest request)
        {
            try
            {
                var id = _userIdentity.GetIdentity();

                if (id == null)
                {
                    return new BadRequestResult();
                }

                var note = _repository.GetNote(request.Id);
                if (note == null)
                {
                    return new NotFoundResult();
                }

                _repository.DeleteNote(_userIdentity, note);

                return new OkObjectResult(new DeleteNoteResponse()
                {
                    IsSuccess = true,
                    ErrorMessage = String.Empty
                });
            }
            catch (Exception ex)
            {
                return new OkObjectResult(new DeleteNoteResponse()
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        [HttpPut]
        [Route("CreateNote")]
        public ActionResult<CreateNoteResponse> CreateNote([FromBody] CreateNoteRequest request)
        {
            try
            {
                var id = _userIdentity.GetIdentity();

                if (id == null)
                {
                    return new BadRequestResult();
                }

                var note = new Note()
                {
                    NoteType = request.NoteType,
                    ParentId = request.Key,
                    Note1 = request.Note
                };

                _repository.CreateNote(_userIdentity, note);

                return new OkObjectResult(new CreateNoteResponse()
                {
                    Id = note.Id,
                    IsSuccess = true,
                    ErrorMessage = String.Empty
                });
            }
            catch (Exception ex)
            {
                return new OkObjectResult(new CreateNoteResponse()
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                });
            }
        }
    }
}
