using System;

namespace HttpClientApp
{
    class Program
    {
        static void Main(string[] args)
        {
            TestJaegerExporter.Run("localhost", 6831);
        }
    }
}