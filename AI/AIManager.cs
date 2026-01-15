using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AtlasAI.Core;

namespace AtlasAI.AI
{
    public static class AIManager
    {
        private static readonly Dictionary<AIProviderType, IAIProvider> providers = new();
        private static AIProviderType activeProvider = AIProviderType.Claude;
        private static string selectedModel = "";
        
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AtlasAI", "ai_settings.json");

        public static event Action<AIProviderType>? ProviderChanged;
        public static event Action<string>? ConnectionStatusChanged;

        static AIManager()
        {
            // Register providers
            providers[AIProviderType.Claude] = new ClaudeProvider();
            providers[AIProviderType.OpenAI] = new OpenAIProvider();
            
            LoadSettings();
            
            // Subscribe to connection status changes
            ApiConnectionStatus.Instance.StatusChanged += OnConnectionStatusChanged;
        }
        
        private static void OnConnectionStatusChanged(string provider, ConnectionStatus status)
        {
            ConnectionStatusChanged?.Invoke($"{provider}: {status}");
        }

        public static async Task<bool> ConfigureProviderAsync(AIProviderType providerType, Dictionary<string, string> config)
        {
            if (providers.TryGetValue(providerType, out var provider))
            {
                var success = await provider.ConfigureAsync(config);
                if (success)
                {
                    SaveSettings();
                    
                    // Test connection after configuration
                    _ = TestProviderConnectionAsync(providerType);
                }
                return success;
            }
            return false;
        }
        
        /// <summary>
        /// Test connection to a specific provider
        /// </summary>
        public static async Task<bool> TestProviderConnectionAsync(AIProviderType providerType)
        {
            if (providers.TryGetValue(providerType, out var provider))
            {
                try
                {
                    return await provider.TestConnectionAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"AIManager: Test connection failed for {providerType}: {ex.Message}");
                    return false;
                }
            }
            return false;
        }
        
        /// <summary>
        /// Get connection status for the active provider
        /// </summary>
        public static ConnectionStatus GetActiveProviderStatus()
        {
            var providerName = activeProvider.ToString().ToLower();
            return ApiConnectionStatus.Instance.GetStatus(providerName);
        }
        
        /// <summary>
        /// Get connection status message for the active provider
        /// </summary>
        public static string GetActiveProviderStatusMessage()
        {
            var providerName = activeProvider.ToString().ToLower();
            return ApiConnectionStatus.Instance.GetStatusMessage(providerName);
        }
        
        /// <summary>
        /// Check if active provider has an API key configured
        /// </summary>
        public static bool HasActiveProviderApiKey()
        {
            var providerName = activeProvider.ToString().ToLower();
            return ApiKeyManager.HasApiKey(providerName);
        }

        public static async Task<bool> SetActiveProviderAsync(AIProviderType providerType)
        {
            System.Diagnostics.Debug.WriteLine($"Setting active provider to: {providerType}");
            if (providers.TryGetValue(providerType, out var provider))
            {
                activeProvider = providerType;
                SaveSettings();
                ProviderChanged?.Invoke(providerType);
                System.Diagnostics.Debug.WriteLine($"Active provider set successfully. IsConfigured: {provider.IsConfigured}");
                return true;
            }
            System.Diagnostics.Debug.WriteLine($"Failed to find provider: {providerType}");
            return false;
        }

        public static AIProviderType GetActiveProvider() => activeProvider;

        public static IAIProvider? GetProvider(AIProviderType providerType)
        {
            return providers.TryGetValue(providerType, out var provider) ? provider : null;
        }

        public static IAIProvider? GetActiveProviderInstance()
        {
            var provider = GetProvider(activeProvider);
            System.Diagnostics.Debug.WriteLine($"Getting active provider: {activeProvider}, Found: {provider != null}, Configured: {provider?.IsConfigured}");
            return provider;
        }

        public static List<IAIProvider> GetAllProviders()
        {
            return providers.Values.ToList();
        }

        public static List<IAIProvider> GetConfiguredProviders()
        {
            return providers.Values.Where(p => p.IsConfigured).ToList();
        }

        public static async Task<List<AIModel>> GetAvailableModelsAsync()
        {
            var provider = GetActiveProviderInstance();
            if (provider != null)
            {
                try
                {
                    return await provider.GetModelsAsync();
                }
                catch
                {
                    // If provider fails, return empty list but don't crash
                    return new List<AIModel>();
                }
            }
            return new List<AIModel>();
        }

        public static void SetSelectedModel(string modelId)
        {
            selectedModel = modelId;
            SaveSettings();
        }

        public static string GetSelectedModel() => selectedModel;

        public static async Task<AIResponse> SendMessageAsync(List<object> messages, int maxTokens = 500)
        {
            var provider = GetActiveProviderInstance();
            if (provider != null && provider.IsConfigured)
            {
                return await provider.SendMessageAsync(messages, selectedModel, maxTokens);
            }
            
            // Provide helpful guidance when no API key is configured
            var providerName = activeProvider == AIProviderType.Claude ? "Claude (Anthropic)" : "OpenAI";
            return new AIResponse 
            { 
                Success = false, 
                Error = $"🔑 **{providerName} API Key Required**\n\n" +
                       $"To use AI features like screenshot analysis and smart responses:\n" +
                       $"1. Open Settings → AI Provider\n" +
                       $"2. Add your {providerName} API key\n" +
                       $"3. Test the connection\n\n" +
                       $"💡 **Get API Keys:**\n" +
                       $"• Claude: https://console.anthropic.com/\n" +
                       $"• OpenAI: https://platform.openai.com/api-keys\n\n" +
                       $"Basic features like screenshots, system scan, and commands work without API keys."
            };
        }

        public static async Task<bool> TestActiveProviderAsync()
        {
            var provider = GetActiveProviderInstance();
            if (provider != null && provider.IsConfigured)
            {
                return await provider.TestConnectionAsync();
            }
            return false;
        }

        private static void LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    using var doc = JsonDocument.Parse(json);
                    
                    if (doc.RootElement.TryGetProperty("activeProvider", out var providerElement))
                    {
                        if (Enum.TryParse<AIProviderType>(providerElement.GetString(), out var provider))
                        {
                            activeProvider = provider;
                        }
                    }
                    
                    if (doc.RootElement.TryGetProperty("selectedModel", out var modelElement))
                    {
                        selectedModel = modelElement.GetString() ?? "";
                    }
                }
            }
            catch { }
        }

        private static void SaveSettings()
        {
            try
            {
                var dir = Path.GetDirectoryName(SettingsPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir!);
                
                var settings = new
                {
                    activeProvider = activeProvider.ToString(),
                    selectedModel = selectedModel
                };
                
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch { }
        }
    }
}