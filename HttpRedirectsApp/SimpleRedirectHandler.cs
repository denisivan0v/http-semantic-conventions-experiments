using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HttpRedirectsApp
{
    public class SimpleRedirectHandler : DelegatingHandler
    {
        private const int MaxRedirects = 5;

        public SimpleRedirectHandler(HttpMessageHandler innerHandler)
            : base(innerHandler)
        { }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            // Handle request stream appropriately (e.g. by buffering content)
            HttpResponseMessage response = null;
            for (int i = 0; i < MaxRedirects; i++)
            {
                response = await base.SendAsync(request, cancellationToken);
                if (response.Headers.Location is null ||
                    response.StatusCode is not 
                    HttpStatusCode.Moved or
                    HttpStatusCode.Redirect or 
                    HttpStatusCode.TemporaryRedirect or 
                    HttpStatusCode.PermanentRedirect) 
                {
                    return response;
                }

                request.RequestUri = new Uri(response.Headers.Location.AbsoluteUri);
            }

            return response;
        }
    }
}