using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Win32;
using MinimalApp.Ledger;

namespace MinimalApp.SecuritySuite.Services
{
    /// <summary>
    /// Watches startup entries (Registry Run keys + Startup folders) for changes.
    /// </summary>
    public class StartupWatcher : IDisposable
    {
        private static readonly Lazy<StartupWatcher> _instance = new(() => new StartupWatcher());
        public static StartupWatcher Instance => _instance.Value;

        private Timer? _pollTimer;
        private readonly object _lock = new();
        private bool _isDisposed;
        private bool _isInitialized;

        // Baseline snapshots
        private Dictionary<string, StartupEntry> _registryBaseline = new();
        private Dictionary<string, StartupFolderEntry> _folderBaseline = new();

        // Registry paths to monitor
        private static readonly (RegistryKey Root, string Path, string Location)[] RegistryPaths = new[]
        {
            (Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", "HKCU\\...\\Run"),
            (Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run", "HKLM\\...\\Run"),
        };

        // Startup folder paths
        private readonly string _userStartupFolder;
        private readonly string _allUsersStartupFolder;

        private const int PollIntervalMs = 4000; // 4 seconds

        public event Action<string>? StatusChanged;

        private StartupWatcher()
        {
            _userStartupFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"Microsoft\Windows\Start Menu\Programs\Startup");

            _allUsersStartupFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                @"Microsoft\Windows\Start Menu\Programs\Startup");
        }

        public void Start()
        {
            if (_isInitialized) return;

            try
            {
                // Capture baseline
                CaptureBaseline();

                // Start polling timer
                _pollTimer = new Timer(OnPollTick, null, PollIntervalMs, PollIntervalMs);

                _isInitialized = true;
                StatusChanged?.Invoke("Startup watcher started");
                Debug.WriteLine("[StartupWatcher] Started monitoring startup entries");

                // Add initial ledger event
                var initEvent = new LedgerEvent
                {
                    Category = LedgerCategory.Startup,
                    Severity = LedgerSeverity.Info,
                    Title = "Startup monitoring active",
                    WhyItMatters = "Atlas is watching for new programs added to startup."
                };
                initEvent.WithEvidence("Registry Keys", $"{_registryBaseline.Count} entries")
                         .WithEvidence("Startup Folders", $"{_folderBaseline.Count} items")
                         .WithAction(LedgerAction.Dismiss("Got it"));

                LedgerManager.Instance.AddEvent(initEvent);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StartupWatcher] Failed to start: {ex.Message}");
                StatusChanged?.Invoke($"Failed to start startup watcher: {ex.Message}");
            }
        }

        public void Stop()
        {
            _pollTimer?.Dispose();
            _pollTimer = null;
            _isInitialized = false;
            StatusChanged?.Invoke("Startup watcher stopped");
        }

        private void CaptureBaseline()
        {
            _registryBaseline = GetRegistryEntries();
            _folderBaseline = GetStartupFolderEntries();
            Debug.WriteLine($"[StartupWatcher] Baseline: {_registryBaseline.Count} registry, {_folderBaseline.Count} folder entries");
        }

        private void OnPollTick(object? state)
        {
            if (_isDisposed) return;

            lock (_lock)
            {
                try
                {
                    CheckRegistryChanges();
                    CheckFolderChanges();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[StartupWatcher] Poll error: {ex.Message}");
                }
            }
        }

        private void CheckRegistryChanges()
        {
            var current = GetRegistryEntries();

            // Check for added entries
            foreach (var kvp in current)
            {
                if (!_registryBaseline.TryGetValue(kvp.Key, out var oldEntry))
                {
                    // New entry added
                    CreateRegistryAddedEvent(kvp.Value);
                }
                else if (oldEntry.Command != kvp.Value.Command)
                {
                    // Entry modified
                    CreateRegistryModifiedEvent(oldEntry, kvp.Value);
                }
            }

            // Check for removed entries
            foreach (var kvp in _registryBaseline)
            {
                if (!current.ContainsKey(kvp.Key))
                {
                    CreateRegistryRemovedEvent(kvp.Value);
                }
            }

            _registryBaseline = current;
        }

        private void CheckFolderChanges()
        {
            var current = GetStartupFolderEntries();

            // Check for added entries
            foreach (var kvp in current)
            {
                if (!_folderBaseline.ContainsKey(kvp.Key))
                {
                    CreateFolderAddedEvent(kvp.Value);
                }
            }

            // Check for removed entries
            foreach (var kvp in _folderBaseline)
            {
                if (!current.ContainsKey(kvp.Key))
                {
                    CreateFolderRemovedEvent(kvp.Value);
                }
            }

            _folderBaseline = current;
        }

        #region Registry Entry Detection

        private Dictionary<string, StartupEntry> GetRegistryEntries()
        {
            var entries = new Dictionary<string, StartupEntry>();

            foreach (var (root, path, location) in RegistryPaths)
            {
                try
                {
                    using var key = root.OpenSubKey(path, false);
                    if (key == null) continue;

                    foreach (var valueName in key.GetValueNames())
                    {
                        var value = key.GetValue(valueName)?.ToString() ?? "";
                        var fullKey = $"{location}\\{valueName}";
                        entries[fullKey] = new StartupEntry
                        {
                            Name = valueName,
                            Command = value,
                            RegistryPath = $"{root.Name}\\{path}",
                            Location = location,
                            IsHKLM = root == Registry.LocalMachine
                        };
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[StartupWatcher] Error reading {location}: {ex.Message}");
                }
            }

            return entries;
        }

        private void CreateRegistryAddedEvent(StartupEntry entry)
        {
            Debug.WriteLine($"[StartupWatcher] Registry entry added: {entry.Name}");

            var evt = new LedgerEvent
            {
                Category = LedgerCategory.Startup,
                Severity = LedgerSeverity.High,
                Title = "Startup entry added",
                WhyItMatters = "A new program has been configured to run at Windows startup. This could be legitimate software or potentially unwanted.",
                BackupData = null // No previous value for new entries
            };

            evt.WithEvidence("Name", entry.Name)
               .WithEvidence("Command", entry.Command)
               .WithEvidence("Location", entry.RegistryPath)
               .WithEvidence("Detected", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            // Store entry data for revert
            evt.BackupData = System.Text.Json.JsonSerializer.Serialize(new
            {
                Type = "RegistryAdd",
                entry.Name,
                entry.Command,
                entry.RegistryPath,
                entry.IsHKLM
            });

            evt.WithAction(new LedgerAction
            {
                Label = "🗑️ Remove",
                Type = LedgerActionType.Delete,
                Data = evt.BackupData,
                RequiresConfirmation = true
            });
            evt.WithAction(LedgerAction.Inspect("📂 Open Location"));
            evt.WithAction(LedgerAction.Dismiss("✓ Allow"));

            LedgerManager.Instance.AddEvent(evt);
            StatusChanged?.Invoke($"Startup entry added: {entry.Name}");
        }

        private void CreateRegistryModifiedEvent(StartupEntry oldEntry, StartupEntry newEntry)
        {
            Debug.WriteLine($"[StartupWatcher] Registry entry modified: {newEntry.Name}");

            var evt = new LedgerEvent
            {
                Category = LedgerCategory.Startup,
                Severity = LedgerSeverity.High,
                Title = "Startup entry modified",
                WhyItMatters = "An existing startup entry was changed. Verify this is expected."
            };

            evt.WithEvidence("Name", newEntry.Name)
               .WithEvidence("Old Command", oldEntry.Command)
               .WithEvidence("New Command", newEntry.Command)
               .WithEvidence("Location", newEntry.RegistryPath)
               .WithEvidence("Detected", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            // Store old value for revert
            evt.BackupData = System.Text.Json.JsonSerializer.Serialize(new
            {
                Type = "RegistryModify",
                oldEntry.Name,
                OldCommand = oldEntry.Command,
                NewCommand = newEntry.Command,
                oldEntry.RegistryPath,
                oldEntry.IsHKLM
            });

            evt.WithAction(new LedgerAction
            {
                Label = "⏪ Revert",
                Type = LedgerActionType.Revert,
                Data = evt.BackupData,
                RequiresConfirmation = true
            });
            evt.WithAction(LedgerAction.Dismiss("✓ Allow"));

            LedgerManager.Instance.AddEvent(evt);
            StatusChanged?.Invoke($"Startup entry modified: {newEntry.Name}");
        }

        private void CreateRegistryRemovedEvent(StartupEntry entry)
        {
            Debug.WriteLine($"[StartupWatcher] Registry entry removed: {entry.Name}");

            var evt = new LedgerEvent
            {
                Category = LedgerCategory.Startup,
                Severity = LedgerSeverity.Medium,
                Title = "Startup entry removed",
                WhyItMatters = "A startup entry was removed. This may be from uninstalling software or manual cleanup."
            };

            evt.WithEvidence("Name", entry.Name)
               .WithEvidence("Command", entry.Command)
               .WithEvidence("Location", entry.RegistryPath)
               .WithEvidence("Detected", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            evt.WithAction(LedgerAction.Dismiss("✓ Got it"));

            LedgerManager.Instance.AddEvent(evt);
            StatusChanged?.Invoke($"Startup entry removed: {entry.Name}");
        }

        #endregion

        #region Startup Folder Detection

        private Dictionary<string, StartupFolderEntry> GetStartupFolderEntries()
        {
            var entries = new Dictionary<string, StartupFolderEntry>();

            ScanStartupFolder(_userStartupFolder, "User Startup", false, entries);
            ScanStartupFolder(_allUsersStartupFolder, "All Users Startup", true, entries);

            return entries;
        }

        private void ScanStartupFolder(string folderPath, string location, bool isAllUsers, Dictionary<string, StartupFolderEntry> entries)
        {
            if (!Directory.Exists(folderPath)) return;

            try
            {
                foreach (var file in Directory.GetFiles(folderPath))
                {
                    var info = new FileInfo(file);
                    entries[file] = new StartupFolderEntry
                    {
                        FileName = info.Name,
                        FullPath = file,
                        FolderPath = folderPath,
                        Location = location,
                        LastWriteTime = info.LastWriteTime,
                        IsAllUsers = isAllUsers
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StartupWatcher] Error scanning {location}: {ex.Message}");
            }
        }

        private void CreateFolderAddedEvent(StartupFolderEntry entry)
        {
            Debug.WriteLine($"[StartupWatcher] Startup folder item added: {entry.FileName}");

            var evt = new LedgerEvent
            {
                Category = LedgerCategory.Startup,
                Severity = LedgerSeverity.High,
                Title = "Startup shortcut added",
                WhyItMatters = "A new file was added to the startup folder. It will run automatically when Windows starts."
            };

            evt.WithEvidence("File", entry.FileName)
               .WithEvidence("Path", entry.FullPath, isPath: true)
               .WithEvidence("Location", entry.Location)
               .WithEvidence("Detected", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            evt.BackupData = System.Text.Json.JsonSerializer.Serialize(new
            {
                Type = "FolderAdd",
                entry.FileName,
                entry.FullPath,
                entry.IsAllUsers
            });

            evt.WithAction(new LedgerAction
            {
                Label = "🗑️ Delete",
                Type = LedgerActionType.Delete,
                Data = evt.BackupData,
                RequiresConfirmation = true
            });
            evt.WithAction(LedgerAction.Inspect("📂 Open Location", entry.FolderPath));
            evt.WithAction(LedgerAction.Dismiss("✓ Allow"));

            LedgerManager.Instance.AddEvent(evt);
            StatusChanged?.Invoke($"Startup shortcut added: {entry.FileName}");
        }

        private void CreateFolderRemovedEvent(StartupFolderEntry entry)
        {
            Debug.WriteLine($"[StartupWatcher] Startup folder item removed: {entry.FileName}");

            var evt = new LedgerEvent
            {
                Category = LedgerCategory.Startup,
                Severity = LedgerSeverity.Low,
                Title = "Startup shortcut removed",
                WhyItMatters = "A file was removed from the startup folder."
            };

            evt.WithEvidence("File", entry.FileName)
               .WithEvidence("Path", entry.FullPath)
               .WithEvidence("Location", entry.Location)
               .WithEvidence("Detected", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            evt.WithAction(LedgerAction.Dismiss("✓ Got it"));

            LedgerManager.Instance.AddEvent(evt);
            StatusChanged?.Invoke($"Startup shortcut removed: {entry.FileName}");
        }

        #endregion

        #region Revert Actions

        /// <summary>
        /// Remove a registry startup entry
        /// </summary>
        public (bool Success, string Message) RemoveRegistryEntry(string name, string registryPath, bool isHKLM)
        {
            try
            {
                var root = isHKLM ? Registry.LocalMachine : Registry.CurrentUser;
                var subKeyPath = registryPath.Replace(root.Name + "\\", "");

                using var key = root.OpenSubKey(subKeyPath, writable: true);
                if (key == null)
                    return (false, "Registry key not found");

                key.DeleteValue(name, throwOnMissingValue: false);

                // Create follow-up event
                var evt = new LedgerEvent
                {
                    Category = LedgerCategory.Startup,
                    Severity = LedgerSeverity.Info,
                    Title = "Startup entry removed",
                    WhyItMatters = "The startup entry was successfully removed."
                };
                evt.WithEvidence("Name", name)
                   .WithEvidence("Location", registryPath)
                   .WithEvidence("Removed At", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
                   .WithAction(LedgerAction.Dismiss("Got it"));

                LedgerManager.Instance.AddEvent(evt);

                return (true, "✅ Startup entry removed");
            }
            catch (UnauthorizedAccessException)
            {
                return (false, "⚠️ Administrator privileges required to modify HKLM entries");
            }
            catch (Exception ex)
            {
                return (false, $"❌ Failed to remove entry: {ex.Message}");
            }
        }

        /// <summary>
        /// Restore a modified registry entry to its previous value
        /// </summary>
        public (bool Success, string Message) RestoreRegistryEntry(string name, string oldCommand, string registryPath, bool isHKLM)
        {
            try
            {
                var root = isHKLM ? Registry.LocalMachine : Registry.CurrentUser;
                var subKeyPath = registryPath.Replace(root.Name + "\\", "");

                using var key = root.OpenSubKey(subKeyPath, writable: true);
                if (key == null)
                    return (false, "Registry key not found");

                key.SetValue(name, oldCommand);

                // Create follow-up event
                var evt = new LedgerEvent
                {
                    Category = LedgerCategory.Startup,
                    Severity = LedgerSeverity.Info,
                    Title = "Startup entry restored",
                    WhyItMatters = "The startup entry was restored to its previous value."
                };
                evt.WithEvidence("Name", name)
                   .WithEvidence("Restored Command", oldCommand)
                   .WithEvidence("Restored At", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
                   .WithAction(LedgerAction.Dismiss("Got it"));

                LedgerManager.Instance.AddEvent(evt);

                return (true, "✅ Startup entry restored");
            }
            catch (UnauthorizedAccessException)
            {
                return (false, "⚠️ Administrator privileges required to modify HKLM entries");
            }
            catch (Exception ex)
            {
                return (false, $"❌ Failed to restore entry: {ex.Message}");
            }
        }

        /// <summary>
        /// Delete a startup folder item
        /// </summary>
        public (bool Success, string Message) DeleteStartupFile(string fullPath)
        {
            try
            {
                if (!File.Exists(fullPath))
                    return (false, "File not found");

                var fileName = Path.GetFileName(fullPath);
                File.Delete(fullPath);

                // Create follow-up event
                var evt = new LedgerEvent
                {
                    Category = LedgerCategory.Startup,
                    Severity = LedgerSeverity.Info,
                    Title = "Startup shortcut deleted",
                    WhyItMatters = "The startup shortcut was successfully deleted."
                };
                evt.WithEvidence("File", fileName)
                   .WithEvidence("Deleted At", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
                   .WithAction(LedgerAction.Dismiss("Got it"));

                LedgerManager.Instance.AddEvent(evt);

                return (true, "✅ Startup shortcut deleted");
            }
            catch (UnauthorizedAccessException)
            {
                return (false, "⚠️ Administrator privileges required to delete from All Users startup");
            }
            catch (Exception ex)
            {
                return (false, $"❌ Failed to delete file: {ex.Message}");
            }
        }

        #endregion

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            Stop();
            GC.SuppressFinalize(this);
        }

        #region Data Classes

        private class StartupEntry
        {
            public string Name { get; set; } = "";
            public string Command { get; set; } = "";
            public string RegistryPath { get; set; } = "";
            public string Location { get; set; } = "";
            public bool IsHKLM { get; set; }
        }

        private class StartupFolderEntry
        {
            public string FileName { get; set; } = "";
            public string FullPath { get; set; } = "";
            public string FolderPath { get; set; } = "";
            public string Location { get; set; } = "";
            public DateTime LastWriteTime { get; set; }
            public bool IsAllUsers { get; set; }
        }

        #endregion
    }
}
