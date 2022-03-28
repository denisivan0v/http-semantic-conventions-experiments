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
        {
            AppContext.SetSwitch("System.Net.Http.EnableActivityPropagation", false);
            
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        }
    }

    [MemoryDiagnoser]
    public abstract class Benchmarks : IDisposable
    {
        protected const string Url = "http://localhost:19999/api/request/";

        private TracerProvider _openTelemetryOriginal;
        private TracerProvider _openTelemetryImproved;
        private SampleServer _server;

        protected ActivitySource ActivitySource;

        protected Benchmarks()
        {
            _openTelemetryOriginal = Sdk.CreateTracerProviderBuilder()
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("dotnet-app"))
                .AddOriginalHttpClientInstrumentation()
                .AddSource("Samples.SampleClient", "Samples.SampleServer")
                // .AddJaegerExporter(o =>
                // {
                //     o.AgentHost = "localhost";
                //     o.AgentPort = 6831;
                //
                //     // Examples for the rest of the options, defaults unless otherwise specified
                //     // Omitting Process Tags example as Resource API is recommended for additional tags
                //     o.MaxPayloadSizeInBytes = 4096;
                //
                //     // Using Batch Exporter (which is default)
                //     // The other option is ExportProcessorType.Simple
                //     o.ExportProcessorType = ExportProcessorType.Simple;
                // })
                .Build();
            _openTelemetryImproved = Sdk.CreateTracerProviderBuilder()
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("dotnet-app"))
                .AddImprovedHttpClientInstrumentation()
                .AddSource("Samples.SampleClient", "Samples.SampleServer")
                // .AddJaegerExporter(o =>
                // {
                //     o.AgentHost = "localhost";
                //     o.AgentPort = 6831;
                //
                //     // Examples for the rest of the options, defaults unless otherwise specified
                //     // Omitting Process Tags example as Resource API is recommended for additional tags
                //     o.MaxPayloadSizeInBytes = 4096;
                //
                //     // Using Batch Exporter (which is default)
                //     // The other option is ExportProcessorType.Simple
                //     o.ExportProcessorType = ExportProcessorType.Simple;
                // })
                .Build();
            _server = new SampleServer();
            _server.Start(Url);
            ActivitySource = new ActivitySource("Samples.SampleClient");
        }
        
        public virtual void Dispose()
        {
            ActivitySource.Dispose();
            ActivitySource.Dispose();
            _server.Dispose();
            _openTelemetryOriginal.Dispose();
            _openTelemetryImproved.Dispose();
        }
    }
    
    public class BenchmarksHandler : Benchmarks
    {
        private readonly HttpClient _clientOriginal;
        private readonly HttpClient _clientImproved;

        public BenchmarksHandler() 
        {
            _clientOriginal = new HttpClient(new SimpleRetryHandler(new DiagnosticsHandler(new SocketsHttpHandler(), "OriginalHttpHandlerDiagnosticListener")));
            _clientImproved = new HttpClient(new SimpleRetryHandler(new DiagnosticsHandler(new SocketsHttpHandler(), "ImprovedHttpHandlerDiagnosticListener")));
        }

        [Benchmark(Baseline = true)]
        public async Task HandlerOriginal() 
        {
            using var activity = ActivitySource.StartActivity(ActivityKind.Internal);
            var content = new StringContent($"client message: {DateTime.Now}", Encoding.UTF8);
            using var response = await _clientOriginal.PostAsync(Url, content).ConfigureAwait(false);
        }

        [Benchmark]
        public async Task HandlerImproved() 
        {
            using var activity = ActivitySource.StartActivity(ActivityKind.Internal);
            var content = new StringContent($"client message: {DateTime.Now}", Encoding.UTF8);
            using var response = await _clientImproved.PostAsync(Url, content).ConfigureAwait(false);
        }

        public override void Dispose() 
        {
            base.Dispose();
            _clientOriginal.Dispose();
            _clientImproved.Dispose();
        }
    }
    
    public class BenchmarksPolly : Benchmarks
    {
        private readonly HttpClient _clientOriginal;
        private readonly HttpClient _clientImproved;
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;

        public BenchmarksPolly()
        {
            _clientOriginal = new HttpClient(new DiagnosticsHandler(new SocketsHttpHandler(), "OriginalHttpHandlerDiagnosticListener"));
            _clientImproved = new HttpClient(new DiagnosticsHandler(new SocketsHttpHandler(), "ImprovedHttpHandlerDiagnosticListener"));
            _retryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(message => !message.IsSuccessStatusCode)
                .RetryAsync(3);                        
        }

        [Benchmark(Baseline = true)]
        public async Task PollyOriginal() 
        {
            using var activity = ActivitySource.StartActivity(ActivityKind.Internal);
            var content = new StringContent($"client message: {DateTime.Now}", Encoding.UTF8);
            using var response = await _retryPolicy.ExecuteAsync(async () => await _clientOriginal.PostAsync(Url, content).ConfigureAwait(false)).ConfigureAwait(false);
        }

        [Benchmark]
        public async Task PollyImproved() 
        {
            using var activity = ActivitySource.StartActivity(ActivityKind.Internal);
            var content = new StringContent($"client message: {DateTime.Now}", Encoding.UTF8);
            using var response = await _retryPolicy.ExecuteAsync(async () => await _clientImproved.PostAsync(Url, content).ConfigureAwait(false)).ConfigureAwait(false);
        }

        public override void Dispose() 
        {
            base.Dispose();
            _clientOriginal.Dispose();
            _clientImproved.Dispose();
        }
    }
}