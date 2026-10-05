using OF.UI.Database;
using OF.UI.Identity;
using Infragistics.Web.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OF.UI.Controllers
{
    [Route("/api/[controller]")]
    [ApiController]
    [Authorize]
    public class ProductController : ControllerBase
    {
        private readonly IDataRepository _repository;
        private readonly IUserIdentity _userIdentity;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ProductController(IDataRepository repository, IUserIdentity userIdentity, IHttpContextAccessor httpContextAccessor)
        { 
            _repository = repository;
            _userIdentity = userIdentity;
            _httpContextAccessor = httpContextAccessor;
        }


        [HttpGet]
        [Route("ProductFamilies")]
        public ActionResult GetProductFamilies()
        {
            return new ObjectResult(_repository.GetProductFamilies().Select(t => new { Id = t.Id, Text = t.FamilyDescription }));
        }

        [HttpGet]
        [Route("ProductLines")]
        public ActionResult GetProductLines()
        {
            return new ObjectResult(_repository.GetProductLines().Select(t => new { Id = t.Id, Text = t.LineDescription, ParentId = t.FamilyId, GroupText = t.Family.FamilyDescription }));
        }

        [HttpGet]
        [Route("Generics")]
        public ActionResult GetGenerics(string division)
        {
            return new ObjectResult(_repository.GetActiveGenerics(division).Select(t => new { Id = t.Id, Text = t.GenericCode + " " + t.GenericDescription, ParentId = t.LineId, Code = t.GenericCode }));
        }

        [HttpGet]
        [Route("ItemNumbers")]
        public ActionResult GetItemNumbers(string division)
        {
            return new ObjectResult(_repository.GetActiveItems(division).OrderBy(x => x.ItemNumber).Select(x => new { Id = x.ItemNumber, Text = x.ItemNumber + " " + x.DescriptionIntl, ParentId = x.GenericId }));
        }

        [HttpGet]
        [Route("ItemNumbersAttributes")]
        public ActionResult GetItemNumbersAttributes(string division, int genericId, string? attributes)
        {
            return new ObjectResult(_repository.GetItemsForGenericAndAttributes(division, genericId, attributes).OrderBy(x => x.ItemNumber).Select(x => new { Id = x.ItemNumber, Text = x.ItemNumber + " " + x.DescriptionIntl, ParentId = x.GenericId }));
        }

        [HttpGet]
        [Route("Attributes")]
        public ActionResult GetProductAttributes(int genericId)
        {
            return new ObjectResult(_repository.GetProductAttributes(genericId));
        }
    }
}
