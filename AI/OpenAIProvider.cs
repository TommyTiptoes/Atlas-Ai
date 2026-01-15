using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AtlasAI.Core;

namespace AtlasAI.AI
{
    public class OpenAIProvider : IAIProvider, IDisposable
    {
        private static readonly HttpClient httpClient = new HttpClient() { Timeout = TimeSpan.FromSeconds(60) };
        private string apiKey = "";
        private bool _disposed = false;

        public OpenAIProvider()
        {
            // Load API key from centralized manager
            LoadApiKeyFromManager();
            
            // Initialize HTTP client with API key
            InitializeHttpClient();
            
            // Subscribe to key changes
            ApiKeyManager.KeyChanged += OnApiKeyChanged;
        }
        
        ~OpenAIProvider()
        {
            Dispose(false);
        }
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Unsubscribe from events to prevent memory leaks
                    ApiKeyManager.KeyChanged -= OnApiKeyChanged;
                }
                _disposed = true;
            }
        }
        
        private void LoadApiKeyFromManager()
        {
            apiKey = ApiKeyManager.GetApiKey("openai");
            if (!string.IsNullOrEmpty(apiKey))
            {
                System.Diagnostics.Debug.WriteLine($"OpenAIProvider: Loaded key from ApiKeyManager");
            }
        }
        
        private void InitializeHttpClient()
        {
            if (!string.IsNullOrEmpty(apiKey))
            {
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            }
        }
        
        private void OnApiKeyChanged(string provider)
        {
            if (provider.ToLower() == "openai")
            {
                LoadApiKeyFromManager();
                InitializeHttpClient();
                System.Diagnostics.Debug.WriteLine("OpenAIProvider: API key updated");
            }
        }

        public string DisplayName => "OpenAI (GPT)";
        public AIProviderType ProviderType => AIProviderType.OpenAI;
        public bool IsConfigured => !string.IsNullOrEmpty(apiKey);

        public Task<bool> ConfigureAsync(Dictionary<string, string> config)
        {
            if (config.TryGetValue("ApiKey", out var key))
            {
                // Validate key format before saving
                if (!ApiKeyManager.IsValidKeyFormat("openai", key))
                {
                    System.Diagnostics.Debug.WriteLine("OpenAIProvider: Invalid API key format");
                    return Task.FromResult(false);
                }
                
                // Save to centralized manager
                if (ApiKeyManager.SaveApiKey("openai", key))
                {
                    apiKey = key;
                    InitializeHttpClient();
                    
                    // Clear connection status to force retest
                    ApiConnectionStatus.Instance.ClearStatus("openai");
                    
                    return Task.FromResult(true);
                }
                
                return Task.FromResult(false);
            }
            return Task.FromResult(false);
        }

        // Valid OpenAI models (January 2026)
        private static readonly HashSet<string> ValidModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // GPT-5.2 family (Latest - Dec 2025)
            "gpt-5.2", "gpt-5.2-chat-latest", "gpt-5.2-pro",
            // GPT-5.1 family
            "gpt-5.1", "gpt-5.1-mini", "gpt-5.1-nano",
            // GPT-5 family (Aug 2025)
            "gpt-5", "gpt-5-mini", "gpt-5-nano",
            // GPT-4o family (fallback)
            "gpt-4o", "gpt-4o-mini",
            // Legacy
            "gpt-4-turbo", "gpt-4", "gpt-3.5-turbo",
            // Reasoning models
            "o1", "o1-mini", "o1-preview", "o3-mini", "o4-mini"
        };
        
        public Task<List<AIModel>> GetModelsAsync()
        {
            System.Diagnostics.Debug.WriteLine("OpenAIProvider.GetModelsAsync() called");
            var models = new List<AIModel>
            {
                // GPT-5.2 models (Latest - Dec 2025)
                new AIModel { Id = "gpt-5.2", DisplayName = "GPT-5.2 (Latest)", Description = "Most advanced - thinking mode", MaxTokens = 400000 },
                new AIModel { Id = "gpt-5.2-chat-latest", DisplayName = "GPT-5.2 Chat", Description = "ChatGPT model - instant", MaxTokens = 400000 },
                new AIModel { Id = "gpt-5.2-pro", DisplayName = "GPT-5.2 Pro", Description = "Extended thinking", MaxTokens = 400000 },
                // GPT-5.1 models
                new AIModel { Id = "gpt-5.1", DisplayName = "GPT-5.1", Description = "Previous flagship", MaxTokens = 256000 },
                new AIModel { Id = "gpt-5.1-mini", DisplayName = "GPT-5.1 Mini", Description = "Fast and efficient", MaxTokens = 256000 },
                // GPT-5 (Aug 2025)
                new AIModel { Id = "gpt-5", DisplayName = "GPT-5", Description = "Original GPT-5", MaxTokens = 128000 },
                new AIModel { Id = "gpt-5-mini", DisplayName = "GPT-5 Mini", Description = "Smaller GPT-5", MaxTokens = 128000 },
                // GPT-4o models (fallback)
                new AIModel { Id = "gpt-4o", DisplayName = "GPT-4o", Description = "Multimodal GPT-4", MaxTokens = 128000 },
                new AIModel { Id = "gpt-4o-mini", DisplayName = "GPT-4o Mini", Description = "Fast and affordable", MaxTokens = 128000 },
                // Reasoning models
                new AIModel { Id = "o4-mini", DisplayName = "o4 Mini", Description = "Latest reasoning", MaxTokens = 128000 },
                new AIModel { Id = "o3-mini", DisplayName = "o3 Mini", Description = "Fast reasoning", MaxTokens = 128000 },
                new AIModel { Id = "o1", DisplayName = "o1", Description = "Advanced reasoning", MaxTokens = 128000 }
            };
            System.Diagnostics.Debug.WriteLine($"OpenAIProvider returning {models.Count} models");
            return Task.FromResult(models);
        }

        public async Task<AIResponse> SendMessageAsync(List<object> messages, string model = "", int maxTokens = 500)
        {
            if (!IsConfigured)
            {
                ApiConnectionStatus.Instance.UpdateStatus("openai", ConnectionStatus.NoApiKey, "No API key configured");
                return new AIResponse { 
                    Success = false, 
                    Error = "🔑 **OpenAI API Key Required**\n\n" +
                           "To use OpenAI:\n" +
                           "1. Get an API key from: https://platform.openai.com/api-keys\n" +
                           "2. Open Settings → AI Provider\n" +
                           "3. Select OpenAI and enter your API key\n" +
                           "4. Click Test Connection to verify\n\n" +
                           "💡 **Alternative:** Switch to Claude in Settings if you have an Anthropic API key."
                };
            }

            try
            {
                // Validate model - fallback to gpt-5.2-chat-latest if empty or invalid
                var selectedModel = model;
                if (string.IsNullOrEmpty(selectedModel) || !ValidModels.Contains(selectedModel))
                {
                    System.Diagnostics.Debug.WriteLine($"[OpenAI] Model '{model}' not in valid list, using gpt-5.2-chat-latest");
                    selectedModel = "gpt-5.2-chat-latest";
                }
                
                // Messages already contain system prompt from caller - use them directly
                var openAIMessages = new List<object>(messages);

                // Use max_completion_tokens for GPT-5.x and o-series models, max_tokens for older ones
                object request;
                if (selectedModel.StartsWith("gpt-5") || selectedModel.StartsWith("o1") || selectedModel.StartsWith("o3") || selectedModel.StartsWith("o4"))
                {
                    // GPT-5.x and reasoning models need more tokens for thinking + output
                    var minTokens = Math.Max(maxTokens, 4096);
                    request = new
                    {
                        model = selectedModel,
                        messages = openAIMessages,
                        max_completion_tokens = minTokens
                    };
                }
                else
                {
                    request = new
                    {
                        model = selectedModel,
                        messages = openAIMessages,
                        max_tokens = maxTokens,
                        temperature = 0.7
                    };
                }

                var json = JsonSerializer.Serialize(request);
                System.Diagnostics.Debug.WriteLine($"[OpenAI] Sending request to model: {selectedModel}, messages: {messages.Count}");
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
                var responseJson = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"[OpenAI] Response status: {response.StatusCode}, Length: {responseJson.Length}");

                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"[OpenAI] Error response: {responseJson}");
                    
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        ApiConnectionStatus.Instance.UpdateStatus("openai", ConnectionStatus.InvalidKey, "Invalid API key");
                        return new AIResponse 
                        { 
                            Success = false, 
                            Error = "🔑 **Invalid OpenAI API Key**\n\n" +
                                   "Your API key is not valid or has expired.\n\n" +
                                   "💡 **To fix this:**\n" +
                                   "1. Get a valid API key from: https://platform.openai.com/api-keys\n" +
                                   "2. Open Settings → AI Provider\n" +
                                   "3. Enter your new API key\n" +
                                   "4. Click Test Connection to verify\n\n" +
                                   "📱 **Alternative:** Switch to Claude in Settings if you have an Anthropic API key."
                        };
                    }
                    
                    if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        ApiConnectionStatus.Instance.UpdateStatus("openai", ConnectionStatus.RateLimited, "Rate limited");
                        return new AIResponse 
                        { 
                            Success = false, 
                            Error = "⏸️ **Rate Limit Exceeded**\n\nYou've made too many requests. Please wait a moment and try again.\n\n" +
                                   "💡 **Tips:**\n" +
                                   "- Wait 1-2 minutes before trying again\n" +
                                   "- Consider upgrading your OpenAI plan for higher limits\n" +
                                   "- Switch to Claude temporarily if available"
                        };
                    }
                    
                    // Check for model not found error
                    if (responseJson.Contains("model_not_found") || responseJson.Contains("does not exist"))
                    {
                        return new AIResponse 
                        { 
                            Success = false, 
                            Error = $"🔴 **Model Not Available**\n\nThe model '{selectedModel}' is not available on your account.\n\nTry selecting a different model in Settings → AI Provider."
                        };
                    }
                    
                    ApiConnectionStatus.Instance.UpdateStatus("openai", ConnectionStatus.Disconnected, $"API error: {response.StatusCode}");
                    return new AIResponse 
                    { 
                        Success = false, 
                        Error = $"🔴 **OpenAI API Error**\n\nStatus: {response.StatusCode}\nDetails: {responseJson}" 
                    };
                }

                using var doc = JsonDocument.Parse(responseJson);
                
                // Try to extract content - handle different response formats
                string assistantMessage = "";
                
                try
                {
                    // Standard format: choices[0].message.content
                    if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                    {
                        var firstChoice = choices[0];
                        if (firstChoice.TryGetProperty("message", out var message))
                        {
                            if (message.TryGetProperty("content", out var msgContent))
                            {
                                assistantMessage = msgContent.GetString() ?? "";
                            }
                        }
                    }
                    
                    // If still empty, try output format (some newer models)
                    if (string.IsNullOrWhiteSpace(assistantMessage) && doc.RootElement.TryGetProperty("output", out var output))
                    {
                        assistantMessage = output.GetString() ?? "";
                    }
                }
                catch (Exception parseEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[OpenAI] Parse error: {parseEx.Message}");
                    System.Diagnostics.Debug.WriteLine($"[OpenAI] Raw response: {responseJson}");
                }
                
                // Check for empty content
                if (string.IsNullOrWhiteSpace(assistantMessage))
                {
                    System.Diagnostics.Debug.WriteLine($"[OpenAI] Empty content in response: {responseJson}");
                    
                    // Show truncated response for debugging
                    var truncatedResponse = responseJson.Length > 500 ? responseJson.Substring(0, 500) + "..." : responseJson;
                    return new AIResponse 
                    { 
                        Success = false, 
                        Error = $"🤔 The AI returned an empty response.\n\nModel: {selectedModel}\nResponse preview: {truncatedResponse}"
                    };
                }

                var tokensUsed = 0;
                if (doc.RootElement.TryGetProperty("usage", out var usage))
                {
                    if (usage.TryGetProperty("completion_tokens", out var completionTokens))
                        tokensUsed = completionTokens.GetInt32();
                }

                // Update connection status on success
                ApiConnectionStatus.Instance.UpdateStatus("openai", ConnectionStatus.Connected);
                
                return new AIResponse
                {
                    Success = true,
                    Content = assistantMessage,
                    TokensUsed = tokensUsed,
                    Model = selectedModel
                };
            }
            catch (TaskCanceledException)
            {
                ApiConnectionStatus.Instance.UpdateStatus("openai", ConnectionStatus.Disconnected, "Request timeout");
                return new AIResponse { 
                    Success = false, 
                    Error = "⏱️ **Request Timed Out**\n\n" +
                           "The request took too long (60s limit).\n\n" +
                           "💡 **What to try:**\n" +
                           "- Check your internet connection\n" +
                           "- Try again in a moment\n" +
                           "- Use a smaller/faster model if available"
                };
            }
            catch (HttpRequestException ex)
            {
                ApiConnectionStatus.Instance.UpdateStatus("openai", ConnectionStatus.Disconnected, "Network error");
                return new AIResponse { 
                    Success = false, 
                    Error = $"🔴 **Network Error**\n\n{ex.Message}\n\n" +
                           "💡 **Troubleshooting:**\n" +
                           "- Check your internet connection\n" +
                           "- Verify firewall/proxy settings\n" +
                           "- Try switching networks (WiFi/mobile data)"
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OpenAI] Exception: {ex}");
                ApiConnectionStatus.Instance.UpdateStatus("openai", ConnectionStatus.Disconnected, ex.Message);
                return new AIResponse { Success = false, Error = $"🔴 **OpenAI Error**\n\n{ex.Message}" };
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            if (!IsConfigured)
            {
                ApiConnectionStatus.Instance.UpdateStatus("openai", ConnectionStatus.NoApiKey);
                return false;
            }

            // Use the connection status manager for testing with retry logic
            return await ApiConnectionStatus.Instance.TestConnectionAsync("openai", async () =>
            {
                try
                {
                    var testMessages = new List<object>
                    {
                        new { role = "user", content = "test" }
                    };

                    var response = await SendMessageAsync(testMessages, "", 10);
                    return response.Success;
                }
                catch
                {
                    return false;
                }
            });
        }
    }
}
