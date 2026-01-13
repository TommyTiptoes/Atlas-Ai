using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AtlasAI.AI
{
    public class OpenAIProvider : IAIProvider
    {
        private static readonly HttpClient httpClient = new HttpClient() { Timeout = TimeSpan.FromSeconds(60) };
        private string apiKey = "";

        public OpenAIProvider()
        {
            // Load API key from user's settings file
            LoadApiKeyFromSettings();
            
            // Initialize HTTP client with API key
            if (!string.IsNullOrEmpty(apiKey))
            {
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
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
                    if (doc.RootElement.TryGetProperty("openai", out var openaiKey))
                    {
                        var key = openaiKey.GetString();
                        if (!string.IsNullOrEmpty(key))
                        {
                            apiKey = key;
                            System.Diagnostics.Debug.WriteLine($"OpenAIProvider: Loaded key from ai_keys.json");
                            return;
                        }
                    }
                }
                
                // Try openai_key.txt
                var settingsPath = System.IO.Path.Combine(appDataPath, "openai_key.txt");
                if (System.IO.File.Exists(settingsPath))
                {
                    apiKey = System.IO.File.ReadAllText(settingsPath).Trim();
                    System.Diagnostics.Debug.WriteLine($"OpenAIProvider: Loaded key from openai_key.txt");
                    return;
                }
                
                // Also check settings.txt for backward compatibility
                var oldSettingsPath = System.IO.Path.Combine(appDataPath, "settings.txt");
                if (System.IO.File.Exists(oldSettingsPath))
                {
                    var content = System.IO.File.ReadAllText(oldSettingsPath).Trim();
                    // Check if it looks like an OpenAI API key
                    if (content.StartsWith("sk-") && !content.StartsWith("sk-ant-"))
                    {
                        apiKey = content;
                        System.Diagnostics.Debug.WriteLine($"OpenAIProvider: Loaded key from settings.txt");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OpenAIProvider: Error loading key: {ex.Message}");
            }
        }

        public string DisplayName => "OpenAI (GPT)";
        public AIProviderType ProviderType => AIProviderType.OpenAI;
        public bool IsConfigured => !string.IsNullOrEmpty(apiKey);

        public Task<bool> ConfigureAsync(Dictionary<string, string> config)
        {
            if (config.TryGetValue("ApiKey", out var key))
            {
                apiKey = key;
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                
                // Save API key to file for persistence
                try
                {
                    var settingsDir = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AtlasAI");
                    if (!System.IO.Directory.Exists(settingsDir))
                        System.IO.Directory.CreateDirectory(settingsDir);
                    System.IO.File.WriteAllText(System.IO.Path.Combine(settingsDir, "openai_key.txt"), apiKey);
                }
                catch { }
                
                return Task.FromResult(true);
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
                return new AIResponse { Success = false, Error = "🔑 OpenAI API key not configured. Please add your OpenAI API key in Settings → AI Provider." };

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
                        return new AIResponse 
                        { 
                            Success = false, 
                            Error = "🔑 **Invalid OpenAI API Key**\n\n" +
                                   "Your API key is not valid or has expired.\n\n" +
                                   "💡 **To fix this:**\n" +
                                   "1. Get a valid API key from: https://platform.openai.com/api-keys\n" +
                                   "2. Open Settings → AI Provider\n" +
                                   "3. Enter your new API key\n" +
                                   "4. Test the connection\n\n" +
                                   "📱 **Alternative:** Switch to Claude in Settings if you have an Anthropic API key."
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
                return new AIResponse { Success = false, Error = "⏱️ **Request Timed Out**\n\nThe request took too long. Please try again." };
            }
            catch (HttpRequestException ex)
            {
                return new AIResponse { Success = false, Error = $"🔴 **Network Error**\n\n{ex.Message}\n\nCheck your internet connection." };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OpenAI] Exception: {ex}");
                return new AIResponse { Success = false, Error = $"🔴 **OpenAI Error**\n\n{ex.Message}" };
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
