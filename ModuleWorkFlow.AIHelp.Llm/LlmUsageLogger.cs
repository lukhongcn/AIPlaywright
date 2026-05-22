using System;
using System.IO;
using System.Text;

namespace ModuleWorkFlow.AIHelp.Llm
{
    internal sealed class LlmUsageLogger
    {
        private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false);

        private readonly string _logPath;

        public LlmUsageLogger()
        {
            _logPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "logs",
                "llm_usage.csv");
        }

        public void Append(string model, int inputTokens, int outputTokens, int totalTokens)
        {
            var directory = Path.GetDirectoryName(_logPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (!File.Exists(_logPath))
            {
                File.WriteAllText(
                    _logPath,
                    "timestamp,model,input_tokens,output_tokens,total_tokens" + Environment.NewLine,
                    Utf8WithoutBom);
            }

            var line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "," +
                       EscapeCsv(model) + "," +
                       inputTokens + "," +
                       outputTokens + "," +
                       totalTokens +
                       Environment.NewLine;

            File.AppendAllText(_logPath, line, Utf8WithoutBom);
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            if (value.IndexOf(',') >= 0 || value.IndexOf('"') >= 0 || value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0)
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }

            return value;
        }
    }
}
