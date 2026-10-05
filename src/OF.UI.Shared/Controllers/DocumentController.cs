using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OF.UI.Database;

namespace OF.UI.Shared.Controllers
{
    [Authorize]
    public class DocumentController : CommonController
    {
        public DocumentController(IDataRepository dataRepository) : base(dataRepository)
        {
        }

        [Route("Document/{documentId}")]
        public IActionResult Index(string documentId)
        {
            if (string.IsNullOrEmpty(documentId))
            {
                return NotFound();
            }

            var user = User.Identity.Name;
 
            return View();
        }
    }
}
