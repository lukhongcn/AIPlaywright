namespace ModuleWorkFlow.PlaywrightRunner.Actions.PartProcess
{
    public sealed class PartProcessMoveDryRunRequest
    {
        public string BaseUrl { get; set; }

        public string ModuleId { get; set; }

        public string PartNo { get; set; }

        public string PartNoList { get; set; }

        public string MenuId { get; set; }

        public string PageIndex { get; set; }

        public string ListUrl { get; set; }

        public int? FromPosition { get; set; }

        public int? ToPosition { get; set; }

        public string Direction { get; set; }

        public int Steps { get; set; }

        public string ProcessName { get; set; }
    }
}
