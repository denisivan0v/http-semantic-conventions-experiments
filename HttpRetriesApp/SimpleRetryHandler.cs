using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HttpClientApp
{
    public class SimpleRetryHandler : DelegatingHandler
    {
        private const int MaxRetries = 5;

        public SimpleRetryHandler(HttpMessageHandler innerHandler)
            : base(innerHandler)
        { }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            // Handle request stream appropriately (e.g. by buffering content)
            HttpResponseMessage response = null;
            for (int i = 0; i < MaxRetries; i++)
            {
                response = await base.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode) {
                    return response;
                }
            }

            return response;
        }
    }

}