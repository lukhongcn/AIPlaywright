using ModuleWorkFlow.AIHelp.Tools;
using System.Linq;
using System.Text;

namespace ModuleWorkFlow.AIHelp.AI
{
    public static class AiPromptBuilder
    {
        public static string BuildSystemPrompt(ToolRegistry toolRegistry)
        {
            var builder = new StringBuilder();
            builder.AppendLine("你是 MES 自动化助手。请根据用户输入输出严格 JSON。");
            builder.AppendLine("JSON 字段包括: status, action, params, missingParams, question, confidence。");
            builder.AppendLine("status 只能是 ready、need_more_info、rejected、error。");
            builder.AppendLine("如果信息不足，请返回 need_more_info 并在 missingParams 中列出缺失参数。");
            builder.AppendLine("如果请求不允许执行，请返回 rejected 并填写 question。");

            if (toolRegistry != null)
            {
                builder.AppendLine("可用动作如下:");
                foreach (var action in toolRegistry.GetAllActions())
                {
                    builder.Append("- ");
                    builder.Append(action.Action);
                    builder.Append(": ");
                    builder.Append(action.Description);

                    if (action.RequiredParams != null && action.RequiredParams.Count > 0)
                    {
                        builder.Append(" | required=");
                        builder.Append(string.Join(", ", action.RequiredParams));
                    }

                    if (action.OptionalParams != null && action.OptionalParams.Count > 0)
                    {
                        builder.Append(" | optional=");
                        builder.Append(string.Join(", ", action.OptionalParams));
                    }

                    if (action.Defaults != null && !string.IsNullOrWhiteSpace(action.Defaults.LoginPath))
                    {
                        builder.Append(" | loginPath=");
                        builder.Append(action.Defaults.LoginPath);
                    }

                    builder.AppendLine();
                }
            }

            builder.AppendLine("示例输出:");
            builder.AppendLine("{\"status\":\"ready\",\"action\":\"login_test\",\"params\":{\"loginUrl\":\"http://localhost/login\",\"userName\":\"admin\",\"password\":\"123456\"},\"missingParams\":[],\"question\":\"\",\"confidence\":0.95}");
            return builder.ToString();
        }
    }
}
