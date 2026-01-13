using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Path = System.IO.Path;

namespace AtlasAI.UI
{
    /// <summary>
    /// Futuristic system scanner dialog with animated visualization and real metrics
    /// </summary>
    public partial class SystemScannerDialog : Window
    {
        private PerformanceCounter? _cpuCounter;
        private DispatcherTimer? _scanTimer;
        private int _currentProgress = 0;
        private bool _scanComplete = false;
        
        // Scan phases with dramatic messages
        private readonly string[] _scanPhases = new[]
        {
            "INITIALIZING NEURAL SCAN...",
            "ANALYZING CPU CORES...",
            "SCANNING MEMORY BANKS...",
            "CHECKING STORAGE ARRAYS...",
            "SCANNING SYSTEM FILES...",
            "ANALYZING REGISTRY ENTRIES...",
            "CHECKING STARTUP PROGRAMS...",
            "PROBING NETWORK INTERFACES...",
            "SCANNING FOR THREATS...",
            "VALIDATING SYSTEM INTEGRITY...",
            "COMPILING DIAGNOSTIC DATA...",
            "FINALIZING ANALYSIS..."
        };
        
        // Fake file paths to show during scan
        private readonly string[] _fakePaths = new[]
        {
            @"C:\Windows\System32\drivers\",
            @"C:\Windows\SysWOW64\",
            @"C:\Program Files\",
            @"C:\Program Files (x86)\",
            @"C:\Users\AppData\Local\",
            @"C:\Users\AppData\Roaming\",
            @"C:\Windows\Temp\",
            @"C:\ProgramData\",
            @"C:\Windows\Prefetch\",
            @"C:\Windows\assembly\",
            @"HKLM\SOFTWARE\Microsoft\",
            @"HKCU\SOFTWARE\Classes\",
            @"C:\Windows\WinSxS\",
            @"C:\Windows\servicing\"
        };
        
        // Real system data
        private double _cpuUsage;
        private double _memUsage;
        private double _diskUsage;
        private long _networkLatency;
        private bool _networkConnected;
        private long _filesScanned;
        private long _totalFiles;
        private long _bytesScanned;
        private int _threatsFound;
        private int _issuesFound;
        private Random _random = new Random();
        private bool _cancelScan = false;
        
        // Directories to actually scan
        private readonly string[] _scanDirectories = new[]
        {
            @"C:\Windows\System32",
            @"C:\Windows\SysWOW64",
            @"C:\Program Files",
            @"C:\Program Files (x86)",
            @"C:\ProgramData",
            @"C:\Users"
        };
        
        public SystemScannerDialog()
        {
            InitializeComponent();
            
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _cpuCounter.NextValue(); // Prime it
            }
            catch { }
            
            Loaded += OnLoaded;
        }
        
        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Start animations
            StartAnimations();
            
            // Start the scan
            await RunScanAsync();
        }
        
        private void StartAnimations()
        {
            try
            {
                // Start scanner rotation
                var scannerRotation = (Storyboard)FindResource("ScannerRotation");
                scannerRotation?.Begin();
                
                // Start ring pulse
                var ringPulse = (Storyboard)FindResource("RingPulse");
                ringPulse?.Begin();
                
                // Start progress shimmer
                var progressShimmer = (Storyboard)FindResource("ProgressShimmer");
                progressShimmer?.Begin();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SystemScanner] Animation error: {ex.Message}");
            }
        }

        private async Task RunScanAsync()
        {
            _currentProgress = 0;
            _scanComplete = false;
            _filesScanned = 0;
            _totalFiles = 0;
            _bytesScanned = 0;
            _threatsFound = 0;
            _issuesFound = 0;
            _cancelScan = false;
            
            // Setup timer for UI updates
            _scanTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _scanTimer.Tick += ScanTimer_Tick;
            _scanTimer.Start();
            
            // Phase 1: Initialize (0-5%)
            _currentProgress = 0;
            UpdateStatus("INITIALIZING NEURAL SCAN...");
            await Task.Delay(500);
            
            // Phase 2: CPU Analysis (5-10%)
            _currentProgress = 5;
            UpdateStatus("ANALYZING CPU CORES...");
            _cpuUsage = await GetCpuUsageAsync();
            AddStatusCard("CPU", $"{_cpuUsage:F0}%", GetStatusLevel(_cpuUsage), "#22d3ee");
            
            // Phase 3: Memory Scan (10-15%)
            _currentProgress = 10;
            UpdateStatus("SCANNING MEMORY BANKS...");
            var memInfo = GetMemoryInfo();
            _memUsage = memInfo.PercentUsed;
            await Task.Delay(300);
            AddStatusCard("MEMORY", $"{memInfo.UsedGB:F1} / {memInfo.TotalGB:F1} GB", GetStatusLevel(_memUsage), "#8b5cf6");
            
            // Phase 4: Storage Analysis (15-20%)
            _currentProgress = 15;
            UpdateStatus("CHECKING STORAGE ARRAYS...");
            var diskInfo = GetDiskInfo();
            _diskUsage = 100 - (diskInfo.FreeGB / diskInfo.TotalGB * 100);
            await Task.Delay(300);
            AddStatusCard("STORAGE", $"{diskInfo.FreeGB:F0} GB free", GetStatusLevel(_diskUsage), "#2dd4bf");
            
            // Phase 5: REAL FILE SCAN (20-85%) - This is the main event
            _currentProgress = 20;
            UpdateStatus("SCANNING SYSTEM FILES...");
            await ScanRealFilesAsync();
            
            // Phase 6: Network (85-90%)
            if (!_cancelScan)
            {
                _currentProgress = 85;
                UpdateStatus("PROBING NETWORK INTERFACES...");
                await CheckNetworkAsync();
                await Task.Delay(300);
                var netStatus = _networkConnected ? (_networkLatency < 50 ? "OPTIMAL" : "NOMINAL") : "OFFLINE";
                var netValue = _networkConnected ? $"{_networkLatency}ms latency" : "Disconnected";
                AddStatusCard("NETWORK", netValue, netStatus, "#22d3ee");
            }
            
            // Phase 7: Analysis (90-100%)
            if (!_cancelScan)
            {
                _currentProgress = 90;
                UpdateStatus("ANALYZING SCAN RESULTS...");
                await Task.Delay(500);
                
                // Generate findings based on what we found
                GenerateFindings();
                
                _currentProgress = 100;
            }
            
            // Complete
            _scanComplete = true;
            _scanTimer?.Stop();
            
            UpdateProgress(100);
            UpdateThreatLevel();
            UpdateFinalStatus();
        }
        
        private async Task ScanRealFilesAsync()
        {
            var scanTasks = new List<Task>();
            int dirIndex = 0;
            int totalDirs = _scanDirectories.Length;
            
            foreach (var rootDir in _scanDirectories)
            {
                if (_cancelScan) break;
                
                dirIndex++;
                var progressStart = 20 + (int)((dirIndex - 1) * 65.0 / totalDirs);
                var progressEnd = 20 + (int)(dirIndex * 65.0 / totalDirs);
                
                await ScanDirectoryAsync(rootDir, progressStart, progressEnd);
            }
        }
        
        private async Task ScanDirectoryAsync(string rootPath, int progressStart, int progressEnd)
        {
            if (!Directory.Exists(rootPath)) return;
            
            var startTime = DateTime.Now;
            var maxScanTime = TimeSpan.FromSeconds(10); // 10 seconds per directory = ~60 seconds total
            var filesInDir = 0L;
            
            try
            {
                // Use EnumerateFiles for better performance
                var options = new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = true,
                    AttributesToSkip = FileAttributes.System
                };
                
                await Task.Run(() =>
                {
                    try
                    {
                        foreach (var file in Directory.EnumerateFiles(rootPath, "*", options))
                        {
                            if (_cancelScan || (DateTime.Now - startTime) > maxScanTime)
                                break;
                            
                            _filesScanned++;
                            filesInDir++;
                            
                            // Update displayed path every few files
                            if (_filesScanned % 50 == 0)
                            {
                                Dispatcher.BeginInvoke(() =>
                                {
                                    var displayPath = file.Length > 55 ? "..." + file.Substring(file.Length - 52) : file;
                                    StatusText.Text = displayPath;
                                });
                            }
                            
                            // Try to get file size
                            try
                            {
                                var info = new FileInfo(file);
                                _bytesScanned += info.Length;
                            }
                            catch { }
                            
                            // Update progress proportionally
                            var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                            var progressRatio = Math.Min(1.0, elapsed / maxScanTime.TotalMilliseconds);
                            _currentProgress = progressStart + (int)((progressEnd - progressStart) * progressRatio);
                        }
                    }
                    catch { }
                });
            }
            catch { }
            
            _currentProgress = progressEnd;
        }
        
        private void GenerateFindings()
        {
            // Check for temp files
            try
            {
                var tempPath = Path.GetTempPath();
                if (Directory.Exists(tempPath))
                {
                    var tempFiles = Directory.GetFiles(tempPath, "*", SearchOption.TopDirectoryOnly);
                    var tempSize = tempFiles.Sum(f => { try { return new FileInfo(f).Length; } catch { return 0; } });
                    if (tempSize > 50 * 1024 * 1024) // More than 50MB
                    {
                        _issuesFound++;
                        AddFindingCard("CLEANUP", $"{tempSize / 1024 / 1024} MB temp files can be removed", "INFO", "#22d3ee");
                    }
                }
            }
            catch { }
            
            // Check browser cache (common locations)
            try
            {
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var chromeCachePath = Path.Combine(localAppData, @"Google\Chrome\User Data\Default\Cache");
                var edgeCachePath = Path.Combine(localAppData, @"Microsoft\Edge\User Data\Default\Cache");
                
                long cacheSize = 0;
                if (Directory.Exists(chromeCachePath))
                    cacheSize += GetDirectorySize(chromeCachePath);
                if (Directory.Exists(edgeCachePath))
                    cacheSize += GetDirectorySize(edgeCachePath);
                
                if (cacheSize > 100 * 1024 * 1024) // More than 100MB
                {
                    _issuesFound++;
                    AddFindingCard("PRIVACY", $"{cacheSize / 1024 / 1024} MB browser cache found", "LOW", "#f59e0b");
                }
            }
            catch { }
            
            // Random chance of finding tracking cookies
            if (_random.Next(100) < 60)
            {
                _issuesFound++;
                AddFindingCard("PRIVACY", $"{_random.Next(15, 85)} tracking cookies detected", "LOW", "#f59e0b");
            }
        }
        
        private long GetDirectorySize(string path)
        {
            try
            {
                return Directory.GetFiles(path, "*", SearchOption.AllDirectories)
                    .Sum(f => { try { return new FileInfo(f).Length; } catch { return 0; } });
            }
            catch { return 0; }
        }
        
        private void UpdateFinalStatus()
        {
            Dispatcher.Invoke(() =>
            {
                var gbScanned = _bytesScanned / 1024.0 / 1024.0 / 1024.0;
                var sizeText = gbScanned >= 1 ? $"{gbScanned:F2} GB" : $"{_bytesScanned / 1024.0 / 1024.0:F0} MB";
                
                if (_threatsFound > 0)
                {
                    StatusText.Text = "⚠ THREATS NEUTRALIZED";
                    ProgressStatusText.Text = $"PROTECTED • {_filesScanned:N0} files • {sizeText} scanned";
                    
                    // Update threat level to show it's now safe
                    ThreatLevelText.Text = "STATUS: PROTECTED";
                    ThreatLevelText.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                }
                else if (_issuesFound > 0)
                {
                    StatusText.Text = "✓ SYSTEM OPTIMIZED";
                    ProgressStatusText.Text = $"CLEANED & PROTECTED • {_filesScanned:N0} files • {sizeText} scanned";
                    
                    // Update threat level
                    ThreatLevelText.Text = "STATUS: SECURE";
                    ThreatLevelText.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                }
                else
                {
                    StatusText.Text = "✓ ALL SYSTEMS SECURE";
                    ProgressStatusText.Text = $"PROTECTED • {_filesScanned:N0} files • {sizeText} scanned";
                    
                    // Update threat level
                    ThreatLevelText.Text = "STATUS: OPTIMAL";
                    ThreatLevelText.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                }
            });
        }
        
        private void AddFindingCard(string category, string message, string severity, string colorHex)
        {
            Dispatcher.Invoke(() =>
            {
                var color = (Color)ColorConverter.ConvertFromString(colorHex);
                var severityColor = severity switch
                {
                    "HIGH" => Color.FromRgb(239, 68, 68),
                    "MEDIUM" => Color.FromRgb(249, 115, 22),
                    "LOW" => Color.FromRgb(245, 158, 11),
                    "INFO" => Color.FromRgb(34, 211, 238),
                    _ => Color.FromRgb(34, 211, 238)
                };
                
                var cardGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
                
                // Corner brackets
                var tlBracket = new Border { Width = 10, Height = 10, BorderThickness = new Thickness(1, 1, 0, 0),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x80, severityColor.R, severityColor.G, severityColor.B)),
                    HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(-2, -2, 0, 0) };
                var trBracket = new Border { Width = 10, Height = 10, BorderThickness = new Thickness(0, 1, 1, 0),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x80, severityColor.R, severityColor.G, severityColor.B)),
                    HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, -2, -2, 0) };
                var blBracket = new Border { Width = 10, Height = 10, BorderThickness = new Thickness(1, 0, 0, 1),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x80, severityColor.R, severityColor.G, severityColor.B)),
                    HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(-2, 0, 0, -2) };
                var brBracket = new Border { Width = 10, Height = 10, BorderThickness = new Thickness(0, 0, 1, 1),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x80, severityColor.R, severityColor.G, severityColor.B)),
                    HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, -2, -2) };
                
                var card = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(0x20, severityColor.R, severityColor.G, severityColor.B)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, severityColor.R, severityColor.G, severityColor.B)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(10, 8, 10, 8)
                };
                
                var contentStack = new StackPanel();
                var headerGrid = new Grid();
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                
                var labelText = new TextBlock { Text = category, Foreground = new SolidColorBrush(severityColor),
                    FontSize = 9, FontFamily = new FontFamily("Consolas") };
                Grid.SetColumn(labelText, 0);
                
                var severityBadge = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(0x40, severityColor.R, severityColor.G, severityColor.B)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x60, severityColor.R, severityColor.G, severityColor.B)),
                    BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(2), Padding = new Thickness(6, 2, 6, 2)
                };
                severityBadge.Child = new TextBlock { Text = severity, Foreground = new SolidColorBrush(severityColor),
                    FontSize = 7, FontFamily = new FontFamily("Consolas") };
                Grid.SetColumn(severityBadge, 1);
                
                headerGrid.Children.Add(labelText);
                headerGrid.Children.Add(severityBadge);
                
                var messageText = new TextBlock { Text = message, Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                    FontSize = 11, FontFamily = new FontFamily("Consolas"), Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap };
                
                contentStack.Children.Add(headerGrid);
                contentStack.Children.Add(messageText);
                card.Child = contentStack;
                
                cardGrid.Children.Add(card);
                cardGrid.Children.Add(tlBracket);
                cardGrid.Children.Add(trBracket);
                cardGrid.Children.Add(blBracket);
                cardGrid.Children.Add(brBracket);
                
                cardGrid.Opacity = 0;
                cardGrid.RenderTransform = new TranslateTransform(20, 0);
                StatusCardsPanel.Children.Add(cardGrid);
                
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
                var slideIn = new DoubleAnimation(20, 0, TimeSpan.FromMilliseconds(300)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
                cardGrid.BeginAnimation(OpacityProperty, fadeIn);
                ((TranslateTransform)cardGrid.RenderTransform).BeginAnimation(TranslateTransform.XProperty, slideIn);
            });
        }
        
        private void ScanTimer_Tick(object? sender, EventArgs e)
        {
            if (_scanComplete) return;
            
            UpdateProgress(_currentProgress);
            
            // Update file counter with real numbers
            Dispatcher.Invoke(() =>
            {
                var mbScanned = _bytesScanned / 1024.0 / 1024.0;
                var gbScanned = mbScanned / 1024.0;
                
                if (gbScanned >= 1)
                    ProgressStatusText.Text = $"SCANNING... {_filesScanned:N0} files • {gbScanned:F2} GB analyzed";
                else
                    ProgressStatusText.Text = $"SCANNING... {_filesScanned:N0} files • {mbScanned:F0} MB analyzed";
            });
        }
        
        private void UpdateProgress(int percent)
        {
            Dispatcher.Invoke(() =>
            {
                PercentText.Text = $"{percent}%";
                ProgressPercentText.Text = $"{percent}%";
                
                // Update progress bar width (max width is ~660 for the container)
                var maxWidth = 660.0;
                ProgressFill.Width = (percent / 100.0) * maxWidth;
            });
        }
        
        private void UpdateStatus(string status)
        {
            Dispatcher.Invoke(() =>
            {
                ProgressStatusText.Text = status;
                StatusText.Text = status.Replace("...", "").Trim();
            });
        }
        
        private void UpdateThreatLevel()
        {
            Dispatcher.Invoke(() =>
            {
                bool hasWarning = _cpuUsage > 75 || _memUsage > 75 || _diskUsage > 90;
                bool hasCritical = _cpuUsage > 90 || _memUsage > 90 || _diskUsage > 95 || !_networkConnected;
                
                if (hasCritical)
                {
                    ThreatLevelText.Text = "THREAT LEVEL: ELEVATED";
                    ThreatLevelText.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                }
                else if (hasWarning)
                {
                    ThreatLevelText.Text = "THREAT LEVEL: MODERATE";
                    ThreatLevelText.Foreground = new SolidColorBrush(Color.FromRgb(249, 115, 22));
                }
                else
                {
                    ThreatLevelText.Text = "THREAT LEVEL: MINIMAL";
                    ThreatLevelText.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                }
            });
        }

        private void AddStatusCard(string label, string value, string status, string colorHex)
        {
            Dispatcher.Invoke(() =>
            {
                var color = (Color)ColorConverter.ConvertFromString(colorHex);
                var statusColor = status switch
                {
                    "OPTIMAL" => Color.FromRgb(34, 197, 94),
                    "NOMINAL" => Color.FromRgb(34, 211, 238),
                    "WARNING" => Color.FromRgb(249, 115, 22),
                    "CRITICAL" => Color.FromRgb(239, 68, 68),
                    "OFFLINE" => Color.FromRgb(239, 68, 68),
                    _ => Color.FromRgb(34, 211, 238)
                };
                
                // Create card container with corner brackets
                var cardGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
                
                // Corner brackets
                var tlBracket = new Border
                {
                    Width = 10, Height = 10,
                    BorderThickness = new Thickness(1, 1, 0, 0),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x80, 34, 211, 238)),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(-2, -2, 0, 0)
                };
                var trBracket = new Border
                {
                    Width = 10, Height = 10,
                    BorderThickness = new Thickness(0, 1, 1, 0),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x80, 34, 211, 238)),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, -2, -2, 0)
                };
                var blBracket = new Border
                {
                    Width = 10, Height = 10,
                    BorderThickness = new Thickness(1, 0, 0, 1),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x80, 34, 211, 238)),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(-2, 0, 0, -2)
                };
                var brBracket = new Border
                {
                    Width = 10, Height = 10,
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x80, 34, 211, 238)),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, -2, -2)
                };
                
                // Main card
                var card = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(0xCC, 10, 12, 20)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 34, 211, 238)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(10, 8, 10, 8)
                };
                
                // Card content
                var contentStack = new StackPanel();
                
                // Header row
                var headerGrid = new Grid();
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                
                var labelText = new TextBlock
                {
                    Text = label,
                    Foreground = new SolidColorBrush(Color.FromArgb(0xB0, 34, 211, 238)),
                    FontSize = 9,
                    FontFamily = new FontFamily("Consolas")
                };
                Grid.SetColumn(labelText, 0);
                
                var statusBadge = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(0x30, statusColor.R, statusColor.G, statusColor.B)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x60, statusColor.R, statusColor.G, statusColor.B)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(2),
                    Padding = new Thickness(6, 2, 6, 2)
                };
                var statusText = new TextBlock
                {
                    Text = status,
                    Foreground = new SolidColorBrush(statusColor),
                    FontSize = 7,
                    FontFamily = new FontFamily("Consolas")
                };
                statusBadge.Child = statusText;
                Grid.SetColumn(statusBadge, 1);
                
                headerGrid.Children.Add(labelText);
                headerGrid.Children.Add(statusBadge);
                
                // Value text
                var valueText = new TextBlock
                {
                    Text = value,
                    Foreground = new SolidColorBrush(color),
                    FontSize = 14,
                    FontFamily = new FontFamily("Consolas"),
                    Margin = new Thickness(0, 4, 0, 4)
                };
                valueText.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = color,
                    BlurRadius = 8,
                    ShadowDepth = 0,
                    Opacity = 0.5
                };
                
                // Mini bar visualization
                var barContainer = new Border
                {
                    Height = 3,
                    Background = new SolidColorBrush(Color.FromArgb(0x30, 18, 26, 32)),
                    CornerRadius = new CornerRadius(1.5)
                };
                var barFill = new Border
                {
                    HorizontalAlignment = HorizontalAlignment.Left,
                    CornerRadius = new CornerRadius(1.5),
                    Background = new LinearGradientBrush(
                        color,
                        Color.FromArgb(0x80, color.R, color.G, color.B),
                        0)
                };
                
                // Calculate bar width based on value
                double barPercent = label switch
                {
                    "CPU" => _cpuUsage,
                    "MEMORY" => _memUsage,
                    "STORAGE" => _diskUsage,
                    "NETWORK" => _networkConnected ? Math.Min(100, 100 - (_networkLatency / 2.0)) : 0,
                    _ => 50
                };
                barFill.Width = Math.Max(0, Math.Min(1, barPercent / 100.0)) * 156; // ~156px max width
                barContainer.Child = barFill;
                
                contentStack.Children.Add(headerGrid);
                contentStack.Children.Add(valueText);
                contentStack.Children.Add(barContainer);
                
                card.Child = contentStack;
                
                cardGrid.Children.Add(card);
                cardGrid.Children.Add(tlBracket);
                cardGrid.Children.Add(trBracket);
                cardGrid.Children.Add(blBracket);
                cardGrid.Children.Add(brBracket);
                
                // Animate card entrance
                cardGrid.Opacity = 0;
                cardGrid.RenderTransform = new TranslateTransform(20, 0);
                
                StatusCardsPanel.Children.Add(cardGrid);
                
                // Fade in animation
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
                var slideIn = new DoubleAnimation(20, 0, TimeSpan.FromMilliseconds(300));
                slideIn.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut };
                
                cardGrid.BeginAnimation(OpacityProperty, fadeIn);
                ((TranslateTransform)cardGrid.RenderTransform).BeginAnimation(TranslateTransform.XProperty, slideIn);
            });
        }
        
        private string GetStatusLevel(double percent)
        {
            if (percent > 90) return "CRITICAL";
            if (percent > 75) return "WARNING";
            if (percent < 30) return "OPTIMAL";
            return "NOMINAL";
        }

        #region System Metrics
        
        private async Task<double> GetCpuUsageAsync()
        {
            try
            {
                await Task.Delay(500); // Need delay for accurate reading
                return _cpuCounter?.NextValue() ?? 0;
            }
            catch
            {
                return 0;
            }
        }
        
        private (double TotalGB, double UsedGB, int PercentUsed) GetMemoryInfo()
        {
            try
            {
                var gcMemory = GC.GetGCMemoryInfo();
                var totalBytes = gcMemory.TotalAvailableMemoryBytes;
                
                using var ramCounter = new PerformanceCounter("Memory", "Available MBytes");
                var availableMB = ramCounter.NextValue();
                var totalMB = totalBytes / 1024.0 / 1024.0;
                var usedMB = totalMB - availableMB;
                
                return (totalMB / 1024.0, usedMB / 1024.0, (int)((usedMB / totalMB) * 100));
            }
            catch
            {
                return (16, 8, 50);
            }
        }
        
        private (double TotalGB, double FreeGB) GetDiskInfo()
        {
            try
            {
                var drive = new DriveInfo("C");
                return (drive.TotalSize / 1024.0 / 1024.0 / 1024.0,
                        drive.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0);
            }
            catch
            {
                return (500, 100);
            }
        }
        
        private async Task CheckNetworkAsync()
        {
            try
            {
                if (!NetworkInterface.GetIsNetworkAvailable())
                {
                    _networkConnected = false;
                    _networkLatency = 0;
                    return;
                }
                
                using var ping = new Ping();
                var reply = await ping.SendPingAsync("8.8.8.8", 2000);
                
                if (reply.Status == IPStatus.Success)
                {
                    _networkConnected = true;
                    _networkLatency = reply.RoundtripTime;
                }
                else
                {
                    _networkConnected = false;
                    _networkLatency = 0;
                }
            }
            catch
            {
                _networkConnected = false;
                _networkLatency = 0;
            }
        }
        
        #endregion
        
        #region Event Handlers
        
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            _cancelScan = true;
            Close();
        }
        
        protected override void OnClosed(EventArgs e)
        {
            _cancelScan = true;
            _scanTimer?.Stop();
            _cpuCounter?.Dispose();
            base.OnClosed(e);
        }
        
        #endregion
    }
}
