using System;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HttpRedirectsApp
{
    internal class InstrumentationWithActivitySource : IDisposable
    {
        private const string RequestPath = "/api/request";
        private readonly SampleServer _server = new SampleServer();
        private readonly SampleClient _client = new SampleClient();

        public void Start(ushort port = 19999)
        {
            var url = $"http://localhost:{port.ToString(CultureInfo.InvariantCulture)}{RequestPath}/";
            _server.Start(url);
            _client.Start(url);
        }

        public void Dispose()
        {
            _client.Dispose();
            _server.Dispose();
        }

        private class SampleServer : IDisposable
        {
            private readonly HttpListener _listener = new HttpListener();

            public void Start(string url)
            {
                _listener.Prefixes.Add(url);
                _listener.Start();
                
                Task.Run(() =>
                {
                    using var source = new ActivitySource("Samples.SampleServer");

                    var requestCount = 1;
                    while (_listener.IsListening)
                    {
                        try
                        {
                            var context = _listener.GetContext();
                            
                            var activityContext = ActivityContext.Parse(
                                context.Request.Headers["traceparent"],
                                context.Request.Headers["tracestate"]);
                            
                            using var activity = source.StartActivity(
                                $"{context.Request.HttpMethod}:{context.Request.Url.AbsolutePath}",
                                ActivityKind.Server,
                                activityContext);
                            
                            var headerKeys = context.Request.Headers.AllKeys;
                            foreach (var headerKey in headerKeys)
                            {
                                string headerValue = context.Request.Headers[headerKey];
                                activity?.SetTag($"http.header.{headerKey}", headerValue);
                            }

                            if (requestCount % 4 != 0)
                            {
                                context.Response.StatusCode = 301;
                                context.Response.RedirectLocation = $"{context.Request.Url.AbsoluteUri}redirect/{requestCount}/";
                            }
                            else
                            {
                                var echo = Encoding.UTF8.GetBytes("You've reached " + context.Request.Url);
                                context.Response.ContentEncoding = Encoding.UTF8;
                                context.Response.ContentLength64 = echo.Length;
                                context.Response.OutputStream.Write(echo, 0, echo.Length);
                            }

                            requestCount++;
                            
                            context.Response.Close();
                        }
                        catch (Exception)
                        {
                            // expected when closing the listener.
                        }
                    }
                });
            }

            public void Dispose()
            {
                ((IDisposable)_listener).Dispose();
            }
        }

        private class SampleClient : IDisposable
        {
            private CancellationTokenSource _cts;
            private Task _requestTask;

            public void Start(string url)
            {
                _cts = new CancellationTokenSource();
                var cancellationToken = _cts.Token;

                _requestTask = Task.Run(
                    async () =>
                    {
                        using var source = new ActivitySource("Samples.SampleClient");
                        
                        //using var client = new HttpClient();
                        using var client = new HttpClient(new SimpleRedirectHandler(new HttpClientHandler{ AllowAutoRedirect = false }));
                        
                        //while (!cancellationToken.IsCancellationRequested)
                        {
                            using var topLevelActivity = source.StartActivity(ActivityKind.Internal);

                            using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
                        }

                        try
                        {
                            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
                        }
                        catch (TaskCanceledException)
                        {
                            return;
                        }
                    },
                    cancellationToken);
            }

            public void Dispose()
            {
                if (_cts != null)
                {
                    _cts.Cancel();
                    _requestTask.Wait();
                    _requestTask.Dispose();
                    _cts.Dispose();
                }
            }
        }
    }
}