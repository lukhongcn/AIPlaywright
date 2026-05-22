using System;
using System.Net;
using System.Threading.Tasks;
using ModuleWorkFlow.PlaywrightRunner.Actions.Orders;

namespace ModuleWorkFlow.PlaywrightRunner
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            RunAsync(args).GetAwaiter().GetResult();
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
    }
}
