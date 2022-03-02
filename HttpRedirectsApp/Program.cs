using System;

namespace HttpRedirectsApp
{
    class Program
    {
        static void Main(string[] args)
        {
            TestJaegerExporter.Run("localhost", 6831);
        }
    }
}