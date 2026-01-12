using System;
using System.Diagnostics;
using System.Speech.Recognition;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;

namespace MinimalApp.Voice
{
    public class WakeWordDetector : IDisposable
    {
        private SpeechRecognitionEngine? _recognizer;
        private bool _isListening = false;
        private bool _isDisposed = false;
        
        public event EventHandler<string>? WakeWordDetected;
        public event EventHandler<string>? Error;
        public event EventHandler<string>? AudioStateChanged;
        
        public bool IsListening => _isListening;
        
        public static string GetDefaultRecordingDeviceName()
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                var defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
                return defaultDevice?.FriendlyName ?? "Unknown";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WakeWordDetector] Failed to get default device: {ex.Message}");
                return "Unknown";
            }
        }
        
        public WakeWordDetector()
        {
            Initialize();
        }
        
        private void Initialize()
        {
            try
            {
                Debug.WriteLine("[WakeWordDetector] INITIALIZING WAKE WORD DETECTION");
                _recognizer = new SpeechRecognitionEngine();
                Debug.WriteLine($"[WakeWordDetector] Engine created: {_recognizer.RecognizerInfo.Name}");
                
                var wakeWords = new Choices(new string[] { "Atlas", "atlas", "at last", "Hey Atlas", "OK Atlas" });
                var grammarBuilder = new GrammarBuilder(wakeWords);
                grammarBuilder.Culture = _recognizer.RecognizerInfo.Culture;
                var grammar = new Grammar(grammarBuilder);
                grammar.Name = "WakeWord";
                _recognizer.LoadGrammar(grammar);
                
                _recognizer.SetInputToDefaultAudioDevice();
                var defaultDevice = GetDefaultRecordingDeviceName();
                Debug.WriteLine($"[WakeWordDetector] Using Windows default mic: {defaultDevice}");
                
                _recognizer.BabbleTimeout = TimeSpan.FromSeconds(0);
                _recognizer.InitialSilenceTimeout = TimeSpan.FromSeconds(0);
                _recognizer.EndSilenceTimeout = TimeSpan.FromMilliseconds(500);
                _recognizer.EndSilenceTimeoutAmbiguous = TimeSpan.FromMilliseconds(500);
                
                _recognizer.SpeechRecognized += OnSpeechRecognized;
                _recognizer.SpeechRecognitionRejected += OnSpeechRejected;
                _recognizer.RecognizeCompleted += OnRecognizeCompleted;
                _recognizer.AudioStateChanged += OnAudioStateChanged;
                _recognizer.SpeechDetected += OnSpeechDetected;
                _recognizer.SpeechHypothesized += OnSpeechHypothesized;
                
                Debug.WriteLine("[WakeWordDetector] INITIALIZATION COMPLETE");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WakeWordDetector] Init failed: {ex.Message}");
                Error?.Invoke(this, $"Wake word init failed: {ex.Message}");
            }
        }
        
        private void OnSpeechHypothesized(object? sender, SpeechHypothesizedEventArgs e)
        {
            Debug.WriteLine($"[WakeWordDetector] HYPOTHESIS: '{e.Result.Text}' ({e.Result.Confidence:P0})");
        }
        
        public void StartListening()
        {
            if (_recognizer == null || _isDisposed) return;
            
            if (_isListening)
            {
                try { _recognizer.RecognizeAsyncStop(); } catch { }
                _isListening = false;
                System.Threading.Thread.Sleep(100);
            }
            
            try
            {
                var defaultDevice = GetDefaultRecordingDeviceName();
                Debug.WriteLine($"[WakeWordDetector] STARTING - Using: {defaultDevice}");
                _recognizer.SetInputToDefaultAudioDevice();
                _recognizer.RecognizeAsync(RecognizeMode.Multiple);
                _isListening = true;
                Debug.WriteLine("[WakeWordDetector] NOW LISTENING for 'Atlas'");
            }
            catch (InvalidOperationException ex)
            {
                Debug.WriteLine($"[WakeWordDetector] Already recognizing: {ex.Message}");
                _isListening = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WakeWordDetector] Start failed: {ex.Message}");
                _isListening = false;
                Error?.Invoke(this, ex.Message);
            }
        }
        
        public void StopListening()
        {
            if (!_isListening || _recognizer == null) return;
            try
            {
                _recognizer.RecognizeAsyncStop();
                _isListening = false;
            }
            catch { }
        }
        
        private void OnSpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
        {
            Debug.WriteLine($"[WakeWordDetector] RECOGNIZED: '{e.Result.Text}' ({e.Result.Confidence:P0})");
            
            if (e.Result.Confidence < 0.2) return;
            
            var text = e.Result.Text.ToLower();
            if (text.Contains("atlas") || text.Contains("at last"))
            {
                Debug.WriteLine("[WakeWordDetector] *** WAKE WORD DETECTED! ***");
                WakeWordDetected?.Invoke(this, e.Result.Text);
            }
        }
        
        private void OnSpeechRejected(object? sender, SpeechRecognitionRejectedEventArgs e)
        {
            if (e.Result?.Alternates.Count > 0)
                Debug.WriteLine($"[WakeWordDetector] Rejected: '{e.Result.Alternates[0].Text}'");
        }
        
        private void OnAudioStateChanged(object? sender, AudioStateChangedEventArgs e)
        {
            Debug.WriteLine($"[WakeWordDetector] Audio state: {e.AudioState}");
            AudioStateChanged?.Invoke(this, e.AudioState.ToString());
        }
        
        private void OnSpeechDetected(object? sender, SpeechDetectedEventArgs e)
        {
            Debug.WriteLine($"[WakeWordDetector] Speech detected at: {e.AudioPosition}");
        }
        
        private void OnRecognizeCompleted(object? sender, RecognizeCompletedEventArgs e)
        {
            if (_isListening && !_isDisposed && _recognizer != null)
            {
                Task.Delay(100).ContinueWith(_ =>
                {
                    if (_isListening && !_isDisposed && _recognizer != null)
                    {
                        try
                        {
                            _recognizer.SetInputToDefaultAudioDevice();
                            _recognizer.RecognizeAsync(RecognizeMode.Multiple);
                        }
                        catch { }
                    }
                });
            }
        }
        
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            StopListening();
            _recognizer?.Dispose();
            _recognizer = null;
        }
    }
}
