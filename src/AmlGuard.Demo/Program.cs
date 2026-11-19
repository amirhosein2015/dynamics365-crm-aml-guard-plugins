using System;
using System.Text;

namespace AmlGuard.Demo
{
    internal static class Program
    {
        private static int Main()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("AML Guard demo. Fictional data, in-memory Dataverse, real plug-in classes.");
            Console.WriteLine("Lines without a prefix are the plug-in trace output (ITracingService).");

            new DemoScenario(new DemoEnvironment()).Run();
            return 0;
        }
    }
}
