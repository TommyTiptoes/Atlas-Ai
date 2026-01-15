using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Diagnostics;
using System.Linq;

namespace AtlasAI.Core
{
    /// <summary>
    /// Centralized API key management for all providers.
    /// Loads keys from multiple sources: ai_keys.json, individual .txt files, and environment variables.
    /// Provides validation and secure storage.
    /// </summary>
    public static class ApiKeyManager
    {
        private static readonly string AppDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AtlasAI");
        
        private static readonly string KeysFilePath = Path.Combine(AppDataPath, "ai_keys.json");
        
        // Cache for loaded keys
        private static readonly Dictionary<string, string> _keyCache = new();
        private static bool _cacheInitialized = false;
        
        public static event Action<string>? KeyChanged;
        
        /// <summary>
        /// Key source priority: 1) Environment variable, 2) ai_keys.json, 3) Individual .txt files
        /// </summary>
        public static string GetApiKey(string provider)
        {
            if (!_cacheInitialized)
            {
                LoadAllKeys();
            }
            
            return _keyCache.TryGetValue(provider.ToLower(), out var key) ? key : string.Empty;
        }
        
        /// <summary>
        /// Load all API keys from all sources
        /// </summary>
        private static void LoadAllKeys()
        {
            _keyCache.Clear();
            
            // Load from ai_keys.json first
            LoadFromKeysJson();
            
            // Load from individual .txt files (override if exists)
            LoadFromIndividualFiles();
            
            // Load from environment variables (highest priority)
            LoadFromEnvironmentVariables();
            
            _cacheInitialized = true;
            
            Debug.WriteLine($"[ApiKeyManager] Loaded {_keyCache.Count} API keys");
        }
        
        private static void LoadFromKeysJson()
        {
            try
            {
                if (File.Exists(KeysFilePath))
                {
                    var json = File.ReadAllText(KeysFilePath);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    
                    foreach (var property in root.EnumerateObject())
                    {
                        var key = property.Value.GetString();
                        if (!string.IsNullOrEmpty(key) && IsValidKeyFormat(property.Name, key))
                        {
                            _keyCache[property.Name.ToLower()] = key;
                            Debug.WriteLine($"[ApiKeyManager] Loaded {property.Name} from ai_keys.json");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ApiKeyManager] Error loading ai_keys.json: {ex.Message}");
            }
        }
        
        private static void LoadFromIndividualFiles()
        {
            try
            {
                if (!Directory.Exists(AppDataPath))
                    return;
                    
                // Map of provider names to file patterns
                var filePatterns = new Dictionary<string, string[]>
                {
                    { "openai", new[] { "openai_key.txt", "settings.txt" } },
                    { "claude", new[] { "claude_key.txt", "settings.txt" } },
                    { "elevenlabs", new[] { "elevenlabs_key.txt" } },
                    { "canva", new[] { "canva_key.txt" } }
                };
                
                foreach (var kvp in filePatterns)
                {
                    foreach (var pattern in kvp.Value)
                    {
                        var filePath = Path.Combine(AppDataPath, pattern);
                        if (File.Exists(filePath))
                        {
                            var content = File.ReadAllText(filePath).Trim();
                            
                            // For settings.txt, determine provider by key format
                            if (pattern == "settings.txt")
                            {
                                if (content.StartsWith("sk-ant-") && kvp.Key == "claude")
                                {
                                    _keyCache[kvp.Key] = content;
                                    Debug.WriteLine($"[ApiKeyManager] Loaded {kvp.Key} from {pattern}");
                                    break;
                                }
                                else if (content.StartsWith("sk-") && !content.StartsWith("sk-ant-") && kvp.Key == "openai")
                                {
                                    _keyCache[kvp.Key] = content;
                                    Debug.WriteLine($"[ApiKeyManager] Loaded {kvp.Key} from {pattern}");
                                    break;
                                }
                            }
                            else if (IsValidKeyFormat(kvp.Key, content))
                            {
                                _keyCache[kvp.Key] = content;
                                Debug.WriteLine($"[ApiKeyManager] Loaded {kvp.Key} from {pattern}");
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ApiKeyManager] Error loading individual files: {ex.Message}");
            }
        }
        
        private static void LoadFromEnvironmentVariables()
        {
            try
            {
                var envVars = new Dictionary<string, string>
                {
                    { "openai", "OPENAI_API_KEY" },
                    { "claude", "ANTHROPIC_API_KEY" },
                    { "elevenlabs", "ELEVENLABS_API_KEY" },
                    { "canva", "CANVA_API_KEY" }
                };
                
                foreach (var kvp in envVars)
                {
                    var value = Environment.GetEnvironmentVariable(kvp.Value);
                    if (!string.IsNullOrEmpty(value) && IsValidKeyFormat(kvp.Key, value))
                    {
                        _keyCache[kvp.Key] = value;
                        Debug.WriteLine($"[ApiKeyManager] Loaded {kvp.Key} from environment variable {kvp.Value}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ApiKeyManager] Error loading environment variables: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Validate API key format based on provider
        /// </summary>
        public static bool IsValidKeyFormat(string provider, string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;
                
            switch (provider.ToLower())
            {
                case "openai":
                    // OpenAI keys start with "sk-" (not "sk-ant-") and are typically 48+ chars
                    return key.StartsWith("sk-") && !key.StartsWith("sk-ant-") && key.Length >= 20;
                    
                case "claude":
                case "anthropic":
                    // Claude/Anthropic keys start with "sk-ant-"
                    return key.StartsWith("sk-ant-") && key.Length >= 20;
                    
                case "elevenlabs":
                    // ElevenLabs keys are typically 32 hex characters
                    return key.Length >= 20;
                    
                case "canva":
                    // Canva API keys vary, just check basic length
                    return key.Length >= 10;
                    
                default:
                    // For unknown providers, just check it's not empty
                    return key.Length >= 10;
            }
        }
        
        /// <summary>
        /// Save API key to persistent storage (ai_keys.json)
        /// </summary>
        public static bool SaveApiKey(string provider, string key)
        {
            try
            {
                if (!IsValidKeyFormat(provider, key))
                {
                    Debug.WriteLine($"[ApiKeyManager] Invalid key format for {provider}");
                    return false;
                }
                
                // Ensure directory exists
                if (!Directory.Exists(AppDataPath))
                    Directory.CreateDirectory(AppDataPath);
                
                // Load existing keys
                var keys = new Dictionary<string, string>();
                if (File.Exists(KeysFilePath))
                {
                    var json = File.ReadAllText(KeysFilePath);
                    using var doc = JsonDocument.Parse(json);
                    foreach (var property in doc.RootElement.EnumerateObject())
                    {
                        keys[property.Name.ToLower()] = property.Value.GetString() ?? "";
                    }
                }
                
                // Update with new key
                keys[provider.ToLower()] = key;
                
                // Save to file
                var updatedJson = JsonSerializer.Serialize(keys, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(KeysFilePath, updatedJson);
                
                // Update cache
                _keyCache[provider.ToLower()] = key;
                
                Debug.WriteLine($"[ApiKeyManager] Saved {provider} API key");
                KeyChanged?.Invoke(provider);
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ApiKeyManager] Error saving API key: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Check if a provider has a configured API key
        /// </summary>
        public static bool HasApiKey(string provider)
        {
            return !string.IsNullOrEmpty(GetApiKey(provider));
        }
        
        /// <summary>
        /// Get all configured providers
        /// </summary>
        public static List<string> GetConfiguredProviders()
        {
            if (!_cacheInitialized)
            {
                LoadAllKeys();
            }
            
            return _keyCache.Keys.ToList();
        }
        
        /// <summary>
        /// Clear the key cache and reload from disk
        /// </summary>
        public static void RefreshKeys()
        {
            _cacheInitialized = false;
            LoadAllKeys();
        }
        
        /// <summary>
        /// Mask an API key for display (show first/last few chars)
        /// </summary>
        public static string MaskApiKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return "";
                
            if (key.Length <= 8)
                return "****";
                
            return $"{key.Substring(0, 4)}...{key.Substring(key.Length - 4)}";
        }
    }
}
