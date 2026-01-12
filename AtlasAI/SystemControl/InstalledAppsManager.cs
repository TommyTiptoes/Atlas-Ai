using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace MinimalApp.SystemControl
{
    /// <summary>
    /// Manages installed applications - scans, remembers, and launches apps
    /// </summary>
    public class InstalledAppsManager
    {
        private static InstalledAppsManager? _instance;
        public static InstalledAppsManager Instance => _instance ??= new InstalledAppsManager();
        
        private Dictionary<string, InstalledApp> _apps = new();
        private readonly string _cacheFile;
        private DateTime _lastScan = DateTime.MinValue;
        private FileSystemWatcher? _startMenuWatcher;
        private FileSystemWatcher? _desktopWatcher;
        
        public event Action<string>? AppInstalled;
        public event Action<string>? AppRemoved;
        
        public int AppCount => _apps.Count;
        
        private InstalledAppsManager()
        {
            _cacheFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AtlasAI", "installed_apps.json");
            
            Directory.CreateDirectory(Path.GetDirectoryName(_cacheFile)!);
            LoadCache();
        }
        
        public async Task InitializeAsync()
        {
            await ScanAllAppsAsync();
            SetupFileWatchers();
        }
        
        public async Task ScanAllAppsAsync()
        {
            await Task.Run(() =>
            {
                var newApps = new Dictionary<string, InstalledApp>(StringComparer.OrdinalIgnoreCase);
                
                ScanStartMenu(newApps);
                ScanDesktop(newApps);
                ScanRegistry(newApps);
                ScanProgramFolders(newApps);
                AddBuiltInApps(newApps);
                
                _apps = newApps;
                _lastScan = DateTime.Now;
                SaveCache();
                
                Debug.WriteLine($"[InstalledApps] Scanned {_apps.Count} applications");
            });
        }

        
        private void ScanStartMenu(Dictionary<string, InstalledApp> apps)
        {
            var startMenuPaths = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)
            };
            
            foreach (var startMenu in startMenuPaths)
            {
                if (!Directory.Exists(startMenu)) continue;
                
                try
                {
                    foreach (var lnk in Directory.GetFiles(startMenu, "*.lnk", SearchOption.AllDirectories))
                    {
                        try
                        {
                            var target = GetShortcutTarget(lnk);
                            if (string.IsNullOrEmpty(target) || !File.Exists(target)) continue;
                            if (!target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                            
                            var name = Path.GetFileNameWithoutExtension(lnk);
                            if (name.Contains("uninstall", StringComparison.OrdinalIgnoreCase) ||
                                name.Contains("update", StringComparison.OrdinalIgnoreCase)) continue;
                            
                            AddApp(apps, name, target, "StartMenu");
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }
        
        private void ScanDesktop(Dictionary<string, InstalledApp> apps)
        {
            var desktopPaths = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
            };
            
            foreach (var desktop in desktopPaths)
            {
                if (!Directory.Exists(desktop)) continue;
                
                try
                {
                    foreach (var lnk in Directory.GetFiles(desktop, "*.lnk"))
                    {
                        try
                        {
                            var target = GetShortcutTarget(lnk);
                            if (string.IsNullOrEmpty(target) || !File.Exists(target)) continue;
                            if (!target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                            
                            var name = Path.GetFileNameWithoutExtension(lnk);
                            AddApp(apps, name, target, "Desktop");
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }
        
        private void ScanRegistry(Dictionary<string, InstalledApp> apps)
        {
            var registryPaths = new[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };
            
            foreach (var regPath in registryPaths)
            {
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(regPath);
                    if (key == null) continue;
                    
                    foreach (var subKeyName in key.GetSubKeyNames())
                    {
                        try
                        {
                            using var subKey = key.OpenSubKey(subKeyName);
                            if (subKey == null) continue;
                            
                            var displayName = subKey.GetValue("DisplayName") as string;
                            var installLocation = subKey.GetValue("InstallLocation") as string;
                            var displayIcon = subKey.GetValue("DisplayIcon") as string;
                            
                            if (string.IsNullOrEmpty(displayName)) continue;
                            
                            string? exePath = null;
                            
                            if (!string.IsNullOrEmpty(displayIcon) && displayIcon.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                            {
                                exePath = displayIcon.Split(',')[0].Trim('"');
                            }
                            else if (!string.IsNullOrEmpty(installLocation) && Directory.Exists(installLocation))
                            {
                                var exes = Directory.GetFiles(installLocation, "*.exe", SearchOption.TopDirectoryOnly);
                                exePath = exes.FirstOrDefault(e => 
                                    Path.GetFileNameWithoutExtension(e).Contains(displayName.Split(' ')[0], StringComparison.OrdinalIgnoreCase));
                                if (exePath == null && exes.Length > 0)
                                    exePath = exes[0];
                            }
                            
                            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                            {
                                AddApp(apps, displayName, exePath, "Registry");
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }
        
        private void ScanProgramFolders(Dictionary<string, InstalledApp> apps)
        {
            var programPaths = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                @"D:\Program Files", @"D:\Games", @"D:\Steam", @"D:\SteamLibrary\steamapps\common",
                @"C:\Program Files (x86)\Steam\steamapps\common"
            };
            
            foreach (var programPath in programPaths.Distinct())
            {
                if (!Directory.Exists(programPath)) continue;
                
                try
                {
                    foreach (var dir in Directory.GetDirectories(programPath))
                    {
                        try
                        {
                            var dirName = Path.GetFileName(dir);
                            if (dirName.StartsWith("Windows") || dirName == "Common Files") continue;
                            
                            var exes = Directory.GetFiles(dir, "*.exe", SearchOption.TopDirectoryOnly)
                                .Where(e => !Path.GetFileName(e).Contains("unins", StringComparison.OrdinalIgnoreCase) &&
                                           !Path.GetFileName(e).Contains("update", StringComparison.OrdinalIgnoreCase))
                                .ToList();
                            
                            var mainExe = exes.FirstOrDefault(e => 
                                Path.GetFileNameWithoutExtension(e).Equals(dirName, StringComparison.OrdinalIgnoreCase));
                            
                            if (mainExe == null)
                                mainExe = exes.FirstOrDefault(e => 
                                    Path.GetFileNameWithoutExtension(e).Contains(dirName.Split(' ')[0], StringComparison.OrdinalIgnoreCase));
                            
                            if (mainExe == null && exes.Count > 0)
                                mainExe = exes[0];
                            
                            if (mainExe != null)
                                AddApp(apps, dirName, mainExe, "ProgramFiles");
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }

        
        private void AddBuiltInApps(Dictionary<string, InstalledApp> apps)
        {
            // Common Windows apps and their paths/commands
            var builtIn = new Dictionary<string, (string Path, bool IsUWP)>
            {
                { "notepad", ("notepad.exe", false) },
                { "calculator", ("calc.exe", false) },
                { "calc", ("calc.exe", false) },
                { "paint", ("mspaint.exe", false) },
                { "wordpad", ("wordpad.exe", false) },
                { "snipping tool", ("snippingtool.exe", false) },
                { "file explorer", ("explorer.exe", false) },
                { "explorer", ("explorer.exe", false) },
                { "cmd", ("cmd.exe", false) },
                { "command prompt", ("cmd.exe", false) },
                { "powershell", ("powershell.exe", false) },
                { "terminal", ("wt.exe", false) },
                { "windows terminal", ("wt.exe", false) },
                { "task manager", ("taskmgr.exe", false) },
                { "control panel", ("control.exe", false) },
                { "settings", ("ms-settings:", true) },
                { "edge", ("msedge.exe", false) },
                { "microsoft edge", ("msedge.exe", false) },
            };
            
            foreach (var (name, (path, isUwp)) in builtIn)
            {
                if (!apps.ContainsKey(name))
                {
                    apps[name] = new InstalledApp
                    {
                        Name = name,
                        ExecutablePath = path,
                        Source = "BuiltIn",
                        IsUWP = isUwp,
                        LastSeen = DateTime.Now
                    };
                }
            }
            
            // Add common apps with known paths
            AddKnownApp(apps, "chrome", "Google Chrome", @"C:\Program Files\Google\Chrome\Application\chrome.exe");
            AddKnownApp(apps, "firefox", "Firefox", @"C:\Program Files\Mozilla Firefox\firefox.exe");
            AddKnownApp(apps, "spotify", "Spotify", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Spotify", "Spotify.exe"));
            AddKnownApp(apps, "discord", "Discord", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Discord", "Update.exe"));
            AddKnownApp(apps, "steam", "Steam", @"C:\Program Files (x86)\Steam\steam.exe");
            AddKnownApp(apps, "vscode", "Visual Studio Code", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Microsoft VS Code", "Code.exe"));
            AddKnownApp(apps, "visual studio code", "Visual Studio Code", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Microsoft VS Code", "Code.exe"));
        }
        
        private void AddKnownApp(Dictionary<string, InstalledApp> apps, string key, string name, string path)
        {
            if (!apps.ContainsKey(key) && File.Exists(path))
            {
                apps[key] = new InstalledApp { Name = name, ExecutablePath = path, Source = "Known", IsUWP = false, LastSeen = DateTime.Now };
            }
        }
        
        private void AddApp(Dictionary<string, InstalledApp> apps, string name, string exePath, string source)
        {
            var key = name.ToLower().Trim();
            if (apps.ContainsKey(key)) return;
            
            apps[key] = new InstalledApp
            {
                Name = name,
                ExecutablePath = exePath,
                Source = source,
                IsUWP = false,
                LastSeen = DateTime.Now
            };
            
            // Add aliases
            var aliases = GenerateAliases(name);
            foreach (var alias in aliases)
            {
                if (!apps.ContainsKey(alias))
                    apps[alias] = apps[key];
            }
        }
        
        private List<string> GenerateAliases(string name)
        {
            var aliases = new List<string>();
            var lower = name.ToLower();
            
            var cleaned = lower.Replace(" - shortcut", "").Replace(" shortcut", "")
                .Replace(" (x64)", "").Replace(" (x86)", "").Replace(" (64-bit)", "").Replace(" (32-bit)", "").Trim();
            
            if (cleaned != lower) aliases.Add(cleaned);
            
            var words = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > 1) aliases.Add(words[0]);
            
            // Common abbreviations
            if (cleaned.Contains("visual studio code")) aliases.Add("vscode");
            if (cleaned.Contains("visual studio") && !cleaned.Contains("code")) aliases.Add("vs");
            if (cleaned.Contains("google chrome")) { aliases.Add("chrome"); aliases.Add("browser"); }
            if (cleaned.Contains("mozilla firefox")) { aliases.Add("firefox"); }
            if (cleaned.Contains("microsoft edge")) { aliases.Add("edge"); }
            
            return aliases;
        }
        
        // Simple .lnk parser without COM
        private string? GetShortcutTarget(string lnkPath)
        {
            try
            {
                using var fs = new FileStream(lnkPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var br = new BinaryReader(fs);
                
                // Read header
                fs.Seek(0x14, SeekOrigin.Begin);
                var flags = br.ReadUInt32();
                
                fs.Seek(0x4C, SeekOrigin.Begin);
                
                // Skip shell item ID list if present
                if ((flags & 1) == 1)
                {
                    var idListSize = br.ReadUInt16();
                    fs.Seek(idListSize, SeekOrigin.Current);
                }
                
                // Read file location info
                var fileInfoStart = fs.Position;
                var fileInfoSize = br.ReadUInt32();
                br.ReadUInt32(); // header size
                br.ReadUInt32(); // flags
                br.ReadUInt32(); // volume ID offset
                var localPathOffset = br.ReadUInt32();
                
                fs.Seek(fileInfoStart + localPathOffset, SeekOrigin.Begin);
                
                var pathBytes = new List<byte>();
                byte b;
                while ((b = br.ReadByte()) != 0 && pathBytes.Count < 260)
                    pathBytes.Add(b);
                
                return Encoding.Default.GetString(pathBytes.ToArray());
            }
            catch
            {
                return null;
            }
        }

        
        private void SetupFileWatchers()
        {
            try
            {
                var startMenu = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
                if (Directory.Exists(startMenu))
                {
                    _startMenuWatcher = new FileSystemWatcher(startMenu, "*.lnk")
                    {
                        IncludeSubdirectories = true,
                        EnableRaisingEvents = true
                    };
                    _startMenuWatcher.Created += OnShortcutCreated;
                }
                
                var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                if (Directory.Exists(desktop))
                {
                    _desktopWatcher = new FileSystemWatcher(desktop, "*.lnk") { EnableRaisingEvents = true };
                    _desktopWatcher.Created += OnShortcutCreated;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[InstalledApps] Watcher error: {ex.Message}");
            }
        }
        
        private async void OnShortcutCreated(object sender, FileSystemEventArgs e)
        {
            await Task.Delay(1000);
            try
            {
                var target = GetShortcutTarget(e.FullPath);
                if (!string.IsNullOrEmpty(target) && File.Exists(target) && target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    var name = Path.GetFileNameWithoutExtension(e.Name);
                    AddApp(_apps, name!, target, "NewInstall");
                    SaveCache();
                    AppInstalled?.Invoke(name!);
                }
            }
            catch { }
        }
        
        public InstalledApp? FindApp(string query)
        {
            var lower = query.ToLower().Trim();
            
            if (_apps.TryGetValue(lower, out var exact))
                return exact;
            
            var contains = _apps.FirstOrDefault(a => a.Key.Contains(lower) || lower.Contains(a.Key));
            if (contains.Value != null)
                return contains.Value;
            
            return null;
        }
        
        public (bool Success, string Message) LaunchApp(string appName)
        {
            var app = FindApp(appName);
            if (app == null)
            {
                var builtIn = TryLaunchBuiltIn(appName);
                if (builtIn.Success) return builtIn;
                return (false, $"Couldn't find '{appName}'. Say \"scan my apps\" to discover installed programs.");
            }
            
            try
            {
                if (app.IsUWP || app.ExecutablePath.StartsWith("ms-"))
                {
                    Process.Start(new ProcessStartInfo(app.ExecutablePath) { UseShellExecute = true });
                }
                else if (app.ExecutablePath.Contains("Discord") && app.ExecutablePath.Contains("Update.exe"))
                {
                    Process.Start(app.ExecutablePath, "--processStart Discord.exe");
                }
                else
                {
                    Process.Start(new ProcessStartInfo(app.ExecutablePath) { UseShellExecute = true });
                }
                return (true, $"🚀 Launching {app.Name}.");
            }
            catch (Exception ex)
            {
                return (false, $"❌ Couldn't launch {app.Name}: {ex.Message}");
            }
        }
        
        private (bool Success, string Message) TryLaunchBuiltIn(string name)
        {
            var lower = name.ToLower();
            try
            {
                if (lower.Contains("notepad")) { Process.Start("notepad.exe"); return (true, "🚀 Launching Notepad."); }
                if (lower.Contains("calculator") || lower == "calc") { Process.Start("calc.exe"); return (true, "🚀 Launching Calculator."); }
                if (lower.Contains("paint")) { Process.Start("mspaint.exe"); return (true, "🚀 Launching Paint."); }
                if (lower.Contains("explorer")) { Process.Start("explorer.exe"); return (true, "🚀 Launching File Explorer."); }
                if (lower == "cmd" || lower.Contains("command prompt")) { Process.Start("cmd.exe"); return (true, "🚀 Launching Command Prompt."); }
                if (lower.Contains("powershell")) { Process.Start("powershell.exe"); return (true, "🚀 Launching PowerShell."); }
                if (lower.Contains("terminal")) { Process.Start(new ProcessStartInfo("wt.exe") { UseShellExecute = true }); return (true, "🚀 Launching Terminal."); }
                if (lower.Contains("edge")) { Process.Start(new ProcessStartInfo("msedge.exe") { UseShellExecute = true }); return (true, "🚀 Launching Edge."); }
                if (lower.Contains("chrome")) { Process.Start(new ProcessStartInfo("chrome.exe") { UseShellExecute = true }); return (true, "🚀 Launching Chrome."); }
                if (lower.Contains("firefox")) { Process.Start(new ProcessStartInfo("firefox.exe") { UseShellExecute = true }); return (true, "🚀 Launching Firefox."); }
            }
            catch { }
            return (false, "");
        }
        
        public List<InstalledApp> GetAllApps() => _apps.Values.Distinct().ToList();
        
        public List<InstalledApp> SearchApps(string query)
        {
            var lower = query.ToLower();
            return _apps.Values.Where(a => a.Name.ToLower().Contains(lower)).Distinct().OrderBy(a => a.Name).ToList();
        }
        
        private void LoadCache()
        {
            try
            {
                if (File.Exists(_cacheFile))
                {
                    var json = File.ReadAllText(_cacheFile);
                    var cached = JsonSerializer.Deserialize<List<InstalledApp>>(json);
                    if (cached != null)
                        foreach (var app in cached)
                            _apps[app.Name.ToLower()] = app;
                }
            }
            catch { }
        }
        
        private void SaveCache()
        {
            try
            {
                var apps = _apps.Values.Distinct().ToList();
                var json = JsonSerializer.Serialize(apps, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_cacheFile, json);
            }
            catch { }
        }
    }
}
