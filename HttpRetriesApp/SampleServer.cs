using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace HttpClientApp {
    public class SampleServer : IDisposable 
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
                            context.Response.StatusCode = 404;
                        }
                        else
                        {
                            string requestContent;
                            using (var childSpan = source.StartActivity("ReadRequestStream", ActivityKind.Consumer))
                            using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
                            {
                                requestContent = reader.ReadToEnd();
                                childSpan.AddEvent(new ActivityEvent("StreamReader.ReadToEnd"));
                            }

                            activity?.SetTag("request.content", requestContent);
                            activity?.SetTag("request.length", requestContent.Length.ToString());

                            var echo = Encoding.UTF8.GetBytes("echo: " + requestContent);
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
            ((IDisposable) _listener).Dispose();
        }
    }
}