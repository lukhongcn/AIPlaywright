using Newtonsoft.Json;
using System.Collections.Generic;

namespace ModuleWorkFlow.AIHelp.Tools
{
    public sealed class ToolRegistryFile
    {
        [JsonProperty("actions")]
        public List<ActionToolDefinition> Actions { get; set; } = new List<ActionToolDefinition>();
    }

    public sealed class ActionToolDefinition
    {
        [JsonProperty("action")]
        public string Action { get; set; } = "";

        [JsonProperty("description")]
        public string Description { get; set; } = "";

        [JsonProperty("readonly")]
        public bool Readonly { get; set; } = true;

        [JsonProperty("riskLevel")]
        public string RiskLevel { get; set; } = "low";

        [JsonProperty("requiredParams")]
        public List<string> RequiredParams { get; set; } = new List<string>();

        [JsonProperty("optionalParams")]
        public List<string> OptionalParams { get; set; } = new List<string>();

        [JsonProperty("requiresLogin")]
        public bool RequiresLogin { get; set; } = true;

        [JsonProperty("dryRunOnly")]
        public bool DryRunOnly { get; set; } = true;

        [JsonProperty("allowTechnicalPostback")]
        public bool AllowTechnicalPostback { get; set; } = true;

        [JsonProperty("businessCommitAllowed")]
        public bool BusinessCommitAllowed { get; set; } = false;

        [JsonProperty("forbiddenButtonTexts")]
        public List<string> ForbiddenButtonTexts { get; set; } = new List<string>();

        [JsonProperty("forbiddenBusinessActions")]
        public List<string> ForbiddenBusinessActions { get; set; } = new List<string>();

        [JsonProperty("allowedUiEffects")]
        public List<string> AllowedUiEffects { get; set; } = new List<string>();

        [JsonProperty("defaults")]
        public ActionDefaults Defaults { get; set; } = new ActionDefaults();
    }

    public sealed class ActionDefaults
    {
        [JsonProperty("loginPath")]
        public string LoginPath { get; set; } = "";
    }
}
