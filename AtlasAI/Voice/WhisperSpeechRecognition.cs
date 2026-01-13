using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.CoreAudioApi;
using NAudio.MediaFoundation;

namespace AtlasAI.Voice
{
    /// <summary>
    /// OpenAI Whisper-based speech recognition with automatic silence detection
    /// Uses Windows Core Audio API (WASAPI) for proper Bluetooth/AirPods support
    /// </summary>
    public class WhisperSpeechRecognition : IDisposable
    {
        private static readonly HttpClient httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        private WasapiCapture? wasapiCapture;
        private WaveInEvent? waveIn;
        private MemoryStream? audioBuffer;
        private WaveFileWriter? waveWriter;
        private bool isRecording = false;
        private string? apiKey;
        private string? deviceId = null; // null = auto-detect best device (use device ID for WASAPI)
        private int deviceNumber = -1; // Legacy WaveIn device number (fallback)
        
        // Pre-buffer to capture audio BEFORE speech is detected (prevents cutting off beginning)
        private byte[] preBuffer = new byte[48000]; // ~1.5 seconds at 16kHz 16-bit mono (bigger buffer!)
        private int preBufferWritePos = 0;
        private bool preBufferFull = false;
        private bool preBufferWritten = false;
        
        // Silence detection - LOWERED threshold to catch quieter speech
        private DateTime lastSoundTime;
        private bool hasDetectedSpeech = false;
        private int silenceThreshold = 300; // LOWERED from 500 - more sensitive to catch short words
        private double silenceTimeoutSeconds = 1.5; // Stop after silence (give more time to think)
        private double maxRecordingSeconds = 20; // Longer max recording time for questions
        private double noSpeechTimeoutSeconds = 8.0; // Give plenty of time to start speaking
        private Timer? silenceTimer;
        private DateTime recordingStartTime;
        
        // FIXED: Track continuous speech to avoid false triggers
        private int consecutiveSpeechFrames = 0;
        private const int MinSpeechFramesRequired = 2; // LOWERED from 3 - faster detection for short words
        
        // Event to signal completion (for continuous listening)
        public event EventHandler? RecognitionComplete;
        
        public event EventHandler<string>? SpeechRecognized;
        public event EventHandler<string>? RecognitionError;
        public event EventHandler? RecordingStarted;
        public event EventHandler? RecordingStopped;
        
        /// <summary>
        /// Event fired when audio level changes - use for visual feedback (green when hearing audio)
        /// Args: (currentLevel, threshold, isAboveThreshold)
        /// </summary>
        public event EventHandler<(double Level, double Threshold, bool IsHearing)>? AudioLevelChanged;
        
        public bool IsRecording => isRecording;
        
        /// <summary>
        /// Set shorter silence timeout for wake word detection (faster response)
        /// </summary>
        public double SilenceTimeout
        {
            get => silenceTimeoutSeconds;
            set => silenceTimeoutSeconds = Math.Max(0.5, Math.Min(5.0, value));
        }
        
        public WhisperSpeechRecognition()
        {
            LoadApiKey();
            LoadHardwareSettings();
        }
        
        private void LoadHardwareSettings()
        {
            try
            {
                var settingsPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "AtlasAI", "hardware_settings.json");
                    
                if (File.Exists(settingsPath))
                {
                    var json = File.ReadAllText(settingsPath);
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    
                    // Load device ID (new format for WASAPI/Bluetooth)
                    if (root.TryGetProperty("micDeviceId", out var devId))
                    {
                        var id = devId.GetString();
                        if (!string.IsNullOrEmpty(id))
                            deviceId = id;
                    }
                    
                    // Legacy: load device index
                    if (root.TryGetProperty("micDevice", out var device))
                        deviceNumber = device.GetInt32();
                    
                    if (root.TryGetProperty("micSensitivity", out var sens))
                        silenceThreshold = sens.GetInt32();
                    
                    if (root.TryGetProperty("qualityMode", out var quality))
                    {
                        var mode = quality.GetString() ?? "balanced";
                        switch (mode)
                        {
                            case "low":
                                // Battery saver - longer timeouts, less responsive
                                silenceTimeoutSeconds = 2.0;
                                noSpeechTimeoutSeconds = 10.0;
                                maxRecordingSeconds = 30;
                                break;
                            case "high":
                                // Performance - faster response but still reasonable
                                silenceTimeoutSeconds = 1.0;
                                noSpeechTimeoutSeconds = 5.0;
                                maxRecordingSeconds = 15;
                                break;
                            default: // balanced
                                silenceTimeoutSeconds = 1.5;
                                noSpeechTimeoutSeconds = 8.0;
                                maxRecordingSeconds = 20;
                                break;
                        }
                    }
                    
                    Debug.WriteLine($"[Whisper] Loaded settings: device={deviceNumber}, sensitivity={silenceThreshold}, silence={silenceTimeoutSeconds}s");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Whisper] Failed to load hardware settings: {ex.Message}");
            }
        }
        
        private void LoadApiKey()
        {
            try
            {
                var appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AtlasAI");
                
                // First check ai_keys.json
                var keysPath = Path.Combine(appDataPath, "ai_keys.json");
                if (File.Exists(keysPath))
                {
                    var json = File.ReadAllText(keysPath);
                    var doc = System.Text.Json.JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("openai", out var openaiKey))
                    {
                        var key = openaiKey.GetString();
                        if (!string.IsNullOrEmpty(key))
                        {
                            apiKey = key;
                            Debug.WriteLine("[Whisper] Loaded key from ai_keys.json");
                            return;
                        }
                    }
                }
                
                // Try openai_key.txt
                var settingsPath = Path.Combine(appDataPath, "openai_key.txt");
                if (File.Exists(settingsPath))
                {
                    apiKey = File.ReadAllText(settingsPath).Trim();
                    Debug.WriteLine("[Whisper] Loaded key from openai_key.txt");
                    return;
                }
                
                // Check settings.txt for backward compatibility
                var oldSettingsPath = Path.Combine(appDataPath, "settings.txt");
                if (File.Exists(oldSettingsPath))
                {
                    var content = File.ReadAllText(oldSettingsPath).Trim();
                    if (content.StartsWith("sk-") && !content.StartsWith("sk-ant-"))
                    {
                        apiKey = content;
                        Debug.WriteLine("[Whisper] Loaded key from settings.txt");
                        return;
                    }
                }
                
                Debug.WriteLine("[Whisper] No API key found - voice activation will not work");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Whisper] Failed to load API key: {ex.Message}");
            }
        }
        
        public bool IsConfigured => !string.IsNullOrEmpty(apiKey);

        /// <summary>
        /// Get list of available audio input devices using Core Audio API (supports Bluetooth/AirPods)
        /// </summary>
        public static List<(int Index, string Name, string DeviceId)> GetAvailableDevicesEx()
        {
            var devices = new List<(int, string, string)>();
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                var mmDevices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
                
                int index = 0;
                foreach (var device in mmDevices)
                {
                    var name = device.FriendlyName;
                    var id = device.ID;
                    devices.Add((index, name, id));
                    Debug.WriteLine($"[Whisper] WASAPI Device {index}: {name} (ID: {id})");
                    index++;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Whisper] MMDevice enumeration failed: {ex.Message}, falling back to WaveIn");
                // Fallback to legacy WaveIn
                for (int i = 0; i < WaveIn.DeviceCount; i++)
                {
                    var caps = WaveIn.GetCapabilities(i);
                    devices.Add((i, caps.ProductName, i.ToString()));
                    Debug.WriteLine($"[Whisper] WaveIn Device {i}: {caps.ProductName}");
                }
            }
            return devices;
        }
        
        /// <summary>
        /// Get list of available audio input devices (legacy method for compatibility)
        /// </summary>
        public static List<(int Index, string Name)> GetAvailableDevices()
        {
            var devices = new List<(int, string)>();
            var devicesEx = GetAvailableDevicesEx();
            foreach (var d in devicesEx)
            {
                devices.Add((d.Index, d.Name));
            }
            return devices;
        }
        
        /// <summary>
        /// Set the device to use for recording by device ID (for WASAPI/Bluetooth devices)
        /// </summary>
        public void SetDeviceById(string? id)
        {
            deviceId = id;
            Debug.WriteLine($"[Whisper] Device ID set to: {id ?? "auto"}");
        }
        
        /// <summary>
        /// Set the device to use for recording by index (-1 = auto-detect)
        /// </summary>
        public void SetDevice(int deviceIndex)
        {
            deviceNumber = deviceIndex;
            deviceId = null; // Clear device ID when using index
            Debug.WriteLine($"[Whisper] Device index set to: {deviceIndex}");
        }
        
        /// <summary>
        /// Auto-detect the best microphone using Core Audio API (prefers Bluetooth/AirPods over built-in)
        /// </summary>
        private MMDevice? GetBestMMDevice()
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                
                // If specific device ID is set, use it
                if (!string.IsNullOrEmpty(deviceId))
                {
                    try
                    {
                        var device = enumerator.GetDevice(deviceId);
                        if (device != null && device.State == DeviceState.Active)
                        {
                            Debug.WriteLine($"[Whisper] Using specified device: {device.FriendlyName}");
                            return device;
                        }
                    }
                    catch { }
                }
                
                // Get all active capture devices
                var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
                if (devices.Count == 0)
                {
                    Debug.WriteLine("[Whisper] No active capture devices found");
                    return null;
                }
                
                MMDevice? bestDevice = null;
                int bestScore = -1000;
                
                foreach (var device in devices)
                {
                    var name = device.FriendlyName.ToLower();
                    int score = 0;
                    
                    // Prefer Bluetooth/wireless devices (AirPods, headsets)
                    if (name.Contains("airpod") || name.Contains("bluetooth") || name.Contains("wireless"))
                        score += 100;
                    
                    // Prefer headsets
                    if (name.Contains("headset") || name.Contains("headphone") || name.Contains("earphone") || name.Contains("hands-free"))
                        score += 50;
                    
                    // Prefer USB microphones
                    if (name.Contains("usb"))
                        score += 30;
                    
                    // Avoid "stereo mix" or "what u hear" (these capture system audio, not mic)
                    if (name.Contains("stereo mix") || name.Contains("what u hear") || name.Contains("loopback"))
                        score -= 100;
                    
                    // Avoid "digital" (often virtual devices)
                    if (name.Contains("digital"))
                        score -= 20;
                    
                    Debug.WriteLine($"[Whisper] Device: {device.FriendlyName} (score: {score})");
                    
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestDevice = device;
                    }
                }
                
                if (bestDevice != null)
                {
                    Debug.WriteLine($"[Whisper] Selected device: {bestDevice.FriendlyName}");
                }
                
                return bestDevice;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Whisper] GetBestMMDevice error: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Legacy method - auto-detect the best microphone using WaveIn (fallback)
        /// </summary>
        private int GetBestDevice()
        {
            if (deviceNumber >= 0 && deviceNumber < WaveIn.DeviceCount)
                return deviceNumber;
            
            int bestDevice = 0;
            int bestScore = 0;
            
            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                var caps = WaveIn.GetCapabilities(i);
                var name = caps.ProductName.ToLower();
                int score = 0;
                
                // Prefer Bluetooth/wireless devices (AirPods, headsets)
                if (name.Contains("airpod") || name.Contains("bluetooth") || name.Contains("wireless"))
                    score += 100;
                
                // Prefer headsets
                if (name.Contains("headset") || name.Contains("headphone") || name.Contains("earphone"))
                    score += 50;
                
                // Prefer USB microphones
                if (name.Contains("usb"))
                    score += 30;
                
                // Avoid "stereo mix" or "what u hear" (these capture system audio, not mic)
                if (name.Contains("stereo mix") || name.Contains("what u hear") || name.Contains("loopback"))
                    score -= 100;
                
                // Avoid "digital" (often virtual devices)
                if (name.Contains("digital"))
                    score -= 20;
                
                Debug.WriteLine($"[Whisper] WaveIn Device {i}: {caps.ProductName} (score: {score})");
                
                if (score > bestScore)
                {
                    bestScore = score;
                    bestDevice = i;
                }
            }
            
            Debug.WriteLine($"[Whisper] Selected WaveIn device {bestDevice}");
            return bestDevice;
        }

        /// <summary>
        /// Find a working microphone - ALWAYS uses Windows default device
        /// </summary>
        private int FindWorkingMicrophone()
        {
            // ALWAYS use Windows default device (device 0)
            // This ensures we use whatever the user has set in Windows Sound settings
            Debug.WriteLine("[Whisper] Using Windows default microphone (device 0)");
            
            try
            {
                // Get the Windows default device name for display
                using var enumerator = new MMDeviceEnumerator();
                var defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
                if (defaultDevice != null)
                {
                    _lastWorkingDeviceName = defaultDevice.FriendlyName;
                    Debug.WriteLine($"[Whisper] Windows default mic: {_lastWorkingDeviceName}");
                }
                else
                {
                    _lastWorkingDeviceName = "Default Microphone";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Whisper] Could not get default device name: {ex.Message}");
                _lastWorkingDeviceName = "Default Microphone";
            }
            
            _lastWorkingDevice = 0; // Device 0 = Windows default
            return 0;
        }
        
        // Track last working device for status reporting
        private int _lastWorkingDevice = -1;
        private string _lastWorkingDeviceName = "";
        
        /// <summary>
        /// Event fired when microphone status changes (for UI feedback)
        /// </summary>
        public event EventHandler<string>? MicrophoneStatusChanged;
        
        /// <summary>
        /// Get the name of the last working microphone
        /// </summary>
        public string LastWorkingMicrophoneName => _lastWorkingDeviceName;
        
        /// <summary>
        /// Test if a microphone actually produces audio by recording briefly and checking levels.
        /// This is the key to universal mic support - we don't trust device names, we test!
        /// </summary>
        private bool TestMicrophoneProducesAudio(int deviceIndex, int testDurationMs = 300)
        {
            if (deviceIndex < 0 || deviceIndex >= WaveIn.DeviceCount)
                return false;
            
            try
            {
                var caps = WaveIn.GetCapabilities(deviceIndex);
                Debug.WriteLine($"[Whisper] Testing mic: {caps.ProductName}");
                
                double maxLevel = 0;
                int samplesRead = 0;
                var testComplete = new System.Threading.ManualResetEventSlim(false);
                
                using var testWaveIn = new WaveInEvent
                {
                    DeviceNumber = deviceIndex,
                    WaveFormat = new WaveFormat(16000, 16, 1),
                    BufferMilliseconds = 50
                };
                
                testWaveIn.DataAvailable += (s, e) =>
                {
                    // Calculate RMS level
                    double sum = 0;
                    for (int i = 0; i < e.BytesRecorded; i += 2)
                    {
                        if (i + 1 < e.BytesRecorded)
                        {
                            short sample = (short)(e.Buffer[i] | (e.Buffer[i + 1] << 8));
                            sum += Math.Abs(sample);
                        }
                    }
                    double avgLevel = sum / Math.Max(1, e.BytesRecorded / 2);
                    if (avgLevel > maxLevel)
                        maxLevel = avgLevel;
                    samplesRead += e.BytesRecorded;
                };
                
                testWaveIn.RecordingStopped += (s, e) =>
                {
                    testComplete.Set();
                };
                
                testWaveIn.StartRecording();
                
                // Wait for test duration
                Thread.Sleep(testDurationMs);
                
                testWaveIn.StopRecording();
                testComplete.Wait(500); // Wait for stop to complete
                
                // A working mic should have SOME noise floor (> 1) even in silence
                // A broken mic (like AirPods on Windows) will have exactly 0
                bool hasAudio = maxLevel > 1 && samplesRead > 0;
                
                Debug.WriteLine($"[Whisper] Mic test result: {caps.ProductName} - maxLevel={maxLevel:F0}, samples={samplesRead}, working={hasAudio}");
                
                return hasAudio;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Whisper] Mic test failed for device {deviceIndex}: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Public method to test a specific microphone using WASAPI (for Settings UI)
        /// This works with Bluetooth/AirPods devices that don't work with WaveIn
        /// </summary>
        public static (bool Working, double Level, string Message) TestMicrophoneByDeviceId(string deviceId)
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                var device = enumerator.GetDevice(deviceId);
                if (device == null || device.State != DeviceState.Active)
                    return (false, 0, "Device not available");
                
                return TestMicrophoneWasapi(device);
            }
            catch (Exception ex)
            {
                return (false, 0, $"Error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Test microphone using WASAPI - works with Bluetooth/AirPods
        /// </summary>
        private static (bool Working, double Level, string Message) TestMicrophoneWasapi(MMDevice device)
        {
            double maxLevel = 0;
            double totalLevel = 0;
            int sampleCount = 0;
            int samplesRead = 0;
            var testComplete = new System.Threading.ManualResetEventSlim(false);
            WasapiCapture? capture = null;
            
            try
            {
                var deviceName = device.FriendlyName;
                Debug.WriteLine($"[Whisper] WASAPI testing: {deviceName}");
                
                // Try ONLY shared mode for testing (audio-friendly)
                Exception? lastError = null;
                
                // Try 1: Shared mode with default buffer ONLY
                try
                {
                    capture = new WasapiCapture(device, true); // shared mode, default buffer
                    Debug.WriteLine($"[Whisper] WASAPI shared mode created for testing (audio-friendly)");
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    Debug.WriteLine($"[Whisper] WASAPI shared mode failed: {ex.Message}");
                    capture?.Dispose();
                    capture = null;
                }
                
                // DON'T try exclusive mode during testing - it can disrupt audio
                
                if (capture == null)
                {
                    return (false, 0, $"WASAPI shared mode failed: {lastError?.Message ?? "Unknown error"} (exclusive mode disabled to prevent audio interference)");
                }
                
                var format = capture.WaveFormat;
                Debug.WriteLine($"[Whisper] WASAPI format: {format.SampleRate}Hz, {format.Channels}ch, {format.BitsPerSample}bit, {format.Encoding}");
                
                capture.DataAvailable += (s, e) =>
                {
                    double level = 0;
                    
                    // Handle IEEE float format (common with WASAPI)
                    if (format.Encoding == WaveFormatEncoding.IeeeFloat && format.BitsPerSample == 32)
                    {
                        for (int i = 0; i < e.BytesRecorded; i += 4 * format.Channels)
                        {
                            if (i + 3 < e.BytesRecorded)
                            {
                                float sample = BitConverter.ToSingle(e.Buffer, i);
                                level += Math.Abs(sample) * 32767; // Scale to 16-bit range
                            }
                        }
                        level /= Math.Max(1, e.BytesRecorded / (4 * format.Channels));
                    }
                    else if (format.BitsPerSample == 16)
                    {
                        // 16-bit PCM
                        for (int i = 0; i < e.BytesRecorded; i += 2 * format.Channels)
                        {
                            if (i + 1 < e.BytesRecorded)
                            {
                                short sample = (short)(e.Buffer[i] | (e.Buffer[i + 1] << 8));
                                level += Math.Abs(sample);
                            }
                        }
                        level /= Math.Max(1, e.BytesRecorded / (2 * format.Channels));
                    }
                    else if (format.BitsPerSample == 8)
                    {
                        // 8-bit PCM (common with Bluetooth HFP)
                        for (int i = 0; i < e.BytesRecorded; i += format.Channels)
                        {
                            byte sample = e.Buffer[i];
                            level += Math.Abs(sample - 128) * 256; // Convert to 16-bit range
                        }
                        level /= Math.Max(1, e.BytesRecorded / format.Channels);
                    }
                    
                    totalLevel += level;
                    sampleCount++;
                    if (level > maxLevel)
                        maxLevel = level;
                    samplesRead += e.BytesRecorded;
                    
                    Debug.WriteLine($"[Whisper] WASAPI data: {e.BytesRecorded} bytes, level={level:F0}");
                };
                
                capture.RecordingStopped += (s, e) => 
                {
                    if (e.Exception != null)
                        Debug.WriteLine($"[Whisper] WASAPI stopped with error: {e.Exception.Message}");
                    testComplete.Set();
                };
                
                capture.StartRecording();
                Debug.WriteLine($"[Whisper] WASAPI recording started");
                Thread.Sleep(800); // Test for 800ms (longer for Bluetooth)
                capture.StopRecording();
                testComplete.Wait(2000); // Wait longer for Bluetooth
                
                bool working = samplesRead > 0 && sampleCount > 0;
                double avgOverall = sampleCount > 0 ? totalLevel / sampleCount : 0;
                
                string message = working 
                    ? $"✓ {deviceName} is working (level: {maxLevel:F0})"
                    : $"✗ {deviceName} not responding";
                
                Debug.WriteLine($"[Whisper] WASAPI test: {deviceName} - max:{maxLevel:F0} avg:{avgOverall:F0} samples:{samplesRead} working:{working}");
                
                return (working, maxLevel, message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Whisper] WASAPI test error: {ex.Message}");
                return (false, 0, $"Error: {ex.Message}");
            }
            finally
            {
                capture?.Dispose();
            }
        }
        
        /// <summary>
        /// Public method to test a specific microphone (for Settings UI)
        /// Falls back to WASAPI for Bluetooth devices
        /// </summary>
        public static (bool Working, double Level, string Message) TestMicrophone(int deviceIndex)
        {
            if (deviceIndex < 0 || deviceIndex >= WaveIn.DeviceCount)
                return (false, 0, "Invalid device");
            
            try
            {
                var caps = WaveIn.GetCapabilities(deviceIndex);
                var nameLower = caps.ProductName.ToLower();
                
                // For Bluetooth/wireless devices, use WASAPI test instead
                if (nameLower.Contains("airpod") || nameLower.Contains("bluetooth") || 
                    nameLower.Contains("wireless") || nameLower.Contains("headset") ||
                    nameLower.Contains("hands-free"))
                {
                    Debug.WriteLine($"[Whisper] Using WASAPI test for Bluetooth device: {caps.ProductName}");
                    // Try to find matching WASAPI device
                    try
                    {
                        using var enumerator = new MMDeviceEnumerator();
                        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
                        foreach (var device in devices)
                        {
                            var mmName = device.FriendlyName.ToLower();
                            if (mmName.Contains(nameLower.Substring(0, Math.Min(10, nameLower.Length))) ||
                                nameLower.Contains(mmName.Substring(0, Math.Min(10, mmName.Length))))
                            {
                                return TestMicrophoneWasapi(device);
                            }
                        }
                    }
                    catch { }
                }
                
                // Standard WaveIn test for regular devices
                double maxLevel = 0;
                double totalLevel = 0;
                int sampleCount = 0;
                int samplesRead = 0;
                var testComplete = new System.Threading.ManualResetEventSlim(false);
                
                using var testWaveIn = new WaveInEvent
                {
                    DeviceNumber = deviceIndex,
                    WaveFormat = new WaveFormat(16000, 16, 1),
                    BufferMilliseconds = 50
                };
                
                testWaveIn.DataAvailable += (s, e) =>
                {
                    double sum = 0;
                    for (int i = 0; i < e.BytesRecorded; i += 2)
                    {
                        if (i + 1 < e.BytesRecorded)
                        {
                            short sample = (short)(e.Buffer[i] | (e.Buffer[i + 1] << 8));
                            sum += Math.Abs(sample);
                        }
                    }
                    double avgLevel = sum / Math.Max(1, e.BytesRecorded / 2);
                    totalLevel += avgLevel;
                    sampleCount++;
                    if (avgLevel > maxLevel)
                        maxLevel = avgLevel;
                    samplesRead += e.BytesRecorded;
                };
                
                testWaveIn.RecordingStopped += (s, e) => testComplete.Set();
                
                testWaveIn.StartRecording();
                Thread.Sleep(500); // Test for 500ms
                testWaveIn.StopRecording();
                testComplete.Wait(500);
                
                // A working mic should produce samples - even very quiet mics are fine
                // The key is that we got samples at all (samplesRead > 0)
                // maxLevel > 0 means there's SOME signal (even noise floor counts)
                bool working = samplesRead > 0 && sampleCount > 0;
                double avgOverall = sampleCount > 0 ? totalLevel / sampleCount : 0;
                
                string message = working 
                    ? $"✓ {caps.ProductName} is working (level: {maxLevel:F0}, avg: {avgOverall:F0})"
                    : $"✗ {caps.ProductName} not responding (no samples received)";
                
                Debug.WriteLine($"[Whisper] Mic test: {caps.ProductName} - max:{maxLevel:F0} avg:{avgOverall:F0} samples:{samplesRead} working:{working}");
                
                return (working, maxLevel, message);
            }
            catch (Exception ex)
            {
                return (false, 0, $"Error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Find all working microphones (for Settings UI to show which mics actually work)
        /// </summary>
        public static List<(int Index, string Name, bool Working, double Level)> GetWorkingMicrophones()
        {
            var results = new List<(int, string, bool, double)>();
            
            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                var caps = WaveIn.GetCapabilities(i);
                var name = caps.ProductName;
                
                // Skip obviously bad devices
                var nameLower = name.ToLower();
                if (nameLower.Contains("stereo mix") || nameLower.Contains("what u hear") || nameLower.Contains("loopback"))
                {
                    results.Add((i, name, false, 0));
                    continue;
                }
                
                var (working, level, _) = TestMicrophone(i);
                results.Add((i, name, working, level));
            }
            
            return results;
        }
        
        public void StartRecording()
        {
            if (isRecording) return;
            if (!IsConfigured)
            {
                RecognitionError?.Invoke(this, "OpenAI API key not configured");
                return;
            }
            
            try
            {
                // SIMPLIFIED: Always use Windows default device (WaveIn device 0)
                // This is what the user requested - universal, whatever Windows has as default
                Debug.WriteLine("[Whisper] ========================================");
                Debug.WriteLine("[Whisper] STARTING RECORDING - USING WINDOWS DEFAULT");
                Debug.WriteLine("[Whisper] ========================================");
                
                // Get the Windows default device name for logging
                try
                {
                    using var enumerator = new MMDeviceEnumerator();
                    var defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
                    if (defaultDevice != null)
                    {
                        _lastWorkingDeviceName = defaultDevice.FriendlyName;
                        Debug.WriteLine($"[Whisper] Windows default mic: {_lastWorkingDeviceName}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Whisper] Could not get default device name: {ex.Message}");
                    _lastWorkingDeviceName = "Default Microphone";
                }
                
                // ALWAYS use WaveIn with device 0 (Windows default)
                // This is the most reliable and universal approach
                StartWaveInRecording();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Whisper] Start error: {ex.Message}");
                RecognitionError?.Invoke(this, $"Failed to start: {ex.Message}");
                Cleanup();
            }
        }
        
        private void StartWasapiRecording(MMDevice device)
        {
            try
            {
                // EXTREME AUDIO DISTORTION FIX: Use maximum buffer sizes and lowest priority
                Debug.WriteLine($"[Whisper] Starting WASAPI in ULTRA audio-friendly mode to prevent distortion");
                
                var deviceName = device.FriendlyName;
                Debug.WriteLine($"[Whisper] WASAPI device: {deviceName}");
                
                // Create audio buffer
                audioBuffer = new MemoryStream();
                
                // CRITICAL: Use ONLY shared mode with ULTRA-CONSERVATIVE buffer to prevent audio interference
                try
                {
                    // Use 1000ms buffer for MAXIMUM audio compatibility and zero interference
                    wasapiCapture = new WasapiCapture(device, true, 1000); // shared mode, 1 second buffer
                    Debug.WriteLine($"[Whisper] WASAPI shared mode with 1000ms buffer (ZERO interference mode)");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Whisper] WASAPI with 1000ms buffer failed: {ex.Message}, trying 800ms");
                    try
                    {
                        wasapiCapture = new WasapiCapture(device, true, 800); // shared mode, 800ms buffer
                        Debug.WriteLine($"[Whisper] WASAPI shared mode with 800ms buffer");
                    }
                    catch (Exception ex2)
                    {
                        Debug.WriteLine($"[Whisper] WASAPI with 800ms buffer failed: {ex2.Message}, trying 500ms");
                        try
                        {
                            wasapiCapture = new WasapiCapture(device, true, 500); // shared mode, 500ms buffer
                            Debug.WriteLine($"[Whisper] WASAPI shared mode with 500ms buffer");
                        }
                        catch (Exception ex3)
                        {
                            Debug.WriteLine($"[Whisper] WASAPI with 500ms buffer failed: {ex3.Message}, trying default");
                            wasapiCapture = new WasapiCapture(device, true); // shared mode, default buffer
                            Debug.WriteLine($"[Whisper] WASAPI shared mode with default buffer");
                        }
                    }
                }
                
                // Create wave writer for the WASAPI format (we'll convert later)
                var targetFormat = new WaveFormat(16000, 16, 1); // Whisper format
                waveWriter = new WaveFileWriter(audioBuffer, targetFormat);
                
                // WASAPI may use different format, we'll convert to 16kHz mono for Whisper
                var sourceFormat = wasapiCapture.WaveFormat;
                Debug.WriteLine($"[Whisper] WASAPI format: {sourceFormat.SampleRate}Hz, {sourceFormat.Channels}ch, {sourceFormat.BitsPerSample}bit, Encoding: {sourceFormat.Encoding}");
                
                // Reset silence detection
                lastSoundTime = DateTime.Now;
                hasDetectedSpeech = false;
                consecutiveSpeechFrames = 0; // FIXED: Reset consecutive speech counter
                recordingStartTime = DateTime.Now;
                
                // Reset pre-buffer
                preBufferWritePos = 0;
                preBufferFull = false;
                preBufferWritten = false;
                
                // FIXED: Use reasonable thresholds - not too sensitive to avoid false triggers
                var deviceNameLower = deviceName.ToLower();
                if (deviceNameLower.Contains("airpod") || deviceNameLower.Contains("bluetooth") || deviceNameLower.Contains("wireless"))
                {
                    silenceThreshold = Math.Max(200, silenceThreshold / 2); // Lower for Bluetooth but still reasonable
                    Debug.WriteLine($"[Whisper] Adjusted threshold for Bluetooth: {silenceThreshold}");
                }
                
                // For USB audio devices, also adjust threshold
                if (deviceNameLower.Contains("usb") || deviceNameLower.Contains("usbaudio"))
                {
                    silenceThreshold = Math.Max(150, silenceThreshold / 2); // Lower for USB mics but still reasonable
                    Debug.WriteLine($"[Whisper] Adjusted threshold for USB audio: {silenceThreshold}");
                }
                
                // REMOVED: Extra sensitive mode was causing false triggers
                
                wasapiCapture.DataAvailable += OnWasapiDataAvailable;
                wasapiCapture.RecordingStopped += (s, e) =>
                {
                    if (e.Exception != null)
                        Debug.WriteLine($"[Whisper] WASAPI recording error: {e.Exception.Message}");
                };
                
                // Start silence detection timer AFTER a warm-up delay
                // This prevents the recognizer from timing out due to the ping sound or ambient noise
                silenceTimer = new Timer(CheckSilence, null, 800, 100); // 800ms initial delay, then check every 100ms
                
                wasapiCapture.StartRecording();
                isRecording = true;
                RecordingStarted?.Invoke(this, EventArgs.Empty);
                
                // Notify audio coordinator to pause other audio systems
                AudioCoordinator.NotifyVoiceRecordingStarted();
                
                Debug.WriteLine($"[Whisper] WASAPI recording started on: {device.FriendlyName} (audio-friendly mode)");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Whisper] WASAPI start failed: {ex.Message}, trying WaveIn fallback");
                wasapiCapture?.Dispose();
                wasapiCapture = null;
                StartWaveInRecording();
            }
        }
        
        private void StartWaveInRecording()
        {
            // List all available devices for debugging
            Debug.WriteLine($"[Whisper] WaveIn device count: {WaveIn.DeviceCount}");
            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                var caps = WaveIn.GetCapabilities(i);
                Debug.WriteLine($"[Whisper] Device {i}: {caps.ProductName}");
            }
            
            if (WaveIn.DeviceCount == 0)
            {
                RecognitionError?.Invoke(this, "No microphone detected");
                return;
            }
            
            // ALWAYS use device 0 (Windows default) - this is what the user requested
            int selectedDevice = 0;
            var deviceCaps = WaveIn.GetCapabilities(0);
            Debug.WriteLine($"[Whisper] Using Windows default device 0: {deviceCaps.ProductName}");
            _lastWorkingDeviceName = deviceCaps.ProductName;
            
            audioBuffer = new MemoryStream();
            
            // FIXED: Use lower threshold to catch quieter speech and short words
            silenceThreshold = 300; // LOWERED - more sensitive to catch short words like "time"
            Debug.WriteLine($"[Whisper] Using silence threshold: {silenceThreshold}");
            
            waveIn = new WaveInEvent
            {
                DeviceNumber = selectedDevice,
                WaveFormat = new WaveFormat(16000, 16, 1),
                BufferMilliseconds = 100 // Smaller buffer for faster response
            };
            
            waveWriter = new WaveFileWriter(audioBuffer, waveIn.WaveFormat);
            
            // Reset silence detection
            lastSoundTime = DateTime.Now;
            hasDetectedSpeech = false;
            consecutiveSpeechFrames = 0; // FIXED: Reset consecutive speech counter
            recordingStartTime = DateTime.Now;
            
            // Reset pre-buffer
            preBufferWritePos = 0;
            preBufferFull = false;
            preBufferWritten = false;
            
            waveIn.DataAvailable += OnDataAvailable;
            waveIn.RecordingStopped += (s, e) =>
            {
                if (e.Exception != null)
                    Debug.WriteLine($"[Whisper] Recording error: {e.Exception.Message}");
            };
            
            // Start silence detection timer AFTER a warm-up delay
            // This prevents the recognizer from timing out due to the ping sound or ambient noise
            silenceTimer = new Timer(CheckSilence, null, 800, 100); // 800ms initial delay, then check every 100ms
            
            waveIn.StartRecording();
            isRecording = true;
            RecordingStarted?.Invoke(this, EventArgs.Empty);
            
            Debug.WriteLine("[Whisper] WaveIn recording started - LISTENING NOW");
        }
        
        // Resampler for WASAPI audio (convert to 16kHz mono for Whisper)
        private byte[] ResampleToWhisperFormat(byte[] data, WaveFormat sourceFormat)
        {
            // If already in correct format, return as-is
            if (sourceFormat.SampleRate == 16000 && sourceFormat.Channels == 1 && sourceFormat.BitsPerSample == 16)
                return data;
            
            try
            {
                // Handle IEEE float format (common with WASAPI/Bluetooth)
                if (sourceFormat.Encoding == WaveFormatEncoding.IeeeFloat)
                {
                    Debug.WriteLine($"[Whisper] Converting IEEE float audio: {sourceFormat.SampleRate}Hz {sourceFormat.Channels}ch");
                    return ConvertIeeeFloatTo16BitMono(data, sourceFormat);
                }
                
                // Use MediaFoundation resampler for PCM formats
                using var sourceStream = new RawSourceWaveStream(new MemoryStream(data), sourceFormat);
                using var resampler = new MediaFoundationResampler(sourceStream, new WaveFormat(16000, 16, 1));
                resampler.ResamplerQuality = 60;
                
                using var outputStream = new MemoryStream();
                byte[] buffer = new byte[4096];
                int bytesRead;
                while ((bytesRead = resampler.Read(buffer, 0, buffer.Length)) > 0)
                {
                    outputStream.Write(buffer, 0, bytesRead);
                }
                return outputStream.ToArray();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Whisper] Resample error: {ex.Message}");
                // Try simple conversion as fallback
                try
                {
                    return ConvertIeeeFloatTo16BitMono(data, sourceFormat);
                }
                catch
                {
                    return data; // Return original if all conversion fails
                }
            }
        }
        
        // Convert IEEE float audio to 16-bit PCM mono
        private byte[] ConvertIeeeFloatTo16BitMono(byte[] data, WaveFormat sourceFormat)
        {
            int bytesPerSample = sourceFormat.BitsPerSample / 8;
            int channels = sourceFormat.Channels;
            int sampleCount = data.Length / (bytesPerSample * channels);
            
            // Calculate output size (mono 16-bit at 16kHz)
            double ratio = 16000.0 / sourceFormat.SampleRate;
            int outputSampleCount = (int)(sampleCount * ratio);
            byte[] output = new byte[outputSampleCount * 2]; // 16-bit = 2 bytes per sample
            
            for (int i = 0; i < outputSampleCount; i++)
            {
                // Find source sample index
                int srcIndex = (int)(i / ratio);
                if (srcIndex >= sampleCount) srcIndex = sampleCount - 1;
                
                float sample = 0;
                
                // Read and average all channels
                for (int ch = 0; ch < channels; ch++)
                {
                    int offset = (srcIndex * channels + ch) * bytesPerSample;
                    if (offset + bytesPerSample <= data.Length)
                    {
                        if (bytesPerSample == 4) // 32-bit float
                        {
                            sample += BitConverter.ToSingle(data, offset);
                        }
                        else if (bytesPerSample == 2) // 16-bit PCM
                        {
                            sample += BitConverter.ToInt16(data, offset) / 32768f;
                        }
                    }
                }
                sample /= channels; // Average channels to mono
                
                // Clamp and convert to 16-bit
                sample = Math.Max(-1f, Math.Min(1f, sample));
                short pcmSample = (short)(sample * 32767);
                
                // Write to output
                output[i * 2] = (byte)(pcmSample & 0xFF);
                output[i * 2 + 1] = (byte)((pcmSample >> 8) & 0xFF);
            }
            
            return output;
        }
        
        private void OnWasapiDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (waveWriter == null || !isRecording || wasapiCapture == null) return;
            
            try
            {
                // Resample to Whisper format (16kHz mono)
                var resampledData = ResampleToWhisperFormat(e.Buffer.Take(e.BytesRecorded).ToArray(), wasapiCapture.WaveFormat);
                
                if (resampledData.Length == 0)
                {
                    Debug.WriteLine("[Whisper] Warning: Resampled data is empty");
                    return;
                }
                
                // Calculate audio level (RMS) from resampled data
                double sum = 0;
                for (int i = 0; i < resampledData.Length; i += 2)
                {
                    if (i + 1 < resampledData.Length)
                    {
                        short sample = (short)(resampledData[i] | (resampledData[i + 1] << 8));
                        sum += sample * sample;
                    }
                }
                double rms = Math.Sqrt(sum / Math.Max(1, resampledData.Length / 2));
                
                // Debug: Log audio level periodically
                var elapsed = (DateTime.Now - recordingStartTime).TotalSeconds;
                if ((int)(elapsed * 4) % 4 == 0) // Every 0.25 seconds
                {
                    Debug.WriteLine($"[Whisper] WASAPI audio: {e.BytesRecorded} bytes, level: {rms:F0} (threshold: {silenceThreshold}, speech: {hasDetectedSpeech})");
                }
                
                // Check if there's sound
                bool hasSoundNow = rms > silenceThreshold;
                
                // Fire audio level event for UI feedback (green when hearing)
                AudioLevelChanged?.Invoke(this, (rms, silenceThreshold, hasSoundNow));
                
                if (hasSoundNow)
                {
                    consecutiveSpeechFrames++;
                    lastSoundTime = DateTime.Now;
                    
                    // FIXED: Only mark as "speech detected" after consecutive frames of real audio
                    // This prevents random noise spikes from triggering false positives
                    if (!hasDetectedSpeech && consecutiveSpeechFrames >= MinSpeechFramesRequired)
                    {
                        hasDetectedSpeech = true;
                        Debug.WriteLine($"[Whisper] Speech CONFIRMED (WASAPI) at level {rms:F0} after {consecutiveSpeechFrames} frames - writing pre-buffer");
                        
                        // Write the pre-buffer to capture audio from BEFORE speech was detected
                        if (!preBufferWritten)
                        {
                            preBufferWritten = true;
                            if (preBufferFull)
                            {
                                waveWriter.Write(preBuffer, preBufferWritePos, preBuffer.Length - preBufferWritePos);
                                waveWriter.Write(preBuffer, 0, preBufferWritePos);
                            }
                            else if (preBufferWritePos > 0)
                            {
                                waveWriter.Write(preBuffer, 0, preBufferWritePos);
                            }
                        }
                    }
                }
                else
                {
                    // Reset consecutive speech counter when silence detected
                    consecutiveSpeechFrames = 0;
                }
                
                // If speech detected, write directly to the main buffer
                if (hasDetectedSpeech)
                {
                    waveWriter.Write(resampledData, 0, resampledData.Length);
                }
                else
                {
                    // No speech yet - store in circular pre-buffer
                    int bytesToWrite = resampledData.Length;
                    int srcOffset = 0;
                    
                    while (bytesToWrite > 0)
                    {
                        int spaceLeft = preBuffer.Length - preBufferWritePos;
                        int writeNow = Math.Min(bytesToWrite, spaceLeft);
                        
                        Buffer.BlockCopy(resampledData, srcOffset, preBuffer, preBufferWritePos, writeNow);
                        
                        preBufferWritePos += writeNow;
                        srcOffset += writeNow;
                        bytesToWrite -= writeNow;
                        
                        // Wrap around
                        if (preBufferWritePos >= preBuffer.Length)
                        {
                            preBufferWritePos = 0;
                            preBufferFull = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Whisper] OnWasapiDataAvailable error: {ex.Message}");
            }
        }
        
        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (waveWriter == null || !isRecording) return;
            
            // Calculate audio level (RMS) first
            double sum = 0;
            for (int i = 0; i < e.BytesRecorded; i += 2)
            {
                short sample = (short)(e.Buffer[i] | (e.Buffer[i + 1] << 8));
                sum += sample * sample;
            }
            double rms = Math.Sqrt(sum / Math.Max(1, e.BytesRecorded / 2));
            
            // Debug: Log audio level every second
            var elapsed = (DateTime.Now - recordingStartTime).TotalSeconds;
            if ((int)(elapsed * 4) % 4 == 0 && e.BytesRecorded > 0) // Every 0.25 seconds
            {
                Debug.WriteLine($"[Whisper] WaveIn audio: {e.BytesRecorded} bytes, level: {rms:F0} (threshold: {silenceThreshold}, speech: {hasDetectedSpeech})");
            }
            
            // Check if there's sound
            bool hasSoundNow = rms > silenceThreshold;
            
            // Fire audio level event for UI feedback (green when hearing)
            AudioLevelChanged?.Invoke(this, (rms, silenceThreshold, hasSoundNow));
            
            if (hasSoundNow)
            {
                consecutiveSpeechFrames++;
                lastSoundTime = DateTime.Now;
                
                // FIXED: Only mark as "speech detected" after consecutive frames of real audio
                // This prevents random noise spikes from triggering false positives
                if (!hasDetectedSpeech && consecutiveSpeechFrames >= MinSpeechFramesRequired)
                {
                    hasDetectedSpeech = true;
                    Debug.WriteLine($"[Whisper] Speech CONFIRMED at level {rms:F0} after {consecutiveSpeechFrames} frames - writing pre-buffer");
                    
                    // Write the pre-buffer to capture audio from BEFORE speech was detected
                    if (!preBufferWritten)
                    {
                        preBufferWritten = true;
                        if (preBufferFull)
                        {
                            // Write from current position to end, then from start to current position
                            waveWriter.Write(preBuffer, preBufferWritePos, preBuffer.Length - preBufferWritePos);
                            waveWriter.Write(preBuffer, 0, preBufferWritePos);
                        }
                        else if (preBufferWritePos > 0)
                        {
                            // Write from start to current position
                            waveWriter.Write(preBuffer, 0, preBufferWritePos);
                        }
                    }
                }
            }
            else
            {
                // Reset consecutive speech counter when silence detected
                consecutiveSpeechFrames = 0;
            }
            
            // If speech detected, write directly to the main buffer
            if (hasDetectedSpeech)
            {
                waveWriter.Write(e.Buffer, 0, e.BytesRecorded);
            }
            else
            {
                // No speech yet - store in circular pre-buffer
                int bytesToWrite = e.BytesRecorded;
                int srcOffset = 0;
                
                while (bytesToWrite > 0)
                {
                    int spaceLeft = preBuffer.Length - preBufferWritePos;
                    int writeNow = Math.Min(bytesToWrite, spaceLeft);
                    
                    Buffer.BlockCopy(e.Buffer, srcOffset, preBuffer, preBufferWritePos, writeNow);
                    
                    preBufferWritePos += writeNow;
                    srcOffset += writeNow;
                    bytesToWrite -= writeNow;
                    
                    // Wrap around
                    if (preBufferWritePos >= preBuffer.Length)
                    {
                        preBufferWritePos = 0;
                        preBufferFull = true;
                    }
                }
            }
        }
        
        private async void CheckSilence(object? state)
        {
            if (!isRecording) return;
            
            var elapsed = (DateTime.Now - recordingStartTime).TotalSeconds;
            var silenceDuration = (DateTime.Now - lastSoundTime).TotalSeconds;
            
            // Debug logging every second to track silence detection
            if ((int)elapsed != (int)(elapsed - 0.1))
            {
                Debug.WriteLine($"[Whisper] CheckSilence: elapsed={elapsed:F1}s, silence={silenceDuration:F1}s, hasSpoken={hasDetectedSpeech}, timeout={silenceTimeoutSeconds}s");
            }
            
            // Max recording time reached
            if (elapsed > maxRecordingSeconds)
            {
                Debug.WriteLine("[Whisper] Max recording time reached");
                await StopAndTranscribeAsync();
                return;
            }
            
            // For wake word mode: if no speech detected after noSpeechTimeoutSeconds, restart
            if (!hasDetectedSpeech && elapsed > noSpeechTimeoutSeconds)
            {
                Debug.WriteLine($"[Whisper] No speech detected after {noSpeechTimeoutSeconds}s, restarting...");
                // Don't transcribe, just restart
                silenceTimer?.Dispose();
                silenceTimer = null;
                isRecording = false;
                wasapiCapture?.StopRecording();
                waveIn?.StopRecording();
                Cleanup();
                
                // Notify audio coordinator that recording has stopped
                AudioCoordinator.NotifyVoiceRecordingStopped();
                
                RecognitionError?.Invoke(this, "no_speech"); // Signal to restart
                RecognitionComplete?.Invoke(this, EventArgs.Empty); // IMPORTANT: Signal completion for restart
                return;
            }
            
            // Check for silence after speech was detected
            // IMPROVED: More aggressive silence detection - stop as soon as user pauses
            if (hasDetectedSpeech)
            {
                if (silenceDuration > silenceTimeoutSeconds)
                {
                    Debug.WriteLine($"[Whisper] ✅ SILENCE DETECTED ({silenceDuration:F1}s > {silenceTimeoutSeconds}s), auto-stopping...");
                    await StopAndTranscribeAsync();
                }
            }
            // IMPROVED: Also check if we've been recording for a while with minimal speech
            // This handles cases where user spoke briefly then stopped
            else if (elapsed > 3.0 && silenceDuration > 2.0)
            {
                // Been recording 3+ seconds but 2+ seconds of silence - user probably isn't speaking
                Debug.WriteLine($"[Whisper] Extended silence without speech ({silenceDuration:F1}s), stopping...");
                silenceTimer?.Dispose();
                silenceTimer = null;
                isRecording = false;
                wasapiCapture?.StopRecording();
                waveIn?.StopRecording();
                Cleanup();
                
                AudioCoordinator.NotifyVoiceRecordingStopped();
                RecognitionError?.Invoke(this, "no_speech");
                RecognitionComplete?.Invoke(this, EventArgs.Empty);
            }
        }
        
        private async Task StopAndTranscribeAsync()
        {
            silenceTimer?.Dispose();
            silenceTimer = null;
            
            if (!isRecording) return;
            isRecording = false;
            
            try
            {
                wasapiCapture?.StopRecording();
                waveIn?.StopRecording();
                waveWriter?.Flush();
                
                RecordingStopped?.Invoke(this, EventArgs.Empty);
                Debug.WriteLine("[Whisper] Recording stopped, transcribing...");
                
                // IMPROVED: More lenient check - if we have audio data, try to transcribe it
                // even if hasDetectedSpeech is false (might have been a quiet utterance)
                var bufferLength = audioBuffer?.Length ?? 0;
                Debug.WriteLine($"[Whisper] Buffer check: length={bufferLength}, hasDetectedSpeech={hasDetectedSpeech}");
                
                if (audioBuffer != null && bufferLength > 1000)
                {
                    var audioData = audioBuffer.ToArray();
                    Debug.WriteLine($"[Whisper] Audio buffer size: {audioData.Length} bytes, attempting transcription...");
                    
                    // CRITICAL FIX: Add overall timeout to prevent indefinite hangs
                    string? text = null;
                    try
                    {
                        using var overallCts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                        var transcribeTask = TranscribeAsync(audioData);
                        var completedTask = await Task.WhenAny(transcribeTask, Task.Delay(20000, overallCts.Token));
                        
                        if (completedTask == transcribeTask)
                        {
                            text = await transcribeTask;
                        }
                        else
                        {
                            Debug.WriteLine("[Whisper] Overall transcription timeout (20s)");
                            text = null;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        Debug.WriteLine("[Whisper] Transcription cancelled");
                        text = null;
                    }
                    
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        Debug.WriteLine($"[Whisper] Transcribed: {text}");
                        SpeechRecognized?.Invoke(this, text);
                    }
                    else
                    {
                        Debug.WriteLine("[Whisper] Transcription returned empty or timed out");
                        RecognitionError?.Invoke(this, "Couldn't understand - try again");
                    }
                }
                else
                {
                    Debug.WriteLine($"[Whisper] No valid audio: buffer={bufferLength}, speech={hasDetectedSpeech}");
                    RecognitionError?.Invoke(this, "no_speech");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Whisper] StopAndTranscribe error: {ex.Message}");
                RecognitionError?.Invoke(this, $"Error: {ex.Message}");
            }
            finally
            {
                Cleanup();
                
                // Notify audio coordinator that recording has stopped
                AudioCoordinator.NotifyVoiceRecordingStopped();
                
                // Signal that recognition cycle is complete (for continuous listening)
                RecognitionComplete?.Invoke(this, EventArgs.Empty);
            }
        }
        
        public async Task StopRecordingAndTranscribeAsync()
        {
            await StopAndTranscribeAsync();
        }

        
        private async Task<string?> TranscribeAsync(byte[] audioData)
        {
            try
            {
                Debug.WriteLine($"[Whisper] Sending {audioData.Length} bytes to API...");
                
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15)); // 15 second timeout
                using var content = new MultipartFormDataContent();
                
                var audioContent = new ByteArrayContent(audioData);
                audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
                content.Add(audioContent, "file", "audio.wav");
                content.Add(new StringContent("whisper-1"), "model");
                content.Add(new StringContent("en"), "language");
                // Add prompt to improve accuracy for voice commands
                content.Add(new StringContent("Voice command for AI assistant. Commands like: open spotify, play music, open chrome, search for, what time is it, set volume, open notepad, play Kevin and Perry."), "prompt");
                
                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/audio/transcriptions");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Content = content;
                
                var response = await httpClient.SendAsync(request, cts.Token);
                var responseText = await response.Content.ReadAsStringAsync(cts.Token);
                
                Debug.WriteLine($"[Whisper] API response status: {response.StatusCode}");
                
                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[Whisper] API error: {responseText}");
                    return null;
                }
                
                var doc = System.Text.Json.JsonDocument.Parse(responseText);
                if (doc.RootElement.TryGetProperty("text", out var textElement))
                {
                    var text = textElement.GetString()?.Trim();
                    Debug.WriteLine($"[Whisper] Got text: {text}");
                    return text;
                }
                
                Debug.WriteLine("[Whisper] No text in response");
                return null;
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[Whisper] Transcription timed out");
                return null;
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"[Whisper] Network error: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Whisper] Transcribe error: {ex.Message}");
                return null;
            }
        }
        
        private void Cleanup()
        {
            try
            {
                silenceTimer?.Dispose();
                silenceTimer = null;
                
                if (wasapiCapture != null)
                {
                    wasapiCapture.DataAvailable -= OnWasapiDataAvailable;
                    wasapiCapture.Dispose();
                    wasapiCapture = null;
                }
                
                if (waveIn != null)
                {
                    waveIn.DataAvailable -= OnDataAvailable;
                    waveIn.Dispose();
                    waveIn = null;
                }
                
                waveWriter?.Dispose();
                waveWriter = null;
                
                audioBuffer?.Dispose();
                audioBuffer = null;
            }
            catch { }
        }
        
        public void Dispose()
        {
            isRecording = false;
            Cleanup();
        }
    }
}
