using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AtlasAI.AI
{
    public class ClaudeProvider : IAIProvider
    {
        private readonly HttpClient httpClient;
        private string apiKey = "";

        public ClaudeProvider()
        {
            httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30); // 30 second timeout to prevent hanging
            
            // Load API key from user's settings file
            LoadApiKeyFromSettings();
            
            // Initialize HTTP client with API key
            InitializeHttpClient();
        }
        
        private void InitializeHttpClient()
        {
            if (!string.IsNullOrEmpty(apiKey))
            {
                try
                {
                    httpClient.DefaultRequestHeaders.Clear();
                    httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
                    httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
                    System.Diagnostics.Debug.WriteLine($"ClaudeProvider: API key loaded, length={apiKey.Length}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ClaudeProvider: Error setting headers: {ex.Message}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("ClaudeProvider: No API key found");
            }
        }
        
        private void LoadApiKeyFromSettings()
        {
            try
            {
                var appDataPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AtlasAI");
                
                // First check ai_keys.json (where keys are stored)
                var keysPath = System.IO.Path.Combine(appDataPath, "ai_keys.json");
                if (System.IO.File.Exists(keysPath))
                {
                    var json = System.IO.File.ReadAllText(keysPath);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("claude", out var claudeKey))
                    {
                        var key = claudeKey.GetString();
                        if (!string.IsNullOrEmpty(key))
                        {
                            apiKey = key;
                            System.Diagnostics.Debug.WriteLine($"ClaudeProvider: Loaded key from ai_keys.json");
                            return;
                        }
                    }
                }
                
                // Check settings.txt
                var settingsPath = System.IO.Path.Combine(appDataPath, "settings.txt");
                if (System.IO.File.Exists(settingsPath))
                {
                    var content = System.IO.File.ReadAllText(settingsPath).Trim();
                    if (content.StartsWith("sk-ant-"))
                    {
                        apiKey = content;
                        System.Diagnostics.Debug.WriteLine($"ClaudeProvider: Loaded key from settings.txt");
                        return;
                    }
                }
                
                // Also check claude_key.txt
                var claudeKeyPath = System.IO.Path.Combine(appDataPath, "claude_key.txt");
                if (System.IO.File.Exists(claudeKeyPath))
                {
                    apiKey = System.IO.File.ReadAllText(claudeKeyPath).Trim();
                    System.Diagnostics.Debug.WriteLine($"ClaudeProvider: Loaded key from claude_key.txt");
                    return;
                }
                
                System.Diagnostics.Debug.WriteLine("ClaudeProvider: No API key file found");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ClaudeProvider: Error loading key: {ex.Message}");
            }
        }

        public string DisplayName => "Claude (Anthropic)";
        public AIProviderType ProviderType => AIProviderType.Claude;
        public bool IsConfigured => !string.IsNullOrEmpty(apiKey);

        public Task<bool> ConfigureAsync(Dictionary<string, string> config)
        {
            if (config.TryGetValue("ApiKey", out var key))
            {
                apiKey = key;
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
                httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
                
                // Save API key to file for persistence
                try
                {
                    var settingsDir = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AtlasAI");
                    if (!System.IO.Directory.Exists(settingsDir))
                        System.IO.Directory.CreateDirectory(settingsDir);
                    System.IO.File.WriteAllText(System.IO.Path.Combine(settingsDir, "claude_key.txt"), apiKey);
                }
                catch { }
                
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public Task<List<AIModel>> GetModelsAsync()
        {
            System.Diagnostics.Debug.WriteLine("ClaudeProvider.GetModelsAsync() called");
            var models = new List<AIModel>
            {
                // Claude 4.5 models (Latest - Nov 2025)
                new AIModel { Id = "claude-opus-4-5-20251124", DisplayName = "Claude Opus 4.5 (Latest)", Description = "Most capable", MaxTokens = 200000 },
                new AIModel { Id = "claude-sonnet-4-5-20250929", DisplayName = "Claude Sonnet 4.5", Description = "Best for coding & agents", MaxTokens = 200000 },
                new AIModel { Id = "claude-haiku-4-5-20251015", DisplayName = "Claude Haiku 4.5", Description = "Fastest responses", MaxTokens = 200000 },
                // Claude 4 models (May 2025)
                new AIModel { Id = "claude-sonnet-4-20250514", DisplayName = "Claude Sonnet 4", Description = "Balanced performance", MaxTokens = 200000 },
                new AIModel { Id = "claude-opus-4-20250514", DisplayName = "Claude Opus 4", Description = "Complex reasoning", MaxTokens = 200000 },
                // Claude 3.7 (Feb 2025)
                new AIModel { Id = "claude-3-7-sonnet-20250219", DisplayName = "Claude 3.7 Sonnet", Description = "Extended thinking", MaxTokens = 200000 },
                // Claude 3.5 models (Stable fallback)
                new AIModel { Id = "claude-3-5-sonnet-20241022", DisplayName = "Claude 3.5 Sonnet", Description = "Proven stable", MaxTokens = 200000 },
                new AIModel { Id = "claude-3-5-haiku-20241022", DisplayName = "Claude 3.5 Haiku", Description = "Fast responses", MaxTokens = 200000 }
            };
            System.Diagnostics.Debug.WriteLine($"ClaudeProvider returning {models.Count} models");
            return Task.FromResult(models);
        }

        public async Task<AIResponse> SendMessageAsync(List<object> messages, string model = "", int maxTokens = 500)
        {
            if (!IsConfigured)
                return new AIResponse { Success = false, Error = "🔑 Claude API key not configured. Please add your Anthropic API key in Settings → AI Provider." };

            try
            {
                // Use Claude Sonnet 4.5 as default (latest recommended)
                var selectedModel = string.IsNullOrEmpty(model) ? "claude-sonnet-4-5-20250929" : model;
                
                // Filter out system messages - Claude uses a separate system parameter
                var filteredMessages = new List<object>();
                string systemPrompt = "You are Atlas, a friendly AI assistant with a warm personality. Be conversational and natural, like talking to a friend. Keep responses concise but warm.";
                
                foreach (var msg in messages)
                {
                    // Check if this is a system message and extract it
                    var msgJson = JsonSerializer.Serialize(msg);
                    using var msgDoc = JsonDocument.Parse(msgJson);
                    if (msgDoc.RootElement.TryGetProperty("role", out var roleElement))
                    {
                        var role = roleElement.GetString();
                        if (role == "system")
                        {
                            // Extract system content but don't add to messages
                            if (msgDoc.RootElement.TryGetProperty("content", out var contentElement))
                            {
                                systemPrompt = contentElement.GetString() ?? systemPrompt;
                            }
                            continue; // Skip system messages
                        }
                    }
                    filteredMessages.Add(msg);
                }
                
                var request = new
                {
                    model = selectedModel,
                    max_tokens = maxTokens,
                    system = systemPrompt,
                    messages = filteredMessages
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync("https://api.anthropic.com/v1/messages", content);
                var responseJson = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        return new AIResponse 
                        { 
                            Success = false, 
                            Error = "🔑 **Invalid Claude API Key**\n\n" +
                                   "Your Anthropic API key is not valid or has expired.\n\n" +
                                   "💡 **To fix this:**\n" +
                                   "1. Get a valid API key from: https://console.anthropic.com/\n" +
                                   "2. Open Settings → AI Provider\n" +
                                   "3. Enter your new API key\n" +
                                   "4. Test the connection\n\n" +
                                   "📱 **Alternative:** Switch to OpenAI in Settings if you have an OpenAI API key."
                        };
                    }
                    return new AIResponse 
                    { 
                        Success = false, 
                        Error = $"🔴 **Claude API Error**\n\nStatus: {response.StatusCode}\nDetails: {responseJson}" 
                    };
                }

                using var doc = JsonDocument.Parse(responseJson);
                var assistantMessage = doc.RootElement
                    .GetProperty("content")[0]
                    .GetProperty("text")
                    .GetString() ?? "Sorry, I couldn't generate a response.";

                var tokensUsed = 0;
                if (doc.RootElement.TryGetProperty("usage", out var usage))
                {
                    if (usage.TryGetProperty("output_tokens", out var outputTokens))
                        tokensUsed = outputTokens.GetInt32();
                }

                return new AIResponse
                {
                    Success = true,
                    Content = assistantMessage,
                    TokensUsed = tokensUsed,
                    Model = selectedModel
                };
            }
            catch (Exception ex)
            {
                return new AIResponse { Success = false, Error = $"🔴 **Claude Connection Error**\n\n{ex.Message}\n\nCheck your internet connection and API key." };
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            if (!IsConfigured) return false;

            try
            {
                var testMessages = new List<object>
                {
                    new { role = "user", content = "Hello" }
                };

                var response = await SendMessageAsync(testMessages, "", 10);
                return response.Success;
            }
            catch
            {
                return false;
            }
        }
    }
}