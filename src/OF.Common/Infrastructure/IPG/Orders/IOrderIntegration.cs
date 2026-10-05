using Refit;

namespace OF.Common.Infrastructure.IPG.Orders;

public interface IOrderIntegration
{
    [Get("/{program}/{transaction}")]
    Task<HttpResponseMessage> ExecuteM3Program(string program, string transaction, [Query] IDictionary<string, string> parameters, CancellationToken cancellationToken);
}
