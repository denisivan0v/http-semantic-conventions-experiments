using System;

namespace HttpClientApp
{
    class Program
    {
        static void Main(string[] args)
        {
            AppContext.SetSwitch("System.Net.Http.EnableActivityPropagation", false);
            
            TestJaegerExporter.Run("localhost", 6831);
        }
    }
}