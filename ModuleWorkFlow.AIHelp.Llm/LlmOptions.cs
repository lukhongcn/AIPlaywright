namespace ModuleWorkFlow.AIHelp.Llm
{
    public sealed class LlmOptions
    {
        public string Region { get; set; } = "cn";

        public string BaseUrl { get; set; } = "https://llm-jqrqzdbt1aa6kqyq.cn-beijing.maas.aliyuncs.com/compatible-mode/v1";

        public string ApiKey { get; set; } = "";

        public string Model { get; set; } = "qwen3.6-plus";

        public double Temperature { get; set; } = 0.1;

        public bool EnableThinking { get; set; } = false;

        public int TimeoutSeconds { get; set; } = 60;
    }
}