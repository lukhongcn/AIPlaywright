using ModuleWorkFlow.AIHelp.Config;
using ModuleWorkFlow.AIHelp.Llm;
using System;
using System.Threading.Tasks;

namespace ModuleWorkFlow.PlaywrightRunner
{
    internal static class LlmTestRunner
    {
        public static async Task RunAsync()
        {
            var config = AppConfig.Load();
            var client = new OpenAiCompatibleLlmClient(config.Llm);

            Console.WriteLine("LLM 配置：");
            Console.WriteLine("Region: " + config.Llm.Region);
            Console.WriteLine("BaseUrl: " + config.Llm.BaseUrl);
            Console.WriteLine("Model: " + config.Llm.Model);
            Console.WriteLine("EnableThinking: " + config.Llm.EnableThinking);
            Console.WriteLine("ApiKey: " + (string.IsNullOrWhiteSpace(config.Llm.ApiKey) ? "未配置" : "已配置"));
            Console.WriteLine();

            var systemPrompt = "你是一個助手。請用簡短中文回答。";
            var userPrompt = "請用一句話回答：你是什麼模型？";

            Console.WriteLine("正在調用 LLM API...");
            var result = await client.ChatWithUsageAsync(systemPrompt, userPrompt);

            Console.WriteLine("LLM 返回：");
            Console.WriteLine(result.Content);
            Console.WriteLine();
            Console.WriteLine("Token 用量：");
            Console.WriteLine("InputTokens: " + result.InputTokens);
            Console.WriteLine("OutputTokens: " + result.OutputTokens);
            Console.WriteLine("TotalTokens: " + result.TotalTokens);
        }
    }
}
