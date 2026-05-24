using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace ModuleWorkFlow.AIHelp.Training
{
    /// <summary>
    /// Reads clean training samples from JSONL and returns the closest few-shot matches.
    /// </summary>
    public sealed class CleanTrainingRetriever
    {
        private readonly string _cleanJsonlPath;

        public CleanTrainingRetriever(string cleanJsonlPath = null)
        {
            _cleanJsonlPath = string.IsNullOrWhiteSpace(cleanJsonlPath)
                ? Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "logs",
                    "training",
                    "clean",
                    "mes_ai_training_clean.jsonl")
                : cleanJsonlPath;
        }

        public string CleanJsonlPath
        {
            get { return _cleanJsonlPath; }
        }

        public IList<RetrievedTrainingSample> FindMatches(string userInput, int topK)
        {
            var normalizedInput = NormalizeText(userInput);
            if (string.IsNullOrWhiteSpace(normalizedInput) || topK <= 0)
            {
                return new List<RetrievedTrainingSample>();
            }

            var inputTokens = Tokenize(normalizedInput);
            if (inputTokens.Count == 0)
            {
                return new List<RetrievedTrainingSample>();
            }

            var matches = new List<RetrievedTrainingSample>();
            foreach (var sample in LoadSamples())
            {
                var sampleUserText = GetUserMessage(sample);
                if (string.IsNullOrWhiteSpace(sampleUserText))
                {
                    continue;
                }

                var normalizedSampleText = NormalizeText(sampleUserText);
                var sampleTokens = Tokenize(normalizedSampleText);
                if (sampleTokens.Count == 0)
                {
                    continue;
                }

                var score = CalculateScore(normalizedInput, inputTokens, normalizedSampleText, sampleTokens);
                if (score <= 0)
                {
                    continue;
                }

                matches.Add(new RetrievedTrainingSample
                {
                    UserInput = sampleUserText,
                    AssistantOutput = GetAssistantMessage(sample),
                    Action = sample.Metadata == null ? string.Empty : sample.Metadata.Action,
                    Status = sample.Metadata == null ? string.Empty : sample.Metadata.Status,
                    SampleType = sample.Metadata == null ? string.Empty : sample.Metadata.SampleType,
                    Score = score
                });
            }

            return matches
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.UserInput == null ? 0 : x.UserInput.Length)
                .Take(topK)
                .ToList();
        }

        private IEnumerable<CleanTrainingSampleRecord> LoadSamples()
        {
            if (!File.Exists(_cleanJsonlPath))
            {
                yield break;
            }

            foreach (var line in File.ReadLines(_cleanJsonlPath))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                CleanTrainingSampleRecord sample;
                try
                {
                    sample = JsonConvert.DeserializeObject<CleanTrainingSampleRecord>(line);
                }
                catch
                {
                    continue;
                }

                if (sample != null)
                {
                    yield return sample;
                }
            }
        }

        private static string GetUserMessage(CleanTrainingSampleRecord sample)
        {
            if (sample == null || sample.Messages == null)
            {
                return string.Empty;
            }

            var message = sample.Messages.FirstOrDefault(x =>
                string.Equals(x.Role, "user", StringComparison.OrdinalIgnoreCase));

            return message == null ? string.Empty : message.Content ?? string.Empty;
        }

        private static string GetAssistantMessage(CleanTrainingSampleRecord sample)
        {
            if (sample == null || sample.Messages == null)
            {
                return string.Empty;
            }

            var message = sample.Messages.FirstOrDefault(x =>
                string.Equals(x.Role, "assistant", StringComparison.OrdinalIgnoreCase));

            return message == null ? string.Empty : message.Content ?? string.Empty;
        }

        private static double CalculateScore(
            string normalizedInput,
            HashSet<string> inputTokens,
            string normalizedSampleText,
            HashSet<string> sampleTokens)
        {
            var overlapCount = inputTokens.Intersect(sampleTokens).Count();
            var unionCount = inputTokens.Union(sampleTokens).Count();
            if (overlapCount == 0 || unionCount == 0)
            {
                return 0;
            }

            var jaccard = (double)overlapCount / unionCount;
            var substringBonus = normalizedSampleText.IndexOf(normalizedInput, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 normalizedInput.IndexOf(normalizedSampleText, StringComparison.OrdinalIgnoreCase) >= 0
                ? 0.35
                : 0;

            return jaccard + substringBonus;
        }

        private static string NormalizeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var normalized = text.Trim().ToLowerInvariant();
            normalized = Regex.Replace(normalized, "\\s+", " ");
            return normalized;
        }

        private static HashSet<string> Tokenize(string text)
        {
            var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(text))
            {
                return tokens;
            }

            foreach (Match match in Regex.Matches(text, "[\\p{L}\\p{Nd}_]+"))
            {
                var token = match.Value.Trim();
                if (!string.IsNullOrWhiteSpace(token))
                {
                    tokens.Add(token);
                }
            }

            // Add short CJK n-grams so phrases like "設定訂單" and "新增訂單" can still match reasonably.
            var compact = text.Replace(" ", string.Empty);
            for (var i = 0; i < compact.Length - 1; i++)
            {
                var bigram = compact.Substring(i, 2);
                if (!string.IsNullOrWhiteSpace(bigram))
                {
                    tokens.Add(bigram);
                }
            }

            return tokens;
        }
    }

    public sealed class RetrievedTrainingSample
    {
        public string UserInput { get; set; }

        public string AssistantOutput { get; set; }

        public string Action { get; set; }

        public string Status { get; set; }

        public string SampleType { get; set; }

        public double Score { get; set; }
    }

    internal sealed class CleanTrainingSampleRecord
    {
        [JsonProperty("messages")]
        public List<CleanTrainingMessageRecord> Messages { get; set; }

        [JsonProperty("metadata")]
        public CleanTrainingMetadataRecord Metadata { get; set; }
    }

    internal sealed class CleanTrainingMessageRecord
    {
        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }
    }

    internal sealed class CleanTrainingMetadataRecord
    {
        [JsonProperty("action")]
        public string Action { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("sampleType")]
        public string SampleType { get; set; }
    }
}
