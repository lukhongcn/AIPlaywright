using System;
using Newtonsoft.Json;

namespace ModuleWorkFlow.PlaywrightRunner
{
    internal static class JsonConsoleWriter
    {
        public static void WriteResult(bool success, string action, string message, string currentUrl)
        {
            Console.WriteLine(
                "{\"success\":" + success.ToString().ToLowerInvariant() +
                ",\"action\":\"" + EscapeJson(action) +
                "\",\"message\":\"" + EscapeJson(message) +
                "\",\"data\":{\"currentUrl\":\"" + EscapeJson(currentUrl) + "\"}}");
        }

        public static void WriteResult(Actions.Orders.OrderDesignActionResult result)
        {
            if (result == null)
            {
                WriteResult(false, string.Empty, "結果為空", string.Empty);
                return;
            }

            Console.WriteLine(JsonConvert.SerializeObject(result));
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }
    }
}
