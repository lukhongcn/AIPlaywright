using ModuleWorkFlow.AIHelp.Config;
using ModuleWorkFlow.AIHelp.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ModuleWorkFlow.AIHelp.AI
{
    public static class AiDecisionParser
    {
        public static AiDecision Parse(string aiRawText, AppConfig config, ToolRegistry toolRegistry, string contextLabel)
        {
            if (string.IsNullOrWhiteSpace(aiRawText))
            {
                return new AiDecision
                {
                    Status = "error",
                    Question = (contextLabel ?? "AI 返回") + "为空"
                };
            }

            try
            {
                var jsonText = ExtractJson(aiRawText);
                var token = JToken.Parse(jsonText);
                var obj = token as JObject ?? new JObject();

                var decision = new AiDecision
                {
                    Status = GetString(obj, "status"),
                    Action = GetString(obj, "action"),
                    Question = GetString(obj, "question"),
                    Confidence = GetDouble(obj, "confidence"),
                    Params = ReadParams(obj["params"]),
                    MissingParams = ReadMissingParams(obj["missingParams"])
                };

                NormalizeDecision(decision, toolRegistry);
                return decision;
            }
            catch (Exception ex)
            {
                return new AiDecision
                {
                    Status = "error",
                    Question = string.Format("{0} 解析失败: {1}", contextLabel ?? "AI 返回", ex.Message)
                };
            }
        }

        private static void NormalizeDecision(AiDecision decision, ToolRegistry toolRegistry)
        {
            if (decision == null)
            {
                return;
            }

            decision.Status = string.IsNullOrWhiteSpace(decision.Status) ? "error" : decision.Status.Trim();
            decision.Action = string.IsNullOrWhiteSpace(decision.Action) ? "" : decision.Action.Trim();
            decision.Question = decision.Question ?? "";
            decision.Params = decision.Params ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            decision.MissingParams = decision.MissingParams ?? new List<string>();

            if (decision.Params.Count > 0 && !(decision.Params.Comparer.Equals(StringComparer.OrdinalIgnoreCase)))
            {
                decision.Params = new Dictionary<string, string>(decision.Params, StringComparer.OrdinalIgnoreCase);
            }

            if (string.Equals(decision.Status, "ready", StringComparison.OrdinalIgnoreCase) && toolRegistry != null)
            {
                var actionDefinition = toolRegistry.GetAction(decision.Action);
                if (actionDefinition != null && actionDefinition.RequiredParams != null)
                {
                    var missing = new List<string>();
                    foreach (var requiredParam in actionDefinition.RequiredParams)
                    {
                        string value;
                        if (!decision.Params.TryGetValue(requiredParam, out value) || string.IsNullOrWhiteSpace(value))
                        {
                            missing.Add(requiredParam);
                        }
                    }

                    if (missing.Count > 0)
                    {
                        decision.Status = "need_more_info";
                        decision.MissingParams = missing;
                        if (string.IsNullOrWhiteSpace(decision.Question))
                        {
                            decision.Question = "请补充以下参数: " + string.Join(", ", missing);
                        }
                    }
                }
            }

            if (decision.MissingParams.Count > 0 && string.IsNullOrWhiteSpace(decision.Question))
            {
                decision.Question = "请补充以下参数: " + string.Join(", ", decision.MissingParams);
            }
        }

        private static string ExtractJson(string input)
        {
            var trimmed = input.Trim();
            var fencedStart = trimmed.IndexOf('{');
            var fencedEnd = trimmed.LastIndexOf('}');
            if (fencedStart >= 0 && fencedEnd > fencedStart)
            {
                return trimmed.Substring(fencedStart, fencedEnd - fencedStart + 1);
            }

            return trimmed;
        }

        private static string GetString(JObject obj, string name)
        {
            var token = obj[name];
            return token == null ? "" : (token.Type == JTokenType.String ? (string)token : token.ToString(Formatting.None));
        }

        private static double GetDouble(JObject obj, string name)
        {
            var token = obj[name];
            if (token == null)
            {
                return 0;
            }

            double value;
            return double.TryParse(token.ToString(), out value) ? value : 0;
        }

        private static Dictionary<string, string> ReadParams(JToken token)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var obj = token as JObject;
            if (obj == null)
            {
                return result;
            }

            foreach (var property in obj.Properties())
            {
                result[property.Name] = property.Value == null ? "" : property.Value.ToString();
            }

            return result;
        }

        private static List<string> ReadMissingParams(JToken token)
        {
            var array = token as JArray;
            if (array == null)
            {
                return new List<string>();
            }

            return array.Select(x => x == null ? "" : x.ToString()).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        }
    }
}
