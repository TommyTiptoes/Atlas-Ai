using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MinimalApp.AI;

namespace MinimalApp.Agent
{
    /// <summary>
    /// Represents an action taken by the agent (for history/undo)
    /// </summary>
    public class AgentAction
    {
        public string Tool { get; set; } = "";
        public Dictionary<string, object> Params { get; set; } = new();
        public string Result { get; set; } = "";
        public bool Success { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string? UndoData { get; set; } // For undo support (e.g., original file content)
    }
    
    /// <summary>
    /// The Agent Orchestrator - runs the AI in a loop, executing tools until task is complete.
    /// This is what makes Atlas work like Kiro - it can actually DO things, not just talk.
    /// </summary>
    public class AgentOrchestrator
    {
        private readonly string _workspacePath;
        private readonly List<AgentMessage> _conversationHistory = new();
        private readonly List<AgentAction> _actionHistory = new();
        private const int MaxIterations = 20; // Safety limit
        
        // Destructive operations that require confirmation
        private static readonly HashSet<string> DestructiveTools = new(StringComparer.OrdinalIgnoreCase)
        {
            "delete_file", "uninstall_software", "uninstall", "run_command", "run_powershell"
        };
        
        public event EventHandler<string>? OnThinking;
        public event EventHandler<string>? OnToolExecuting;
        public event EventHandler<ToolResult>? OnToolResult;
        public event EventHandler<string>? OnResponse;
        public event EventHandler<string>? OnError;
        
        /// <summary>
        /// Event raised when a destructive operation needs confirmation.
        /// Handler should return true to proceed, false to cancel.
        /// </summary>
        public Func<string, string, Task<bool>>? OnConfirmationRequired;
        
        /// <summary>
        /// Get the history of actions taken by the agent
        /// </summary>
        public IReadOnlyList<AgentAction> ActionHistory => _actionHistory.AsReadOnly();

        public AgentOrchestrator(string workspacePath)
        {
            _workspacePath = workspacePath;
        }
        
        /// <summary>
        /// Undo the last file write action
        /// </summary>
        public async Task<string> UndoLastActionAsync()
        {
            // Find the last undoable action (file writes)
            for (int i = _actionHistory.Count - 1; i >= 0; i--)
            {
                var action = _actionHistory[i];
                if (action.Tool == "write_file" && !string.IsNullOrEmpty(action.UndoData))
                {
                    var path = action.Params.GetValueOrDefault("path")?.ToString();
                    if (!string.IsNullOrEmpty(path))
                    {
                        var fullPath = Path.Combine(_workspacePath, path);
                        if (action.UndoData == "__NEW_FILE__")
                        {
                            // File was newly created - delete it
                            if (File.Exists(fullPath))
                            {
                                File.Delete(fullPath);
                                _actionHistory.RemoveAt(i);
                                return $"✅ Undone: Deleted newly created file `{path}`";
                            }
                        }
                        else
                        {
                            // Restore original content
                            await File.WriteAllTextAsync(fullPath, action.UndoData);
                            _actionHistory.RemoveAt(i);
                            return $"✅ Undone: Restored `{path}` to previous version";
                        }
                    }
                }
                else if (action.Tool == "delete_file" && !string.IsNullOrEmpty(action.UndoData))
                {
                    var path = action.Params.GetValueOrDefault("path")?.ToString();
                    if (!string.IsNullOrEmpty(path))
                    {
                        var fullPath = Path.Combine(_workspacePath, path);
                        await File.WriteAllTextAsync(fullPath, action.UndoData);
                        _actionHistory.RemoveAt(i);
                        return $"✅ Undone: Restored deleted file `{path}`";
                    }
                }
            }
            return "❌ No undoable actions found";
        }
        
        /// <summary>
        /// Get a summary of recent actions
        /// </summary>
        public string GetActionSummary(int count = 5)
        {
            if (_actionHistory.Count == 0)
                return "No agent actions recorded yet.";
            
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("📋 **Recent Agent Actions:**\n");
            
            var recent = _actionHistory.TakeLast(count).Reverse();
            foreach (var action in recent)
            {
                var status = action.Success ? "✅" : "❌";
                var time = action.Timestamp.ToString("HH:mm:ss");
                sb.AppendLine($"{status} `{action.Tool}` at {time}");
                if (action.Params.TryGetValue("path", out var path))
                    sb.AppendLine($"   📄 {path}");
            }
            
            return sb.ToString();
        }
        
        /// <summary>
        /// Generate a human-readable description of a tool call for confirmation dialogs
        /// </summary>
        private string GetToolDescription(ToolCall toolCall)
        {
            return toolCall.Tool.ToLower() switch
            {
                "delete_file" => $"Delete file: {toolCall.Params.GetValueOrDefault("path")}",
                "uninstall_software" or "uninstall" => $"Uninstall software: {toolCall.Params.GetValueOrDefault("name")}",
                "run_command" => $"Run command: {toolCall.Params.GetValueOrDefault("command")}",
                "run_powershell" => $"Run PowerShell: {toolCall.Params.GetValueOrDefault("script")?.ToString()?.Substring(0, Math.Min(100, toolCall.Params.GetValueOrDefault("script")?.ToString()?.Length ?? 0))}...",
                _ => $"{toolCall.Tool}: {string.Join(", ", toolCall.Params.Select(p => $"{p.Key}={p.Value}"))}"
            };
        }

        /// <summary>
        /// Run the agent on a task - it will use tools until complete
        /// </summary>
        public async Task<string> RunAsync(string userRequest)
        {
            // Add system prompt with tool definitions
            if (_conversationHistory.Count == 0)
            {
                _conversationHistory.Add(new AgentMessage
                {
                    Role = "system",
                    Content = GetSystemPrompt()
                });
            }

            // Add user request
            _conversationHistory.Add(new AgentMessage { Role = "user", Content = userRequest });

            var iterations = 0;
            string finalResponse = "";

            while (iterations < MaxIterations)
            {
                iterations++;
                OnThinking?.Invoke(this, $"Thinking... (step {iterations})");

                // Call AI with timeout
                var messages = ConvertToApiFormat();
                
                AIResponse response;
                try
                {
                    // Use a task with timeout to prevent hanging
                    var aiTask = AIManager.SendMessageAsync(messages, 4096);
                    var timeoutTask = Task.Delay(TimeSpan.FromSeconds(45));
                    
                    var completedTask = await Task.WhenAny(aiTask, timeoutTask);
                    if (completedTask == timeoutTask)
                    {
                        OnError?.Invoke(this, "AI request timed out after 45 seconds");
                        return "⏱️ AI request timed out. The model may be overloaded. Please try again.";
                    }
                    
                    response = await aiTask;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Agent] AI call exception: {ex.Message}");
                    OnError?.Invoke(this, $"AI error: {ex.Message}");
                    return $"❌ AI error: {ex.Message}";
                }

                if (!response.Success || string.IsNullOrEmpty(response.Content))
                {
                    var error = $"AI error: {response.Error ?? "No response"}";
                    OnError?.Invoke(this, error);
                    return error;
                }

                var aiContent = response.Content;
                Debug.WriteLine($"[Agent] AI response: {aiContent.Substring(0, Math.Min(200, aiContent.Length))}...");

                // Check for tool call
                var toolCall = ExtractToolCall(aiContent);
                
                if (toolCall != null)
                {
                    Debug.WriteLine($"[Agent] Tool call detected: {toolCall.Tool}");
                    
                    // Check if this is a destructive operation that needs confirmation
                    if (DestructiveTools.Contains(toolCall.Tool))
                    {
                        Debug.WriteLine($"[Agent] Destructive tool detected: {toolCall.Tool}, OnConfirmationRequired is {(OnConfirmationRequired != null ? "set" : "null")}");
                        
                        if (OnConfirmationRequired != null)
                        {
                            var description = GetToolDescription(toolCall);
                            Debug.WriteLine($"[Agent] Requesting confirmation for: {description}");
                            var confirmed = await OnConfirmationRequired(toolCall.Tool, description);
                            
                            if (!confirmed)
                            {
                                Debug.WriteLine($"[Agent] User cancelled {toolCall.Tool}");
                                // User cancelled - add to history and continue
                                _conversationHistory.Add(new AgentMessage { Role = "assistant", Content = aiContent });
                                _conversationHistory.Add(new AgentMessage 
                                { 
                                    Role = "user", 
                                    Content = $"⚠️ User cancelled the {toolCall.Tool} operation. Please continue without this action or suggest an alternative." 
                                });
                                continue;
                            }
                            Debug.WriteLine($"[Agent] User confirmed {toolCall.Tool}");
                        }
                    }
                    
                    // Execute the tool
                    OnToolExecuting?.Invoke(this, $"🔧 {toolCall.Tool}");
                    
                    // Capture undo data before execution
                    string? undoData = null;
                    if (toolCall.Tool == "write_file" || toolCall.Tool == "delete_file")
                    {
                        var path = toolCall.Params.GetValueOrDefault("path")?.ToString();
                        if (!string.IsNullOrEmpty(path))
                        {
                            var fullPath = Path.Combine(_workspacePath, path);
                            if (File.Exists(fullPath))
                                undoData = await File.ReadAllTextAsync(fullPath);
                            else if (toolCall.Tool == "write_file")
                                undoData = "__NEW_FILE__"; // Mark as newly created
                        }
                    }
                    
                    var result = await AgentTools.ExecuteToolAsync(toolCall.Tool, toolCall.Params, _workspacePath);
                    OnToolResult?.Invoke(this, result);
                    
                    // Track action in history
                    _actionHistory.Add(new AgentAction
                    {
                        Tool = toolCall.Tool,
                        Params = toolCall.Params,
                        Result = result.Output,
                        Success = result.Success,
                        UndoData = undoData
                    });

                    // Add AI response and tool result to history
                    _conversationHistory.Add(new AgentMessage { Role = "assistant", Content = aiContent });
                    _conversationHistory.Add(new AgentMessage 
                    { 
                        Role = "user", 
                        Content = $"Tool result ({toolCall.Tool}):\n{result.Output}" 
                    });

                    // Continue the loop - AI will process the result
                    continue;
                }
                else
                {
                    // No tool call - this is the final response
                    finalResponse = aiContent;
                    _conversationHistory.Add(new AgentMessage { Role = "assistant", Content = aiContent });
                    OnResponse?.Invoke(this, finalResponse);
                    break;
                }
            }

            if (iterations >= MaxIterations)
            {
                finalResponse = "⚠️ Reached maximum iterations. Task may be incomplete.";
                OnError?.Invoke(this, finalResponse);
            }

            return finalResponse;
        }

        /// <summary>
        /// Extract a tool call from AI response
        /// </summary>
        private ToolCall? ExtractToolCall(string content)
        {
            Debug.WriteLine($"[Agent] ExtractToolCall parsing content length: {content.Length}");
            
            // Look for ```tool ... ``` blocks (most common format)
            var toolBlockMatch = Regex.Match(content, @"```tool\s*\n?({[\s\S]*?})\s*```", RegexOptions.IgnoreCase);
            if (toolBlockMatch.Success)
            {
                Debug.WriteLine($"[Agent] Found ```tool block: {toolBlockMatch.Groups[1].Value}");
                return ParseToolJson(toolBlockMatch.Groups[1].Value);
            }

            // Try ```json blocks that look like tool calls
            var jsonBlockMatch = Regex.Match(content, @"```json\s*\n?({[\s\S]*?""tool""[\s\S]*?})\s*```", RegexOptions.IgnoreCase);
            if (jsonBlockMatch.Success)
            {
                Debug.WriteLine($"[Agent] Found ```json block with tool: {jsonBlockMatch.Groups[1].Value}");
                return ParseToolJson(jsonBlockMatch.Groups[1].Value);
            }
            
            // Try ``` blocks without language specifier
            var plainBlockMatch = Regex.Match(content, @"```\s*\n?({[\s\S]*?""tool""[\s\S]*?})\s*```", RegexOptions.IgnoreCase);
            if (plainBlockMatch.Success)
            {
                Debug.WriteLine($"[Agent] Found plain ``` block with tool: {plainBlockMatch.Groups[1].Value}");
                return ParseToolJson(plainBlockMatch.Groups[1].Value);
            }

            // Try inline JSON with "tool" key (nested params)
            var nestedMatch = Regex.Match(content, @"\{\s*""tool""\s*:\s*""([^""]+)""\s*,\s*""params""\s*:\s*(\{[^}]+\})\s*\}", RegexOptions.IgnoreCase);
            if (nestedMatch.Success)
            {
                Debug.WriteLine($"[Agent] Found nested JSON tool call");
                return ParseToolJson(nestedMatch.Value);
            }
            
            // Try flat JSON with "tool" key
            var flatMatch = Regex.Match(content, @"\{\s*""tool""\s*:\s*""[^""]+""[^}]*\}", RegexOptions.IgnoreCase);
            if (flatMatch.Success)
            {
                Debug.WriteLine($"[Agent] Found flat JSON tool call: {flatMatch.Value}");
                return ParseToolJson(flatMatch.Value);
            }
            
            // FALLBACK: Try to infer tool call from natural language response
            var inferredTool = InferToolFromText(content);
            if (inferredTool != null)
            {
                Debug.WriteLine($"[Agent] Inferred tool call from text: {inferredTool.Tool}");
                return inferredTool;
            }
            
            // Check if AI is trying to use a tool but in wrong format
            if (content.Contains("\"tool\"") || content.Contains("'tool'"))
            {
                Debug.WriteLine($"[Agent] WARNING: Content contains 'tool' but no valid JSON found. Content preview: {content.Substring(0, Math.Min(500, content.Length))}");
            }

            Debug.WriteLine($"[Agent] No tool call found in response");
            return null;
        }
        
        /// <summary>
        /// Try to infer a tool call from natural language when AI doesn't use proper format
        /// </summary>
        private ToolCall? InferToolFromText(string content)
        {
            var lower = content.ToLowerInvariant();
            
            // Check for file creation patterns
            var createFileMatch = Regex.Match(content, @"(?:create|write|save).*?(?:file|script).*?[`'""]([^`'""]+\.\w+)[`'""]", RegexOptions.IgnoreCase);
            if (createFileMatch.Success)
            {
                var filename = createFileMatch.Groups[1].Value;
                // Try to extract code content
                var codeMatch = Regex.Match(content, @"```(?:\w+)?\s*\n?([\s\S]*?)```");
                var codeContent = codeMatch.Success ? codeMatch.Groups[1].Value.Trim() : "# TODO: Add content";
                
                return new ToolCall
                {
                    Tool = "write_file",
                    Params = new Dictionary<string, object>
                    {
                        { "path", filename },
                        { "content", codeContent }
                    }
                };
            }
            
            // Check for list directory patterns
            if (lower.Contains("list") && (lower.Contains("file") || lower.Contains("director") || lower.Contains("folder")))
            {
                return new ToolCall
                {
                    Tool = "list_directory",
                    Params = new Dictionary<string, object>
                    {
                        { "path", "." },
                        { "recursive", false }
                    }
                };
            }
            
            // Check for install patterns
            var installMatch = Regex.Match(content, @"install\s+(\w+)", RegexOptions.IgnoreCase);
            if (installMatch.Success)
            {
                return new ToolCall
                {
                    Tool = "install_software",
                    Params = new Dictionary<string, object>
                    {
                        { "name", installMatch.Groups[1].Value }
                    }
                };
            }
            
            return null;
        }

        private ToolCall? ParseToolJson(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("tool", out var toolProp))
                    return null;

                var toolCall = new ToolCall { Tool = toolProp.GetString() ?? "" };

                if (root.TryGetProperty("params", out var paramsProp))
                {
                    foreach (var prop in paramsProp.EnumerateObject())
                    {
                        toolCall.Params[prop.Name] = prop.Value.ValueKind switch
                        {
                            JsonValueKind.String => prop.Value.GetString() ?? "",
                            JsonValueKind.Number => prop.Value.GetDouble(),
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            _ => prop.Value.ToString()
                        };
                    }
                }
                // Also check for flat structure (tool + other params at same level)
                else
                {
                    foreach (var prop in root.EnumerateObject())
                    {
                        if (prop.Name == "tool") continue;
                        toolCall.Params[prop.Name] = prop.Value.ValueKind switch
                        {
                            JsonValueKind.String => prop.Value.GetString() ?? "",
                            JsonValueKind.Number => prop.Value.GetDouble(),
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            _ => prop.Value.ToString()
                        };
                    }
                }

                return toolCall;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Agent] Failed to parse tool JSON: {ex.Message}");
                return null;
            }
        }

        private List<object> ConvertToApiFormat()
        {
            var messages = new List<object>();
            foreach (var msg in _conversationHistory)
            {
                messages.Add(new { role = msg.Role, content = msg.Content });
            }
            return messages;
        }

        private string GetSystemPrompt()
        {
            return $@"You are Atlas, an AI agent. You execute tasks using tools.

WORKSPACE: {_workspacePath}

TOOLS AVAILABLE:
- write_file: Create files. Params: path, content
- read_file: Read files. Params: path
- delete_file: Delete files. Params: path
- list_directory: List folder. Params: path, recursive
- search_files: Find files. Params: pattern, path
- install_software: Install apps. Params: name
- run_command: Run shell commands. Params: command

HOW TO USE TOOLS:
Respond with ONLY this format:
```tool
{{""tool"": ""write_file"", ""params"": {{""path"": ""test.py"", ""content"": ""print('hi')""}}}}
```

EXAMPLES:
User: create hello.py
```tool
{{""tool"": ""write_file"", ""params"": {{""path"": ""hello.py"", ""content"": ""print('Hello!')""}}}}
```

User: list files
```tool
{{""tool"": ""list_directory"", ""params"": {{""path"": ""."", ""recursive"": false}}}}
```

User: find divinity folders
```tool
{{""tool"": ""search_files"", ""params"": {{""pattern"": ""divinity"", ""path"": ""C:\\Users\\{Environment.UserName}""}}}}
```

RULES:
1. Always respond with a tool call when asked to do something
2. Use search_files to find files/folders
3. Desktop: C:\\Users\\{Environment.UserName}\\Desktop
4. Documents: C:\\Users\\{Environment.UserName}\\Documents";
        }

        public void ClearHistory()
        {
            _conversationHistory.Clear();
        }
    }

    public class AgentMessage
    {
        public string Role { get; set; } = "";
        public string Content { get; set; } = "";
    }
}
