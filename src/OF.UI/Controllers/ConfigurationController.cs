using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using OF.Common.Infrastructure.IPG.Pricing.Models;
using OF.Common.Infrastructure.MDP.Services;
using OF.Data.Database;
using OF.UI.Database;
using OF.UI.Identity;
using OF.UI.Models;

namespace ExternalConfigurator.Controllers
{
    [Authorize()]
    public class ConfigurationController : Controller
    {
        private readonly IDataRepository dataRepository;
        private readonly ILogger<ConfigurationController> _logger;
        private readonly IConfiguration _configuration;
        private readonly ISalesforceUserLanguageService _salesforceUserLanguageService;
        private readonly IUserIdentity _userIdentity;

        public ConfigurationController(
            IDataRepository dataRepository,
            ILogger<ConfigurationController> logger,
            IConfiguration configuration,
            ISalesforceUserLanguageService salesforceUserLanguageService,
            IUserIdentity userIdentity)
        {
            this.dataRepository = dataRepository;
            _logger = logger;
            _configuration = configuration;
            _salesforceUserLanguageService = salesforceUserLanguageService;
            _userIdentity = userIdentity;
        }

        public async Task<IActionResult> Selection(string agreementNumber, string json, string quantity)
        {
            var agreement = dataRepository.GetAgreement(agreementNumber);
            var query = GetFromQuery(Request.Query, agreement, json, quantity);

            var language = await _salesforceUserLanguageService.GetUserLanguageAsync(_userIdentity.GetIdentity().LoginName);
            var theLanguage = !String.IsNullOrEmpty(language?.LanguageLocaleKey) ? language.LanguageLocaleKey : "en";
            return View(new SelectionModel { Language = theLanguage, Header = agreement, Query = query });
        }

        public async Task<IActionResult> Index(string agreementNumber, string genericCode, string json, string quantity)
        {
            var agreement = dataRepository.GetAgreement(agreementNumber);
            var query = GetFromQuery(Request.Query, agreement, json, quantity);

            var language = await _salesforceUserLanguageService.GetUserLanguageAsync(_userIdentity.GetIdentity().LoginName);
            var theLanguage = !String.IsNullOrEmpty(language?.LanguageLocaleKey) ? language.LanguageLocaleKey : "en";
            return View(new IndexModel { Language = theLanguage, Header = agreement, GenericCode = genericCode, Query = query });
        }

        private AgreementQuery GetFromQuery(IQueryCollection query, Header agreement, string json, string quantity)
        {
            var pricingDetails = JsonConvert.DeserializeObject<PricingRequest>(json);
            pricingDetails.Id = Guid.NewGuid().ToString();
            pricingDetails.Quote.Details.Id = Guid.NewGuid().ToString();
            pricingDetails.Quote.Lines[0].Id = Guid.NewGuid().ToString();
            decimal quantityParsed = 1;
            decimal.TryParse(quantity, out quantityParsed);
            return new AgreementQuery(
                AgreementNumber: query["agreementNumber"].ToString(),
                Attributes: query["attributes"].ToString(),
                PricingRequest: pricingDetails,
                LineId: query["lineId"],
                Quantity: quantityParsed
            );
        }
    }
}
