using ModuleWorkFlow.AIHelp.AI;
using ModuleWorkFlow.AIHelp.Tools;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ModuleWorkFlow.AIHelp.Training
{
    public class TrainingDataLogger
    {
        // Keep BOM so Windows tools read Chinese training logs as UTF-8 consistently.
        private static readonly UTF8Encoding Utf8WithBom = new UTF8Encoding(true);

        private readonly string _trainingDirectory;
        private readonly string _rawDirectory;
        private readonly string _reviewDirectory;
        private readonly string _cleanDirectory;
        private readonly string _rawJsonlPath;
        private readonly string _reviewMarkdownPath;
        private readonly string _cleanJsonlPath;

        public TrainingDataLogger()
        {
            _trainingDirectory = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "logs",
                "training");
            _rawDirectory = Path.Combine(_trainingDirectory, "raw");
            _reviewDirectory = Path.Combine(_trainingDirectory, "review");
            _cleanDirectory = Path.Combine(_trainingDirectory, "clean");
            _rawJsonlPath = Path.Combine(_rawDirectory, "mes_ai_training_raw.jsonl");
            _reviewMarkdownPath = Path.Combine(_reviewDirectory, "mes_ai_training_review.md");
            _cleanJsonlPath = Path.Combine(_cleanDirectory, "mes_ai_training_clean.jsonl");
        }

        public void Append(
            string systemPrompt,
            ToolRegistry toolRegistry,
            string userInput,
            string aiRawText,
            AiDecision decision,
            TrainingExecutionResult executionResult,
            bool maskUserName = false)
        {
            Directory.CreateDirectory(_rawDirectory);
            Directory.CreateDirectory(_reviewDirectory);
            Directory.CreateDirectory(_cleanDirectory);
            EnsureFileExists(_rawJsonlPath, Encoding.UTF8);
            EnsureFileExists(_reviewMarkdownPath, Encoding.UTF8);
            EnsureFileExists(_cleanJsonlPath, Utf8WithBom);

            var safeDecision = CloneDecision(decision);
            var safeExecutionResult = CloneExecutionResult(executionResult);

            SanitizeDecision(safeDecision, maskUserName);
            SanitizeExecutionResult(safeExecutionResult, maskUserName);

            var sanitizedUserInput = SanitizeText(userInput, decision, maskUserName);
            var sanitizedAiRawText = SanitizeText(aiRawText, decision, maskUserName);

            var sample = new TrainingLogSample
            {
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                UserInput = sanitizedUserInput,
                AiRawText = sanitizedAiRawText,
                AiDecision = safeDecision,
                ExecutionResult = safeExecutionResult
            };

            var rawSample = BuildRawSample(sample);

            File.AppendAllText(
                _rawJsonlPath,
                JsonConvert.SerializeObject(rawSample, Formatting.None) + Environment.NewLine,
                Encoding.UTF8);
            File.AppendAllText(_reviewMarkdownPath, BuildReviewMarkdown(sample), Encoding.UTF8);

            if (ShouldWriteCleanSample(sample, toolRegistry))
            {
                File.AppendAllText(
                    _cleanJsonlPath,
                    BuildCleanJsonl(systemPrompt, sample) + Environment.NewLine,
                    Utf8WithBom);
            }
        }

        public void AppendCorrectedCleanSample(
            string systemPrompt,
            string userInput,
            AiDecision correctedDecision,
            TrainingExecutionResult executionResult,
            bool maskUserName = false)
        {
            Directory.CreateDirectory(_cleanDirectory);
            EnsureFileExists(_cleanJsonlPath, Utf8WithBom);

            var safeDecision = CloneDecision(correctedDecision);
            var safeExecutionResult = CloneExecutionResult(executionResult);

            SanitizeDecision(safeDecision, maskUserName);
            SanitizeExecutionResult(safeExecutionResult, maskUserName);

            var sample = new TrainingLogSample
            {
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                UserInput = SanitizeText(userInput, correctedDecision, maskUserName),
                AiRawText = SerializeDecisionForTraining(safeDecision),
                AiDecision = safeDecision,
                ExecutionResult = safeExecutionResult
            };

            File.AppendAllText(
                _cleanJsonlPath,
                BuildCleanJsonl(systemPrompt, sample) + Environment.NewLine,
                Utf8WithBom);
        }

        private static void EnsureFileExists(string path, Encoding encoding)
        {
            if (File.Exists(path))
            {
                return;
            }

            File.WriteAllText(path, string.Empty, encoding);
        }

        private static string BuildReviewMarkdown(TrainingLogSample sample)
        {
            var builder = new StringBuilder();
            builder.AppendLine("---");
            builder.AppendLine("## " + sample.Timestamp);
            builder.AppendLine();
            builder.AppendLine("### User Input");
            builder.AppendLine("```text");
            builder.AppendLine(sample.UserInput ?? string.Empty);
            builder.AppendLine("```");
            builder.AppendLine();
            builder.AppendLine("### AI Raw Text");
            builder.AppendLine("```json");
            builder.AppendLine(sample.AiRawText ?? string.Empty);
            builder.AppendLine("```");
            builder.AppendLine();
            builder.AppendLine("### AiDecision");
            builder.AppendLine("```json");
            builder.AppendLine(JsonConvert.SerializeObject(sample.AiDecision, Formatting.Indented));
            builder.AppendLine("```");
            builder.AppendLine();
            builder.AppendLine("### Execution Result");
            builder.AppendLine("```json");
            builder.AppendLine(JsonConvert.SerializeObject(sample.ExecutionResult, Formatting.Indented));
            builder.AppendLine("```");
            builder.AppendLine();
            return builder.ToString();
        }

        private static bool ShouldWriteCleanSample(TrainingLogSample sample, ToolRegistry toolRegistry)
        {
            if (sample == null || sample.AiDecision == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(sample.UserInput))
            {
                return false;
            }

            if (string.Equals(sample.AiDecision.Status, "need_more_info", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Raw/review keep rejected samples; clean keeps only normalized positive/clarification samples.

            if (!string.Equals(sample.AiDecision.Status, "ready", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(sample.AiDecision.Action))
            {
                return false;
            }

            if (toolRegistry == null)
            {
                return false;
            }

            var actionDefinition = toolRegistry.GetAction(sample.AiDecision.Action);
            if (actionDefinition == null)
            {
                return false;
            }

            if (actionDefinition.RequiredParams == null || actionDefinition.RequiredParams.Count == 0)
            {
                return true;
            }

            if (sample.AiDecision.Params == null)
            {
                return false;
            }

            foreach (var requiredParam in actionDefinition.RequiredParams)
            {
                string value;

                if (!sample.AiDecision.Params.TryGetValue(requiredParam, out value) ||
                    string.IsNullOrWhiteSpace(value))
                {
                    return false;
                }
            }

            return true;
        }

        private static string BuildCleanJsonl(string systemPrompt, TrainingLogSample sample)
        {
            var decisionForTraining = CloneDecision(sample.AiDecision);

            if (decisionForTraining.Params != null && decisionForTraining.Params.ContainsKey("loginUrl"))
            {
                decisionForTraining.Params.Remove("loginUrl");
            }

            if (decisionForTraining.Params != null && decisionForTraining.Params.ContainsKey("password"))
            {
                decisionForTraining.Params["password"] = "<PASSWORD>";
            }

            if (decisionForTraining.Confidence > 0)
            {
                decisionForTraining.Confidence = Math.Round(decisionForTraining.Confidence, 2);
            }

            var cleanSample = new CleanTrainingSample
            {
                Messages = new List<CleanMessage>
                {
                    new CleanMessage
                    {
                        Role = "system",
                        Content = systemPrompt ?? string.Empty
                    },
                    new CleanMessage
                    {
                        Role = "user",
                        Content = sample.UserInput ?? string.Empty
                    },
                    new CleanMessage
                    {
                        Role = "assistant",
                        Content = SerializeDecisionForTraining(decisionForTraining)
                    }
                },
                Metadata = new CleanMetadata
                {
                    Action = decisionForTraining.Action,
                    Status = decisionForTraining.Status,
                    Success = sample.ExecutionResult != null && sample.ExecutionResult.Success,
                    ExecutionSuccess = sample.ExecutionResult != null && sample.ExecutionResult.Success,
                    ExecutionStatus = sample.ExecutionResult == null ? string.Empty : sample.ExecutionResult.Status,
                    ExecutionMessage = sample.ExecutionResult == null ? string.Empty : sample.ExecutionResult.Message,
                    SampleType = GetSampleType(decisionForTraining, sample.ExecutionResult),
                    Source = "ModuleWorkFlow.PlaywrightRunner"
                }
            };

            return JsonConvert.SerializeObject(cleanSample, Formatting.None);
        }

        private static string SerializeDecisionForTraining(AiDecision decisionForTraining)
        {
            var roundedConfidence = decisionForTraining == null
                ? 0D
                : decisionForTraining.Confidence;

            if (roundedConfidence > 0)
            {
                roundedConfidence = Math.Round(roundedConfidence, 2);
            }

            var serializedDecision = new SerializedAiDecision
            {
                Status = decisionForTraining == null ? string.Empty : decisionForTraining.Status,
                Action = decisionForTraining == null ? string.Empty : decisionForTraining.Action,
                Params = decisionForTraining == null || decisionForTraining.Params == null
                    ? new Dictionary<string, string>()
                    : new Dictionary<string, string>(decisionForTraining.Params),
                MissingParams = decisionForTraining == null || decisionForTraining.MissingParams == null
                    ? new List<string>()
                    : new List<string>(decisionForTraining.MissingParams),
                Question = decisionForTraining == null ? string.Empty : decisionForTraining.Question,
                Confidence = Convert.ToDecimal(roundedConfidence)
            };

            return JsonConvert.SerializeObject(serializedDecision, Formatting.None);
        }

        private static string GetSampleType(AiDecision decision, TrainingExecutionResult executionResult)
        {
            if (decision == null)
            {
                return string.Empty;
            }

            if (string.Equals(decision.Status, "need_more_info", StringComparison.OrdinalIgnoreCase))
            {
                return "need_more_info";
            }

            if (string.Equals(decision.Status, "rejected", StringComparison.OrdinalIgnoreCase))
            {
                return "rejected";
            }

            if (string.Equals(decision.Status, "ready", StringComparison.OrdinalIgnoreCase))
            {
                if (executionResult != null && executionResult.Success)
                {
                    return "ready_execution_success";
                }

                return "ready_intent_only";
            }

            return string.Empty;
        }

        private static RawTrainingLogSample BuildRawSample(TrainingLogSample sample)
        {
            return new RawTrainingLogSample
            {
                Timestamp = sample.Timestamp,
                UserInput = sample.UserInput,
                AiRawText = sample.AiRawText,
                AiDecision = sample.AiDecision,
                ExecutionResult = new RawExecutionResult
                {
                    Executed = sample.ExecutionResult != null && sample.ExecutionResult.Executed,
                    Success = sample.ExecutionResult != null && sample.ExecutionResult.Success,
                    Status = sample.ExecutionResult == null ? null : sample.ExecutionResult.Status,
                    Message = sample.ExecutionResult == null ? null : sample.ExecutionResult.Message
                }
            };
        }

        private static string SanitizeText(string text, AiDecision decision, bool maskUserName)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            string password;
            string userName;

            password = GetParamValue(decision, "password");
            userName = GetParamValue(decision, "userName");

            if (!string.IsNullOrWhiteSpace(password))
            {
                text = text.Replace(password, "<PASSWORD>");
            }

            if (maskUserName && !string.IsNullOrWhiteSpace(userName))
            {
                text = text.Replace(userName, MaskUserNameValue(userName));
            }

            return text;
        }

        private static void SanitizeDecision(AiDecision decision, bool maskUserName)
        {
            if (decision == null || decision.Params == null)
            {
                return;
            }

            if (decision.Params.ContainsKey("password"))
            {
                decision.Params["password"] = "<PASSWORD>";
            }

            if (maskUserName && decision.Params.ContainsKey("userName"))
            {
                decision.Params["userName"] = MaskUserNameValue(decision.Params["userName"]);
            }
        }

        private static void SanitizeExecutionResult(TrainingExecutionResult executionResult, bool maskUserName)
        {
            if (executionResult == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(executionResult.Password))
            {
                executionResult.Password = "<PASSWORD>";
            }

            if (maskUserName && !string.IsNullOrWhiteSpace(executionResult.UserName))
            {
                executionResult.UserName = MaskUserNameValue(executionResult.UserName);
            }
        }

        private static string GetParamValue(AiDecision decision, string key)
        {
            string value;

            if (decision == null || decision.Params == null)
            {
                return null;
            }

            if (!decision.Params.TryGetValue(key, out value))
            {
                return null;
            }

            return value;
        }

        private static string MaskUserNameValue(string userName)
        {
            if (string.IsNullOrEmpty(userName))
            {
                return string.Empty;
            }

            if (userName.Length <= 2)
            {
                return "<USER>";
            }

            return userName.Substring(0, 1) + "***" + userName.Substring(userName.Length - 1, 1);
        }

        private static AiDecision CloneDecision(AiDecision decision)
        {
            var clone = new AiDecision();

            if (decision == null)
            {
                return clone;
            }

            clone.Status = decision.Status;
            clone.Action = decision.Action;
            clone.Question = decision.Question;
            clone.Confidence = decision.Confidence;
            clone.MissingParams = decision.MissingParams == null
                ? new List<string>()
                : new List<string>(decision.MissingParams);
            clone.Params = new Dictionary<string, string>();

            if (decision.Params != null)
            {
                foreach (var pair in decision.Params)
                {
                    clone.Params[pair.Key] = pair.Value;
                }
            }

            return clone;
        }

        private static TrainingExecutionResult CloneExecutionResult(TrainingExecutionResult executionResult)
        {
            if (executionResult == null)
            {
                return new TrainingExecutionResult();
            }

            return new TrainingExecutionResult
            {
                Executed = executionResult.Executed,
                Status = executionResult.Status,
                Success = executionResult.Success,
                Message = executionResult.Message,
                CurrentUrl = executionResult.CurrentUrl,
                UserName = executionResult.UserName,
                Password = executionResult.Password
            };
        }

        private sealed class TrainingLogSample
        {
            public string Timestamp { get; set; }

            public string UserInput { get; set; }

            public string AiRawText { get; set; }

            public AiDecision AiDecision { get; set; }

            public TrainingExecutionResult ExecutionResult { get; set; }
        }
    }

    public sealed class TrainingExecutionResult
    {
        public bool Executed { get; set; }

        public string Status { get; set; }

        public bool Success { get; set; }

        public string Message { get; set; }

        public string CurrentUrl { get; set; }

        public string UserName { get; set; }

        public string Password { get; set; }
    }

    internal sealed class CleanTrainingSample
    {
        [JsonProperty("messages")]
        public List<CleanMessage> Messages { get; set; }

        [JsonProperty("metadata")]
        public CleanMetadata Metadata { get; set; }
    }

    internal sealed class CleanMessage
    {
        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }
    }

    internal sealed class CleanMetadata
    {
        [JsonProperty("action")]
        public string Action { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("executionSuccess")]
        public bool ExecutionSuccess { get; set; }

        [JsonProperty("executionStatus")]
        public string ExecutionStatus { get; set; }

        [JsonProperty("executionMessage")]
        public string ExecutionMessage { get; set; }

        [JsonProperty("sampleType")]
        public string SampleType { get; set; }

        [JsonProperty("source")]
        public string Source { get; set; }
    }

    internal sealed class RawTrainingLogSample
    {
        [JsonProperty("timestamp")]
        public string Timestamp { get; set; }

        [JsonProperty("userInput")]
        public string UserInput { get; set; }

        [JsonProperty("aiRawText")]
        public string AiRawText { get; set; }

        [JsonProperty("aiDecision")]
        public AiDecision AiDecision { get; set; }

        [JsonProperty("executionResult")]
        public RawExecutionResult ExecutionResult { get; set; }
    }

    internal sealed class RawExecutionResult
    {
        [JsonProperty("executed")]
        public bool Executed { get; set; }

        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }

    internal sealed class SerializedAiDecision
    {
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("action")]
        public string Action { get; set; }

        [JsonProperty("params")]
        public Dictionary<string, string> Params { get; set; }

        [JsonProperty("missingParams")]
        public List<string> MissingParams { get; set; }

        [JsonProperty("question")]
        public string Question { get; set; }

        [JsonProperty("confidence")]
        public decimal Confidence { get; set; }
    }
}
