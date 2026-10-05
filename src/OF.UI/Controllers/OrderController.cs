using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OF.Common.Infrastructure.Storage;
using OF.UI.Database;
using OF.UI.Identity;

namespace OF.UI.Controllers
{
    [Route("/api/[controller]")]
    [ApiController]
    [Authorize]
    public class OrderController : ControllerBase
    {
        private readonly UpdateAgreementQueueClient _updateAgreementQueue;
        private readonly UpsertQuoteQueueClient _upsertQuoteQueue;
        private readonly ActivateAgreementQueueClient _activateHeaderQueue;
        private readonly IDataRepository _dataRepository;
        private readonly IUserIdentity _userIdentity;
        private readonly ILogger<OrderController> _logger;

        public OrderController( 
            UpdateAgreementQueueClient updateAgreementQueue,
            UpsertQuoteQueueClient upsertQuoteQueue,
            ActivateAgreementQueueClient activateHeaderQueue,
            IDataRepository dataRepository,
            IUserIdentity userIdentity,
            ILogger<OrderController> logger)
        { 
            _updateAgreementQueue = updateAgreementQueue;
            _upsertQuoteQueue = upsertQuoteQueue;
            _activateHeaderQueue = activateHeaderQueue;
            _dataRepository = dataRepository;
            _userIdentity = userIdentity;
            _logger = logger;
        }

        /// <summary>
        /// Activates an Order for a given headerId
        /// </summary>
        /// <param name="headerId">Comes from the url of the request</param>
        /// <returns></returns>
        [HttpPost]
        [Route("Activate/{headerId}")]
        public async Task<ActionResult> Activate(int headerId)
        {
            await _dataRepository.SetHeaderForActivation(headerId, _userIdentity);
            await _activateHeaderQueue.QueueActivation(headerId);

            return new OkObjectResult(new { type = "Activation" });
        }

        /// <summary>
        /// Abandon a Quote only for a given headerId
        /// </summary>
        /// <param name="headerId">Comes from the url of the request</param>
        /// <returns></returns>
        [HttpDelete]
        [Route("Abandon/{headerId}")]
        public ActionResult Abandon(int headerId)
        {
            var loginName = _userIdentity.GetIdentity().LoginName;

            _logger.LogInformation(
                "Abandon requested for HeaderId {HeaderId} by {DeletedBy}",
                headerId,
                loginName);

            var audit = _dataRepository.DeleteHeaderAndReservationsFromId(headerId, loginName);

            _logger.LogWarning(
                "Abandon completed: HeaderId={HeaderId}, Quote={QuotePublicId}, DeletedBy={DeletedBy}, HeaderWasDeleted={WasHeaderAlreadyDeleted}, ActiveLinesMarkedDeleted={ActiveLinesMarkedDeleted}, AlreadyDeletedLines={AlreadyDeletedLines}, ReservationsDeleted={ReservationsDeleted}, DeletedAtUtc={DeletedAtUtc}",
                audit.HeaderId,
                audit.QuotePublicId,
                audit.DeletedBy,
                audit.WasHeaderAlreadyDeleted,
                audit.ActiveLinesMarkedDeleted,
                audit.AlreadyDeletedLines,
                audit.ReservationsDeleted,
                audit.DeletedAtUtc);

            return new OkObjectResult(new { type = "Activation" });
        }

        /// <summary>
        /// Cancel activation of non sub-lines for a given headerId
        /// </summary>
        /// <param name="headerId">Comes from the url of the request</param>
        /// <returns></returns>
        [HttpPost]
        [Route("CancelActivation/{headerId}")]
        public ActionResult CancelActivation(int headerId)
        {
            _dataRepository.TimeoutSublineActivation(headerId, _userIdentity);
            return new OkObjectResult(new { type = "CancelActivation" });
        }

        /// <summary>
        /// Put a message on the queue to pull a quote or agreement
        /// </summary>
        /// <param name="number"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("Pull/{number}")]
        public async Task<ActionResult> PullDataForQuoteOrAgreementByNumber(string number)
        {
            number = number.ToUpper();

            if (number.StartsWith("Q"))
            {
                await _upsertQuoteQueue.QueueQuote(number);
                return new OkObjectResult(new { type = "Quote" });
            }

            if (number.StartsWith("A") || number.StartsWith("T"))
            {
                await _updateAgreementQueue.QueueAgreement(number);
                return new OkObjectResult(new { type = "Agreement" });
            }

            return new BadRequestResult();
        }
    }
}
