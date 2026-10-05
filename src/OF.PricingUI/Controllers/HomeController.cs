using Microsoft.AspNetCore.Mvc;
using OF.UI.Database;
using OF.UI.Shared.Controllers;

namespace OF.PricingUI.Controllers
{
    public class HomeController : CommonController
    {
        public HomeController(IDataRepository dataRepository) : base(dataRepository)
        {
        }

        public IActionResult Index()
        {
            return View();
        }
    }
}
