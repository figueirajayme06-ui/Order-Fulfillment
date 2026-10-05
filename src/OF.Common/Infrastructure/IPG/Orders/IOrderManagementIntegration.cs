using OF.Common.Infrastructure.IPG.Orders.Models.Orders;
using OF.Common.Infrastructure.IPG.Orders.Models.Quotes;
using OF.Common.Infrastructure.IPG.Orders.Models.Quotes.Response;
using Refit;

namespace OF.Common.Infrastructure.IPG.Orders;

public interface IOrderManagementIntegration
{
    [Get("/query/?q={query}")]
    Task<SOQLResponse<T>> Query<T>(string query) where T : SfObjectBase;

    [Get("/query/?q={query}")]
    Task<HttpResponseMessage> QueryRaw<T>(string query) where T : SfObjectBase;

    [Post("/order/activation")]
    Task<HttpResponseMessage> OrderActivation([Body, Property(Constants.Infrastructure.Http.CorrelatableOptionsKey)] OrderActivationRequest body);

    [Post("/order/line")]
    Task<HttpResponseMessage> OrderLineCreate([Body, Property(Constants.Infrastructure.Http.CorrelatableOptionsKey)] OrderLineCreateRequest body);

    [Delete("/order/line")]
    Task<HttpResponseMessage> OrderLineDelete([Body, Property(Constants.Infrastructure.Http.CorrelatableOptionsKey)] OrderLineDeleteRequest body);

    [Put("/order/header/processStatus")]
    Task<HttpResponseMessage> OrderProcessStatus([Body] OrderProcessStatusRequest body);

    [Put("/order/line/processStatus")]
    Task<HttpResponseMessage> OrderLineProcessStatus([Body] OrderLineProcessStatusRequest body);

    [Put("/order/line")]
    Task<HttpResponseMessage> OrderLineUpdate([Body, Property(Constants.Infrastructure.Http.CorrelatableOptionsKey)] OrderLineUpdateRequest body);
}
