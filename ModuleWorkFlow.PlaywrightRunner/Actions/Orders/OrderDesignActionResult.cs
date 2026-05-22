using System.Collections.Generic;

namespace ModuleWorkFlow.PlaywrightRunner.Actions.Orders
{
    public sealed class OrderDesignActionResult
    {
        public bool Executed { get; set; }

        public bool Success { get; set; }

        public string Status { get; set; }

        public string Message { get; set; }

        public string CurrentUrl { get; set; }

        public string PageTitle { get; set; }

        public string Action { get; set; }

        public string ScreenshotPath { get; set; }

        public Dictionary<string, object> Data { get; set; } = new Dictionary<string, object>();
    }
}
