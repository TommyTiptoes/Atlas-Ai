using System;
using System.Collections.Generic;

namespace AtlasAI.Core
{
    /// <summary>
    /// Registry for feature modules in the application.
    /// Tracks available features and their metadata.
    /// </summary>
    public sealed class ModuleRegistry
    {
        private static ModuleRegistry? _instance;
        public static ModuleRegistry Instance => _instance ??= new ModuleRegistry();

        private readonly Dictionary<string, ModuleInfo> _modules = new(StringComparer.OrdinalIgnoreCase);

        public void RegisterModule(string key, string displayName, string icon, string description = "")
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Module key cannot be null/empty.", nameof(key));

            _modules[key] = new ModuleInfo
            {
                Key = key,
                DisplayName = displayName,
                Icon = icon,
                Description = description
            };
        }

        public IReadOnlyCollection<ModuleInfo> GetAllModules() => _modules.Values;

        public bool TryGetModule(string key, out ModuleInfo? module)
        {
            return _modules.TryGetValue(key, out module);
        }

        public class ModuleInfo
        {
            public string Key { get; init; } = string.Empty;
            public string DisplayName { get; init; } = string.Empty;
            public string Icon { get; init; } = string.Empty;
            public string Description { get; init; } = string.Empty;
        }
    }
}
