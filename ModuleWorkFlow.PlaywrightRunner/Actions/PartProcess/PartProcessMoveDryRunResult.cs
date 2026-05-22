using System.Collections.Generic;

namespace ModuleWorkFlow.PlaywrightRunner.Actions.PartProcess
{
    public sealed class PartProcessMoveDryRunResult
    {
        public string Action { get; set; }

        public bool Executed { get; set; }

        public bool Success { get; set; }

        public string Status { get; set; }

        public string Message { get; set; }

        public string CurrentUrl { get; set; }

        public string ScreenshotPath { get; set; }

        public bool Saved { get; set; }

        public int? FromPosition { get; set; }

        public int? ToPosition { get; set; }

        public string Direction { get; set; }

        public int Steps { get; set; }

        public List<PartProcessItem> BeforeProcesses { get; set; } = new List<PartProcessItem>();

        public List<PartProcessItem> AfterProcesses { get; set; } = new List<PartProcessItem>();
    }

    public sealed class PartProcessItem
    {
        public int Position { get; set; }

        public string OrderNo { get; set; }

        public string ProcessName { get; set; }
    }
}
