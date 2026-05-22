using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace ModuleWorkFlow.AIHelp.Llm
{
    public sealed class OpenAiCompatibleLlmClient
    {
        private readonly LlmOptions _options;
        private readonly HttpClient _httpClient;
        private readonly LlmUsageLogger _usageLogger;

        public OpenAiCompatibleLlmClient(LlmOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));

            if (string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                throw new InvalidOperationException(
                    "缺少 LLM API Key。請在 appsettings.json 的 Llm.ApiKey 中配置，或設置環境變量 LLM_API_KEY。");
            }

            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds <= 0 ? 60 : _options.TimeoutSeconds)
            };
            _usageLogger = new LlmUsageLogger();
        }

        public async Task<string> ChatAsync(string systemPrompt, string userPrompt)
        {
            var result = await ChatWithUsageAsync(systemPrompt, userPrompt);
            return result.Content;
        }

        public async Task<LlmChatResult> ChatWithUsageAsync(string systemPrompt, string userPrompt)
        {
            if (string.IsNullOrWhiteSpace(userPrompt))
            {
                throw new ArgumentException("userPrompt 不能為空", nameof(userPrompt));
            }

            var endpoint = _options.BaseUrl.TrimEnd('/') + "/chat/completions";

            var requestBody = new
            {
                model = _options.Model,
                messages = new object[]
                {
                    new
                    {
                        role = "system",
                        content = systemPrompt ?? string.Empty
                    },
                    new
                    {
                        role = "user",
                        content = userPrompt
                    }
                },
                temperature = _options.Temperature,
                enable_thinking = _options.EnableThinking
            };

            var requestJson = JsonConvert.SerializeObject(requestBody, Formatting.None);

            using (var request = new HttpRequestMessage(HttpMethod.Post, endpoint))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
                request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

                using (var response = await _httpClient.SendAsync(request))
                {
                    var responseText = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        ThrowFriendlyApiException(response.StatusCode.ToString(), responseText);
                    }

                    var result = ExtractChatResult(responseText);
                    _usageLogger.Append(_options.Model, result.InputTokens, result.OutputTokens, result.TotalTokens);
                    return result;
                }
            }
        }

        private static LlmChatResult ExtractChatResult(string responseText)
        {
            if (string.IsNullOrWhiteSpace(responseText))
            {
                throw new Exception("LLM API 返回內容為空");
            }

            var root = JObject.Parse(responseText);
            var content = root["choices"]?[0]?["message"]?["content"]?.ToString();

            if (string.IsNullOrWhiteSpace(content))
            {
                throw new Exception("LLM API 返回 message.content 為空。\n" + responseText);
            }

            var usage = root["usage"];
            var inputTokens = GetInt32OrDefault(usage, "input_tokens");
            if (inputTokens <= 0)
            {
                inputTokens = GetInt32OrDefault(usage, "prompt_tokens");
            }

            var outputTokens = GetInt32OrDefault(usage, "output_tokens");
            if (outputTokens <= 0)
            {
                outputTokens = GetInt32OrDefault(usage, "completion_tokens");
            }

            var totalTokens = GetInt32OrDefault(usage, "total_tokens");
            if (totalTokens <= 0 && (inputTokens > 0 || outputTokens > 0))
            {
                totalTokens = inputTokens + outputTokens;
            }

            return new LlmChatResult
            {
                Content = content.Trim(),
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                TotalTokens = totalTokens,
                RawResponse = responseText
            };
        }

        private static int GetInt32OrDefault(JToken parent, string propertyName)
        {
            var value = parent == null ? null : parent[propertyName];
            if (value == null)
            {
                return 0;
            }

            int number;
            if (int.TryParse(value.ToString(), out number))
            {
                return number;
            }

            return 0;
        }

        private static void ThrowFriendlyApiException(string statusCode, string responseText)
        {
            if (IsQuotaOrBillingError(responseText))
            {
                throw new Exception(
                    "千問調用失敗：可能是免費額度已用完、帳號欠費或模型配額不足。\n\n" +
                    "請檢查：\n" +
                    "1. 百煉控制台 > 模型廣場 > 對應模型 > 免費額度\n" +
                    "2. 阿里雲控制台 > 費用與成本 > 賬戶餘額\n" +
                    "3. 是否開啟了「免費額度用完即停 / 僅使用免費額度」\n" +
                    "4. 當前 API Key 是否有該模型調用權限\n\n" +
                    "原始錯誤：\n" +
                    responseText);
            }

            throw new Exception(
                "LLM API 調用失敗：HTTP " + statusCode + "\n" +
                "原始錯誤：\n" + responseText);
        }

        private static bool IsQuotaOrBillingError(string responseText)
        {
            if (string.IsNullOrWhiteSpace(responseText))
            {
                return false;
            }

            var text = responseText.ToLowerInvariant();

            string[] keywords =
            {
                "allocationquota.freetieronly",
                "insufficient quota",
                "insufficient_quota",
                "quota exceeded",
                "free tier",
                "arrearage",
                "balance",
                "account is in good standing",
                "欠费",
                "欠費",
                "额度",
                "額度",
                "配额",
                "配額",
                "余额",
                "餘額"
            };

            return keywords.Any(k => text.Contains(k.ToLowerInvariant()));
        }
    }
}
