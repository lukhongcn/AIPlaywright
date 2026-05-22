using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ModuleWorkFlow.AIHelp.Tools
{
    public sealed class ToolRegistry
    {
        private readonly ToolRegistryFile _registryFile;

        private ToolRegistry(ToolRegistryFile registryFile)
        {
            _registryFile = registryFile ?? new ToolRegistryFile();
        }

        public static ToolRegistry Load(string toolsPath = null)
        {
            if (string.IsNullOrWhiteSpace(toolsPath))
            {
                toolsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools.json");
            }

            if (!File.Exists(toolsPath))
            {
                throw new FileNotFoundException("找不到 tools.json：" + toolsPath);
            }

            var json = File.ReadAllText(toolsPath);
            var registryFile = JsonConvert.DeserializeObject<ToolRegistryFile>(json);

            if (registryFile == null)
            {
                throw new Exception("tools.json 解析失敗");
            }

            if (registryFile.Actions == null || registryFile.Actions.Count == 0)
            {
                throw new Exception("tools.json 中沒有任何 action 定義");
            }

            return new ToolRegistry(registryFile);
        }

        public ActionToolDefinition GetAction(string action)
        {
            if (string.IsNullOrWhiteSpace(action))
            {
                return null;
            }

            return _registryFile.Actions
                .FirstOrDefault(x => string.Equals(x.Action, action, StringComparison.OrdinalIgnoreCase));
        }

        public IReadOnlyList<ActionToolDefinition> GetAllActions()
        {
            return _registryFile.Actions == null
                ? new List<ActionToolDefinition>()
                : _registryFile.Actions.ToList();
        }
    }
}
