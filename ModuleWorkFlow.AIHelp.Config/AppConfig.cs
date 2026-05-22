using ModuleWorkFlow.AIHelp.Llm;
using Newtonsoft.Json;
using System;
using System.IO;

namespace ModuleWorkFlow.AIHelp.Config
{
    public sealed class AppConfig
    {
        public LlmOptions Llm { get; set; } = new LlmOptions();

        public MesOptions Mes { get; set; } = new MesOptions();

        public static AppConfig Load(string configPath = null)
        {
            if (string.IsNullOrWhiteSpace(configPath))
            {
                configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            }

            var config = new AppConfig();

            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                var loaded = JsonConvert.DeserializeObject<AppConfig>(json);

                if (loaded != null)
                {
                    config = loaded;
                }
            }

            ApplyEnvironmentOverrides(config);

            ValidateAndNormalize(config);

            return config;
        }

        private static void ApplyEnvironmentOverrides(AppConfig config)
        {
            var region = Environment.GetEnvironmentVariable("LLM_REGION");
            var baseUrl = Environment.GetEnvironmentVariable("LLM_BASE_URL");
            var apiKey = Environment.GetEnvironmentVariable("LLM_API_KEY");
            var model = Environment.GetEnvironmentVariable("LLM_MODEL");

            if (!string.IsNullOrWhiteSpace(region))
            {
                config.Llm.Region = region.Trim();
            }

            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                config.Llm.BaseUrl = baseUrl.Trim();
            }

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                config.Llm.ApiKey = apiKey.Trim();
            }

            if (!string.IsNullOrWhiteSpace(model))
            {
                config.Llm.Model = model.Trim();
            }
        }

        private static void ValidateAndNormalize(AppConfig config)
        {
            if (config.Llm == null)
            {
                config.Llm = new LlmOptions();
            }

            if (string.IsNullOrWhiteSpace(config.Llm.BaseUrl))
            {
                config.Llm.BaseUrl = "https://llm-jqrqzdbt1aa6kqyq.cn-beijing.maas.aliyuncs.com/compatible-mode/v1";
            }

            config.Llm.BaseUrl = config.Llm.BaseUrl.Trim().TrimEnd('/');

            if (string.IsNullOrWhiteSpace(config.Llm.Model))
            {
                config.Llm.Model = "qwen3.6-plus";
            }

            if (config.Llm.TimeoutSeconds <= 0)
            {
                config.Llm.TimeoutSeconds = 60;
            }
        }
    }
}
