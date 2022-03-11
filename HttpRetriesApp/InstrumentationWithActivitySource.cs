using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HttpClientApp
{
    internal class InstrumentationWithActivitySource : IDisposable
    {
        private const string RequestPath = "/api/request";
        private readonly SampleServer _server = new SampleServer();
        private readonly SampleClient _client = new SampleClient();

        public void Start(ushort port = 19999)
        {
            //var url = "https://api.publicapis.org/404";
            var url = $"http://localhost:{port.ToString(CultureInfo.InvariantCulture)}{RequestPath}/";
            _server.Start(url);
            _client.Start(url);
        }

        public void Dispose()
        {
            _client.Dispose();
            _server.Dispose();
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
                        using var client = new HttpClient(new SimpleRetryHandler(new HttpClientHandler()));
                        
                        //while (!cancellationToken.IsCancellationRequested)
                        {
                            using var activity = source.StartActivity(ActivityKind.Internal);

                            var content = new StringContent($"client message: {DateTime.Now}", Encoding.UTF8);
                            
                            using var response = await client.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
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