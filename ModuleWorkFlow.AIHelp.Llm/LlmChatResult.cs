namespace ModuleWorkFlow.AIHelp.Llm
{
    public sealed class LlmChatResult
    {
        public string Content { get; set; }

        public int InputTokens { get; set; }

        public int OutputTokens { get; set; }

        public int TotalTokens { get; set; }

        public string RawResponse { get; set; }
    }
}
