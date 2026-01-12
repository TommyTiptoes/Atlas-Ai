using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MinimalApp.AI
{
    /// <summary>
    /// Windows AI Provider - Placeholder for Microsoft's Phi Silica local LLM
    /// Requires Windows 11 Copilot+ PC with NPU and Windows App SDK
    /// Currently disabled - requires Visual Studio build for Windows App SDK
    /// </summary>
    public class WindowsAIProvider : IAIProvider
    {
        public string Name => "Windows AI (Phi Silica)";
        public string DisplayName => "Windows AI (Phi Silica)";
        public AIProviderType ProviderType => AIProviderType.WindowsAI;
        public bool IsConfigured { get; private set; } = false;
        public bool IsAvailable { get; private set; } = false;

        public Task<bool> ConfigureAsync(Dictionary<string, string> config)
        {
            // Windows AI requires Windows App SDK which needs Visual Studio to build
            // This is a placeholder - will be enabled when built with VS
            System.Diagnostics.Debug.WriteLine("[WindowsAI] Windows AI APIs require Windows App SDK (Visual Studio build)");
            IsAvailable = false;
            IsConfigured = false;
            return Task.FromResult(false);
        }

        public Task<List<AIModel>> GetModelsAsync()
        {
            return Task.FromResult(new List<AIModel>
            {
                new AIModel
                {
                    Id = "phi-silica-unavailable",
                    Name = "Phi Silica (Not Available)",
                    Description = "Requires Windows 11 Copilot+ PC with NPU. Build with Visual Studio to enable.",
                    MaxTokens = 0,
                    Provider = "Windows AI"
                }
            });
        }

        public Task<AIResponse> SendMessageAsync(List<object> messages, string model = "", int maxTokens = 500)
        {
            return Task.FromResult(new AIResponse
            {
                Success = false,
                Error = "Windows AI (Phi Silica) is not available. Requires:\n" +
                       "1. Windows 11 Copilot+ PC with NPU\n" +
                       "2. Build with Visual Studio (Windows App SDK)\n\n" +
                       "Use Claude or OpenAI instead."
            });
        }

        public Task<bool> TestConnectionAsync()
        {
            return Task.FromResult(false);
        }
    }
}
