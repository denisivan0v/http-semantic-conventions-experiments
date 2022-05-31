namespace HttpClientApp
{
    /// <summary>
    /// Defines names of DiagnosticListener and Write events for DiagnosticHandler
    /// </summary>
    internal static class DiagnosticsHandlerLoggingStrings
    {
        public const string ImprovedDiagnosticListenerName = "ImprovedHttpHandlerDiagnosticListener";
        public const string OriginalDiagnosticListenerName = "OriginalHttpHandlerDiagnosticListener";
        public const string RequestWriteNameDeprecated = "System.Net.Http.Request";
        public const string ResponseWriteNameDeprecated = "System.Net.Http.Response";

        public const string ExceptionEventName = "System.Net.Http.Exception";
        public const string ActivityName = "System.Net.Http.HttpRequestOut";
        public const string ActivityStartName = "System.Net.Http.HttpRequestOut.Start";

        public const string RequestIdHeaderName = "Request-Id";
        public const string CorrelationContextHeaderName = "Correlation-Context";

        public const string TraceParentHeaderName = "traceparent";
        public const string TraceStateHeaderName = "tracestate";
    }
}