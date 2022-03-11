using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Polly;
using Polly.Extensions.Http;
using Polly.Retry;

namespace HttpClientApp
{
    class Program 
    {
        static void Main(string[] args)
            => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
    
    [MemoryDiagnoser]
    public abstract class Benchmarks : IDisposable
    {
        protected const string _url = "http://localhost:19999/api/request/";
        private TracerProvider _openTelemetryOrig;
        private TracerProvider _openTelemetryNew;
        private SampleServer _server;
        protected ActivitySource _activitySourceOrig;
        protected ActivitySource _activitySourceNew;
        
        protected Benchmarks() 
        {
            _openTelemetryOrig = Sdk.CreateTracerProviderBuilder()
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("dotnet-app"))
                .AddHttpClientInstrumentation(true)
                .AddSource("Samples.SampleClient.Orig", "Samples.SampleServer")
                .AddJaegerExporter(o =>
                {
                    o.AgentHost = "localhost";
                    o.AgentPort = 6831;

                    // Examples for the rest of the options, defaults unless otherwise specified
                    // Omitting Process Tags example as Resource API is recommended for additional tags
                    o.MaxPayloadSizeInBytes = 4096;

                    // Using Batch Exporter (which is default)
                    // The other option is ExportProcessorType.Simple
                    o.ExportProcessorType = ExportProcessorType.Simple;
                })
                .Build();
            _openTelemetryNew = Sdk.CreateTracerProviderBuilder()
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("dotnet-app"))
                .AddHttpClientInstrumentation(false)
                .AddSource("Samples.SampleClient.New", "Samples.SampleServer")
                .AddJaegerExporter(o =>
                {
                    o.AgentHost = "localhost";
                    o.AgentPort = 6831;

                    // Examples for the rest of the options, defaults unless otherwise specified
                    // Omitting Process Tags example as Resource API is recommended for additional tags
                    o.MaxPayloadSizeInBytes = 4096;

                    // Using Batch Exporter (which is default)
                    // The other option is ExportProcessorType.Simple
                    o.ExportProcessorType = ExportProcessorType.Simple;
                })
                .Build();
            _server = new SampleServer();
            _server.Start(_url);
            _activitySourceOrig = new ActivitySource("Samples.SampleClient.Orig");
            _activitySourceNew = new ActivitySource("Samples.SampleClient.New");
        }

        public virtual void Dispose()
        {
            _activitySourceOrig.Dispose();
            _activitySourceNew.Dispose();
            _server.Dispose();
            _openTelemetryOrig.Dispose();
            _openTelemetryNew.Dispose();
        }
    }

    public class BenchmarksHandler : Benchmarks
    {
        private HttpClient _clientHandler;

        public BenchmarksHandler() 
        {
            _clientHandler = new HttpClient(new SimpleRetryHandler(new HttpClientHandler()));
        }
        
        [Benchmark(Baseline = true)]
        public async Task HandlerOriginal() 
        {
            using var activity = _activitySourceOrig.StartActivity(ActivityKind.Internal);
            var content = new StringContent($"client message: {DateTime.Now}", Encoding.UTF8);
            using var response = await _clientHandler.PostAsync(_url, content).ConfigureAwait(false);
        }

        [Benchmark]
        public async Task HandlerNew() 
        {
            using var activity = _activitySourceNew.StartActivity(ActivityKind.Internal);
            var content = new StringContent($"client message: {DateTime.Now}", Encoding.UTF8);
            using var response = await _clientHandler.PostAsync(_url, content).ConfigureAwait(false);
        }

        public override void Dispose() 
        {
            base.Dispose();
            _clientHandler.Dispose();
        }
    }
    
    public class BenchmarksPolly : Benchmarks
    {
        private HttpClient _clientPolly;
        private AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;

        public BenchmarksPolly()
        {
            _clientPolly = new HttpClient();
            _retryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(message => !message.IsSuccessStatusCode)
                .RetryAsync(3);                        
        }
        
        [Benchmark(Baseline = true)]
        public async Task PollyOriginal() 
        {
            using var activity = _activitySourceOrig.StartActivity(ActivityKind.Internal);
            var content = new StringContent($"client message: {DateTime.Now}", Encoding.UTF8);
            using var response = await _retryPolicy.ExecuteAsync(async () => await _clientPolly.PostAsync(_url, content).ConfigureAwait(false)).ConfigureAwait(false);
        }

        [Benchmark]
        public async Task PollyNew() 
        {
            using var activity = _activitySourceNew.StartActivity(ActivityKind.Internal);
            var content = new StringContent($"client message: {DateTime.Now}", Encoding.UTF8);
            using var response = await _retryPolicy.ExecuteAsync(async () => await _clientPolly.PostAsync(_url, content).ConfigureAwait(false)).ConfigureAwait(false);
        }
        
        public override void Dispose() 
        {
            base.Dispose();
            _clientPolly.Dispose();
        }
    }
}