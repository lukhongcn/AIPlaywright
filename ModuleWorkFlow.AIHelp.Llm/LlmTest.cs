using System;
using System.Threading.Tasks;

namespace ModuleWorkFlow.AIHelp.Llm
{
    internal static class LlmTest
    {
        public static async Task RunAsync()
        {
            var config = AppConfig.Load();

            var client = new OpenAiCompatibleLlmClient(config.Llm);

            var systemPrompt =
                "你是一個 MES action 解析器。你的任務是把使用者輸入解析成固定 JSON。\n" +
                "你目前只允許一個 action：login_test。\n" +
                "login_test 必填參數只有兩個：userName、password。\n" +
                "loginUrl 是可選參數。\n" +
                "如果使用者沒有提供 loginUrl，不要追問 loginUrl。\n" +
                "loginUrl 由程序根據 action 配置自動補全，不屬於必填缺失項。\n" +
                "你必須根據使用者輸入判斷是否已提供完整參數。\n" +
                "如果 userName 和 password 都存在，status 必須返回 ready。\n" +
                "如果參數完整，必須只輸出以下結構的 JSON：\n" +
                "{\n" +
                "  \"status\": \"ready\",\n" +
                "  \"action\": \"login_test\",\n" +
                "  \"params\": {\n" +
                "    \"loginUrl\": \"\",\n" +
                "    \"userName\": \"\",\n" +
                "    \"password\": \"\"\n" +
                "  },\n" +
                "  \"missingParams\": [],\n" +
                "  \"question\": \"\",\n" +
                "  \"confidence\": 0.95\n" +
                "}\n" +
                "如果缺少任一參數，必須只輸出以下結構的 JSON：\n" +
                "{\n" +
                "  \"status\": \"need_more_info\",\n" +
                "  \"action\": \"login_test\",\n" +
                "  \"params\": {},\n" +
                "  \"missingParams\": [\"userName\", \"password\"],\n" +
                "  \"question\": \"請提供帳號和密碼。\",\n" +
                "  \"confidence\": 0.9\n" +
                "}\n" +
                "missingParams 必須只保留實際缺少的欄位。\n" +
                "如果只有 loginUrl 缺少，但 userName 和 password 已存在，仍然必須返回 ready。\n" +
                "如果 action 不是 login_test，也要輸出 need_more_info，並引導使用者目前只支援 login_test。\n" +
                "你只能輸出 JSON 本體，不要輸出 Markdown，不要輸出程式碼區塊，不要輸出任何解釋文字。";
            var userPrompt = Console.ReadLine() ?? string.Empty;

            var result = await client.ChatAsync(systemPrompt, userPrompt);

            Console.WriteLine(result);
        }
    }
}
