using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using AtlasAI.Voice;
using AtlasAI.AI;
using AtlasAI.Conversation.Models;
using NAudio.Wave;
using NAudio.CoreAudioApi;

namespace AtlasAI
{
    public partial class SettingsWindow : Window
    {
        private static readonly string SettingsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AtlasAI");
        private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.txt");
        private static readonly string VoiceKeysPath = Path.Combine(SettingsDir, "voice_keys.json");
        private static readonly string HardwareSettingsPath = Path.Combine(SettingsDir, "hardware_settings.json");
        private static readonly string ProfilePath = Path.Combine(SettingsDir, "user_profile.json");
        private const string StartupKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "AtlasAI";
        
        // Hardware settings
        private string? _selectedMicDeviceId = null; // Device ID for WASAPI
        private int _selectedMicDevice = -1; // Legacy device index (fallback)
        private int _micSensitivity = 120;
        private string _qualityMode = "balanced";
        
        // Audio monitoring
        private WaveInEvent? _monitorWaveIn;
        private bool _isMonitoring = false;
        private double _peakLevel = 0;
        private DispatcherTimer? _levelDecayTimer;
        
        // Flag to prevent saving during initialization
        private bool _isLoadingSettings = true;

        public SettingsWindow()
        {
            InitializeComponent();
            
            // Initialize providers first
            LoadAIProviders();
            LoadVoiceProviders();
            
            // Load voice settings SYNCHRONOUSLY in constructor to ensure it's set before window shows
            LoadVoiceSettingsSync();
            
            // Load settings after window is fully initialized
            Loaded += SettingsWindow_Loaded;
            Closed += SettingsWindow_Closed;
            
            // Subscribe to voice recording events to prevent audio interference
            SubscribeToVoiceRecordingEvents();
            
            // Subscribe to API connection status changes for live updates
            Core.ApiConnectionStatus.Instance.StatusChanged += OnApiConnectionStatusChanged;
            AI.AIManager.ConnectionStatusChanged += OnAIManagerStatusChanged;
        }
        
        private void OnApiConnectionStatusChanged(string provider, Core.ConnectionStatus status)
        {
            // Update UI on dispatcher thread
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // Only update if this is the active provider
                var activeProvider = AI.AIManager.GetActiveProvider().ToString().ToLower();
                if (provider.ToLower() == activeProvider)
                {
                    UpdateApiStatusDisplay();
                }
            }));
        }
        
        private void OnAIManagerStatusChanged(string statusMessage)
        {
            // Update UI on dispatcher thread
            Dispatcher.BeginInvoke(new Action(() =>
            {
                Debug.WriteLine($"[Settings] AI Manager status changed: {statusMessage}");
                UpdateApiStatusDisplay();
            }));
        }
        
        /// <summary>
        /// Load voice settings synchronously to ensure combo is set before window displays
        /// </summary>
        private void LoadVoiceSettingsSync()
        {
            try
            {
                if (File.Exists(VoiceKeysPath))
                {
                    var json = File.ReadAllText(VoiceKeysPath);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    
                    if (root.TryGetProperty("openai", out var openai))
                        OpenAIKeyBox.Password = openai.GetString() ?? "";
                    if (root.TryGetProperty("elevenlabs", out var eleven))
                        ElevenLabsKeyBox.Password = eleven.GetString() ?? "";
                    if (root.TryGetProperty("provider", out var prov))
                    {
                        var provString = prov.GetString();
                        Debug.WriteLine($"[Settings] LoadVoiceSettingsSync: Found provider '{provString}'");
                        if (Enum.TryParse<VoiceProviderType>(provString, out var provType))
                        {
                            for (int i = 0; i < VoiceProviderCombo.Items.Count; i++)
                            {
                                if (VoiceProviderCombo.Items[i] is ComboBoxItem item && 
                                    item.Tag is VoiceProviderType t && t == provType)
                                {
                                    VoiceProviderCombo.SelectedIndex = i;
                                    Debug.WriteLine($"[Settings] LoadVoiceSettingsSync: Set SelectedIndex to {i}");
                                    break;
                                }
                            }
                        }
                    }
                }
                
                // Fallback to first item if nothing selected
                if (VoiceProviderCombo.SelectedIndex < 0 && VoiceProviderCombo.Items.Count > 0)
                {
                    VoiceProviderCombo.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Settings] LoadVoiceSettingsSync error: {ex.Message}");
                if (VoiceProviderCombo.Items.Count > 0)
                    VoiceProviderCombo.SelectedIndex = 0;
            }
        }
        
        private void SettingsWindow_Closed(object? sender, EventArgs e)
        {
            StopAudioMonitor();
            AudioCoordinator.UnregisterMonitor(this);
            
            // Unsubscribe from events
            Core.ApiConnectionStatus.Instance.StatusChanged -= OnApiConnectionStatusChanged;
            AI.AIManager.ConnectionStatusChanged -= OnAIManagerStatusChanged;
        }
        
        // Audio system coordination to prevent interference
        private bool _wasMonitoringBeforeRecording = false;
        
        /// <summary>
        /// Subscribe to voice recording events to coordinate audio systems and prevent interference
        /// </summary>
        private void SubscribeToVoiceRecordingEvents()
        {
            // Register this settings window with the audio coordinator
            AudioCoordinator.RegisterMonitor(this);
        }
        
        /// <summary>
        /// Called when voice recording starts - stop audio monitoring to prevent interference
        /// </summary>
        internal void OnVoiceRecordingStarted()
        {
            if (_isMonitoring)
            {
                Debug.WriteLine("[Settings] Voice recording started - stopping audio monitoring to prevent interference");
                _wasMonitoringBeforeRecording = true;
                StopAudioMonitor();
                
                // Update UI to show why monitoring stopped
                MicStatusText.Text = "🎤 Monitoring paused (voice recording active)";
                MicStatusText.Foreground = Brushes.Orange;
            }
        }
        
        /// <summary>
        /// Called when voice recording stops - restart audio monitoring if it was running before
        /// </summary>
        internal void OnVoiceRecordingStopped()
        {
            if (_wasMonitoringBeforeRecording && !_isMonitoring)
            {
                Debug.WriteLine("[Settings] Voice recording stopped - restarting audio monitoring");
                _wasMonitoringBeforeRecording = false;
                
                // Wait a moment for voice recording to fully stop, then restart monitoring
                Task.Delay(500).ContinueWith(_ =>
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            StartAudioMonitor();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[Settings] Failed to restart audio monitoring: {ex.Message}");
                            MicStatusText.Text = "⚠️ Failed to restart monitoring";
                            MicStatusText.Foreground = Brushes.Red;
                        }
                    }));
                });
            }
        }
        
        private async void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadSettingsAsync();
            LoadHardwareSettings();
            DetectSystemHardware();
            LoadMicrophones();
        }

        private async Task LoadSettingsAsync()
        {
            _isLoadingSettings = true;
            await LoadSettings();
            await LoadAIModelsAsync();
            LoadOrbSettings();
            _isLoadingSettings = false;
        }

        #region Audio Level Monitor
        
        // WASAPI capture for Bluetooth devices
        private WasapiCapture? _monitorWasapi;
        private bool _usingWasapiMonitor = false;
        
        private void StartMonitor_Click(object sender, RoutedEventArgs e)
        {
            StartAudioMonitor();
        }
        
        private void StopMonitor_Click(object sender, RoutedEventArgs e)
        {
            StopAudioMonitor();
        }
        
        private void StartAudioMonitor()
        {
            if (_isMonitoring) return;
            
            // Check if emergency audio protection is active
            if (AudioCoordinator.IsEmergencyProtectionActive)
            {
                MicStatusText.Text = "🛡️ Audio protection active - Monitoring disabled to prevent distortion";
                MicStatusText.Foreground = Brushes.Orange;
                MessageBox.Show(
                    "Emergency Audio Protection is currently active.\n\n" +
                    "Audio monitoring is disabled to prevent headphone distortion.\n\n" +
                    "Disable audio protection in the main chat window to enable monitoring.",
                    "Audio Protection Active",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }
            
            try
            {
                string deviceName = "Windows Default";
                
                // ALWAYS use Windows default capture device via WASAPI - most universal approach
                try
                {
                    using var enumerator = new MMDeviceEnumerator();
                    var defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
                    
                    if (defaultDevice != null && defaultDevice.State == DeviceState.Active)
                    {
                        deviceName = defaultDevice.FriendlyName;
                        Debug.WriteLine($"[Monitor] Using Windows default mic: {deviceName}");
                        
                        // Use WASAPI for the default device - works with ALL device types
                        StartWasapiMonitorForDevice(defaultDevice);
                    }
                    else
                    {
                        throw new Exception("No default capture device available");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Monitor] WASAPI default device failed: {ex.Message}, trying WaveIn fallback");
                    
                    // Fallback to WaveIn device 0 (Windows default)
                    if (WaveIn.DeviceCount > 0)
                    {
                        var caps = WaveIn.GetCapabilities(0);
                        deviceName = caps.ProductName;
                        
                _monitorWaveIn = new WaveInEvent
                {
                    DeviceNumber = 0, // Device 0 = Windows default
                    WaveFormat = new WaveFormat(16000, 16, 1),
                    BufferMilliseconds = 600 // EXTREME: Massive buffer to prevent audio interference (was 300ms)
                };
                        
                        _monitorWaveIn.DataAvailable += MonitorWaveIn_DataAvailable;
                        _monitorWaveIn.RecordingStopped += (s, e) =>
                        {
                            if (e.Exception != null)
                                Debug.WriteLine($"[Monitor] Error: {e.Exception.Message}");
                        };
                        
                        _monitorWaveIn.StartRecording();
                        _usingWasapiMonitor = false;
                    }
                    else
                    {
                        throw new Exception("No microphones available");
                    }
                }
                
                // Start decay timer for peak indicator
                _levelDecayTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(100)
                };
                _levelDecayTimer.Tick += (s, e) =>
                {
                    _peakLevel *= 0.9; // Decay peak
                };
                _levelDecayTimer.Start();
                
                // Start a timer to check if mic is actually producing audio
                _noAudioTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(2)
                };
                _noAudioCheckCount = 0;
                _noAudioTimer.Tick += NoAudioTimer_Tick;
                _noAudioTimer.Start();
                
                _isMonitoring = true;
                _monitorMaxLevel = 0;
                
                StartMonitorBtn.IsEnabled = false;
                StopMonitorBtn.IsEnabled = true;
                MicStatusText.Text = $"🎤 Monitoring: {deviceName}";
                MicStatusText.Foreground = Brushes.LightGreen;
                
                Debug.WriteLine($"[Monitor] Started on: {deviceName} (WASAPI: {_usingWasapiMonitor})");
            }
            catch (Exception ex)
            {
                MicStatusText.Text = $"❌ Monitor failed: {ex.Message}";
                MicStatusText.Foreground = Brushes.Red;
                Debug.WriteLine($"[Monitor] Start failed: {ex.Message}");
            }
        }
        
        private void StartWasapiMonitorForDevice(MMDevice device)
        {
            try
            {
                var deviceName = device.FriendlyName;
                Debug.WriteLine($"[Monitor] Starting WASAPI for: {deviceName}");
                
                // Use EXTREME conservative buffer settings to prevent audio distortion
                try
                {
                    // Use MASSIVE buffer (300ms) to prevent distortion in headphones
                    var bufferDuration = TimeSpan.FromMilliseconds(300);
                    _monitorWasapi = new WasapiCapture(device, true, (int)bufferDuration.TotalMilliseconds); // shared mode with 300ms buffer
                    var format = _monitorWasapi.WaveFormat;
                    Debug.WriteLine($"[Monitor] WASAPI shared with 300ms buffer: {format.SampleRate}Hz, {format.Channels}ch, {format.BitsPerSample}bit, {format.Encoding}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Monitor] WASAPI with custom buffer failed: {ex.Message}, trying default");
                    // Fallback to default buffer
                    _monitorWasapi = new WasapiCapture(device, true); // shared mode with default buffer
                    var format = _monitorWasapi.WaveFormat;
                    Debug.WriteLine($"[Monitor] WASAPI shared with default buffer: {format.SampleRate}Hz, {format.Channels}ch, {format.BitsPerSample}bit, {format.Encoding}");
                }
                
                _monitorWasapi.DataAvailable += MonitorWasapi_DataAvailable;
                _monitorWasapi.RecordingStopped += (s, e) =>
                {
                    if (e.Exception != null)
                        Debug.WriteLine($"[Monitor] WASAPI Error: {e.Exception.Message}");
                };
                
                _monitorWasapi.StartRecording();
                _usingWasapiMonitor = true;
                Debug.WriteLine($"[Monitor] WASAPI started in shared mode (audio-friendly, no distortion)");
                return;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Monitor] WASAPI shared mode failed: {ex.Message}");
                _monitorWasapi?.Dispose();
                _monitorWasapi = null;
                throw new Exception($"WASAPI shared mode capture failed: {ex.Message} (exclusive mode disabled to prevent audio distortion)");
            }
        }
        
        private void StartWasapiMonitor(MMDevice device)
        {
            try
            {
                var deviceName = device.FriendlyName;
                Debug.WriteLine($"[Monitor] Starting WASAPI for: {deviceName}");
                
                // For AirPods/Bluetooth "Headset" mode, we need special handling
                // The Hands-Free profile often has issues with standard WASAPI
                var nameLower = deviceName.ToLower();
                bool isHandsFree = nameLower.Contains("hands-free") || nameLower.Contains("headset");
                
                if (isHandsFree)
                {
                    Debug.WriteLine($"[Monitor] Detected Hands-Free/Headset profile - using conservative buffer settings");
                }
                
                // Use conservative buffer settings to prevent audio distortion
                try
                {
                    // Use larger buffer (100ms) especially for Bluetooth/headset devices
                    var bufferDuration = TimeSpan.FromMilliseconds(isHandsFree ? 150 : 100);
                    _monitorWasapi = new WasapiCapture(device, true, (int)bufferDuration.TotalMilliseconds); // shared mode with conservative buffer
                    var format = _monitorWasapi.WaveFormat;
                    Debug.WriteLine($"[Monitor] WASAPI shared mode with {bufferDuration.TotalMilliseconds}ms buffer: {format.SampleRate}Hz, {format.Channels}ch, {format.BitsPerSample}bit, {format.Encoding}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Monitor] WASAPI with custom buffer failed: {ex.Message}, trying default");
                    // Fallback to default buffer
                    _monitorWasapi = new WasapiCapture(device, true); // shared mode with default buffer
                    var format = _monitorWasapi.WaveFormat;
                    Debug.WriteLine($"[Monitor] WASAPI shared mode with default buffer: {format.SampleRate}Hz, {format.Channels}ch, {format.BitsPerSample}bit, {format.Encoding}");
                }
                
                _monitorWasapi.DataAvailable += MonitorWasapi_DataAvailable;
                _monitorWasapi.RecordingStopped += (s, e) =>
                {
                    if (e.Exception != null)
                        Debug.WriteLine($"[Monitor] WASAPI Error: {e.Exception.Message}");
                };
                
                _monitorWasapi.StartRecording();
                _usingWasapiMonitor = true;
                Debug.WriteLine($"[Monitor] WASAPI started successfully in shared mode (audio-friendly, no distortion)");
                return;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Monitor] WASAPI shared mode failed: {ex.Message}");
                _monitorWasapi?.Dispose();
                _monitorWasapi = null;
                throw new Exception($"WASAPI shared mode capture failed: {ex.Message} (exclusive mode disabled to prevent audio distortion)");
            }
        }
        
        private void MonitorWasapi_DataAvailable(object? sender, WaveInEventArgs e)
        {
            if (_monitorWasapi == null) return;
            
            var format = _monitorWasapi.WaveFormat;
            double level = 0;
            double max = 0;
            int sampleCount = 0;
            
            // Handle IEEE float format (common with WASAPI/Bluetooth)
            if (format.Encoding == WaveFormatEncoding.IeeeFloat && format.BitsPerSample == 32)
            {
                for (int i = 0; i < e.BytesRecorded; i += 4 * format.Channels)
                {
                    if (i + 3 < e.BytesRecorded)
                    {
                        float sample = BitConverter.ToSingle(e.Buffer, i);
                        double abs = Math.Abs(sample) * 32767; // Scale to 16-bit range
                        level += abs;
                        if (abs > max) max = abs;
                        sampleCount++;
                    }
                }
            }
            else if (format.BitsPerSample == 16)
            {
                // 16-bit PCM
                for (int i = 0; i < e.BytesRecorded; i += 2 * format.Channels)
                {
                    if (i + 1 < e.BytesRecorded)
                    {
                        short sample = (short)(e.Buffer[i] | (e.Buffer[i + 1] << 8));
                        double abs = Math.Abs(sample);
                        level += abs;
                        if (abs > max) max = abs;
                        sampleCount++;
                    }
                }
            }
            else if (format.BitsPerSample == 8)
            {
                // 8-bit PCM (common with Bluetooth HFP/Hands-Free)
                for (int i = 0; i < e.BytesRecorded; i += format.Channels)
                {
                    byte sample = e.Buffer[i];
                    double abs = Math.Abs(sample - 128) * 256; // Convert to 16-bit range
                    level += abs;
                    if (abs > max) max = abs;
                    sampleCount++;
                }
            }
            else
            {
                // Unknown format - try to read as bytes
                Debug.WriteLine($"[Monitor] Unknown format: {format.BitsPerSample}bit, {format.Encoding}");
                for (int i = 0; i < e.BytesRecorded; i++)
                {
                    double abs = Math.Abs(e.Buffer[i] - 128) * 256;
                    level += abs;
                    if (abs > max) max = abs;
                    sampleCount++;
                }
            }
            
            double avgLevel = sampleCount > 0 ? level / sampleCount : 0;
            
            // Normalize to 0-100 scale
            double normalizedLevel = Math.Min(100, (avgLevel / 32767.0) * 500);
            double normalizedPeak = Math.Min(100, (max / 32767.0) * 200);
            
            // Track max level for no-audio detection
            if (normalizedLevel > _monitorMaxLevel)
                _monitorMaxLevel = normalizedLevel;
            
            // Update peak
            if (normalizedPeak > _peakLevel)
                _peakLevel = normalizedPeak;
            
            // Update UI
            UpdateMonitorUI(normalizedLevel, normalizedPeak);
        }
        
        private DispatcherTimer? _noAudioTimer;
        private int _noAudioCheckCount = 0;
        private double _monitorMaxLevel = 0;
        
        private void NoAudioTimer_Tick(object? sender, EventArgs e)
        {
            _noAudioCheckCount++;
            
            // If we've been monitoring for 2+ seconds with no audio, warn user
            if (_monitorMaxLevel < 5 && _noAudioCheckCount >= 1)
            {
                MicStatusText.Text = "⚠️ No audio detected! Try Auto-Scan or select different mic";
                MicStatusText.Foreground = Brushes.Orange;
            }
            
            // Stop checking after first check
            _noAudioTimer?.Stop();
        }
        
        private int FindFirstWorkingMic()
        {
            // Try to find a mic that actually produces audio
            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                var caps = WaveIn.GetCapabilities(i);
                var name = caps.ProductName.ToLower();
                
                // Skip obviously bad devices
                if (name.Contains("stereo mix") || name.Contains("what u hear") || 
                    name.Contains("loopback") || name.Contains("digital"))
                    continue;
                
                // Prefer real microphones
                if (name.Contains("microphone") || name.Contains("mic") || 
                    name.Contains("usb") || name.Contains("sound blaster"))
                {
                    return i;
                }
            }
            
            // Fallback to first non-loopback device
            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                var caps = WaveIn.GetCapabilities(i);
                var name = caps.ProductName.ToLower();
                if (!name.Contains("stereo mix") && !name.Contains("what u hear"))
                    return i;
            }
            
            return 0;
        }
        
        private void StopAudioMonitor()
        {
            if (!_isMonitoring) return;
            
            try
            {
                _levelDecayTimer?.Stop();
                _levelDecayTimer = null;
                
                _noAudioTimer?.Stop();
                _noAudioTimer = null;
                
                // Stop WASAPI monitor if active
                if (_monitorWasapi != null)
                {
                    _monitorWasapi.StopRecording();
                    _monitorWasapi.Dispose();
                    _monitorWasapi = null;
                }
                
                // Stop WaveIn monitor if active
                _monitorWaveIn?.StopRecording();
                _monitorWaveIn?.Dispose();
                _monitorWaveIn = null;
                
                _isMonitoring = false;
                _usingWasapiMonitor = false;
                
                StartMonitorBtn.IsEnabled = true;
                StopMonitorBtn.IsEnabled = false;
                
                // Reset visualizer
                AudioLevelBar.Width = 0;
                AudioPeakIndicator.Visibility = Visibility.Collapsed;
                AudioLevelText.Text = "0";
                
                MicStatusText.Text = "🎤 Monitor stopped";
                MicStatusText.Foreground = Brushes.Orange;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Monitor] Stop error: {ex.Message}");
            }
        }
        
        private async void AutoScan_Click(object sender, RoutedEventArgs e)
        {
            // Stop any current monitoring
            StopAudioMonitor();
            
            AutoScanBtn.IsEnabled = false;
            AutoScanBtn.Content = "Scanning...";
            MicStatusText.Text = "🔍 Scanning all microphones...";
            MicStatusText.Foreground = Brushes.Yellow;
            
            await Task.Run(() =>
            {
                var results = WhisperSpeechRecognition.GetWorkingMicrophones();
                var workingMics = results.Where(r => r.Working).OrderByDescending(r => r.Level).ToList();
                
                Dispatcher.Invoke(() =>
                {
                    if (workingMics.Count > 0)
                    {
                        var best = workingMics.First();
                        MicStatusText.Text = $"✓ Found {workingMics.Count} working mic(s)! Best: {best.Name}";
                        MicStatusText.Foreground = Brushes.LightGreen;
                        
                        // Find and select this mic in the dropdown
                        // First, rebuild the dropdown with WaveIn indices for reliability
                        RebuildMicDropdownWithWaveIn();
                        
                        // Select the best working mic
                        for (int i = 0; i < MicrophoneCombo.Items.Count; i++)
                        {
                            if (MicrophoneCombo.Items[i] is ComboBoxItem comboItem)
                            {
                                var content = comboItem.Content?.ToString() ?? "";
                                if (content.Contains(best.Name))
                                {
                                    MicrophoneCombo.SelectedIndex = i;
                                    _selectedMicDevice = best.Index;
                                    break;
                                }
                            }
                        }
                        
                        // Auto-start monitoring with the working mic
                        StartAudioMonitor();
                    }
                    else
                    {
                        MicStatusText.Text = "❌ No working microphones found! Check connections.";
                        MicStatusText.Foreground = Brushes.Red;
                    }
                    
                    AutoScanBtn.IsEnabled = true;
                    AutoScanBtn.Content = "🔍 Auto-Scan";
                });
            });
        }
        
        private void RebuildMicDropdownWithWaveIn()
        {
            // Rebuild dropdown using WaveIn indices for more reliable device selection
            MicrophoneCombo.Items.Clear();
            
            MicrophoneCombo.Items.Add(new ComboBoxItem
            {
                Content = "🔄 Auto-detect (Recommended)",
                Tag = "auto"
            });
            
            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                var caps = WaveIn.GetCapabilities(i);
                var name = caps.ProductName;
                
                var icon = "🎤";
                var nameLower = name.ToLower();
                if (nameLower.Contains("airpod") || nameLower.Contains("bluetooth") || nameLower.Contains("wireless"))
                    icon = "🎧";
                else if (nameLower.Contains("headset") || nameLower.Contains("headphone"))
                    icon = "🎧";
                else if (nameLower.Contains("usb"))
                    icon = "🎙️";
                else if (nameLower.Contains("webcam") || nameLower.Contains("camera"))
                    icon = "📷";
                
                MicrophoneCombo.Items.Add(new ComboBoxItem
                {
                    Content = $"{icon} {name}",
                    Tag = i.ToString() // Store WaveIn index as string
                });
            }
        }
        
        private void MonitorWaveIn_DataAvailable(object? sender, WaveInEventArgs e)
        {
            // Calculate RMS level
            double sum = 0;
            double max = 0;
            for (int i = 0; i < e.BytesRecorded; i += 2)
            {
                if (i + 1 < e.BytesRecorded)
                {
                    short sample = (short)(e.Buffer[i] | (e.Buffer[i + 1] << 8));
                    double abs = Math.Abs(sample);
                    sum += abs;
                    if (abs > max) max = abs;
                }
            }
            double avgLevel = sum / Math.Max(1, e.BytesRecorded / 2);
            
            // Normalize to 0-100 scale (32767 is max for 16-bit)
            double normalizedLevel = Math.Min(100, (avgLevel / 32767.0) * 500); // Amplify for visibility
            double normalizedPeak = Math.Min(100, (max / 32767.0) * 200);
            
            // Track max level for no-audio detection
            if (normalizedLevel > _monitorMaxLevel)
                _monitorMaxLevel = normalizedLevel;
            
            // Update peak
            if (normalizedPeak > _peakLevel)
                _peakLevel = normalizedPeak;
            
            // Update UI
            UpdateMonitorUI(normalizedLevel, normalizedPeak);
        }
        
        private void UpdateMonitorUI(double normalizedLevel, double normalizedPeak)
        {
            // Update UI on dispatcher thread
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    // Get the parent border width for scaling
                    var parentBorder = AudioLevelBar.Parent as Grid;
                    double maxWidth = parentBorder?.ActualWidth ?? 200;
                    if (maxWidth <= 0) maxWidth = 200;
                    
                    // Update level bar
                    double barWidth = (normalizedLevel / 100.0) * maxWidth;
                    AudioLevelBar.Width = Math.Max(0, Math.Min(maxWidth, barWidth));
                    
                    // Color based on level
                    if (normalizedLevel > 70)
                        AudioLevelBar.Background = Brushes.Red;
                    else if (normalizedLevel > 40)
                        AudioLevelBar.Background = Brushes.Orange;
                    else if (normalizedLevel > 10)
                        AudioLevelBar.Background = new SolidColorBrush(Color.FromRgb(0, 170, 0));
                    else
                        AudioLevelBar.Background = new SolidColorBrush(Color.FromRgb(0, 100, 0));
                    
                    // Update peak indicator
                    if (_peakLevel > 5)
                    {
                        AudioPeakIndicator.Visibility = Visibility.Visible;
                        double peakPos = (_peakLevel / 100.0) * maxWidth;
                        AudioPeakIndicator.Margin = new Thickness(Math.Max(0, peakPos - 1.5), 2, 0, 2);
                    }
                    else
                    {
                        AudioPeakIndicator.Visibility = Visibility.Collapsed;
                    }
                    
                    // Update text
                    AudioLevelText.Text = $"{normalizedLevel:F0}";
                    
                    // Update status if we detect sound
                    if (normalizedLevel > 10)
                    {
                        MicStatusText.Text = "🎤 ✓ Audio detected!";
                        MicStatusText.Foreground = Brushes.LightGreen;
                    }
                }
                catch { }
            }));
        }
        
        private int GetSelectedDeviceIndex()
        {
            if (MicrophoneCombo.SelectedItem is ComboBoxItem item)
            {
                if (item.Tag is string tagStr)
                {
                    if (tagStr == "auto") return -1;
                    
                    // Try to parse as WaveIn index first
                    if (int.TryParse(tagStr, out int idx)) 
                    {
                        if (idx >= 0 && idx < WaveIn.DeviceCount)
                            return idx;
                    }
                    
                    // It's a WASAPI device ID - try to find matching WaveIn device by name
                    try
                    {
                        using var enumerator = new MMDeviceEnumerator();
                        var mmDevice = enumerator.GetDevice(tagStr);
                        if (mmDevice != null)
                        {
                            var mmName = mmDevice.FriendlyName.ToLower();
                            Debug.WriteLine($"[Settings] GetSelectedDeviceIndex looking for: {mmDevice.FriendlyName}");
                            
                            // Find WaveIn device with similar name
                            for (int i = 0; i < WaveIn.DeviceCount; i++)
                            {
                                var caps = WaveIn.GetCapabilities(i);
                                var waveInName = caps.ProductName.ToLower();
                                
                                Debug.WriteLine($"[Settings] Comparing WASAPI '{mmName}' with WaveIn '{waveInName}'");
                                
                                // WaveIn names are truncated, check for partial match
                                if (mmName.Contains(waveInName) || 
                                    waveInName.Contains(mmName.Substring(0, Math.Min(15, mmName.Length))) ||
                                    (mmName.Contains("usbaudio") && waveInName.Contains("usbaudio")) ||
                                    (mmName.Contains("sound blaster") && waveInName.Contains("sound blaster")) ||
                                    (mmName.Contains("airpod") && waveInName.Contains("airpod")) ||
                                    (mmName.Contains("realtek") && waveInName.Contains("realtek")) ||
                                    // Match headset devices (AirPods shows as "Headset")
                                    (mmName.Contains("headset") && waveInName.Contains("headset")) ||
                                    // Match hands-free devices
                                    (mmName.Contains("hands-free") && waveInName.Contains("hands-free")))
                                {
                                    Debug.WriteLine($"[Settings] Matched to WaveIn device {i}: {caps.ProductName}");
                                    return i;
                                }
                            }
                            
                            // No match - use Windows default
                            Debug.WriteLine($"[Settings] No WaveIn match for '{mmDevice.FriendlyName}', using default device 0");
                            return 0;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Settings] Error in GetSelectedDeviceIndex: {ex.Message}");
                    }
                }
                else if (item.Tag is int idx)
                {
                    return idx;
                }
            }
            return -1;
        }
        
        #endregion

        #region Hardware Detection
        
        private void DetectSystemHardware()
        {
            try
            {
                var cpuName = "Unknown CPU";
                var ramGB = 0;
                var gpuName = "Unknown GPU";
                
                // Get CPU info
                try
                {
                    using var searcher = new ManagementObjectSearcher("select Name from Win32_Processor");
                    foreach (var item in searcher.Get())
                    {
                        cpuName = item["Name"]?.ToString() ?? "Unknown";
                        break;
                    }
                }
                catch { }
                
                // Get RAM
                try
                {
                    using var searcher = new ManagementObjectSearcher("select TotalPhysicalMemory from Win32_ComputerSystem");
                    foreach (var item in searcher.Get())
                    {
                        var bytes = Convert.ToInt64(item["TotalPhysicalMemory"]);
                        ramGB = (int)(bytes / 1024 / 1024 / 1024);
                        break;
                    }
                }
                catch { }
                
                // Get GPU
                try
                {
                    using var searcher = new ManagementObjectSearcher("select Name from Win32_VideoController");
                    foreach (var item in searcher.Get())
                    {
                        gpuName = item["Name"]?.ToString() ?? "Unknown";
                        break;
                    }
                }
                catch { }
                
                // Update UI
                HardwareInfoText.Text = $"🖥️ {cpuName}\n💾 {ramGB} GB RAM | 🎮 {gpuName}";
                
                // Auto-select quality mode based on hardware
                if (string.IsNullOrEmpty(_qualityMode) || _qualityMode == "auto")
                {
                    if (ramGB >= 16)
                        _qualityMode = "high";
                    else if (ramGB >= 8)
                        _qualityMode = "balanced";
                    else
                        _qualityMode = "low";
                    
                    SelectQualityMode(_qualityMode);
                }
            }
            catch (Exception ex)
            {
                HardwareInfoText.Text = $"🖥️ Could not detect hardware: {ex.Message}";
            }
        }
        
        private void LoadMicrophones()
        {
            MicrophoneCombo.Items.Clear();
            
            // Add auto-detect option
            MicrophoneCombo.Items.Add(new ComboBoxItem
            {
                Content = "🔄 Auto-detect (Recommended)",
                Tag = "auto"
            });
            
            // Use Core Audio API (WASAPI) for better Bluetooth/AirPods support
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
                
                if (devices.Count == 0)
                {
                    MicStatusText.Text = "🎤 No microphone detected!";
                    MicStatusText.Foreground = System.Windows.Media.Brushes.Red;
                    MicrophoneCombo.SelectedIndex = 0;
                    return;
                }
                
                foreach (var device in devices)
                {
                    var name = device.FriendlyName;
                    var id = device.ID;
                    
                    // Add icon based on device type
                    var icon = "🎤";
                    var nameLower = name.ToLower();
                    if (nameLower.Contains("airpod") || nameLower.Contains("bluetooth") || nameLower.Contains("wireless"))
                        icon = "🎧";
                    else if (nameLower.Contains("headset") || nameLower.Contains("headphone") || nameLower.Contains("hands-free"))
                        icon = "🎧";
                    else if (nameLower.Contains("usb"))
                        icon = "🎙️";
                    else if (nameLower.Contains("webcam") || nameLower.Contains("camera"))
                        icon = "📷";
                    
                    MicrophoneCombo.Items.Add(new ComboBoxItem
                    {
                        Content = $"{icon} {name}",
                        Tag = id // Store device ID instead of index
                    });
                    
                    Debug.WriteLine($"[Settings] Found mic: {name} (ID: {id})");
                }
                
                // Select saved device or auto
                if (!string.IsNullOrEmpty(_selectedMicDeviceId))
                {
                    for (int i = 1; i < MicrophoneCombo.Items.Count; i++)
                    {
                        if (MicrophoneCombo.Items[i] is ComboBoxItem item && item.Tag as string == _selectedMicDeviceId)
                        {
                            MicrophoneCombo.SelectedIndex = i;
                            break;
                        }
                    }
                }
                
                if (MicrophoneCombo.SelectedIndex < 0)
                    MicrophoneCombo.SelectedIndex = 0; // Auto
                
                // Update status with best device
                var bestDevice = GetBestMicDevice();
                if (bestDevice != null)
                {
                    MicStatusText.Text = $"🎤 Active: {bestDevice.FriendlyName}";
                    MicStatusText.Foreground = System.Windows.Media.Brushes.LightGreen;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Settings] MMDevice enumeration failed: {ex.Message}");
                MicStatusText.Text = $"⚠️ Error detecting mics: {ex.Message}";
                MicStatusText.Foreground = System.Windows.Media.Brushes.Orange;
                
                // Fallback to legacy WaveIn
                LoadMicrophonesLegacy();
            }
        }
        
        // Fallback method using legacy WaveIn API
        private void LoadMicrophonesLegacy()
        {
            var deviceCount = WaveIn.DeviceCount;
            if (deviceCount == 0)
            {
                MicStatusText.Text = "🎤 No microphone detected!";
                MicStatusText.Foreground = System.Windows.Media.Brushes.Red;
                MicrophoneCombo.SelectedIndex = 0;
                return;
            }
            
            for (int i = 0; i < deviceCount; i++)
            {
                var caps = WaveIn.GetCapabilities(i);
                var name = caps.ProductName;
                
                var icon = "🎤";
                var nameLower = name.ToLower();
                if (nameLower.Contains("airpod") || nameLower.Contains("bluetooth") || nameLower.Contains("wireless"))
                    icon = "🎧";
                else if (nameLower.Contains("headset") || nameLower.Contains("headphone"))
                    icon = "🎧";
                else if (nameLower.Contains("usb"))
                    icon = "🎙️";
                else if (nameLower.Contains("webcam") || nameLower.Contains("camera"))
                    icon = "📷";
                
                MicrophoneCombo.Items.Add(new ComboBoxItem
                {
                    Content = $"{icon} {name}",
                    Tag = i.ToString() // Store index as string for legacy
                });
            }
            
            MicrophoneCombo.SelectedIndex = 0;
        }
        
        private MMDevice? GetBestMicDevice()
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                
                // If specific device ID is set, use it
                if (!string.IsNullOrEmpty(_selectedMicDeviceId) && _selectedMicDeviceId != "auto")
                {
                    try
                    {
                        var device = enumerator.GetDevice(_selectedMicDeviceId);
                        if (device != null && device.State == DeviceState.Active)
                            return device;
                    }
                    catch { }
                }
                
                // Auto-detect best device (prefer Bluetooth/AirPods)
                var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
                MMDevice? bestDevice = null;
                int bestScore = -1000;
                
                foreach (var device in devices)
                {
                    var name = device.FriendlyName.ToLower();
                    int score = 0;
                    
                    if (name.Contains("airpod") || name.Contains("bluetooth") || name.Contains("wireless"))
                        score += 100;
                    if (name.Contains("headset") || name.Contains("headphone") || name.Contains("hands-free"))
                        score += 50;
                    if (name.Contains("usb"))
                        score += 30;
                    if (name.Contains("stereo mix") || name.Contains("loopback"))
                        score -= 100;
                    
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestDevice = device;
                    }
                }
                
                return bestDevice;
            }
            catch
            {
                return null;
            }
        }
        
        private void RefreshMic_Click(object sender, RoutedEventArgs e)
        {
            LoadMicrophones();
        }
        
        private async void TestMic_Click(object sender, RoutedEventArgs e)
        {
            TestMicBtn.IsEnabled = false;
            TestMicBtn.Content = "Testing...";
            MicStatusText.Text = "🎤 Testing microphone...";
            MicStatusText.Foreground = System.Windows.Media.Brushes.Yellow;
            
            // Check if we have a WASAPI device ID selected (for Bluetooth/AirPods)
            string? deviceId = null;
            if (MicrophoneCombo.SelectedItem is ComboBoxItem item && item.Tag is string tagStr && tagStr != "auto")
            {
                // Check if it's a WASAPI device ID (not a numeric index)
                if (!int.TryParse(tagStr, out _))
                {
                    deviceId = tagStr;
                }
            }
            
            // If we have a device ID, test using WASAPI directly
            if (!string.IsNullOrEmpty(deviceId))
            {
                Debug.WriteLine($"[Settings] Testing WASAPI device: {deviceId}");
                
                await Task.Run(() =>
                {
                    var (working, level, message) = WhisperSpeechRecognition.TestMicrophoneByDeviceId(deviceId);
                    
                    Dispatcher.Invoke(() =>
                    {
                        if (working)
                        {
                            MicStatusText.Text = $"✓ Mic working! Level: {level:F0}";
                            MicStatusText.Foreground = System.Windows.Media.Brushes.LightGreen;
                        }
                        else
                        {
                            MicStatusText.Text = $"✗ Mic NOT responding! Level: {level:F0}";
                            MicStatusText.Foreground = System.Windows.Media.Brushes.Red;
                        }
                        
                        TestMicBtn.IsEnabled = true;
                        TestMicBtn.Content = "🎤 Test";
                    });
                });
                return;
            }
            
            // Use WaveIn device index
            int deviceIndex = GetSelectedDeviceIndex();
            
            // If auto or invalid, test all mics
            if (deviceIndex < 0 || deviceIndex >= WaveIn.DeviceCount)
            {
                await TestAllMicrophonesAsync();
                return;
            }
            
            // Test the specific mic
            string deviceName = "Unknown";
            if (deviceIndex >= 0 && deviceIndex < WaveIn.DeviceCount)
            {
                var caps = WaveIn.GetCapabilities(deviceIndex);
                deviceName = caps.ProductName;
            }
            Debug.WriteLine($"[Settings] Testing device {deviceIndex}: {deviceName}");
            
            await Task.Run(() =>
            {
                var (working, level, message) = WhisperSpeechRecognition.TestMicrophone(deviceIndex);
                
                Dispatcher.Invoke(() =>
                {
                    if (working)
                    {
                        MicStatusText.Text = $"✓ Mic working! Level: {level:F0}";
                        MicStatusText.Foreground = System.Windows.Media.Brushes.LightGreen;
                    }
                    else
                    {
                        MicStatusText.Text = $"✗ Mic NOT responding! Level: {level:F0}";
                        MicStatusText.Foreground = System.Windows.Media.Brushes.Red;
                    }
                    
                    TestMicBtn.IsEnabled = true;
                    TestMicBtn.Content = "🎤 Test";
                });
            });
        }
        
        private async Task TestAllMicrophonesAsync()
        {
            TestMicBtn.IsEnabled = false;
            TestMicBtn.Content = "Testing...";
            MicStatusText.Text = "🎤 Testing all microphones...";
            MicStatusText.Foreground = System.Windows.Media.Brushes.Yellow;
            
            await Task.Run(() =>
            {
                var results = WhisperSpeechRecognition.GetWorkingMicrophones();
                var workingMics = results.Where(r => r.Working).ToList();
                
                Dispatcher.Invoke(() =>
                {
                    if (workingMics.Count > 0)
                    {
                        var best = workingMics.OrderByDescending(m => m.Level).First();
                        MicStatusText.Text = $"✓ Found {workingMics.Count} working mic(s). Best: {best.Name}";
                        MicStatusText.Foreground = System.Windows.Media.Brushes.LightGreen;
                        
                        // Auto-select the best working mic
                        for (int i = 0; i < MicrophoneCombo.Items.Count; i++)
                        {
                            if (MicrophoneCombo.Items[i] is ComboBoxItem comboItem)
                            {
                                var content = comboItem.Content?.ToString() ?? "";
                                if (content.Contains(best.Name))
                                {
                                    MicrophoneCombo.SelectedIndex = i;
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        MicStatusText.Text = "✗ No working microphones found!";
                        MicStatusText.Foreground = System.Windows.Media.Brushes.Red;
                    }
                    
                    TestMicBtn.IsEnabled = true;
                    TestMicBtn.Content = "🎤 Test";
                });
            });
        }
        
        private void Microphone_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (MicrophoneCombo.SelectedItem is ComboBoxItem item && item.Tag is string deviceTag)
            {
                if (deviceTag == "auto")
                {
                    _selectedMicDeviceId = null;
                    _selectedMicDevice = -1;
                    
                    var bestDevice = GetBestMicDevice();
                    if (bestDevice != null)
                    {
                        MicStatusText.Text = $"🎤 Auto-selected: {bestDevice.FriendlyName}";
                        MicStatusText.Foreground = System.Windows.Media.Brushes.LightGreen;
                    }
                }
                else
                {
                    _selectedMicDeviceId = deviceTag;
                    
                    // Try to get device name for status
                    try
                    {
                        using var enumerator = new MMDeviceEnumerator();
                        var device = enumerator.GetDevice(deviceTag);
                        if (device != null)
                        {
                            MicStatusText.Text = $"🎤 Selected: {device.FriendlyName}";
                            MicStatusText.Foreground = System.Windows.Media.Brushes.LightGreen;
                        }
                    }
                    catch
                    {
                        MicStatusText.Text = $"🎤 Selected device";
                        MicStatusText.Foreground = System.Windows.Media.Brushes.LightGreen;
                    }
                }
            }
            // Legacy fallback for int tags
            else if (MicrophoneCombo.SelectedItem is ComboBoxItem legacyItem && legacyItem.Tag is int deviceIndex)
            {
                _selectedMicDevice = deviceIndex;
                _selectedMicDeviceId = null;
                
                if (deviceIndex == -1)
                {
                    var bestDevice = GetBestMicDevice();
                    if (bestDevice != null)
                    {
                        MicStatusText.Text = $"🎤 Auto-selected: {bestDevice.FriendlyName}";
                        MicStatusText.Foreground = System.Windows.Media.Brushes.LightGreen;
                    }
                }
                else if (deviceIndex >= 0 && deviceIndex < WaveIn.DeviceCount)
                {
                    var caps = WaveIn.GetCapabilities(deviceIndex);
                    MicStatusText.Text = $"🎤 Selected: {caps.ProductName}";
                    MicStatusText.Foreground = System.Windows.Media.Brushes.LightGreen;
                }
            }
        }
        
        private void MicSensitivity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _micSensitivity = (int)MicSensitivitySlider.Value;
            if (MicSensitivityValue != null)
                MicSensitivityValue.Text = _micSensitivity.ToString();
        }
        
        private void QualityMode_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingSettings) return; // Prevent crash during initialization
            if (QualityModeCombo?.SelectedItem is ComboBoxItem item && item.Tag is string mode)
            {
                _qualityMode = mode;
                UpdatePerformanceInfo(mode);
            }
        }
        
        private void SelectQualityMode(string mode)
        {
            for (int i = 0; i < QualityModeCombo.Items.Count; i++)
            {
                if (QualityModeCombo.Items[i] is ComboBoxItem item && item.Tag as string == mode)
                {
                    QualityModeCombo.SelectedIndex = i;
                    break;
                }
            }
        }
        
        private void UpdatePerformanceInfo(string mode)
        {
            if (PerformanceInfoText == null) return; // Null check
            switch (mode)
            {
                case "low":
                    PerformanceInfoText.Text = "🐢 Battery Saver: Longer response times, lower CPU usage. Good for laptops on battery.";
                    break;
                case "balanced":
                    PerformanceInfoText.Text = "⚖️ Balanced: Good response times with moderate CPU usage. Recommended for most systems.";
                    break;
                case "high":
                    PerformanceInfoText.Text = "🚀 Performance: Fastest response times, higher CPU usage. Best for desktop PCs with good specs.";
                    break;
            }
        }
        
        private void LoadHardwareSettings()
        {
            try
            {
                if (File.Exists(HardwareSettingsPath))
                {
                    var json = File.ReadAllText(HardwareSettingsPath);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    
                    // Load device ID (new format)
                    if (root.TryGetProperty("micDeviceId", out var micId))
                        _selectedMicDeviceId = micId.GetString();
                    
                    // Legacy: load device index
                    if (root.TryGetProperty("micDevice", out var mic))
                        _selectedMicDevice = mic.GetInt32();
                        
                    if (root.TryGetProperty("micSensitivity", out var sens))
                    {
                        _micSensitivity = sens.GetInt32();
                        MicSensitivitySlider.Value = _micSensitivity;
                        MicSensitivityValue.Text = _micSensitivity.ToString();
                    }
                    if (root.TryGetProperty("qualityMode", out var quality))
                    {
                        _qualityMode = quality.GetString() ?? "balanced";
                        SelectQualityMode(_qualityMode);
                    }
                    else
                    {
                        // Default to balanced if not in settings
                        SelectQualityMode("balanced");
                    }
                }
                else
                {
                    // No settings file - set default quality mode
                    SelectQualityMode("balanced");
                }
            }
            catch { }
        }
        
        private void SaveHardwareSettings()
        {
            try
            {
                var settings = new Dictionary<string, object>
                {
                    ["micDeviceId"] = _selectedMicDeviceId ?? "",
                    ["micDevice"] = _selectedMicDevice, // Keep for backward compatibility
                    ["micSensitivity"] = _micSensitivity,
                    ["qualityMode"] = _qualityMode
                };
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(HardwareSettingsPath, json);
            }
            catch { }
        }
        
        #endregion

        #region AI Settings
        
        private async Task LoadAIModelsAsync()
        {
            try
            {
                var provider = AIManager.GetActiveProviderInstance();
                var models = new List<AIModel>();
                if (provider != null)
                    models = await provider.GetModelsAsync();
                
                AIModelComboBox.Items.Clear();
                foreach (var model in models)
                {
                    var item = new ComboBoxItem
                    {
                        Content = $"{model.DisplayName} - {model.Description}",
                        Tag = model.Id
                    };
                    AIModelComboBox.Items.Add(item);
                }
                
                var selectedModel = AIManager.GetSelectedModel();
                for (int i = 0; i < AIModelComboBox.Items.Count; i++)
                {
                    if (AIModelComboBox.Items[i] is ComboBoxItem item && 
                        item.Tag as string == selectedModel)
                    {
                        AIModelComboBox.SelectedIndex = i;
                        break;
                    }
                }
                
                if (AIModelComboBox.SelectedIndex < 0 && AIModelComboBox.Items.Count > 0)
                    AIModelComboBox.SelectedIndex = 0;
            }
            catch { }
        }

        private void LoadAIProviders()
        {
            AIProviderComboBox.Items.Clear();
            
            var providers = AIManager.GetAllProviders();
            foreach (var provider in providers)
            {
                var item = new ComboBoxItem
                {
                    Content = provider.DisplayName,
                    Tag = provider.ProviderType
                };
                AIProviderComboBox.Items.Add(item);
            }
            
            var activeProvider = AIManager.GetActiveProvider();
            for (int i = 0; i < AIProviderComboBox.Items.Count; i++)
            {
                if (AIProviderComboBox.Items[i] is ComboBoxItem item && 
                    item.Tag is AIProviderType type && type == activeProvider)
                {
                    AIProviderComboBox.SelectedIndex = i;
                    break;
                }
            }
            
            if (AIProviderComboBox.SelectedIndex < 0 && AIProviderComboBox.Items.Count > 0)
                AIProviderComboBox.SelectedIndex = 0;
            
            // Load API key and status for the selected provider
            if (activeProvider != default)
            {
                LoadApiKeyForProvider(activeProvider);
                UpdateApiStatusDisplay();
            }
        }

        private async void AIProviderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AIProviderComboBox.SelectedItem is ComboBoxItem item && item.Tag is AIProviderType type)
            {
                await AIManager.SetActiveProviderAsync(type);
                await LoadAIModelsAsync();
                
                // Load API key for selected provider
                LoadApiKeyForProvider(type);
                
                // Update connection status display
                UpdateApiStatusDisplay();
            }
        }
        
        private void LoadApiKeyForProvider(AIProviderType providerType)
        {
            try
            {
                var providerName = providerType.ToString().ToLower();
                var apiKey = Core.ApiKeyManager.GetApiKey(providerName);
                
                if (!string.IsNullOrEmpty(apiKey))
                {
                    // Show masked key
                    AIApiKeyBox.Password = apiKey;
                    ApiKeyStatusText.Text = $"🔑 Loaded: {Core.ApiKeyManager.MaskApiKey(apiKey)}";
                    ApiKeyStatusText.Visibility = Visibility.Visible;
                }
                else
                {
                    AIApiKeyBox.Password = "";
                    ApiKeyStatusText.Text = "🔑 No API key configured";
                    ApiKeyStatusText.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Settings] Error loading API key: {ex.Message}");
            }
        }
        
        private void UpdateApiStatusDisplay()
        {
            try
            {
                var statusMessage = AIManager.GetActiveProviderStatusMessage();
                var status = AIManager.GetActiveProviderStatus();
                
                ApiStatusText.Text = statusMessage;
                
                // Update colors based on status
                switch (status)
                {
                    case Core.ConnectionStatus.Connected:
                        ApiStatusBorder.Background = new SolidColorBrush(Color.FromRgb(0x22, 0xc5, 0x5e)) { Opacity = 0.1 };
                        ApiStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x22, 0xc5, 0x5e));
                        break;
                    case Core.ConnectionStatus.NoApiKey:
                    case Core.ConnectionStatus.InvalidKey:
                        ApiStatusBorder.Background = new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44)) { Opacity = 0.1 };
                        ApiStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44));
                        break;
                    case Core.ConnectionStatus.RateLimited:
                        ApiStatusBorder.Background = new SolidColorBrush(Color.FromRgb(0xf5, 0x9e, 0x0b)) { Opacity = 0.1 };
                        ApiStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xf5, 0x9e, 0x0b));
                        break;
                    case Core.ConnectionStatus.Testing:
                        ApiStatusBorder.Background = new SolidColorBrush(Color.FromRgb(0x22, 0xd3, 0xee)) { Opacity = 0.1 };
                        ApiStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x22, 0xd3, 0xee));
                        break;
                    default:
                        ApiStatusBorder.Background = new SolidColorBrush(Color.FromRgb(0x94, 0xa3, 0xb8)) { Opacity = 0.1 };
                        ApiStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xa3, 0xb8));
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Settings] Error updating API status: {ex.Message}");
            }
        }
        
        private void AIApiKey_Changed(object sender, RoutedEventArgs e)
        {
            // Just mark that the key has changed - actual save happens on Save_Click
            if (!_isLoadingSettings && !string.IsNullOrEmpty(AIApiKeyBox.Password))
            {
                ApiKeyStatusText.Text = "💾 Key changed - click Save to apply";
                ApiKeyStatusText.Visibility = Visibility.Visible;
            }
        }
        
        private async void TestApiConnection_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TestApiConnectionBtn.IsEnabled = false;
                TestApiConnectionBtn.Content = "⏳ Testing...";
                
                // Save the API key first if it has changed
                var currentProvider = AIManager.GetActiveProvider();
                var providerName = currentProvider.ToString().ToLower();
                var apiKey = AIApiKeyBox.Password;
                
                if (!string.IsNullOrEmpty(apiKey))
                {
                    // Validate and save the key
                    if (Core.ApiKeyManager.IsValidKeyFormat(providerName, apiKey))
                    {
                        Core.ApiKeyManager.SaveApiKey(providerName, apiKey);
                        
                        // Configure the provider
                        await AIManager.ConfigureProviderAsync(currentProvider, new Dictionary<string, string>
                        {
                            { "ApiKey", apiKey }
                        });
                    }
                    else
                    {
                        ApiStatusText.Text = $"❌ Invalid API key format for {currentProvider}";
                        ApiStatusBorder.Background = new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44)) { Opacity = 0.1 };
                        ApiStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44));
                        TestApiConnectionBtn.IsEnabled = true;
                        TestApiConnectionBtn.Content = "🔌 Test";
                        return;
                    }
                }
                
                // Test connection
                ApiStatusText.Text = "🔄 Testing connection...";
                UpdateApiStatusDisplay();
                
                var success = await AIManager.TestProviderConnectionAsync(currentProvider);
                
                // Update status display
                UpdateApiStatusDisplay();
                
                if (success)
                {
                    ApiKeyStatusText.Text = $"✅ Connection successful!";
                    ApiKeyStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x22, 0xc5, 0x5e));
                }
                else
                {
                    var error = Core.ApiConnectionStatus.Instance.GetLastError(providerName);
                    ApiKeyStatusText.Text = $"❌ Connection failed: {error}";
                    ApiKeyStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44));
                }
                ApiKeyStatusText.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                ApiStatusText.Text = $"❌ Error: {ex.Message}";
                ApiStatusBorder.Background = new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44)) { Opacity = 0.1 };
                ApiStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44));
                Debug.WriteLine($"[Settings] Test connection error: {ex.Message}");
            }
            finally
            {
                TestApiConnectionBtn.IsEnabled = true;
                TestApiConnectionBtn.Content = "🔌 Test";
            }
        }

        private void AIModelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AIModelComboBox.SelectedItem is ComboBoxItem item && item.Tag is string modelId)
            {
                AIManager.SetSelectedModel(modelId);
            }
        }
        
        #endregion

        #region Voice Settings

        private void LoadVoiceProviders()
        {
            Debug.WriteLine("[Settings] LoadVoiceProviders called");
            VoiceProviderCombo.Items.Clear();
            VoiceProviderCombo.Items.Add(new ComboBoxItem 
            { 
                Content = "🖥️ Windows SAPI (Instant)", 
                Tag = VoiceProviderType.WindowsSAPI 
            });
            VoiceProviderCombo.Items.Add(new ComboBoxItem 
            { 
                Content = "🖥️ Edge TTS (Free)", 
                Tag = VoiceProviderType.EdgeTTS 
            });
            VoiceProviderCombo.Items.Add(new ComboBoxItem 
            { 
                Content = "☁️ OpenAI TTS (Cloud)", 
                Tag = VoiceProviderType.OpenAI 
            });
            VoiceProviderCombo.Items.Add(new ComboBoxItem 
            { 
                Content = "☁️ ElevenLabs (Cloud)", 
                Tag = VoiceProviderType.ElevenLabs 
            });
            Debug.WriteLine($"[Settings] LoadVoiceProviders: Added {VoiceProviderCombo.Items.Count} items, SelectedIndex={VoiceProviderCombo.SelectedIndex}");
            // Don't set SelectedIndex here - let LoadSettings() do it
        }

        private void VoiceProvider_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (VoiceProviderCombo.SelectedItem is ComboBoxItem item && item.Tag is VoiceProviderType type)
            {
                UpdateProviderInfo(type);
            }
        }

        private void UpdateProviderInfo(VoiceProviderType type)
        {
            switch (type)
            {
                case VoiceProviderType.WindowsSAPI:
                    ProviderInfoText.Text = "ℹ️ Windows SAPI: Instant response, works offline. Basic voice quality.";
                    CloudIndicator.Visibility = Visibility.Collapsed;
                    break;
                case VoiceProviderType.EdgeTTS:
                    ProviderInfoText.Text = "ℹ️ Edge TTS: Free neural voices via Microsoft Edge. Requires internet.";
                    CloudIndicator.Visibility = Visibility.Collapsed;
                    break;
                case VoiceProviderType.OpenAI:
                    ProviderInfoText.Text = "ℹ️ OpenAI TTS: High-quality voices. Requires API key. ~$0.015/1K chars.";
                    CloudIndicator.Visibility = Visibility.Visible;
                    break;
                case VoiceProviderType.ElevenLabs:
                    ProviderInfoText.Text = "ℹ️ ElevenLabs: Premium expressive voices. Requires API key.";
                    CloudIndicator.Visibility = Visibility.Visible;
                    break;
            }
        }
        
        #endregion

        #region Load/Save Settings

        private async Task LoadSettings()
        {
            try
            {
                Debug.WriteLine($"[Settings] LoadSettings called, VoiceKeysPath={VoiceKeysPath}");
                if (File.Exists(VoiceKeysPath))
                {
                    var json = File.ReadAllText(VoiceKeysPath);
                    Debug.WriteLine($"[Settings] Loaded voice_keys.json: {json}");
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    
                    if (root.TryGetProperty("openai", out var openai))
                        OpenAIKeyBox.Password = openai.GetString() ?? "";
                    if (root.TryGetProperty("elevenlabs", out var eleven))
                        ElevenLabsKeyBox.Password = eleven.GetString() ?? "";
                    if (root.TryGetProperty("provider", out var prov))
                    {
                        var provString = prov.GetString();
                        Debug.WriteLine($"[Settings] Found provider in JSON: '{provString}'");
                        if (Enum.TryParse<VoiceProviderType>(provString, out var provType))
                        {
                            Debug.WriteLine($"[Settings] Parsed provider type: {provType}");
                            for (int i = 0; i < VoiceProviderCombo.Items.Count; i++)
                            {
                                if (VoiceProviderCombo.Items[i] is ComboBoxItem item && 
                                    item.Tag is VoiceProviderType t && t == provType)
                                {
                                    Debug.WriteLine($"[Settings] Setting VoiceProviderCombo.SelectedIndex = {i}");
                                    VoiceProviderCombo.SelectedIndex = i;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            Debug.WriteLine($"[Settings] Failed to parse provider type from '{provString}'");
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"[Settings] No 'provider' property found in JSON");
                    }
                }
                else
                {
                    Debug.WriteLine($"[Settings] voice_keys.json does not exist");
                }
                
                // Fallback: if no provider was selected, default to first item
                if (VoiceProviderCombo.SelectedIndex < 0 && VoiceProviderCombo.Items.Count > 0)
                {
                    Debug.WriteLine($"[Settings] No provider selected, defaulting to index 0");
                    VoiceProviderCombo.SelectedIndex = 0;
                }
                
                Debug.WriteLine($"[Settings] After LoadSettings: VoiceProviderCombo.SelectedIndex={VoiceProviderCombo.SelectedIndex}");
                
                // Load honorific from user profile
                await LoadHonorificFromProfileAsync();
                
                // Load integration API keys (Canva, etc.)
                var integrationKeys = GetIntegrationApiKeys();
                if (integrationKeys.TryGetValue("canva", out var canvaKey) && !string.IsNullOrEmpty(canvaKey))
                {
                    CanvaApiKeyBox.Password = canvaKey;
                    // Configure Canva with the loaded key
                    Canva.CanvaTool.Instance.ConfigureApi(canvaKey);
                }
                
                using var key = Registry.CurrentUser.OpenSubKey(StartupKey, false);
                AutoStartCheckbox.IsChecked = key?.GetValue(AppName) != null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Settings] LoadSettings exception: {ex.Message}");
            }
        }
        
        private async Task LoadHonorificFromProfileAsync()
        {
            try
            {
                if (File.Exists(ProfilePath))
                {
                    var json = await File.ReadAllTextAsync(ProfilePath);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    
                    if (root.TryGetProperty("Honorific", out var honorificProp))
                    {
                        var honorificValue = honorificProp.GetInt32();
                        var honorific = (UserHonorific)honorificValue;
                        
                        // Find and select the matching item in the combo box
                        for (int i = 0; i < HonorificComboBox.Items.Count; i++)
                        {
                            if (HonorificComboBox.Items[i] is ComboBoxItem item && 
                                item.Tag?.ToString() == honorific.ToString())
                            {
                                HonorificComboBox.SelectedIndex = i;
                                break;
                            }
                        }
                    }
                    else
                    {
                        // Default to Sir if not set
                        HonorificComboBox.SelectedIndex = 0;
                    }
                }
                else
                {
                    // Default to Sir if no profile exists
                    HonorificComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Settings] Error loading honorific: {ex.Message}");
                HonorificComboBox.SelectedIndex = 0;
            }
        }
        
        private void HonorificComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Just track the selection - actual save happens in Save_Click
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!Directory.Exists(SettingsDir)) 
                    Directory.CreateDirectory(SettingsDir);
                
                // Save voice settings
                var selectedProvider = VoiceProviderType.WindowsSAPI;
                if (VoiceProviderCombo.SelectedItem is ComboBoxItem item && item.Tag is VoiceProviderType type)
                    selectedProvider = type;

                var voiceSettings = new Dictionary<string, string>
                {
                    ["openai"] = OpenAIKeyBox.Password,
                    ["elevenlabs"] = ElevenLabsKeyBox.Password,
                    ["provider"] = selectedProvider.ToString()
                };
                var json = JsonSerializer.Serialize(voiceSettings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(VoiceKeysPath, json);
                
                // Save hardware settings
                SaveHardwareSettings();
                
                // Save honorific to user profile
                await SaveHonorificToProfileAsync();
                
                // Save integration API keys (Canva, etc.)
                var canvaKey = CanvaApiKeyBox.Password;
                if (!string.IsNullOrEmpty(canvaKey))
                {
                    SetIntegrationApiKey("canva", canvaKey);
                }
                
                // Handle auto-start
                SetAutoStart(AutoStartCheckbox.IsChecked == true);
                
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private async Task SaveHonorificToProfileAsync()
        {
            try
            {
                // Get selected honorific
                UserHonorific selectedHonorific = UserHonorific.Sir;
                if (HonorificComboBox.SelectedItem is ComboBoxItem item && item.Tag != null)
                {
                    if (Enum.TryParse<UserHonorific>(item.Tag.ToString(), out var parsed))
                    {
                        selectedHonorific = parsed;
                    }
                }
                
                // Load existing profile or create new one
                UserProfile profile;
                if (File.Exists(ProfilePath))
                {
                    var json = await File.ReadAllTextAsync(ProfilePath);
                    profile = JsonSerializer.Deserialize<UserProfile>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new UserProfile();
                }
                else
                {
                    profile = new UserProfile();
                }
                
                // Update honorific
                profile.Honorific = selectedHonorific;
                profile.LastUpdated = DateTime.Now;
                
                // Save profile
                var updatedJson = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(ProfilePath, updatedJson);
                
                Debug.WriteLine($"[Settings] Saved honorific: {selectedHonorific}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Settings] Error saving honorific: {ex.Message}");
            }
        }

        private void SetAutoStart(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(StartupKey, true);
                if (key == null) return;
                
                if (enable)
                {
                    var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrEmpty(exePath))
                        key.SetValue(AppName, $"\"{exePath}\"");
                }
                else
                {
                    key.DeleteValue(AppName, false);
                }
            }
            catch { }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
        
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
        
        private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
                DragMove();
        }
        
        private void CanvaApiLink_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://www.canva.com/developers/") { UseShellExecute = true });
            }
            catch { }
        }
        
        #endregion

        #region Static Helpers

        public static string GetApiKey()
        {
            try
            {
                if (File.Exists(SettingsPath))
                    return File.ReadAllText(SettingsPath).Trim();
            }
            catch { }
            return string.Empty;
        }

        public static VoiceProviderType GetSelectedVoiceProvider()
        {
            try
            {
                if (File.Exists(VoiceKeysPath))
                {
                    var json = File.ReadAllText(VoiceKeysPath);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("provider", out var prov) &&
                        Enum.TryParse<VoiceProviderType>(prov.GetString(), out var type))
                        return type;
                }
            }
            catch { }
            return VoiceProviderType.WindowsSAPI;
        }

        public static Dictionary<string, string> GetVoiceApiKeys()
        {
            var keys = new Dictionary<string, string>();
            try
            {
                if (File.Exists(VoiceKeysPath))
                {
                    var json = File.ReadAllText(VoiceKeysPath);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    
                    if (root.TryGetProperty("openai", out var openai))
                        keys["openai"] = openai.GetString() ?? "";
                    if (root.TryGetProperty("elevenlabs", out var eleven))
                        keys["elevenlabs"] = eleven.GetString() ?? "";
                }
            }
            catch { }
            return keys;
        }
        
        /// <summary>
        /// Get integration API keys (Canva, etc.) - users provide their own keys
        /// </summary>
        public static Dictionary<string, string> GetIntegrationApiKeys()
        {
            var keys = new Dictionary<string, string>();
            try
            {
                var path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "AtlasAI", "integration_keys.json");
                    
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    
                    if (root.TryGetProperty("canva", out var canva))
                        keys["canva"] = canva.GetString() ?? "";
                    if (root.TryGetProperty("spotify", out var spotify))
                        keys["spotify"] = spotify.GetString() ?? "";
                }
            }
            catch { }
            return keys;
        }
        
        /// <summary>
        /// Save integration API key (user's own key)
        /// </summary>
        public static void SetIntegrationApiKey(string service, string apiKey)
        {
            try
            {
                var appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AtlasAI");
                Directory.CreateDirectory(appDataPath);
                
                var path = Path.Combine(appDataPath, "integration_keys.json");
                
                // Load existing keys
                var keys = GetIntegrationApiKeys();
                keys[service] = apiKey;
                
                var json = JsonSerializer.Serialize(keys, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
                
                Debug.WriteLine($"[Settings] Saved {service} API key");
                
                // Configure the service if it's Canva
                if (service == "canva" && !string.IsNullOrEmpty(apiKey))
                {
                    Canva.CanvaTool.Instance.ConfigureApi(apiKey);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Settings] Failed to save {service} API key: {ex.Message}");
            }
        }
        
        public static (int device, int sensitivity, string quality, string? deviceId) GetHardwareSettings()
        {
            try
            {
                var path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "AtlasAI", "hardware_settings.json");
                    
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    
                    var deviceId = root.TryGetProperty("micDeviceId", out var id) ? id.GetString() : null;
                    var device = root.TryGetProperty("micDevice", out var d) ? d.GetInt32() : -1;
                    var sensitivity = root.TryGetProperty("micSensitivity", out var s) ? s.GetInt32() : 120;
                    var quality = root.TryGetProperty("qualityMode", out var q) ? q.GetString() ?? "balanced" : "balanced";
                    
                    return (device, sensitivity, quality, deviceId);
                }
            }
            catch { }
            return (-1, 120, "balanced", null);
        }
        
        /// <summary>
        /// Set hardware settings (used by automatic microphone fallback)
        /// </summary>
        public static void SetHardwareSettings(int device, int sensitivity, string quality, string? deviceId)
        {
            try
            {
                var appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AtlasAI");
                Directory.CreateDirectory(appDataPath);
                
                var path = Path.Combine(appDataPath, "hardware_settings.json");
                
                var settings = new
                {
                    micDevice = device,
                    micDeviceId = deviceId ?? "",
                    micSensitivity = sensitivity,
                    qualityMode = quality
                };
                
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
                
                Debug.WriteLine($"[Settings] Updated hardware settings: device={device}, deviceId={deviceId}, sensitivity={sensitivity}, quality={quality}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Settings] Failed to save hardware settings: {ex.Message}");
            }
        }
        
        #endregion
        
        #region Orb Appearance Settings
        
        private void OrbStyle_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (OrbStyleCombo?.SelectedItem is ComboBoxItem item && item.Tag is string style)
            {
                // Don't apply during loading
                if (_isLoadingSettings)
                    return;
                    
                bool useParticles = style == "particles";
                
                // Apply to ChatWindow
                if (Owner is ChatWindow ownerChat)
                {
                    if (useParticles)
                    {
                        ownerChat.SetOrbStyle(false, null); // Use particles
                    }
                    else
                    {
                        ownerChat.SetOrbStyle(true, style); // Use Lottie with specific animation
                    }
                    Debug.WriteLine($"[Settings] Applied orb style via Owner: {style}");
                }
                else
                {
                    foreach (Window window in Application.Current.Windows)
                    {
                        if (window is ChatWindow chatWindow)
                        {
                            if (useParticles)
                            {
                                chatWindow.SetOrbStyle(false, null);
                            }
                            else
                            {
                                chatWindow.SetOrbStyle(true, style);
                            }
                            Debug.WriteLine($"[Settings] Applied orb style: {style}");
                            break;
                        }
                    }
                }
                
                // Save setting
                SaveOrbSettings();
            }
        }
        
        private void OrbColor_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (OrbColorCombo?.SelectedItem is ComboBoxItem item && item.Tag is string preset)
            {
                // Don't apply during loading - wait for LoadOrbSettings to set the correct value
                if (_isLoadingSettings)
                    return;
                    
                // Apply to ChatWindow (Owner or find in windows)
                if (Owner is ChatWindow ownerChat && ownerChat.AtlasCoreControl != null)
                {
                    ownerChat.AtlasCoreControl.ApplyColorPreset(preset);
                    Debug.WriteLine($"[Settings] Applied orb color preset via Owner: {preset}");
                }
                else
                {
                    foreach (Window window in Application.Current.Windows)
                    {
                        if (window is ChatWindow chatWindow && chatWindow.AtlasCoreControl != null)
                        {
                            chatWindow.AtlasCoreControl.ApplyColorPreset(preset);
                            Debug.WriteLine($"[Settings] Applied orb color preset: {preset}");
                            break;
                        }
                    }
                }
                
                // Save setting
                SaveOrbSettings();
            }
        }
        
        private void OrbSpeed_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (OrbSpeedLabel != null && OrbSpeedSlider != null)
            {
                var speed = OrbSpeedSlider.Value;
                OrbSpeedLabel.Text = $"{speed:F1}x";
                
                // Don't apply during loading
                if (_isLoadingSettings)
                    return;
                
                // Apply to ChatWindow (Owner or find in windows)
                if (Owner is ChatWindow ownerChat && ownerChat.AtlasCoreControl != null)
                {
                    ownerChat.AtlasCoreControl.AnimationSpeed = speed;
                    Debug.WriteLine($"[Settings] Applied orb speed via Owner: {speed}");
                }
                else
                {
                    foreach (Window window in Application.Current.Windows)
                    {
                        if (window is ChatWindow chatWindow && chatWindow.AtlasCoreControl != null)
                        {
                            chatWindow.AtlasCoreControl.AnimationSpeed = speed;
                            Debug.WriteLine($"[Settings] Applied orb speed: {speed}");
                            break;
                        }
                    }
                }
                
                SaveOrbSettings();
            }
        }
        
        private void OrbParticle_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (OrbParticleLabel != null && OrbParticleSlider != null)
            {
                var count = (int)OrbParticleSlider.Value;
                OrbParticleLabel.Text = count.ToString();
                
                // Don't apply during loading
                if (_isLoadingSettings)
                    return;
                
                // Apply to ChatWindow (Owner or find in windows)
                if (Owner is ChatWindow ownerChat && ownerChat.AtlasCoreControl != null)
                {
                    ownerChat.AtlasCoreControl.ParticleCount = count;
                    Debug.WriteLine($"[Settings] Applied orb particle count via Owner: {count}");
                }
                else
                {
                    foreach (Window window in Application.Current.Windows)
                    {
                        if (window is ChatWindow chatWindow && chatWindow.AtlasCoreControl != null)
                        {
                            chatWindow.AtlasCoreControl.ParticleCount = count;
                            Debug.WriteLine($"[Settings] Applied orb particle count: {count}");
                            break;
                        }
                    }
                }
                
                SaveOrbSettings();
            }
        }
        
        private void LoadOrbSettings()
        {
            try
            {
                var path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "AtlasAI", "orb_settings.json");
                    
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    
                    if (root.TryGetProperty("colorPreset", out var color))
                    {
                        var preset = color.GetString() ?? "cyan";
                        for (int i = 0; i < OrbColorCombo.Items.Count; i++)
                        {
                            if (OrbColorCombo.Items[i] is ComboBoxItem item && item.Tag as string == preset)
                            {
                                OrbColorCombo.SelectedIndex = i;
                                break;
                            }
                        }
                    }
                    else
                    {
                        // Default to cyan
                        OrbColorCombo.SelectedIndex = 0;
                    }
                    
                    if (root.TryGetProperty("orbStyle", out var orbStyle))
                    {
                        var style = orbStyle.GetString() ?? "Siri Animation.json";
                        for (int i = 0; i < OrbStyleCombo.Items.Count; i++)
                        {
                            if (OrbStyleCombo.Items[i] is ComboBoxItem item && item.Tag as string == style)
                            {
                                OrbStyleCombo.SelectedIndex = i;
                                break;
                            }
                        }
                    }
                    else
                    {
                        // Default to Siri Animation (index 1)
                        OrbStyleCombo.SelectedIndex = 1;
                    }
                    
                    if (root.TryGetProperty("animationSpeed", out var speed))
                    {
                        OrbSpeedSlider.Value = speed.GetDouble();
                    }
                    
                    if (root.TryGetProperty("particleCount", out var particles))
                    {
                        OrbParticleSlider.Value = particles.GetInt32();
                    }
                }
                else
                {
                    // No settings file - set defaults
                    OrbColorCombo.SelectedIndex = 0; // Cyan
                    OrbStyleCombo.SelectedIndex = 1; // Siri Animation
                }
            }
            catch 
            {
                // On error, set defaults
                if (OrbColorCombo.SelectedIndex < 0) OrbColorCombo.SelectedIndex = 0;
                if (OrbStyleCombo.SelectedIndex < 0) OrbStyleCombo.SelectedIndex = 1;
            }
        }
        
        private void SaveOrbSettings()
        {
            try
            {
                var appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AtlasAI");
                Directory.CreateDirectory(appDataPath);
                
                var path = Path.Combine(appDataPath, "orb_settings.json");
                
                var colorPreset = "cyan";
                if (OrbColorCombo.SelectedItem is ComboBoxItem item && item.Tag is string preset)
                    colorPreset = preset;
                
                var orbStyle = "Siri Animation.json";
                if (OrbStyleCombo.SelectedItem is ComboBoxItem styleItem && styleItem.Tag is string style)
                    orbStyle = style;
                
                var settings = new
                {
                    colorPreset = colorPreset,
                    orbStyle = orbStyle,
                    animationSpeed = OrbSpeedSlider.Value,
                    particleCount = (int)OrbParticleSlider.Value
                };
                
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch { }
        }
        
        /// <summary>
        /// Get saved orb settings (for loading on startup)
        /// </summary>
        public static (string colorPreset, string orbStyle, double speed, int particles) GetOrbSettings()
        {
            try
            {
                var path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "AtlasAI", "orb_settings.json");
                    
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    
                    var colorPreset = root.TryGetProperty("colorPreset", out var c) ? c.GetString() ?? "cyan" : "cyan";
                    var orbStyle = root.TryGetProperty("orbStyle", out var o) ? o.GetString() ?? "Siri Animation.json" : "Siri Animation.json";
                    var speed = root.TryGetProperty("animationSpeed", out var s) ? s.GetDouble() : 1.0;
                    var particles = root.TryGetProperty("particleCount", out var p) ? p.GetInt32() : 180;
                    
                    return (colorPreset, orbStyle, speed, particles);
                }
            }
            catch { }
            return ("cyan", "Siri Animation.json", 1.0, 180);
        }
        
        #endregion
    }
}
