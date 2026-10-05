using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using OF.UI.Database;
using OF.UI.Shared.Controllers;
using System.Security.Claims;
using OF.PricingUI.ViewModels;

namespace OF.PricingUI.Controllers
{
    public class LoginController : CommonController
    {
        public LoginController(IDataRepository dataRepository) : base(dataRepository)
        {
        }

        public IActionResult Index(string returnUrl)
        {
            return View(new LoginViewModel() { ReturnUrl = returnUrl });
        }

        [HttpPost]
        public async Task<IActionResult> Index(string email, string code, string returnUrl)
        {
            var isValidCode = ValidateCode(email, code);

            if (isValidCode)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, email),
                };

                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

                return Redirect(returnUrl); 
            }

            return View("Login", new LoginViewModel() { ReturnUrl = returnUrl });
        }

        private bool ValidateCode(string email, string code)
        {
            return true;
        }
    }
}
