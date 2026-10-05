using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using OF.UI.Database;
using System.Reflection;

namespace OF.UI.Shared.Controllers
{
    public class CommonController : Controller
    {
        private readonly IDataRepository _repository;

        public CommonController(IDataRepository repository)
        {
            _repository = repository;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            base.OnActionExecuting(context);

            ViewBag.DataRefreshes = _repository.GetRefreshes();

            Assembly assembly = Assembly.GetExecutingAssembly();
            ViewBag.Version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? assembly.GetName().Version?.ToString();
        }
    }
}
