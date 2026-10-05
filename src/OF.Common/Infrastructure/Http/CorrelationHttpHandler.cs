using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace OF.Common.Infrastructure.Http
{
    /// <summary>
    /// Adds a correlation ID to outgoing requests and logs it before the request is sent and against any registered response headers.
    /// </summary>
    public class CorrelationHttpHandler : DelegatingHandler
    {
        private readonly ILogger _logger;

        public CorrelationHttpHandler(ILogger<CorrelationHttpHandler> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private bool TryGetCorrelationId(HttpRequestMessage request, [MaybeNullWhen(false)] out string correlationId)
        {
            var success = request.Options.TryGetValue(new(Constants.Infrastructure.Http.CorrelatableOptionsKey), out ICorrelatable? correlatable);
            correlationId = success && !string.IsNullOrWhiteSpace(correlatable!.CorrelationId) ? correlatable.CorrelationId : default;
            return success;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            const string headerName = "x-correlation-id";

            if (TryGetCorrelationId(request, out var correlationId))
            {
                _logger.LogInformation($"Adding correlation id [{correlationId}]");
                request.Headers.Remove(headerName);
                request.Headers.Add(headerName, correlationId);
            }
            else
            {
                _logger.LogDebug($"No correlation id found to inject!");
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}