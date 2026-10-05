using OF.Common.Infrastructure.IPG.Orders.Auth;
using OF.Common.Infrastructure.IPG.Orders.Settings;
using System.Net.Http.Headers;

namespace OF.Common.Infrastructure.IPG.Orders.Handlers
{
    public sealed class OAuthHttpHandler<TSettings> : DelegatingHandler
        where TSettings : IntegrationClientSettings
    {
        private readonly OAuthTokenProvider _tokenProvider;
        private readonly string _audience;

        public OAuthHttpHandler(OAuthTokenProvider tokenProvider, TSettings clientSettings)
        {
            _tokenProvider = tokenProvider;
            _audience = clientSettings.Audience;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var bearer = await _tokenProvider.GetBearerTokenAsync(_audience, cancellationToken);

            if (bearer == null)
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized);
            }

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            return await base.SendAsync(request, cancellationToken);
        }
    }
}
