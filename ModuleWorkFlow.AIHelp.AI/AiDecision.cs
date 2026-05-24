using Newtonsoft.Json;
using System.Collections.Generic;

namespace ModuleWorkFlow.AIHelp.AI
{
    public sealed class AiDecision
    {
        [JsonProperty("status")]
        public string Status { get; set; } = "";

        [JsonProperty("action")]
        public string Action { get; set; } = "";

        [JsonProperty("params")]
        public Dictionary<string, string> Params { get; set; } = new Dictionary<string, string>();

        [JsonProperty("missingParams")]
        public List<string> MissingParams { get; set; } = new List<string>();

        [JsonProperty("question")]
        public string Question { get; set; } = "";

        [JsonProperty("confidence")]
        public double Confidence { get; set; }
    }
}
