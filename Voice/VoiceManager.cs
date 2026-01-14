using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

namespace AtlasAI.Voice
{
    /// <summary>
    /// Central voice management system for Atlas AI.
    /// Handles provider switching, voice selection, playback, and caching.
    /// </summary>
    public class VoiceManager : IDisposable
    {
        private readonly Dictionary<VoiceProviderType, IVoiceProvider> _providers;
        private readonly MediaPlayer _mediaPlayer;
        private readonly string _cacheDir;
        private readonly string _settingsPath;
        
        private IVoiceProvider _activeProvider;
        private VoiceInfo? _selectedVoice;
        private bool _speechEnabled = true;
        private double _volume = 1.0;
        private double _rate = 1.0;
        private CancellationTokenSource? _playbackCts;

        public event EventHandler? SpeechStarted;
        public event EventHandler? SpeechEnded;
        public event EventHandler<string>? SpeechError;
        public event EventHandler<VoiceProviderType>? ProviderChanged;

        public VoiceManager()
        {
            _mediaPlayer = new MediaPlayer();
            _mediaPlayer.MediaEnded += (s, e) => OnSpeechEnded();
            _mediaPlayer.MediaFailed += (s, e) => SpeechError?.Invoke(this, e.ErrorException?.Message ?? "Playback failed");

            _cacheDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AtlasAI", "voice_cache");
            _settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AtlasAI", "voice_settings.json");

            // Initialize providers
            _providers = new Dictionary<VoiceProviderType, IVoiceProvider>
            {
                { VoiceProviderType.WindowsSAPI, new WindowsSapiProvider() },
                { VoiceProviderType.EdgeTTS, new EdgeTtsProvider() },
                { VoiceProviderType.OpenAI, new OpenAITtsProvider() },
                { VoiceProviderType.ElevenLabs, new ElevenLabsProvider() }
            };

            // Default to Windows SAPI for instant response (no delay!)
            _activeProvider = _providers[VoiceProviderType.WindowsSAPI];

            // Ensure cache directory exists
            Directory.CreateDirectory(_cacheDir);
            
            LoadSettings();
        }

        // Properties
        public bool SpeechEnabled
        {
            get => _speechEnabled;
            set
            {
                _speechEnabled = value;
                if (!value) Stop();
                SaveSettings();
            }
        }

        public double Volume
        {
            get => _volume;
            set
            {
                _volume = Math.Clamp(value, 0.0, 1.0);
                _mediaPlayer.Volume = _volume;
                SaveSettings();
            }
        }

        public double Rate
        {
            get => _rate;
            set
            {
                _rate = Math.Clamp(value, 0.5, 2.0);
                SaveSettings();
            }
        }

        public VoiceProviderType ActiveProviderType => _activeProvider.ProviderType;
        public VoiceInfo? SelectedVoice => _selectedVoice;
        public bool IsCloudVoice => _selectedVoice?.IsCloud ?? false;
        public bool IsSpeaking { get; private set; }

        /// <summary>Get all registered providers</summary>
        public IEnumerable<IVoiceProvider> GetProviders() => _providers.Values;

        /// <summary>Get a specific provider</summary>
        public IVoiceProvider GetProvider(VoiceProviderType type) => _providers[type];

        /// <summary>Refresh voices from the active provider (clears cache)</summary>
        public void RefreshVoices()
        {
            if (_activeProvider is ElevenLabsProvider elevenLabs)
            {
                elevenLabs.RefreshVoices();
            }
            // Other providers can be added here if they support refresh
        }

        /// <summary>Switch to a different voice provider</summary>
        public async Task<bool> SetProviderAsync(VoiceProviderType type, CancellationToken ct = default)
        {
            System.Diagnostics.Debug.WriteLine($"[VoiceManager] SetProviderAsync called for {type}");
            
            if (!_providers.TryGetValue(type, out var provider))
            {
                System.Diagnostics.Debug.WriteLine($"[VoiceManager] Provider {type} not found");
                return false;
            }

            if (!await provider.IsAvailableAsync(ct))
            {
                System.Diagnostics.Debug.WriteLine($"[VoiceManager] Provider {type} not available");
                return false;
            }

            Stop();
            _activeProvider = provider;
            
            // Try to restore saved voice first
            string? savedVoiceId = null;
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("voiceId", out var vid))
                        savedVoiceId = vid.GetString();
                    System.Diagnostics.Debug.WriteLine($"[VoiceManager] Found saved voiceId: {savedVoiceId}");
                }
            }
            catch { }
            
            // Get available voices
            var voices = await provider.GetVoicesAsync(ct);
            
            // Try to use saved voice if it exists for this provider
            if (!string.IsNullOrEmpty(savedVoiceId))
            {
                var savedVoice = voices.FirstOrDefault(v => v.Id == savedVoiceId);
                if (savedVoice != null)
                {
                    _selectedVoice = savedVoice;
                    System.Diagnostics.Debug.WriteLine($"[VoiceManager] Restored saved voice: {_selectedVoice.DisplayName}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[VoiceManager] Saved voice '{savedVoiceId}' not found in provider {type}");
                    _selectedVoice = null; // Will fall through to default selection
                }
            }
            
            // If no saved voice or saved voice not found, select default
            if (_selectedVoice == null)
            {
                // For ElevenLabs, prefer the Atlas AI voice
                if (type == VoiceProviderType.ElevenLabs)
                {
                    _selectedVoice = voices.FirstOrDefault(v => v.Id == "atQICwskSXjGu0SZpOep") // Atlas AI voice ID
                                  ?? voices.FirstOrDefault(v => v.DisplayName?.Contains("Atlas", StringComparison.OrdinalIgnoreCase) == true)
                                  ?? voices.FirstOrDefault();
                }
                else
                {
                    _selectedVoice = voices.FirstOrDefault();
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"[VoiceManager] Provider set to {type}, selected voice: {_selectedVoice?.DisplayName ?? "NULL"}");
            
            ProviderChanged?.Invoke(this, type);
            // Don't save settings here - only save when user explicitly changes voice
            return true;
        }

        /// <summary>Configure a provider with settings (e.g., API key)</summary>
        public void ConfigureProvider(VoiceProviderType type, Dictionary<string, string> settings)
        {
            if (_providers.TryGetValue(type, out var provider))
            {
                provider.Configure(settings);
            }
        }

        /// <summary>Get all voices from the active provider</summary>
        public Task<IReadOnlyList<VoiceInfo>> GetVoicesAsync(CancellationToken ct = default)
        {
            return _activeProvider.GetVoicesAsync(ct);
        }

        /// <summary>Select a voice by ID</summary>
        public async Task<bool> SelectVoiceAsync(string voiceId, CancellationToken ct = default)
        {
            var voices = await _activeProvider.GetVoicesAsync(ct);
            var voice = voices.FirstOrDefault(v => v.Id == voiceId);
            if (voice != null)
            {
                _selectedVoice = voice;
                SaveSettings();
                return true;
            }
            return false;
        }

        /// <summary>Restore the saved voice from settings file (call after API keys are configured)</summary>
        public async Task RestoreSavedVoiceAsync()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    
                    if (root.TryGetProperty("voiceId", out var vid))
                    {
                        var voiceId = vid.GetString();
                        if (!string.IsNullOrEmpty(voiceId))
                        {
                            var success = await SelectVoiceAsync(voiceId);
                            System.Diagnostics.Debug.WriteLine($"[VoiceManager] Restored saved voice '{voiceId}': {(success ? "success" : "failed")}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VoiceManager] Error restoring saved voice: {ex.Message}");
            }
        }

        /// <summary>Speak text using the current voice</summary>
        public async Task SpeakAsync(string text, CancellationToken ct = default)
        {
            System.Diagnostics.Debug.WriteLine($"[VoiceManager] SpeakAsync called - Enabled: {_speechEnabled}, Voice: {_selectedVoice?.DisplayName ?? "NULL"}, Text length: {text?.Length ?? 0}");
            
            if (!_speechEnabled || string.IsNullOrWhiteSpace(text))
            {
                System.Diagnostics.Debug.WriteLine($"[VoiceManager] Skipping - speech disabled or empty text");
                return;
            }
            
            // Clean text for TTS - remove markdown formatting
            text = CleanTextForTTS(text);
            
            if (string.IsNullOrWhiteSpace(text))
            {
                System.Diagnostics.Debug.WriteLine($"[VoiceManager] Skipping - text empty after cleaning");
                return;
            }
            
            // Auto-select a voice if none selected
            if (_selectedVoice == null)
            {
                System.Diagnostics.Debug.WriteLine($"[VoiceManager] No voice selected, auto-selecting...");
                var voices = await _activeProvider.GetVoicesAsync(ct);
                _selectedVoice = voices.FirstOrDefault();
                if (_selectedVoice == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[VoiceManager] No voices available from provider {_activeProvider.ProviderType}");
                    SpeechError?.Invoke(this, "No voices available");
                    return;
                }
                System.Diagnostics.Debug.WriteLine($"[VoiceManager] Auto-selected voice: {_selectedVoice.DisplayName}");
            }

            Stop();
            _playbackCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            try
            {
                IsSpeaking = true;
                SpeechStarted?.Invoke(this, EventArgs.Empty);

                var options = new SynthesisOptions
                {
                    VoiceId = _selectedVoice.Id,
                    Rate = _rate,
                    Volume = _volume
                };

                // Windows SAPI plays directly - no file needed
                if (_activeProvider.ProviderType == VoiceProviderType.WindowsSAPI)
                {
                    var result = await _activeProvider.SynthesizeAsync(text, options, _playbackCts.Token);
                    if (!result.Success)
                    {
                        SpeechError?.Invoke(this, result.ErrorMessage ?? "Synthesis failed");
                    }
                    // SAPI handles playback internally, so we're done
                    return;
                }

                // For other providers, check cache first
                var cacheKey = GetCacheKey(text, _selectedVoice.Id, _rate);
                var cachedFile = Path.Combine(_cacheDir, $"{cacheKey}.mp3");

                string audioFile;
                if (File.Exists(cachedFile))
                {
                    audioFile = cachedFile;
                }
                else
                {
                    var result = await _activeProvider.SynthesizeAsync(text, options, _playbackCts.Token);
                    
                    if (!result.Success)
                    {
                        SpeechError?.Invoke(this, result.ErrorMessage ?? "Synthesis failed");
                        return;
                    }

                    audioFile = result.AudioFilePath!;

                    // Cache short phrases (under 200 chars)
                    if (text.Length < 200 && File.Exists(audioFile))
                    {
                        try { File.Copy(audioFile, cachedFile, true); } catch { }
                    }
                }

                // Play audio
                await PlayAudioAsync(audioFile, _playbackCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Cancelled - normal
                System.Diagnostics.Debug.WriteLine($"[VoiceManager] Speech cancelled");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VoiceManager] Speech error: {ex.Message}");
                SpeechError?.Invoke(this, ex.Message);
            }
            finally
            {
                IsSpeaking = false;
                _playbackCts = null;
                OnSpeechEnded();
            }
        }
        
        /// <summary>
        /// Clean text for TTS - removes markdown formatting, limits length, and normalizes for speech
        /// </summary>
        private string CleanTextForTTS(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            
            // Remove markdown bold/italic (asterisks)
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\*\*\*(.+?)\*\*\*", "$1"); // ***bold italic***
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\*\*(.+?)\*\*", "$1"); // **bold**
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\*(.+?)\*", "$1"); // *italic*
            text = System.Text.RegularExpressions.Regex.Replace(text, @"__(.+?)__", "$1"); // __bold__
            text = System.Text.RegularExpressions.Regex.Replace(text, @"_(.+?)_", "$1"); // _italic_
            
            // Remove markdown headers
            text = System.Text.RegularExpressions.Regex.Replace(text, @"^#{1,6}\s*", "", System.Text.RegularExpressions.RegexOptions.Multiline);
            
            // Remove markdown links [text](url) -> text
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\[([^\]]+)\]\([^\)]+\)", "$1");
            
            // Remove markdown images ![alt](url)
            text = System.Text.RegularExpressions.Regex.Replace(text, @"!\[([^\]]*)\]\([^\)]+\)", "");
            
            // Remove code blocks ```code```
            text = System.Text.RegularExpressions.Regex.Replace(text, @"```[\s\S]*?```", "");
            
            // Remove inline code `code`
            text = System.Text.RegularExpressions.Regex.Replace(text, @"`([^`]+)`", "$1");
            
            // Remove bullet points and list markers
            text = System.Text.RegularExpressions.Regex.Replace(text, @"^[\s]*[-*+]\s+", "", System.Text.RegularExpressions.RegexOptions.Multiline);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"^[\s]*\d+\.\s+", "", System.Text.RegularExpressions.RegexOptions.Multiline);
            
            // Remove blockquotes
            text = System.Text.RegularExpressions.Regex.Replace(text, @"^>\s*", "", System.Text.RegularExpressions.RegexOptions.Multiline);
            
            // Remove horizontal rules
            text = System.Text.RegularExpressions.Regex.Replace(text, @"^[-*_]{3,}$", "", System.Text.RegularExpressions.RegexOptions.Multiline);
            
            // Remove emoji shortcodes :emoji:
            text = System.Text.RegularExpressions.Regex.Replace(text, @":[a-zA-Z0-9_+-]+:", "");
            
            // Clean up multiple newlines
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\n{3,}", "\n\n");
            
            // Clean up multiple spaces
            text = System.Text.RegularExpressions.Regex.Replace(text, @"[ \t]{2,}", " ");
            
            // Trim
            text = text.Trim();
            
            // Limit length for TTS - very long messages take forever to synthesize
            // Keep first ~2500 chars (about 3-4 minutes of speech max)
            const int maxLength = 2500;
            if (text.Length > maxLength)
            {
                // Try to cut at a sentence boundary
                var cutPoint = text.LastIndexOf('.', maxLength);
                if (cutPoint < maxLength / 2) cutPoint = text.LastIndexOf(' ', maxLength);
                if (cutPoint < maxLength / 2) cutPoint = maxLength;
                
                text = text.Substring(0, cutPoint).Trim();
                // Don't add "continued in text" - just truncate cleanly
            }
            
            System.Diagnostics.Debug.WriteLine($"[VoiceManager] Cleaned text for TTS: {text.Length} chars");
            return text;
        }

        /// <summary>Stop current speech</summary>
        public void Stop()
        {
            _playbackCts?.Cancel();
            _activeProvider.CancelCurrentSpeech();
            _mediaPlayer.Stop();
            IsSpeaking = false;
            OnSpeechEnded();
        }

        /// <summary>Clear the voice cache</summary>
        public void ClearCache()
        {
            try
            {
                foreach (var file in Directory.GetFiles(_cacheDir, "*.mp3"))
                {
                    try { File.Delete(file); } catch { }
                }
            }
            catch { }
        }

        private async Task PlayAudioAsync(string audioFile, CancellationToken ct)
        {
            var tcs = new TaskCompletionSource<bool>();
            var openedTcs = new TaskCompletionSource<bool>();
            
            void OnOpened(object? s, EventArgs e) => openedTcs.TrySetResult(true);
            void OnEnded(object? s, EventArgs e) => tcs.TrySetResult(true);
            void OnFailed(object? s, ExceptionEventArgs e) 
            {
                openedTcs.TrySetException(e.ErrorException);
                tcs.TrySetException(e.ErrorException);
            }

            _mediaPlayer.MediaOpened += OnOpened;
            _mediaPlayer.MediaEnded += OnEnded;
            _mediaPlayer.MediaFailed += OnFailed;

            try
            {
                _mediaPlayer.Open(new Uri(audioFile));
                _mediaPlayer.Volume = _volume;
                
                // Wait for media to be fully loaded before playing (fixes audio cutout at start)
                using var openReg = ct.Register(() => openedTcs.TrySetCanceled());
                try
                {
                    // Wait up to 2 seconds for media to open (increased from 500ms)
                    var openTask = openedTcs.Task;
                    var timeoutTask = Task.Delay(2000, ct);
                    await Task.WhenAny(openTask, timeoutTask);
                    
                    // If opened successfully, wait a bit more for the audio device to be ready
                    if (openTask.IsCompletedSuccessfully)
                    {
                        System.Diagnostics.Debug.WriteLine("[VoiceManager] Media opened successfully, waiting for audio device...");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[VoiceManager] Error waiting for media open: {ex.Message}");
                }
                
                // Increased buffer to ensure audio device is ready and no audio is cut off
                // This is critical for Bluetooth/USB audio devices which have higher latency
                await Task.Delay(150, ct);
                
                System.Diagnostics.Debug.WriteLine("[VoiceManager] Starting playback...");
                _mediaPlayer.Play();

                using var reg = ct.Register(() => 
                {
                    _mediaPlayer.Stop();
                    tcs.TrySetCanceled();
                });

                await tcs.Task;
            }
            finally
            {
                _mediaPlayer.MediaOpened -= OnOpened;
                _mediaPlayer.MediaEnded -= OnEnded;
                _mediaPlayer.MediaFailed -= OnFailed;
            }
        }

        private void OnSpeechEnded()
        {
            IsSpeaking = false;
            SpeechEnded?.Invoke(this, EventArgs.Empty);
        }

        private string GetCacheKey(string text, string voiceId, double rate)
        {
            var input = $"{text}|{voiceId}|{rate:F1}";
            using var sha = System.Security.Cryptography.SHA256.Create();
            var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(hash)[..16];
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("speechEnabled", out var se))
                        _speechEnabled = se.GetBoolean();
                    if (root.TryGetProperty("volume", out var vol))
                        _volume = vol.GetDouble();
                    if (root.TryGetProperty("rate", out var rate))
                        _rate = rate.GetDouble();
                    if (root.TryGetProperty("provider", out var prov) &&
                        Enum.TryParse<VoiceProviderType>(prov.GetString(), out var provType))
                    {
                        _activeProvider = _providers[provType];
                    }
                    if (root.TryGetProperty("voiceId", out var vid))
                    {
                        var voiceId = vid.GetString();
                        if (!string.IsNullOrEmpty(voiceId))
                        {
                            _ = SelectVoiceAsync(voiceId);
                        }
                    }
                }
            }
            catch { }

            // Ensure we have a selected voice - prefer Atlas for ElevenLabs
            if (_selectedVoice == null)
            {
                _ = Task.Run(async () =>
                {
                    var voices = await _activeProvider.GetVoicesAsync();
                    if (_activeProvider.ProviderType == VoiceProviderType.ElevenLabs)
                    {
                        _selectedVoice = voices.FirstOrDefault(v => v.Id == "atQICwskSXjGu0SZpOep") // Atlas AI
                                      ?? voices.FirstOrDefault(v => v.DisplayName?.Contains("Atlas", StringComparison.OrdinalIgnoreCase) == true)
                                      ?? voices.FirstOrDefault();
                    }
                    else
                    {
                        _selectedVoice = voices.FirstOrDefault();
                    }
                });
            }
        }

        private void SaveSettings()
        {
            try
            {
                var settings = new
                {
                    speechEnabled = _speechEnabled,
                    volume = _volume,
                    rate = _rate,
                    provider = _activeProvider.ProviderType.ToString(),
                    voiceId = _selectedVoice?.Id
                };

                var dir = Path.GetDirectoryName(_settingsPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir!);
                
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsPath, json);
            }
            catch { }
        }

        public void Dispose()
        {
            Stop();
            _mediaPlayer.Close();
        }
    }
}
