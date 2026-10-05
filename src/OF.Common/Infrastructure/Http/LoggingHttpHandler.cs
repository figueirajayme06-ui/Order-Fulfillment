using Ardalis.GuardClauses;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.ExceptionServices;

namespace OF.Common.Infrastructure.Http
{
    public class LoggingHttpHandler : DelegatingHandler
    {
        private readonly ILogger<LoggingHttpHandler> _logger;

        public LoggingHttpHandler(ILogger<LoggingHttpHandler> logger)
        {
            _logger = Guard.Against.Null(logger, nameof(logger));
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var call = $"HTTP {request.Method} [{request.RequestUri}]";
            var stopwatch = Stopwatch.StartNew();
            var details = new Dictionary<string, object>();
            await AddRequest(details, request, "http-request-", "-before", cancellationToken);

            using (_logger.BeginScope(details))
            {
                try
                {
                    var response = await base.SendAsync(request, cancellationToken);

                    await AddRequest(details, request, "http-request-", "-after", cancellationToken);
                    await AddResponse(details, response, "http-response-", cancellationToken);

                    using (_logger.BeginScope(details))
                    {
                        string reason = $"{response.ReasonPhrase} ({(int)response.StatusCode})";

                        if (response.IsSuccessStatusCode)
                        {
                            _logger.LogInformation($"{call} was successful: {reason} after {stopwatch.Elapsed}");
                        }
                        else
                        {
                            var message = await response.ReadStringContent(1024, cancellationToken: cancellationToken);

                            _logger.LogWarning($"{call} was not successful: {reason} after {stopwatch.Elapsed} [{message}]");
                        }

                        return response;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"{call} was not successful: {ex.FullMessage()} after {stopwatch.Elapsed}");

                    ExceptionDispatchInfo.Capture(ex).Throw();
                    throw;
                }
            }
        }

        private static async Task AddRequest(
            IDictionary<string, object> details,
            HttpRequestMessage request,
            string prefix,
            string suffix,
            CancellationToken cancellationToken)
        {
            details[prefix + "headers" + suffix] = request.Headers;
            details[prefix + "request-uri" + suffix] = request.RequestUri;
            details[prefix + "method" + suffix] = request.Method;
            details[prefix + "content-headers" + suffix] = request.Content?.Headers;
            details[prefix + "content" + suffix] = await request.ReadStringContent(1024, cancellationToken: cancellationToken);
        }

        private static async Task AddResponse(
            IDictionary<string, object> details,
            HttpResponseMessage response,
            string prefix,
            CancellationToken cancellationToken)
        {
            details[prefix + "headers"] = response.Headers;
            details[prefix + "status"] = response.StatusCode;
            details[prefix + "status-code"] = (int)response.StatusCode;
            details[prefix + "reason"] = response.ReasonPhrase;
            details[prefix + "content-headers"] = response.Content?.Headers;
            details[prefix + "content"] = await response.ReadStringContent(1024, cancellationToken: cancellationToken);
        }
    }
}