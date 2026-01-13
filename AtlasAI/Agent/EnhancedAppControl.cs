using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace AtlasAI.Agent
{
    /// <summary>
    /// Enhanced app control - smarter process management with fuzzy matching.
    /// </summary>
    public static class EnhancedAppControl
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        
        private const int SW_RESTORE = 9;
        private const int SW_MINIMIZE = 6;
        
        // Extended app aliases with paths
        private static readonly Dictionary<string, AppInfo> AppDatabase = new(StringComparer.OrdinalIgnoreCase)
        {
            // Browsers
            { "chrome", new AppInfo("chrome", "Google Chrome", new[] { "chrome" }) },
            { "firefox", new AppInfo("firefox", "Mozilla Firefox", new[] { "firefox" }) },
            { "edge", new AppInfo("msedge", "Microsoft Edge", new[] { "msedge" }) },
            { "brave", new AppInfo("brave", "Brave Browser", new[] { "brave" }) },
            { "opera", new AppInfo("opera", "Opera", new[] { "opera" }) },
            
            // Communication
            { "discord", new AppInfo("discord", "Discord", new[] { "Discord" }) },
            { "slack", new AppInfo("slack", "Slack", new[] { "slack" }) },
            { "teams", new AppInfo("ms-teams:", "Microsoft Teams", new[] { "Teams", "ms-teams" }, true) },
            { "zoom", new AppInfo("zoom", "Zoom", new[] { "Zoom" }) },
            { "skype", new AppInfo("skype:", "Skype", new[] { "Skype" }, true) },
            { "telegram", new AppInfo("telegram", "Telegram", new[] { "Telegram" }) },
            { "whatsapp", new AppInfo("whatsapp:", "WhatsApp", new[] { "WhatsApp" }, true) },
            
            // Media
            { "spotify", new AppInfo("spotify", "Spotify", new[] { "Spotify" }) },
            { "vlc", new AppInfo("vlc", "VLC Media Player", new[] { "vlc" }) },
            { "itunes", new AppInfo("itunes", "iTunes", new[] { "iTunes" }) },
            
            // Dev tools
            { "vscode", new AppInfo("code", "Visual Studio Code", new[] { "Code" }) },
            { "code", new AppInfo("code", "Visual Studio Code", new[] { "Code" }) },
            { "vs", new AppInfo("devenv", "Visual Studio", new[] { "devenv" }) },
            { "visual studio", new AppInfo("devenv", "Visual Studio", new[] { "devenv" }) },
            { "rider", new AppInfo("rider64", "JetBrains Rider", new[] { "rider64" }) },
            { "pycharm", new AppInfo("pycharm64", "PyCharm", new[] { "pycharm64" }) },
            { "intellij", new AppInfo("idea64", "IntelliJ IDEA", new[] { "idea64" }) },
            { "sublime", new AppInfo("sublime_text", "Sublime Text", new[] { "sublime_text" }) },
            { "notepad++", new AppInfo("notepad++", "Notepad++", new[] { "notepad++" }) },
            
            // Terminal
            { "terminal", new AppInfo("wt", "Windows Terminal", new[] { "WindowsTerminal" }) },
            { "cmd", new AppInfo("cmd", "Command Prompt", new[] { "cmd" }) },
            { "powershell", new AppInfo("powershell", "PowerShell", new[] { "powershell" }) },
            { "git bash", new AppInfo("git-bash", "Git Bash", new[] { "bash", "mintty" }) },
            
            // Office
            { "word", new AppInfo("winword", "Microsoft Word", new[] { "WINWORD" }) },
            { "excel", new AppInfo("excel", "Microsoft Excel", new[] { "EXCEL" }) },
            { "powerpoint", new AppInfo("powerpnt", "PowerPoint", new[] { "POWERPNT" }) },
            { "outlook", new AppInfo("outlook", "Microsoft Outlook", new[] { "OUTLOOK" }) },
            { "onenote", new AppInfo("onenote", "OneNote", new[] { "ONENOTE" }) },
            
            // System
            { "explorer", new AppInfo("explorer", "File Explorer", new[] { "explorer" }) },
            { "files", new AppInfo("explorer", "File Explorer", new[] { "explorer" }) },
            { "settings", new AppInfo("ms-settings:", "Settings", new[] { "SystemSettings" }, true) },
            { "control panel", new AppInfo("control", "Control Panel", new[] { "control" }) },
            { "task manager", new AppInfo("taskmgr", "Task Manager", new[] { "Taskmgr" }) },
            { "device manager", new AppInfo("devmgmt.msc", "Device Manager", new[] { "mmc" }) },
            
            // Utils
            { "calculator", new AppInfo("calc", "Calculator", new[] { "Calculator", "calc" }) },
            { "calc", new AppInfo("calc", "Calculator", new[] { "Calculator", "calc" }) },
            { "notepad", new AppInfo("notepad", "Notepad", new[] { "notepad" }) },
            { "paint", new AppInfo("mspaint", "Paint", new[] { "mspaint" }) },
            { "snipping tool", new AppInfo("snippingtool", "Snipping Tool", new[] { "SnippingTool" }) },
            { "snip", new AppInfo("snippingtool", "Snipping Tool", new[] { "SnippingTool" }) },
            
            // Games
            { "steam", new AppInfo("steam", "Steam", new[] { "steam", "steamwebhelper" }) },
            { "epic", new AppInfo("EpicGamesLauncher", "Epic Games", new[] { "EpicGamesLauncher" }) },
            { "battle.net", new AppInfo("Battle.net", "Battle.net", new[] { "Battle.net" }) },
            { "origin", new AppInfo("Origin", "EA Origin", new[] { "Origin" }) },
            
            // Creative
            { "photoshop", new AppInfo("Photoshop", "Adobe Photoshop", new[] { "Photoshop" }) },
            { "premiere", new AppInfo("Adobe Premiere Pro", "Premiere Pro", new[] { "Adobe Premiere Pro" }) },
            { "after effects", new AppInfo("AfterFX", "After Effects", new[] { "AfterFX" }) },
            { "blender", new AppInfo("blender", "Blender", new[] { "blender" }) },
            { "obs", new AppInfo("obs64", "OBS Studio", new[] { "obs64" }) },
        };
        
        /// <summary>
        /// Open an app with fuzzy matching
        /// </summary>
        public static async Task<string> OpenAppAsync(string query)
        {
            var lower = query.ToLowerInvariant().Trim();
            
            // Direct match
            if (AppDatabase.TryGetValue(lower, out var app))
            {
                return await LaunchAppAsync(app);
            }
            
            // Fuzzy match
            var bestMatch = AppDatabase
                .Where(kv => kv.Key.Contains(lower) || lower.Contains(kv.Key) || 
                             kv.Value.DisplayName.ToLower().Contains(lower))
                .OrderBy(kv => LevenshteinDistance(kv.Key, lower))
                .FirstOrDefault();
            
            if (bestMatch.Value != null)
            {
                return await LaunchAppAsync(bestMatch.Value);
            }
            
            // Try direct launch
            try
            {
                Process.Start(new ProcessStartInfo(query) { UseShellExecute = true });
                return $"✓ Opened {query}";
            }
            catch
            {
                return $"❌ Couldn't find or open '{query}'";
            }
        }
        
        /// <summary>
        /// Close/kill an app with fuzzy matching
        /// </summary>
        public static async Task<string> CloseAppAsync(string query)
        {
            var lower = query.ToLowerInvariant().Trim();
            
            // Get process names to kill
            string[] processNames;
            
            if (AppDatabase.TryGetValue(lower, out var app))
            {
                processNames = app.ProcessNames;
            }
            else
            {
                // Fuzzy match
                var bestMatch = AppDatabase
                    .Where(kv => kv.Key.Contains(lower) || lower.Contains(kv.Key))
                    .FirstOrDefault();
                
                if (bestMatch.Value != null)
                {
                    processNames = bestMatch.Value.ProcessNames;
                }
                else
                {
                    processNames = new[] { query };
                }
            }
            
            int killed = 0;
            foreach (var name in processNames)
            {
                try
                {
                    var procs = Process.GetProcessesByName(name.Replace(".exe", ""));
                    foreach (var proc in procs)
                    {
                        try
                        {
                            proc.Kill();
                            killed++;
                        }
                        catch { }
                    }
                }
                catch { }
            }
            
            return killed > 0 ? $"✓ Closed {query}" : $"❌ {query} wasn't running";
        }
        
        /// <summary>
        /// Focus an already running app
        /// </summary>
        public static async Task<string> FocusAppAsync(string query)
        {
            var lower = query.ToLowerInvariant().Trim();
            string[] processNames;
            
            if (AppDatabase.TryGetValue(lower, out var app))
            {
                processNames = app.ProcessNames;
            }
            else
            {
                processNames = new[] { query };
            }
            
            foreach (var name in processNames)
            {
                try
                {
                    var procs = Process.GetProcessesByName(name.Replace(".exe", ""));
                    var proc = procs.FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero);
                    
                    if (proc != null)
                    {
                        ShowWindow(proc.MainWindowHandle, SW_RESTORE);
                        SetForegroundWindow(proc.MainWindowHandle);
                        return $"✓ Focused {query}";
                    }
                }
                catch { }
            }
            
            return $"❌ {query} isn't running";
        }
        
        /// <summary>
        /// Minimize an app
        /// </summary>
        public static async Task<string> MinimizeAppAsync(string query)
        {
            var lower = query.ToLowerInvariant().Trim();
            string[] processNames;
            
            if (AppDatabase.TryGetValue(lower, out var app))
            {
                processNames = app.ProcessNames;
            }
            else
            {
                processNames = new[] { query };
            }
            
            foreach (var name in processNames)
            {
                try
                {
                    var procs = Process.GetProcessesByName(name.Replace(".exe", ""));
                    foreach (var proc in procs.Where(p => p.MainWindowHandle != IntPtr.Zero))
                    {
                        ShowWindow(proc.MainWindowHandle, SW_MINIMIZE);
                    }
                    if (procs.Any())
                        return $"✓ Minimized {query}";
                }
                catch { }
            }
            
            return $"❌ {query} isn't running";
        }
        
        /// <summary>
        /// Get currently running apps
        /// </summary>
        public static List<string> GetRunningApps()
        {
            return Process.GetProcesses()
                .Where(p => p.MainWindowHandle != IntPtr.Zero && !string.IsNullOrEmpty(p.MainWindowTitle))
                .Select(p => p.MainWindowTitle)
                .Distinct()
                .OrderBy(x => x)
                .ToList();
        }
        
        /// <summary>
        /// Get the current foreground app
        /// </summary>
        public static string? GetForegroundApp()
        {
            try
            {
                var hwnd = GetForegroundWindow();
                GetWindowThreadProcessId(hwnd, out uint pid);
                var proc = Process.GetProcessById((int)pid);
                return proc.MainWindowTitle;
            }
            catch
            {
                return null;
            }
        }
        
        private static async Task<string> LaunchAppAsync(AppInfo app)
        {
            try
            {
                var psi = new ProcessStartInfo(app.Command) { UseShellExecute = true };
                Process.Start(psi);
                return $"✓ Opened {app.DisplayName}";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AppControl] Launch error: {ex.Message}");
                return $"❌ Couldn't open {app.DisplayName}";
            }
        }
        
        private static int LevenshteinDistance(string s1, string s2)
        {
            int[,] d = new int[s1.Length + 1, s2.Length + 1];
            for (int i = 0; i <= s1.Length; i++) d[i, 0] = i;
            for (int j = 0; j <= s2.Length; j++) d[0, j] = j;
            
            for (int i = 1; i <= s1.Length; i++)
            {
                for (int j = 1; j <= s2.Length; j++)
                {
                    int cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                }
            }
            return d[s1.Length, s2.Length];
        }
    }
    
    public class AppInfo
    {
        public string Command { get; }
        public string DisplayName { get; }
        public string[] ProcessNames { get; }
        public bool IsProtocol { get; }
        
        public AppInfo(string command, string displayName, string[] processNames, bool isProtocol = false)
        {
            Command = command;
            DisplayName = displayName;
            ProcessNames = processNames;
            IsProtocol = isProtocol;
        }
    }
}
