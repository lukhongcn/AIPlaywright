using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using ModuleWorkFlow.PlaywrightRunner.Actions.Orders;

namespace ModuleWorkFlow.PlaywrightRunner
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            Console.InputEncoding = new UTF8Encoding(false);
            Console.OutputEncoding = new UTF8Encoding(false);
            try
            {
                RunAsync(args).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                PrintException(ex);
                Environment.ExitCode = 1;
            }
        }

        private static async Task RunAsync(string[] args)
        {
            if (args != null && args.Length > 0 &&
                string.Equals(args[0], "--llm-test", StringComparison.OrdinalIgnoreCase))
            {
                await LlmTestRunner.RunAsync();
                return;
            }

            if (args != null && args.Length > 0 &&
                string.Equals(args[0], "--ai-login", StringComparison.OrdinalIgnoreCase))
            {
                await AiConsoleRunner.RunAsync();
                return;
            }

            if (args != null && args.Length > 0 &&
                string.Equals(args[0], "--order-new-dryrun", StringComparison.OrdinalIgnoreCase))
            {
                var result = await OrderDesignNewDryRunRunner.RunAsync(new OrderDesignNewDryRunRequest());
                JsonConsoleWriter.WriteResult(result);
                return;
            }

            await RequestJsonRunner.RunAsync(args);
        }

        private static void PrintException(Exception ex)
        {
            if (ex == null)
            {
                Console.Error.WriteLine("Unknown error.");
                return;
            }

            Console.Error.WriteLine("Unhandled exception:");
            Console.Error.WriteLine(ex.GetType().FullName);
            Console.Error.WriteLine(ex.Message);

            if (!string.IsNullOrWhiteSpace(ex.StackTrace))
            {
                Console.Error.WriteLine(ex.StackTrace);
            }

            var inner = ex.InnerException;
            while (inner != null)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine("Inner exception:");
                Console.Error.WriteLine(inner.GetType().FullName);
                Console.Error.WriteLine(inner.Message);

                if (!string.IsNullOrWhiteSpace(inner.StackTrace))
                {
                    Console.Error.WriteLine(inner.StackTrace);
                }

                inner = inner.InnerException;
            }
        }
    }
}
