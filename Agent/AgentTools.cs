using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MinimalApp.Tools;

namespace MinimalApp.Agent
{
    /// <summary>
    /// Tool definitions that the AI can call - similar to how Kiro works
    /// </summary>
    public static class AgentTools
    {
        // Tool definitions for the AI system prompt
        public static string GetToolDefinitions() => @"
You have access to the following tools to help complete tasks. Use them by responding with a JSON tool call.

## FILE TOOLS:

### read_file
Read the contents of a file.
Parameters: { ""path"": ""relative/path/to/file"" }

### write_file  
Create or overwrite a file with content.
Parameters: { ""path"": ""relative/path/to/file"", ""content"": ""file contents"" }

### append_file
Append content to an existing file.
Parameters: { ""path"": ""relative/path/to/file"", ""content"": ""content to append"" }

### list_directory
List files and folders in a directory.
Parameters: { ""path"": ""relative/path/to/dir"", ""recursive"": false }

### search_files
Search for files matching a pattern.
Parameters: { ""pattern"": ""*.cs"", ""path"": ""."" }

### search_content
Search for text content in files (like grep).
Parameters: { ""query"": ""search term"", ""path"": ""."", ""file_pattern"": ""*.cs"" }

### create_directory
Create a new directory.
Parameters: { ""path"": ""relative/path/to/new/dir"" }

### delete_file
Delete a file.
Parameters: { ""path"": ""relative/path/to/file"" }

### move_file
Move or rename a file.
Parameters: { ""source"": ""old/path"", ""destination"": ""new/path"" }

### get_file_info
Get metadata about a file (size, modified date, etc).
Parameters: { ""path"": ""relative/path/to/file"" }

## SOFTWARE INSTALLATION TOOLS:

### install_software
Install any software using winget, pip, npm, or choco. Just say what you want!
Parameters: { ""name"": ""python"" } or { ""name"": ""discord"" } or { ""name"": ""numpy"" }
Examples: python, node, git, vscode, discord, spotify, chrome, numpy, pandas, typescript

### uninstall_software
Uninstall software.
Parameters: { ""name"": ""software name"" }

### check_installed
Check if software is installed.
Parameters: { ""name"": ""python"" }

## CODE TOOLS:

### generate_code
Generate code based on a description.
Parameters: { ""request"": ""create a function that..., ""language"": ""python"" }

### modify_code
Modify existing code with instructions.
Parameters: { ""code"": ""existing code"", ""instructions"": ""add error handling"", ""language"": ""python"" }

### fix_code
Fix errors in code.
Parameters: { ""code"": ""broken code"", ""error"": ""error message (optional)"", ""language"": ""python"" }

### explain_code
Explain what code does.
Parameters: { ""code"": ""code to explain"", ""language"": ""python"" }

### create_code_file
Generate and save a code file.
Parameters: { ""path"": ""src/utils.py"", ""description"": ""utility functions for string manipulation"" }

### refactor_code
Improve code quality.
Parameters: { ""code"": ""code to refactor"", ""language"": ""python"" }

### generate_tests
Generate unit tests for code.
Parameters: { ""code"": ""code to test"", ""language"": ""python"", ""framework"": ""pytest"" }

## COMMAND TOOLS:

### run_command
Execute a shell command.
Parameters: { ""command"": ""dotnet build"", ""working_dir"": ""."" }

### run_powershell
Execute a PowerShell script.
Parameters: { ""script"": ""Get-Process | Select-Object -First 5"" }

## How to use tools:

When you need to use a tool, respond with ONLY a JSON block like this:
```tool
{""tool"": ""install_software"", ""params"": {""name"": ""python""}}
```

After I execute the tool, I'll give you the result and you can continue.

## Important Rules:
1. Use tools to actually make changes - don't just describe what to do
2. For installations, just use install_software with the name
3. Read files before modifying them to understand context
4. After writing files, verify your changes worked
5. For multi-step tasks, execute one tool at a time
6. Always use relative paths from the workspace root
";

        // Execute a tool call and return the result
        public static async Task<ToolResult> ExecuteToolAsync(string toolName, Dictionary<string, object> parameters, string workspacePath)
        {
            try
            {
                return toolName.ToLower() switch
                {
                    // File tools
                    "read_file" => await ReadFileAsync(parameters, workspacePath),
                    "write_file" => await WriteFileAsync(parameters, workspacePath),
                    "append_file" => await AppendFileAsync(parameters, workspacePath),
                    "list_directory" => await ListDirectoryAsync(parameters, workspacePath),
                    "search_files" => await SearchFilesAsync(parameters, workspacePath),
                    "search_content" => await SearchContentAsync(parameters, workspacePath),
                    "create_directory" => await CreateDirectoryAsync(parameters, workspacePath),
                    "delete_file" => await DeleteFileAsync(parameters, workspacePath),
                    "move_file" => await MoveFileAsync(parameters, workspacePath),
                    "get_file_info" => await GetFileInfoAsync(parameters, workspacePath),
                    
                    // Software installation tools
                    "install_software" or "install" => await InstallSoftwareAsync(parameters),
                    "uninstall_software" or "uninstall" => await UninstallSoftwareAsync(parameters),
                    "check_installed" => await CheckInstalledAsync(parameters),
                    
                    // Code tools
                    "generate_code" => await GenerateCodeAsync(parameters),
                    "modify_code" => await ModifyCodeAsync(parameters),
                    "fix_code" => await FixCodeAsync(parameters),
                    "explain_code" => await ExplainCodeAsync(parameters),
                    "create_code_file" => await CreateCodeFileAsync(parameters, workspacePath),
                    "refactor_code" => await RefactorCodeAsync(parameters),
                    "generate_tests" => await GenerateTestsAsync(parameters),
                    
                    // Command tools
                    "run_command" => await RunCommandAsync(parameters, workspacePath),
                    "run_powershell" => await RunPowerShellAsync(parameters),
                    
                    _ => new ToolResult { Success = false, Output = $"Unknown tool: {toolName}" }
                };
            }
            catch (Exception ex)
            {
                return new ToolResult { Success = false, Output = $"Tool error: {ex.Message}" };
            }
        }
        
        // ==================== SOFTWARE INSTALLATION ====================
        
        private static async Task<ToolResult> InstallSoftwareAsync(Dictionary<string, object> p)
        {
            var name = GetParam(p, "name");
            if (string.IsNullOrEmpty(name))
                return new ToolResult { Success = false, Output = "Software name is required" };
            
            var result = await SoftwareInstaller.InstallAsync(name);
            return new ToolResult { Success = !result.StartsWith("❌"), Output = result };
        }
        
        private static async Task<ToolResult> UninstallSoftwareAsync(Dictionary<string, object> p)
        {
            var name = GetParam(p, "name");
            if (string.IsNullOrEmpty(name))
                return new ToolResult { Success = false, Output = "Software name is required" };
            
            var result = await SoftwareInstaller.UninstallAsync(name);
            return new ToolResult { Success = !result.StartsWith("❌"), Output = result };
        }
        
        private static async Task<ToolResult> CheckInstalledAsync(Dictionary<string, object> p)
        {
            var name = GetParam(p, "name");
            if (string.IsNullOrEmpty(name))
                return new ToolResult { Success = false, Output = "Software name is required" };
            
            var isInstalled = await SoftwareInstaller.IsInstalledAsync(name);
            return new ToolResult 
            { 
                Success = true, 
                Output = isInstalled ? $"✅ {name} is installed" : $"❌ {name} is NOT installed" 
            };
        }
        
        // ==================== CODE TOOLS ====================
        
        private static async Task<ToolResult> GenerateCodeAsync(Dictionary<string, object> p)
        {
            var request = GetParam(p, "request");
            var language = GetParam(p, "language");
            
            if (string.IsNullOrEmpty(request))
                return new ToolResult { Success = false, Output = "Request description is required" };
            
            var result = await CodeAssistant.GenerateCodeAsync(request, language);
            
            if (!result.Success)
                return new ToolResult { Success = false, Output = result.Error ?? "Failed to generate code" };
            
            var output = new StringBuilder();
            if (!string.IsNullOrEmpty(result.Explanation))
                output.AppendLine(result.Explanation).AppendLine();
            output.AppendLine($"```{result.Language ?? ""}");
            output.AppendLine(result.Code);
            output.AppendLine("```");
            
            return new ToolResult { Success = true, Output = output.ToString() };
        }
        
        private static async Task<ToolResult> ModifyCodeAsync(Dictionary<string, object> p)
        {
            var code = GetParam(p, "code");
            var instructions = GetParam(p, "instructions");
            var language = GetParam(p, "language");
            
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(instructions))
                return new ToolResult { Success = false, Output = "Code and instructions are required" };
            
            var result = await CodeAssistant.ModifyCodeAsync(code, instructions, language);
            
            if (!result.Success)
                return new ToolResult { Success = false, Output = result.Error ?? "Failed to modify code" };
            
            return new ToolResult { Success = true, Output = $"```{result.Language ?? ""}\n{result.Code}\n```" };
        }
        
        private static async Task<ToolResult> FixCodeAsync(Dictionary<string, object> p)
        {
            var code = GetParam(p, "code");
            var error = GetParam(p, "error");
            var language = GetParam(p, "language");
            
            if (string.IsNullOrEmpty(code))
                return new ToolResult { Success = false, Output = "Code is required" };
            
            var result = await CodeAssistant.FixCodeAsync(code, error, language);
            
            if (!result.Success)
                return new ToolResult { Success = false, Output = result.Error ?? "Failed to fix code" };
            
            return new ToolResult { Success = true, Output = $"```{result.Language ?? ""}\n{result.Code}\n```" };
        }
        
        private static async Task<ToolResult> ExplainCodeAsync(Dictionary<string, object> p)
        {
            var code = GetParam(p, "code");
            var language = GetParam(p, "language");
            
            if (string.IsNullOrEmpty(code))
                return new ToolResult { Success = false, Output = "Code is required" };
            
            var explanation = await CodeAssistant.ExplainCodeAsync(code, language);
            return new ToolResult { Success = true, Output = explanation };
        }
        
        private static async Task<ToolResult> CreateCodeFileAsync(Dictionary<string, object> p, string workspace)
        {
            var path = GetParam(p, "path");
            var description = GetParam(p, "description");
            
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(description))
                return new ToolResult { Success = false, Output = "Path and description are required" };
            
            var result = await CodeAssistant.CreateFileAsync(path, description, workspace);
            return new ToolResult { Success = !result.StartsWith("❌"), Output = result };
        }
        
        private static async Task<ToolResult> RefactorCodeAsync(Dictionary<string, object> p)
        {
            var code = GetParam(p, "code");
            var language = GetParam(p, "language");
            
            if (string.IsNullOrEmpty(code))
                return new ToolResult { Success = false, Output = "Code is required" };
            
            var result = await CodeAssistant.RefactorCodeAsync(code, language);
            
            if (!result.Success)
                return new ToolResult { Success = false, Output = result.Error ?? "Failed to refactor code" };
            
            return new ToolResult { Success = true, Output = $"```{result.Language ?? ""}\n{result.Code}\n```" };
        }
        
        private static async Task<ToolResult> GenerateTestsAsync(Dictionary<string, object> p)
        {
            var code = GetParam(p, "code");
            var language = GetParam(p, "language");
            var framework = GetParam(p, "framework");
            
            if (string.IsNullOrEmpty(code))
                return new ToolResult { Success = false, Output = "Code is required" };
            
            var result = await CodeAssistant.GenerateTestsAsync(code, language, framework);
            
            if (!result.Success)
                return new ToolResult { Success = false, Output = result.Error ?? "Failed to generate tests" };
            
            return new ToolResult { Success = true, Output = $"```{result.Language ?? ""}\n{result.Code}\n```" };
        }
        
        private static async Task<ToolResult> RunPowerShellAsync(Dictionary<string, object> p)
        {
            var script = GetParam(p, "script");
            if (string.IsNullOrEmpty(script))
                return new ToolResult { Success = false, Output = "Script is required" };
            
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script.Replace("\"", "\\\"")}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            var output = new StringBuilder();
            var error = new StringBuilder();

            process.OutputDataReceived += (s, e) => { if (e.Data != null) output.AppendLine(e.Data); };
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) error.AppendLine(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var completed = await Task.Run(() => process.WaitForExit(60000));
            if (!completed)
            {
                process.Kill();
                return new ToolResult { Success = false, Output = "⚠️ Script timed out after 60s" };
            }

            var result = output.ToString();
            if (error.Length > 0)
                result += "\n--- Errors ---\n" + error.ToString();

            return new ToolResult 
            { 
                Success = process.ExitCode == 0, 
                Output = string.IsNullOrWhiteSpace(result) ? "(no output)" : result 
            };
        }

        private static string GetParam(Dictionary<string, object> p, string key, string defaultVal = "")
        {
            if (p.TryGetValue(key, out var val))
                return val?.ToString() ?? defaultVal;
            return defaultVal;
        }

        private static bool GetBoolParam(Dictionary<string, object> p, string key, bool defaultVal = false)
        {
            if (p.TryGetValue(key, out var val))
            {
                if (val is bool b) return b;
                if (val is JsonElement je && je.ValueKind == JsonValueKind.True) return true;
                if (val is JsonElement je2 && je2.ValueKind == JsonValueKind.False) return false;
                return bool.TryParse(val?.ToString(), out var result) && result;
            }
            return defaultVal;
        }

        private static string ResolvePath(string relativePath, string workspacePath)
        {
            // Security: prevent path traversal
            var normalized = relativePath.Replace('/', '\\').TrimStart('\\');
            if (normalized.Contains(".."))
                throw new SecurityException("Path traversal not allowed");
            
            return Path.Combine(workspacePath, normalized);
        }

        private static async Task<ToolResult> ReadFileAsync(Dictionary<string, object> p, string workspace)
        {
            var path = ResolvePath(GetParam(p, "path"), workspace);
            if (!File.Exists(path))
                return new ToolResult { Success = false, Output = $"File not found: {GetParam(p, "path")}" };

            var content = await File.ReadAllTextAsync(path);
            // Truncate very large files
            if (content.Length > 50000)
                content = content.Substring(0, 50000) + "\n\n[... truncated, file too large ...]";
            
            return new ToolResult { Success = true, Output = content };
        }

        private static async Task<ToolResult> WriteFileAsync(Dictionary<string, object> p, string workspace)
        {
            var relativePath = GetParam(p, "path");
            var path = ResolvePath(relativePath, workspace);
            var content = GetParam(p, "content");
            
            // Track for undo
            var safety = AgentSafetyManager.Instance;
            string? originalContent = null;
            bool isNewFile = !File.Exists(path);
            
            if (!isNewFile)
            {
                try { originalContent = await File.ReadAllTextAsync(path); }
                catch { }
            }
            
            // Create directory if needed
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            await File.WriteAllTextAsync(path, content);
            
            // Push undo action
            safety.PushUndo(new AgentUndoAction
            {
                Type = isNewFile ? UndoType.FileCreated : UndoType.FileModified,
                Description = isNewFile ? $"Created {relativePath}" : $"Modified {relativePath}",
                TargetPath = path,
                OriginalContent = originalContent
            });
            
            return new ToolResult { Success = true, Output = $"✅ Written {content.Length} chars to {relativePath}" };
        }

        private static async Task<ToolResult> AppendFileAsync(Dictionary<string, object> p, string workspace)
        {
            var path = ResolvePath(GetParam(p, "path"), workspace);
            var content = GetParam(p, "content");
            
            await File.AppendAllTextAsync(path, content);
            return new ToolResult { Success = true, Output = $"✅ Appended {content.Length} chars to {GetParam(p, "path")}" };
        }

        private static Task<ToolResult> ListDirectoryAsync(Dictionary<string, object> p, string workspace)
        {
            var path = ResolvePath(GetParam(p, "path", "."), workspace);
            var recursive = GetBoolParam(p, "recursive", false);

            if (!Directory.Exists(path))
                return Task.FromResult(new ToolResult { Success = false, Output = $"Directory not found: {GetParam(p, "path")}" });

            var sb = new StringBuilder();
            var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            
            foreach (var dir in Directory.GetDirectories(path, "*", option).Take(100))
            {
                var rel = Path.GetRelativePath(workspace, dir);
                sb.AppendLine($"📁 {rel}/");
            }
            foreach (var file in Directory.GetFiles(path, "*", option).Take(200))
            {
                var rel = Path.GetRelativePath(workspace, file);
                var info = new FileInfo(file);
                sb.AppendLine($"📄 {rel} ({FormatSize(info.Length)})");
            }

            return Task.FromResult(new ToolResult { Success = true, Output = sb.ToString() });
        }

        private static Task<ToolResult> SearchFilesAsync(Dictionary<string, object> p, string workspace)
        {
            var pattern = GetParam(p, "pattern", "*");
            var path = ResolvePath(GetParam(p, "path", "."), workspace);

            if (!Directory.Exists(path))
                return Task.FromResult(new ToolResult { Success = false, Output = "Directory not found" });

            var files = Directory.GetFiles(path, pattern, SearchOption.AllDirectories)
                .Take(100)
                .Select(f => Path.GetRelativePath(workspace, f));

            return Task.FromResult(new ToolResult { Success = true, Output = string.Join("\n", files) });
        }

        private static async Task<ToolResult> SearchContentAsync(Dictionary<string, object> p, string workspace)
        {
            var query = GetParam(p, "query");
            var path = ResolvePath(GetParam(p, "path", "."), workspace);
            var filePattern = GetParam(p, "file_pattern", "*");

            if (string.IsNullOrEmpty(query))
                return new ToolResult { Success = false, Output = "Query is required" };

            var results = new StringBuilder();
            var matchCount = 0;

            foreach (var file in Directory.GetFiles(path, filePattern, SearchOption.AllDirectories).Take(500))
            {
                try
                {
                    var lines = await File.ReadAllLinesAsync(file);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (lines[i].Contains(query, StringComparison.OrdinalIgnoreCase))
                        {
                            var rel = Path.GetRelativePath(workspace, file);
                            results.AppendLine($"{rel}:{i + 1}: {lines[i].Trim()}");
                            matchCount++;
                            if (matchCount >= 50) break;
                        }
                    }
                }
                catch { }
                if (matchCount >= 50) break;
            }

            return new ToolResult 
            { 
                Success = true, 
                Output = matchCount > 0 ? results.ToString() : "No matches found" 
            };
        }

        private static async Task<ToolResult> RunCommandAsync(Dictionary<string, object> p, string workspace)
        {
            var command = GetParam(p, "command");
            var workingDir = ResolvePath(GetParam(p, "working_dir", "."), workspace);

            // Security: block dangerous commands
            var dangerous = new[] { "rm -rf /", "format", "del /s /q c:", "shutdown", "restart" };
            if (dangerous.Any(d => command.ToLower().Contains(d)))
                return new ToolResult { Success = false, Output = "⚠️ Command blocked for safety" };

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {command}",
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            var output = new StringBuilder();
            var error = new StringBuilder();

            process.OutputDataReceived += (s, e) => { if (e.Data != null) output.AppendLine(e.Data); };
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) error.AppendLine(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Timeout after 60 seconds
            var completed = await Task.Run(() => process.WaitForExit(60000));
            if (!completed)
            {
                process.Kill();
                return new ToolResult { Success = false, Output = "⚠️ Command timed out after 60s" };
            }

            var result = output.ToString();
            if (error.Length > 0)
                result += "\n--- Errors ---\n" + error.ToString();

            return new ToolResult 
            { 
                Success = process.ExitCode == 0, 
                Output = string.IsNullOrWhiteSpace(result) ? "(no output)" : result 
            };
        }

        private static Task<ToolResult> CreateDirectoryAsync(Dictionary<string, object> p, string workspace)
        {
            var relativePath = GetParam(p, "path");
            var path = ResolvePath(relativePath, workspace);
            
            bool alreadyExists = Directory.Exists(path);
            Directory.CreateDirectory(path);
            
            // Track for undo (only if newly created)
            if (!alreadyExists)
            {
                AgentSafetyManager.Instance.PushUndo(new AgentUndoAction
                {
                    Type = UndoType.DirectoryCreated,
                    Description = $"Created directory {relativePath}",
                    TargetPath = path
                });
            }
            
            return Task.FromResult(new ToolResult { Success = true, Output = $"✅ Created directory: {relativePath}" });
        }

        private static async Task<ToolResult> DeleteFileAsync(Dictionary<string, object> p, string workspace)
        {
            var relativePath = GetParam(p, "path");
            var path = ResolvePath(relativePath, workspace);
            
            if (!File.Exists(path))
                return new ToolResult { Success = false, Output = $"File not found: {relativePath}" };
            
            // Backup content for undo
            var safety = AgentSafetyManager.Instance;
            string? originalContent = null;
            try { originalContent = await File.ReadAllTextAsync(path); }
            catch { }
            
            // Delete the file
            File.Delete(path);
            
            // Push undo action
            safety.PushUndo(new AgentUndoAction
            {
                Type = UndoType.FileDeleted,
                Description = $"Deleted {relativePath}",
                TargetPath = path,
                OriginalContent = originalContent
            });
            
            return new ToolResult { Success = true, Output = $"✅ Deleted {relativePath}" };
        }

        private static Task<ToolResult> MoveFileAsync(Dictionary<string, object> p, string workspace)
        {
            var source = ResolvePath(GetParam(p, "source"), workspace);
            var dest = ResolvePath(GetParam(p, "destination"), workspace);
            
            if (!File.Exists(source))
                return Task.FromResult(new ToolResult { Success = false, Output = "Source file not found" });

            var destDir = Path.GetDirectoryName(dest);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);

            File.Move(source, dest, overwrite: true);
            return Task.FromResult(new ToolResult { Success = true, Output = $"✅ Moved to: {GetParam(p, "destination")}" });
        }

        private static Task<ToolResult> GetFileInfoAsync(Dictionary<string, object> p, string workspace)
        {
            var path = ResolvePath(GetParam(p, "path"), workspace);
            if (!File.Exists(path))
                return Task.FromResult(new ToolResult { Success = false, Output = "File not found" });

            var info = new FileInfo(path);
            var output = $@"Path: {GetParam(p, "path")}
Size: {FormatSize(info.Length)}
Created: {info.CreationTime}
Modified: {info.LastWriteTime}
Extension: {info.Extension}";

            return Task.FromResult(new ToolResult { Success = true, Output = output });
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
        }
    }

    public class ToolResult
    {
        public bool Success { get; set; }
        public string Output { get; set; } = "";
    }

    public class ToolCall
    {
        public string Tool { get; set; } = "";
        public Dictionary<string, object> Params { get; set; } = new();
    }
}
