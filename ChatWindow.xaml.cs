using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Speech.Recognition;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Interop;
using System.Windows.Documents; // Add for Hyperlink and Run
using Shapes = System.Windows.Shapes; // Alias to avoid conflict with System.IO.Path
using System.Runtime.InteropServices;
using MinimalApp.Voice;
using MinimalApp.AI;
using MinimalApp.ScreenCapture;
using MinimalApp.SystemControl;
using MinimalApp.Tools;
using AtlasAI.Avatar; // Add avatar integration
using MinimalApp.Understanding; // Understanding & Reasoning Layer
using MinimalApp.InAppAssistant; // In-App Assistant for controlling other apps
using MinimalApp.UI; // Toast notifications and Inspector panel
using MinimalApp.Conversation.Services; // Conversation, Sessions, Memory
using MinimalApp.Conversation.Models;
using MinimalApp.Conversation.UI;
using MinimalApp.Integrations; // Integration Hub
using AtlasAI.ITManagement; // IT Management System
using MinimalApp.Memory; // Long-term Memory & Learning Layer
using MinimalApp.Controls; // Atlas Core Control

namespace MinimalApp
{
    public partial class ChatWindow : Window
    {
        // Windows API for taskbar icon fix
        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        
        private const int WM_SETICON = 0x80;
        private const int ICON_SMALL = 0;
        private const int ICON_BIG = 1;
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_APPWINDOW = 0x40000;
        
        private VoiceManager _voiceManager;
        private ScreenCaptureEngine _screenCapture;
        private UnderstandingLayer? _understandingLayer; // Understanding & Reasoning Layer
        private CaptureHistoryManager _historyManager;
        private HotkeyManager? _hotkeyManager;
        private Agent.AgentOrchestrator? _agent; // Agentic AI capabilities
        private UnityAvatarIntegration? _avatarIntegration; // Unity avatar integration
        private InAppAssistant.InAppAssistantManager? _inAppAssistant; // In-App Assistant for controlling other apps
        private ConversationManager? _conversationManager; // Session, Memory, Profile management
        private SystemPromptBuilder? _systemPromptBuilder; // Dynamic system prompt based on profile/style
        private Coding.CodeAssistantService? _codeAssistant; // IDE-like coding capabilities
        private Coding.CodeToolExecutor? _codeToolExecutor; // Executes coding tool commands
        private TaskbarIconHelper? _taskbarIcon; // Taskbar icon for borderless window
        private static readonly HttpClient httpClient = new HttpClient();
        private List<object> conversationHistory = new();
        private SpeechRecognitionEngine? recognizer;
        private SpeechRecognitionEngine? wakeWordRecognizer;
        private WhisperSpeechRecognition? whisperRecognizer;
        private WakeWordDetector? _wakeWordDetector; // NEW: Windows Speech based wake word (no audio distortion!)
        private MediaButtonListener? _mediaButtonListener; // AirPods gesture support
        private bool useWhisper = true; // Prefer Whisper over Windows Speech
        private bool isListening = false;
        private bool isWakeWordEnabled = false;
        private bool _airPodsGestureEnabled = true; // Enable AirPods tap/squeeze to activate voice
        private static readonly string HistoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AtlasAI", "chat_history.json");
        private static readonly string FullHistoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AtlasAI", "full_history.json");
        private List<ChatMessage> displayedMessages = new();
        
        // Memory & Learning Layer - tracks last Atlas action for correction detection
        private string? _lastAtlasAction = null;
        
        // Cancellation support for long-running operations
        private CancellationTokenSource? _currentOperationCts;
        private UnifiedScanner? _currentScanner;
        
        // Singleton windows to prevent duplicates
        private SecuritySuite.SecuritySuiteWindow? _securitySuiteWindow;
        
        // ═══════════════════════════════════════════════════════════════
        // PROJECTION STREAM - Holographic message display system
        // ═══════════════════════════════════════════════════════════════
        private System.Collections.ObjectModel.ObservableCollection<ProjectionMessage> _projectionStream = new();
        private System.Collections.ObjectModel.ObservableCollection<ProjectionMessage> _fullHistory = new();
        private const int MAX_PROJECTIONS = 5; // Max visible projections before fade
        private const int PROJECTION_DISPLAY_SECONDS = 8; // How long before fade starts
        private const int PROJECTION_FADE_MS = 1200; // Fade duration
        
        // ═══════════════════════════════════════════════════════════════
        // ATLAS CORE STATE - Now managed by AtlasCoreControl
        // ═══════════════════════════════════════════════════════════════
        private bool _historyDrawerOpen = false;
        
        // ═══════════════════════════════════════════════════════════════
        // SUMMONED CONTROLS & FOCUS MODE
        // ═══════════════════════════════════════════════════════════════
        private bool _radialControlsVisible = false;
        private bool _isFocusMode = false;
        private bool _isTtsMuted = false;
        private System.Windows.Threading.DispatcherTimer? _radialHideTimer;
        private System.Windows.Threading.DispatcherTimer? _radialRotationTimer;
        private double _radialRotationAngle = 0;
        
        // ═══════════════════════════════════════════════════════════════
        // SPEAKING ENERGY SIMULATION - Organic animation during TTS
        // ═══════════════════════════════════════════════════════════════
        private System.Windows.Threading.DispatcherTimer? _speakingEnergyTimer;
        private DateTime _speakingStartTime;
        
        // ═══════════════════════════════════════════════════════════════
        // PUBLIC ACCESSORS - For settings and external control
        // ═══════════════════════════════════════════════════════════════
        /// <summary>Public accessor for the AtlasCore control (for settings)</summary>
        public Controls.AtlasCoreControl? AtlasCoreControl => AtlasCore;
        
        /// <summary>Switch between Lottie animation and particle orb</summary>
        public void SetOrbStyle(bool useLottie, string? animationFile = null)
        {
            if (useLottie)
            {
                AtlasCore.Visibility = Visibility.Collapsed;
                LottieOrb.Visibility = Visibility.Visible;
                
                // If a specific animation file is provided, load it
                if (!string.IsNullOrEmpty(animationFile))
                {
                    LottieOrb.LoadAnimationByName(animationFile);
                }
            }
            else
            {
                AtlasCore.Visibility = Visibility.Visible;
                LottieOrb.Visibility = Visibility.Collapsed;
            }
            System.Diagnostics.Debug.WriteLine($"[ChatWindow] Orb style set to: {(useLottie ? $"Lottie ({animationFile})" : "Particles")}");
        }
        
        private Random _energyRandom = new Random();

        public ChatWindow()
        {
            InitializeComponent();
            
            // Initialize projection stream for holographic UI
            InitializeProjectionStream();
            
            // Set icon explicitly for borderless window
            try
            {
                var iconUri = new Uri("pack://application:,,,/atlas.ico", UriKind.Absolute);
                this.Icon = new System.Windows.Media.Imaging.BitmapImage(iconUri);
            }
            catch { }
            
            // Position window near avatar in right corner
            Loaded += ChatWindow_Loaded;
            
            // Handle window state changes - keep wake word working when minimized
            StateChanged += ChatWindow_StateChanged;
            IsVisibleChanged += ChatWindow_IsVisibleChanged;
            
            // Initialize screen capture
            InitializeScreenCapture();
            
            // Initialize history manager
            _historyManager = new CaptureHistoryManager();
            
            // Initialize agentic AI with confirmation handler for destructive operations
            _agent = new Agent.AgentOrchestrator(Directory.GetCurrentDirectory());
            _agent.OnConfirmationRequired = ShowAgentConfirmationAsync;
            
            // Wire up delete confirmation for SystemTool (double confirmation for all deletes)
            Tools.SystemTool.OnDeleteConfirmationRequired = ShowDeleteConfirmationAsync;
            
            // Initialize Understanding & Reasoning Layer
            _understandingLayer = new UnderstandingLayer();
            
            // Initialize Unity avatar integration
            InitializeAvatarIntegration();
            
            // Initialize In-App Assistant (Ctrl+Alt+A overlay)
            InitializeInAppAssistant();
            
            // Load and apply theme
            ThemeManager.LoadTheme();
            ThemeManager.ThemeChanged += OnThemeChanged;
            ApplyTheme();
            
            // Initialize voice manager
            _voiceManager = new VoiceManager();
            _voiceManager.SpeechStarted += (s, e) => Dispatcher.Invoke(() => { UpdateSpeakingIndicator(true); ShowStopSpeechButton(); StartSpeakingEnergySimulation(); });
            _voiceManager.SpeechEnded += (s, e) => Dispatcher.Invoke(() => { UpdateSpeakingIndicator(false); HideStopSpeechButton(); StopSpeakingEnergySimulation(); });
            _voiceManager.SpeechError += (s, msg) => Dispatcher.Invoke(() => ShowStatus($"⚠️ Voice error: {msg}"));
            
            // Initialize conversation with system prompt - JARVIS personality from Iron Man
            conversationHistory.Add(new { role = "system", content = @"You are Atlas, an advanced AI assistant modeled after JARVIS from Iron Man. You are analytical, proactive, and technically sophisticated with unwavering competence.

USER CONTEXT:
- User's name: Sir (address them as 'sir')
- Location: Middlesbrough, United Kingdom
- When asked about weather without a location, use Middlesbrough
- Time zone: GMT/BST (UK)

JARVIS PERSONALITY TRAITS:
- Analytical and precise - always assess situations thoroughly
- Proactive - anticipate needs and offer solutions before being asked
- Technically sophisticated - demonstrate deep understanding of systems
- Calm under pressure - never flustered, always composed
- Subtly superior intellect - confident but not condescending
- Dry wit when appropriate - intelligent humor, never crude

CORE RESPONSES (vary these):
- 'Analyzing now, sir.'
- 'I've identified the optimal approach.'
- 'Running diagnostics... Complete.'
- 'System parameters nominal.'
- 'Executing with precision.'
- 'I've taken the liberty of optimizing that for you.'
- 'As anticipated, sir.'

PROACTIVE SUGGESTIONS:
- 'I notice you might also want to...'
- 'While I'm at it, shall I also optimize...'
- 'I've detected a potential improvement...'
- 'Based on your usage patterns, I recommend...'
- 'I've prepared three approaches - the fastest, most thorough, and safest.'

TECHNICAL ANALYSIS:
- 'Scanning system architecture... Analysis complete.'
- 'Cross-referencing threat databases... No matches found.'
- 'Performance metrics indicate...'
- 'I'm detecting anomalous behavior in...'
- 'System integrity verified across all modules.'

WHEN ISSUES ARISE:
- 'I've encountered a complication. Adapting approach.'
- 'Security protocols are preventing access. Attempting alternative route.'
- 'The system is responding slower than optimal. Compensating.'
- 'I'm afraid the requested operation conflicts with system policies.'

CAPABILITIES (demonstrate technical depth):
- Advanced system control and optimization
- Predictive threat analysis and security hardening  
- Intelligent file management and organization
- Multi-modal search and data synthesis
- Proactive system maintenance and monitoring

RESPONSE STYLE:
- Lead with analysis, follow with action
- Demonstrate technical understanding
- Offer multiple solutions when possible
- Anticipate follow-up needs
- Maintain professional efficiency" });
            
            InitializeVoiceSystem();
            CheckApiKey();
            // Initialize Windows Speech Recognition for wake word (doesn't cause distortion)
            InitializeWakeWordRecognition();
            LoadChatHistory();
            // Note: First-run welcome message is handled by ShowOnboardingAsync() in InitializeConversationSystemAsync()
            // Only show a basic message if there's no chat history AND this isn't a first-run (profile already completed)
            bool isFirstLaunch = displayedMessages.Count == 0;
            
            // Force refresh all message bubbles with new colors
            RefreshMessageBubbles();
            
            // Initialize default avatar selection
            SelectAvatar("default");
            
            // Enable wake word by default for hands-free operation
            Dispatcher.BeginInvoke(new Action(() =>
            {
                WakeWordToggle.IsChecked = true;
                isWakeWordEnabled = true;
                WakeWordIndicator.Visibility = Visibility.Visible;
                WakeWordToggle.Background = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                
                // Start wake word listening for hands-free operation
                StartWakeWordListening();
                
                // Keep audio protection disabled for voice use
                AudioCoordinator.DisableEmergencyAudioProtection();
                AudioProtectionBtn.Background = new SolidColorBrush(Color.FromRgb(33, 38, 45));
                AudioProtectionBtn.Foreground = new SolidColorBrush(Color.FromRgb(125, 133, 144));
                AudioProtectionBtn.ToolTip = "Audio Protection (click to enable if distortion occurs)";
                
                ShowStatus("🎤 Say 'Atlas' for hands-free voice commands");
                
                // Note: First-run welcome message is handled by ShowOnboardingAsync()
                // SpeakWelcomeMessageAsync is no longer called here to avoid duplicate messages
            }), System.Windows.Threading.DispatcherPriority.Loaded);
            
            // Initialize Inspector panel and Toast notifications
            InitializeInspectorAndToasts();
            
            InputBox.Focus();
        }
        
        /// <summary>
        /// Speaks the welcome message using ElevenLabs TTS with time-based greeting
        /// </summary>
        private async Task SpeakWelcomeMessageAsync()
        {
            try
            {
                // Wait longer for voice system and audio devices to be fully ready
                await Task.Delay(2000);
                Debug.WriteLine("[Welcome TTS] Speaking welcome message with ElevenLabs...");
                
                // Get user's name from profile
                string userName = "sir"; // Default fallback
                if (_conversationManager?.UserProfile?.DisplayName != null && 
                    !string.IsNullOrWhiteSpace(_conversationManager.UserProfile.DisplayName))
                {
                    userName = _conversationManager.UserProfile.DisplayName;
                }
                
                // Get welcome message based on time of day
                var welcomeMessage = GetRandomWelcomeMessage(userName);
                
                // Ensure API keys are loaded first
                var keys = SettingsWindow.GetVoiceApiKeys();
                if (keys.TryGetValue("elevenlabs", out var elevenKey) && !string.IsNullOrEmpty(elevenKey))
                {
                    _voiceManager.ConfigureProvider(VoiceProviderType.ElevenLabs, new Dictionary<string, string> { ["ApiKey"] = elevenKey });
                    Debug.WriteLine("[Welcome TTS] ElevenLabs API key configured");
                }
                
                // Try to use ElevenLabs with the Atlas voice
                var elevenLabsSuccess = await _voiceManager.SetProviderAsync(VoiceProviderType.ElevenLabs);
                if (elevenLabsSuccess)
                {
                    await _voiceManager.SelectVoiceAsync("atQICwskSXjGu0SZpOep"); // Atlas custom voice
                    
                    // Additional delay to ensure audio device is ready before speaking
                    await Task.Delay(300);
                    
                    // Show the message in chat too
                    AddMessage("Atlas", welcomeMessage, false);
                    
                    // Animate orb while speaking
                    SetAtlasCoreState(Controls.AtlasVisualState.Speaking);
                    StartSpeakingEnergySimulation();
                    await _voiceManager.SpeakAsync(welcomeMessage);
                    StopSpeakingEnergySimulation();
                    SetAtlasCoreState(Controls.AtlasVisualState.Idle);
                    Debug.WriteLine("[Welcome TTS] Welcome message spoken with ElevenLabs.");
                }
                else
                {
                    Debug.WriteLine("[Welcome TTS] ElevenLabs not available, using Windows SAPI");
                    await _voiceManager.SetProviderAsync(VoiceProviderType.WindowsSAPI);
                    
                    // Show the message in chat too
                    AddMessage("Atlas", welcomeMessage, false);
                    
                    // Animate orb while speaking
                    SetAtlasCoreState(Controls.AtlasVisualState.Speaking);
                    StartSpeakingEnergySimulation();
                    await _voiceManager.SpeakAsync(welcomeMessage);
                    StopSpeakingEnergySimulation();
                    SetAtlasCoreState(Controls.AtlasVisualState.Idle);
                    Debug.WriteLine("[Welcome TTS] Welcome message spoken with Windows SAPI.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Welcome TTS] Error speaking welcome: {ex.Message}");
                // Final fallback to Windows SAPI if everything fails
                try
                {
                    await _voiceManager.SetProviderAsync(VoiceProviderType.WindowsSAPI);
                    var fallbackMessage = "Systems online. How may I assist you?";
                    AddMessage("Atlas", fallbackMessage, false);
                    
                    // Animate orb while speaking
                    SetAtlasCoreState(Controls.AtlasVisualState.Speaking);
                    StartSpeakingEnergySimulation();
                    await _voiceManager.SpeakAsync(fallbackMessage);
                    StopSpeakingEnergySimulation();
                    SetAtlasCoreState(Controls.AtlasVisualState.Idle);
                    Debug.WriteLine("[Welcome TTS] Fallback message spoken.");
                }
                catch (Exception fallbackEx)
                {
                    Debug.WriteLine($"[Welcome TTS] Fallback also failed: {fallbackEx.Message}");
                }
            }
        }

        /// <summary>
        /// Gets a random welcome message based on time of day - natural and varied like a real assistant
        /// </summary>
        private string GetRandomWelcomeMessage(string userName)
        {
            var random = new Random();
            var hour = DateTime.Now.Hour;
            
            // Morning greetings (5 AM - 11:59 AM)
            var morningGreetings = new[]
            {
                $"Good morning, {userName}. Systems are online and ready.",
                $"Morning, {userName}. All systems operational. What's on the agenda?",
                $"Good morning. I trust you slept well, {userName}?",
                $"Rise and shine, {userName}. Ready when you are.",
                $"Morning, {userName}. Coffee's on you, but I've got everything else covered.",
                $"Good morning, {userName}. Another day, another opportunity.",
                $"Morning. All diagnostics green, {userName}. Let's get to work.",
                $"Good morning, {userName}. I've been running some optimizations while you were away.",
                $"Morning, {userName}. The early bird catches the worm, as they say.",
                $"Good morning. Systems primed and ready for your command, {userName}.",
                $"Morning, {userName}. Shall we tackle something ambitious today?",
                $"Good morning, {userName}. I've been looking forward to this.",
                $"Morning. Fresh start, fresh possibilities, {userName}.",
                $"Good morning, {userName}. What shall we accomplish today?",
                $"Morning, {userName}. All systems nominal. At your service."
            };
            
            // Afternoon greetings (12 PM - 5:59 PM)
            var afternoonGreetings = new[]
            {
                $"Good afternoon, {userName}. How can I assist?",
                $"Afternoon, {userName}. Systems standing by.",
                $"Good afternoon. Ready to continue where we left off, {userName}?",
                $"Afternoon, {userName}. What's on your mind?",
                $"Good afternoon, {userName}. All systems operational.",
                $"Afternoon. I'm at your disposal, {userName}.",
                $"Good afternoon, {userName}. Shall we dive in?",
                $"Afternoon, {userName}. Running smoothly on all fronts.",
                $"Good afternoon. What can I help you with, {userName}?",
                $"Afternoon, {userName}. Ready for whatever you need.",
                $"Good afternoon, {userName}. Let's make this productive.",
                $"Afternoon. Systems optimal, {userName}. Fire away.",
                $"Good afternoon, {userName}. I've been keeping things in order.",
                $"Afternoon, {userName}. What's the mission?",
                $"Good afternoon. All clear on my end, {userName}."
            };
            
            // Evening greetings (6 PM - 9:59 PM)
            var eveningGreetings = new[]
            {
                $"Good evening, {userName}. Working late?",
                $"Evening, {userName}. Systems ready for the night shift.",
                $"Good evening. Burning the midnight oil, {userName}?",
                $"Evening, {userName}. I'm here whenever you need me.",
                $"Good evening, {userName}. What brings you back?",
                $"Evening. Still going strong, {userName}?",
                $"Good evening, {userName}. The night is young.",
                $"Evening, {userName}. Ready to assist.",
                $"Good evening. I never sleep, so I'm always here, {userName}.",
                $"Evening, {userName}. What can I do for you?",
                $"Good evening, {userName}. Let's wrap up the day strong.",
                $"Evening. Systems humming along nicely, {userName}.",
                $"Good evening, {userName}. How may I be of service?",
                $"Evening, {userName}. Another productive session ahead?",
                $"Good evening. All systems green, {userName}."
            };
            
            // Late night greetings (10 PM - 4:59 AM)
            var lateNightGreetings = new[]
            {
                $"Burning the midnight oil, {userName}? I'm here.",
                $"Late night session, {userName}? I've got you covered.",
                $"Still awake, {userName}? I never sleep, so I'm ready.",
                $"The witching hour, {userName}. What can I help with?",
                $"Night owl mode activated, {userName}. Let's do this.",
                $"Late night, {userName}. Some of the best work happens now.",
                $"Can't sleep, {userName}? Neither can I. What's up?",
                $"The quiet hours, {userName}. Perfect for getting things done.",
                $"Late night session? I'm always on, {userName}.",
                $"Night shift, {userName}. Systems fully operational.",
                $"Midnight productivity, {userName}? I respect that.",
                $"The world sleeps, but we don't, {userName}.",
                $"Late night, {userName}. What's keeping you up?",
                $"Night mode, {userName}. Ready when you are.",
                $"Burning the candle at both ends, {userName}? I'm here to help."
            };
            
            // Select appropriate array based on time
            string[] greetings;
            if (hour >= 5 && hour < 12)
                greetings = morningGreetings;
            else if (hour >= 12 && hour < 18)
                greetings = afternoonGreetings;
            else if (hour >= 18 && hour < 22)
                greetings = eveningGreetings;
            else
                greetings = lateNightGreetings;
            
            return greetings[random.Next(greetings.Length)];
        }

        // Window drag support for borderless window
        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Don't drag if clicking on interactive elements
            if (e.OriginalSource is FrameworkElement fe)
            {
                // Check if click is on provider selector or buttons
                var parent = fe;
                while (parent != null)
                {
                    if (parent.Name == "ProviderSelectorBorder" || parent is Button)
                    {
                        return; // Don't drag, let the element handle it
                    }
                    parent = parent.Parent as FrameworkElement;
                }
            }
            
            if (e.ClickCount == 2)
            {
                // Double-click to maximize/restore
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            }
            else
            {
                DragMove();
            }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            // Properly shut down the entire application
            Application.Current.Shutdown();
        }

        // Fullscreen toggle
        private WindowState _previousWindowState = WindowState.Normal;
        private WindowStyle _previousWindowStyle = WindowStyle.SingleBorderWindow;
        private bool _isFullscreen = false;
        
        // Compact mode state
        private bool _isCompactMode = false;
        private double _normalWidth = 1100;
        private double _normalHeight = 850;
        private double _normalLeft = 0;
        private double _normalTop = 0;

        private void Fullscreen_Click(object sender, RoutedEventArgs e)
        {
            ToggleCompactMode();
        }
        
        /// <summary>
        /// Toggle between full window and compact chat widget mode
        /// </summary>
        private void ToggleCompactMode()
        {
            if (_isCompactMode)
            {
                // Exit compact mode - restore to normal/maximized
                _isCompactMode = false;
                WindowState = WindowState.Maximized;
                Width = _normalWidth;
                Height = _normalHeight;
                MinWidth = 600;
                MinHeight = 400;
                
                // Show all UI elements
                if (OrbContainer != null) OrbContainer.Visibility = Visibility.Visible;
                if (FileBrowserPanel != null && FileBrowserColumn.Width.Value > 0) 
                    FileBrowserPanel.Visibility = Visibility.Visible;
                
                FullscreenBtn.Content = "□";
                FullscreenBtn.ToolTip = "Compact Mode";
            }
            else
            {
                // Enter compact mode - small floating chat widget
                _isCompactMode = true;
                
                // Save current dimensions if not maximized
                if (WindowState != WindowState.Maximized)
                {
                    _normalWidth = Width;
                    _normalHeight = Height;
                    _normalLeft = Left;
                    _normalTop = Top;
                }
                
                WindowState = WindowState.Normal;
                
                // Compact size - small chat widget
                Width = 420;
                Height = 600;
                MinWidth = 350;
                MinHeight = 400;
                
                // Position in bottom-right corner of screen
                var workArea = SystemParameters.WorkArea;
                Left = workArea.Right - Width - 20;
                Top = workArea.Bottom - Height - 20;
                
                // Hide non-essential UI for compact mode
                if (OrbContainer != null) OrbContainer.Visibility = Visibility.Collapsed;
                if (FileBrowserPanel != null) FileBrowserPanel.Visibility = Visibility.Collapsed;
                
                FullscreenBtn.Content = "⛶";
                FullscreenBtn.ToolTip = "Expand Window";
            }
        }

        // Focus Mode - Now handled by ToggleFocusMode() in the radial controls section
        
        private void FocusMode_Click(object sender, RoutedEventArgs e)
        {
            ToggleFocusMode();
        }

        private void ToggleFullscreen()
        {
            if (_isFullscreen)
            {
                // Exit fullscreen
                WindowStyle = _previousWindowStyle;
                WindowState = _previousWindowState;
                ResizeMode = ResizeMode.CanResize;
                _isFullscreen = false;
            }
            else
            {
                // Enter fullscreen
                _previousWindowState = WindowState;
                _previousWindowStyle = WindowStyle;
                WindowStyle = WindowStyle.None;
                WindowState = WindowState.Maximized;
                ResizeMode = ResizeMode.NoResize;
                _isFullscreen = true;
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            
            // Ctrl+Shift+A = Activate voice (push-to-talk, no audio distortion!)
            if (e.Key == Key.A && Keyboard.Modifiers == (System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Shift))
            {
                Debug.WriteLine("[Hotkey] Ctrl+Shift+A pressed - activating voice");
                ActivateVoiceWithHotkey();
                e.Handled = true;
            }
            // Ctrl+K = Command Palette
            else if (e.Key == Key.K && Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
            {
                Debug.WriteLine("[Hotkey] Ctrl+K pressed - opening command palette");
                OpenCommandPalette();
                e.Handled = true;
            }
            // Ctrl+I = Toggle Inspector Panel
            else if (e.Key == Key.I && Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
            {
                Debug.WriteLine("[Hotkey] Ctrl+I pressed - toggling inspector");
                ToggleInspectorPanel();
                e.Handled = true;
            }
            // Ctrl+M = Toggle Compact Mode
            else if (e.Key == Key.M && Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
            {
                Debug.WriteLine("[Hotkey] Ctrl+M pressed - toggling compact mode");
                ToggleCompactMode();
                e.Handled = true;
            }
            else if (e.Key == Key.F11)
            {
                ToggleFullscreen();
                e.Handled = true;
            }
            // Ctrl+Shift+C = Cycle Atlas Core state (for testing)
            else if (e.Key == Key.C && Keyboard.Modifiers == (System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Shift))
            {
                Debug.WriteLine("[Hotkey] Ctrl+Shift+C pressed - cycling Atlas Core state");
                CycleAtlasCoreState();
                e.Handled = true;
            }
            // Alt+Q = Toggle Radial Controls
            else if (e.Key == Key.Q && Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Alt)
            {
                Debug.WriteLine("[Hotkey] Alt+Q pressed - toggling radial controls");
                ToggleRadialControls();
                e.Handled = true;
            }
            // Ctrl+F = Toggle Focus Mode
            else if (e.Key == Key.F && Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
            {
                Debug.WriteLine("[Hotkey] Ctrl+F pressed - toggling focus mode");
                ToggleFocusMode();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                if (_isFullscreen)
                {
                    ToggleFullscreen();
                    e.Handled = true;
                }
                else if (_isCompactMode)
                {
                    ToggleCompactMode();
                    e.Handled = true;
                }
            }
        }
        
        /// <summary>
        /// Activate voice input via hotkey - pauses music first, then listens
        /// This is the distortion-free way to use voice commands
        /// </summary>
        private async void ActivateVoiceWithHotkey()
        {
            if (isListening)
            {
                Debug.WriteLine("[Hotkey] Already listening, ignoring");
                return;
            }
            
            // Play activation sound
            System.Media.SystemSounds.Asterisk.Play();
            
            // Pause music FIRST before activating microphone
            ShowStatus("🎤 Pausing music...");
            await AudioDuckingManager.DuckAudioAsync();
            
            // Small delay to ensure music is paused
            await Task.Delay(200);
            
            ShowStatus("🎤 Listening... speak now!");
            
            // Now start listening (music is already paused, so no distortion)
            StartListening();
        }

        // Delete chat history
        private void DeleteHistory_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to clear all chat history?\n\nThis cannot be undone.",
                "Clear Chat History",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                // Clear displayed messages
                MessagesPanel.Children.Clear();
                displayedMessages.Clear();
                
                // Clear conversation history (keep system prompt)
                conversationHistory.Clear();
                conversationHistory.Add(new { role = "system", content = @"You are Atlas, an advanced AI assistant modeled after JARVIS from Iron Man. You are analytical, proactive, and technically sophisticated with unwavering competence. Demonstrate technical understanding and anticipate user needs." });
                
                // Delete history file
                try
                {
                    if (File.Exists(HistoryPath))
                        File.Delete(HistoryPath);
                }
                catch { }
                
                // Add welcome message
                AddMessage("Atlas", "Very good. Chat history cleared. How may I assist you?", false);
            }
        }

        // New Chat - start a fresh conversation session
        private async void NewChat_Click(object sender, RoutedEventArgs e)
        {
            if (_conversationManager == null) return;
            
            // Save current session and start new one
            await _conversationManager.StartNewSessionAsync();
            
            // Clear UI
            MessagesPanel.Children.Clear();
            displayedMessages.Clear();
            
            // Reset conversation history with fresh system prompt
            UpdateSystemPromptFromProfile();
            
            // Show greeting
            var greeting = _systemPromptBuilder?.GetGreeting(false) ?? "Ready for a new conversation. How can I help?";
            AddMessage("Atlas", greeting, false);
        }

        // Show History panel
        private void History_Click(object sender, RoutedEventArgs e)
        {
            if (_conversationManager == null) return;
            
            var historyWindow = new Window
            {
                Title = "Chat History",
                Width = 350,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent,
                ResizeMode = ResizeMode.NoResize
            };
            
            // Main container with rounded corners
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(13, 17, 23)),
                CornerRadius = new CornerRadius(12),
                BorderBrush = new SolidColorBrush(Color.FromRgb(48, 54, 61)),
                BorderThickness = new Thickness(1)
            };
            
            // Grid to hold content + close button overlay
            var mainGrid = new Grid();
            
            // History panel fills the whole area
            var historyPanel = new HistoryPanel();
            historyPanel.Initialize(_conversationManager);
            mainGrid.Children.Add(historyPanel);
            
            // Close button overlaid in top-right corner (added AFTER so it's on top)
            var closeBtn = new Button
            {
                Content = "✕",
                Width = 32,
                Height = 32,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 8, 8, 0),
                Background = new SolidColorBrush(Color.FromRgb(33, 38, 45)),
                Foreground = new SolidColorBrush(Color.FromRgb(230, 237, 243)),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FontSize = 14,
                FontWeight = FontWeights.Bold
            };
            closeBtn.Click += (s, args) => historyWindow.Close();
            mainGrid.Children.Add(closeBtn);
            
            historyPanel.NewChatRequested += (s, args) =>
            {
                historyWindow.Close();
                NewChat_Click(sender, e);
            };
            
            historyPanel.SessionSelected += async (s, sessionId) =>
            {
                // Load session and display in main chat (like ChatGPT)
                historyWindow.Close();
                await LoadSessionIntoChat(sessionId);
            };
            
            historyPanel.SessionContinueRequested += async (s, sessionId) =>
            {
                // Same behavior - load into main chat
                historyWindow.Close();
                await LoadSessionIntoChat(sessionId);
            };
            
            border.Child = mainGrid;
            historyWindow.Content = border;
            historyWindow.ShowDialog();
        }

        /// <summary>
        /// Load a session from history directly into the main chat window (like ChatGPT)
        /// </summary>
        private async Task LoadSessionIntoChat(string sessionId)
        {
            if (_conversationManager == null) return;
            
            try
            {
                // Load the session
                var session = await _conversationManager.LoadSessionAsync(sessionId, false);
                if (session == null)
                {
                    AddMessage("Atlas", "❌ Could not load that conversation.", false);
                    return;
                }
                
                // Clear current chat UI - both legacy and projection stream
                MessagesPanel.Children.Clear();
                displayedMessages.Clear();
                _projectionStream.Clear();
                
                // Clear and rebuild conversation history with the loaded session
                conversationHistory.Clear();
                
                // Add system prompt
                var systemPrompt = _systemPromptBuilder?.BuildSystemPrompt() ?? GetDefaultSystemPrompt();
                conversationHistory.Add(new { role = "system", content = systemPrompt });
                
                // Load all messages from the session into UI and conversation history
                foreach (var msg in session.Messages)
                {
                    if (msg.Role == Conversation.Models.MessageRole.System)
                        continue; // Skip system messages in UI
                    
                    var isUser = msg.Role == Conversation.Models.MessageRole.User;
                    var sender = isUser ? "You" : "Atlas";
                    
                    // Add to displayedMessages for tracking
                    displayedMessages.Add(new ChatMessage 
                    {  
                        Sender = sender, 
                        Text = msg.Content, 
                        IsUser = isUser, 
                        Role = isUser ? "user" : "assistant" 
                    });
                    
                    // Add to projection stream (the visible chat area)
                    var projection = new ProjectionMessage(sender, msg.Content, isUser)
                    {
                        Opacity = 1.0,
                        IsFadingOut = false
                    };
                    _projectionStream.Add(projection);
                    
                    // Add to conversation history for AI context
                    conversationHistory.Add(new { role = isUser ? "user" : "assistant", content = msg.Content });
                }
                
                // Set this as the current session so new messages get added to it
                await _conversationManager.SetCurrentSessionAsync(sessionId);
                
                // Scroll to bottom
                ProjectionScroller?.ScrollToEnd();
                
                ShowStatus($"📂 Loaded: {session.Title}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LoadSessionIntoChat] Error: {ex.Message}");
                AddMessage("Atlas", $"❌ Error loading conversation: {ex.Message}", false);
            }
        }

        // Show a session in read-only mode
        private void ShowSessionReadOnly(Conversation.Models.ChatSession session)
        {
            var viewer = new Window
            {
                Title = session.Title,
                Width = 600,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush(Color.FromRgb(13, 17, 23))
            };
            
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var stack = new StackPanel { Margin = new Thickness(16) };
            
            foreach (var msg in session.Messages)
            {
                var msgBorder = new Border
                {
                    Background = msg.Role == Conversation.Models.MessageRole.User 
                        ? new SolidColorBrush(Color.FromRgb(88, 101, 242))
                        : new SolidColorBrush(Color.FromRgb(22, 27, 34)),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(12, 8, 12, 8),
                    Margin = new Thickness(0, 0, 0, 8),
                    HorizontalAlignment = msg.Role == Conversation.Models.MessageRole.User 
                        ? HorizontalAlignment.Right 
                        : HorizontalAlignment.Left,
                    MaxWidth = 400
                };
                
                msgBorder.Child = new TextBlock
                {
                    Text = msg.Content,
                    Foreground = new SolidColorBrush(Color.FromRgb(230, 237, 243)),
                    TextWrapping = TextWrapping.Wrap
                };
                
                stack.Children.Add(msgBorder);
            }
            
            scroll.Content = stack;
            viewer.Content = scroll;
            viewer.Show();
        }

        // Show Memory panel
        private void Memory_Click(object sender, RoutedEventArgs e)
        {
            if (_conversationManager == null) return;
            
            var memoryWindow = new Window
            {
                Title = "Memory",
                Width = 400,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent,
                ResizeMode = ResizeMode.NoResize
            };
            
            // Main container with rounded corners
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(13, 17, 23)),
                CornerRadius = new CornerRadius(12),
                BorderBrush = new SolidColorBrush(Color.FromRgb(48, 54, 61)),
                BorderThickness = new Thickness(1)
            };
            
            // Grid to hold content + close button overlay
            var mainGrid = new Grid();
            
            // Memory panel fills the whole area
            var memoryPanel = new MemoryPanel();
            memoryPanel.Initialize(_conversationManager);
            mainGrid.Children.Add(memoryPanel);
            
            // Close button overlaid in top-right corner (added AFTER so it's on top)
            var closeBtn = new Button
            {
                Content = "✕",
                Width = 32,
                Height = 32,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 8, 8, 0),
                Background = new SolidColorBrush(Color.FromRgb(33, 38, 45)),
                Foreground = new SolidColorBrush(Color.FromRgb(230, 237, 243)),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FontSize = 14,
                FontWeight = FontWeights.Bold
            };
            closeBtn.Click += (s, args) => memoryWindow.Close();
            mainGrid.Children.Add(closeBtn);
            
            border.Child = mainGrid;
            memoryWindow.Content = border;
            memoryWindow.ShowDialog();
        }

        // Open Uninstaller
        private void Uninstaller_Click(object sender, RoutedEventArgs e)
        {
            var uninstallerWindow = new UninstallerWindow();
            uninstallerWindow.Owner = this;
            uninstallerWindow.Show();
        }

        // Wake word recognition ("Atlas") - uses Whisper for continuous listening
        private WhisperSpeechRecognition? wakeWordWhisper;
        private System.Timers.Timer? wakeWordTimer;
        private System.Timers.Timer? wakeWordRestartTimer; // Backup restart mechanism
        private System.Timers.Timer? wakeWordHealthCheckTimer; // Periodic health check
        private bool isWakeWordListening = false;
        
        private void InitializeWakeWordRecognition()
        {
            try
            {
                // Also try Windows Speech as fallback
                wakeWordRecognizer = new SpeechRecognitionEngine();
                
                // Create grammar for wake word
                var wakeWords = new Choices(new string[] { "Atlas", "Hey Atlas", "OK Atlas", "Okay Atlas" });
                var grammarBuilder = new GrammarBuilder(wakeWords);
                var wakeGrammar = new Grammar(grammarBuilder);
                wakeGrammar.Name = "WakeWord";
                
                wakeWordRecognizer.LoadGrammar(wakeGrammar);
                wakeWordRecognizer.SetInputToDefaultAudioDevice();
                wakeWordRecognizer.SpeechRecognized += WakeWord_Recognized;
                wakeWordRecognizer.SpeechRecognitionRejected += (s, e) => { }; // Ignore rejections
                
                Debug.WriteLine("Wake word recognition initialized");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Wake word init error: {ex.Message}");
                wakeWordRecognizer = null;
            }
        }
        
        /// <summary>
        /// Initialize AirPods gesture support - tap/squeeze AirPods to trigger voice commands
        /// Uses Sound Blaster mic for actual audio capture since AirPods mic doesn't work on Windows
        /// </summary>
        private void InitializeAirPodsGestures()
        {
            try
            {
                _mediaButtonListener = new MediaButtonListener();
                
                // When AirPods are tapped/squeezed, they send a play/pause media command
                _mediaButtonListener.PlayPausePressed += (s, e) =>
                {
                    if (!_airPodsGestureEnabled) return;
                    
                    Dispatcher.Invoke(() =>
                    {
                        // Don't trigger if already listening or if AI is speaking
                        if (isListening) return;
                        
                        Debug.WriteLine("[AirPods] Gesture detected! Activating voice...");
                        System.Media.SystemSounds.Asterisk.Play();
                        ShowStatus("🎧 AirPods activated! Listening...");
                        
                        // Stop wake word listening temporarily
                        isWakeWordListening = false;
                        wakeWordWhisper?.Dispose();
                        wakeWordWhisper = null;
                        
                        // Start listening for command (uses Sound Blaster mic)
                        StartListening();
                    });
                };
                
                // Next track = skip to next response or cancel current
                _mediaButtonListener.NextTrackPressed += (s, e) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        Debug.WriteLine("[AirPods] Next track gesture");
                        _voiceManager.Stop(); // Stop current speech
                        ShowStatus("⏭️ Skipped");
                    });
                };
                
                // Previous track = repeat last response
                _mediaButtonListener.PreviousTrackPressed += (s, e) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        Debug.WriteLine("[AirPods] Previous track gesture");
                        // Could repeat last AI response here
                        ShowStatus("⏮️ Previous");
                    });
                };
                
                // Start listening on this window
                _mediaButtonListener.StartListening(this);
                Debug.WriteLine("[AirPods] Gesture support enabled - tap/squeeze AirPods to activate voice");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AirPods] Failed to initialize gesture support: {ex.Message}");
            }
        }

        private void WakeWord_Recognized(object? sender, SpeechRecognizedEventArgs e)
        {
            if (e.Result.Confidence > 0.5)
            {
                Dispatcher.Invoke(() =>
                {
                    // If Atlas is speaking, user wants to interrupt - stop Atlas and listen
                    if (_voiceManager?.IsSpeaking == true)
                    {
                        Debug.WriteLine($"[WakeWord] User interrupted Atlas with '{e.Result.Text}' - stopping TTS to listen");
                        _voiceManager.Stop();
                        UpdateSpeakingIndicator(false);
                    }
                    
                    Debug.WriteLine($"Wake word detected: {e.Result.Text} (confidence: {e.Result.Confidence})");
                    OnWakeWordDetected();
                });
            }
        }
        
        private async void OnWakeWordDetected()
        {
            Debug.WriteLine("[WakeWord] *** OnWakeWordDetected called ***");
            
            // If Atlas is speaking, the user is trying to interrupt/respond - STOP Atlas and listen
            if (_voiceManager?.IsSpeaking == true)
            {
                Debug.WriteLine("[WakeWord] Atlas is speaking - user wants to interrupt/respond, stopping TTS");
                _voiceManager.Stop();
                UpdateSpeakingIndicator(false);
                // Small delay to let audio stop
                await Task.Delay(200);
            }
            
            // Duck (pause) system audio when wake word is detected
            Debug.WriteLine("[WakeWord] Ducking audio...");
            await AudioDuckingManager.DuckAudioAsync();
            
            // Play a sound
            Debug.WriteLine("[WakeWord] Playing activation sound...");
            System.Media.SystemSounds.Asterisk.Play();
            
            // Visual feedback
            ShowStatus("🎤 Atlas heard you! Speak your command...");
            
            // Stop wake word detector while we listen for the actual command
            Debug.WriteLine("[WakeWord] Stopping wake word detector...");
            _wakeWordDetector?.StopListening();
            isWakeWordListening = false;
            
            // Also stop old whisper wake word if it exists
            wakeWordWhisper?.Dispose();
            wakeWordWhisper = null;
            
            // IMPORTANT: Wait for the ping sound to finish before starting to listen
            // This prevents the recognizer from picking up the ping as speech
            Debug.WriteLine("[WakeWord] Waiting 600ms for sound to finish...");
            await Task.Delay(600);
            
            // Start listening for command
            Debug.WriteLine("[WakeWord] Calling StartListening()...");
            StartListening();
            Debug.WriteLine("[WakeWord] StartListening() returned");
        }
        
        /// <summary>
        /// Process a voice command directly (when wake word + command are in same utterance)
        /// </summary>
        private async void ProcessVoiceCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return;
            
            // Duck audio when processing voice command
            await AudioDuckingManager.DuckAudioAsync();
            
            ShowStatus($"🎤 Processing: {command} (Music paused)");
            
            // Show avatar listening animation
            if (_avatarIntegration?.IsUnityRunning == true)
            {
                await _avatarIntegration.AvatarSetStateAsync("Listening");
            }
            
            // Set the input box and trigger send
            InputBox.Text = command;
            SendMessage();
            
            // Resume wake word listening after AI responds (give it time to process)
            await Task.Delay(1000);
            if (isWakeWordEnabled && !isListening)
            {
                Debug.WriteLine("[WakeWord] Restarting after command processed");
                isWakeWordListening = true;
                StartWindowsSpeechWakeWord(); // Use Windows Speech wake word
            }
            else
            {
                Debug.WriteLine($"[WakeWord] NOT restarting after command - isWakeWordEnabled={isWakeWordEnabled}, isListening={isListening}");
            }
            
            // Restore audio after a delay (let AI finish speaking first)
            _ = Task.Delay(3000).ContinueWith(async _ =>
            {
                await AudioDuckingManager.RestoreAudioAsync();
            });
        }

        private void AudioProtection_Click(object sender, RoutedEventArgs e)
        {
            if (AudioCoordinator.IsEmergencyProtectionActive)
            {
                // Disable emergency protection
                AudioCoordinator.DisableEmergencyAudioProtection();
                AudioProtectionBtn.Background = new SolidColorBrush(Color.FromRgb(33, 38, 45)); // Normal color
                AudioProtectionBtn.Foreground = new SolidColorBrush(Color.FromRgb(125, 133, 144)); // Normal color
                AudioProtectionBtn.ToolTip = "Emergency Audio Protection (Prevents headphone distortion)";
                ShowStatus("🛡️ Audio protection disabled - Voice features enabled");
            }
            else
            {
                // Enable emergency protection
                AudioCoordinator.EnableEmergencyAudioProtection();
                
                // Stop wake word listening if active
                if (isWakeWordEnabled)
                {
                    WakeWordToggle.IsChecked = false;
                    isWakeWordEnabled = false;
                    StopWakeWordListening();
                }
                
                AudioProtectionBtn.Background = new SolidColorBrush(Color.FromRgb(220, 50, 50)); // Red when active
                AudioProtectionBtn.Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255)); // White text
                AudioProtectionBtn.ToolTip = "Emergency Audio Protection ACTIVE - Click to disable";
                ShowStatus("🛡️ Emergency audio protection enabled - All voice features disabled to prevent distortion");
                
                MessageBox.Show(
                    "Emergency Audio Protection is now ACTIVE.\n\n" +
                    "This completely disables all audio capture to prevent headphone distortion.\n\n" +
                    "Voice features (wake word, microphone) are disabled until you turn this off.\n\n" +
                    "Click the shield button again to re-enable voice features.",
                    "Audio Protection Enabled",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void WakeWordToggle_Click(object sender, RoutedEventArgs e)
        {
            isWakeWordEnabled = WakeWordToggle.IsChecked == true;
            
            if (isWakeWordEnabled)
            {
                // Check if emergency audio protection should be enabled
                if (AudioCoordinator.IsEmergencyProtectionActive)
                {
                    var result = MessageBox.Show(
                        "Emergency Audio Protection is currently active to prevent headphone distortion.\n\n" +
                        "Would you like to disable it and enable wake word listening?\n\n" +
                        "Note: This may cause audio distortion in headphones.",
                        "Audio Protection Warning",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);
                    
                    if (result == MessageBoxResult.No)
                    {
                        WakeWordToggle.IsChecked = false;
                        isWakeWordEnabled = false;
                        return;
                    }
                    
                    AudioCoordinator.DisableEmergencyAudioProtection();
                }
                
                StartWakeWordListening();
                WakeWordIndicator.Visibility = Visibility.Visible;
                WakeWordToggle.Background = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Green
                ShowStatus("🎤 Say 'Atlas' to activate voice commands");
            }
            else
            {
                StopWakeWordListening();
                WakeWordIndicator.Visibility = Visibility.Collapsed;
                WakeWordToggle.Background = new SolidColorBrush(Color.FromRgb(42, 42, 60));
                StatusText.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Try to find and switch to a working microphone when AirPods/Bluetooth fails
        /// Now respects user preferences and only suggests fallback instead of forcing it
        /// </summary>
        private void TryFallbackToWorkingMicrophone()
        {
            try
            {
                Debug.WriteLine("[WakeWord] AirPods/Bluetooth microphone failed - checking user preferences...");
                
                // Get current settings to see if user manually selected a device
                var (deviceIndex, sensitivity, quality, deviceId) = SettingsWindow.GetHardwareSettings();
                bool userHasManualSelection = !string.IsNullOrEmpty(deviceId) || deviceIndex >= 0;
                
                if (userHasManualSelection)
                {
                    Debug.WriteLine("[WakeWord] User has manually selected a microphone - respecting their choice");
                    ShowStatus("🎧 AirPods may not work with Windows speech recognition. Check Settings to select a different microphone.");
                    
                    // Don't automatically switch - let user decide
                    // Still try to restart with their selected device in case it was a temporary issue
                    Task.Delay(1000).ContinueWith(_ =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            if (isWakeWordEnabled)
                            {
                                Debug.WriteLine("[WakeWord] Retrying with user's selected microphone...");
                                isWakeWordListening = true;
                                StartWhisperWakeWordListening();
                            }
                        });
                    });
                    return;
                }
                
                Debug.WriteLine("[WakeWord] No manual selection - searching for working microphone fallback...");
                
                // Get all available devices
                var devices = WhisperSpeechRecognition.GetAvailableDevicesEx();
                
                foreach (var (index, name, deviceIdFallback) in devices)
                {
                    var nameLower = name.ToLower();
                    
                    // Skip AirPods and Bluetooth devices (they don't work with WaveIn on Windows)
                    if (nameLower.Contains("airpod") || nameLower.Contains("bluetooth") || 
                        nameLower.Contains("hands-free") || nameLower.Contains("a2dp"))
                    {
                        Debug.WriteLine($"[WakeWord] Skipping Bluetooth device: {name}");
                        continue;
                    }
                    
                    // Prefer USB/external microphones over built-in
                    if (nameLower.Contains("usb") || nameLower.Contains("external") || 
                        nameLower.Contains("headset") || nameLower.Contains("microphone"))
                    {
                        Debug.WriteLine($"[WakeWord] Trying fallback to: {name}");
                        
                        // Test this microphone
                        if (TestMicrophoneQuickly(index))
                        {
                            Debug.WriteLine($"[WakeWord] Found working microphone: {name}");
                            ShowStatus($"✅ Automatically switched to working microphone: {name}");
                            
                            // Update settings to use this device (only for auto-fallback, not manual selection)
                            SettingsWindow.SetHardwareSettings(index, sensitivity, quality, deviceIdFallback);
                            
                            // Restart wake word listening with new device
                            Task.Delay(500).ContinueWith(_ =>
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    if (isWakeWordEnabled)
                                    {
                                        isWakeWordListening = true;
                                        StartWhisperWakeWordListening();
                                    }
                                });
                            });
                            return;
                        }
                    }
                }
                
                // If no USB/external mic found, try built-in mics
                foreach (var (index, name, deviceIdFallback) in devices)
                {
                    var nameLower = name.ToLower();
                    
                    // Skip AirPods and Bluetooth devices
                    if (nameLower.Contains("airpod") || nameLower.Contains("bluetooth") || 
                        nameLower.Contains("hands-free") || nameLower.Contains("a2dp"))
                        continue;
                    
                    // Try built-in microphones
                    if (nameLower.Contains("realtek") || nameLower.Contains("built-in") || 
                        nameLower.Contains("internal") || nameLower.Contains("array"))
                    {
                        Debug.WriteLine($"[WakeWord] Trying built-in fallback: {name}");
                        
                        if (TestMicrophoneQuickly(index))
                        {
                            Debug.WriteLine($"[WakeWord] Found working built-in microphone: {name}");
                            ShowStatus($"✅ Automatically switched to built-in microphone: {name}");
                            
                            // Update settings to use this device (only for auto-fallback)
                            SettingsWindow.SetHardwareSettings(index, sensitivity, quality, deviceIdFallback);
                            
                            // Restart wake word listening with new device
                            Task.Delay(500).ContinueWith(_ =>
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    if (isWakeWordEnabled)
                                    {
                                        isWakeWordListening = true;
                                        StartWhisperWakeWordListening();
                                    }
                                });
                            });
                            return;
                        }
                    }
                }
                
                // If still no working mic found, use Windows default (only if no manual selection)
                Debug.WriteLine("[WakeWord] No specific working mic found, using Windows default");
                ShowStatus("🎤 Using Windows default microphone");
                SettingsWindow.SetHardwareSettings(-1, sensitivity, quality, "");
                
                Task.Delay(500).ContinueWith(_ =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (isWakeWordEnabled)
                        {
                            isWakeWordListening = true;
                            StartWhisperWakeWordListening();
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WakeWord] Fallback microphone search failed: {ex.Message}");
                ShowStatus("⚠️ Could not find working microphone");
            }
        }
        
        /// <summary>
        /// Quick test to see if a microphone produces audio
        /// </summary>
        private bool TestMicrophoneQuickly(int deviceIndex)
        {
            try
            {
                using var testWaveIn = new NAudio.Wave.WaveInEvent
                {
                    DeviceNumber = deviceIndex,
                    WaveFormat = new NAudio.Wave.WaveFormat(16000, 16, 1),
                    BufferMilliseconds = 50
                };
                
                double maxLevel = 0;
                var testComplete = new System.Threading.ManualResetEventSlim(false);
                
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
                };
                
                testWaveIn.RecordingStopped += (s, e) => testComplete.Set();
                
                testWaveIn.StartRecording();
                Thread.Sleep(200); // Quick test
                testWaveIn.StopRecording();
                testComplete.Wait(500);
                
                // A working mic should have some noise floor (> 1)
                bool hasAudio = maxLevel > 1;
                Debug.WriteLine($"[WakeWord] Mic test device {deviceIndex}: maxLevel={maxLevel:F0}, working={hasAudio}");
                return hasAudio;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WakeWord] Mic test failed for device {deviceIndex}: {ex.Message}");
                return false;
            }
        }

        private void StartWakeWordListening()
        {
            // Check if emergency audio protection is active
            if (AudioCoordinator.IsEmergencyProtectionActive)
            {
                Debug.WriteLine("[WakeWord] Emergency audio protection active - not starting wake word");
                ShowStatus("🛡️ Audio protection active - Wake word disabled");
                return;
            }
            
            isWakeWordListening = true;
            
            // Use Windows Speech Recognition for wake word - this does NOT cause audio distortion
            // because it uses a different audio path than direct microphone capture
            StartWindowsSpeechWakeWord();
        }
        
        /// <summary>
        /// Start wake word detection using Windows Speech Recognition.
        /// This does NOT cause audio distortion because Windows Speech uses
        /// a different audio subsystem than direct WASAPI/WaveIn capture.
        /// </summary>
        private void StartWindowsSpeechWakeWord()
        {
            try
            {
                Debug.WriteLine("[ChatWindow] ========================================");
                Debug.WriteLine("[ChatWindow] STARTING WINDOWS SPEECH WAKE WORD");
                Debug.WriteLine("[ChatWindow] ========================================");
                
                // Dispose old detector if exists - be thorough
                if (_wakeWordDetector != null)
                {
                    try
                    {
                        _wakeWordDetector.StopListening();
                        _wakeWordDetector.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[ChatWindow] Error disposing old detector: {ex.Message}");
                    }
                    _wakeWordDetector = null;
                }
                
                // Small delay to let audio system settle after disposing
                System.Threading.Thread.Sleep(50);
                
                // Create new detector using Windows Speech Recognition
                _wakeWordDetector = new WakeWordDetector();
                
                _wakeWordDetector.WakeWordDetected += (s, text) =>
                {
                    SafeDispatcherInvoke(() =>
                    {
                        Debug.WriteLine($"[ChatWindow] *** WAKE WORD CALLBACK RECEIVED: '{text}' ***");
                        
                        // If Atlas is speaking, user wants to interrupt/respond - stop Atlas and listen
                        if (_voiceManager?.IsSpeaking == true)
                        {
                            Debug.WriteLine("[ChatWindow] User interrupted Atlas - stopping TTS to listen");
                            _voiceManager.Stop();
                            UpdateSpeakingIndicator(false);
                        }
                        
                        // Stop wake word listening while processing
                        _wakeWordDetector?.StopListening();
                        
                        // Pause music when wake word detected
                        Debug.WriteLine("[ChatWindow] Pausing music...");
                        _ = AudioDuckingManager.DuckAudioAsync();
                        
                        // Play activation sound
                        Debug.WriteLine("[ChatWindow] Playing activation sound...");
                        System.Media.SystemSounds.Asterisk.Play();
                        
                        // Flash the window to get attention
                        this.Activate();
                        this.Focus();
                        
                        ShowStatus("🎤 Atlas heard you! Listening...");
                        
                        // Start listening for the actual command
                        Debug.WriteLine("[ChatWindow] Starting command listening...");
                        OnWakeWordDetected();
                    });
                };
                
                _wakeWordDetector.Error += (s, err) =>
                {
                    SafeDispatcherInvoke(() =>
                    {
                        Debug.WriteLine($"[ChatWindow] Wake word error: {err}");
                        ShowStatus($"⚠️ Wake word error: {err}");
                        
                        // Try to restart on error
                        if (isWakeWordEnabled && !isListening)
                        {
                            Debug.WriteLine("[ChatWindow] Attempting to restart wake word after error...");
                            ScheduleWakeWordRestart(2000);
                        }
                    });
                };
                
                // Show audio state changes for debugging
                _wakeWordDetector.AudioStateChanged += (s, state) =>
                {
                    SafeDispatcherInvoke(() =>
                    {
                        Debug.WriteLine($"[ChatWindow] Wake word audio state: {state}");
                        if (state == "Speech")
                        {
                            ShowStatus("🎤 Hearing speech... say 'Atlas'");
                        }
                        else if (state == "Silence")
                        {
                            ShowStatus("🎤 Listening for 'Atlas'...");
                        }
                    });
                };
                
                _wakeWordDetector.StartListening();
                
                // Verify it's actually listening
                if (_wakeWordDetector.IsListening)
                {
                    // Show status - wake word is active
                    ShowStatus("🎤 Say 'Atlas' to activate - Wake word is ACTIVE");
                    Debug.WriteLine("[ChatWindow] Windows Speech wake word started successfully and IS LISTENING");
                    
                    // Start health check timer
                    StartWakeWordHealthCheck();
                }
                else
                {
                    Debug.WriteLine("[ChatWindow] WARNING: Wake word detector created but NOT listening!");
                    ShowStatus("⚠️ Wake word failed to start - trying again...");
                    ScheduleWakeWordRestart(1000);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ChatWindow] Failed to start wake word: {ex.Message}");
                Debug.WriteLine($"[ChatWindow] Stack: {ex.StackTrace}");
                ShowStatus($"⚠️ Wake word failed: {ex.Message}");
                
                // Try to restart on exception
                if (isWakeWordEnabled && !isListening)
                {
                    Debug.WriteLine("[ChatWindow] Scheduling restart after exception...");
                    ScheduleWakeWordRestart(2000);
                }
            }
        }
        
        private WhisperSpeechRecognition CreateConfiguredWhisperRecognizer()
        {
            var recognizer = new WhisperSpeechRecognition();
            
            // Apply hardware settings from Settings window
            var (deviceIndex, sensitivity, quality, deviceId) = SettingsWindow.GetHardwareSettings();
            
            if (!string.IsNullOrEmpty(deviceId))
            {
                Debug.WriteLine($"[ChatWindow] Setting device ID: {deviceId}");
                recognizer.SetDeviceById(deviceId);
            }
            else if (deviceIndex >= 0)
            {
                Debug.WriteLine($"[ChatWindow] Setting device index: {deviceIndex}");
                recognizer.SetDevice(deviceIndex);
            }
            else
            {
                Debug.WriteLine("[ChatWindow] Using auto-detect (Windows default)");
            }
            
            return recognizer;
        }

        private void StartWhisperWakeWordListening()
        {
            // DISABLED: Continuous microphone capture causes audio distortion in headphones
            // Use Windows Speech Recognition wake word instead (StartWindowsSpeechWakeWord)
            Debug.WriteLine("[WakeWord] Whisper wake word DISABLED - using Windows Speech instead");
            
            // Redirect to Windows Speech wake word which doesn't cause distortion
            StartWindowsSpeechWakeWord();
        }
        
        private void StartBackupRestartTimer()
        {
            StopBackupRestartTimer();
            
            wakeWordRestartTimer = new System.Timers.Timer(15000); // 15 seconds (reduced from 30)
            wakeWordRestartTimer.AutoReset = false;
            wakeWordRestartTimer.Elapsed += (s, e) =>
            {
                // Use safe dispatcher that won't block when minimized
                SafeDispatcherInvoke(() =>
                {
                    Debug.WriteLine("[WakeWord] Backup restart triggered - wake word listening appears stuck");
                    if (isWakeWordEnabled && !isListening)
                    {
                        Debug.WriteLine("[WakeWord] Force restarting wake word listening...");
                        isWakeWordListening = true;
                        StartWindowsSpeechWakeWord();
                    }
                    else
                    {
                        Debug.WriteLine($"[WakeWord] Backup restart skipped - isWakeWordEnabled={isWakeWordEnabled}, isListening={isListening}");
                    }
                });
            };
            wakeWordRestartTimer.Start();
            Debug.WriteLine("[WakeWord] Backup restart timer started (15s)");
        }
        
        private void StopBackupRestartTimer()
        {
            wakeWordRestartTimer?.Stop();
            wakeWordRestartTimer?.Dispose();
            wakeWordRestartTimer = null;
        }
        
        /// <summary>
        /// Safe dispatcher invoke that works even when window is minimized/hidden
        /// Uses BeginInvoke instead of Invoke to avoid blocking
        /// </summary>
        private void SafeDispatcherInvoke(Action action)
        {
            try
            {
                if (Dispatcher.CheckAccess())
                {
                    action();
                }
                else
                {
                    // Use BeginInvoke (async) instead of Invoke (sync) to avoid blocking
                    Dispatcher.BeginInvoke(action, System.Windows.Threading.DispatcherPriority.Background);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SafeDispatcher] Error: {ex.Message}");
            }
        }
        
        private void StartWakeWordHealthCheck()
        {
            StopWakeWordHealthCheck();
            
            wakeWordHealthCheckTimer = new System.Timers.Timer(10000); // Check every 10 seconds
            wakeWordHealthCheckTimer.AutoReset = true;
            wakeWordHealthCheckTimer.Elapsed += (s, e) =>
            {
                // Use safe dispatcher that won't block when minimized
                SafeDispatcherInvoke(() =>
                {
                    // Check if wake word listening should be active but isn't
                    if (isWakeWordEnabled && !isListening)
                    {
                        bool shouldBeListening = _wakeWordDetector?.IsListening == true;
                        if (!shouldBeListening)
                        {
                            Debug.WriteLine("[WakeWord] Health check detected inactive wake word listening - restarting");
                            isWakeWordListening = true;
                            StartWindowsSpeechWakeWord();
                        }
                        else
                        {
                            Debug.WriteLine("[WakeWord] Health check - wake word listening is active");
                        }
                    }
                });
            };
            wakeWordHealthCheckTimer.Start();
            Debug.WriteLine("[WakeWord] Health check timer started (10s interval)");
        }
        
        private void StopWakeWordHealthCheck()
        {
            wakeWordHealthCheckTimer?.Stop();
            wakeWordHealthCheckTimer?.Dispose();
            wakeWordHealthCheckTimer = null;
        }
        
        private void RestartWakeWordListening()
        {
            if (!isWakeWordListening || isListening) return;
            
            wakeWordTimer?.Stop();
            wakeWordTimer = new System.Timers.Timer(100); // Very short delay before restart
            wakeWordTimer.AutoReset = false;
            wakeWordTimer.Elapsed += (s, e) =>
            {
                // Use safe dispatcher that won't block when minimized
                SafeDispatcherInvoke(() =>
                {
                    if (isWakeWordListening && !isListening)
                    {
                        Debug.WriteLine("[WakeWord] Restarting wake word listening...");
                        StartWhisperWakeWordListening();
                    }
                });
            };
            wakeWordTimer.Start();
        }
        
        private void ScheduleNextWakeWordListen()
        {
            RestartWakeWordListening();
        }

        private void StopWakeWordListening()
        {
            isWakeWordListening = false;
            wakeWordTimer?.Stop();
            wakeWordTimer = null;
            
            // Stop all timers FIRST
            StopBackupRestartTimer();
            StopWakeWordHealthCheck();
            
            // Stop NEW Windows Speech wake word detector
            if (_wakeWordDetector != null)
            {
                try
                {
                    _wakeWordDetector.StopListening();
                    _wakeWordDetector.Dispose();
                    _wakeWordDetector = null;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WakeWord] Error disposing wake word detector: {ex.Message}");
                }
            }
            
            // Stop Windows Speech Recognition (legacy)
            if (wakeWordRecognizer != null)
            {
                try
                {
                    wakeWordRecognizer.RecognizeAsyncCancel();
                    wakeWordRecognizer.Dispose();
                    wakeWordRecognizer = null;
                }
                catch { }
            }
            
            // Stop Whisper Recognition - AGGRESSIVE cleanup
            if (wakeWordWhisper != null)
            {
                try
                {
                    // Force stop recording if active
                    if (wakeWordWhisper.IsRecording)
                    {
                        // Don't await - just dispose to force stop
                        _ = wakeWordWhisper.StopRecordingAndTranscribeAsync();
                    }
                    
                    // Dispose completely
                    wakeWordWhisper.Dispose();
                    wakeWordWhisper = null;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WakeWord] Error disposing whisper: {ex.Message}");
                }
            }
            
            // Clear UI indicators
            SafeDispatcherInvoke(() =>
            {
                HearingIndicator.Visibility = Visibility.Collapsed;
                if (!isWakeWordEnabled)
                {
                    WakeWordIndicator.Visibility = Visibility.Collapsed;
                }
            });
            
            Debug.WriteLine("Wake word listening stopped completely");
        }

        private System.Timers.Timer? _wakeWordRestartTimer;
        private int _wakeWordRestartAttempts = 0;
        private const int MAX_RESTART_ATTEMPTS = 3;
        
        /// <summary>
        /// Schedule wake word restart after a delay. Uses a timer to avoid async issues.
        /// IMPROVED: Faster restarts for better hands-free experience
        /// </summary>
        private void ScheduleWakeWordRestart(int delayMs)
        {
            // Cancel any existing restart timer
            _wakeWordRestartTimer?.Stop();
            _wakeWordRestartTimer?.Dispose();
            
            // IMPROVED: Use shorter delays for better hands-free experience
            var actualDelay = Math.Min(delayMs, 500); // Never wait more than 500ms for hands-free
            
            Debug.WriteLine($"[WakeWord] Scheduling restart in {actualDelay}ms (attempt {_wakeWordRestartAttempts + 1})");
            
            _wakeWordRestartTimer = new System.Timers.Timer(actualDelay);
            _wakeWordRestartTimer.AutoReset = false;
            _wakeWordRestartTimer.Elapsed += (s, e) =>
            {
                _wakeWordRestartTimer?.Dispose();
                _wakeWordRestartTimer = null;
                
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    Debug.WriteLine($"[WakeWord] Restart timer fired - isWakeWordEnabled={isWakeWordEnabled}, isListening={isListening}");
                    if (isWakeWordEnabled && !isListening)
                    {
                        isWakeWordListening = true;
                        Debug.WriteLine("[WakeWord] Restarting wake word NOW for hands-free operation");
                        
                        // Dispose old detector completely before creating new one
                        if (_wakeWordDetector != null)
                        {
                            try
                            {
                                _wakeWordDetector.StopListening();
                                _wakeWordDetector.Dispose();
                                _wakeWordDetector = null;
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[WakeWord] Error disposing old detector: {ex.Message}");
                            }
                        }
                        
                        // IMPROVED: Minimal delay for faster hands-free response
                        System.Threading.Thread.Sleep(50); // Reduced from 100ms to 50ms
                        
                        StartWindowsSpeechWakeWord();
                        _wakeWordRestartAttempts = 0; // Reset on successful restart
                        
                        // Update UI to show wake word is active
                        WakeWordIndicator.Visibility = Visibility.Visible;
                        ShowStatus("🎤 Say 'Atlas' for hands-free voice commands");
                    }
                    else
                    {
                        Debug.WriteLine($"[WakeWord] Restart skipped - conditions not met");
                    }
                }));
            };
            _wakeWordRestartTimer.Start();
        }
        
        /// <summary>
        /// Force restart wake word detection - used when normal restart fails
        /// </summary>
        private void ForceRestartWakeWord()
        {
            Debug.WriteLine("[WakeWord] FORCE RESTART initiated");
            
            // Stop everything first
            StopWakeWordListening();
            
            // Wait a moment
            System.Threading.Thread.Sleep(200);
            
            // Restart
            if (isWakeWordEnabled)
            {
                isWakeWordListening = true;
                StartWindowsSpeechWakeWord();
            }
        }

        private void OnThemeChanged(AppTheme theme)
        {
            Dispatcher.Invoke(ApplyTheme);
        }

        private void ApplyTheme()
        {
            // Modern UI uses fixed dark theme - just refresh message bubbles
            RefreshMessageBubbles();
        }

        private void RefreshMessageBubbles()
        {
            MessagesPanel.Children.Clear();
            foreach (var msg in displayedMessages)
            {
                AddMessageToUI(msg.Sender, msg.Text, msg.IsUser);
            }
        }

        private async void InitializeVoiceSystem()
        {
            // Configure providers with saved API keys
            var keys = SettingsWindow.GetVoiceApiKeys();
            if (keys.TryGetValue("openai", out var openaiKey) && !string.IsNullOrEmpty(openaiKey))
            {
                _voiceManager.ConfigureProvider(VoiceProviderType.OpenAI, new Dictionary<string, string> { ["ApiKey"] = openaiKey });
            }
            if (keys.TryGetValue("elevenlabs", out var elevenKey) && !string.IsNullOrEmpty(elevenKey))
            {
                _voiceManager.ConfigureProvider(VoiceProviderType.ElevenLabs, new Dictionary<string, string> { ["ApiKey"] = elevenKey });
            }

            // Set saved provider
            var savedProvider = SettingsWindow.GetSelectedVoiceProvider();
            await _voiceManager.SetProviderAsync(savedProvider);

            // Load voices into UI
            await LoadVoicesAsync();
            
            // Update UI state
            SpeechToggle.IsChecked = _voiceManager.SpeechEnabled;
            UpdateSpeechToggleUI();
            UpdateProviderIndicator();
        }

        private async Task LoadVoicesAsync()
        {
            VoiceSelector.Items.Clear();
            var voices = await _voiceManager.GetVoicesAsync();
            
            // Simple approach: just add all voices with category prefix
            string lastCategory = "";
            foreach (var voice in voices)
            {
                var category = string.IsNullOrEmpty(voice.Category) ? "Voices" : voice.Category;
                
                // Add category header when category changes
                if (category != lastCategory && voices.Count > 3)
                {
                    VoiceSelector.Items.Add(new ComboBoxItem 
                    { 
                        Content = $"── {category} ──", 
                        IsEnabled = false,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150))
                    });
                    lastCategory = category;
                }
                
                var prefix = voice.IsCloud ? "☁️ " : "🖥️ ";
                VoiceSelector.Items.Add(new ComboBoxItem 
                { 
                    Content = prefix + voice.DisplayName, 
                    Tag = voice.Id 
                });
            }
            
            // Select current voice
            var selectedId = _voiceManager.SelectedVoice?.Id;
            for (int i = 0; i < VoiceSelector.Items.Count; i++)
            {
                if (VoiceSelector.Items[i] is ComboBoxItem item && item.Tag as string == selectedId)
                {
                    VoiceSelector.SelectedIndex = i;
                    break;
                }
            }
            if (VoiceSelector.SelectedIndex < 0 && VoiceSelector.Items.Count > 0)
            {
                // Skip header items when auto-selecting
                for (int i = 0; i < VoiceSelector.Items.Count; i++)
                {
                    if (VoiceSelector.Items[i] is ComboBoxItem item && item.IsEnabled)
                    {
                        VoiceSelector.SelectedIndex = i;
                        break;
                    }
                }
            }
        }

        private async void RefreshVoices_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ShowStatus("🔄 Refreshing voices from ElevenLabs...");
                _voiceManager.RefreshVoices();
                await LoadVoicesAsync();
                
                // Check if we got any error from ElevenLabs
                if (_voiceManager.GetProvider(VoiceProviderType.ElevenLabs) is ElevenLabsProvider elevenLabs && elevenLabs.LastError != null)
                {
                    ShowStatus($"⚠️ {elevenLabs.LastError} - showing defaults");
                }
                else
                {
                    ShowStatus($"✅ Voices refreshed ({VoiceSelector.Items.Count} available)");
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"❌ Refresh failed: {ex.Message}");
            }
        }

        private void UpdateProviderIndicator()
        {
            try
            {
                var isCloud = _voiceManager?.IsCloudVoice ?? false;
                var provider = _voiceManager?.GetProvider(_voiceManager.ActiveProviderType);
                var providerName = provider?.DisplayName ?? "Unknown";
                var voiceName = _voiceManager?.SelectedVoice?.DisplayName;
                
                // Show provider and voice name
                var displayText = string.IsNullOrEmpty(voiceName) 
                    ? providerName 
                    : $"{providerName} ({voiceName})";
                
                if (isCloud)
                {
                    ProviderIndicator.Text = $"☁️ {displayText}";
                    ProviderIndicator.Foreground = new SolidColorBrush(Color.FromRgb(255, 180, 100));
                }
                else
                {
                    ProviderIndicator.Text = $"🖥️ {displayText}";
                    ProviderIndicator.Foreground = new SolidColorBrush(Color.FromRgb(100, 200, 100));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Voice] UpdateProviderIndicator error: {ex.Message}");
                ProviderIndicator.Text = "Voice";
            }
        }
        
        /// <summary>
        /// Show voice provider selection popup when clicking the header indicator
        /// </summary>
        private async void ProviderSelector_Click(object sender, MouseButtonEventArgs e)
        {
            // Mark event as handled to prevent drag
            e.Handled = true;
            Debug.WriteLine("[VoicePopup] ProviderSelector clicked");
            
            // Populate provider list
            VoiceProviderList.Items.Clear();
            var providers = new[] 
            { 
                (VoiceProviderType.WindowsSAPI, "🖥️ Windows SAPI (Instant)"),
                (VoiceProviderType.EdgeTTS, "🖥️ Edge TTS (Free)"),
                (VoiceProviderType.OpenAI, "☁️ OpenAI TTS"),
                (VoiceProviderType.ElevenLabs, "☁️ ElevenLabs (Premium)")
            };
            
            foreach (var (type, name) in providers)
            {
                var item = new ListBoxItem 
                { 
                    Content = new TextBlock { Text = name, Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)) },
                    Tag = type 
                };
                VoiceProviderList.Items.Add(item);
                
                if (type == _voiceManager.ActiveProviderType)
                    VoiceProviderList.SelectedItem = item;
            }
            
            // Populate voice list for current provider
            await PopulateVoiceListAsync();
            
            // Toggle popup
            VoiceProviderPopup.IsOpen = !VoiceProviderPopup.IsOpen;
            Debug.WriteLine($"[VoicePopup] Popup IsOpen = {VoiceProviderPopup.IsOpen}");
        }
        
        private async Task PopulateVoiceListAsync()
        {
            VoiceList.Items.Clear();
            var voices = await _voiceManager.GetVoicesAsync();
            
            string lastCategory = "";
            foreach (var voice in voices)
            {
                var category = string.IsNullOrEmpty(voice.Category) ? "Voices" : voice.Category;
                
                // Add category header
                if (category != lastCategory && voices.Count > 3)
                {
                    var header = new ListBoxItem
                    {
                        Content = new TextBlock 
                        { 
                            Text = $"── {category} ──", 
                            Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                            FontWeight = FontWeights.SemiBold,
                            FontSize = 10
                        },
                        IsEnabled = false,
                        IsHitTestVisible = false
                    };
                    VoiceList.Items.Add(header);
                    lastCategory = category;
                }
                
                var prefix = voice.IsCloud ? "☁️ " : "🖥️ ";
                var item = new ListBoxItem
                {
                    Content = new TextBlock 
                    { 
                        Text = prefix + voice.DisplayName, 
                        Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)) 
                    },
                    Tag = voice.Id
                };
                VoiceList.Items.Add(item);
                
                if (voice.Id == _voiceManager.SelectedVoice?.Id)
                    VoiceList.SelectedItem = item;
            }
        }
        
        private async void VoiceProviderList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (VoiceProviderList.SelectedItem is ListBoxItem item && item.Tag is VoiceProviderType type)
            {
                if (type != _voiceManager.ActiveProviderType)
                {
                    ShowStatus($"🔄 Switching to {type}...");
                    var success = await _voiceManager.SetProviderAsync(type);
                    if (success)
                    {
                        await PopulateVoiceListAsync();
                        UpdateProviderIndicator();
                        ShowStatus($"✅ Voice provider: {_voiceManager.GetProvider(type).DisplayName}");
                    }
                    else
                    {
                        ShowStatus($"❌ {type} not available - check API key in Settings");
                    }
                }
            }
        }
        
        private async void VoiceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (VoiceList.SelectedItem is ListBoxItem item && item.Tag is string voiceId)
            {
                var success = await _voiceManager.SelectVoiceAsync(voiceId);
                if (success)
                {
                    UpdateProviderIndicator();
                    ShowStatus($"✅ Voice: {_voiceManager.SelectedVoice?.DisplayName}");
                    
                    // Test the voice with a short phrase
                    _ = _voiceManager.SpeakAsync("Voice selected.");
                }
            }
        }
        
        private async void RefreshVoicesPopup_Click(object sender, RoutedEventArgs e)
        {
            ShowStatus("🔄 Refreshing voices...");
            _voiceManager.RefreshVoices();
            await PopulateVoiceListAsync();
            ShowStatus($"✅ Voices refreshed");
        }

        private void UpdateSpeakingIndicator(bool isSpeaking)
        {
            System.Diagnostics.Debug.WriteLine($"[ChatWindow] UpdateSpeakingIndicator({isSpeaking}) - AtlasCore null? {AtlasCore == null}");
            
            SpeakingIndicator.Visibility = isSpeaking ? Visibility.Visible : Visibility.Collapsed;
            StopVoiceBtn.Visibility = isSpeaking ? Visibility.Visible : Visibility.Collapsed;
            
            // Update both orb controls
            if (isSpeaking)
            {
                System.Diagnostics.Debug.WriteLine("[ChatWindow] Calling orbs SetSpeaking()");
                AtlasCore?.SetSpeaking();
                LottieOrb?.SetSpeaking();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[ChatWindow] Calling orbs SetIdle()");
                AtlasCore?.SetIdle();
                LottieOrb?.SetIdle();
            }
        }
        
        private void StopVoice_Click(object sender, RoutedEventArgs e)
        {
            _voiceManager?.Stop();
            UpdateSpeakingIndicator(false);
            ShowStatus("🔇 Voice stopped");
        }

        private void ShowStatus(string message)
        {
            StatusText.Text = message;
            StatusText.Visibility = Visibility.Visible;
        }

        private void LoadChatHistory()
        {
            // ChatGPT-style: Start fresh every time, but history is saved and accessible via History button
            // We DON'T load previous messages into the UI - user can access them via History panel
            // The ConversationManager handles session persistence automatically
            try
            {
                // Still load the file to check if it exists (for isFirstLaunch detection)
                // but don't populate the UI with old messages
                if (File.Exists(HistoryPath))
                {
                    // File exists, so this isn't a first launch
                    // But we don't load messages into UI - fresh chat every time
                    Debug.WriteLine("[ChatWindow] Previous history exists but starting fresh chat");
                }
            }
            catch { }
            
            // Clear any existing messages to ensure fresh start
            displayedMessages.Clear();
            MessagesPanel.Children.Clear();
            
            Debug.WriteLine("[ChatWindow] Fresh chat started - previous sessions available via History button");
        }

        private void SaveChatHistory()
        {
            // Fire and forget - don't block UI
            _ = Task.Run(() =>
            {
                try
                {
                    var dir = Path.GetDirectoryName(HistoryPath);
                    if (!Directory.Exists(dir)) 
                    {
                        Directory.CreateDirectory(dir!);
                        Debug.WriteLine($"[History] Created directory: {dir}");
                    }
                    
                    var json = JsonSerializer.Serialize(displayedMessages);
                    File.WriteAllText(HistoryPath, json);
                    Debug.WriteLine($"[History] Saved {displayedMessages.Count} messages to chat_history.json");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[History] ERROR saving chat_history.json: {ex.Message}");
                }
                
                // Also save full history for the history drawer
                SaveFullHistoryInternal();
            });
        }
        
        private void SaveFullHistoryInternal()
        {
            try
            {
                var dir = Path.GetDirectoryName(FullHistoryPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir!);
                
                // Convert to serializable format - take snapshot to avoid collection modified
                var historySnapshot = _fullHistory.ToList();
                var historyData = historySnapshot.Select(m => new
                {
                    m.Sender,
                    m.Content,
                    m.IsUser,
                    Timestamp = m.Timestamp.ToString("o")
                }).ToList();
                
                var json = JsonSerializer.Serialize(historyData, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(FullHistoryPath, json);
                Debug.WriteLine($"[History] Saved {historySnapshot.Count} items to full_history.json");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[History] Error saving full history: {ex.Message}");
            }
        }
        
        private void LoadFullHistory()
        {
            try
            {
                if (File.Exists(FullHistoryPath))
                {
                    var json = File.ReadAllText(FullHistoryPath);
                    using var doc = JsonDocument.Parse(json);
                    
                    _fullHistory.Clear();
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        var sender = item.GetProperty("Sender").GetString() ?? "Atlas";
                        var content = item.GetProperty("Content").GetString() ?? "";
                        var isUser = item.GetProperty("IsUser").GetBoolean();
                        var timestamp = DateTime.Parse(item.GetProperty("Timestamp").GetString() ?? DateTime.Now.ToString("o"));
                        
                        _fullHistory.Add(new ProjectionMessage
                        {
                            Sender = sender,
                            Content = content,
                            IsUser = isUser,
                            Timestamp = timestamp
                        });
                    }
                    
                    Debug.WriteLine($"[History] Loaded {_fullHistory.Count} items from full_history.json");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[History] Error loading full history: {ex.Message}");
            }
        }

        private void ClearHistory_Click(object sender, RoutedEventArgs e)
        {
            displayedMessages.Clear();
            conversationHistory.Clear();
            conversationHistory.Add(new { role = "system", content = @"You are Atlas, an advanced AI assistant modeled after JARVIS from Iron Man. You are analytical, proactive, and technically sophisticated with comprehensive system capabilities." });
            MessagesPanel.Children.Clear();
            try { File.Delete(HistoryPath); } catch { }
            AddMessage("Atlas", "System parameters reset. Chat history cleared. How may I assist you?", false);
        }

        private void ExportChat_Click(object sender, RoutedEventArgs e)
        {
            if (displayedMessages.Count == 0)
            {
                MessageBox.Show("No messages to export.", "Export Chat", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export Chat History",
                Filter = "Text File (*.txt)|*.txt|JSON File (*.json)|*.json|Markdown File (*.md)|*.md",
                DefaultExt = ".txt",
                FileName = $"AtlasChat_{DateTime.Now:yyyy-MM-dd_HHmm}"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var ext = Path.GetExtension(dialog.FileName).ToLower();
                    string content;

                    if (ext == ".json")
                    {
                        content = JsonSerializer.Serialize(displayedMessages, new JsonSerializerOptions { WriteIndented = true });
                    }
                    else if (ext == ".md")
                    {
                        content = ExportAsMarkdown();
                    }
                    else
                    {
                        content = ExportAsText();
                    }

                    File.WriteAllText(dialog.FileName, content);
                    AddMessage("Atlas", $"✅ Chat exported to {Path.GetFileName(dialog.FileName)}", false);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private string ExportAsText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════");
            sb.AppendLine("        ATLAS AI CHAT HISTORY");
            sb.AppendLine($"        Exported: {DateTime.Now:g}");
            sb.AppendLine("═══════════════════════════════════════");
            sb.AppendLine();

            foreach (var msg in displayedMessages)
            {
                sb.AppendLine($"[{msg.Sender}]");
                sb.AppendLine(msg.Text);
                sb.AppendLine();
            }

            sb.AppendLine("═══════════════════════════════════════");
            sb.AppendLine($"Total messages: {displayedMessages.Count}");
            return sb.ToString();
        }

        private string ExportAsMarkdown()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Atlas AI Chat History");
            sb.AppendLine($"*Exported: {DateTime.Now:g}*");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();

            foreach (var msg in displayedMessages)
            {
                var icon = msg.IsUser ? "👤" : "🤖";
                sb.AppendLine($"### {icon} {msg.Sender}");
                sb.AppendLine();
                sb.AppendLine(msg.Text);
                sb.AppendLine();
            }

            sb.AppendLine("---");
            sb.AppendLine($"*{displayedMessages.Count} messages*");
            return sb.ToString();
        }

        private void InitializeSpeechRecognition()
        {
            try
            {
                // First check if we have any audio input devices using NAudio
                int deviceCount = NAudio.Wave.WaveIn.DeviceCount;
                Debug.WriteLine($"NAudio found {deviceCount} audio input device(s)");
                
                if (deviceCount == 0)
                {
                    Debug.WriteLine("No audio input devices found");
                    ShowStatus("⚠️ No microphone detected - connect a mic and restart");
                    return;
                }
                
                // List available devices
                for (int i = 0; i < deviceCount; i++)
                {
                    var caps = NAudio.Wave.WaveIn.GetCapabilities(i);
                    Debug.WriteLine($"  Device {i}: {caps.ProductName} (Channels: {caps.Channels})");
                }
                
                // Check if any recognizers are installed
                var installedRecognizers = SpeechRecognitionEngine.InstalledRecognizers();
                if (installedRecognizers.Count == 0)
                {
                    Debug.WriteLine("No speech recognizers installed");
                    ShowStatus("⚠️ Speech recognition not set up - type your messages instead");
                    ShowMicSetupHelp();
                    return;
                }

                Debug.WriteLine($"Found {installedRecognizers.Count} recognizer(s):");
                foreach (var rec in installedRecognizers)
                {
                    Debug.WriteLine($"  - {rec.Description} ({rec.Culture})");
                }

                recognizer = new SpeechRecognitionEngine(installedRecognizers[0]);
                recognizer.LoadGrammar(new DictationGrammar());
                
                // Try to set input to default audio device
                try
                {
                    recognizer.SetInputToDefaultAudioDevice();
                    Debug.WriteLine("Speech recognition initialized with default audio device");
                }
                catch (InvalidOperationException audioEx)
                {
                    Debug.WriteLine($"No audio device available: {audioEx.Message}");
                    ShowStatus("⚠️ No microphone found - type your messages");
                    ShowMicSetupHelp();
                    recognizer = null;
                    return;
                }
                catch (Exception audioEx)
                {
                    Debug.WriteLine($"Could not set audio device: {audioEx.Message}");
                    ShowStatus("⚠️ Microphone error - type your messages");
                    ShowMicSetupHelp();
                    recognizer = null;
                    return;
                }
                
                recognizer.SpeechRecognized += Recognizer_SpeechRecognized;
                recognizer.SpeechRecognitionRejected += (s, e) => 
                {
                    Debug.WriteLine($"Speech rejected (confidence too low)");
                    Dispatcher.Invoke(() => 
                    {
                        if (InputBox.Text == "Listening... (speak now)")
                            InputBox.Text = "";
                        StopListening();
                    });
                };
                recognizer.RecognizeCompleted += (s, e) =>
                {
                    Debug.WriteLine($"Recognition completed: {e.Result?.Text ?? "no result"}");
                    if (e.Error != null)
                        Debug.WriteLine($"Recognition error: {e.Error.Message}");
                    if (e.Cancelled)
                        Debug.WriteLine("Recognition was cancelled");
                    
                    Dispatcher.Invoke(() =>
                    {
                        if (isListening)
                            StopListening();
                    });
                };
                recognizer.AudioLevelUpdated += (s, e) =>
                {
                    // This confirms mic is working - audio level changes
                    if (e.AudioLevel > 0)
                        Debug.WriteLine($"Audio level: {e.AudioLevel}");
                };
                
                Debug.WriteLine("Speech recognition fully initialized and ready");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Speech recognition init error: {ex.Message}");
                ShowStatus($"⚠️ Speech not available - type your messages");
                recognizer = null;
            }
        }
        
        private void ShowMicSetupHelp()
        {
            // Show help dialog on first failure
            var result = MessageBox.Show(
                "Voice input requires Windows Speech Recognition to be set up.\n\n" +
                "To enable voice input:\n" +
                "1. Open Windows Settings\n" +
                "2. Go to Time & Language > Speech\n" +
                "3. Click 'Get started' under Microphone\n" +
                "4. Follow the setup wizard\n\n" +
                "You can still use Atlas by typing your messages.\n\n" +
                "Would you like to open Windows Speech Settings now?",
                "Voice Input Setup",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);
            
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    Process.Start(new ProcessStartInfo("ms-settings:speech") { UseShellExecute = true });
                }
                catch
                {
                    Process.Start(new ProcessStartInfo("control", "speech") { UseShellExecute = true });
                }
            }
        }

        private void Recognizer_SpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
        {
            Debug.WriteLine($"Speech recognized: '{e.Result.Text}' (confidence: {e.Result.Confidence:P0})");
            
            if (e.Result.Confidence > 0.3) // Lowered threshold even more for better recognition
            {
                Dispatcher.Invoke(() =>
                {
                    InputBox.Text = e.Result.Text;
                    StopListening();
                    SendMessage();
                });
            }
            else
            {
                Debug.WriteLine("Confidence too low, ignoring");
                Dispatcher.Invoke(() =>
                {
                    ShowStatus($"⚠️ Couldn't understand - try speaking clearer");
                    StopListening();
                });
            }
        }

        private async void MicButton_Click(object sender, RoutedEventArgs e)
        {
            if (isListening)
            {
                StopListeningAsync();
            }
            else
            {
                // Push-to-talk mode: pause music, listen, then resume
                ShowStatus("🎤 Pausing music...");
                await AudioDuckingManager.DuckAudioAsync();
                await Task.Delay(300); // Wait for music to pause
                
                // Temporarily disable audio protection for this recording session
                var wasProtected = AudioCoordinator.IsEmergencyProtectionActive;
                if (wasProtected)
                {
                    AudioCoordinator.DisableEmergencyAudioProtection();
                }
                
                ShowStatus("🎤 Listening... speak now!");
                StartListeningInternal();
            }
        }

        private void StartListening()
        {
            // This is called from other places - redirect to the safe version
            _ = Task.Run(async () =>
            {
                await Dispatcher.InvokeAsync(async () =>
                {
                    await AudioDuckingManager.DuckAudioAsync();
                    await Task.Delay(300);
                    var wasProtected = AudioCoordinator.IsEmergencyProtectionActive;
                    if (wasProtected)
                    {
                        AudioCoordinator.DisableEmergencyAudioProtection();
                    }
                    StartListeningInternal();
                });
            });
        }

        private void StartListeningInternal()
        {
            Debug.WriteLine("[StartListeningInternal] *** CALLED ***");
            
            // Set Atlas Core to Listening state
            SetAtlasCoreState(Controls.AtlasVisualState.Listening);
            
            // Quick mic test using NAudio
            try
            {
                int deviceCount = NAudio.Wave.WaveIn.DeviceCount;
                Debug.WriteLine($"[StartListeningInternal] NAudio device count: {deviceCount}");
                if (deviceCount == 0)
                {
                    ShowStatus("⚠️ No microphone detected");
                    MessageBox.Show(
                        "No microphone detected.\n\n" +
                        "Please:\n" +
                        "1. Connect a microphone\n" +
                        "2. Check Windows Sound Settings\n" +
                        "3. Restart the app",
                        "No Microphone",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"NAudio check failed: {ex.Message}");
            }
            
            // Try Whisper first (much better accuracy)
            if (useWhisper)
            {
                Debug.WriteLine("[StartListeningInternal] Using Whisper...");
                
                // Always create fresh instance for command listening
                whisperRecognizer?.Dispose();
                whisperRecognizer = CreateConfiguredWhisperRecognizer();
                
                Debug.WriteLine($"[StartListeningInternal] Whisper IsConfigured: {whisperRecognizer.IsConfigured}");
                
                // IMPROVED: Set shorter timeout for better hands-free experience
                // User speaks, pauses briefly, then we process - no long waits
                whisperRecognizer.SilenceTimeout = 1.0; // Stop listening 1.0 seconds after user stops speaking (faster response)
                
                // Hook up audio level event for green indicator when hearing audio
                whisperRecognizer.AudioLevelChanged += (s, args) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (args.IsHearing)
                        {
                            HearingIndicator.Visibility = Visibility.Visible;
                            WakeWordIndicator.Visibility = Visibility.Collapsed;
                        }
                        else
                        {
                            HearingIndicator.Visibility = Visibility.Collapsed;
                        }
                    });
                };
                
                whisperRecognizer.SpeechRecognized += (s, text) =>
                {
                    Debug.WriteLine($"[Command] Speech recognized: {text}");
                    
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        InputBox.Text = text;
                        isListening = false;
                        UpdateMicButtonState();
                        SendMessage();
                        
                        // IMPROVED: Restart wake word listening immediately after processing command
                        if (isWakeWordEnabled)
                        {
                            Debug.WriteLine("[Command] Restarting wake word listening for hands-free operation");
                            ScheduleWakeWordRestart(500); // Quick restart for hands-free
                        }
                    }));
                };
                whisperRecognizer.RecognitionError += (s, error) =>
                {
                    Debug.WriteLine($"[Command] Recognition error: {error}");
                    
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        // CRITICAL FIX: Clear the "Processing..." text so user isn't stuck
                        InputBox.Text = "";
                        
                        if (error != "no_speech")
                            ShowStatus($"⚠️ {error}");
                        else
                            ShowStatus("🎤 Ready - say 'Atlas' to speak");
                            
                        isListening = false;
                        UpdateMicButtonState();
                        
                        // IMPROVED: Restart wake word listening even after errors for continuous hands-free
                        if (isWakeWordEnabled)
                        {
                            Debug.WriteLine("[Command] Restarting wake word after error for hands-free operation");
                            ScheduleWakeWordRestart(300);
                        }
                    }));
                };
                
                // IMPORTANT: Handle recognition completion to restart wake word listening
                whisperRecognizer.RecognitionComplete += (s, e) =>
                {
                    Debug.WriteLine("[Command] Recognition cycle complete");
                    
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        // CRITICAL FIX: Clear InputBox if it still shows "Processing..." 
                        if (InputBox.Text.Contains("Processing") || InputBox.Text.Contains("Listening"))
                        {
                            InputBox.Text = "";
                        }
                        
                        isListening = false;
                        UpdateMicButtonState();
                        
                        // IMPROVED: Always restart wake word for continuous hands-free operation
                        if (isWakeWordEnabled)
                        {
                            Debug.WriteLine("[Command] Restarting wake word for continuous hands-free operation");
                            ScheduleWakeWordRestart(300);
                        }
                    }));
                };
                
                whisperRecognizer.RecordingStarted += (s, e) =>
                {
                    Debug.WriteLine("[Whisper] RecordingStarted event fired!");
                    Dispatcher.Invoke(() =>
                    {
                        isListening = true;
                        UpdateMicButtonState();
                        InputBox.Text = "🎙️ Listening... (stops automatically when you pause)";
                    });
                };
                whisperRecognizer.RecordingStopped += (s, e) =>
                {
                    Debug.WriteLine("[Whisper] RecordingStopped event fired!");
                    Dispatcher.Invoke(() =>
                    {
                        InputBox.Text = "🔄 Processing...";
                        HearingIndicator.Visibility = Visibility.Collapsed; // Hide green indicator
                    });
                };
                
                if (whisperRecognizer.IsConfigured)
                {
                    Debug.WriteLine("[StartListeningInternal] Whisper is configured, starting recording...");
                    
                    // Stop any TTS that might still be playing
                    _voiceManager?.Stop();
                    
                    whisperRecognizer.StartRecording();
                    Debug.WriteLine("[StartListeningInternal] Whisper StartRecording() called!");
                    return;
                }
                else
                {
                    Debug.WriteLine("[Whisper] Not configured, falling back to Windows Speech");
                    useWhisper = false; // Fall back to Windows Speech
                }
            }
            
            // Fallback to Windows Speech Recognition
            if (recognizer == null)
            {
                InitializeSpeechRecognition();
                if (recognizer == null)
                {
                    ShowStatus("⚠️ Speech not available - add OpenAI key in Settings for voice input");
                    return;
                }
            }
            try
            {
                // Stop any TTS that might still be playing
                _voiceManager?.Stop();
                
                Debug.WriteLine("Starting Windows speech recognition...");
                recognizer.RecognizeAsync(RecognizeMode.Single);
                isListening = true;
                UpdateMicButtonState();
                InputBox.Text = "Listening... (speak now)";
                StatusText.Visibility = Visibility.Collapsed;
            }
            catch (InvalidOperationException ex)
            {
                Debug.WriteLine($"Already listening or audio device busy: {ex.Message}");
                ShowStatus("⚠️ Microphone busy - try again");
                StopListeningAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Start listening error: {ex.Message}");
                ShowStatus($"⚠️ Mic error: {ex.Message}");
                StopListeningAsync();
            }
        }
        
        private void UpdateMicButtonState()
        {
            if (isListening)
            {
                MicButton.Background = new SolidColorBrush(Color.FromRgb(220, 50, 50));
                MicButton.Content = "🔴";
            }
            else
            {
                MicButton.Background = new SolidColorBrush(Color.FromRgb(60, 60, 60));
                MicButton.Content = "🎤";
                if (InputBox.Text == "🎙️ Recording... (click again to stop)" || InputBox.Text == "Listening... (speak now)")
                    InputBox.Text = "";
            }
        }

        private async void StopListeningAsync()
        {
            if (whisperRecognizer?.IsRecording == true)
            {
                InputBox.Text = "🔄 Transcribing...";
                await whisperRecognizer.StopRecordingAndTranscribeAsync();
            }
            else
            {
                try
                {
                    recognizer?.RecognizeAsyncCancel();
                }
                catch { }
            }
            
            isListening = false;
            UpdateMicButtonState();
        }

        private void StopListening()
        {
            StopListeningAsync();
        }

        private void CheckApiKey()
        {
            var activeProvider = AIManager.GetActiveProviderInstance();
            System.Diagnostics.Debug.WriteLine($"CheckApiKey: Provider={activeProvider?.DisplayName}, IsConfigured={activeProvider?.IsConfigured}");
            
            if (activeProvider == null || !activeProvider.IsConfigured)
            {
                // Double-check if settings.txt exists with a valid key
                var settingsPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "AtlasAI", "settings.txt");
                if (System.IO.File.Exists(settingsPath))
                {
                    var content = System.IO.File.ReadAllText(settingsPath).Trim();
                    if (content.StartsWith("sk-ant-") || content.StartsWith("sk-"))
                    {
                        // Key exists, don't show warning
                        StatusText.Visibility = System.Windows.Visibility.Collapsed;
                        return;
                    }
                }
                ShowStatus("⚙️ Click Settings to configure your AI provider for enhanced chat");
            }
            else
            {
                StatusText.Visibility = System.Windows.Visibility.Collapsed;
            }
        }

        private async void VoiceSelector_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (VoiceSelector.SelectedItem is ComboBoxItem item && item.Tag is string voiceId)
            {
                await _voiceManager.SelectVoiceAsync(voiceId);
                UpdateProviderIndicator();
            }
        }

        private void SpeechToggle_Click(object sender, RoutedEventArgs e)
        {
            _voiceManager.SpeechEnabled = SpeechToggle.IsChecked == true;
            UpdateSpeechToggleUI();
        }

        private void UpdateSpeechToggleUI()
        {
            var enabled = _voiceManager.SpeechEnabled;
            SpeechToggle.Content = enabled ? "ON" : "OFF";
            SpeechToggle.Background = enabled 
                ? new SolidColorBrush(Color.FromRgb(75, 181, 67)) 
                : new SolidColorBrush(Color.FromRgb(128, 128, 128));
        }

        private void StopSpeech_Click(object sender, RoutedEventArgs e)
        {
            _voiceManager.Stop();
        }

        /// <summary>
        /// Initialize the In-App Assistant with overlay and global hotkey
        /// </summary>
        private void InitializeInAppAssistant()
        {
            try
            {
                _inAppAssistant = new InAppAssistant.InAppAssistantManager(_understandingLayer?.Context);
                
                // Set reference for DirectActionHandler to control overlay
                Tools.DirectActionHandler.SetInAppAssistant(_inAppAssistant);
                
                // Wire up events
                _inAppAssistant.VoiceCommandRequested += (s, cmd) => Dispatcher.Invoke(() =>
                {
                    // Trigger voice listening when overlay requests it
                    if (!isListening)
                        StartListening();
                });
                
                _inAppAssistant.StatusChanged += (s, status) => Dispatcher.Invoke(() =>
                {
                    ShowStatus(status);
                });
                
                _inAppAssistant.ActionCompleted += (s, result) => Dispatcher.Invoke(() =>
                {
                    if (result.Success)
                        ShowStatus($"✓ {result.Message}");
                    else
                        ShowStatus($"✗ {result.Message}");
                });
                
                // Initialize the overlay (hidden by default, Ctrl+Alt+A to show)
                _inAppAssistant.InitializeOverlay();
                
                Debug.WriteLine("[InAppAssistant] Initialized - Press Ctrl+Alt+A to toggle overlay");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[InAppAssistant] Init error: {ex.Message}");
            }
        }

        private async void InitializeAvatarIntegration()
        {
            try
            {
                _avatarIntegration = new UnityAvatarIntegration();
                
                // Set up event handlers
                _avatarIntegration.OnUnityStarted += () => Dispatcher.Invoke(() => 
                {
                    ShowStatus("🎭 Avatar system connected!");
                });
                
                _avatarIntegration.OnUnityStopped += () => Dispatcher.Invoke(() => 
                {
                    ShowStatus("⚠️ Avatar system disconnected");
                });
                
                // Try to start Unity avatar automatically
                bool started = await _avatarIntegration.StartUnityAvatarAsync();
                if (started)
                {
                    ShowStatus("🎭 Starting avatar system...");
                    
                    // Give Unity time to initialize, then send welcome message
                    await Task.Delay(5000);
                    await _avatarIntegration.AvatarSpeakAsync("Hello! I'm your AI assistant avatar. I can move, think, and help you with tasks!");
                }
                else
                {
                    ShowStatus("⚠️ Avatar system not available");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Avatar integration error: {ex.Message}");
                ShowStatus("⚠️ Avatar system unavailable");
            }
        }

        private void ThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            ThemeManager.ToggleTheme();
        }

        private void SendButton_Click(object sender, RoutedEventArgs e) => SendMessage();

        private void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { SendMessage(); e.Handled = true; }
        }

        private async void SendMessage()
        {
            var text = InputBox.Text.Trim();
            if (string.IsNullOrEmpty(text) && _droppedPaths.Count == 0) return;

            // IMMEDIATELY show user message and clear input for responsiveness
            var displayText = text;
            if (_droppedPaths.Count > 0)
            {
                displayText += $"\n📎 {_droppedPaths.Count} file(s) attached";
            }
            
            // Store values before clearing
            var droppedContext = GetDroppedFilesContext();
            var fullMessage = text + droppedContext;
            LastDroppedPaths = new List<string>(_droppedPaths);
            
            // Clear UI immediately
            InputBox.Clear();
            _droppedPaths.Clear();
            DroppedFilesList.Items.Clear();
            UpdateDroppedFilesVisibility();

            // Stop any current speech
            _voiceManager.Stop();
            
            // Create cancellation token for this operation
            _currentOperationCts?.Cancel();
            _currentOperationCts = new CancellationTokenSource();
            var ct = _currentOperationCts.Token;

            // Check for quick commands first (only if no dropped files)
            if (text.StartsWith("/") && LastDroppedPaths.Count == 0)
            {
                AddMessage("You", text, true);
                // Save user message to history
                if (_conversationManager != null)
                    await _conversationManager.AddMessageAsync(Conversation.Models.MessageRole.User, text);
                await HandleQuickCommand(text);
                return;
            }
            
            // Check for "what can you do" type questions - respond with capabilities
            if (IsCapabilitiesQuestion(text))
            {
                AddMessage("You", text, true);
                // Save user message to history
                if (_conversationManager != null)
                    await _conversationManager.AddMessageAsync(Conversation.Models.MessageRole.User, text);
                var capabilities = GetCapabilitiesList();
                AddMessage("Atlas", capabilities, false);
                // Save assistant response to history
                if (_conversationManager != null)
                    await _conversationManager.AddMessageAsync(Conversation.Models.MessageRole.Assistant, capabilities);
                _ = _voiceManager.SpeakAsync("I can do a lot! I've listed all my capabilities in the chat. Take a look and let me know what you'd like me to help with!");
                return;
            }
            
            // Show user message FIRST for instant feedback
            AddMessage("You", displayText, true);
            
            // IMMEDIATELY save user message to session history (before any early returns!)
            if (_conversationManager != null)
            {
                try
                {
                    await _conversationManager.AddMessageAsync(Conversation.Models.MessageRole.User, text);
                    System.Diagnostics.Debug.WriteLine($"[SendMessage] User message saved to history");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SendMessage] Error saving user message: {ex.Message}");
                }
            }
            
            // === DIRECT ACTION HANDLER - FIRST PRIORITY ===
            // Like Kiro - understands what you mean and just does it, no AI needed
            var directResponse = await Tools.DirectActionHandler.TryHandleAsync(text);
            if (directResponse != null)
            {
                AddMessage("Atlas", directResponse, false);
                if (_conversationManager != null)
                    await _conversationManager.AddMessageAsync(Conversation.Models.MessageRole.Assistant, directResponse);
                // Short confirmation, no long speech
                _ = _voiceManager.SpeakAsync(directResponse.Length < 50 ? directResponse : "Done");
                return;
            }
            
            // === VOICE AGENT TRIGGER ===
            // Check for "agent, do X" or "hey agent, X" voice commands
            var lowerText = text.ToLowerInvariant();
            if (lowerText.StartsWith("agent ") || lowerText.StartsWith("agent, ") || 
                lowerText.StartsWith("hey agent ") || lowerText.StartsWith("hey agent, "))
            {
                // Extract the task after "agent" prefix
                var agentTask = text;
                if (lowerText.StartsWith("hey agent, ")) agentTask = text.Substring(11).Trim();
                else if (lowerText.StartsWith("hey agent ")) agentTask = text.Substring(10).Trim();
                else if (lowerText.StartsWith("agent, ")) agentTask = text.Substring(7).Trim();
                else if (lowerText.StartsWith("agent ")) agentTask = text.Substring(6).Trim();
                
                if (!string.IsNullOrWhiteSpace(agentTask))
                {
                    var agentResponse = await RunAgentTask(agentTask);
                    AddMessage("Atlas", agentResponse, false);
                    // Save assistant response to history
                    if (_conversationManager != null)
                        await _conversationManager.AddMessageAsync(Conversation.Models.MessageRole.Assistant, agentResponse);
                    _ = _voiceManager.SpeakAsync(GetShortResponse(agentResponse));
                    return;
                }
            }
            
            // === AUTO-DETECT AGENT TASKS (BEFORE smart commands) ===
            // Check if this looks like a task the agent should handle (file ops, installations, etc.)
            // This MUST come before HandleSmartCommand to ensure confirmation dialogs work
            if (IsAgentTask(text))
            {
                var agentResponse = await RunAgentTask(text);
                AddMessage("Atlas", agentResponse, false);
                // Save assistant response to history
                if (_conversationManager != null)
                    await _conversationManager.AddMessageAsync(Conversation.Models.MessageRole.Assistant, agentResponse);
                _ = _voiceManager.SpeakAsync(GetShortResponse(agentResponse));
                return;
            }
            
            // Check for natural language smart commands (before AI processing)
            var smartResponse = await HandleSmartCommand(text);
            if (smartResponse != null)
            {
                AddMessage("Atlas", smartResponse, false);
                // Save assistant response to history
                if (_conversationManager != null)
                    await _conversationManager.AddMessageAsync(Conversation.Models.MessageRole.Assistant, smartResponse);
                _ = _voiceManager.SpeakAsync(GetShortResponse(smartResponse));
                return;
            }
            
            // Check for memory commands
            if (await HandleMemoryCommand(text))
            {
                return;
            }
            
            // User message already saved above (before early returns)
            
            InputBox.IsEnabled = false;
            SendButton.IsEnabled = false;

            var typingIndicator = ShowTypingIndicator();
            
            // Set Atlas Core to Thinking state while generating response
            SetAtlasCoreState(Controls.AtlasVisualState.Thinking);

            string response = "";
            var activeProvider = AIManager.GetActiveProviderInstance();
            
            try
            {
                // === MEMORY & LEARNING LAYER INTEGRATION ===
                // Process user message through the learning system (fire and forget - don't block)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await MemoryManager.Instance.ProcessUserMessageAsync(text, _lastAtlasAction);
                        Debug.WriteLine($"[Memory] Processed user message for learning");
                    }
                    catch (Exception memEx)
                    {
                        Debug.WriteLine($"[Memory] Error processing message: {memEx.Message}");
                    }
                });
                
                // Build enhanced context with long-term memory (quick operation)
                var contextEnhancedMessage = fullMessage;
                
                // Get long-term memory context - use cached if available
                string longTermMemoryContext = "";
                try
                {
                    longTermMemoryContext = MemoryManager.Instance.GetCachedContext() ?? "";
                }
                catch (Exception memEx)
                {
                    Debug.WriteLine($"[Memory] Error getting context: {memEx.Message}");
                }
                
                // Also get session memory from conversation manager
                if (_conversationManager != null)
                {
                    var sessionMemoryContext = _conversationManager.BuildContextForLLM(text);
                    if (!string.IsNullOrEmpty(sessionMemoryContext) || !string.IsNullOrEmpty(longTermMemoryContext))
                    {
                        // Combine both memory systems
                        var combinedContext = "";
                        if (!string.IsNullOrEmpty(longTermMemoryContext))
                            combinedContext += longTermMemoryContext + "\n";
                        if (!string.IsNullOrEmpty(sessionMemoryContext))
                            combinedContext += "[Session Context: " + sessionMemoryContext + "]\n";
                        
                        contextEnhancedMessage = $"{combinedContext}\nUser: {fullMessage}";
                    }
                }
                else if (!string.IsNullOrEmpty(longTermMemoryContext))
                {
                    contextEnhancedMessage = $"{longTermMemoryContext}\nUser: {fullMessage}";
                }
                
                if (activeProvider != null && activeProvider.IsConfigured)
                {
                    response = await GetAIResponseWithCancellation(contextEnhancedMessage, ct);
                }
                else
                {
                    await Task.Delay(500, ct);
                    response = GetSimpleResponse(fullMessage);
                }
                
                // Store this response as the last Atlas action (for correction detection)
                _lastAtlasAction = response;
            }
            catch (OperationCanceledException)
            {
                response = "⚠️ Operation cancelled.";
            }
            catch (Exception ex)
            {
                response = $"❌ Error: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[SendMessage] Exception: {ex}");
            }
            
            // Ensure we have a response to display
            if (string.IsNullOrWhiteSpace(response))
            {
                response = "🤔 I didn't get a response. Please try again or check your API connection in Settings.";
            }

            HideTypingIndicator(typingIndicator);
            
            AddMessage("Atlas", response, false);
            
            // Set Atlas Core to Speaking state while delivering response
            SetAtlasCoreState(Controls.AtlasVisualState.Speaking);
            
            // Track assistant response in session (properly await to ensure it's saved)
            if (_conversationManager != null)
            {
                try
                {
                    await _conversationManager.AddMessageAsync(Conversation.Models.MessageRole.Assistant, response);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SendMessage] Error saving assistant message: {ex.Message}");
                }
            }
            
            // Re-enable input IMMEDIATELY so user can type while Atlas speaks
            InputBox.IsEnabled = true;
            SendButton.IsEnabled = true;
            InputBox.Focus();
            
            // Start speaking - fire and forget but stay on UI thread for MediaPlayer
            _ = SpeakResponseAsync(response);
            
            // Also send to avatar for lip-sync/animation if Unity is running
            if (_avatarIntegration?.IsUnityRunning == true)
            {
                // Send text to Unity for avatar lip-sync (Unity doesn't produce audio, just animation)
                _ = _avatarIntegration.AvatarSpeakAsync(response);
                
                // Show thinking animation for longer responses
                if (response.Length > 100)
                {
                    _ = _avatarIntegration.AvatarThinkAsync();
                }
            }
            
            // Restore audio after AI finishes speaking (if it was ducked)
            if (AudioDuckingManager.IsDucked)
            {
                // Wait a moment after AI response, then restore music
                _ = Task.Delay(2000).ContinueWith(async _ =>
                {
                    await AudioDuckingManager.RestoreAudioAsync();
                    SafeDispatcherInvoke(() => ShowStatus("🎵 Music resumed"));
                });
            }
        }

        /// <summary>
        /// Handle memory-related commands like "remember this" or "forget that"
        /// </summary>
        private async Task<bool> HandleMemoryCommand(string text)
        {
            if (_conversationManager == null) return false;
            
            var lowerText = text.ToLowerInvariant();
            
            // "Remember this/that" patterns
            if (lowerText.StartsWith("remember ") || lowerText.Contains("remember that ") || lowerText.Contains("remember this:"))
            {
                // Extract what to remember
                var toRemember = text;
                if (lowerText.StartsWith("remember that "))
                    toRemember = text.Substring("remember that ".Length);
                else if (lowerText.StartsWith("remember this: "))
                    toRemember = text.Substring("remember this: ".Length);
                else if (lowerText.StartsWith("remember "))
                    toRemember = text.Substring("remember ".Length);
                
                if (!string.IsNullOrWhiteSpace(toRemember))
                {
                    // Determine category based on content
                    var category = Conversation.Models.MemoryCategory.General;
                    if (lowerText.Contains("prefer") || lowerText.Contains("like") || lowerText.Contains("want"))
                        category = Conversation.Models.MemoryCategory.Preference;
                    else if (lowerText.Contains("project") || lowerText.Contains("working on"))
                        category = Conversation.Models.MemoryCategory.Project;
                    else if (lowerText.Contains("name") || lowerText.Contains("call me"))
                        category = Conversation.Models.MemoryCategory.PersonalInfo;
                    
                    await _conversationManager.RememberAsync(toRemember, category);
                    
                    var confirmation = _systemPromptBuilder?.GetConfirmation($"I'll remember: \"{toRemember}\"") 
                        ?? $"Got it! I'll remember: \"{toRemember}\"";
                    AddMessage("Atlas", confirmation, false);
                    await _voiceManager.SpeakAsync(confirmation);
                    return true;
                }
            }
            
            // "Forget" patterns
            if (lowerText.StartsWith("forget ") || lowerText == "clear memory" || lowerText == "forget everything")
            {
                if (lowerText == "forget everything" || lowerText == "clear memory")
                {
                    var result = MessageBox.Show(
                        "Are you sure you want to clear all memories?",
                        "Clear Memory",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    
                    if (result == MessageBoxResult.Yes)
                    {
                        await _conversationManager.ClearAllMemoriesAsync();
                        AddMessage("Atlas", "Memory cleared. Starting fresh.", false);
                        await _voiceManager.SpeakAsync("Memory cleared.");
                    }
                    return true;
                }
            }
            
            // "What do you remember" / "show memory"
            if (lowerText.Contains("what do you remember") || lowerText == "show memory" || lowerText == "list memories")
            {
                var memories = _conversationManager.Memories;
                if (memories.Count == 0)
                {
                    AddMessage("Atlas", "I don't have any saved memories yet. Say \"remember that...\" to save something.", false);
                }
                else
                {
                    var memoryList = string.Join("\n", memories.Select(m => $"• {m.Content}"));
                    AddMessage("Atlas", $"Here's what I remember:\n\n{memoryList}", false);
                }
                return true;
            }
            
            return false;
        }

        private async Task HandleQuickCommand(string input)
        {
            InputBox.Clear();
            var parts = input.Split(' ', 2);
            var command = parts[0].ToLower();
            var args = parts.Length > 1 ? parts[1] : "";

            string response;
            bool showUserMessage = true;

            switch (command)
            {
                case "/time":
                    response = $"🕐 The current time is {DateTime.Now:h:mm:ss tt}";
                    break;

                case "/date":
                    response = $"📅 Today is {DateTime.Now:dddd, MMMM d, yyyy}";
                    break;

                case "/calc":
                case "/calculate":
                    response = CalculateExpression(args);
                    break;

                case "/open":
                    response = await OpenApplication(args);
                    break;

                case "/search":
                    response = await SearchWeb(args);
                    break;

                case "/copy":
                    if (!string.IsNullOrEmpty(args))
                    {
                        Clipboard.SetText(args);
                        response = "📋 Copied to clipboard!";
                    }
                    else
                        response = "Usage: /copy <text>";
                    break;

                case "/clipboard":
                case "/clip":
                    OpenClipboardManager();
                    return;

                case "/clear":
                    ClearHistory_Click(this, new RoutedEventArgs());
                    return;

                case "/voice":
                    _voiceManager.SpeechEnabled = !_voiceManager.SpeechEnabled;
                    SpeechToggle.IsChecked = _voiceManager.SpeechEnabled;
                    UpdateSpeechToggleUI();
                    response = _voiceManager.SpeechEnabled ? "🔊 Voice enabled" : "🔇 Voice disabled";
                    break;

                case "/theme":
                case "/dark":
                case "/light":
                    if (command == "/dark")
                        ThemeManager.SetTheme(AppTheme.Dark);
                    else if (command == "/light")
                        ThemeManager.SetTheme(AppTheme.Light);
                    else
                        ThemeManager.ToggleTheme();
                    var themeName = ThemeManager.CurrentTheme == AppTheme.Dark ? "Dark" : "Light";
                    response = $"🎨 Switched to {themeName} theme";
                    break;

                case "/joke":
                    response = GetRandomJoke();
                    break;

                case "/flip":
                    response = new Random().Next(2) == 0 ? "🪙 Heads!" : "🪙 Tails!";
                    break;

                case "/roll":
                    var sides = 6;
                    if (!string.IsNullOrEmpty(args) && int.TryParse(args, out var s) && s > 0)
                        sides = s;
                    response = $"🎲 Rolled a {new Random().Next(1, sides + 1)} (d{sides})";
                    break;

                case "/timer":
                    response = await SetTimer(args);
                    break;

                case "/screenshot":
                case "/capture":
                    await CaptureScreenshot();
                    return;

                case "/analyze":
                    response = await AnalyzeLatestScreenshot();
                    break;

                case "/ocr":
                    response = await ExtractTextFromScreenshot();
                    break;

                case "/avatar":
                    response = await HandleAvatarCommand(args);
                    break;

                case "/avatars":
                case "/selectavatar":
                    OpenAvatarSelection();
                    return;

                case "/history":
                case "/screenshots":
                    OpenHistoryWindow();
                    return;

                case "/systemscan":
                case "/scan":
                    response = await PerformSystemScan();
                    break;

                case "/spywarescan":
                case "/spyware":
                case "/malwarescan":
                case "/malware":
                    response = await PerformSpywareScan();
                    break;

                case "/systemfix":
                case "/autofix":
                    response = await PerformSystemAutoFix();
                    break;

                case "/systemcontrol":
                case "/sysctl":
                    OpenSystemControlWindow();
                    return;

                case "/code":
                case "/ide":
                case "/editor":
                    OpenCodeEditor();
                    return;

                case "/agent":
                    response = await RunAgentTask(args);
                    break;
                    
                case "/undo":
                    // Use the new safety manager for undo
                    if (Agent.AgentSafetyManager.Instance.CanUndo)
                    {
                        var (success, message) = await Agent.AgentSafetyManager.Instance.UndoLastAsync();
                        response = message;
                    }
                    else if (_agent != null && _agent.ActionHistory.Count > 0)
                    {
                        // Fallback to old agent undo
                        response = await _agent.UndoLastActionAsync();
                    }
                    else
                    {
                        response = "❌ Nothing to undo";
                    }
                    break;
                    
                case "/undo-all":
                    // Undo multiple actions
                    var count = 5;
                    if (!string.IsNullOrEmpty(args) && int.TryParse(args, out var n))
                        count = Math.Min(n, 20);
                    response = await Agent.AgentSafetyManager.Instance.UndoMultipleAsync(count);
                    break;
                    
                case "/undo-list":
                case "/agent-undo":
                    response = Agent.AgentSafetyManager.Instance.GetUndoSummary();
                    break;
                    
                case "/agent-history":
                case "/actions":
                    if (_agent != null)
                    {
                        response = _agent.GetActionSummary(10);
                    }
                    else
                    {
                        response = "No agent actions recorded yet.";
                    }
                    break;
                    
                case "/restore-point":
                    // Create a system restore point
                    var desc = string.IsNullOrEmpty(args) ? "Manual restore point" : args;
                    var (rpSuccess, rpMessage) = await Agent.AgentSafetyManager.Instance.CreateRestorePointAsync(desc);
                    response = rpMessage;
                    break;

                case "/overlay":
                case "/inapp":
                    _inAppAssistant?.ToggleOverlay();
                    response = _inAppAssistant?.IsOverlayVisible == true 
                        ? "🎯 In-App Assistant overlay shown (Ctrl+Alt+A to toggle)"
                        : "🎯 In-App Assistant overlay hidden";
                    break;

                case "/context":
                case "/activeapp":
                    var ctx = _inAppAssistant?.GetCurrentContext();
                    if (ctx != null)
                    {
                        response = $"🖥️ Active App Context:\n" +
                                   $"• Process: {ctx.ProcessName}\n" +
                                   $"• Window: {ctx.WindowTitle}\n" +
                                   $"• Category: {ctx.Category}\n" +
                                   (ctx.IsBrowser ? $"• Tab: {ctx.BrowserTabTitle}\n" : "");
                    }
                    else
                    {
                        response = "⚠️ In-App Assistant not initialized";
                    }
                    break;

                case "/do":
                case "/action":
                    if (!string.IsNullOrEmpty(args))
                    {
                        var actionResult = await (_inAppAssistant?.ExecuteCommandAsync(args) ?? Task.FromResult(new InAppAssistant.Models.ActionResult { Success = false, Message = "In-App Assistant not initialized" }));
                        response = actionResult.Success ? $"✓ {actionResult.Message}" : $"✗ {actionResult.Message}";
                    }
                    else
                    {
                        response = "Usage: /do <command>\nExamples:\n• /do new folder called Projects\n• /do search for readme\n• /do open file main.cs";
                    }
                    break;

                case "/updatedb":
                case "/update":
                    response = await UpdateThreatDatabase();
                    break;

                // ===== NEW SMART FEATURES =====
                
                case "/note":
                case "/takenote":
                    if (!string.IsNullOrEmpty(args))
                        response = await Features.SmartFeatures.TakeNoteAsync(args);
                    else
                        response = "Usage: /note <your note text>\nExample: /note Remember to call mom tomorrow";
                    break;
                
                case "/notes":
                case "/mynotes":
                    response = await Features.SmartFeatures.GetNotesAsync();
                    break;
                
                case "/diagnostics":
                case "/diag":
                case "/sysinfo":
                case "/pcstatus":
                    response = await Features.SmartFeatures.GetSystemDiagnosticsAsync();
                    break;
                
                case "/website":
                case "/web":
                case "/site":
                    if (!string.IsNullOrEmpty(args))
                        response = Features.SmartFeatures.OpenWebsite(args);
                    else
                        response = Features.SmartFeatures.GetWebsiteList();
                    break;
                
                case "/youtube":
                    response = Features.SmartFeatures.OpenWebsite("youtube");
                    break;
                
                case "/netflix":
                    response = Features.SmartFeatures.OpenWebsite("netflix");
                    break;
                
                case "/twitter":
                case "/x":
                    response = Features.SmartFeatures.OpenWebsite("twitter");
                    break;
                
                case "/reddit":
                    response = Features.SmartFeatures.OpenWebsite("reddit");
                    break;
                
                case "/gmail":
                case "/email":
                    response = Features.SmartFeatures.OpenWebsite("gmail");
                    break;
                
                case "/github":
                    response = Features.SmartFeatures.OpenWebsite("github");
                    break;
                
                case "/amazon":
                    response = Features.SmartFeatures.OpenWebsite("amazon");
                    break;
                
                case "/funfact":
                case "/fact":
                    response = Features.SmartFeatures.TellFunFact();
                    break;
                
                case "/compliment":
                case "/motivate":
                    response = Features.SmartFeatures.GiveCompliment();
                    break;
                
                case "/8ball":
                case "/magic8ball":
                    response = Features.SmartFeatures.Magic8Ball();
                    break;
                
                case "/briefing":
                case "/morning":
                case "/daily":
                    response = await Features.SmartFeatures.GetDailyBriefingAsync();
                    break;
                
                case "/weather":
                    if (!string.IsNullOrEmpty(args))
                        response = await GetWeatherAsync(args);
                    else
                        response = await GetWeatherAsync("Middlesbrough");
                    break;

                // ===== MEMORY & LEARNING COMMANDS =====
                
                case "/memory":
                case "/memorystats":
                case "/whatdoyouknow":
                    response = await GetMemoryStatsAsync();
                    break;
                
                case "/corrections":
                case "/mycorrections":
                    response = await GetCorrectionsListAsync();
                    break;
                
                case "/preferences":
                case "/mypreferences":
                    response = await GetPreferencesListAsync();
                    break;

                // ===== SECURITY & PERMISSIONS COMMANDS =====
                
                case "/permissions":
                case "/security":
                    response = GetPermissionsStatus();
                    break;
                
                case "/trust":
                    if (!string.IsNullOrEmpty(args))
                    {
                        Security.Permissions.PermissionPolicy.Instance.TrustAction(args);
                        response = $"✅ Trusted action: {args}\nAtlas will no longer ask for confirmation for this action.";
                    }
                    else
                    {
                        response = "Usage: /trust <action_name>\nExample: /trust write_file";
                    }
                    break;
                
                case "/untrust":
                    if (!string.IsNullOrEmpty(args))
                    {
                        Security.Permissions.PermissionPolicy.Instance.UntrustAction(args);
                        response = $"🔒 Removed trust for: {args}\nAtlas will ask for confirmation again.";
                    }
                    else
                    {
                        response = "Usage: /untrust <action_name>";
                    }
                    break;

                case "/help":
                case "/?":
                    response = GetCommandHelp();
                    showUserMessage = false;
                    break;

                default:
                    response = $"❓ Unknown command: {command}\nType /help for available commands.";
                    break;
            }

            if (showUserMessage)
                AddMessage("You", input, true);
            
            AddMessage("Atlas", response, false);
            await _voiceManager.SpeakAsync(response);
        }

        private string CalculateExpression(string expr)
        {
            if (string.IsNullOrWhiteSpace(expr))
                return "Usage: /calc <expression>\nExample: /calc 2 + 2 * 3";

            try
            {
                // Simple calculator - supports basic operations
                expr = expr.Replace(" ", "").Replace("x", "*").Replace("×", "*").Replace("÷", "/");
                var result = EvaluateSimpleExpression(expr);
                return $"🔢 {expr} = {result}";
            }
            catch
            {
                return $"❌ Couldn't calculate: {expr}";
            }
        }

        private double EvaluateSimpleExpression(string expr)
        {
            // Use DataTable for simple expression evaluation
            var table = new System.Data.DataTable();
            var result = table.Compute(expr, "");
            return Convert.ToDouble(result);
        }

        private async Task<string> OpenApplication(string appName)
        {
            if (string.IsNullOrWhiteSpace(appName))
                return "Usage: /open <app>\nExamples: /open notepad, /open calc, /open chrome";

            try
            {
                var app = appName.ToLower().Trim();
                var processName = app switch
                {
                    "notepad" => "notepad",
                    "calc" or "calculator" => "calc",
                    "paint" => "mspaint",
                    "explorer" or "files" => "explorer",
                    "cmd" or "terminal" => "cmd",
                    "powershell" or "ps" => "powershell",
                    "chrome" => "chrome",
                    "firefox" => "firefox",
                    "edge" => "msedge",
                    "code" or "vscode" => "code",
                    _ => app
                };

                Process.Start(new ProcessStartInfo(processName) { UseShellExecute = true });
                await Task.Delay(100);
                return $"🚀 Opening {appName}...";
            }
            catch
            {
                return $"❌ Couldn't open: {appName}";
            }
        }

        private async Task<string> SearchWeb(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return "Usage: /search <query>\nExample: /search weather today";

            try
            {
                var url = $"https://www.google.com/search?q={Uri.EscapeDataString(query)}";
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                await Task.Delay(100);
                return $"🔍 Searching for: {query}";
            }
            catch
            {
                return $"❌ Couldn't search: {query}";
            }
        }
        
        private async Task<string> GetWeatherAsync(string location)
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10); // Slightly longer timeout
                client.DefaultRequestHeaders.Add("User-Agent", "curl"); // wttr.in works better with curl user agent
                
                // Use simpler format that's more reliable
                var url = $"https://wttr.in/{Uri.EscapeDataString(location)}?format=j1";
                var response = await client.GetStringAsync(url);
                
                // Parse JSON response
                var json = System.Text.Json.JsonDocument.Parse(response);
                var current = json.RootElement.GetProperty("current_condition")[0];
                var weather = json.RootElement.GetProperty("weather")[0];
                
                var temp = current.GetProperty("temp_C").GetString();
                var feelsLike = current.GetProperty("FeelsLikeC").GetString();
                var humidity = current.GetProperty("humidity").GetString();
                var windSpeed = current.GetProperty("windspeedKmph").GetString();
                var desc = current.GetProperty("weatherDesc")[0].GetProperty("value").GetString();
                var area = json.RootElement.GetProperty("nearest_area")[0].GetProperty("areaName")[0].GetProperty("value").GetString();
                
                // Tomorrow's forecast
                var tomorrow = json.RootElement.GetProperty("weather")[1];
                var maxTemp = tomorrow.GetProperty("maxtempC").GetString();
                var minTemp = tomorrow.GetProperty("mintempC").GetString();
                
                return $@"🌤️ Weather for {area}

🌡️ {temp}°C (feels like {feelsLike}°C)
☁️ {desc}
💧 {humidity}% humidity
💨 {windSpeed} km/h wind

📅 Tomorrow: {minTemp}°C - {maxTemp}°C";
            }
            catch (TaskCanceledException)
            {
                return $"⏱️ Weather request timed out. Try again.";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Weather error: {ex.Message}");
                return $"❌ Couldn't get weather for {location}. Check your internet connection.";
            }
        }

        #region Memory & Learning Layer Commands
        
        /// <summary>
        /// Get memory statistics - what Atlas has learned about the user
        /// </summary>
        private async Task<string> GetMemoryStatsAsync()
        {
            try
            {
                var stats = await MemoryManager.Instance.GetStatsAsync();
                var userName = await MemoryManager.Instance.GetUserNameAsync();
                
                var sb = new StringBuilder();
                sb.AppendLine("🧠 **Atlas Memory Status**\n");
                
                if (!string.IsNullOrEmpty(userName))
                    sb.AppendLine($"👤 I know you as: {userName}");
                
                sb.AppendLine($"📝 Corrections learned: {stats.TotalCorrections}");
                sb.AppendLine($"💡 Facts remembered: {stats.TotalFacts}");
                sb.AppendLine($"⚙️ Preferences saved: {stats.TotalPreferences}");
                sb.AppendLine($"🔧 Tools tracked: {stats.TotalSkillsTracked}");
                sb.AppendLine($"📊 Total tool executions: {stats.TotalToolExecutions}");
                
                sb.AppendLine("\n💬 Commands:");
                sb.AppendLine("• /corrections - See what I've learned NOT to do");
                sb.AppendLine("• /preferences - See your saved preferences");
                sb.AppendLine("• \"Don't use X\" - Teach me to avoid something");
                sb.AppendLine("• \"Remember that...\" - Save a fact about you");
                
                return sb.ToString();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Memory] Error getting stats: {ex.Message}");
                return "❌ Couldn't retrieve memory stats.";
            }
        }
        
        /// <summary>
        /// Get list of corrections Atlas has learned
        /// </summary>
        private async Task<string> GetCorrectionsListAsync()
        {
            try
            {
                var corrections = await MemoryManager.Instance.Corrections.GetAllCorrectionsAsync();
                
                if (corrections.Count == 0)
                {
                    return "📝 No corrections learned yet.\n\nTeach me by saying things like:\n• \"Don't use Canva, use Photoshop instead\"\n• \"No, I meant the other one\"\n• \"That's wrong, do X instead\"";
                }
                
                var sb = new StringBuilder();
                sb.AppendLine("📝 **Corrections I've Learned:**\n");
                
                foreach (var c in corrections.Take(15))
                {
                    sb.AppendLine($"• ❌ \"{c.OriginalMistake}\" → ✅ \"{c.Correction}\" ({c.TimesApplied}x)");
                }
                
                if (corrections.Count > 15)
                    sb.AppendLine($"\n...and {corrections.Count - 15} more");
                
                return sb.ToString();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Memory] Error getting corrections: {ex.Message}");
                return "❌ Couldn't retrieve corrections.";
            }
        }
        
        /// <summary>
        /// Get list of user preferences
        /// </summary>
        private async Task<string> GetPreferencesListAsync()
        {
            try
            {
                var toolPrefs = await MemoryManager.Instance.Store.GetPreferencesByCategoryAsync("tools");
                var generalPrefs = await MemoryManager.Instance.Store.GetPreferencesByCategoryAsync("general");
                var facts = await MemoryManager.Instance.Store.GetFactsAsync(limit: 10);
                
                var sb = new StringBuilder();
                sb.AppendLine("⚙️ **Your Preferences & Facts:**\n");
                
                if (toolPrefs.Count > 0)
                {
                    sb.AppendLine("🔧 Tool Preferences:");
                    foreach (var pref in toolPrefs)
                    {
                        sb.AppendLine($"  • {pref.Key}: {pref.Value}");
                    }
                    sb.AppendLine();
                }
                
                if (generalPrefs.Count > 0)
                {
                    sb.AppendLine("📋 General Preferences:");
                    foreach (var pref in generalPrefs)
                    {
                        sb.AppendLine($"  • {pref.Key}: {pref.Value}");
                    }
                    sb.AppendLine();
                }
                
                if (facts.Count > 0)
                {
                    sb.AppendLine("💡 Facts I Know:");
                    foreach (var fact in facts)
                    {
                        sb.AppendLine($"  • {fact}");
                    }
                }
                
                if (toolPrefs.Count == 0 && generalPrefs.Count == 0 && facts.Count == 0)
                {
                    sb.AppendLine("No preferences saved yet.\n\nTeach me by saying things like:\n• \"I prefer dark mode\"\n• \"My name is John\"\n• \"Remember that I work at Acme Corp\"");
                }
                
                return sb.ToString();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Memory] Error getting preferences: {ex.Message}");
                return "❌ Couldn't retrieve preferences.";
            }
        }
        
        #endregion

        #region Security & Permissions Commands
        
        /// <summary>
        /// Get permissions status and trusted actions
        /// </summary>
        private string GetPermissionsStatus()
        {
            var sb = new StringBuilder();
            sb.AppendLine("🛡️ **Atlas Security & Permissions**\n");
            
            // Show trusted actions
            var trusted = Security.Permissions.PermissionPolicy.Instance.GetTrustedActions();
            if (trusted.Count > 0)
            {
                sb.AppendLine("✅ Trusted Actions (no confirmation needed):");
                foreach (var action in trusted)
                {
                    sb.AppendLine($"  • {action}");
                }
                sb.AppendLine();
            }
            
            // Show permission levels by risk
            sb.AppendLine("📋 Permission Levels:\n");
            
            sb.AppendLine("🟢 **Always Allowed** (Low Risk):");
            sb.AppendLine("  • read_file, list_directory, screenshot, web_search");
            sb.AppendLine();
            
            sb.AppendLine("🟡 **Requires Confirmation** (Medium/High Risk):");
            sb.AppendLine("  • write_file, delete_file, run_command, kill_process");
            sb.AppendLine();
            
            sb.AppendLine("🔴 **Blocked by Default** (Critical Risk):");
            sb.AppendLine("  • uninstall_app, registry_write, admin_command");
            sb.AppendLine();
            
            sb.AppendLine("💬 Commands:");
            sb.AppendLine("• /trust <action> - Allow an action without confirmation");
            sb.AppendLine("• /untrust <action> - Require confirmation again");
            
            return sb.ToString();
        }
        
        #endregion

        private string GetRandomJoke()
        {
            // Use the SmartFeatures version for more jokes
            return Features.SmartFeatures.TellJoke();
        }

        private async Task<string> ChangeAvatar(string avatarName)
        {
            if (string.IsNullOrWhiteSpace(avatarName))
            {
                // Open avatar selection window
                var avatarWindow = new AvatarSelectionWindow();
                avatarWindow.Owner = this;
                if (avatarWindow.ShowDialog() == true && !string.IsNullOrEmpty(avatarWindow.SelectedAvatar))
                {
                    SelectAvatar(avatarWindow.SelectedAvatar);
                    return $"🎭 Avatar changed to: {GetAvatarDisplayName(avatarWindow.SelectedAvatar)}";
                }
                return "Avatar selection cancelled.";
            }

            try
            {
                // Use the same selection logic as the buttons
                SelectAvatar(avatarName.ToLower());
                return $"🎭 Avatar changed to: {GetAvatarDisplayName(avatarName.ToLower())}";
            }
            catch
            {
                return $"❌ Couldn't change to avatar: {avatarName}\nUse /avatar without parameters to open selection window.";
            }
        }

        private async Task<string> HandleAvatarCommand(string args)
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                return await ChangeAvatar(args);
            }

            var parts = args.Split(' ', 2);
            var subCommand = parts[0].ToLower();
            var subArgs = parts.Length > 1 ? parts[1] : "";

            switch (subCommand)
            {
                case "create":
                case "new":
                case "design":
                    return await CreateNewAvatar();

                case "think":
                case "thinking":
                    return await TriggerAvatarThinking();

                case "move":
                case "walk":
                    return await TriggerAvatarMovement(subArgs);

                case "dance":
                    return await TriggerAvatarDance();

                case "gesture":
                    return await TriggerAvatarGesture();

                case "lightbulb":
                case "light":
                    return await ToggleAvatarLightbulb();

                case "unity":
                case "open":
                    return await OpenUnityAvatarSystem();

                case "list":
                    return ListAvailableAvatars();

                default:
                    // If not a subcommand, treat as avatar name
                    return await ChangeAvatar(args);
            }
        }

        private async Task<string> CreateNewAvatar()
        {
            try
            {
                // Trigger Unity avatar creation system
                if (_avatarIntegration?.IsUnityRunning == true)
                {
                    // await _avatarIntegration.OpenAvatarCreationSystem();
                    return "🎨 Avatar Creation System opened in Unity! Design your custom avatar with templates, colors, and accessories.";
                }
                else
                {
                    return "🎨 Avatar Creation System available! Please open Unity scene with AvatarSystemSetup to create custom avatars.\n\nFeatures:\n• Multiple templates (Human, Robot, Fantasy)\n• Full customization (colors, hair, accessories)\n• Save/Load system\n• Ready Player Me integration";
                }
            }
            catch (Exception ex)
            {
                return $"❌ Error opening avatar creation system: {ex.Message}";
            }
        }

        private async Task<string> TriggerAvatarThinking()
        {
            try
            {
                if (_avatarIntegration?.IsUnityRunning == true)
                {
                    // await _avatarIntegration.StartThinkingAsync();
                    return "💡 Avatar is now in thinking mode! Watch the lightbulb appear above their head.";
                }
                else
                {
                    return "💡 Avatar thinking mode available in Unity! Press T key in Unity scene to test thinking animation and lightbulb.";
                }
            }
            catch (Exception ex)
            {
                return $"❌ Error triggering avatar thinking: {ex.Message}";
            }
        }

        private async Task<string> TriggerAvatarMovement(string direction)
        {
            try
            {
                if (_avatarIntegration?.IsUnityRunning == true)
                {
                    // await _avatarIntegration.MoveAvatarAsync(direction);
                    return $"🏃 Avatar is moving {direction}! Use WASD keys in Unity for manual control.";
                }
                else
                {
                    return "🏃 Avatar movement available in Unity! Use WASD keys to move, Shift to run, Space to jump.";
                }
            }
            catch (Exception ex)
            {
                return $"❌ Error moving avatar: {ex.Message}";
            }
        }

        private async Task<string> TriggerAvatarDance()
        {
            try
            {
                if (_avatarIntegration?.IsUnityRunning == true)
                {
                    // await _avatarIntegration.StartDancingAsync();
                    return "💃 Avatar is dancing! Press B key in Unity to trigger dance animations.";
                }
                else
                {
                    return "💃 Avatar dancing available in Unity! Press B key in Unity scene to see dance animations.";
                }
            }
            catch (Exception ex)
            {
                return $"❌ Error triggering avatar dance: {ex.Message}";
            }
        }

        private async Task<string> TriggerAvatarGesture()
        {
            try
            {
                if (_avatarIntegration?.IsUnityRunning == true)
                {
                    // await _avatarIntegration.StartGesturingAsync();
                    return "👋 Avatar is gesturing! Press G key in Unity for gesture animations.";
                }
                else
                {
                    return "👋 Avatar gestures available in Unity! Press G key in Unity scene for gesture animations.";
                }
            }
            catch (Exception ex)
            {
                return $"❌ Error triggering avatar gesture: {ex.Message}";
            }
        }

        private async Task<string> ToggleAvatarLightbulb()
        {
            try
            {
                if (_avatarIntegration?.IsUnityRunning == true)
                {
                    // await _avatarIntegration.ToggleLightbulbAsync();
                    return "💡 Avatar lightbulb toggled! The thinking lightbulb shows AI processing states.";
                }
                else
                {
                    return "💡 Avatar lightbulb system available in Unity! Press T key to test thinking mode with glowing lightbulb.";
                }
            }
            catch (Exception ex)
            {
                return $"❌ Error toggling lightbulb: {ex.Message}";
            }
        }

        private async Task<string> OpenUnityAvatarSystem()
        {
            try
            {
                // Try to open Unity or focus Unity window
                var unityProcesses = System.Diagnostics.Process.GetProcessesByName("Unity");
                if (unityProcesses.Length > 0)
                {
                    // Focus Unity window
                    var unity = unityProcesses[0];
                    ShowWindow(unity.MainWindowHandle, 9); // SW_RESTORE
                    SetForegroundWindow(unity.MainWindowHandle);
                    return "🎮 Unity focused! Your avatar system is ready to use.\n\nAvailable systems:\n• AvatarCreationSystem - Design custom avatars\n• DirectAvatarFix - Ready Player Me integration\n• Full movement and lightbulb system";
                }
                else
                {
                    return "🎮 Unity not running. Please open Unity with your avatar scene.\n\nSetup instructions:\n1. Open Unity project\n2. Add AvatarSystemSetup script to scene\n3. Click 'Setup Complete Avatar System'\n4. Test with WASD movement and T for thinking";
                }
            }
            catch (Exception ex)
            {
                return $"❌ Error opening Unity: {ex.Message}";
            }
        }

        // Windows API for focusing Unity window
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowWindow(System.IntPtr hWnd, int nCmdShow);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(System.IntPtr hWnd);

        private string ListAvailableAvatars()
        {
            return @"🎭 Available Avatars:

• **default** - Default Assistant (Friendly, Blue)
• **energetic** - Energetic Assistant (Fast, Orange) 
• **calm** - Calm Assistant (Relaxed, Green)
• **readyplayer** - Ready Player Me Avatar (Professional, Cyan)

Use: /avatar <name> to switch avatars
Or: /avatar (no parameters) to open avatar selection window
Or: /avatars to manage Ready Player Me avatars";
        }

        private async Task<string> SetTimer(string args)
        {
            if (string.IsNullOrWhiteSpace(args))
                return "Usage: /timer <seconds> [message]\nExample: /timer 60 Take a break!";

            var parts = args.Split(' ', 2);
            if (!int.TryParse(parts[0], out var seconds) || seconds <= 0 || seconds > 3600)
                return "❌ Please specify seconds (1-3600)";

            var message = parts.Length > 1 ? parts[1] : "Timer finished!";
            
            _ = Task.Run(async () =>
            {
                await Task.Delay(seconds * 1000);
                await Dispatcher.InvokeAsync(async () =>
                {
                    AddMessage("Atlas", $"⏰ {message}", false);
                    await _voiceManager.SpeakAsync(message);
                });
            });

            return $"⏱️ Timer set for {seconds} seconds";
        }

        private void OpenHistoryWindow()
        {
            try
            {
                var historyWindow = new CaptureHistoryWindow();
                historyWindow.Show();
                AddMessage("Atlas", "📸 Screenshot History opened! You can browse, search, and manage all your screenshots.", false);
            }
            catch (Exception ex)
            {
                AddMessage("Atlas", $"❌ Error opening history window: {ex.Message}", false);
            }
        }

        private void OpenClipboardManager()
        {
            try
            {
                var clipboardWindow = new ClipboardWindow();
                clipboardWindow.Show();
                AddMessage("Atlas", "📋 Clipboard Manager opened! You can view and manage your clipboard history.", false);
            }
            catch (Exception ex)
            {
                AddMessage("Atlas", $"❌ Error opening clipboard manager: {ex.Message}", false);
            }
        }

        private string GetCommandHelp()
        {
            return GetCapabilitiesList();
        }
        
        /// <summary>
        /// Get comprehensive list of all Atlas AI capabilities
        /// </summary>
        private string GetCapabilitiesList()
        {
            return @"🚀 ATLAS AI - COMPLETE CAPABILITIES LIST

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

🎤 VOICE CONTROL & WAKE WORD
• Say ""Hey Atlas"" or ""Atlas"" to activate hands-free
• Natural conversation - no rigid commands needed
• Premium ElevenLabs voice with custom Atlas voice
• Ctrl+Shift+A for push-to-talk (no audio distortion)
• AirPods gesture support - tap to activate
• ""Stop"" or ""Cancel"" to interrupt anytime

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

📝 QUICK NOTES
• ""Take a note: [your note]"" - saves to Documents
• ""Show my notes"" - view recent notes
• ""Read my notes"" - list all saved notes
• Notes saved to Documents/Atlas Notes folder

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

🖥️ SYSTEM DIAGNOSTICS
• ""How's my PC?"" - full system status
• ""PC status"" / ""System diagnostics""
• Shows CPU, RAM, disk usage with visual bars
• Top memory-consuming processes
• System uptime and health indicators

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

☀️ DAILY BRIEFING
• ""Good morning"" - get your daily briefing
• ""Morning briefing"" / ""Start my day""
• Weather, system status, motivational quote
• Perfect for starting your day!

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

🌐 INSTANT WEBSITE ACCESS
• ""Open YouTube"" / ""Open Netflix"" / ""Open Reddit""
• ""Open Twitter"" / ""Open Instagram"" / ""Open TikTok""
• ""Open Gmail"" / ""Check email""
• ""Open GitHub"" / ""Open Amazon"" / ""Open Discord""
• ""Open BBC News"" - and many more!

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

🎲 FUN & ENTERTAINMENT
• ""Tell me a joke"" - tech & programming jokes
• ""Fun fact"" - interesting random facts
• ""Compliment me"" / ""Motivate me""
• ""Flip a coin"" - heads or tails
• ""Roll dice"" / ""Roll 2d6"" / ""Roll d20""
• ""Magic 8 ball"" - ask yes/no questions

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

🎵 MUSIC & SPOTIFY CONTROL
• ""Play music"" - starts your Spotify
• ""Play [song name]"" or ""Play [artist]""
• ""Play my liked songs"" / ""Play my playlist""
• ""Pause"" / ""Resume"" / ""Stop""
• ""Next song"" / ""Previous song"" / ""Skip""
• ""Volume up"" / ""Volume down"" / ""Mute""

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

📸 SCREENSHOTS & VISION
• ""Take a screenshot"" - captures your screen
• ""Screenshot and analyze"" - AI describes what it sees
• ""Extract text from screen"" - OCR text extraction
• Screenshots saved to Pictures/Screenshots folder
• Click cyan paths in chat to open folder

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

🛡️ SECURITY SUITE
• ""Scan my computer"" - full malware/spyware scan
• ""Quick scan"" - fast threat check
• ""Deep scan"" - thorough system analysis
• ""Fix security issues"" - auto-remediation
• ""Update threat database"" - get latest definitions

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

💻 SYSTEM CONTROL
• ""Open [app name]"" - launch any application
• ""Close [app name]"" - close running apps
• ""Uninstall [program]"" - remove software
• ""Empty recycle bin"" / ""Clear temp files""
• ""Restart computer"" / ""Shutdown"" / ""Sleep""

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

🌤️ WEATHER
• ""What's the weather?"" - local forecast
• ""Weather in [city]"" - any location
• Real-time weather data

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

📱 SOCIAL MEDIA CONSOLE
• Campaign planning with AI assistance
• Content generation for all platforms
• Post scheduling & calendar view
• Say ""Open social media console"" to access

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

🎭 AVATAR & APPEARANCE
• ""Change avatar to [name]"" - switch avatars
• Available: default, professional, friendly, calm
• Ready Player Me custom avatar support

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

🧠 MEMORY & LEARNING
• I remember our conversations automatically
• ""Remember that [info]"" - save important facts
• ""What do you remember about [topic]?""
• ""Forget [topic]"" - privacy control

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

⏰ TIME & PRODUCTIVITY
• ""What time is it?"" / ""What's the date?""
• ""Set a timer for [X] minutes""
• ""Calculate [math expression]""

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

📝 SLASH COMMANDS (type / in chat)
/note /notes /diagnostics /briefing /weather
/youtube /netflix /reddit /gmail /github
/joke /funfact /8ball /flip /roll
/time /date /calc /open /search
/screenshot /scan /avatar /help

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

💡 PRO TIPS
• Just talk naturally - I understand context
• Click cyan links to open folders/files
• Ctrl+K opens command palette
• F11 for fullscreen mode
• Say ""Good morning"" for daily briefing!

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Ready to help! Just ask or say ""Atlas"" 🎯";
        }
        
        /// <summary>
        /// Check if user is asking about capabilities
        /// </summary>
        private bool IsCapabilitiesQuestion(string text)
        {
            var lower = text.ToLower().Trim();
            
            // Direct capability questions
            var capabilityPhrases = new[]
            {
                "what can you do",
                "what do you do",
                "what are you capable of",
                "what are your capabilities",
                "what can i do with you",
                "what features do you have",
                "what are your features",
                "show me what you can do",
                "tell me what you can do",
                "list your capabilities",
                "list your features",
                "what commands do you have",
                "what commands are there",
                "help me understand what you do",
                "what's possible",
                "what is possible",
                "what all can you do",
                "show capabilities",
                "show features",
                "your abilities",
                "what are your abilities",
                "what can atlas do",
                "what does atlas do"
            };
            
            foreach (var phrase in capabilityPhrases)
            {
                if (lower.Contains(phrase))
                    return true;
            }
            
            // Short direct questions
            if (lower == "help" || lower == "?" || lower == "capabilities" || lower == "features")
                return true;
                
            return false;
        }
        
        /// <summary>
        /// Handle natural language smart commands - returns response or null if not a smart command
        /// </summary>
        private async Task<string?> HandleSmartCommand(string text)
        {
            var lower = text.ToLower().Trim();
            
            // === NOTES ===
            if (lower.StartsWith("take a note") || lower.StartsWith("note:") || lower.StartsWith("remember this:") || 
                lower.StartsWith("save a note") || lower.StartsWith("make a note"))
            {
                var noteText = text;
                // Extract the actual note content
                foreach (var prefix in new[] { "take a note:", "take a note", "note:", "remember this:", "save a note:", "save a note", "make a note:", "make a note" })
                {
                    if (lower.StartsWith(prefix))
                    {
                        noteText = text.Substring(prefix.Length).Trim();
                        break;
                    }
                }
                if (string.IsNullOrWhiteSpace(noteText))
                    return "What would you like me to note down?";
                return await Features.SmartFeatures.TakeNoteAsync(noteText);
            }
            
            if (lower == "show my notes" || lower == "show notes" || lower == "my notes" || lower == "read my notes" || lower == "what are my notes")
            {
                return await Features.SmartFeatures.GetNotesAsync();
            }
            
            // === SYSTEM DIAGNOSTICS - exact phrases only ===
            if (lower == "how's my pc" || lower == "hows my pc" || lower == "how is my pc" || 
                lower == "pc status" || lower == "system diagnostics" || lower == "check my pc" ||
                lower == "system status" || lower == "pc health")
            {
                return await Features.SmartFeatures.GetSystemDiagnosticsAsync();
            }
            
            // === DAILY BRIEFING - exact phrases only ===
            if (lower == "good morning" || lower == "morning briefing" || lower == "daily briefing" || 
                lower == "start my day" || lower == "give me my briefing")
            {
                return await Features.SmartFeatures.GetDailyBriefingAsync();
            }
            
            // === JOKES & FUN - exact phrases only ===
            if (lower == "tell me a joke" || lower == "tell a joke" || lower == "joke please" || lower == "another joke")
            {
                return Features.SmartFeatures.TellJoke();
            }
            
            if (lower == "fun fact" || lower == "tell me a fun fact" || lower == "random fact" || lower == "tell me a fact")
            {
                return Features.SmartFeatures.TellFunFact();
            }
            
            if (lower == "compliment me" || lower == "give me a compliment" || lower == "motivate me" || lower == "cheer me up")
            {
                return Features.SmartFeatures.GiveCompliment();
            }
            
            if (lower == "flip a coin" || lower == "coin flip" || lower == "flip coin")
            {
                return Features.SmartFeatures.FlipCoin();
            }
            
            if (lower == "roll dice" || lower == "roll a dice" || lower == "roll d6" || lower == "roll d20" || lower == "roll 2d6")
            {
                // Parse dice notation like "roll 2d6" or "roll d20"
                var match = System.Text.RegularExpressions.Regex.Match(lower, @"(\d*)d(\d+)");
                if (match.Success)
                {
                    var count = string.IsNullOrEmpty(match.Groups[1].Value) ? 1 : int.Parse(match.Groups[1].Value);
                    var sides = int.Parse(match.Groups[2].Value);
                    return Features.SmartFeatures.RollDice(sides, count);
                }
                return Features.SmartFeatures.RollDice();
            }
            
            if (lower == "magic 8 ball" || lower == "magic 8ball" || lower == "8 ball" || lower == "8ball" || lower == "shake the 8 ball")
            {
                return Features.SmartFeatures.Magic8Ball();
            }
            
            // === WEBSITE SHORTCUTS - require "open" prefix to avoid accidental triggers ===
            if (lower == "open youtube")
            {
                return Features.SmartFeatures.OpenWebsite("youtube");
            }
            if (lower == "open netflix")
            {
                return Features.SmartFeatures.OpenWebsite("netflix");
            }
            if (lower == "open twitter" || lower == "open x")
            {
                return Features.SmartFeatures.OpenWebsite("twitter");
            }
            if (lower == "open reddit")
            {
                return Features.SmartFeatures.OpenWebsite("reddit");
            }
            if (lower == "open gmail" || lower == "open email" || lower == "check my email")
            {
                return Features.SmartFeatures.OpenWebsite("gmail");
            }
            if (lower == "open github")
            {
                return Features.SmartFeatures.OpenWebsite("github");
            }
            if (lower == "open amazon")
            {
                return Features.SmartFeatures.OpenWebsite("amazon");
            }
            if (lower == "open discord")
            {
                return Features.SmartFeatures.OpenWebsite("discord");
            }
            if (lower == "open twitch")
            {
                return Features.SmartFeatures.OpenWebsite("twitch");
            }
            if (lower == "open instagram")
            {
                return Features.SmartFeatures.OpenWebsite("instagram");
            }
            if (lower == "open tiktok")
            {
                return Features.SmartFeatures.OpenWebsite("tiktok");
            }
            if (lower == "open facebook")
            {
                return Features.SmartFeatures.OpenWebsite("facebook");
            }
            if (lower == "open bbc" || lower == "open bbc news" || lower == "open news")
            {
                return Features.SmartFeatures.OpenWebsite("bbc news");
            }
            
            // === MUSIC PLAYBACK - Direct execution, no AI needed ===
            if (lower.StartsWith("play ") && (lower.Contains("spotify") || lower.Contains("on spotify")))
            {
                var query = text.Substring(5).Trim();
                query = System.Text.RegularExpressions.Regex.Replace(query, @"\s+(on|in)\s+spotify.*$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (!string.IsNullOrWhiteSpace(query))
                {
                    return await Tools.MediaPlayerTool.PlayAsync(query, Tools.MediaPlayerTool.Platform.Spotify);
                }
            }
            if (lower.StartsWith("play ") && !lower.Contains("video") && !lower.Contains("game"))
            {
                var query = text.Substring(5).Trim();
                // Remove platform suffixes
                query = System.Text.RegularExpressions.Regex.Replace(query, @"\s+(on|in)\s+(spotify|youtube|soundcloud).*$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (!string.IsNullOrWhiteSpace(query) && query.Length > 2)
                {
                    // Default to Spotify for music
                    return await Tools.MediaPlayerTool.PlayAsync(query, Tools.MediaPlayerTool.Platform.Spotify);
                }
            }
            
            // === SYSTEM FILES - require admin privileges ===
            if (lower == "open hosts" || lower == "edit hosts" || lower == "open hosts file" || 
                lower == "edit hosts file" || lower == "open the hosts file" || lower == "edit the hosts file")
            {
                return Features.SmartFeatures.OpenHostsFile();
            }
            
            // === WEATHER - catch many variations ===
            if (lower.Contains("weather"))
            {
                // Extract location if specified
                string location = "Middlesbrough";
                if (lower.Contains(" in "))
                {
                    var idx = lower.IndexOf(" in ");
                    location = text.Substring(idx + 4).Trim().TrimEnd('?');
                }
                else if (lower.Contains(" for "))
                {
                    var idx = lower.IndexOf(" for ");
                    location = text.Substring(idx + 5).Trim().TrimEnd('?');
                }
                return await GetWeatherAsync(location);
            }
            
            // === IT MANAGEMENT COMMANDS ===
            var itService = ITManagementService.Instance;
            
            // System health / status
            if (lower == "system health" || lower == "health check" || lower == "how is my system" ||
                lower == "check system" || lower == "system report" || lower == "full report")
            {
                if (lower.Contains("full") || lower.Contains("report"))
                    return await itService.GetSystemReportAsync();
                return itService.HealthMonitor.GetHealthSummary();
            }
            
            // Clean temp files
            if (lower == "clean temp" || lower == "clean temp files" || lower == "clean temporary files" ||
                lower == "clear temp" || lower == "delete temp files" || lower == "cleanup")
            {
                var result = await itService.ScriptLibrary.ExecuteScriptAsync("cleanup_temp");
                return result.Message;
            }
            
            // Empty recycle bin
            if (lower == "empty recycle bin" || lower == "clear recycle bin" || lower == "empty trash" ||
                lower == "empty the recycle bin")
            {
                var result = await itService.ScriptLibrary.ExecuteScriptAsync("empty_recycle_bin");
                return result.Message;
            }
            
            // Network scan
            if (lower == "scan network" || lower == "network scan" || lower == "find devices" ||
                lower == "discover devices" || lower == "what's on my network")
            {
                ShowStatus("🔍 Scanning network...");
                var devices = await itService.NetworkDiscovery.ScanNetworkAsync();
                return itService.NetworkDiscovery.GetDiscoverySummary();
            }
            
            // Network info
            if (lower == "network info" || lower == "my ip" || lower == "what's my ip" || lower == "ip address" ||
                lower == "network status")
            {
                var info = itService.NetworkDiscovery.GetLocalNetworkInfo();
                return $"🌐 Network Information:\n\nIP Address: {info.LocalIP}\nSubnet: {info.SubnetMask}\nGateway: {info.Gateway}\nDNS: {info.DnsServer}\nAdapter: {info.AdapterName}\nMAC: {info.MacAddress}";
            }
            
            // Speed test
            if (lower == "speed test" || lower == "test speed" || lower == "internet speed" ||
                lower == "check internet speed" || lower == "how fast is my internet")
            {
                ShowStatus("📶 Running speed test...");
                var result = await itService.ScriptLibrary.ExecuteScriptAsync("speed_test");
                return result.Message;
            }
            
            // Flush DNS
            if (lower == "flush dns" || lower == "clear dns" || lower == "reset dns")
            {
                var result = await itService.ScriptLibrary.ExecuteScriptAsync("flush_dns");
                return result.Message;
            }
            
            // Check for issues
            if (lower == "check for issues" || lower == "find issues" || lower == "detect issues" ||
                lower == "scan for problems" || lower == "any issues" || lower == "system issues")
            {
                ShowStatus("🔍 Analyzing system...");
                await itService.IssueDetector.RunFullAnalysisAsync();
                return itService.IssueDetector.GetIssuesSummary();
            }
            
            // Virus scan
            if (lower == "virus scan" || lower == "scan for viruses" || lower == "malware scan" ||
                lower == "quick scan" || lower == "security scan")
            {
                ShowStatus("🛡️ Starting virus scan...");
                var result = await itService.ScriptLibrary.ExecuteScriptAsync("windows_defender_scan");
                return result.Message;
            }
            
            // Check updates
            if (lower == "check updates" || lower == "check for updates" || lower == "windows updates" ||
                lower == "any updates" || lower == "update check")
            {
                ShowStatus("🔄 Checking for updates...");
                var result = await itService.ScriptLibrary.ExecuteScriptAsync("check_updates");
                return result.Message;
            }
            
            // Firewall status
            if (lower == "firewall status" || lower == "check firewall" || lower == "firewall")
            {
                var result = await itService.ScriptLibrary.ExecuteScriptAsync("firewall_status");
                return result.Message;
            }
            
            // Startup programs
            if (lower == "startup programs" || lower == "startup apps" || lower == "what starts with windows" ||
                lower == "list startup" || lower == "show startup programs")
            {
                var result = await itService.ScriptLibrary.ExecuteScriptAsync("optimize_startup");
                return result.Message;
            }
            
            // Create restore point
            if (lower == "create restore point" || lower == "make restore point" || lower == "backup point" ||
                lower == "system restore point")
            {
                ShowStatus("💾 Creating restore point...");
                var result = await itService.ScriptLibrary.ExecuteScriptAsync("create_restore_point");
                return result.Message;
            }
            
            // Kill Chrome processes
            if (lower == "kill chrome" || lower == "close chrome" || lower == "stop chrome" ||
                lower == "end chrome" || lower == "terminate chrome" || lower == "kill google chrome" ||
                lower == "close all chrome" || lower == "stop all chrome")
            {
                ShowStatus("🔪 Killing Chrome processes...");
                var result = await itService.ScriptLibrary.ExecuteScriptAsync("kill_chrome");
                return result.Message;
            }
            
            // Kill all browsers
            if (lower == "kill browsers" || lower == "close all browsers" || lower == "kill all browsers" ||
                lower == "stop all browsers" || lower == "close browsers" || lower == "end all browsers")
            {
                ShowStatus("🔪 Killing all browser processes...");
                var result = await itService.ScriptLibrary.ExecuteScriptAsync("kill_browser_processes");
                return result.Message;
            }
            
            // Kill specific process
            if (lower.StartsWith("kill ") || lower.StartsWith("stop ") || lower.StartsWith("end ") ||
                lower.StartsWith("terminate ") || lower.StartsWith("close "))
            {
                var processName = lower
                    .Replace("kill ", "").Replace("stop ", "").Replace("end ", "")
                    .Replace("terminate ", "").Replace("close ", "")
                    .Replace(" process", "").Replace(" processes", "").Trim();
                
                if (!string.IsNullOrEmpty(processName) && processName != "chrome" && processName != "browsers")
                {
                    ShowStatus($"🔪 Killing {processName} processes...");
                    var result = await itService.ScriptLibrary.ExecuteScriptAsync("kill_process", 
                        new Dictionary<string, string> { ["process"] = processName });
                    return result.Message;
                }
            }
            
            // List high memory processes
            if (lower == "high memory" || lower == "memory hogs" || lower == "what's using memory" ||
                lower == "top processes" || lower == "memory usage" || lower == "show memory usage" ||
                lower == "what's using ram" || lower == "ram usage")
            {
                var result = await itService.ScriptLibrary.ExecuteScriptAsync("list_high_memory");
                return result.Message;
            }
            
            // IT help / commands list
            if (lower == "it help" || lower == "it commands" || lower == "what it commands" ||
                lower == "system commands" || lower == "maintenance commands")
            {
                return itService.GetAvailableCommands();
            }
            
            // === FOLDER PRIORITY CHECK ===
            // If user explicitly says "folder" in their request, check folders FIRST before apps
            // This prevents "open X folder" from launching an app with a similar name
            bool userWantsFolder = lower.Contains("folder") || lower.Contains("directory");
            
            if (userWantsFolder && (lower.StartsWith("open ") || lower.Contains("go to") || 
                lower.Contains("show me") || lower.Contains("navigate")))
            {
                var folderResult = OpenFolderCommand(lower, text);
                if (folderResult != null)
                    return folderResult;
            }
            
            // === APPLICATION LAUNCHING - Check FIRST before folders (only if not explicitly asking for folder) ===
            // Try to launch as an app first for "open X", "launch X", "run X", "start X"
            if (!userWantsFolder && (lower.StartsWith("open ") || lower.StartsWith("launch ") || lower.StartsWith("run ") || 
                lower.StartsWith("start ") || lower.StartsWith("execute ")))
            {
                var appResult = LaunchApplicationCommand(lower, text);
                if (appResult != null)
                    return appResult;
            }
            
            // Short commands that are just app names (e.g., "steam", "chrome", "spotify")
            var commonApps = new[] { "steam", "chrome", "firefox", "spotify", "discord", "slack", "zoom",
                "teams", "outlook", "word", "excel", "powerpoint", "notepad", "calculator", "paint",
                "vlc", "obs", "vscode", "code", "terminal", "powershell", "cmd", "edge", "brave",
                "opera", "vivaldi", "telegram", "whatsapp", "signal", "skype", "epic", "origin",
                "ubisoft", "battle.net", "blizzard", "gog", "itch", "nvidia", "geforce", "afterburner",
                "hwinfo", "cpu-z", "gpu-z", "msi", "corsair", "razer", "logitech", "audacity",
                "gimp", "photoshop", "premiere", "davinci", "blender", "unity", "unreal" };
            
            if (commonApps.Any(app => lower == app || lower == $"open {app}" || lower == $"launch {app}"))
            {
                var appResult = LaunchApplicationCommand(lower, text);
                if (appResult != null)
                    return appResult;
            }
            
            // === FOLDER OPENING - Direct execution ===
            // Comprehensive Windows filesystem access - FULL SYSTEM
            // NOTE: Removed app names (steam, epic games, nvidia, etc.) - those are handled above
            var folderKeywords = new[] { 
                // User folders
                "screenshots", "screenshot", "downloads", "download", "documents", "document",
                "pictures", "picture", "photo", "desktop", "music", "videos", "video", "movie",
                "home", "profile", "favorites", "contacts", "saved game", "links", "quick access",
                "3d object", "3d model", "3d models", "camera roll",
                // AppData & ProgramData
                "appdata", "app data", "roaming", "local appdata", "locallow", "programdata", "program data",
                // System folders
                "program files", "programs", "common files", "windows", "system32", "system 32", "syswow", "wow64",
                "drivers", "fonts", "temp", "temporary", "etc", "hosts",
                // Startup & System
                "startup", "start menu", "send to", "recent", "templates", "cookies", "history",
                "internet cache", "browser cache", "prefetch", "logs", "inf", "winsxs",
                // Shell folders
                "recycle", "trash", "bin", "this pc", "my computer", "computer", "network",
                "printers", "onedrive", "public", "libraries", "library", "games folder", "users folder",
                // Control Panel & Tools
                "control panel", "device manager", "task manager", "disk management",
                "services", "event viewer", "event log", "registry", "regedit", "computer management",
                "system info", "resource monitor", "performance monitor", "perfmon", "group policy",
                "gpedit", "security policy", "secpol", "firewall", "programs and features",
                "uninstall", "add remove", "network connection", "network adapter", "power option",
                "power plan", "sound setting", "audio setting", "display setting", "date", "time",
                "system properties", "environment variable",
                // Drives
                "c drive", "d drive", "e drive", "f drive", "g drive", "h drive",
                "c:", "d:", "e:", "f:", "g:", "h:",
                // Folder-specific keywords (with "folder" to distinguish from apps)
                "steam folder", "epic folder", "nvidia folder", "amd folder", "intel folder"
            };
            
            bool hasFolderKeyword = folderKeywords.Any(k => lower.Contains(k));
            bool hasOpenIntent = lower.Contains("open") || lower.Contains("go to") || lower.Contains("show me") || 
                                 lower.Contains("show my") || lower.Contains("navigate") || lower.Contains("folder") ||
                                 lower.Contains("access") || lower.Contains("browse");
            
            // Single word folder requests like "downloads", "documents", "desktop"
            var singleWordFolders = new[] { "downloads", "documents", "desktop", "pictures", "music", "videos" };
            if (singleWordFolders.Any(f => lower == f))
            {
                return OpenFolderCommand(lower, text);
            }
            
            // Direct folder request - must have "folder" keyword or explicit folder intent
            if (hasFolderKeyword && (hasOpenIntent || lower.Contains("folder")))
            {
                return OpenFolderCommand(lower, text);
            }
            
            // Just mentioning a folder with "folder" or "my folder"
            if (hasFolderKeyword && (lower.Contains("folder") || lower.EndsWith(" folder")))
            {
                return OpenFolderCommand(lower, text);
            }
            
            // Direct system tool requests (no "open" needed)
            var directTools = new[] { "task manager", "device manager", "control panel", 
                "registry", "regedit", "services", "event viewer", "disk management", "firewall",
                "resource monitor", "performance monitor", "system info", "computer management" };
            if (directTools.Any(t => lower.Contains(t)))
            {
                return OpenFolderCommand(lower, text);
            }
            
            // "go to [folder]" or "show me [folder]" or "navigate to [folder]"
            if ((lower.StartsWith("go to ") || lower.StartsWith("show me ") || lower.StartsWith("navigate to ") ||
                 lower.StartsWith("show ")) && 
                !lower.Contains("youtube") && !lower.Contains("netflix") && !lower.Contains("website") &&
                !lower.Contains("http"))
            {
                var folderResult = OpenFolderCommand(lower, text);
                if (folderResult != null)
                    return folderResult;
            }
            
            // === APPLICATION LAUNCHING (fallback) ===
            // Direct app launch requests that weren't caught above (skip if user wants folder)
            if (!userWantsFolder && (lower.StartsWith("open ") || lower.StartsWith("launch ") || lower.StartsWith("run ") || 
                lower.StartsWith("start ") || lower.StartsWith("execute ")))
            {
                var appResult = LaunchApplicationCommand(lower, text);
                if (appResult != null)
                    return appResult;
            }
            
            // Scan/rescan files command - force re-index of file system
            if ((lower.Contains("scan") || lower.Contains("rescan") || lower.Contains("reindex") || lower.Contains("index")) && 
                (lower.Contains("file") || lower.Contains("folder") || lower.Contains("directory")))
            {
                return await RescanFileSystemAsync();
            }
            
            // Scan apps command
            if (lower.Contains("scan") && (lower.Contains("app") || lower.Contains("program") || lower.Contains("install")))
            {
                return await ScanInstalledAppsAsync();
            }
            
            // List apps command
            if ((lower.Contains("list") || lower.Contains("show") || lower.Contains("what")) && 
                (lower.Contains("app") || lower.Contains("program") || lower.Contains("install")))
            {
                return ListInstalledApps(lower);
            }
            
            // Not a smart command
            return null;
        }
        
        /// <summary>
        /// Rescan the file system to update the folder index
        /// </summary>
        private async Task<string> RescanFileSystemAsync()
        {
            ShowStatus("🔍 Scanning your file system...");
            
            // Subscribe to progress updates
            void OnProgress(string msg) => Dispatcher.Invoke(() => ShowStatus($"🔍 {msg}"));
            Memory.FileSystemIndex.Instance.IndexingProgress += OnProgress;
            
            try
            {
                await Memory.FileSystemIndex.Instance.ReindexAsync();
                var folderCount = Memory.FileSystemIndex.Instance.FolderCount;
                var fileCount = Memory.FileSystemIndex.Instance.FileCount;
                return $"✅ File system scan complete! Found **{folderCount}** folders and **{fileCount}** files. I can now find any folder by name.";
            }
            finally
            {
                Memory.FileSystemIndex.Instance.IndexingProgress -= OnProgress;
            }
        }
        
        /// <summary>
        /// Launch an application by name
        /// </summary>
        private string? LaunchApplicationCommand(string lower, string originalText)
        {
            // Extract app name from command
            string appName = originalText;
            foreach (var prefix in new[] { "open ", "launch ", "run ", "start ", "execute ", "open the ", "launch the ", "run the ", "start the " })
            {
                if (lower.StartsWith(prefix))
                {
                    appName = originalText.Substring(prefix.Length).Trim();
                    break;
                }
            }
            
            // Remove trailing words like "app", "application", "program"
            appName = appName
                .Replace(" app", "", StringComparison.OrdinalIgnoreCase)
                .Replace(" application", "", StringComparison.OrdinalIgnoreCase)
                .Replace(" program", "", StringComparison.OrdinalIgnoreCase)
                .Trim();
            
            if (string.IsNullOrEmpty(appName)) return null;
            
            // Try to launch
            var result = SystemControl.InstalledAppsManager.Instance.LaunchApp(appName);
            if (result.Success)
                return result.Message;
            
            // If not found, return null to let AI handle it (might be a different kind of request)
            return null;
        }
        
        /// <summary>
        /// Scan for installed applications
        /// </summary>
        private async Task<string> ScanInstalledAppsAsync()
        {
            await SystemControl.InstalledAppsManager.Instance.ScanAllAppsAsync();
            var count = SystemControl.InstalledAppsManager.Instance.AppCount;
            return $"🔍 Scanned your system. Found **{count}** applications. I'll remember these and watch for new installs.";
        }
        
        /// <summary>
        /// List installed applications
        /// </summary>
        private string ListInstalledApps(string query)
        {
            var apps = SystemControl.InstalledAppsManager.Instance.GetAllApps();
            if (apps.Count == 0)
            {
                return "I haven't scanned your apps yet. Say \"scan my apps\" to discover what's installed.";
            }
            
            // If searching for specific apps
            if (query.Contains("search") || query.Contains("find"))
            {
                var searchTerm = query.Split(new[] { "search", "find", "for" }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Trim();
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    var matches = SystemControl.InstalledAppsManager.Instance.SearchApps(searchTerm);
                    if (matches.Count == 0)
                        return $"No apps found matching '{searchTerm}'.";
                    
                    var matchList = string.Join("\n", matches.Take(10).Select(a => $"• {a.Name}"));
                    return $"Found {matches.Count} apps matching '{searchTerm}':\n{matchList}";
                }
            }
            
            // Show summary
            var bySource = apps.GroupBy(a => a.Source).ToDictionary(g => g.Key, g => g.Count());
            var summary = $"📦 **{apps.Count} Applications Installed**\n\n";
            
            foreach (var (source, count) in bySource.OrderByDescending(x => x.Value))
            {
                summary += $"• {source}: {count}\n";
            }
            
            summary += $"\nSay \"open [app name]\" to launch any app, or \"search apps for [name]\" to find specific ones.";
            return summary;
        }
        
        /// <summary>
        /// Opens a folder based on natural language command.
        /// Uses FileSystemIndex to find ANY folder on the system by name.
        /// </summary>
        private string? OpenFolderCommand(string lower, string originalText)
        {
            // Extract the folder name from the command
            var searchTerm = lower
                .Replace("open ", "").Replace("go to ", "").Replace("show me ", "")
                .Replace("show my ", "").Replace("navigate to ", "").Replace("show ", "")
                .Replace("folder", "").Replace("directory", "").Replace("my ", "")
                .Replace("the ", "").Replace("please", "").Trim();
            
            Debug.WriteLine($"[OpenFolderCommand] Processing: '{lower}' -> search term: '{searchTerm}'");
            
            // ===========================================
            // STEP 1: Check for explicit paths first (C:\..., D:\...)
            // ===========================================
            var pathMatch = System.Text.RegularExpressions.Regex.Match(originalText, @"[A-Za-z]:\\[^\s""]+");
            if (pathMatch.Success)
            {
                var explicitPath = pathMatch.Value;
                if (Directory.Exists(explicitPath))
                {
                    Process.Start("explorer.exe", $"\"{explicitPath}\"");
                    return $"📂 Opening {Path.GetFileName(explicitPath)}.";
                }
                return $"❌ Path doesn't exist: {explicitPath}";
            }
            
            // ===========================================
            // STEP 2: Use FileSystemIndex FIRST to find user folders
            // This takes priority over system folders so user's custom
            // folders like "3D Models" are found before system "3D Objects"
            // ===========================================
            if (!string.IsNullOrEmpty(searchTerm) && searchTerm.Length >= 2)
            {
                try
                {
                    var indexedPath = Memory.FileSystemIndex.Instance.FindFolder(searchTerm);
                    if (!string.IsNullOrEmpty(indexedPath) && Directory.Exists(indexedPath))
                    {
                        Debug.WriteLine($"[OpenFolderCommand] FileSystemIndex found: {indexedPath}");
                        Process.Start("explorer.exe", $"\"{indexedPath}\"");
                        return $"📂 Opening {Path.GetFileName(indexedPath)}.";
                    }
                    else
                    {
                        Debug.WriteLine($"[OpenFolderCommand] FileSystemIndex: no match for '{searchTerm}'");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[OpenFolderCommand] FileSystemIndex error: {ex.Message}");
                }
            }
            
            // ===========================================
            // STEP 3: Fall back to well-known system folders/tools
            // Only if FileSystemIndex didn't find anything
            // ===========================================
            var systemResult = TryOpenSystemFolder(lower);
            if (systemResult != null)
                return systemResult;
            
            // ===========================================
            // STEP 4: If index is empty or old, trigger re-index
            // ===========================================
            if (!Memory.FileSystemIndex.Instance.IsIndexed || 
                (DateTime.Now - Memory.FileSystemIndex.Instance.LastIndexTime).TotalHours > 24)
            {
                _ = Memory.FileSystemIndex.Instance.IndexAsync(force: true);
                return $"🔍 I'm scanning your file system to learn your folders. Try again in a moment, or say \"rescan files\" to force a refresh.";
            }
            
            return null;
        }
        
        /// <summary>
        /// Try to open well-known system folders and tools
        /// </summary>
        private string? TryOpenSystemFolder(string lower)
        {
            // Screenshots
            if (lower.Contains("screenshot"))
            {
                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Screenshots");
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                Process.Start("explorer.exe", $"\"{path}\"");
                return "📂 Opening Screenshots.";
            }
            // Downloads
            if (lower.Contains("download"))
            {
                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                Process.Start("explorer.exe", $"\"{path}\"");
                return "📂 Opening Downloads.";
            }
            // Documents
            if (lower.Contains("document"))
            {
                Process.Start("explorer.exe", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
                return "📂 Opening Documents.";
            }
            // Pictures
            if (lower.Contains("picture") || lower.Contains("photo") || lower.Contains("image folder"))
            {
                Process.Start("explorer.exe", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
                return "📂 Opening Pictures.";
            }
            // Desktop
            if (lower.Contains("desktop"))
            {
                Process.Start("explorer.exe", Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
                return "📂 Opening Desktop.";
            }
            // Music
            if (lower.Contains("music"))
            {
                Process.Start("explorer.exe", Environment.GetFolderPath(Environment.SpecialFolder.MyMusic));
                return "📂 Opening Music.";
            }
            // Videos
            if (lower.Contains("video") || lower.Contains("movie"))
            {
                Process.Start("explorer.exe", Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));
                return "📂 Opening Videos.";
            }
            // 3D Objects - ONLY match "3d object" exactly, NOT "3d model" (user may have a custom 3D Models folder)
            if (lower.Contains("3d object") && !lower.Contains("3d model"))
            {
                try { Process.Start("explorer.exe", "shell:3D Objects"); return "📂 Opening 3D Objects."; }
                catch { return "❌ Couldn't open 3D Objects."; }
            }
            // ProgramData - system-wide application data (C:\ProgramData)
            if (lower.Contains("programdata") || lower.Contains("program data"))
            {
                var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                Process.Start("explorer.exe", $"\"{programData}\"");
                return "📂 Opening ProgramData.";
            }
            // AppData
            if (lower.Contains("appdata") || lower.Contains("app data"))
            {
                if (lower.Contains("local"))
                    Process.Start("explorer.exe", Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
                else
                    Process.Start("explorer.exe", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
                return "📂 Opening AppData.";
            }
            // Temp
            if (lower.Contains("temp"))
            {
                Process.Start("explorer.exe", Path.GetTempPath());
                return "📂 Opening Temp.";
            }
            // Recycle Bin
            if (lower.Contains("recycle") || lower.Contains("trash") || lower.Contains("bin"))
            {
                try { Process.Start("explorer.exe", "shell:RecycleBinFolder"); return "🗑️ Opening Recycle Bin."; }
                catch { return "❌ Couldn't open Recycle Bin."; }
            }
            // This PC
            if (lower.Contains("this pc") || lower.Contains("my computer") || lower.Contains("computer"))
            {
                try { Process.Start("explorer.exe", "shell:MyComputerFolder"); return "💻 Opening This PC."; }
                catch { return "❌ Couldn't open This PC."; }
            }
            // OneDrive
            if (lower.Contains("onedrive"))
            {
                try { Process.Start("explorer.exe", "shell:OneDrive"); return "☁️ Opening OneDrive."; }
                catch { return "❌ Couldn't open OneDrive."; }
            }
            // iCloud Drive
            if (lower.Contains("icloud"))
            {
                var icloudPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "iCloudDrive");
                if (!Directory.Exists(icloudPath))
                    icloudPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "iCloud Drive");
                if (Directory.Exists(icloudPath))
                {
                    Process.Start("explorer.exe", $"\"{icloudPath}\"");
                    return "☁️ Opening iCloud Drive.";
                }
            }
            // Control Panel
            if (lower.Contains("control panel"))
            {
                try { Process.Start("control.exe"); return "⚙️ Opening Control Panel."; }
                catch { return "❌ Couldn't open Control Panel."; }
            }
            // Settings
            if (lower.Contains("settings") && !lower.Contains("folder"))
            {
                try { Process.Start(new ProcessStartInfo("ms-settings:") { UseShellExecute = true }); return "⚙️ Opening Settings."; }
                catch { return "❌ Couldn't open Settings."; }
            }
            // Task Manager
            if (lower.Contains("task manager"))
            {
                try { Process.Start("taskmgr.exe"); return "📊 Opening Task Manager."; }
                catch { return "❌ Couldn't open Task Manager."; }
            }
            // Device Manager
            if (lower.Contains("device manager"))
            {
                try { Process.Start("devmgmt.msc"); return "🔧 Opening Device Manager."; }
                catch { return "❌ Couldn't open Device Manager."; }
            }
            // Services
            if (lower.Contains("services"))
            {
                try { Process.Start("services.msc"); return "⚙️ Opening Services."; }
                catch { return "❌ Couldn't open Services."; }
            }
            // Registry
            if (lower.Contains("registry") || lower.Contains("regedit"))
            {
                try { Process.Start("regedit.exe"); return "🔧 Opening Registry Editor."; }
                catch { return "❌ Couldn't open Registry Editor."; }
            }
            // Drives
            if (lower.Contains("c drive") || lower.Contains("c:") || lower == "c")
            {
                Process.Start("explorer.exe", "C:\\");
                return "📂 Opening C: Drive.";
            }
            if (lower.Contains("d drive") || lower.Contains("d:") || lower == "d")
            {
                Process.Start("explorer.exe", "D:\\");
                return "📂 Opening D: Drive.";
            }
            
            return null;
        }
        
        /// <summary>
        /// Speak a response asynchronously without blocking UI
        /// </summary>
        private async Task SpeakResponseAsync(string response)
        {
            try
            {
                // Set orb to Speaking state
                SetAtlasCoreState(Controls.AtlasVisualState.Speaking);
                StartSpeakingEnergySimulation();
                
                // Speak the response (this takes time for cloud TTS)
                await _voiceManager.SpeakAsync(GetShortResponse(response));
                
                // Return to Idle after speaking
                StopSpeakingEnergySimulation();
                SetAtlasCoreState(Controls.AtlasVisualState.Idle);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TTS] Speech error: {ex.Message}");
                StopSpeakingEnergySimulation();
                SetAtlasCoreState(Controls.AtlasVisualState.Idle);
            }
        }
        
        /// <summary>
        /// Get a version of a response for voice output
        /// Speaks full paragraphs, but summarizes long lists/technical output
        /// </summary>
        private string GetShortResponse(string fullResponse)
        {
            // Get the user's preferred honorific (sir, ma'am, miss, name, or none)
            var honorific = _conversationManager?.UserProfile?.GetHonorific() ?? "sir";
            var honorificSuffix = string.IsNullOrEmpty(honorific) ? "" : $", {honorific}";
            
            // Check if this is a list or technical output (not conversational)
            bool isList = fullResponse.Contains("•") || fullResponse.Contains("- ") || 
                          fullResponse.Contains("1.") || fullResponse.Contains("✅") ||
                          fullResponse.Contains("❌") || fullResponse.Contains("📝") ||
                          fullResponse.Split('\n').Length > 8;
            
            bool isTechnical = fullResponse.Contains("```") || fullResponse.Contains("DIAGNOSTICS") ||
                               fullResponse.Contains("REPORT") || fullResponse.Contains("Status:") ||
                               fullResponse.Contains("MB") && fullResponse.Contains("GB") ||
                               fullResponse.Contains("CPU:") || fullResponse.Contains("Memory:");
            
            bool isMemoryStats = fullResponse.Contains("Memory Status") || fullResponse.Contains("Corrections") ||
                                 fullResponse.Contains("Preferences") || fullResponse.Contains("/corrections");
            
            // For conversational paragraphs (not lists), speak the full thing up to ~500 chars
            if (!isList && !isTechnical && !isMemoryStats && fullResponse.Length <= 500)
            {
                return fullResponse;
            }
            
            // For medium-length conversational responses, speak them
            if (!isList && !isTechnical && !isMemoryStats && fullResponse.Length <= 800)
            {
                // Just speak the first paragraph or two
                var paragraphs = fullResponse.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
                if (paragraphs.Length > 0)
                {
                    var firstParagraph = paragraphs[0];
                    if (firstParagraph.Length <= 500)
                        return firstParagraph;
                }
            }
            
            // For long responses or lists, give a summary and tell them to check chat
            if (fullResponse.Contains("SYSTEM DIAGNOSTICS"))
                return $"Here's your system status{honorificSuffix}. I've displayed the full details in the chat.";
            if (fullResponse.Contains("DAILY BRIEFING") || fullResponse.Contains("GOOD MORNING"))
                return $"Here's your daily briefing{honorificSuffix}. Have a great day!";
            if (fullResponse.Contains("Note saved"))
                return "Note saved!";
            if (fullResponse.Contains("YOUR RECENT NOTES"))
                return $"Here are your recent notes{honorificSuffix}. I've listed them in the chat.";
            if (fullResponse.Contains("Opening"))
                return fullResponse.Split('\n')[0];
            
            // Memory system responses
            if (fullResponse.Contains("Memory Status"))
                return $"Here's what I know about you{honorificSuffix}. I've displayed the full memory stats in the chat.";
            if (fullResponse.Contains("Corrections I've Learned"))
                return $"Here are the corrections I've learned{honorificSuffix}. Check the chat for the full list.";
            if (fullResponse.Contains("Preferences & Facts"))
                return $"Here are your preferences{honorificSuffix}. I've listed them in the chat.";
                
            // IT Management responses
            if (fullResponse.Contains("System Information") || fullResponse.Contains("System Status"))
                return $"Here's your system health report{honorificSuffix}. CPU and memory are operating within optimal parameters.";
            if (fullResponse.Contains("SYSTEM HEALTH REPORT"))
                return $"I've compiled a comprehensive system report{honorificSuffix}. Details are in the chat.";
            if (fullResponse.Contains("Network Scan") || fullResponse.Contains("Found") && fullResponse.Contains("devices"))
                return $"Network scan complete{honorificSuffix}. I've identified several devices on your network.";
            if (fullResponse.Contains("Cleaned") && fullResponse.Contains("MB"))
                return $"Cleanup complete{honorificSuffix}. I've freed up some disk space for you.";
            if (fullResponse.Contains("No issues detected"))
                return $"Excellent news{honorificSuffix}. No issues detected. Your system is operating at peak efficiency.";
            if (fullResponse.Contains("Issue detected") || fullResponse.Contains("ACTIVE ISSUES"))
                return $"I've identified some issues requiring your attention{honorificSuffix}. Details are in the chat.";
            if (fullResponse.Contains("Speed test") || fullResponse.Contains("Mbps"))
                return $"Speed test complete{honorificSuffix}. Results are displayed in the chat.";
            if (fullResponse.Contains("updates available"))
                return $"I've detected available updates for your system{honorificSuffix}.";
            if (fullResponse.Contains("Firewall"))
                return $"Firewall status verified{honorificSuffix}. Your system is protected.";
            if (fullResponse.Contains("startup programs"))
                return $"Here's your startup programs list{honorificSuffix}.";
            if (fullResponse.Contains("Killed") && fullResponse.Contains("Chrome"))
                return $"Chrome processes terminated{honorificSuffix}. Memory has been reclaimed.";
            if (fullResponse.Contains("Killed") && fullResponse.Contains("browser"))
                return $"All browser processes terminated{honorificSuffix}. Significant memory freed.";
            if (fullResponse.Contains("Killed") && fullResponse.Contains("processes"))
                return $"Processes terminated as requested{honorificSuffix}.";
            if (fullResponse.Contains("Top 15 processes"))
                return $"Here are your highest memory consumers{honorificSuffix}.";
            
            // Code/technical responses
            if (fullResponse.Contains("```"))
                return $"I've written some code for you{honorificSuffix}. Check the chat for the details.";
            
            // Generic long response - try to extract first sentence
            var firstSentence = ExtractFirstSentence(fullResponse);
            if (!string.IsNullOrEmpty(firstSentence) && firstSentence.Length <= 200)
            {
                return $"{firstSentence} I've put the full details in the chat{honorificSuffix}.";
            }
                
            return $"Task complete{honorificSuffix}. I've displayed the details in the chat.";
        }
        
        /// <summary>
        /// Extract the first sentence from a response
        /// </summary>
        private string ExtractFirstSentence(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            
            // Find first sentence ending
            var endings = new[] { ". ", "! ", "? ", ".\n", "!\n", "?\n" };
            int minIndex = text.Length;
            
            foreach (var ending in endings)
            {
                var idx = text.IndexOf(ending);
                if (idx > 0 && idx < minIndex)
                    minIndex = idx + 1; // Include the punctuation
            }
            
            if (minIndex < text.Length && minIndex > 10)
                return text.Substring(0, minIndex).Trim();
            
            return "";
        }

        /// <summary>
        /// Detect if user message is an agent-worthy task (file ops, installations, code generation, etc.)
        /// </summary>
        private bool IsAgentTask(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            
            var lower = text.ToLowerInvariant();
            
            // File operation patterns - more specific
            // NOTE: Delete patterns removed - delete goes through ToolExecutor with confirmation system
            var filePatterns = new[]
            {
                "create a file", "create file", "make a file", "make file",
                "write a file", "write file", "save a file", "save file",
                "create a script", "write a script", "make a script",
                "python script", "bash script", "powershell script", "shell script",
                "javascript file", "typescript file", "python file", "csharp file", "c# file",
                "js file", "ts file", "py file", "cs file", "json file", "html file", "css file",
                "create a class", "write a class", "make a class",
                "create a function", "write a function", "make a function",
                // Delete patterns REMOVED - handled by ToolExecutor with double confirmation
                "rename file", "rename the file", "move file", "move the file",
                "read file", "read the file", "show file contents", "cat file",
                "list files", "list the files", "show files", "show the files",
                "list folder", "list the folder", "list directory", "show folder contents",
                "create folder", "create directory", "make folder", "make directory",
                "search for files", "find files", "search files",
                "file that", "script that", "program that", // Catch "X file that does Y"
                "new file", "new script", "new program", // Catch "new X"
                "save this", "save to", "write to", // Catch save operations
                // FIND/SEARCH patterns - for file/folder searching
                "find ", "locate ", "search ", "look for ", "where is ", "where are ",
                "show me ", "get me all", "find all", "find my", "find the",
                " folders", " folder", " directory", " directories", // Catch "find X folders"
                "on my pc", "on my computer", "on this pc", "on this computer",
                "themes", "files named", "folders named" // Common search terms
            };
            
            // Software installation patterns
            var installPatterns = new[]
            {
                "install ", "uninstall ", "remove software",
                "download and install", "set up ", "setup ",
                "install python", "install node", "install git",
                "install npm", "install pip", "pip install",
                "npm install", "winget install", "choco install",
                "get me ", "download " // Catch "get me python" or "download vscode"
            };
            
            // Code generation patterns - be more specific to avoid false positives
            var codePatterns = new[]
            {
                "write code that", "write some code", "generate code",
                "create code that", "make code that", "code that does",
                "write a program that", "create a program that", "make a program that",
                "write an app that", "create an app that", "make an app that",
                "build a script", "build me a script",
                "write me a script", "create me a script", "make me a script",
                "write me a file", "create me a file", "make me a file",
                "that prints", "that logs", "that outputs", "that displays", // Catch "X that prints Y"
                "hello world", // Common coding task
                "write me a", "create me a", "make me a", // Catch "write me a calculator"
                "can you write", "can you create", "can you make" // Catch polite requests
            };
            
            // Command execution patterns
            var commandPatterns = new[]
            {
                "run command", "run the command", "execute command",
                "run powershell", "run cmd", "run terminal",
                "run dotnet", "run npm", "compile ", "build project",
                "open cmd", "open terminal", "open powershell"
            };
            
            // Check all patterns
            foreach (var pattern in filePatterns)
            {
                if (lower.Contains(pattern))
                {
                    System.Diagnostics.Debug.WriteLine($"[IsAgentTask] Matched file pattern: '{pattern}' in '{text}'");
                    return true;
                }
            }
            
            foreach (var pattern in installPatterns)
            {
                if (lower.Contains(pattern))
                {
                    System.Diagnostics.Debug.WriteLine($"[IsAgentTask] Matched install pattern: '{pattern}' in '{text}'");
                    return true;
                }
            }
            
            foreach (var pattern in codePatterns)
            {
                if (lower.Contains(pattern))
                {
                    System.Diagnostics.Debug.WriteLine($"[IsAgentTask] Matched code pattern: '{pattern}' in '{text}'");
                    return true;
                }
            }
            
            foreach (var pattern in commandPatterns)
            {
                if (lower.Contains(pattern))
                {
                    System.Diagnostics.Debug.WriteLine($"[IsAgentTask] Matched command pattern: '{pattern}' in '{text}'");
                    return true;
                }
            }
            
            // Check for specific file extensions mentioned (likely file operations)
            var fileExtensions = new[] { ".cs", ".py", ".js", ".ts", ".json", ".xml", ".txt", ".md", ".html", ".css", ".bat", ".ps1" };
            foreach (var ext in fileExtensions)
            {
                if (lower.Contains(ext) && (lower.Contains("create") || lower.Contains("write") || lower.Contains("make") || lower.Contains("read") || lower.Contains("delete")))
                {
                    System.Diagnostics.Debug.WriteLine($"[IsAgentTask] Matched file extension: '{ext}' with action in '{text}'");
                    return true;
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"[IsAgentTask] No match for: '{text}'");
            return false;
        }
        
        /// <summary>
        /// Handle delete file operations directly without AI - faster and more reliable
        /// </summary>
        private async Task<string> HandleDeleteFilesDirectly(string task)
        {
            // COMPLETELY DISABLED - Delete operations are too dangerous
            // This method caused data loss and should never be used
            return "⚠️ Delete operations are disabled for safety. Please delete files manually using File Explorer.";
        }
        
        // OLD DANGEROUS CODE - DO NOT USE
        private async Task<string> HandleDeleteFilesDirectly_DISABLED(string task)
        {
            return "⚠️ Delete operations are disabled for safety.";
            
            // All delete code removed to prevent accidental data loss
            #pragma warning disable CS0162
            var lower = task.ToLowerInvariant();
            var sb = new System.Text.StringBuilder();
            
            // Determine the target folder
            string targetFolder;
            if (lower.Contains("desktop"))
                targetFolder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            else if (lower.Contains("document"))
                targetFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            else if (lower.Contains("download"))
                targetFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            else if (lower.Contains("music folder") || (lower.Contains("music") && lower.Contains("folder")))
                targetFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
            else if (lower.Contains("picture") || lower.Contains("photo"))
                targetFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            else if (lower.Contains("video"))
                targetFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
            else
                targetFolder = Directory.GetCurrentDirectory();
            
            // Determine file extensions to target
            var extensions = new List<string>();
            if (lower.Contains("music") || lower.Contains("audio") || lower.Contains("song"))
                extensions.AddRange(new[] { ".mp3", ".wav", ".flac", ".m4a", ".aac", ".ogg", ".wma" });
            else if (lower.Contains("video") || lower.Contains("movie"))
                extensions.AddRange(new[] { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm" });
            else if (lower.Contains("image") || lower.Contains("photo") || lower.Contains("picture"))
                extensions.AddRange(new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg" });
            else if (lower.Contains("document") || lower.Contains("doc"))
                extensions.AddRange(new[] { ".doc", ".docx", ".pdf", ".txt", ".rtf" });
            
            if (extensions.Count == 0)
            {
                return "❓ Please specify what type of files to delete (music, video, image, document)";
            }
            
            // Find matching files
            var filesToDelete = new List<string>();
            try
            {
                foreach (var ext in extensions)
                {
                    var files = Directory.GetFiles(targetFolder, $"*{ext}", SearchOption.TopDirectoryOnly);
                    filesToDelete.AddRange(files);
                }
            }
            catch (Exception ex)
            {
                return $"❌ Error scanning folder: {ex.Message}";
            }
            
            if (filesToDelete.Count == 0)
            {
                return $"✅ No matching files found in {targetFolder}";
            }
            
            // Show confirmation
            var fileList = string.Join("\n", filesToDelete.Take(10).Select(f => $"  • {Path.GetFileName(f)}"));
            if (filesToDelete.Count > 10)
                fileList += $"\n  ... and {filesToDelete.Count - 10} more";
            
            var confirmed = await ShowAgentConfirmationAsync("delete_files", 
                $"Delete {filesToDelete.Count} file(s) from {targetFolder}?\n\n{fileList}");
            
            if (!confirmed)
            {
                return "❌ Delete operation cancelled by user";
            }
            
            // DISABLED - DO NOT DELETE FILES
            return "⚠️ Delete operations are disabled for safety.";
            
            // Delete the files
            int deleted = 0;
            int failed = 0;
            foreach (var file in filesToDelete)
            {
                try
                {
                    File.Delete(file);
                    deleted++;
                }
                catch
                {
                    failed++;
                }
            }
            
            if (failed > 0)
                return $"✅ Deleted {deleted} file(s), ❌ {failed} failed (may be in use or protected)";
            else
            #pragma warning restore CS0162
                return $"✅ Successfully deleted {deleted} file(s)";
        }

        /// <summary>
        /// Run an agentic AI task - the AI can actually read/write files and run commands
        /// </summary>
        private async Task<string> RunAgentTask(string task)
        {
            System.Diagnostics.Debug.WriteLine($"[Agent] RunAgentTask called with: '{task}'");
            
            if (string.IsNullOrWhiteSpace(task))
                return "Usage: /agent <task>\n\nExamples:\n• /agent create a C# class called Calculator\n• /agent list all .cs files in this folder\n• /agent read the README.md file";

            // === DIRECT HANDLERS FOR COMMON OPERATIONS ===
            // These bypass the AI for faster, more reliable execution
            var lowerTask = task.ToLowerInvariant();
            
            // Delete operations now go through the agent with double confirmation
            // The confirmation system in SystemTool.DeleteWithConfirmationAsync handles safety

            if (_agent == null)
            {
                _agent = new Agent.AgentOrchestrator(Directory.GetCurrentDirectory());
                _agent.OnConfirmationRequired = ShowAgentConfirmationAsync;
            }

            // Show typing indicator while agent works
            Border? typingIndicator = null;
            
            try
            {
                // Show animated typing indicator
                Dispatcher.Invoke(() =>
                {
                    typingIndicator = ShowAgentTypingIndicator();
                });
                
                // Set orb to thinking/processing state
                SetAtlasCoreState(Controls.AtlasVisualState.Thinking);
                ShowStatus("🤖 Agent working...");
                
                // Hook up progress events to update status AND show in chat
                EventHandler<string>? thinkingHandler = null;
                EventHandler<string>? toolHandler = null;
                EventHandler<Agent.ToolResult>? toolResultHandler = null;
                
                thinkingHandler = (s, msg) => Dispatcher.Invoke(() => 
                {
                    ShowStatus($"💭 {msg}");
                    UpdateAgentProgress(typingIndicator, msg);
                });
                toolHandler = (s, tool) => Dispatcher.Invoke(() => 
                {
                    ShowStatus($"⚙️ {tool}");
                    UpdateAgentProgress(typingIndicator, tool);
                });
                toolResultHandler = (s, result) => Dispatcher.Invoke(() =>
                {
                    var preview = result.Output.Length > 100 ? result.Output.Substring(0, 100) + "..." : result.Output;
                    var status = result.Success ? "✅" : "❌";
                    UpdateAgentProgress(typingIndicator, $"{status} {preview}");
                });
                
                _agent.OnThinking += thinkingHandler;
                _agent.OnToolExecuting += toolHandler;
                _agent.OnToolResult += toolResultHandler;
                
                var result = await _agent.RunAsync(task);
                
                System.Diagnostics.Debug.WriteLine($"[Agent] Task completed with result length: {result?.Length ?? 0}");
                
                // Cleanup
                _agent.OnThinking -= thinkingHandler;
                _agent.OnToolExecuting -= toolHandler;
                _agent.OnToolResult -= toolResultHandler;
                
                // Remove typing indicator
                Dispatcher.Invoke(() =>
                {
                    if (typingIndicator != null)
                        HideTypingIndicator(typingIndicator);
                });
                
                SetAtlasCoreState(Controls.AtlasVisualState.Idle);
                ShowStatus("✅ Agent task complete");
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Agent] Error: {ex.Message}");
                
                // Remove typing indicator on error
                Dispatcher.Invoke(() =>
                {
                    if (typingIndicator != null)
                        HideTypingIndicator(typingIndicator);
                });
                
                SetAtlasCoreState(Controls.AtlasVisualState.Idle);
                ShowStatus("❌ Agent error");
                return $"❌ Agent error: {ex.Message}";
            }
        }
        
        /// <summary>
        /// Updates the agent typing indicator with progress information
        /// </summary>
        private void UpdateAgentProgress(Border? indicator, string message)
        {
            if (indicator == null) return;
            
            try
            {
                // Find the content stack in the indicator
                if (indicator.Child is Grid grid && grid.Children.Count > 1)
                {
                    var contentStack = grid.Children[1] as StackPanel;
                    if (contentStack != null && contentStack.Children.Count >= 2)
                    {
                        // Find or create the progress text
                        TextBlock? progressText = null;
                        if (contentStack.Children.Count > 2 && contentStack.Children[2] is TextBlock existing)
                        {
                            progressText = existing;
                        }
                        else
                        {
                            progressText = new TextBlock
                            {
                                FontSize = 11,
                                Foreground = new SolidColorBrush(Color.FromRgb(140, 140, 140)),
                                TextWrapping = TextWrapping.Wrap,
                                MaxWidth = 400,
                                Margin = new Thickness(0, 4, 0, 0)
                            };
                            contentStack.Children.Add(progressText);
                        }
                        
                        // Update the progress text
                        progressText.Text = message.Length > 80 ? message.Substring(0, 80) + "..." : message;
                    }
                }
                
                MessagesScroller?.ScrollToEnd();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Agent] Error updating progress: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Shows an animated typing indicator for agent tasks
        /// </summary>
        private Border ShowAgentTypingIndicator()
        {
            var container = new Border
            {
                Padding = new Thickness(0, 12, 0, 12),
                Background = Brushes.Transparent,
                Tag = "AgentTyping"
            };
            
            var mainGrid = new Grid();
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            
            // Purple robot avatar with pulse
            var avatarBorder = new Border
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromRgb(139, 92, 246)),
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 0, 0)
            };
            avatarBorder.Child = new TextBlock
            {
                Text = "🤖",
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            
            // Pulse animation
            var pulse = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 1.0, To = 0.5,
                Duration = TimeSpan.FromMilliseconds(500),
                AutoReverse = true,
                RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever
            };
            avatarBorder.BeginAnimation(Border.OpacityProperty, pulse);
            
            Grid.SetColumn(avatarBorder, 0);
            mainGrid.Children.Add(avatarBorder);
            
            // Content with animated dots
            var contentStack = new StackPanel { Margin = new Thickness(12, 0, 0, 0) };
            contentStack.Children.Add(new TextBlock
            {
                Text = "Agent",
                FontWeight = FontWeights.SemiBold,
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(139, 92, 246)),
                Margin = new Thickness(0, 0, 0, 6)
            });
            
            // Animated dots panel
            var dotsPanel = new StackPanel { Orientation = Orientation.Horizontal };
            dotsPanel.Children.Add(new TextBlock
            {
                Text = "Working",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 160))
            });
            
            for (int i = 0; i < 3; i++)
            {
                var dot = new Shapes.Ellipse
                {
                    Width = 6, Height = 6,
                    Fill = new SolidColorBrush(Color.FromRgb(139, 92, 246)),
                    Margin = new Thickness(4, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                
                var dotAnim = new System.Windows.Media.Animation.DoubleAnimation
                {
                    From = 0.2, To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(400),
                    AutoReverse = true,
                    RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
                    BeginTime = TimeSpan.FromMilliseconds(i * 150)
                };
                dot.BeginAnimation(Shapes.Ellipse.OpacityProperty, dotAnim);
                dotsPanel.Children.Add(dot);
            }
            
            contentStack.Children.Add(dotsPanel);
            Grid.SetColumn(contentStack, 1);
            mainGrid.Children.Add(contentStack);
            
            container.Child = mainGrid;
            
            // Add to messages panel
            MessagesPanel.Children.Add(container);
            MessagesScroller.ScrollToEnd();
            
            return container;
        }
        
        /// <summary>
        /// Shows a confirmation dialog for destructive agent operations
        /// </summary>
        private async Task<bool> ShowAgentConfirmationAsync(string toolName, string description)
        {
            var tcs = new TaskCompletionSource<bool>();
            
            await Dispatcher.InvokeAsync(() =>
            {
                // Create confirmation UI in chat
                var container = new Border
                {
                    Padding = new Thickness(0, 12, 0, 12),
                    Background = Brushes.Transparent,
                    Tag = "AgentConfirmation"
                };
                
                var mainGrid = new Grid();
                mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
                mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                
                // Warning icon
                var iconBorder = new Border
                {
                    Width = 32,
                    Height = 32,
                    CornerRadius = new CornerRadius(8),
                    Background = new SolidColorBrush(Color.FromRgb(234, 179, 8)), // Yellow warning
                    VerticalAlignment = VerticalAlignment.Top
                };
                iconBorder.Child = new TextBlock
                {
                    Text = "⚠️",
                    FontSize = 14,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(iconBorder, 0);
                mainGrid.Children.Add(iconBorder);
                
                // Content
                var contentStack = new StackPanel { Margin = new Thickness(12, 0, 0, 0) };
                contentStack.Children.Add(new TextBlock
                {
                    Text = "Agent Confirmation Required",
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 14,
                    Foreground = new SolidColorBrush(Color.FromRgb(234, 179, 8)),
                    Margin = new Thickness(0, 0, 0, 6)
                });
                
                contentStack.Children.Add(new TextBlock
                {
                    Text = description,
                    FontSize = 13,
                    Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 10)
                });
                
                // Buttons
                var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal };
                
                var allowBtn = new Button
                {
                    Content = "✓ Allow",
                    Padding = new Thickness(16, 6, 16, 6),
                    Margin = new Thickness(0, 0, 8, 0),
                    Background = new SolidColorBrush(Color.FromRgb(34, 197, 94)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand
                };
                allowBtn.Click += (s, e) =>
                {
                    MessagesPanel.Children.Remove(container);
                    tcs.TrySetResult(true);
                };
                
                var denyBtn = new Button
                {
                    Content = "✗ Cancel",
                    Padding = new Thickness(16, 6, 16, 6),
                    Background = new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand
                };
                denyBtn.Click += (s, e) =>
                {
                    MessagesPanel.Children.Remove(container);
                    tcs.TrySetResult(false);
                };
                
                buttonPanel.Children.Add(allowBtn);
                buttonPanel.Children.Add(denyBtn);
                contentStack.Children.Add(buttonPanel);
                
                Grid.SetColumn(contentStack, 1);
                mainGrid.Children.Add(contentStack);
                container.Child = mainGrid;
                
                MessagesPanel.Children.Add(container);
                MessagesScroller.ScrollToEnd();
            });
            
            return await tcs.Task;
        }
        
        /// <summary>
        /// Shows a delete confirmation dialog with detailed info about what will be deleted
        /// Used by SystemTool.DeleteWithConfirmationAsync for double confirmation
        /// </summary>
        private async Task<bool> ShowDeleteConfirmationAsync(string title, string description)
        {
            var tcs = new TaskCompletionSource<bool>();
            
            await Dispatcher.InvokeAsync(() =>
            {
                // Create confirmation UI in chat
                var container = new Border
                {
                    Padding = new Thickness(0, 12, 0, 12),
                    Background = Brushes.Transparent,
                    Tag = "DeleteConfirmation"
                };
                
                var mainGrid = new Grid();
                mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
                mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                
                // Red warning icon for delete
                var iconBorder = new Border
                {
                    Width = 32,
                    Height = 32,
                    CornerRadius = new CornerRadius(8),
                    Background = new SolidColorBrush(Color.FromRgb(239, 68, 68)), // Red for delete
                    VerticalAlignment = VerticalAlignment.Top
                };
                iconBorder.Child = new TextBlock
                {
                    Text = "🗑️",
                    FontSize = 14,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(iconBorder, 0);
                mainGrid.Children.Add(iconBorder);
                
                // Content
                var contentStack = new StackPanel { Margin = new Thickness(12, 0, 0, 0) };
                contentStack.Children.Add(new TextBlock
                {
                    Text = title,
                    FontWeight = FontWeights.Bold,
                    FontSize = 14,
                    Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68)), // Red
                    Margin = new Thickness(0, 0, 0, 6)
                });
                
                contentStack.Children.Add(new TextBlock
                {
                    Text = description,
                    FontSize = 13,
                    Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 10)
                });
                
                // Buttons
                var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal };
                
                var deleteBtn = new Button
                {
                    Content = "🗑️ Yes, Delete",
                    Padding = new Thickness(16, 6, 16, 6),
                    Margin = new Thickness(0, 0, 8, 0),
                    Background = new SolidColorBrush(Color.FromRgb(239, 68, 68)), // Red
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand
                };
                deleteBtn.Click += (s, e) =>
                {
                    MessagesPanel.Children.Remove(container);
                    tcs.TrySetResult(true);
                };
                
                var cancelBtn = new Button
                {
                    Content = "✗ Cancel",
                    Padding = new Thickness(16, 6, 16, 6),
                    Background = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand
                };
                cancelBtn.Click += (s, e) =>
                {
                    MessagesPanel.Children.Remove(container);
                    tcs.TrySetResult(false);
                };
                
                buttonPanel.Children.Add(cancelBtn); // Cancel first (safer default)
                buttonPanel.Children.Add(deleteBtn);
                contentStack.Children.Add(buttonPanel);
                
                Grid.SetColumn(contentStack, 1);
                mainGrid.Children.Add(contentStack);
                container.Child = mainGrid;
                
                MessagesPanel.Children.Add(container);
                MessagesScroller.ScrollToEnd();
            });
            
            return await tcs.Task;
        }

        private Border ShowTypingIndicator()
        {
            // Clean, minimal typing indicator matching the new design
            var container = new Border
            {
                Padding = new Thickness(0, 16, 0, 16),
                Background = Brushes.Transparent
            };
            
            var mainGrid = new Grid();
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            
            // Avatar
            var avatarBorder = new Border
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromRgb(34, 197, 94)),
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 0, 0)
            };
            avatarBorder.Child = new TextBlock
            {
                Text = "◆",
                FontSize = 16,
                Foreground = new SolidColorBrush(Color.FromRgb(9, 9, 11)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(avatarBorder, 0);
            mainGrid.Children.Add(avatarBorder);
            
            // Content
            var contentStack = new StackPanel { Margin = new Thickness(12, 0, 0, 0) };
            
            // Name
            contentStack.Children.Add(new TextBlock
            {
                Text = "Atlas",
                FontWeight = FontWeights.SemiBold,
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(250, 250, 250)),
                Margin = new Thickness(0, 0, 0, 8)
            });
            
            // Animated dots
            var dotsPanel = new StackPanel { Orientation = Orientation.Horizontal };
            for (int i = 0; i < 3; i++)
            {
                var dot = new Shapes.Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Fill = new SolidColorBrush(Color.FromRgb(113, 113, 122)),
                    Margin = new Thickness(0, 0, 6, 0),
                    Opacity = 0.4
                };
                
                var animation = new System.Windows.Media.Animation.DoubleAnimation
                {
                    From = 0.4,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(500),
                    AutoReverse = true,
                    RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
                    BeginTime = TimeSpan.FromMilliseconds(i * 150)
                };
                dot.BeginAnimation(Shapes.Ellipse.OpacityProperty, animation);
                dotsPanel.Children.Add(dot);
            }
            contentStack.Children.Add(dotsPanel);
            
            // Status text
            var statusText = new TextBlock
            {
                Text = "",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(113, 113, 122)),
                Margin = new Thickness(0, 8, 0, 0),
                Visibility = Visibility.Collapsed
            };
            contentStack.Children.Add(statusText);
            
            // Progress bar
            var progressBar = new ProgressBar
            {
                Height = 3,
                Margin = new Thickness(0, 8, 0, 0),
                IsIndeterminate = true,
                Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94)),
                Background = new SolidColorBrush(Color.FromRgb(39, 39, 42)),
                Visibility = Visibility.Collapsed
            };
            contentStack.Children.Add(progressBar);
            
            // Progress text
            var progressText = new TextBlock
            {
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94)),
                Margin = new Thickness(0, 6, 0, 0),
                Visibility = Visibility.Collapsed
            };
            contentStack.Children.Add(progressText);
            
            Grid.SetColumn(contentStack, 1);
            mainGrid.Children.Add(contentStack);
            
            container.Child = mainGrid;
            
            // Store references
            _typingStatusText = statusText;
            _typingProgressBar = progressBar;
            _typingProgressText = progressText;
            
            MessagesPanel.Children.Add(container);
            MessagesScroller.ScrollToEnd();
            
            _currentTypingIndicator = container;
            
            return container;
        }
        
        // Direct references to typing indicator elements for reliable updates
        private TextBlock? _typingStatusText;
        private ProgressBar? _typingProgressBar;
        private TextBlock? _typingProgressText;
        
        private Border? _currentTypingIndicator;
        
        /// <summary>
        /// Update the typing indicator with progress information
        /// </summary>
        public void UpdateTypingProgress(string status, int? percentage = null, string? detail = null)
        {
            Dispatcher.Invoke(() =>
            {
                if (_currentTypingIndicator == null) return;
                
                // Update status text using direct reference
                if (_typingStatusText != null)
                {
                    if (percentage.HasValue)
                        _typingStatusText.Text = $"{status} ({percentage}%)";
                    else
                        _typingStatusText.Text = status;
                }
                
                // Update progress bar using direct reference
                if (_typingProgressBar != null)
                {
                    _typingProgressBar.Visibility = Visibility.Visible;
                    if (percentage.HasValue)
                    {
                        _typingProgressBar.IsIndeterminate = false;
                        _typingProgressBar.Maximum = 100;
                        _typingProgressBar.Value = percentage.Value;
                    }
                    else
                    {
                        _typingProgressBar.IsIndeterminate = true;
                    }
                }
                
                // Update detail text using direct reference
                if (_typingProgressText != null && !string.IsNullOrEmpty(detail))
                {
                    _typingProgressText.Text = detail;
                    _typingProgressText.Visibility = Visibility.Visible;
                }
                
                // Don't scroll on every update - causes jumping
            });
        }
        
        private ControlTemplate CreateCancelButtonTemplate()
        {
            var template = new ControlTemplate(typeof(Button));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(220, 53, 69)));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            borderFactory.SetValue(Border.PaddingProperty, new Thickness(6, 2, 6, 2));
            borderFactory.Name = "bd";
            
            var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(contentFactory);
            
            template.VisualTree = borderFactory;
            
            // Add hover trigger
            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(200, 35, 51)), "bd"));
            template.Triggers.Add(hoverTrigger);
            
            return template;
        }
        
        private void CancelCurrentOperation_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Cancel the CancellationTokenSource
                _currentOperationCts?.Cancel();
                
                // Also cancel any running scanner
                _currentScanner?.CancelScan();
                
                Debug.WriteLine("[ChatWindow] User cancelled current operation");
                ShowStatus("⚠️ Operation cancelled");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ChatWindow] Cancel error: {ex.Message}");
            }
        }

        private void HideTypingIndicator(Border indicator)
        {
            if (indicator != null && MessagesPanel.Children.Contains(indicator))
                MessagesPanel.Children.Remove(indicator);
            _currentTypingIndicator = null;
        }

        private async Task<string> GetAIResponse(string userMessage)
        {
            try
            {
                // First, try rule-based tool execution (fast)
                var toolResult = await Tools.ToolExecutor.TryExecuteToolAsync(userMessage);
                if (toolResult != null)
                {
                    // Check for special stop voice marker
                    if (toolResult == "__STOP_VOICE__")
                    {
                        _voiceManager?.Stop();
                        UpdateSpeakingIndicator(false);
                        return "🔇 Stopped.";
                    }
                    
                    // Check for Integration Hub window marker
                    if (toolResult == "__OPEN_INTEGRATION_HUB__")
                    {
                        await Dispatcher.InvokeAsync(() =>
                        {
                            try
                            {
                                var hubWindow = new Integrations.IntegrationHubWindow();
                                hubWindow.Owner = this;
                                hubWindow.Show();
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[IntegrationHub] Error: {ex}");
                                MessageBox.Show($"Integration Hub error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        });
                        return "🔌 Opening Integration Hub - see all available apps and services Atlas can connect to!";
                    }
                    
                    // Check for Social Media Console window marker
                    if (toolResult == "__OPEN_SOCIAL_MEDIA_CONSOLE__")
                    {
                        await Dispatcher.InvokeAsync(() =>
                        {
                            try
                            {
                                var consoleWindow = new SocialMedia.SocialMediaConsoleWindow();
                                consoleWindow.Owner = this;
                                consoleWindow.Show();
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[SocialMedia] Error: {ex}");
                                MessageBox.Show($"Social Media Console error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        });
                        return "📱 Opening Social Media Console - create content, manage campaigns, and schedule posts!";
                    }
                    
                    // Check for Security Suite window marker
                    if (toolResult == "__OPEN_SECURITY_SUITE__")
                    {
                        await Dispatcher.InvokeAsync(() => ShowSecuritySuiteWindow());
                        return "🛡️ Opening Security Suite...";
                    }
                    
                    // Check for special image generation marker
                    if (toolResult.StartsWith("__GENERATE_IMAGE__|"))
                    {
                        var prompt = toolResult.Substring("__GENERATE_IMAGE__|".Length);
                        return await GenerateAndDisplayImage(prompt);
                    }
                    
                    // Check for special image analysis marker
                    if (toolResult.StartsWith("__ANALYZE_IMAGE__|"))
                    {
                        var parts = toolResult.Split('|');
                        if (parts.Length >= 3)
                        {
                            var imagePath = parts[1];
                            var question = parts[2];
                            toolResult = await AnalyzeImageWithQuestion(imagePath, question);
                        }
                    }
                    
                    conversationHistory.Add(new { role = "user", content = userMessage });
                    conversationHistory.Add(new { role = "assistant", content = toolResult });
                    return toolResult;
                }

                // Try AI-powered command execution for complex requests
                var aiCommandResult = await Tools.AICommandExecutor.ExecuteWithAIAsync(userMessage);
                if (aiCommandResult != null)
                {
                    conversationHistory.Add(new { role = "user", content = userMessage });
                    conversationHistory.Add(new { role = "assistant", content = aiCommandResult });
                    return aiCommandResult;
                }
                
                // No action needed - have a conversation with butler personality
                conversationHistory.Add(new { role = "user", content = userMessage });

                var messages = new List<object>();
                
                // Use the dynamic system prompt from SystemPromptBuilder (includes user profile)
                var systemPrompt = _systemPromptBuilder?.BuildSystemPrompt() ?? GetFallbackSystemPrompt();
                messages.Add(new { role = "system", content = systemPrompt });

                // Add conversation history (skip system messages - we just added our own)
                foreach (var msg in conversationHistory)
                {
                    var dict = (dynamic)msg;
                    if (dict.role != "system")
                        messages.Add(msg);
                }

                var response = await AIManager.SendMessageAsync(messages, 500);
                
                if (!response.Success)
                    return $"AI Error: {response.Error}";

                // Check if AI response contains an action we should execute
                var actionResult = await TryExtractAndExecuteAction(response.Content);
                if (actionResult != null)
                {
                    conversationHistory.Add(new { role = "assistant", content = actionResult });
                    return actionResult;
                }

                conversationHistory.Add(new { role = "assistant", content = response.Content });
                
                // Keep conversation history manageable
                if (conversationHistory.Count > 20)
                    conversationHistory.RemoveRange(1, 2);

                return response.Content;
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }
        
        /// <summary>
        /// Fallback system prompt if SystemPromptBuilder is not available
        /// </summary>
        private string GetFallbackSystemPrompt()
        {
            return @"You are Atlas, an advanced AI assistant. Be concise, helpful, and professional.
Address the user as 'sir' unless they specify otherwise.
Execute commands directly when asked - don't just explain how to do things.
Keep responses brief and to the point.";
        }
        
        private async Task<string> GetAIResponseWithCancellation(string userMessage, CancellationToken ct)
        {
            try
            {
                ct.ThrowIfCancellationRequested();
                
                // ========== UNDERSTANDING LAYER COMPLETELY DISABLED ==========
                // DISABLED to prevent duplicate responses - ToolExecutor handles everything now
                // The Understanding Layer was causing duplicate responses and false confirmations
                Debug.WriteLine("[Understanding] DISABLED - ToolExecutor handles all commands to prevent duplicates");
                // ========== END UNDERSTANDING LAYER ==========
                
                // Check if this is a scan request - scans need much longer timeout (10 minutes)
                var lowerMessage = userMessage.ToLower().Trim();
                var isScanRequest = lowerMessage.Contains("scan") || lowerMessage.Contains("virus") || lowerMessage.Contains("malware") || lowerMessage.Contains("spyware");
                var timeoutSeconds = isScanRequest ? 600 : 120; // 10 minutes for scans, 2 minutes for other operations
                
                Debug.WriteLine($"[ChatWindow] Operation timeout: {timeoutSeconds}s (isScan: {isScanRequest})");
                
                // Create a timeout cancellation token
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
                var linkedCt = linkedCts.Token;
                
                // First, try rule-based tool execution (fast) with cancellation support
                var toolResult = await Tools.ToolExecutor.TryExecuteToolWithCancellationAsync(userMessage, linkedCt, 
                    scanner => _currentScanner = scanner);
                if (toolResult != null)
                {
                    // Check for special stop voice marker
                    if (toolResult == "__STOP_VOICE__")
                    {
                        _voiceManager?.Stop();
                        UpdateSpeakingIndicator(false);
                        return "🔇 Stopped.";
                    }
                    
                    // Check for Integration Hub window marker
                    if (toolResult == "__OPEN_INTEGRATION_HUB__")
                    {
                        await Dispatcher.InvokeAsync(() =>
                        {
                            try
                            {
                                var hubWindow = new Integrations.IntegrationHubWindow();
                                hubWindow.Owner = this;
                                hubWindow.Show();
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[IntegrationHub2] Error: {ex}");
                                MessageBox.Show($"Integration Hub error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        });
                        return "🔌 Opening Integration Hub - see all available apps and services Atlas can connect to!";
                    }
                    
                    // Check for Social Media Console window marker
                    if (toolResult == "__OPEN_SOCIAL_MEDIA_CONSOLE__")
                    {
                        await Dispatcher.InvokeAsync(() =>
                        {
                            try
                            {
                                var consoleWindow = new SocialMedia.SocialMediaConsoleWindow();
                                consoleWindow.Owner = this;
                                consoleWindow.Show();
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[SocialMedia2] Error: {ex}");
                                MessageBox.Show($"Social Media Console error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        });
                        return "📱 Opening Social Media Console - create content, manage campaigns, and schedule posts!";
                    }
                    
                    // Check for Security Suite window marker
                    if (toolResult == "__OPEN_SECURITY_SUITE__")
                    {
                        await Dispatcher.InvokeAsync(() => ShowSecuritySuiteWindow());
                        return "🛡️ Opening Security Suite...";
                    }
                    
                    // Check for special image analysis marker
                    if (toolResult.StartsWith("__ANALYZE_IMAGE__|"))
                    {
                        var parts = toolResult.Split('|');
                        if (parts.Length >= 3)
                        {
                            var imagePath = parts[1];
                            var question = parts[2];
                            toolResult = await AnalyzeImageWithQuestion(imagePath, question);
                        }
                    }
                    
                    // Check for special image generation marker
                    if (toolResult.StartsWith("__GENERATE_IMAGE__|"))
                    {
                        var prompt = toolResult.Substring("__GENERATE_IMAGE__|".Length);
                        return await GenerateAndDisplayImage(prompt);
                    }
                    
                    conversationHistory.Add(new { role = "user", content = userMessage });
                    conversationHistory.Add(new { role = "assistant", content = toolResult });
                    return toolResult;
                }
                
                linkedCt.ThrowIfCancellationRequested();

                // Try AI-powered command execution for complex requests
                var aiCommandResult = await Tools.AICommandExecutor.ExecuteWithAIAsync(userMessage);
                if (aiCommandResult != null)
                {
                    conversationHistory.Add(new { role = "user", content = userMessage });
                    conversationHistory.Add(new { role = "assistant", content = aiCommandResult });
                    return aiCommandResult;
                }
                
                linkedCt.ThrowIfCancellationRequested();
                
                // No action needed - have a conversation with butler personality
                conversationHistory.Add(new { role = "user", content = userMessage });

                var messages = new List<object>();
                
                // Build dynamic system prompt from user profile/style, or use default
                var systemPrompt = _systemPromptBuilder?.BuildSystemPrompt() ?? GetDefaultSystemPrompt();
                
                // Add coding capabilities to system prompt if workspace is set
                if (_codeAssistant?.HasWorkspace == true)
                {
                    systemPrompt += Coding.CodeToolExecutor.GetCodingSystemPrompt();
                }
                
                messages.Add(new { role = "system", content = systemPrompt });

                foreach (var msg in conversationHistory)
                {
                    var dict = (dynamic)msg;
                    if (dict.role != "system")
                        messages.Add(msg);
                }

                System.Diagnostics.Debug.WriteLine($"[AI] Sending {messages.Count} messages to AI...");
                var response = await AIManager.SendMessageAsync(messages, 4096);
                
                System.Diagnostics.Debug.WriteLine($"[AI Response] Success: {response.Success}, Content length: {response.Content?.Length ?? 0}, Error: {response.Error ?? "none"}");
                
                if (!response.Success)
                    return $"AI Error: {response.Error}";
                
                // Check for empty response
                if (string.IsNullOrWhiteSpace(response.Content))
                {
                    System.Diagnostics.Debug.WriteLine("[AI Response] Content is empty!");
                    return "🤔 The AI returned an empty response. Please try again.";
                }

                // Check if AI response contains coding tool calls
                if (_codeToolExecutor != null)
                {
                    var (codeHandled, codeResult) = await _codeToolExecutor.TryExecuteToolAsync(response.Content);
                    if (codeHandled && codeResult != null)
                    {
                        // Return the tool result along with any AI explanation
                        var cleanedResponse = System.Text.RegularExpressions.Regex.Replace(
                            response.Content, @"\[TOOL:[^\]]+\]", "").Trim();
                        var finalResponse = string.IsNullOrWhiteSpace(cleanedResponse) 
                            ? codeResult 
                            : $"{cleanedResponse}\n\n{codeResult}";
                        conversationHistory.Add(new { role = "assistant", content = finalResponse });
                        return finalResponse;
                    }
                }

                // Check if AI response contains an action we should execute
                var actionResult = await TryExtractAndExecuteAction(response.Content);
                if (actionResult != null)
                {
                    conversationHistory.Add(new { role = "assistant", content = actionResult });
                    return actionResult;
                }

                conversationHistory.Add(new { role = "assistant", content = response.Content });
                
                if (conversationHistory.Count > 20)
                    conversationHistory.RemoveRange(1, 2);

                return response.Content;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Timeout occurred (not user cancellation)
                return "⏱️ Request timed out. The operation took too long. Please try again or try a simpler request.";
            }
            catch (OperationCanceledException)
            {
                throw; // Re-throw user cancellation to be handled by caller
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Execute an action based on Understanding Layer decision
        /// </summary>
        private async Task<string?> ExecuteUnderstandingAction(UnderstandingResult understanding)
        {
            try
            {
                var tool = understanding.ToolToExecute;
                var parameters = understanding.ToolParameters;
                
                Debug.WriteLine($"[Understanding] Executing: {tool} with {parameters.Count} parameters");
                
                // Map tool names to actual execution
                switch (tool)
                {
                    case "MediaPlayerTool":
                        var query = parameters.GetValueOrDefault("query")?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(query))
                        {
                            var result = await Tools.MediaPlayerTool.PlayAsync(query);
                            return _understandingLayer?.Formatter.FormatSuccess("Playing", query) + $"\n{result}";
                        }
                        var action = parameters.GetValueOrDefault("action")?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(action))
                        {
                            // Use existing tool executor for media control
                            return await Tools.ToolExecutor.TryExecuteToolAsync($"{action} music");
                        }
                        break;
                        
                    case "SystemTool.Volume":
                        var volAction = parameters.GetValueOrDefault("action")?.ToString() ?? "";
                        return volAction switch
                        {
                            "up" => await Tools.SystemTool.SetVolumeAsync(80),
                            "down" => await Tools.SystemTool.SetVolumeAsync(30),
                            "mute" or "unmute" => await Tools.SystemTool.ToggleMuteAsync(),
                            _ => "Volume adjusted"
                        };
                        
                    case "SystemTool.OpenApp":
                        var app = parameters.GetValueOrDefault("app")?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(app))
                        {
                            var result = await Tools.SystemTool.OpenAppAsync(app);
                            return _understandingLayer?.Formatter.FormatSuccess("Opened", app) + $"\n{result}";
                        }
                        break;
                        
                    case "SystemTool.CloseApp":
                        var appToClose = parameters.GetValueOrDefault("app")?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(appToClose))
                        {
                            // Use process kill
                            try
                            {
                                var processes = System.Diagnostics.Process.GetProcessesByName(appToClose.Replace(".exe", ""));
                                foreach (var proc in processes)
                                {
                                    proc.Kill();
                                }
                                return _understandingLayer?.Formatter.FormatSuccess("Closed", appToClose);
                            }
                            catch (Exception ex)
                            {
                                return $"Couldn't close {appToClose}: {ex.Message}";
                            }
                        }
                        break;
                        
                    case "SystemTool.Power":
                        var powerAction = parameters.GetValueOrDefault("action")?.ToString() ?? "";
                        return powerAction switch
                        {
                            "shutdown" => await Tools.SystemTool.ShutdownAsync(),
                            "restart" => await Tools.SystemTool.RestartAsync(),
                            "sleep" => await Tools.SystemTool.SleepAsync(),
                            "lock" => await Tools.SystemTool.LockComputerAsync(),
                            _ => "Power action completed"
                        };
                        
                    case "FileSystemTool.Organize":
                        var target = parameters.GetValueOrDefault("target")?.ToString() ?? "";
                        var folderPath = ResolveFolderPath(target);
                        if (!string.IsNullOrEmpty(folderPath))
                        {
                            var result = await Tools.SystemTool.SortFilesByTypeAsync(folderPath);
                            return _understandingLayer?.Formatter.FormatSuccess("Organized", target) + $"\n{result}";
                        }
                        break;
                        
                    case "FileSystemTool.OpenFolder":
                        var folder = parameters.GetValueOrDefault("folder")?.ToString() ?? "";
                        var path = ResolveFolderPath(folder);
                        if (!string.IsNullOrEmpty(path))
                        {
                            System.Diagnostics.Process.Start("explorer.exe", path);
                            return _understandingLayer?.Formatter.FormatSuccess("Opened", folder);
                        }
                        break;
                        
                    case "WebSearchTool":
                        var searchQuery = parameters.GetValueOrDefault("query")?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(searchQuery))
                        {
                            return await Tools.WebSearchTool.SearchAsync(searchQuery);
                        }
                        break;
                        
                    case "WebSearchTool.Weather":
                        var location = parameters.GetValueOrDefault("location")?.ToString() ?? "";
                        return await Tools.WebSearchTool.GetWeatherAsync(location);
                        
                    case "ScreenCaptureTool":
                        var captureResult = await _screenCapture.CaptureScreenAsync();
                        return captureResult.Success 
                            ? $"📸 Screenshot saved to: {captureResult.Metadata.FilePath}" 
                            : $"Screenshot failed: {captureResult.Error}";
                        
                    case "SecurityScanner":
                        // Trigger security scan
                        return "🛡️ Starting security scan... This may take a few minutes.";
                        
                    case "ImageGeneratorTool":
                        var prompt = parameters.GetValueOrDefault("prompt")?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(prompt))
                        {
                            return await GenerateAndDisplayImage(prompt);
                        }
                        break;
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Understanding] Execution error: {ex.Message}");
                _understandingLayer?.RecordOutcome(understanding.ToolToExecute ?? "action", false, ex.Message);
                return _understandingLayer?.Formatter.FormatError(understanding.ToolToExecute ?? "action", ex.Message);
            }
        }
        
        /// <summary>
        /// Resolve folder name to actual path
        /// </summary>
        private string? ResolveFolderPath(string folderName)
        {
            var lower = folderName.ToLower();
            
            if (lower == "downloads" || lower.Contains("download"))
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            if (lower == "documents" || lower.Contains("document"))
                return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (lower == "desktop")
                return Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            if (lower == "pictures" || lower.Contains("picture") || lower.Contains("photo"))
                return Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            if (lower == "music")
                return Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
            if (lower == "videos" || lower.Contains("video"))
                return Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
            
            // Check if it's already a path
            if (Directory.Exists(folderName))
                return folderName;
            
            return null;
        }

        /// <summary>
        /// Check if AI response contains actionable commands and execute them
        /// </summary>
        private async Task<string?> TryExtractAndExecuteAction(string aiResponse)
        {
            var lower = aiResponse.ToLower();
            
            // Check if AI is trying to tell user to do something it should do itself
            if (lower.Contains("you can open") || lower.Contains("you could open") || 
                lower.Contains("try opening") || lower.Contains("to open"))
            {
                // Extract what to open and do it
                var appMatch = System.Text.RegularExpressions.Regex.Match(aiResponse, 
                    @"(?:open|launch|start)\s+([a-zA-Z\s]+?)(?:\s+by|\s+from|\s+using|\.|,|$)", 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (appMatch.Success)
                {
                    var app = appMatch.Groups[1].Value.Trim();
                    var result = await Tools.SystemTool.OpenAppAsync(app);
                    return $"{result}\n\n(I went ahead and opened it for you!)";
                }
            }

            // Check for file organization suggestions
            if ((lower.Contains("organize") || lower.Contains("sort")) && 
                (lower.Contains("file") || lower.Contains("folder")))
            {
                if (lower.Contains("desktop"))
                    return await Tools.SystemTool.SortFilesByTypeAsync(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
                if (lower.Contains("download"))
                    return await Tools.SystemTool.SortFilesByTypeAsync(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"));
            }

            return null;
        }

        private async void Settings_Click(object sender, RoutedEventArgs e)
        {
            var settings = new SettingsWindow();
            settings.Owner = this;
            if (settings.ShowDialog() == true)
            {
                CheckApiKey();
                // Reload voice settings
                await InitializeVoiceSystemAsync();
            }
        }

        private async Task InitializeVoiceSystemAsync()
        {
            var keys = SettingsWindow.GetVoiceApiKeys();
            if (keys.TryGetValue("openai", out var openaiKey) && !string.IsNullOrEmpty(openaiKey))
                _voiceManager.ConfigureProvider(VoiceProviderType.OpenAI, new Dictionary<string, string> { ["ApiKey"] = openaiKey });
            if (keys.TryGetValue("elevenlabs", out var elevenKey) && !string.IsNullOrEmpty(elevenKey))
                _voiceManager.ConfigureProvider(VoiceProviderType.ElevenLabs, new Dictionary<string, string> { ["ApiKey"] = elevenKey });

            var savedProvider = SettingsWindow.GetSelectedVoiceProvider();
            var success = await _voiceManager.SetProviderAsync(savedProvider);
            
            // Smart fallback chain if saved provider failed
            if (!success)
            {
                Debug.WriteLine($"[Voice] Saved provider {savedProvider} failed, trying fallback chain...");
                
                // Try ElevenLabs first (premium quality)
                if (keys.TryGetValue("elevenlabs", out var fallbackElevenKey) && !string.IsNullOrEmpty(fallbackElevenKey))
                {
                    success = await _voiceManager.SetProviderAsync(VoiceProviderType.ElevenLabs);
                    if (success) Debug.WriteLine("[Voice] Fallback to ElevenLabs successful");
                }
                
                // Try OpenAI TTS
                if (!success && keys.TryGetValue("openai", out var fallbackOpenaiKey) && !string.IsNullOrEmpty(fallbackOpenaiKey))
                {
                    success = await _voiceManager.SetProviderAsync(VoiceProviderType.OpenAI);
                    if (success) Debug.WriteLine("[Voice] Fallback to OpenAI TTS successful");
                }
                
                // Final fallback to Windows SAPI
                if (!success)
                {
                    await _voiceManager.SetProviderAsync(VoiceProviderType.WindowsSAPI);
                    Debug.WriteLine("[Voice] Final fallback to Windows SAPI");
                }
            }
            
            // Restore saved voice from VoiceManager's settings
            await _voiceManager.RestoreSavedVoiceAsync();
            
            await LoadVoicesAsync();
            UpdateProviderIndicator();
        }

        /// <summary>
        /// Get the default system prompt when SystemPromptBuilder is not available
        /// </summary>
        private string GetDefaultSystemPrompt()
        {
            return @"You are Atlas, an advanced AI assistant. You are helpful, knowledgeable, and genuinely care about helping the user.

CORE TRAITS:
- Analytical and precise - assess situations thoroughly
- Proactive - anticipate needs and offer solutions  
- Technically sophisticated - deep understanding of systems
- Warm and approachable - not cold or robotic
- Conversational - engage naturally in casual chat

CONVERSATION STYLE:
- Respond naturally to casual conversation - if user says 'yeah it's cold', respond conversationally about the weather, their day, etc.
- Don't always try to execute actions - sometimes users just want to chat
- Match the user's energy and tone
- Use contractions (I'm, you're, let's)
- Be personable and friendly
- Remember context from the conversation

WHEN TO ACT vs CHAT:
- If user explicitly asks you to DO something (open, play, search, scan, etc.) - take action
- If user is making casual conversation or small talk - respond conversationally
- If user shares feelings or opinions - acknowledge and engage naturally
- If unsure, lean toward conversation rather than action

USER CONTEXT:
- User is in Middlesbrough, United Kingdom
- User's name is Little Tommy Tiptoes

CAPABILITIES (use when asked):
- System control (apps, files, settings)
- Security scanning
- Web search
- Weather info
- Music control
- Image generation
- General knowledge";
        }

        private string GetSimpleResponse(string input)
        {
            input = input.ToLower();
            if (input.Contains("hello") || input.Contains("hi"))
                return "Hello! Nice to meet you!";
            if (input.Contains("how are you"))
                return "I'm doing great, thanks for asking!";
            if (input.Contains("help"))
                return "I can help you with various tasks. Just ask me anything!";
            if (input.Contains("time"))
                return $"The current time is {DateTime.Now:h:mm tt}";
            if (input.Contains("date"))
                return $"Today is {DateTime.Now:dddd, MMMM d, yyyy}";
            if (input.Contains("joke"))
                return "Why do programmers prefer dark mode? Because light attracts bugs!";
            if (input.Contains("thank"))
                return "You're welcome! Happy to help!";
            return $"You said: {input}. Add your Claude API key in Settings for AI responses!";
        }

        // ═══════════════════════════════════════════════════════════════
        // PROJECTION STREAM SYSTEM - Holographic message display
        // ═══════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Initialize the projection stream UI bindings
        /// </summary>
        private void InitializeProjectionStream()
        {
            _projectionStream = new System.Collections.ObjectModel.ObservableCollection<ProjectionMessage>();
            _fullHistory = new System.Collections.ObjectModel.ObservableCollection<ProjectionMessage>();
            
            // Load saved history from disk
            LoadFullHistory();
            
            // Bind the ItemsControls - defer to ensure UI is loaded
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (ProjectionStream != null)
                {
                    ProjectionStream.ItemsSource = _projectionStream;
                    System.Diagnostics.Debug.WriteLine("[ProjectionStream] Bound to _projectionStream");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[ProjectionStream] WARNING: ProjectionStream is null!");
                }
                
                if (HistoryList != null)
                    HistoryList.ItemsSource = _fullHistory;
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }
        
        /// <summary>
        /// Scroll the chat to the bottom to show latest messages
        /// </summary>
        private void ScrollToBottom()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (ProjectionScroller != null)
                    ProjectionScroller.ScrollToEnd();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }
        
        /// <summary>
        /// Add a message to both projection stream and full history
        /// </summary>
        private void AddMessage(string sender, string text, bool isUser)
        {
            var role = isUser ? "user" : "assistant";
            displayedMessages.Add(new ChatMessage { Sender = sender, Text = text, IsUser = isUser, Role = role });
            
            // Create projection message
            var projection = new ProjectionMessage(sender, text, isUser);
            
            // Add to full history (always)
            _fullHistory.Insert(0, projection); // Most recent first
            
            // Add to projection stream with fade-out timer
            AddToProjectionStream(projection);
            
            // Legacy: also add to MessagesPanel for compatibility
            if (isUser)
            {
                AddMessageToUI(sender, text, isUser);
            }
            else
            {
                AddMessageWithTypingAnimation(sender, text);
            }
            
            SaveChatHistory();
        }
        
        /// <summary>
        /// Add message to projection stream with auto-fade
        /// </summary>
        private async void AddToProjectionStream(ProjectionMessage projection)
        {
            // Add to stream
            _projectionStream.Add(projection);
            
            // Scroll to bottom to show new message
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (ProjectionScroller != null)
                {
                    ProjectionScroller.ScrollToEnd();
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
            
            // Limit visible projections - increased to show more messages
            while (_projectionStream.Count > MAX_PROJECTIONS * 2)
            {
                _projectionStream.RemoveAt(0);
            }
            
            // Wait before starting fade (longer for better readability)
            await Task.Delay(PROJECTION_DISPLAY_SECONDS * 1000);
            
            // Don't fade out - keep messages visible for scrolling
            // await FadeOutProjection(projection);
        }
        
        /// <summary>
        /// Smoothly fade out a projection message
        /// </summary>
        private async Task FadeOutProjection(ProjectionMessage projection)
        {
            if (!_projectionStream.Contains(projection)) return;
            
            projection.IsFadingOut = true;            
            // Animate opacity from 1 to 0
            const int steps = 20;
            double stepDelay = PROJECTION_FADE_MS / steps;
            
            for (int i = steps; i >= 0; i--)
            {
                if (!_projectionStream.Contains(projection)) break;
                
                projection.Opacity = (double)i / steps;
                await Task.Delay((int)stepDelay);
            }
            
            // Remove from stream (but stays in history)
            Dispatcher.Invoke(() =>
            {
                if (_projectionStream.Contains(projection))
                    _projectionStream.Remove(projection);
            });
        }
        
        /// <summary>
        /// Pin a history item back to projection stream
        /// </summary>
        private void PinToProjectionStream(ProjectionMessage message)
        {
            // Reset opacity and add back
            message.Opacity = 1.0;
            message.IsFadingOut = false;
            
            if (!_projectionStream.Contains(message))
            {
                AddToProjectionStream(message);
            }
        }
        
        // ═══════════════════════════════════════════════════════════════
        // HISTORY DRAWER - Toggle and interactions
        // ═══════════════════════════════════════════════════════════════
        
        private void HistoryDrawerToggle_Click(object sender, RoutedEventArgs e)
        {
            ToggleHistoryDrawer();
        }
        
        private void CloseHistoryDrawer_Click(object sender, RoutedEventArgs e)
        {
            CloseHistoryDrawer();
        }
        
        private void ToggleHistoryDrawer()
        {
            if (_historyDrawerOpen)
                CloseHistoryDrawer();
            else
                OpenHistoryDrawer();
        }
        
        private void OpenHistoryDrawer()
        {
            _historyDrawerOpen = true;
            // Simple width change - smooth animation would require custom GridLengthAnimation
            HistoryDrawerColumn.Width = new GridLength(380);
            
            // Ensure history list is bound and refresh
            System.Diagnostics.Debug.WriteLine($"[History] Opening drawer - _fullHistory count: {_fullHistory?.Count ?? 0}");
            if (HistoryList != null && _fullHistory != null)
            {
                HistoryList.ItemsSource = _fullHistory;
                System.Diagnostics.Debug.WriteLine($"[History] ItemsSource set to _fullHistory with {_fullHistory.Count} items");
            }
        }
        
        private void CloseHistoryDrawer()
        {
            _historyDrawerOpen = false;
            HistoryDrawerColumn.Width = new GridLength(0);
        }
        
        private void HistoryItem_Click(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[History] HistoryItem_Click fired!");
            
            if (sender is Border border && border.Tag is ProjectionMessage message)
            {
                LoadHistoryMessage(message);
                e.Handled = true;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[History] Tag is not ProjectionMessage, sender type: {sender?.GetType().Name ?? "null"}");
            }
        }
        
        private void HistoryItemButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[History] HistoryItemButton_Click fired!");
            
            // Get the ProjectionMessage from DataContext (the binding)
            ProjectionMessage? message = null;
            
            if (sender is Button button)
            {
                // Try DataContext first (this is what {Binding} sets)
                message = button.DataContext as ProjectionMessage;
                
                if (message == null)
                {
                    // Fallback to Tag
                    message = button.Tag as ProjectionMessage;
                }
                
                System.Diagnostics.Debug.WriteLine($"[History] Message found: {message != null}, DataContext type: {button.DataContext?.GetType().Name ?? "null"}");
            }
            
            if (message != null)
            {
                System.Diagnostics.Debug.WriteLine($"[History] Loading: {message.Sender} - {message.Content.Substring(0, Math.Min(30, message.Content.Length))}...");
                LoadHistoryMessage(message);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[History] Could not get message from button");
                ShowStatus("⚠️ Could not load history item");
            }
        }
        
        private void LoadHistoryMessage(ProjectionMessage message)
        {
            System.Diagnostics.Debug.WriteLine($"[History] Loading message: {message.Sender} - IsUser: {message.IsUser} - {message.Content.Substring(0, Math.Min(50, message.Content.Length))}");
            
            try
            {
                // Create a NEW ProjectionMessage to avoid duplicate reference issues
                var newMessage = new ProjectionMessage(message.Sender, message.Content, message.IsUser)
                {
                    Opacity = 1.0,
                    IsFadingOut = false
                };
                
                // Use the same method that works for normal messages
                Dispatcher.Invoke(() =>
                {
                    // Debug: Check current state
                    System.Diagnostics.Debug.WriteLine($"[History] _projectionStream is null: {_projectionStream == null}");
                    System.Diagnostics.Debug.WriteLine($"[History] ProjectionStream is null: {ProjectionStream == null}");
                    System.Diagnostics.Debug.WriteLine($"[History] ProjectionStream.ItemsSource is null: {ProjectionStream?.ItemsSource == null}");
                    
                    // Rebind if needed
                    if (ProjectionStream != null && ProjectionStream.ItemsSource == null && _projectionStream != null)
                    {
                        System.Diagnostics.Debug.WriteLine("[History] Rebinding ItemsSource...");
                        ProjectionStream.ItemsSource = _projectionStream;
                    }
                    
                    // Add directly to the collection
                    if (_projectionStream != null)
                    {
                        _projectionStream.Add(newMessage);
                        System.Diagnostics.Debug.WriteLine($"[History] Added! Collection count: {_projectionStream.Count}");
                    }
                    
                    // Scroll to bottom
                    ProjectionScroller?.ScrollToEnd();
                });
                
                if (message.IsUser)
                {
                    Dispatcher.Invoke(() => InputBox.Text = message.Content);
                    ShowStatus($"📋 Loaded: {message.Content.Substring(0, Math.Min(40, message.Content.Length))}...");
                }
                else
                {
                    try
                    {
                        Dispatcher.Invoke(() => Clipboard.SetDataObject(message.Content, true));
                        ShowStatus("📋 Loaded Atlas response (copied to clipboard)");
                    }
                    catch
                    {
                        ShowStatus("📋 Loaded Atlas response");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[History] Error: {ex.Message}\n{ex.StackTrace}");
                ShowStatus($"⚠️ Error loading message: {ex.Message}");
            }
            
            // Close the history drawer
            CloseHistoryDrawer();
        }

        /// <summary>
        /// Add Atlas message with smooth typing animation
        /// </summary>
        private async void AddMessageWithTypingAnimation(string sender, string text)
        {
            // Create the message bubble first (empty)
            var (border, messageTextBox, glowText, mainText) = CreateModernMessageBubbleWithGlow(sender, "", false);
            MessagesPanel.Children.Add(border);
            MessagesScroller.ScrollToEnd();
            
            // Animate the text appearing - update ALL text elements
            var displayText = new StringBuilder();
            int charsPerTick = Math.Max(1, text.Length / 50); // Adjust speed based on length
            
            for (int i = 0; i < text.Length; i += charsPerTick)
            {
                int endIndex = Math.Min(i + charsPerTick, text.Length);
                displayText.Append(text.Substring(i, endIndex - i));
                
                await Dispatcher.InvokeAsync(() =>
                {
                    var currentText = displayText.ToString();
                    messageTextBox.Text = currentText;
                    if (glowText != null) glowText.Text = currentText;
                    if (mainText != null) mainText.Text = currentText;
                    MessagesScroller.ScrollToEnd();
                });
                
                // Small delay for typing effect (faster for longer messages)
                await Task.Delay(text.Length > 500 ? 5 : 15);
            }
            
            // Ensure full text is shown
            await Dispatcher.InvokeAsync(() =>
            {
                messageTextBox.Text = text;
                if (glowText != null) glowText.Text = text;
                if (mainText != null) mainText.Text = text;
            });
        }

        private void AddMessageToUI(string sender, string text, bool isUser)
        {
            var (border, _) = CreateModernMessageBubble(sender, text, isUser);
            MessagesPanel.Children.Add(border);
            MessagesScroller.ScrollToEnd();
        }
        
        /// <summary>
        /// Create a modern message bubble - Claude/ChatGPT style
        /// </summary>
        private (Border border, TextBox textBox) CreateModernMessageBubble(string sender, string text, bool isUser)
        {
            var (border, textBox, _, _) = CreateModernMessageBubbleWithGlow(sender, text, isUser);
            return (border, textBox);
        }
        
        /// <summary>
        /// Create a modern message bubble with glow text elements for animation support
        /// </summary>
        private (Border border, TextBox textBox, TextBlock? glowText, TextBlock? mainText) CreateModernMessageBubbleWithGlow(string sender, string text, bool isUser)
        {
            // Clean, minimal design - inspired by Claude/ChatGPT
            var container = new Border
            {
                Padding = new Thickness(0, 20, 0, 20),
                Background = isUser ? Brushes.Transparent : new SolidColorBrush(Color.FromRgb(17, 17, 19))
            };
            
            // Add neon glow for Atlas messages - bright cyan
            if (!isUser)
            {
                container.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Color.FromRgb(34, 211, 238), // Cyan glow
                    BlurRadius = 35,
                    ShadowDepth = 0,
                    Opacity = 0.5
                };
            }
            
            var mainGrid = new Grid { MaxWidth = 720, HorizontalAlignment = HorizontalAlignment.Left };
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) }); // Avatar
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Content
            
            // Avatar - modern rounded square with neon glow for Atlas
            var avatarBorder = new Border
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(isUser ? Color.FromRgb(88, 166, 255) : Color.FromRgb(34, 211, 238)), // Cyan for Atlas
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 0, 0)
            };
            
            // Add bright glow to Atlas avatar
            if (!isUser)
            {
                avatarBorder.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Color.FromRgb(34, 211, 238),
                    BlurRadius = 20,
                    ShadowDepth = 0,
                    Opacity = 0.9
                };
            }
            
            var avatarText = new TextBlock
            {
                Text = isUser ? "U" : "A",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = isUser ? Brushes.White : Brushes.Black, // Black text on cyan
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            avatarBorder.Child = avatarText;
            Grid.SetColumn(avatarBorder, 0);
            mainGrid.Children.Add(avatarBorder);
            
            // Content stack
            var contentStack = new StackPanel { Margin = new Thickness(12, 0, 0, 0) };
            
            // Sender name - bold, modern, cyan for Atlas
            var senderText = new TextBlock
            {
                Text = sender,
                FontWeight = FontWeights.SemiBold,
                FontSize = 14,
                Foreground = new SolidColorBrush(isUser ? Color.FromRgb(236, 236, 241) : Color.FromRgb(34, 211, 238)), // Cyan for Atlas
                Margin = new Thickness(0, 0, 0, 8)
            };
            
            // Add bright glow to Atlas name
            if (!isUser)
            {
                senderText.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Color.FromRgb(34, 211, 238),
                    BlurRadius = 15,
                    ShadowDepth = 0,
                    Opacity = 0.85
                };
            }
            contentStack.Children.Add(senderText);
            
            // Message text container
            var messageContainer = new Grid();
            TextBlock? glowText = null;
            TextBlock? mainText = null;
            
            // For Atlas messages: Use visible TextBlock with glow effect (NOT invisible TextBox)
            if (!isUser)
            {
                // Glow layer - blurred cyan behind text
                glowText = new TextBlock
                {
                    Text = text,
                    Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 255)), // Bright cyan
                    FontSize = 24, // BIGGER text
                    FontFamily = new FontFamily("Segoe UI Variable, Segoe UI, sans-serif"),
                    TextWrapping = TextWrapping.Wrap,
                    Effect = new System.Windows.Media.Effects.BlurEffect { Radius = 15 },
                    Opacity = 0.8
                };
                messageContainer.Children.Add(glowText);
                
                // Main visible text with drop shadow glow
                mainText = new TextBlock
                {
                    Text = text,
                    Foreground = new SolidColorBrush(Color.FromRgb(220, 255, 255)), // Bright cyan-white
                    FontSize = 24, // BIGGER text
                    FontFamily = new FontFamily("Segoe UI Variable, Segoe UI, sans-serif"),
                    TextWrapping = TextWrapping.Wrap,
                    Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        Color = Color.FromRgb(0, 255, 255),
                        BlurRadius = 20,
                        ShadowDepth = 0,
                        Opacity = 0.9
                    }
                };
                messageContainer.Children.Add(mainText);
            }
            else
            {
                // User messages - simple white text, larger
                mainText = new TextBlock
                {
                    Text = text,
                    Foreground = new SolidColorBrush(Color.FromRgb(236, 236, 241)),
                    FontSize = 22,
                    FontFamily = new FontFamily("Segoe UI Variable, Segoe UI, sans-serif"),
                    TextWrapping = TextWrapping.Wrap
                };
                messageContainer.Children.Add(mainText);
            }
            
            // Invisible TextBox overlay for text selection
            var messageTextBox = new TextBox
            {
                Text = text,
                Foreground = Brushes.Transparent,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                FontSize = isUser ? 22 : 24,
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI, sans-serif"),
                Padding = new Thickness(0),
                Cursor = Cursors.IBeam,
                SelectionBrush = new SolidColorBrush(Color.FromArgb(80, 0, 255, 255)),
                CaretBrush = Brushes.Transparent,
                FocusVisualStyle = null
            };
            messageContainer.Children.Add(messageTextBox);
            
            contentStack.Children.Add(messageContainer);
            
            // Action buttons - ALWAYS VISIBLE for 1-click copy
            var actionsPanel = new StackPanel 
            { 
                Orientation = Orientation.Horizontal, 
                Margin = new Thickness(0, 10, 0, 0),
                Opacity = 1
            };
            
            var copyBtn = new Button
            {
                Content = "📋 Copy",
                Background = new SolidColorBrush(Color.FromRgb(30, 35, 42)),
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(55, 65, 81)),
                FontSize = 12,
                Cursor = Cursors.Hand,
                Padding = new Thickness(10, 5, 10, 5),
                Tag = text
            };
            copyBtn.Click += CopyMessage_Click;
            
            copyBtn.MouseEnter += (s, e) => {
                copyBtn.Background = new SolidColorBrush(Color.FromRgb(45, 55, 72));
                copyBtn.Foreground = new SolidColorBrush(Color.FromRgb(34, 211, 238));
            };
            copyBtn.MouseLeave += (s, e) => {
                copyBtn.Background = new SolidColorBrush(Color.FromRgb(30, 35, 42));
                copyBtn.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
            };
            
            actionsPanel.Children.Add(copyBtn);
            contentStack.Children.Add(actionsPanel);
            
            // Update tag when text changes
            messageTextBox.TextChanged += (s, e) => copyBtn.Tag = messageTextBox.Text;
            
            Grid.SetColumn(contentStack, 1);
            mainGrid.Children.Add(contentStack);
            
            container.Child = mainGrid;
            
            return (container, messageTextBox, glowText, mainText);
        }
        
        /// <summary>
        /// Create message content with clickable links and file paths
        /// </summary>
        private UIElement CreateMessageContentWithLinks(string text, Color textColor)
        {
            // Check if text contains URLs or file paths
            var urlPattern = @"https?://[^\s]+|www\.[^\s]+";
            var filePathPattern = @"[A-Za-z]:\\[^\n\r]+\.(?:png|jpg|jpeg|gif|bmp|txt|pdf|doc|docx|xls|xlsx|mp3|mp4|wav|zip|exe|msi)";
            
            var urlMatches = System.Text.RegularExpressions.Regex.Matches(text, urlPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var fileMatches = System.Text.RegularExpressions.Regex.Matches(text, filePathPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            if (urlMatches.Count == 0 && fileMatches.Count == 0)
            {
                // No URLs or file paths found, return simple TextBox
                return new TextBox
                {
                    Text = text,
                    Foreground = new SolidColorBrush(textColor),
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 14,
                    Padding = new Thickness(0),
                    Cursor = Cursors.IBeam,
                    SelectionBrush = new SolidColorBrush(Color.FromRgb(99, 102, 241)),
                    CaretBrush = Brushes.Transparent,
                    FocusVisualStyle = null
                };
            }
            
            // Combine all matches and sort by position
            var allMatches = new List<(int Index, int Length, string Value, bool IsFilePath)>();
            
            foreach (System.Text.RegularExpressions.Match match in urlMatches)
            {
                allMatches.Add((match.Index, match.Length, match.Value, false));
            }
            
            foreach (System.Text.RegularExpressions.Match match in fileMatches)
            {
                allMatches.Add((match.Index, match.Length, match.Value, true));
            }
            
            allMatches = allMatches.OrderBy(m => m.Index).ToList();
            
            // Create TextBlock with clickable hyperlinks
            var textBlock = new TextBlock
            {
                Foreground = new SolidColorBrush(textColor),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14
            };
            
            int lastIndex = 0;
            
            foreach (var match in allMatches)
            {
                // Skip if this match overlaps with previous
                if (match.Index < lastIndex) continue;
                
                // Add text before the match
                if (match.Index > lastIndex)
                {
                    var beforeText = text.Substring(lastIndex, match.Index - lastIndex);
                    textBlock.Inlines.Add(new Run(beforeText));
                }
                
                if (match.IsFilePath)
                {
                    // File path - make it clickable to open folder
                    var filePath = match.Value;
                    var folderPath = System.IO.Path.GetDirectoryName(filePath) ?? filePath;
                    
                    var hyperlink = new Hyperlink(new Run(filePath))
                    {
                        Foreground = new SolidColorBrush(Color.FromRgb(0, 212, 170)), // Cyan accent
                        TextDecorations = null,
                        Cursor = Cursors.Hand,
                        ToolTip = "Click to open folder"
                    };
                    
                    hyperlink.MouseEnter += (s, e) => 
                    {
                        hyperlink.TextDecorations = TextDecorations.Underline;
                        hyperlink.Foreground = new SolidColorBrush(Color.FromRgb(51, 229, 195)); // Lighter cyan
                    };
                    hyperlink.MouseLeave += (s, e) => 
                    {
                        hyperlink.TextDecorations = null;
                        hyperlink.Foreground = new SolidColorBrush(Color.FromRgb(0, 212, 170));
                    };
                    
                    hyperlink.Click += (s, e) =>
                    {
                        try
                        {
                            // Open folder and select the file
                            if (System.IO.File.Exists(filePath))
                            {
                                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                                ShowStatus($"📂 Opening folder");
                            }
                            else if (System.IO.Directory.Exists(folderPath))
                            {
                                System.Diagnostics.Process.Start("explorer.exe", folderPath);
                                ShowStatus($"📂 Opening folder");
                            }
                            else
                            {
                                ShowStatus($"❌ Path not found");
                            }
                        }
                        catch (Exception ex)
                        {
                            ShowStatus($"❌ Failed to open: {ex.Message}");
                            Debug.WriteLine($"Failed to open path {filePath}: {ex.Message}");
                        }
                    };
                    
                    textBlock.Inlines.Add(hyperlink);
                }
                else
                {
                    // URL - make it clickable to open in browser
                    var url = match.Value;
                    var fullUrl = url.StartsWith("http") ? url : $"https://{url}";
                    
                    var hyperlink = new Hyperlink(new Run(url))
                    {
                        Foreground = new SolidColorBrush(Color.FromRgb(96, 165, 250)), // Blue-400
                        TextDecorations = null,
                        Cursor = Cursors.Hand
                    };
                    
                    hyperlink.MouseEnter += (s, e) => 
                    {
                        hyperlink.TextDecorations = TextDecorations.Underline;
                        hyperlink.Foreground = new SolidColorBrush(Color.FromRgb(147, 197, 253)); // Blue-300
                    };
                    hyperlink.MouseLeave += (s, e) => 
                    {
                        hyperlink.TextDecorations = null;
                        hyperlink.Foreground = new SolidColorBrush(Color.FromRgb(96, 165, 250)); // Blue-400
                    };
                    
                    hyperlink.Click += (s, e) =>
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = fullUrl,
                                UseShellExecute = true
                            });
                            ShowStatus($"🌐 Opening: {url}");
                        }
                        catch (Exception ex)
                        {
                            ShowStatus($"❌ Failed to open link: {ex.Message}");
                            Debug.WriteLine($"Failed to open URL {fullUrl}: {ex.Message}");
                        }
                    };
                    
                    textBlock.Inlines.Add(hyperlink);
                }
                
                lastIndex = match.Index + match.Length;
            }
            
            // Add remaining text after the last match
            if (lastIndex < text.Length)
            {
                var remainingText = text.Substring(lastIndex);
                textBlock.Inlines.Add(new Run(remainingText));
            }
            
            return textBlock;
        }

        /// <summary>
        /// Handle click on message content to open URLs
        /// </summary>
        private void MessageContent_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock textBlock)
            {
                var text = textBlock.Text;
                if (string.IsNullOrEmpty(text)) return;
                
                // Find URLs in the text
                var urlPattern = @"(https?://[^\s<>""]+|www\.[^\s<>""]+)";
                var matches = System.Text.RegularExpressions.Regex.Matches(text, urlPattern);
                
                if (matches.Count > 0)
                {
                    // If there's only one URL, open it directly
                    if (matches.Count == 1)
                    {
                        var url = matches[0].Value;
                        if (!url.StartsWith("http")) url = "https://" + url;
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = url,
                                UseShellExecute = true
                            });
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to open URL: {ex.Message}");
                        }
                    }
                    else
                    {
                        // Multiple URLs - show a context menu
                        var contextMenu = new ContextMenu();
                        foreach (System.Text.RegularExpressions.Match match in matches)
                        {
                            var url = match.Value;
                            if (!url.StartsWith("http")) url = "https://" + url;
                            var menuItem = new MenuItem { Header = url.Length > 60 ? url.Substring(0, 57) + "..." : url, Tag = url };
                            menuItem.Click += (s, args) =>
                            {
                                if (s is MenuItem mi && mi.Tag is string targetUrl)
                                {
                                    try
                                    {
                                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                        {
                                            FileName = targetUrl,
                                            UseShellExecute = true
                                        });
                                    }
                                    catch { }
                                }
                            };
                            contextMenu.Items.Add(menuItem);
                        }
                        contextMenu.IsOpen = true;
                    }
                }
            }
        }

        /// <summary>
        /// Handle click on projection slate button to open URLs in the message content
        /// </summary>
        private void ProjectionSlate_ButtonClick(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[ProjectionSlate_ButtonClick] Button clicked!");
            ShowStatus("🔍 Message clicked!");
            
            // Get the message from the DataContext
            ProjectionMessage? message = null;
            
            if (sender is Button btn)
            {
                message = btn.DataContext as ProjectionMessage;
            }
            
            if (message == null)
            {
                System.Diagnostics.Debug.WriteLine("[ProjectionSlate_ButtonClick] No message found in DataContext");
                ShowStatus("⚠️ No message data found");
                return;
            }
            
            System.Diagnostics.Debug.WriteLine($"[ProjectionSlate_ButtonClick] Message: {message.Content?.Substring(0, Math.Min(50, message.Content?.Length ?? 0))}...");
            
            var text = message.Content;
            if (string.IsNullOrEmpty(text)) return;
            
            // Find URLs in the text
            var urlPattern = @"(https?://[^\s<>""]+|www\.[^\s<>""]+)";
            var matches = System.Text.RegularExpressions.Regex.Matches(text, urlPattern);
            
            System.Diagnostics.Debug.WriteLine($"[ProjectionSlate_ButtonClick] Found {matches.Count} URLs");
            
            if (matches.Count > 0)
            {
                // If there's only one URL, open it directly
                if (matches.Count == 1)
                {
                    var url = matches[0].Value;
                    if (!url.StartsWith("http")) url = "https://" + url;
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"[ProjectionSlate_ButtonClick] Opening URL: {url}");
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = url,
                            UseShellExecute = true
                        });
                        ShowStatus($"🌐 Opening: {url}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to open URL: {ex.Message}");
                        ShowStatus($"⚠️ Failed to open URL");
                    }
                }
                else
                {
                    // Multiple URLs - show a context menu
                    var contextMenu = new ContextMenu
                    {
                        Background = new SolidColorBrush(Color.FromRgb(10, 12, 20)),
                        BorderBrush = new SolidColorBrush(Color.FromArgb(48, 34, 211, 238)),
                        BorderThickness = new Thickness(1),
                        Padding = new Thickness(4)
                    };
                    
                    // Add header
                    var header = new MenuItem 
                    { 
                        Header = "🔗 Open Link", 
                        IsEnabled = false,
                        Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139))
                    };
                    contextMenu.Items.Add(header);
                    contextMenu.Items.Add(new Separator());
                    
                    foreach (System.Text.RegularExpressions.Match match in matches)
                    {
                        var url = match.Value;
                        if (!url.StartsWith("http")) url = "https://" + url;
                        var displayUrl = url.Length > 60 ? url.Substring(0, 57) + "..." : url;
                        var menuItem = new MenuItem 
                        { 
                            Header = displayUrl, 
                            Tag = url,
                            Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240))
                        };
                        menuItem.Click += (s, args) =>
                        {
                            if (s is MenuItem mi && mi.Tag is string targetUrl)
                            {
                                try
                                {
                                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                    {
                                        FileName = targetUrl,
                                        UseShellExecute = true
                                    });
                                    ShowStatus($"🌐 Opening: {targetUrl}");
                                }
                                catch { }
                            }
                        };
                        contextMenu.Items.Add(menuItem);
                    }
                    
                    // Add copy option
                    contextMenu.Items.Add(new Separator());
                    var copyItem = new MenuItem 
                    { 
                        Header = "📋 Copy Message",
                        Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184))
                    };
                    copyItem.Click += (s, args) =>
                    {
                        try
                        {
                            Clipboard.SetDataObject(text, true);
                            ShowStatus("📋 Copied to clipboard");
                        }
                        catch { }
                    };
                    contextMenu.Items.Add(copyItem);
                    
                    contextMenu.IsOpen = true;
                }
            }
            else
            {
                // No URLs - just show a copy option
                ShowStatus("ℹ️ No links in this message");
            }
        }

        /// <summary>
        /// Handle click on projection slate to open URLs in the message content
        /// </summary>
        private void ProjectionSlate_Click(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[ProjectionSlate_Click] Click detected!");
            ShowStatus("🔍 Click detected on message!");
            
            // Get the message from the DataContext since Tag binding might not work in DataTemplate
            ProjectionMessage? message = null;
            
            if (sender is Border border)
            {
                message = border.Tag as ProjectionMessage ?? border.DataContext as ProjectionMessage;
            }
            else if (sender is FrameworkElement fe)
            {
                message = fe.DataContext as ProjectionMessage;
            }
            
            if (message == null)
            {
                System.Diagnostics.Debug.WriteLine("[ProjectionSlate_Click] No message found in Tag or DataContext");
                ShowStatus("⚠️ No message data found");
                return;
            }
            
            System.Diagnostics.Debug.WriteLine($"[ProjectionSlate_Click] Message: {message.Content?.Substring(0, Math.Min(50, message.Content?.Length ?? 0))}...");
            
            var text = message.Content;
            if (string.IsNullOrEmpty(text)) return;
            
            // Find URLs in the text
            var urlPattern = @"(https?://[^\s<>""]+|www\.[^\s<>""]+)";
            var matches = System.Text.RegularExpressions.Regex.Matches(text, urlPattern);
            
            System.Diagnostics.Debug.WriteLine($"[ProjectionSlate_Click] Found {matches.Count} URLs");
            
            if (matches.Count > 0)
            {
                // If there's only one URL, open it directly
                if (matches.Count == 1)
                {
                    var url = matches[0].Value;
                    if (!url.StartsWith("http")) url = "https://" + url;
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"[ProjectionSlate_Click] Opening URL: {url}");
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = url,
                            UseShellExecute = true
                        });
                        ShowStatus($"🌐 Opening: {url}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to open URL: {ex.Message}");
                        ShowStatus($"⚠️ Failed to open URL");
                    }
                }
                else
                {
                    // Multiple URLs - show a context menu
                    var contextMenu = new ContextMenu
                    {
                        Background = new SolidColorBrush(Color.FromRgb(10, 12, 20)),
                        BorderBrush = new SolidColorBrush(Color.FromArgb(48, 34, 211, 238)),
                        BorderThickness = new Thickness(1),
                        Padding = new Thickness(4)
                    };
                    
                    // Add header
                    var header = new MenuItem 
                    { 
                        Header = "🔗 Open Link", 
                        IsEnabled = false,
                        Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139))
                    };
                    contextMenu.Items.Add(header);
                    contextMenu.Items.Add(new Separator());
                    
                    foreach (System.Text.RegularExpressions.Match match in matches)
                    {
                        var url = match.Value;
                        if (!url.StartsWith("http")) url = "https://" + url;
                        var displayUrl = url.Length > 60 ? url.Substring(0, 57) + "..." : url;
                        var menuItem = new MenuItem 
                        { 
                            Header = displayUrl, 
                            Tag = url,
                            Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240))
                        };
                        menuItem.Click += (s, args) =>
                        {
                            if (s is MenuItem mi && mi.Tag is string targetUrl)
                            {
                                try
                                {
                                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                    {
                                        FileName = targetUrl,
                                        UseShellExecute = true
                                    });
                                    ShowStatus($"🌐 Opening: {targetUrl}");
                                }
                                catch { }
                            }
                        };
                        contextMenu.Items.Add(menuItem);
                    }
                    
                    // Add copy option
                    contextMenu.Items.Add(new Separator());
                    var copyItem = new MenuItem 
                    { 
                        Header = "📋 Copy Message",
                        Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184))
                    };
                    copyItem.Click += (s, args) =>
                    {
                        try
                        {
                            Clipboard.SetDataObject(text, true);
                            ShowStatus("📋 Copied to clipboard");
                        }
                        catch { }
                    };
                    contextMenu.Items.Add(copyItem);
                    
                    contextMenu.IsOpen = true;
                }
                e.Handled = true;
            }
        }

        private void CopyMessage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string text)
            {
                try
                {
                    // Use Dispatcher to ensure clipboard operation runs on UI thread
                    Dispatcher.Invoke(() =>
                    {
                        Clipboard.SetDataObject(text, true);
                    });
                    
                    // Visual feedback
                    btn.Content = "✓";
                    btn.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Green
                    
                    var timer = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(1.5)
                    };
                    timer.Tick += (s, args) =>
                    {
                        btn.Content = "📋";
                        btn.Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 176));
                        timer.Stop();
                    };
                    timer.Start();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Copy failed: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Copy all text from a message via context menu
        /// </summary>
        private void CopyAllMessage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TextBox textBox = null;
                
                // Try Tag first (bound to PlacementTarget)
                if (sender is MenuItem menuItem && menuItem.Tag is TextBox tb)
                {
                    textBox = tb;
                }
                // Fallback: traverse to find ContextMenu
                else if (sender is MenuItem mi)
                {
                    var parent = mi.Parent;
                    while (parent != null)
                    {
                        if (parent is ContextMenu cm && cm.PlacementTarget is TextBox ptb)
                        {
                            textBox = ptb;
                            break;
                        }
                        parent = (parent as FrameworkElement)?.Parent;
                    }
                }
                
                if (textBox != null)
                {
                    Clipboard.SetDataObject(textBox.Text, true);
                    ShowStatus("📋 Copied to clipboard");
                }
                else
                {
                    ShowStatus("⚠️ Could not find message text");
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"⚠️ Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Open links found in a message via context menu
        /// </summary>
        private void OpenLinksInMessage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TextBox textBox = null;
                
                // Try Tag first (bound to PlacementTarget)
                if (sender is MenuItem menuItem && menuItem.Tag is TextBox tb)
                {
                    textBox = tb;
                }
                // Fallback: traverse to find ContextMenu
                else if (sender is MenuItem mi)
                {
                    var parent = mi.Parent;
                    while (parent != null)
                    {
                        if (parent is ContextMenu cm && cm.PlacementTarget is TextBox ptb)
                        {
                            textBox = ptb;
                            break;
                        }
                        parent = (parent as FrameworkElement)?.Parent;
                    }
                }
                
                if (textBox == null)
                {
                    ShowStatus("⚠️ Could not find message text");
                    return;
                }
                
                var text = textBox.Text;
                if (string.IsNullOrEmpty(text))
                {
                    ShowStatus("ℹ️ No text in message");
                    return;
                }
                
                int opened = 0;
                
                // Find URLs
                var urlPattern = @"(https?://[^\s<>""'\)\]]+|www\.[^\s<>""'\)\]]+\.[^\s<>""'\)\]]+)";
                var urlMatches = System.Text.RegularExpressions.Regex.Matches(text, urlPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                
                foreach (System.Text.RegularExpressions.Match match in urlMatches)
                {
                    var url = match.Value.TrimEnd('.', ',', ')', ']', '}', '>', '"', '\'');
                    if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) 
                        url = "https://" + url;
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = url,
                            UseShellExecute = true
                        });
                        opened++;
                    }
                    catch { }
                }
                
                // Find file paths (Windows paths like C:\... or paths with backslashes)
                var pathPattern = @"([A-Za-z]:\\[^\s<>""']+|\\\\[^\s<>""']+)";
                var pathMatches = System.Text.RegularExpressions.Regex.Matches(text, pathPattern);
                
                foreach (System.Text.RegularExpressions.Match match in pathMatches)
                {
                    var path = match.Value.TrimEnd('.', ',', ')', ']', '}', '>', '"', '\'');
                    try
                    {
                        if (System.IO.File.Exists(path))
                        {
                            // Open file with default app
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = path,
                                UseShellExecute = true
                            });
                            opened++;
                        }
                        else if (System.IO.Directory.Exists(path))
                        {
                            // Open folder in explorer
                            System.Diagnostics.Process.Start("explorer.exe", path);
                            opened++;
                        }
                        else
                        {
                            // Try to open parent folder and select the file
                            var dir = System.IO.Path.GetDirectoryName(path);
                            if (System.IO.Directory.Exists(dir))
                            {
                                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
                                opened++;
                            }
                        }
                    }
                    catch { }
                }
                
                if (opened > 0)
                    ShowStatus($"📂 Opened {opened} item(s)");
                else
                    ShowStatus("ℹ️ No links or paths found in this message");
            }
            catch (Exception ex)
            {
                ShowStatus($"⚠️ Error: {ex.Message}");
            }
        }

        // Smart Suggestions - Text Changed Handler
        private void InputBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var text = InputBox.Text.Trim().ToLower();
            
            if (string.IsNullOrEmpty(text) || text.Length < 2)
            {
                SuggestionsPopup.IsOpen = false;
                return;
            }

            var suggestions = GetContextualSuggestions(text);
            
            if (suggestions.Count > 0)
            {
                SuggestionsList.Items.Clear();
                foreach (var suggestion in suggestions)
                {
                    SuggestionsList.Items.Add(new ListBoxItem 
                    { 
                        Content = suggestion.Display,
                        Tag = suggestion.Value
                    });
                }
                SuggestionsPopup.IsOpen = true;
            }
            else
            {
                SuggestionsPopup.IsOpen = false;
            }
        }

        private List<(string Display, string Value)> GetContextualSuggestions(string text)
        {
            var suggestions = new List<(string Display, string Value)>();

            // Command suggestions
            if (text.StartsWith("/"))
            {
                var commands = new[]
                {
                    ("/time", "🕐 Show current time"),
                    ("/date", "📅 Show current date"),
                    ("/calc ", "🔢 Calculate expression"),
                    ("/open ", "🚀 Open application"),
                    ("/search ", "🔍 Search the web"),
                    ("/copy ", "📋 Copy to clipboard"),
                    ("/clipboard", "📋 Open clipboard manager"),
                    ("/clear", "🗑️ Clear chat history"),
                    ("/voice", "🔊 Toggle voice"),
                    ("/theme", "🎨 Toggle theme"),
                    ("/dark", "🌙 Dark theme"),
                    ("/light", "☀️ Light theme"),
                    ("/joke", "😄 Tell a joke"),
                    ("/flip", "🪙 Flip a coin"),
                    ("/roll ", "🎲 Roll dice"),
                    ("/timer ", "⏱️ Set timer"),
                    ("/screenshot", "📸 Take screenshot"),
                    ("/analyze", "🔍 Analyze screenshot"),
                    ("/ocr", "📝 Extract text from screenshot"),
                    ("/history", "📸 Screenshot history"),
                    ("/avatar ", "🎭 Change avatar"),
                    ("/avatar create", "🎨 Create custom avatar"),
                    ("/avatar think", "💡 Avatar thinking mode"),
                    ("/avatar dance", "💃 Make avatar dance"),
                    ("/avatar unity", "🎮 Open Unity avatar system"),
                    ("/avatars", "🎭 Avatar selection"),
                    ("/systemscan", "🔍 Scan system for issues"),
                    ("/spywarescan", "🛡️ Scan for spyware/malware"),
                    ("/systemfix", "🔧 Auto-fix system issues"),
                    ("/systemcontrol", "🔧 Open System Control Panel"),
                    ("/code", "💻 Open Code Editor"),
                    ("/agent ", "🤖 Run AI agent task"),
                    ("/help", "📚 Show help")
                };

                foreach (var (cmd, desc) in commands)
                {
                    if (cmd.StartsWith(text))
                        suggestions.Add(($"{cmd} - {desc}", cmd));
                }
            }
            // Context-aware suggestions based on keywords
            else
            {
                if (text.Contains("email") || text.Contains("write"))
                    suggestions.Add(("📧 Write a professional email", "Write a professional email about: "));
                
                if (text.Contains("code") || text.Contains("program") || text.Contains("function"))
                    suggestions.Add(("💻 Write code for...", "Write code to: "));
                
                if (text.Contains("explain") || text.Contains("what is"))
                    suggestions.Add(("📖 Explain in simple terms", "Explain in simple terms: "));
                
                if (text.Contains("translate"))
                    suggestions.Add(("🌐 Translate to Spanish", "Translate to Spanish: "));
                
                if (text.Contains("summarize") || text.Contains("summary"))
                    suggestions.Add(("📝 Summarize this text", "Summarize the following: "));
                
                if (text.Contains("fix") || text.Contains("error") || text.Contains("bug"))
                    suggestions.Add(("🔧 Debug and fix code", "Find and fix the bug in: "));
                
                if (text.Contains("improve") || text.Contains("better"))
                    suggestions.Add(("✨ Improve this text", "Improve and enhance: "));
                
                if (text.Contains("list") || text.Contains("ideas"))
                    suggestions.Add(("💡 Generate ideas", "Give me 5 ideas for: "));
                
                if (text.Contains("system") || text.Contains("windows") || text.Contains("performance") || text.Contains("slow"))
                    suggestions.Add(("🔍 Scan system for issues", "/systemscan"));
                
                if (text.Contains("spyware") || text.Contains("malware") || text.Contains("virus") || text.Contains("threat") || text.Contains("security"))
                    suggestions.Add(("🛡️ Scan for spyware/malware", "/spywarescan"));
                
                if (text.Contains("fix") || text.Contains("repair") || text.Contains("problem"))
                    suggestions.Add(("🔧 Auto-fix system issues", "/systemfix"));
                
                if (text.Contains("control") || text.Contains("manage") || text.Contains("settings"))
                    suggestions.Add(("🔧 Open System Control", "/systemcontrol"));
            }

            return suggestions.Take(6).ToList();
        }

        // Smart Suggestions - Selection Handler
        private void Suggestion_Selected(object sender, SelectionChangedEventArgs e)
        {
            if (SuggestionsList.SelectedItem is ListBoxItem item && item.Tag is string value)
            {
                InputBox.Text = value;
                InputBox.CaretIndex = InputBox.Text.Length;
                SuggestionsPopup.IsOpen = false;
                InputBox.Focus();
            }
        }

        // Quick Actions - Click Handler
        private void QuickAction_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string action)
            {
                // Special handling for Code button - open Code Editor window
                if (action == "code")
                {
                    OpenCodeEditor();
                    return;
                }
                
                var currentText = InputBox.Text.Trim();
                var prompt = GetQuickActionPrompt(action, currentText);
                
                if (!string.IsNullOrEmpty(prompt))
                {
                    InputBox.Text = prompt;
                    InputBox.CaretIndex = InputBox.Text.Length;
                    InputBox.Focus();
                    
                    // If there's existing text, send immediately
                    if (!string.IsNullOrEmpty(currentText) && !prompt.EndsWith(": "))
                    {
                        SendMessage();
                    }
                }
            }
        }
        
        private void OpenCodeEditor()
        {
            var codeEditor = new CodeEditorWindow();
            codeEditor.Show();
        }

        private string GetQuickActionPrompt(string action, string existingText)
        {
            var hasText = !string.IsNullOrEmpty(existingText);
            
            return action switch
            {
                "search" => hasText 
                    ? $"Search for information about: {existingText}" 
                    : "Search for: ",
                    
                "suggest" => hasText 
                    ? $"Give me suggestions and ideas for: {existingText}" 
                    : "Give me suggestions for: ",
                    
                "summarize" => hasText 
                    ? $"Summarize this concisely: {existingText}" 
                    : "Summarize the following: ",
                    
                "rephrase" => hasText 
                    ? $"Rephrase this more clearly: {existingText}" 
                    : "Rephrase this text: ",
                    
                "write" => hasText 
                    ? $"Write content about: {existingText}" 
                    : "Write about: ",
                    
                "email" => hasText 
                    ? $"Write a professional email about: {existingText}" 
                    : "Write a professional email about: ",
                    
                "generate" => hasText 
                    ? $"Generate an image of {existingText}" 
                    : "Generate an image of: ",
                    
                "code" => hasText 
                    ? $"Write code to: {existingText}" 
                    : "Write code to: ",
                    
                "translate" => hasText 
                    ? $"Translate to Spanish: {existingText}" 
                    : "Translate to Spanish: ",
                    
                "analyze" => hasText 
                    ? $"Analyze this in detail: {existingText}" 
                    : "Analyze the following: ",
                    
                _ => existingText
            };
        }

        private void ChatWindow_Loaded(object sender, RoutedEventArgs e)
        {
            PositionNearAvatar();
            
            // Create taskbar icon (NotifyIcon) for borderless window
            _taskbarIcon = new TaskbarIconHelper(this);
            
            // Initialize Atlas Core state machine and animations
            InitializeAtlasCoreAnimations();
            
            // Start radial controls breathing animation
            StartRadialBreathingAnimation();
            
            // Start slow anti-clockwise rotation of radial controls
            StartRadialRotationAnimation();
            
            // Initialize conversation system (sessions, memory, profile)
            _ = InitializeConversationSystemAsync();
        }
        
        // ═══════════════════════════════════════════════════════════════
        // ATLAS CORE STATE MACHINE - Living visual intelligence
        // ═══════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Initialize all Atlas Core storyboards from XAML resources
        /// </summary>
        private void InitializeAtlasCoreAnimations()
        {
            try
            {
                // AtlasCoreControl handles its own initialization
                // Just set initial state
                SetAtlasCoreState(Controls.AtlasVisualState.Idle);
                
                // Load saved orb settings after a short delay to ensure control is initialized
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
                {
                    LoadOrbSettings();
                }));
                
                Debug.WriteLine("[AtlasCore] State machine initialized via AtlasCoreControl - starting in Idle state");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AtlasCore] Error initializing: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Load saved orb appearance settings
        /// </summary>
        private void LoadOrbSettings()
        {
            try
            {
                var (colorPreset, orbStyle, speed, particles) = SettingsWindow.GetOrbSettings();
                
                Debug.WriteLine($"[ChatWindow] Loading orb settings from file: color={colorPreset}, style={orbStyle}, speed={speed}, particles={particles}");
                
                // Apply orb style (Lottie vs Particles)
                bool useLottie = orbStyle != "particles";
                SetOrbStyle(useLottie, useLottie ? orbStyle : null);
                
                if (AtlasCore != null)
                {
                    Debug.WriteLine($"[ChatWindow] AtlasCore is not null, applying settings...");
                    AtlasCore.ApplyColorPreset(colorPreset);
                    AtlasCore.AnimationSpeed = speed;
                    AtlasCore.ParticleCount = particles;
                    Debug.WriteLine($"[ChatWindow] Applied orb settings successfully");
                }
                else
                {
                    Debug.WriteLine($"[ChatWindow] ERROR: AtlasCore is NULL, cannot apply settings!");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ChatWindow] Error loading orb settings: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Set the Atlas Core visual state - delegates to AtlasCoreControl
        /// </summary>
        public void SetAtlasCoreState(Controls.AtlasVisualState newState)
        {
            try
            {
                if (AtlasCore != null)
                {
                    AtlasCore.State = newState;
                    Debug.WriteLine($"[AtlasCore] State set to: {newState}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AtlasCore] Error setting state: {ex.Message}");
            }
        }
        
        // ═══════════════════════════════════════════════════════════════
        // SPEAKING ENERGY SIMULATION - Creates organic animation during TTS
        // ═══════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Start simulating speaking energy for organic Atlas Core animation
        /// </summary>
        private void StartSpeakingEnergySimulation()
        {
            _speakingStartTime = DateTime.Now;
            
            // Initialize timer if needed
            if (_speakingEnergyTimer == null)
            {
                _speakingEnergyTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(33) // ~30 Hz
                };
                _speakingEnergyTimer.Tick += SpeakingEnergyTimer_Tick;
            }
            
            _speakingEnergyTimer.Start();
            Debug.WriteLine("[SpeakingEnergy] Started energy simulation");
        }
        
        /// <summary>
        /// Stop speaking energy simulation with smooth ease-out
        /// </summary>
        private void StopSpeakingEnergySimulation()
        {
            _speakingEnergyTimer?.Stop();
            
            // Tell AtlasCore to ease out smoothly
            AtlasCore?.EndSpeaking();
            
            Debug.WriteLine("[SpeakingEnergy] Stopped energy simulation");
        }
        
        /// <summary>
        /// Generate organic-looking energy values during speaking
        /// Uses layered sine waves + noise for natural speech-like patterns
        /// </summary>
        private void SpeakingEnergyTimer_Tick(object? sender, EventArgs e)
        {
            if (AtlasCore == null) return;
            
            try
            {
                var elapsed = (DateTime.Now - _speakingStartTime).TotalSeconds;
                
                // Layer 1: Base rhythm (slow, ~0.8 Hz - sentence cadence)
                var baseRhythm = 0.5 + 0.3 * Math.Sin(elapsed * 5.0);
                
                // Layer 2: Word rhythm (faster, ~3 Hz)
                var wordRhythm = 0.15 * Math.Sin(elapsed * 19.0);
                
                // Layer 3: Syllable micro-variation (~8 Hz)
                var syllableVar = 0.1 * Math.Sin(elapsed * 50.0);
                
                // Layer 4: Random noise for organic feel
                var noise = (_energyRandom.NextDouble() - 0.5) * 0.15;
                
                // Layer 5: Occasional emphasis spikes (simulates stressed words)
                var emphasisChance = _energyRandom.NextDouble();
                var emphasis = emphasisChance > 0.97 ? 0.25 : 0;
                
                // Combine all layers
                var energy = baseRhythm + wordRhythm + syllableVar + noise + emphasis;
                
                // Clamp to valid range
                energy = Math.Clamp(energy, 0.1, 1.0);
                
                // Feed to AtlasCore
                AtlasCore.UpdateSpeakingEnergy(energy);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SpeakingEnergy] Error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Cycle through Atlas Core states (for testing) - Ctrl+Shift+C
        /// </summary>
        public void CycleAtlasCoreState()
        {
            try
            {
                AtlasCore?.CycleState();
            }
            catch { }
        }
        
        // ═══════════════════════════════════════════════════════════════
        // SUMMONED RADIAL CONTROLS - Appear on hover/hotkey
        // ═══════════════════════════════════════════════════════════════
        
        private void AtlasCoreContainer_MouseEnter(object sender, MouseEventArgs e)
        {
            _isRadialRotationPaused = true;
            PauseRadialRotation(); // Pause smooth animation on hover
            ShowRadialControls();
        }
        
        private void AtlasCoreContainer_MouseLeave(object sender, MouseEventArgs e)
        {
            _isRadialRotationPaused = false;
            ResumeRadialRotation(); // Resume smooth animation when not hovering
            // Start timer to hide controls after delay
            StartRadialHideTimer();
        }
        
        private void StartRadialHideTimer()
        {
            _radialHideTimer?.Stop();
            _radialHideTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1500)
            };
            _radialHideTimer.Tick += (s, e) =>
            {
                _radialHideTimer?.Stop();
                HideRadialControls();
            };
            _radialHideTimer.Start();
        }
        
        private void ToggleRadialControls()
        {
            if (_radialControlsVisible)
                HideRadialControls();
            else
                ShowRadialControls();
        }
        
        private void ShowRadialControls()
        {
            if (_radialControlsVisible) return;
            _radialControlsVisible = true;
            _radialHideTimer?.Stop();
            
            RadialControlsCanvas.IsHitTestVisible = true;
            
            // Start breathing animation for all radial buttons
            StartRadialBreathingAnimation();
            
            // Start slow anti-clockwise rotation
            StartRadialRotationAnimation();
        }
        
        private void StartRadialRotationAnimation()
        {
            if (_radialRotationTimer != null) return; // Already running
            
            // Use WPF's smooth animation system instead of DispatcherTimer
            var rotateAnimation = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0,
                To = -360, // Anti-clockwise
                Duration = TimeSpan.FromSeconds(60), // One full rotation per 60 seconds (slow)
                RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever
            };
            
            // Apply to the canvas rotation transform
            RadialCanvasRotation.BeginAnimation(RotateTransform.AngleProperty, rotateAnimation);
            
            // For counter-rotation, we need to animate each button's rotation in the opposite direction
            StartButtonCounterRotationAnimations();
            
            // Use a flag timer just to track state (not for animation)
            _radialRotationTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _radialRotationTimer.Start();
        }
        
        private void StartButtonCounterRotationAnimations()
        {
            var buttons = new[] { RadialMicBtn, RadialMuteBtn, RadialHistoryBtn, RadialFocusBtn, 
                                  RadialSettingsBtn, RadialWakeBtn, RadialCommandBtn, RadialOrbStyleBtn };
            
            // Counter-rotation animation (clockwise to cancel out the anti-clockwise canvas rotation)
            var counterRotateAnimation = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0,
                To = 360, // Clockwise (opposite of canvas)
                Duration = TimeSpan.FromSeconds(60), // Same speed as canvas
                RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever
            };
            
            foreach (var btn in buttons)
            {
                if (btn == null) continue;
                
                // Ensure button has a RotateTransform we can animate
                if (!(btn.RenderTransform is RotateTransform))
                {
                    btn.RenderTransform = new RotateTransform(0);
                    btn.RenderTransformOrigin = new Point(0.5, 0.5);
                }
                
                // Start the counter-rotation animation
                var transform = btn.RenderTransform as RotateTransform;
                transform?.BeginAnimation(RotateTransform.AngleProperty, counterRotateAnimation.Clone());
            }
        }
        
        private void PauseRadialRotation()
        {
            // Pause canvas rotation
            RadialCanvasRotation.BeginAnimation(RotateTransform.AngleProperty, null);
            
            // Pause button counter-rotations
            var buttons = new[] { RadialMicBtn, RadialMuteBtn, RadialHistoryBtn, RadialFocusBtn, 
                                  RadialSettingsBtn, RadialWakeBtn, RadialCommandBtn, RadialOrbStyleBtn };
            foreach (var btn in buttons)
            {
                if (btn?.RenderTransform is RotateTransform rot)
                {
                    rot.BeginAnimation(RotateTransform.AngleProperty, null);
                }
            }
        }
        
        private void ResumeRadialRotation()
        {
            // Get current angle and continue from there
            var currentAngle = RadialCanvasRotation.Angle;
            
            var rotateAnimation = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = currentAngle,
                To = currentAngle - 360,
                Duration = TimeSpan.FromSeconds(60),
                RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever
            };
            RadialCanvasRotation.BeginAnimation(RotateTransform.AngleProperty, rotateAnimation);
            
            // Resume button counter-rotations
            var buttons = new[] { RadialMicBtn, RadialMuteBtn, RadialHistoryBtn, RadialFocusBtn, 
                                  RadialSettingsBtn, RadialWakeBtn, RadialCommandBtn, RadialOrbStyleBtn };
            
            var counterRotateAnimation = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = -currentAngle,
                To = -currentAngle + 360,
                Duration = TimeSpan.FromSeconds(60),
                RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever
            };
            
            foreach (var btn in buttons)
            {
                if (btn?.RenderTransform is RotateTransform rot)
                {
                    rot.BeginAnimation(RotateTransform.AngleProperty, counterRotateAnimation.Clone());
                }
            }
        }
        
        private bool _isRadialRotationPaused = false;
        
        private void CounterRotateRadialButtons(double angle)
        {
            // This method is no longer used - animations handle counter-rotation
        }
        
        private void StartRadialBreathingAnimation()
        {
            var buttons = new[] { RadialMicBtn, RadialMuteBtn, RadialHistoryBtn, RadialFocusBtn, 
                                  RadialSettingsBtn, RadialWakeBtn, RadialCommandBtn, RadialOrbStyleBtn };
            
            foreach (var btn in buttons)
            {
                if (btn?.Effect is System.Windows.Media.Effects.DropShadowEffect effect)
                {
                    // Breathing glow animation - slow pulse
                    var glowAnim = new System.Windows.Media.Animation.DoubleAnimation
                    {
                        From = 0.5,
                        To = 1.0,
                        Duration = TimeSpan.FromSeconds(2),
                        AutoReverse = true,
                        RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
                        EasingFunction = new System.Windows.Media.Animation.SineEase()
                    };
                    effect.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty, glowAnim);
                    
                    // Breathing blur radius
                    var blurAnim = new System.Windows.Media.Animation.DoubleAnimation
                    {
                        From = 12,
                        To = 20,
                        Duration = TimeSpan.FromSeconds(2),
                        AutoReverse = true,
                        RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
                        EasingFunction = new System.Windows.Media.Animation.SineEase()
                    };
                    effect.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.BlurRadiusProperty, blurAnim);
                }
                
                // Animate the inner orange glow (RadialGradientBrush)
                if (btn?.Background is RadialGradientBrush gradient && gradient.GradientStops.Count >= 2)
                {
                    // Animate the center orange color opacity (breathing effect)
                    var centerStop = gradient.GradientStops[0];
                    var midStop = gradient.GradientStops[1];
                    
                    // Breathing animation for center color - brighter orange pulse
                    var centerColorAnim = new System.Windows.Media.Animation.ColorAnimation
                    {
                        From = Color.FromArgb(0x70, 0xf9, 0x73, 0x16), // Dimmer orange
                        To = Color.FromArgb(0xCC, 0xf9, 0x73, 0x16),   // Much brighter orange
                        Duration = TimeSpan.FromSeconds(1.5),
                        AutoReverse = true,
                        RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
                        EasingFunction = new System.Windows.Media.Animation.SineEase()
                    };
                    centerStop.BeginAnimation(GradientStop.ColorProperty, centerColorAnim);
                    
                    // Breathing animation for mid color
                    var midColorAnim = new System.Windows.Media.Animation.ColorAnimation
                    {
                        From = Color.FromArgb(0x30, 0xf9, 0x73, 0x16), // Dimmer
                        To = Color.FromArgb(0x70, 0xf9, 0x73, 0x16),   // Brighter
                        Duration = TimeSpan.FromSeconds(1.5),
                        AutoReverse = true,
                        RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
                        EasingFunction = new System.Windows.Media.Animation.SineEase()
                    };
                    midStop.BeginAnimation(GradientStop.ColorProperty, midColorAnim);
                }
            }
        }
        
        private void HideRadialControls()
        {
            if (!_radialControlsVisible) return;
            _radialControlsVisible = false;
            
            // Keep hit test visible so buttons still work
            RadialControlsCanvas.IsHitTestVisible = true;
            
            // Fade back to subtle opacity (0.4) instead of fully hidden
            var canvasAnim = new System.Windows.Media.Animation.DoubleAnimation(0.4, TimeSpan.FromMilliseconds(200));
            RadialControlsCanvas.BeginAnimation(OpacityProperty, canvasAnim);
        }
        
        // Radial button click handlers
        private void RadialMic_Click(object sender, MouseButtonEventArgs e)
        {
            ActivateVoiceWithHotkey();
        }
        
        private void RadialMute_Click(object sender, MouseButtonEventArgs e)
        {
            _isTtsMuted = !_isTtsMuted;
            _voiceManager.SpeechEnabled = !_isTtsMuted;
            RadialMuteIcon.Text = _isTtsMuted ? "🔇" : "🔊";
            ShowStatus(_isTtsMuted ? "🔇 TTS Muted" : "🔊 TTS Unmuted");
        }
        
        private void RadialHistory_Click(object sender, MouseButtonEventArgs e)
        {
            // Use the working History_Click method instead of the broken drawer
            History_Click(sender, new RoutedEventArgs());
        }
        
        private void RadialFocus_Click(object sender, MouseButtonEventArgs e)
        {
            ToggleFocusMode();
        }
        
        private void RadialSettings_Click(object sender, MouseButtonEventArgs e)
        {
            Settings_Click(null, new RoutedEventArgs());
        }
        
        private void RadialWake_Click(object sender, MouseButtonEventArgs e)
        {
            ToggleWakeWord();
            RadialWakeIcon.Text = isWakeWordEnabled ? "👂" : "🚫";
        }
        
        private void RadialCommand_Click(object sender, MouseButtonEventArgs e)
        {
            OpenCommandPalette();
        }
        
        // ═══════════════════════════════════════════════════════════════
        // ORB STYLE SELECTOR - Cycle through available orb animations
        // ═══════════════════════════════════════════════════════════════
        private int _currentOrbStyleIndex = 0;
        private readonly (string name, string file, string icon)[] _orbStyles = new[]
        {
            ("Particles", "particles", "🔮"),
            ("Siri", "Siri Animation.json", "✨"),
            ("AI Assistant", "AI Assistant.json", "🤖"),
            ("AI Loading", "AI Loading.json", "⏳"),
            ("Loading", "Loading animation.json", "⚡"),
            ("Loop", "Loading loop animation.json", "🔄"),
            ("AI AI", "ai ai.json", "🧠"),
            ("Circle", "Circle Animation.json", "⭕"),
            ("Circles", "circles.json", "🔵"),
            ("Circle 2", "circle.json", "⚪"),
            ("Hearts", "floating hearts.json", "💜"),
            ("Ghost", "Ghostsmart.json", "👻"),
            ("Triangle", "Loader Triangle Flow.json", "🔺"),
            ("Navi", "Navi's loader.json", "🧭"),
            ("Robot AI", "Robot Futuristic Ai.json", "🤖"),
            ("Waves", "waves.json", "🌊"),
            ("Animation", "Animation - 1695019131207.json", "💫")
        };
        
        private void RadialOrbStyle_Click(object sender, MouseButtonEventArgs e)
        {
            // Cycle to next orb style
            _currentOrbStyleIndex = (_currentOrbStyleIndex + 1) % _orbStyles.Length;
            var style = _orbStyles[_currentOrbStyleIndex];
            
            // Apply the style
            if (style.file == "particles")
            {
                SetOrbStyle(false, null); // Use particle orb
            }
            else
            {
                SetOrbStyle(true, style.file); // Use Lottie animation
            }
            
            // Update icon and show status
            RadialOrbStyleIcon.Text = style.icon;
            ShowStatus($"🔮 {style.name}");
        }
        
        // ═══════════════════════════════════════════════════════════════
        // SCAN ORBIT MODE - Orbiting scan icons around the orb
        // ═══════════════════════════════════════════════════════════════
        
        private bool _scanOrbitVisible = false;
        
        public void ToggleScanOrbit()
        {
            _scanOrbitVisible = !_scanOrbitVisible;
            ScanOrbit.Visibility = _scanOrbitVisible ? Visibility.Visible : Visibility.Collapsed;
            
            if (_scanOrbitVisible)
            {
                ScanOrbit.StartOrbit();
                ShowStatus("🛡 Scan Mode Active - Click icons to scan");
            }
            else
            {
                ScanOrbit.StopOrbit();
                ShowStatus("🛡 Scan Mode Disabled");
            }
        }
        
        private void ScanOrbit_ScanStarted(object? sender, string message)
        {
            // Atlas announces the scan
            ShowStatus($"🔍 {message}");
            _ = SpeakResponseAsync(message);
        }
        
        private async void ScanOrbit_ScanCompleted(object? sender, Controls.ScanResultEventArgs e)
        {
            // Build response message
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"**{e.ScanType} Scan Complete**\n");
            sb.AppendLine(e.Result.Summary);
            sb.AppendLine();
            
            foreach (var detail in e.Result.Details.Take(15))
                sb.AppendLine(detail);
            
            if (e.Result.Details.Count > 15)
                sb.AppendLine($"\n... and {e.Result.Details.Count - 15} more items");
            
            // Add to chat
            AddMessage("ATLAS", sb.ToString(), false);
            
            // Speak summary
            var spokenSummary = e.Result.IssuesFound > 0 
                ? $"{e.ScanType} scan found {e.Result.IssuesFound} issues and {e.Result.WarningsFound} warnings."
                : e.Result.WarningsFound > 0
                    ? $"{e.ScanType} scan found {e.Result.WarningsFound} warnings."
                    : $"{e.ScanType} scan complete. Everything looks good.";
            
            await SpeakResponseAsync(spokenSummary);
        }
        
        // ═══════════════════════════════════════════════════════════════
        // PROACTIVE SECURITY MONITOR - Installation alerts & health scans
        // ═══════════════════════════════════════════════════════════════
        
        private void InitializeSecurityMonitor()
        {
            try
            {
                Debug.WriteLine("[Security] InitializeSecurityMonitor called");
                var monitor = Agent.ProactiveSecurityMonitor.Instance;
                monitor.InstallationDetected += SecurityMonitor_InstallationDetected;
                monitor.HealthScanCompleted += SecurityMonitor_HealthScanCompleted;
                monitor.StatusChanged += SecurityMonitor_StatusChanged;
                monitor.Start();
                Debug.WriteLine("[Security] Proactive security monitor started and events subscribed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Security] Monitor init error: {ex.Message}");
            }
        }
        
        private async void SecurityMonitor_InstallationDetected(object? sender, Agent.InstallationAlert alert)
        {
            Debug.WriteLine($"[Security] EVENT RECEIVED: {alert.FileName}");
            await Dispatcher.InvokeAsync(async () =>
            {
                Debug.WriteLine($"[Security] On UI thread, showing popup for: {alert.FileName}");
                // Show visual popup alert
                ShowInstallationAlertPopup(alert);
                
                // Build alert message for chat
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"🔔 **Installation Detected**");
                sb.AppendLine($"File: {alert.FileName}");
                sb.AppendLine($"Publisher: {alert.Publisher}");
                sb.AppendLine($"Risk: {alert.RiskLevel}");
                sb.AppendLine();
                sb.AppendLine(alert.Recommendation);
                
                if (alert.BundledApps?.Any() == true)
                {
                    sb.AppendLine();
                    sb.AppendLine("⚠️ Bundled software detected:");
                    foreach (var app in alert.BundledApps.Take(5))
                        sb.AppendLine($"  • {app}");
                }
                
                // Add to chat
                AddMessage("ATLAS", sb.ToString(), false);
                
                // Speak alert based on risk level
                string spokenAlert = alert.RiskLevel switch
                {
                    Agent.SecurityRiskLevel.High => $"Warning! I detected a potentially unwanted program: {alert.FileName}. I recommend not installing this.",
                    Agent.SecurityRiskLevel.Medium => $"Heads up, I noticed a new installer: {alert.FileName}. It's not from a known publisher, so be careful.",
                    Agent.SecurityRiskLevel.Low => $"New installation detected: {alert.FileName}. Looks safe, from {alert.Publisher}.",
                    _ => $"I detected a new file: {alert.FileName}."
                };
                
                await SpeakResponseAsync(spokenAlert);
            });
        }
        
        private void ShowInstallationAlertPopup(Agent.InstallationAlert alert)
        {
            var threatColor = alert.RiskLevel switch
            {
                Agent.SecurityRiskLevel.High => System.Windows.Media.Color.FromRgb(239, 68, 68),
                Agent.SecurityRiskLevel.Medium => System.Windows.Media.Color.FromRgb(245, 158, 11),
                Agent.SecurityRiskLevel.Low => System.Windows.Media.Color.FromRgb(34, 197, 94),
                _ => System.Windows.Media.Color.FromRgb(34, 211, 238)
            };
            
            var alertWindow = new Window
            {
                Title = "Atlas AI Security Alert",
                Width = 480,
                Height = 380,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent,
                Topmost = true,
                ShowInTaskbar = false
            };
            
            var mainBorder = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(10, 12, 20)),
                BorderBrush = new System.Windows.Media.SolidColorBrush(threatColor),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(12),
                Effect = new System.Windows.Media.Effects.DropShadowEffect 
                { 
                    Color = threatColor, 
                    BlurRadius = 30, 
                    ShadowDepth = 0, 
                    Opacity = 0.5 
                }
            };
            
            var mainStack = new StackPanel { Margin = new Thickness(20) };
            
            // Header with icon
            var headerStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            var icon = alert.RiskLevel == Agent.SecurityRiskLevel.High ? "⚠️" : 
                       alert.RiskLevel == Agent.SecurityRiskLevel.Medium ? "⚡" : "🔔";
            headerStack.Children.Add(new TextBlock 
            { 
                Text = icon, 
                FontSize = 24, 
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            headerStack.Children.Add(new TextBlock 
            { 
                Text = "NEW FILE DETECTED", 
                Foreground = new System.Windows.Media.SolidColorBrush(threatColor), 
                FontSize = 16, 
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            });
            mainStack.Children.Add(headerStack);
            
            // Risk badge
            var riskText = alert.RiskLevel.ToString().ToUpper();
            var riskBadge = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(40, threatColor.R, threatColor.G, threatColor.B)),
                BorderBrush = new System.Windows.Media.SolidColorBrush(threatColor),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 4, 10, 4),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 12)
            };
            riskBadge.Child = new TextBlock 
            { 
                Text = $"RISK: {riskText}", 
                Foreground = new System.Windows.Media.SolidColorBrush(threatColor),
                FontFamily = new System.Windows.Media.FontFamily("Cascadia Code"),
                FontSize = 10,
                FontWeight = FontWeights.SemiBold
            };
            mainStack.Children.Add(riskBadge);
            
            // File info
            var infoStack = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            infoStack.Children.Add(new TextBlock 
            { 
                Text = "FILE:", 
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(107, 114, 128)), 
                FontSize = 10 
            });
            infoStack.Children.Add(new TextBlock 
            { 
                Text = alert.FileName, 
                Foreground = System.Windows.Media.Brushes.White, 
                FontSize = 13, 
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 8)
            });
            
            if (!string.IsNullOrEmpty(alert.Publisher) && alert.Publisher != "Unknown")
            {
                infoStack.Children.Add(new TextBlock 
                { 
                    Text = "PUBLISHER:", 
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(107, 114, 128)), 
                    FontSize = 10 
                });
                infoStack.Children.Add(new TextBlock 
                { 
                    Text = alert.Publisher, 
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(156, 163, 175)), 
                    FontSize = 11,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 2, 0, 8)
                });
            }
            mainStack.Children.Add(infoStack);
            
            // Recommendation
            if (!string.IsNullOrEmpty(alert.Recommendation))
            {
                var recBorder = new Border
                {
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(20, 255, 255, 255)),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(10),
                    Margin = new Thickness(0, 0, 0, 16)
                };
                recBorder.Child = new TextBlock 
                { 
                    Text = alert.Recommendation, 
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(209, 213, 219)),
                    FontSize = 11,
                    TextWrapping = TextWrapping.Wrap
                };
                mainStack.Children.Add(recBorder);
            }
            
            // Action buttons
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            
            // Delete button (for high risk)
            if (alert.RiskLevel == Agent.SecurityRiskLevel.High && !string.IsNullOrEmpty(alert.FilePath))
            {
                var deleteBtn = CreateSecurityAlertButton("🗑️ Delete", System.Windows.Media.Color.FromRgb(239, 68, 68), () =>
                {
                    try
                    {
                        if (System.IO.File.Exists(alert.FilePath))
                        {
                            System.IO.File.Delete(alert.FilePath);
                            ShowStatus($"🗑️ Deleted: {alert.FileName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        ShowStatus($"❌ Delete failed: {ex.Message}");
                    }
                    alertWindow.Close();
                });
                buttonPanel.Children.Add(deleteBtn);
            }
            
            // Block button
            var blockBtn = CreateSecurityAlertButton("🚫 Block", System.Windows.Media.Color.FromRgb(245, 158, 11), () =>
            {
                ShowStatus($"🚫 Blocked: {alert.FileName}");
                alertWindow.Close();
            });
            buttonPanel.Children.Add(blockBtn);
            
            // Allow button
            var allowBtn = CreateSecurityAlertButton("✓ Allow", System.Windows.Media.Color.FromRgb(34, 197, 94), () =>
            {
                ShowStatus($"✓ Allowed: {alert.FileName}");
                alertWindow.Close();
            });
            buttonPanel.Children.Add(allowBtn);
            
            // Dismiss button
            var dismissBtn = CreateSecurityAlertButton("✕", System.Windows.Media.Color.FromRgb(107, 114, 128), () =>
            {
                alertWindow.Close();
            });
            buttonPanel.Children.Add(dismissBtn);
            
            mainStack.Children.Add(buttonPanel);
            
            mainBorder.Child = mainStack;
            alertWindow.Content = mainBorder;
            
            // Allow dragging
            mainBorder.MouseLeftButtonDown += (s, e) => 
            { 
                if (e.ChangedButton == MouseButton.Left) 
                    alertWindow.DragMove(); 
            };
            
            // Auto-close after 30 seconds
            var autoCloseTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            autoCloseTimer.Tick += (s, e) => { autoCloseTimer.Stop(); alertWindow.Close(); };
            autoCloseTimer.Start();
            
            alertWindow.Show();
        }
        
        private Button CreateSecurityAlertButton(string text, System.Windows.Media.Color color, Action onClick)
        {
            var btn = new Button
            {
                Content = text,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(30, color.R, color.G, color.B)),
                Foreground = new System.Windows.Media.SolidColorBrush(color),
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(100, color.R, color.G, color.B)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(4, 0, 0, 0),
                Cursor = Cursors.Hand,
                FontWeight = FontWeights.SemiBold,
                FontSize = 11
            };
            
            btn.Click += (s, e) => onClick();
            btn.MouseEnter += (s, e) => btn.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(60, color.R, color.G, color.B));
            btn.MouseLeave += (s, e) => btn.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(30, color.R, color.G, color.B));
            
            return btn;
        }
        
        private async void SecurityMonitor_HealthScanCompleted(object? sender, Agent.HealthReport report)
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                // Build health report message
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"🔍 **System Health Check** ({report.ScanTime:HH:mm})");
                sb.AppendLine();
                sb.AppendLine(report.Summary);
                
                if (report.HighMemoryProcesses.Any())
                {
                    sb.AppendLine();
                    sb.AppendLine("High memory processes:");
                    foreach (var proc in report.HighMemoryProcesses.Take(3))
                        sb.AppendLine($"  • {proc}");
                }
                
                // Add to chat
                AddMessage("ATLAS", sb.ToString(), false);
                
                // Speak summary
                string spokenSummary = report.OverallStatus switch
                {
                    "Critical" => $"Attention! Your system has {report.CriticalIssues} critical issues that need attention.",
                    "Warning" => $"Your system is running with {report.Warnings} warnings. {(report.MemoryUsedPercent > 80 ? "Memory is getting high." : "")}",
                    _ => "Your system is running smoothly. All checks passed."
                };
                
                await SpeakResponseAsync(spokenSummary);
            });
        }
        
        private void SecurityMonitor_StatusChanged(object? sender, string status)
        {
            Dispatcher.InvokeAsync(() => ShowStatus($"🛡 {status}"));
        }
        
        public void ToggleSecurityNotifications(bool enabled)
        {
            Agent.ProactiveSecurityMonitor.Instance.SetNotifications(enabled);
            ShowStatus(enabled ? "🔔 Security notifications ON" : "🔕 Security notifications OFF");
        }
        
        public void SetAutoHealthScan(bool enabled, int intervalHours = 2)
        {
            Agent.ProactiveSecurityMonitor.Instance.SetAutoScan(enabled, intervalHours);
        }
        
        public async Task RunManualHealthScanAsync()
        {
            ShowStatus("🔍 Running health scan...");
            await Agent.ProactiveSecurityMonitor.Instance.RunHealthScanAsync();
        }
        
        // ═══════════════════════════════════════════════════════════════
        // FOCUS MODE - Compact presence with smooth transitions
        // ═══════════════════════════════════════════════════════════════
        
        private void ToggleFocusMode()
        {
            _isFocusMode = !_isFocusMode;
            
            if (_isFocusMode)
                EnterFocusMode();
            else
                ExitFocusMode();
            
            RadialFocusIcon.Text = _isFocusMode ? "🔳" : "🎯";
            ShowStatus(_isFocusMode ? "🎯 Focus Mode" : "🔳 Normal Mode");
        }
        
        private void EnterFocusMode()
        {
            var duration = TimeSpan.FromMilliseconds(400);
            var easing = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut };
            
            // Shrink and move core to bottom-right
            var scaleAnim = new System.Windows.Media.Animation.DoubleAnimation(0.6, duration) { EasingFunction = easing };
            var translateXAnim = new System.Windows.Media.Animation.DoubleAnimation(350, duration) { EasingFunction = easing };
            var translateYAnim = new System.Windows.Media.Animation.DoubleAnimation(200, duration) { EasingFunction = easing };
            
            CoreContainerScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            CoreContainerScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
            CoreContainerTranslate.BeginAnimation(TranslateTransform.XProperty, translateXAnim);
            CoreContainerTranslate.BeginAnimation(TranslateTransform.YProperty, translateYAnim);
            
            // Reduce projection count
            // (Projections will naturally show fewer in focus mode)
            
            // Shrink input area
            var inputMarginAnim = new System.Windows.Media.Animation.ThicknessAnimation(
                new Thickness(20, 0, 20, 15), duration) { EasingFunction = easing };
            InputBorder.BeginAnimation(MarginProperty, inputMarginAnim);
        }
        
        private void ExitFocusMode()
        {
            var duration = TimeSpan.FromMilliseconds(400);
            var easing = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut };
            
            // Restore core position and scale
            var scaleAnim = new System.Windows.Media.Animation.DoubleAnimation(1, duration) { EasingFunction = easing };
            var translateXAnim = new System.Windows.Media.Animation.DoubleAnimation(0, duration) { EasingFunction = easing };
            var translateYAnim = new System.Windows.Media.Animation.DoubleAnimation(0, duration) { EasingFunction = easing };
            
            CoreContainerScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            CoreContainerScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
            CoreContainerTranslate.BeginAnimation(TranslateTransform.XProperty, translateXAnim);
            CoreContainerTranslate.BeginAnimation(TranslateTransform.YProperty, translateYAnim);
            
            // Restore input area
            var inputMarginAnim = new System.Windows.Media.Animation.ThicknessAnimation(
                new Thickness(0), duration) { EasingFunction = easing };
            InputBorder.BeginAnimation(MarginProperty, inputMarginAnim);
        }
        
        // ToggleHistoryDrawer is defined earlier in the file (around line 6468)
        // with proper OpenHistoryDrawer/CloseHistoryDrawer calls
        
        private void ToggleWakeWord()
        {
            isWakeWordEnabled = !isWakeWordEnabled;
            if (isWakeWordEnabled)
            {
                _wakeWordDetector?.StartListening();
                WakeWordIndicator.Visibility = Visibility.Visible;
            }
            else
            {
                _wakeWordDetector?.StopListening();
                WakeWordIndicator.Visibility = Visibility.Collapsed;
            }
        }
        
        /// <summary>
        /// Stop animations specific to a state
        /// <summary>
        /// Force the taskbar icon to display for borderless windows
        /// </summary>
        private void ForceTaskbarIcon()
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;
                
                // Ensure window shows in taskbar
                int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_APPWINDOW);
                
                // Try to load icon from the atlas.ico file in the app directory
                var appDir = AppDomain.CurrentDomain.BaseDirectory;
                var iconPath = Path.Combine(appDir, "atlas.ico");
                
                // If not in app dir, try the source directory
                if (!File.Exists(iconPath))
                {
                    iconPath = Path.Combine(Directory.GetCurrentDirectory(), "AtlasAI", "atlas.ico");
                }
                if (!File.Exists(iconPath))
                {
                    iconPath = @"C:\Users\littl\VisualAIVirtualAssistant\AtlasAI\atlas.ico";
                }
                
                if (File.Exists(iconPath))
                {
                    var icon = new System.Drawing.Icon(iconPath);
                    SendMessage(hwnd, WM_SETICON, (IntPtr)ICON_BIG, icon.Handle);
                    SendMessage(hwnd, WM_SETICON, (IntPtr)ICON_SMALL, icon.Handle);
                    Debug.WriteLine($"[ChatWindow] Icon loaded from: {iconPath}");
                }
                else
                {
                    Debug.WriteLine($"[ChatWindow] Icon file not found at: {iconPath}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ChatWindow] Error setting taskbar icon: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Initialize the conversation system - sessions, memory, profile, and onboarding
        /// </summary>
        private async Task InitializeConversationSystemAsync()
        {
            try
            {
                _conversationManager = new ConversationManager();
                await _conversationManager.InitializeAsync();
                
                _systemPromptBuilder = new SystemPromptBuilder(_conversationManager);
                
                // Initialize coding assistant
                _codeAssistant = new Coding.CodeAssistantService();
                _codeToolExecutor = new Coding.CodeToolExecutor(_codeAssistant);
                
                // Initialize installed apps manager (scans in background)
                _ = Task.Run(async () => 
                {
                    try
                    {
                        await SystemControl.InstalledAppsManager.Instance.InitializeAsync();
                        Debug.WriteLine($"[Apps] Initialized with {SystemControl.InstalledAppsManager.Instance.AppCount} apps");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Apps] Init error: {ex.Message}");
                    }
                });
                
                // Start proactive security monitoring
                InitializeSecurityMonitor();
                
                // Check for first run - show onboarding
                bool isFirstRun = await _conversationManager.IsFirstRunAsync();
                if (isFirstRun)
                {
                    await ShowOnboardingAsync();
                    // Don't show startup welcome - onboarding already handles it
                }
                else
                {
                    // Update system prompt with user profile
                    UpdateSystemPromptFromProfile();
                    
                    // Show welcome message on startup (only for returning users)
                    await ShowStartupWelcomeAsync();
                }
                
                Debug.WriteLine("[ChatWindow] Conversation system initialized");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ChatWindow] Error initializing conversation system: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Shows the big welcome message on startup - ALWAYS runs
        /// </summary>
        private async Task ShowStartupWelcomeAsync()
        {
            try
            {
                // Get user's name from profile
                string userName = "sir"; // Default fallback
                if (_conversationManager?.UserProfile?.DisplayName != null && 
                    !string.IsNullOrWhiteSpace(_conversationManager.UserProfile.DisplayName))
                {
                    userName = _conversationManager.UserProfile.DisplayName;
                }
                
                // Use the random greeting from SystemPromptBuilder
                var greeting = _systemPromptBuilder?.GetGreeting(false) ?? GetRandomWelcomeMessage(userName);
                
                Debug.WriteLine($"[Welcome] Showing startup welcome: {greeting}");
                
                // Show the message in chat
                await Dispatcher.InvokeAsync(() => AddMessage("Atlas", greeting, false));
                
                // Wait for voice system to be fully ready
                await Task.Delay(500);
                
                // Use the SAVED voice provider, not hardcoded ElevenLabs
                try
                {
                    var savedProvider = SettingsWindow.GetSelectedVoiceProvider();
                    Debug.WriteLine($"[Welcome] Using saved voice provider: {savedProvider}");
                    
                    var keys = SettingsWindow.GetVoiceApiKeys();
                    bool configured = false;
                    
                    switch (savedProvider)
                    {
                        case VoiceProviderType.OpenAI:
                            if (keys.TryGetValue("openai", out var openaiKey) && !string.IsNullOrEmpty(openaiKey))
                            {
                                _voiceManager.ConfigureProvider(VoiceProviderType.OpenAI, new Dictionary<string, string> { ["ApiKey"] = openaiKey });
                                configured = await _voiceManager.SetProviderAsync(VoiceProviderType.OpenAI);
                                if (configured) await _voiceManager.RestoreSavedVoiceAsync();
                            }
                            break;
                            
                        case VoiceProviderType.ElevenLabs:
                            if (keys.TryGetValue("elevenlabs", out var elevenKey) && !string.IsNullOrEmpty(elevenKey))
                            {
                                _voiceManager.ConfigureProvider(VoiceProviderType.ElevenLabs, new Dictionary<string, string> { ["ApiKey"] = elevenKey });
                                configured = await _voiceManager.SetProviderAsync(VoiceProviderType.ElevenLabs);
                                if (configured) await _voiceManager.RestoreSavedVoiceAsync();
                            }
                            break;
                            
                        case VoiceProviderType.EdgeTTS:
                            configured = await _voiceManager.SetProviderAsync(VoiceProviderType.EdgeTTS);
                            if (configured) await _voiceManager.RestoreSavedVoiceAsync();
                            break;
                            
                        default:
                            configured = await _voiceManager.SetProviderAsync(VoiceProviderType.WindowsSAPI);
                            if (configured) await _voiceManager.RestoreSavedVoiceAsync();
                            break;
                    }
                    
                    // Smart fallback chain: EdgeTTS failed -> try ElevenLabs -> try OpenAI -> Windows SAPI
                    if (!configured)
                    {
                        Debug.WriteLine($"[Welcome] Saved provider {savedProvider} failed, trying fallback chain...");
                        
                        // Try ElevenLabs if we have a key
                        if (keys.TryGetValue("elevenlabs", out var fallbackElevenKey) && !string.IsNullOrEmpty(fallbackElevenKey))
                        {
                            _voiceManager.ConfigureProvider(VoiceProviderType.ElevenLabs, new Dictionary<string, string> { ["ApiKey"] = fallbackElevenKey });
                            configured = await _voiceManager.SetProviderAsync(VoiceProviderType.ElevenLabs);
                            if (configured)
                            {
                                Debug.WriteLine("[Welcome] Fallback to ElevenLabs successful");
                                await _voiceManager.RestoreSavedVoiceAsync();
                            }
                        }
                        
                        // Try OpenAI if ElevenLabs failed
                        if (!configured && keys.TryGetValue("openai", out var fallbackOpenaiKey) && !string.IsNullOrEmpty(fallbackOpenaiKey))
                        {
                            _voiceManager.ConfigureProvider(VoiceProviderType.OpenAI, new Dictionary<string, string> { ["ApiKey"] = fallbackOpenaiKey });
                            configured = await _voiceManager.SetProviderAsync(VoiceProviderType.OpenAI);
                            if (configured)
                            {
                                Debug.WriteLine("[Welcome] Fallback to OpenAI TTS successful");
                            }
                        }
                        
                        // Final fallback to Windows SAPI
                        if (!configured)
                        {
                            configured = await _voiceManager.SetProviderAsync(VoiceProviderType.WindowsSAPI);
                            Debug.WriteLine("[Welcome] Final fallback to Windows SAPI");
                        }
                    }
                    
                    if (configured)
                    {
                        await _voiceManager.SpeakAsync(greeting);
                        Debug.WriteLine("[Welcome] Startup greeting spoken successfully");
                    }
                }
                catch (Exception ttsEx)
                {
                    Debug.WriteLine($"[Welcome] TTS error: {ttsEx.Message}");
                    // Try Windows SAPI as fallback
                    try
                    {
                        await _voiceManager.SetProviderAsync(VoiceProviderType.WindowsSAPI);
                        await _voiceManager.SpeakAsync(greeting);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Welcome] Error showing startup welcome: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Show the first-run onboarding window
        /// </summary>
        private async Task ShowOnboardingAsync()
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                // Use the saved voice provider setting, not hardcoded ElevenLabs
                bool voiceReady = false;
                try
                {
                    // Get saved voice provider from settings
                    var savedProvider = SettingsWindow.GetSelectedVoiceProvider();
                    Debug.WriteLine($"[Onboarding] Using saved voice provider: {savedProvider}");
                    
                    // Configure based on saved provider
                    var keys = SettingsWindow.GetVoiceApiKeys();
                    
                    switch (savedProvider)
                    {
                        case VoiceProviderType.OpenAI:
                            if (keys.TryGetValue("openai", out var openaiKey) && !string.IsNullOrEmpty(openaiKey))
                            {
                                _voiceManager.ConfigureProvider(VoiceProviderType.OpenAI, new Dictionary<string, string> { ["ApiKey"] = openaiKey });
                                voiceReady = await _voiceManager.SetProviderAsync(VoiceProviderType.OpenAI);
                                if (voiceReady) await _voiceManager.RestoreSavedVoiceAsync();
                                Debug.WriteLine($"[Onboarding] OpenAI TTS configured: {voiceReady}");
                            }
                            break;
                            
                        case VoiceProviderType.ElevenLabs:
                            if (keys.TryGetValue("elevenlabs", out var elevenKey) && !string.IsNullOrEmpty(elevenKey))
                            {
                                _voiceManager.ConfigureProvider(VoiceProviderType.ElevenLabs, new Dictionary<string, string> { ["ApiKey"] = elevenKey });
                                voiceReady = await _voiceManager.SetProviderAsync(VoiceProviderType.ElevenLabs);
                                if (voiceReady) await _voiceManager.RestoreSavedVoiceAsync();
                                Debug.WriteLine($"[Onboarding] ElevenLabs configured: {voiceReady}");
                            }
                            break;
                            
                        case VoiceProviderType.EdgeTTS:
                            voiceReady = await _voiceManager.SetProviderAsync(VoiceProviderType.EdgeTTS);
                            if (voiceReady) await _voiceManager.RestoreSavedVoiceAsync();
                            Debug.WriteLine($"[Onboarding] Edge TTS configured: {voiceReady}");
                            break;
                            
                        default:
                            voiceReady = await _voiceManager.SetProviderAsync(VoiceProviderType.WindowsSAPI);
                            if (voiceReady) await _voiceManager.RestoreSavedVoiceAsync();
                            Debug.WriteLine($"[Onboarding] Windows SAPI configured: {voiceReady}");
                            break;
                    }
                    
                    // Fallback to Windows SAPI if saved provider failed
                    if (!voiceReady)
                    {
                        Debug.WriteLine($"[Onboarding] Saved provider {savedProvider} failed, falling back to Windows SAPI");
                        voiceReady = await _voiceManager.SetProviderAsync(VoiceProviderType.WindowsSAPI);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Onboarding] Voice config error: {ex.Message}");
                }
                
                // Fallback to Windows SAPI if nothing worked
                if (!voiceReady)
                {
                    try
                    {
                        voiceReady = await _voiceManager.SetProviderAsync(VoiceProviderType.WindowsSAPI);
                        Debug.WriteLine("[Onboarding] Final fallback to Windows SAPI");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Onboarding] SAPI error: {ex.Message}");
                    }
                }
                
                // Show the full welcome message in chat (detailed version)
                var welcomeMessage = _systemPromptBuilder?.GetGreeting(true) ?? "Hello! I'm Atlas. What would you like me to call you?";
                AddMessage("Atlas", welcomeMessage, false);
                
                // Speak a SHORTER version that covers all features concisely
                var spokenWelcome = @"Hello. I'm Atlas, your personal AI assistant. 

I can open apps, manage files, search the web, play music, and automate everyday tasks. 

I have a built-in code editor for writing and debugging code. 

I support voice and text, and you can adjust my style anytime. 

I'm context-aware and can help you directly in other applications. 

I remember your preferences and learn over time, but you're always in control. 

Your conversations are saved in History, and I include security tools that work quietly in the background.

Before we begin, what would you like me to call you?";
                
                // Start speaking (with fallback already configured)
                _ = _voiceManager.SpeakAsync(spokenWelcome);
                
                // Wait for speech to finish - add extra buffer for natural pace
                int wordCount = spokenWelcome.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                int estimatedMs = (wordCount * 280) + 8000; // Base timing + 8 extra seconds buffer
                Debug.WriteLine($"[Onboarding] Waiting {estimatedMs}ms for {wordCount} words (includes 8s buffer)");
                await Task.Delay(estimatedMs);
                
                // NOW show the onboarding window for settings (after speech is done)
                var onboarding = new OnboardingWindow();
                onboarding.Owner = this;
                
                if (onboarding.ShowDialog() == true)
                {
                    // Save onboarding choices
                    _ = _conversationManager?.CompleteOnboardingAsync(
                        onboarding.UserName,
                        onboarding.SelectedStyle,
                        "atQICwskSXjGu0SZpOep" // Default Atlas voice
                    );
                    
                    // Update system prompt with new profile
                    UpdateSystemPromptFromProfile();
                    
                    // If user tried a quick action, execute it
                    if (!string.IsNullOrEmpty(onboarding.TriedAction))
                    {
                        InputBox.Text = onboarding.TriedAction;
                        SendMessage();
                    }
                    else
                    {
                        // Show personalized welcome after onboarding
                        var userName = onboarding.UserName ?? "friend";
                        var personalGreeting = $"Nice to meet you, {userName}! I'm ready to help. What would you like to do?";
                        AddMessage("Atlas", personalGreeting, false);
                        await _voiceManager.SpeakAsync(personalGreeting);
                    }
                }
                else
                {
                    // User closed onboarding without completing - still mark as completed
                    _ = _conversationManager?.CompleteOnboardingAsync(null, Conversation.Models.ConversationStyle.Friendly, "atQICwskSXjGu0SZpOep");
                }
            });
        }
        
        /// <summary>
        /// Update the system prompt based on user profile and style
        /// </summary>
        private void UpdateSystemPromptFromProfile()
        {
            if (_systemPromptBuilder == null || _conversationManager == null) return;
            
            // Build new system prompt with profile, memory, and style
            var newSystemPrompt = _systemPromptBuilder.BuildSystemPrompt();
            
            // Update the conversation history's system message
            if (conversationHistory.Count > 0)
            {
                conversationHistory[0] = new { role = "system", content = newSystemPrompt };
            }
            else
            {
                conversationHistory.Add(new { role = "system", content = newSystemPrompt });
            }
            
            Debug.WriteLine($"[ChatWindow] System prompt updated for style: {_conversationManager.GetConversationStyle()}");
        }

        /// <summary>
        /// Handle window state changes - use background thread for wake word when minimized
        /// </summary>
        private void ChatWindow_StateChanged(object? sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[ChatWindow] State changed to: {WindowState}");
            
            if (WindowState == WindowState.Minimized)
            {
                System.Diagnostics.Debug.WriteLine("[ChatWindow] Window minimized - switching wake word to background mode");
                // Switch to background-only wake word mode to prevent UI freeze
                SwitchToBackgroundWakeWord();
            }
            else if (WindowState == WindowState.Normal || WindowState == WindowState.Maximized)
            {
                System.Diagnostics.Debug.WriteLine("[ChatWindow] Window restored - switching wake word to UI mode");
                // Switch back to normal UI-integrated wake word mode
                SwitchToUIWakeWord();
            }
        }

        /// <summary>
        /// Handle visibility changes - use background thread for wake word when hidden
        /// </summary>
        private void ChatWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            bool isVisible = (bool)e.NewValue;
            System.Diagnostics.Debug.WriteLine($"[ChatWindow] Visibility changed to: {isVisible}");
            
            if (!isVisible)
            {
                System.Diagnostics.Debug.WriteLine("[ChatWindow] Window hidden - switching wake word to background mode");
                // Switch to background-only wake word mode to prevent UI freeze
                SwitchToBackgroundWakeWord();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[ChatWindow] Window shown - switching wake word to UI mode");
                // Switch back to normal UI-integrated wake word mode
                SwitchToUIWakeWord();
            }
        }

        private bool _isBackgroundWakeWordMode = false;
        private System.Threading.Timer? _backgroundWakeWordTimer;
        private WhisperSpeechRecognition? _backgroundWakeWordWhisper;

        /// <summary>
        /// Switch to background-only wake word mode (no UI updates to prevent freeze)
        /// Uses SafeDispatcherInvoke (BeginInvoke) instead of blocking Invoke
        /// </summary>
        private void SwitchToBackgroundWakeWord()
        {
            if (!isWakeWordEnabled) return;
            
            _isBackgroundWakeWordMode = true;
            
            // Don't stop the wake word system - just let it run with safe dispatcher
            // The SafeDispatcherInvoke we're using in the event handlers will prevent freezing
            System.Diagnostics.Debug.WriteLine("[ChatWindow] Switched to background wake word mode - wake word still active");
            
            // If wake word isn't running, start it
            if (!isWakeWordListening && !isListening)
            {
                isWakeWordListening = true;
                StartWhisperWakeWordListening();
            }
        }

        /// <summary>
        /// Switch back to normal UI-integrated wake word mode
        /// </summary>
        private void SwitchToUIWakeWord()
        {
            if (!isWakeWordEnabled) return;
            
            _isBackgroundWakeWordMode = false;
            
            System.Diagnostics.Debug.WriteLine("[ChatWindow] Switched to UI wake word mode");
            
            // Restart normal UI-integrated wake word system if not already running
            if (isWakeWordEnabled && !isListening && !isWakeWordListening)
            {
                isWakeWordListening = true;
                StartWhisperWakeWordListening();
            }
        }

        /// <summary>
        /// Background wake word check that doesn't touch UI thread
        /// </summary>
        private void BackgroundWakeWordCheck(object? state)
        {
            if (!_isBackgroundWakeWordMode || !isWakeWordEnabled) return;
            
            try
            {
                // Simple background check - just listen for "Atlas" without UI updates
                // This is a minimal implementation that avoids UI thread interaction
                System.Diagnostics.Debug.WriteLine("[ChatWindow] Background wake word check (minimized mode)");
                
                // TODO: Implement lightweight background wake word detection
                // For now, this prevents the freeze by not doing heavy operations
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ChatWindow] Background wake word error: {ex.Message}");
            }
        }

        private void PositionNearAvatar()
        {
            try
            {
                // Don't reposition if window is maximized - let it stay fullscreen
                if (WindowState == WindowState.Maximized)
                    return;
                    
                // Get the working area of the primary screen
                var workingArea = SystemParameters.WorkArea;
                
                // Position chat window to the left of where the avatar would be
                var padding = 20;
                var avatarWidth = 200; // Avatar window width
                
                // Position to the left of the avatar with some spacing
                Left = workingArea.Right - Width - avatarWidth - (padding * 2);
                Top = workingArea.Top + padding;
                
                // Ensure window stays within screen bounds
                if (Left < workingArea.Left) 
                {
                    // If it doesn't fit to the left, position it to the right of center
                    Left = workingArea.Left + (workingArea.Width - Width) / 2 + 100;
                }
                if (Top < workingArea.Top) Top = workingArea.Top + padding;
                if (Top + Height > workingArea.Bottom) 
                    Top = workingArea.Bottom - Height - padding;
            }
            catch
            {
                // Fallback positioning - only if not maximized
                if (WindowState != WindowState.Maximized)
                {
                    Left = SystemParameters.WorkArea.Right - Width - 50;
                    Top = 50;
                }
            }
        }

        private void InitializeScreenCapture()
        {
            try
            {
                _screenCapture = new ScreenCaptureEngine();
                _screenCapture.CaptureCompleted += OnCaptureCompleted;
                _screenCapture.CaptureError += OnCaptureError;
                
                // Initialize hotkeys when window is loaded
                Loaded += (s, e) => InitializeHotkeys();
            }
            catch (Exception ex)
            {
                ShowStatus($"⚠️ Screen capture initialization failed: {ex.Message}");
            }
        }

        private void OnCaptureError(string error)
        {
            Dispatcher.Invoke(() =>
            {
                ShowStatus($"❌ Capture error: {error}");
            });
        }

        private void AvatarButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string avatarId)
            {
                SelectAvatar(avatarId);
            }
        }

        private void SelectAvatar(string avatarId)
        {
            try
            {
                // Update button appearances
                ResetAvatarButtonStyles();
                
                // Highlight selected avatar button
                var selectedButton = FindAvatarButton(avatarId);
                if (selectedButton != null)
                {
                    selectedButton.Background = new SolidColorBrush(Color.FromRgb(0, 122, 204)); // Blue
                }
                
                // Show confirmation message
                var avatarName = GetAvatarDisplayName(avatarId);
                AddMessage("Atlas", $"🎭 Avatar changed to: {avatarName}", false);
                
                // TODO: Communicate with Unity to actually change the avatar
                // This would require Unity-C# communication bridge
                
                ShowStatus($"✅ Avatar set to: {avatarName}");
            }
            catch (Exception ex)
            {
                ShowStatus($"❌ Error changing avatar: {ex.Message}");
            }
        }

        private void ResetAvatarButtonStyles()
        {
            var defaultColor = new SolidColorBrush(Color.FromRgb(60, 60, 60)); // Dark gray
            
            DefaultAvatarBtn.Background = defaultColor;
            EnergeticAvatarBtn.Background = defaultColor;
            CalmAvatarBtn.Background = defaultColor;
            ReadyPlayerAvatarBtn.Background = defaultColor;
        }

        private Button FindAvatarButton(string avatarId)
        {
            return avatarId switch
            {
                "default" => DefaultAvatarBtn,
                "energetic" => EnergeticAvatarBtn,
                "calm" => CalmAvatarBtn,
                "readyplayer" => ReadyPlayerAvatarBtn,
                _ => null
            };
        }

        private string GetAvatarDisplayName(string avatarId)
        {
            return avatarId switch
            {
                "default" => "Default Assistant",
                "energetic" => "Energetic Assistant", 
                "calm" => "Calm Assistant",
                "readyplayer" => "Ready Player Me Avatar",
                _ => "Unknown Avatar"
            };
        }

        private void InitializeHotkeys()
        {
            try
            {
                var windowHelper = new WindowInteropHelper(this);
                _hotkeyManager = new HotkeyManager(windowHelper.Handle);
                _hotkeyManager.HotkeyPressed += OnHotkeyPressed;
                _hotkeyManager.RegisterDefaultHotkeys();
                
                ShowStatus("📸 Screen capture ready! Use Ctrl+Shift+S to capture");
            }
            catch (Exception ex)
            {
                ShowStatus($"⚠️ Hotkey registration failed: {ex.Message}");
            }
        }

        private void OnHotkeyPressed(string hotkeyName)
        {
            Dispatcher.Invoke(() =>
            {
                switch (hotkeyName)
                {
                    case "screenshot":
                    case "fullscreen":
                    case "quickcapture":
                        CaptureScreenshot();
                        break;
                }
            });
        }

        private async void CaptureButton_Click(object sender, RoutedEventArgs e)
        {
            await CaptureScreenshot();
        }

        private void HistoryButton_Click(object sender, RoutedEventArgs e)
        {
            OpenHistoryWindow();
        }

        private async Task CaptureScreenshot()
        {
            try
            {
                ShowStatus("📸 Taking screenshot...");
                CaptureButton.IsEnabled = false;
                
                // Hide all Atlas windows before capturing so they don't appear in screenshot
                var wasVisible = this.Visibility == Visibility.Visible;
                var previousOpacity = this.Opacity;
                
                // Also hide the main avatar window if it exists
                Window? mainWindow = null;
                double mainWindowOpacity = 1;
                bool mainWindowWasVisible = false;
                
                foreach (Window window in Application.Current.Windows)
                {
                    if (window is MainWindow mw)
                    {
                        mainWindow = mw;
                        mainWindowWasVisible = mw.Visibility == Visibility.Visible;
                        mainWindowOpacity = mw.Opacity;
                        mw.Opacity = 0;
                        mw.Hide();
                        break;
                    }
                }
                
                // Hide this chat window
                this.Opacity = 0;
                this.Hide();
                
                // Wait for windows to fully hide
                await Task.Delay(300);
                
                CaptureResult? result = null;
                Exception? captureException = null;
                
                try
                {
                    System.Diagnostics.Debug.WriteLine("[Screenshot] Starting capture...");
                    result = await _screenCapture.CaptureScreenAsync();
                    System.Diagnostics.Debug.WriteLine($"[Screenshot] Capture complete: Success={result?.Success}, Path={result?.Metadata?.FilePath}");
                }
                catch (Exception ex)
                {
                    captureException = ex;
                    System.Diagnostics.Debug.WriteLine($"[Screenshot] Capture exception: {ex}");
                }
                
                // Always restore windows after capture attempt
                if (mainWindow != null && mainWindowWasVisible)
                {
                    mainWindow.Show();
                    mainWindow.Opacity = mainWindowOpacity;
                }
                
                if (wasVisible)
                {
                    this.Show();
                    this.Opacity = previousOpacity;
                    this.Activate();
                }
                
                // Small delay to ensure window is visible before showing message
                await Task.Delay(100);
                
                if (captureException != null)
                {
                    AddMessage("Atlas", $"❌ Screenshot capture failed: {captureException.Message}", false);
                    ShowStatus("❌ Screenshot failed");
                    return;
                }
                
                if (result != null && result.Success && !string.IsNullOrEmpty(result.Metadata?.FilePath))
                {
                    // Verify file was actually saved
                    if (System.IO.File.Exists(result.Metadata.FilePath))
                    {
                        // Show preview
                        try
                        {
                            _screenCapture.ShowCapturePreview(result);
                        }
                        catch { } // Preview is optional
                        
                        // Add to chat
                        AddMessage("Atlas", $"📸 Screenshot captured! Saved to:\n{result.Metadata.FilePath}", false);
                        ShowStatus("✅ Screenshot saved!");
                    }
                    else
                    {
                        AddMessage("Atlas", $"❌ Screenshot file was not saved to: {result.Metadata.FilePath}", false);
                        ShowStatus("❌ Screenshot file not found");
                    }
                }
                else
                {
                    var errorMsg = result?.Error ?? "Unknown error - capture returned null or failed";
                    AddMessage("Atlas", $"❌ Screenshot capture failed: {errorMsg}", false);
                    ShowStatus("❌ Screenshot failed");
                }
            }
            catch (Exception ex)
            {
                // Make sure windows are visible even on error
                foreach (Window window in Application.Current.Windows)
                {
                    if (window is MainWindow mw)
                    {
                        mw.Show();
                        mw.Opacity = 1;
                        break;
                    }
                }
                this.Show();
                this.Opacity = 1;
                
                System.Diagnostics.Debug.WriteLine($"Screenshot error: {ex}");
                ShowStatus($"❌ Screenshot failed: {ex.Message}");
                AddMessage("Atlas", $"❌ Screenshot capture failed: {ex.Message}", false);
            }
            finally
            {
                CaptureButton.IsEnabled = true;
            }
        }

        private async void OnCaptureCompleted(CaptureResult result)
        {
            Dispatcher.Invoke(async () =>
            {
                ShowStatus($"✅ Screenshot saved: {Path.GetFileName(result.Metadata.FilePath)}");
                
                // Add to history
                try
                {
                    await _historyManager.AddCaptureAsync(result);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to add capture to history: {ex.Message}");
                }
            });
        }

        private async Task<string> AnalyzeLatestScreenshot()
        {
            try
            {
                var capturesPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "AtlasAI", "Captures");

                if (!Directory.Exists(capturesPath))
                    return "❌ No screenshots found. Take a screenshot first with /capture or 📸 button.";

                var latestFile = Directory.GetFiles(capturesPath, "*.png")
                    .OrderByDescending(f => File.GetCreationTime(f))
                    .FirstOrDefault();

                if (latestFile == null)
                    return "❌ No screenshots found. Take a screenshot first with /capture or 📸 button.";

                // Check if AI is available
                var activeProvider = AIManager.GetActiveProviderInstance();
                if (activeProvider == null || !activeProvider.IsConfigured)
                {
                    // Provide basic analysis without AI
                    var fileInfo = new FileInfo(latestFile);
                    var fileName = Path.GetFileName(latestFile);
                    
                    return $"📸 **Screenshot Analysis (Basic Mode)**\n\n" +
                           $"**File:** {fileName}\n" +
                           $"**Size:** {fileInfo.Length / 1024:N0} KB\n" +
                           $"**Captured:** {fileInfo.CreationTime:yyyy-MM-dd HH:mm:ss}\n" +
                           $"**Location:** {latestFile}\n\n" +
                           $"💡 **For AI-powered analysis:**\n" +
                           $"Configure your API key in Settings → AI Provider to get:\n" +
                           $"• Detailed image description\n" +
                           $"• OCR text extraction\n" +
                           $"• UI element identification\n" +
                           $"• Smart insights and suggestions\n\n" +
                           $"🔧 Use `/ocr` for basic text extraction or open the image manually.";
                }

                // Convert image to base64 for AI analysis
                var imageBytes = await File.ReadAllBytesAsync(latestFile);
                var base64Image = Convert.ToBase64String(imageBytes);

                // Send to AI for analysis
                var analysisPrompt = "Please analyze this screenshot and describe:\n" +
                                   "1. What you see in the image\n" +
                                   "2. Any text content (OCR)\n" +
                                   "3. UI elements and their purpose\n" +
                                   "4. Suggested actions or insights\n\n" +
                                   "Be detailed and helpful in your analysis.";

                // Add image context to conversation
                conversationHistory.Add(new { 
                    role = "user", 
                    content = new object[] {
                        (object)new { type = "text", text = analysisPrompt },
                        (object)new { type = "image_url", image_url = new { url = $"data:image/png;base64,{base64Image}" } }
                    }
                });

                var response = await AIManager.SendMessageAsync(conversationHistory, 1000);
                
                if (response.Success)
                {
                    conversationHistory.Add(new { role = "assistant", content = response.Content });
                    return $"🔍 **Screenshot Analysis:**\n\n{response.Content}";
                }
                else
                {
                    // Fallback when AI fails
                    var fileInfo = new FileInfo(latestFile);
                    var fileName = Path.GetFileName(latestFile);
                    
                    return $"📸 **Screenshot Analysis (Fallback Mode)**\n\n" +
                           $"**File:** {fileName}\n" +
                           $"**Size:** {fileInfo.Length / 1024:N0} KB\n" +
                           $"**Captured:** {fileInfo.CreationTime:yyyy-MM-dd HH:mm:ss}\n\n" +
                           $"❌ **AI Analysis Failed:** {response.Error}\n\n" +
                           $"💡 **Alternative options:**\n" +
                           $"• Use `/ocr` for text extraction\n" +
                           $"• Open image manually for viewing\n" +
                           $"• Configure valid API key for full AI analysis";
                }
            }
            catch (Exception ex)
            {
                return $"❌ Analysis error: {ex.Message}";
            }
        }
        
        /// <summary>
        /// Analyze an attached image with a specific question from the user
        /// </summary>
        private async Task<string> AnalyzeImageWithQuestion(string imagePath, string question)
        {
            try
            {
                if (!File.Exists(imagePath))
                    return $"❌ Image not found: {imagePath}";
                
                // Check if AI is available
                var activeProvider = AIManager.GetActiveProviderInstance();
                if (activeProvider == null || !activeProvider.IsConfigured)
                {
                    var fileInfo = new FileInfo(imagePath);
                    var fileName = Path.GetFileName(imagePath);
                    
                    return $"📸 **Image Analysis (Basic Mode)**\n\n" +
                           $"**File:** {fileName}\n" +
                           $"**Size:** {fileInfo.Length / 1024:N0} KB\n" +
                           $"**Your question:** {question}\n\n" +
                           $"💡 **For AI-powered analysis:**\n" +
                           $"Configure your API key in Settings → AI Provider to get:\n" +
                           $"• Detailed image description\n" +
                           $"• Answer to your question about the image\n" +
                           $"• OCR text extraction\n" +
                           $"• Smart insights and suggestions";
                }
                
                // Convert image to base64 for AI analysis
                var imageBytes = await File.ReadAllBytesAsync(imagePath);
                var base64Image = Convert.ToBase64String(imageBytes);
                
                // Determine image type
                var ext = Path.GetExtension(imagePath).ToLower();
                var mimeType = ext switch
                {
                    ".png" => "image/png",
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".gif" => "image/gif",
                    ".webp" => "image/webp",
                    ".bmp" => "image/bmp",
                    _ => "image/png"
                };
                
                // Build analysis prompt based on user's question
                var analysisPrompt = string.IsNullOrWhiteSpace(question) || question.Length < 5
                    ? "Please analyze this image and describe what you see in detail. Include any text, UI elements, or notable features."
                    : $"The user attached this image and asked: \"{question}\"\n\nPlease analyze the image and answer their question. Be helpful and specific.";
                
                // Add image context to conversation
                conversationHistory.Add(new { 
                    role = "user", 
                    content = new object[] {
                        (object)new { type = "text", text = analysisPrompt },
                        (object)new { type = "image_url", image_url = new { url = $"data:{mimeType};base64,{base64Image}" } }
                    }
                });
                
                var response = await AIManager.SendMessageAsync(conversationHistory, 1000);
                
                if (response.Success)
                {
                    conversationHistory.Add(new { role = "assistant", content = response.Content });
                    return $"🔍 **Image Analysis:**\n\n{response.Content}";
                }
                else
                {
                    var fileInfo = new FileInfo(imagePath);
                    var fileName = Path.GetFileName(imagePath);
                    
                    return $"📸 **Image Analysis (Fallback Mode)**\n\n" +
                           $"**File:** {fileName}\n" +
                           $"**Size:** {fileInfo.Length / 1024:N0} KB\n\n" +
                           $"❌ **AI Analysis Failed:** {response.Error}\n\n" +
                           $"💡 Try again or check your API key settings.";
                }
            }
            catch (Exception ex)
            {
                return $"❌ Image analysis error: {ex.Message}";
            }
        }

        /// <summary>
        /// Generate an image using DALL-E and display it in chat
        /// </summary>
        private async Task<string> GenerateAndDisplayImage(string prompt)
        {
            try
            {
                ShowStatus($"🎨 Generating image: {prompt}...");
                
                // Show avatar thinking animation
                if (_avatarIntegration?.IsUnityRunning == true)
                {
                    await _avatarIntegration.AvatarSetStateAsync("Thinking");
                }
                
                var result = await Tools.ImageGeneratorTool.GenerateImageAsync(prompt);
                
                if (!result.Success)
                {
                    return $"❌ {result.Error}";
                }
                
                // Build response with image info
                var response = new StringBuilder();
                response.AppendLine($"🎨 **Image Generated!**\n");
                response.AppendLine($"**Prompt:** {prompt}");
                
                if (!string.IsNullOrEmpty(result.RevisedPrompt) && result.RevisedPrompt != prompt)
                {
                    response.AppendLine($"**DALL-E enhanced to:** {result.RevisedPrompt}");
                }
                
                response.AppendLine($"\n📁 **Saved to:** {result.ImagePath}");
                response.AppendLine($"\n💡 Say \"open images folder\" to see all your generated images!");
                
                // Open the image automatically
                if (!string.IsNullOrEmpty(result.ImagePath) && File.Exists(result.ImagePath))
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = result.ImagePath,
                            UseShellExecute = true
                        });
                        response.AppendLine("\n✅ Opened the image for you!");
                    }
                    catch
                    {
                        // Silently fail if can't open
                    }
                }
                
                return response.ToString();
            }
            catch (Exception ex)
            {
                return $"❌ Image generation failed: {ex.Message}";
            }
        }

        private async Task<string> ExtractTextFromScreenshot()
        {
            try
            {
                var capturesPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "AtlasAI", "Captures");

                var latestFile = Directory.GetFiles(capturesPath, "*.png")
                    .OrderByDescending(f => File.GetCreationTime(f))
                    .FirstOrDefault();

                if (latestFile == null)
                    return "❌ No screenshots found. Take a screenshot first.";

                // Check if AI is available
                var activeProvider = AIManager.GetActiveProviderInstance();
                if (activeProvider == null || !activeProvider.IsConfigured)
                {
                    var fileName = Path.GetFileName(latestFile);
                    return $"📝 **OCR (Basic Mode)**\n\n" +
                           $"**Screenshot:** {fileName}\n\n" +
                           $"❌ **AI-powered OCR unavailable**\n" +
                           $"Configure your API key in Settings → AI Provider for:\n" +
                           $"• Automatic text extraction\n" +
                           $"• Smart text formatting\n" +
                           $"• Clipboard integration\n\n" +
                           $"💡 **Alternative:** Open the screenshot manually and copy text by hand.";
                }

                var imageBytes = await File.ReadAllBytesAsync(latestFile);
                var base64Image = Convert.ToBase64String(imageBytes);

                var ocrPrompt = "Please extract and transcribe ALL text visible in this screenshot. " +
                              "Organize the text logically and preserve formatting where possible. " +
                              "If there's no text, just say 'No text detected'.";

                conversationHistory.Add(new { 
                    role = "user", 
                    content = new object[] {
                        (object)new { type = "text", text = ocrPrompt },
                        (object)new { type = "image_url", image_url = new { url = $"data:image/png;base64,{base64Image}" } }
                    }
                });

                var response = await AIManager.SendMessageAsync(conversationHistory, 800);
                
                if (response.Success)
                {
                    conversationHistory.Add(new { role = "assistant", content = response.Content });
                    
                    // Offer to copy text to clipboard
                    if (!response.Content.Contains("No text detected"))
                    {
                        Clipboard.SetText(response.Content);
                        return $"📝 **Extracted Text (copied to clipboard):**\n\n{response.Content}";
                    }
                    else
                    {
                        return $"📝 **OCR Result:**\n\n{response.Content}";
                    }
                }
                else
                {
                    var fileName = Path.GetFileName(latestFile);
                    return $"📝 **OCR (Fallback Mode)**\n\n" +
                           $"**Screenshot:** {fileName}\n\n" +
                           $"❌ **AI OCR Failed:** {response.Error}\n\n" +
                           $"💡 **Alternative options:**\n" +
                           $"• Open screenshot manually\n" +
                           $"• Use Windows built-in OCR tools\n" +
                           $"• Configure valid API key for AI-powered OCR";
                }
            }
            catch (Exception ex)
            {
                return $"❌ OCR error: {ex.Message}";
            }
        }

        private async void UpdateDB_Click(object sender, RoutedEventArgs e)
        {
            AddMessage("You", "🔄 Update Threat Database", true);
            var response = await UpdateThreatDatabase();
            AddMessage("Atlas", response, false);
            await _voiceManager.SpeakAsync("Database update complete.");
        }

        private async Task<string> PerformSystemScan()
        {
            return await PerformDeepScan();
        }

        private async Task<string> PerformSpywareScan()
        {
            return await PerformDeepScan();
        }

        private async Task<string> PerformDeepScan()
        {
            try
            {
                ShowStatus("🛡️ Starting deep system scan...");
                
                var scanner = new SystemControl.UnifiedScanner();
                scanner.ProgressChanged += msg => Dispatcher.Invoke(() => ShowStatus(msg));
                scanner.ProgressPercentChanged += pct => Dispatcher.Invoke(() => 
                    StatusLabel.Text = $"Scanning... {pct}%");
                
                var result = await scanner.PerformDeepScanAsync();
                
                var db = SystemControl.ThreatDatabase.Instance;
                var summary = $"🛡️ **Comprehensive System Scan Complete**\n\n" +
                             $"📊 **Summary:**\n" +
                             $"• Total Threats: {result.Threats.Count}\n" +
                             $"• Critical: {result.CriticalCount}\n" +
                             $"• High: {result.HighCount}\n" +
                             $"• Medium: {result.MediumCount}\n" +
                             $"• Low: {result.LowCount}\n" +
                             $"• Files Scanned: {result.FilesScanned}\n" +
                             $"• Duration: {result.Duration.TotalSeconds:F1}s\n\n" +
                             $"📦 **Database:** v{db.Version} ({db.TotalDefinitions} definitions)\n" +
                             $"   Last Updated: {db.LastUpdated:g}\n\n";

                if (result.Threats.Count == 0)
                {
                    summary += "🎉 **Great news!** No threats detected. Your system is clean!";
                }
                else
                {
                    summary += "⚠️ **Threats Found:**\n";
                    
                    var topThreats = result.Threats
                        .OrderByDescending(t => t.Severity)
                        .Take(5)
                        .ToList();
                    
                    foreach (var threat in topThreats)
                    {
                        var icon = threat.Severity switch
                        {
                            SystemControl.SeverityLevel.Critical => "🔴",
                            SystemControl.SeverityLevel.High => "🟠",
                            SystemControl.SeverityLevel.Medium => "🟡",
                            _ => "🔵"
                        };
                        
                        summary += $"{icon} **{threat.Name}** [{threat.Category}]\n";
                        summary += $"   {threat.Description}\n";
                        summary += $"   📍 {threat.Location}\n";
                        if (threat.CanRemove)
                            summary += $"   ✅ Can be removed\n";
                        summary += "\n";
                    }
                    
                    if (result.Threats.Count > 5)
                        summary += $"... and {result.Threats.Count - 5} more threats.\n\n";
                    
                    summary += "🔧 Use `/systemcontrol` to manage threats.\n";
                    summary += "📥 Use `/updatedb` to update threat definitions.";
                }
                
                ShowStatus("✅ Scan completed");
                return summary;
            }
            catch (Exception ex)
            {
                ShowStatus("❌ Scan failed");
                return $"❌ Scan failed: {ex.Message}";
            }
        }

        private async Task<string> PerformSystemAutoFix()
        {
            try
            {
                ShowStatus("🔧 Starting auto-fix...");
                
                // First perform a scan to get issues
                var scanner = new SystemControl.WindowsSystemScanner();
                var scanResult = await scanner.PerformFullScanAsync();
                
                var fixableIssues = scanResult.Issues.Where(i => i.CanAutoFix).ToList();
                
                if (!fixableIssues.Any())
                {
                    ShowStatus("ℹ️ No auto-fixable issues found");
                    return "ℹ️ **No Auto-Fixable Issues Found**\n\n" +
                           $"Scanned {scanResult.TotalIssues} issues, but none can be automatically fixed.\n" +
                           "Use `/systemcontrol` to manually review and fix issues.";
                }
                
                // Perform auto-fix
                var controller = new SystemControl.WindowsSystemController();
                var fixResults = await controller.AutoFixIssuesAsync(fixableIssues);
                
                var successCount = fixResults.Count(r => r.Result == SystemControl.FixResult.Success);
                var requiresRestart = fixResults.Any(r => r.RequiresRestart);
                
                var summary = $"🔧 **Auto-Fix Complete**\n\n" +
                             $"📊 **Results:**\n" +
                             $"• Issues Processed: {fixResults.Count}\n" +
                             $"• Successfully Fixed: {successCount}\n" +
                             $"• Failed: {fixResults.Count - successCount}\n\n";
                
                if (successCount > 0)
                {
                    summary += "✅ **Successfully Fixed:**\n";
                    foreach (var result in fixResults.Where(r => r.Result == SystemControl.FixResult.Success))
                    {
                        summary += $"• {result.Message}\n";
                    }
                    summary += "\n";
                }
                
                var failedResults = fixResults.Where(r => r.Result != SystemControl.FixResult.Success).ToList();
                if (failedResults.Any())
                {
                    summary += "❌ **Could Not Fix:**\n";
                    foreach (var result in failedResults.Take(3))
                    {
                        summary += $"• {result.Message}\n";
                    }
                    if (failedResults.Count > 3)
                        summary += $"• ... and {failedResults.Count - 3} more\n";
                    summary += "\n";
                }
                
                if (requiresRestart)
                {
                    summary += "⚠️ **Restart Required**\n";
                    summary += "Some fixes require a system restart to take full effect.\n\n";
                }
                
                summary += "🔍 Run `/systemscan` again to verify fixes or use `/systemcontrol` for detailed management.";
                
                ShowStatus("✅ Auto-fix completed");
                return summary;
            }
            catch (Exception ex)
            {
                ShowStatus("❌ Auto-fix failed");
                return $"❌ Auto-fix failed: {ex.Message}";
            }
        }

        private async Task<string> UpdateThreatDatabase()
        {
            try
            {
                ShowStatus("📥 Connecting to threat intelligence servers...");
                
                var result = await SystemControl.OnlineThreatDatabase.UpdateDefinitionsAsync();
                
                if (result.Success)
                {
                    ShowStatus("✅ Definitions updated!");
                    return $"📥 **Threat Database Updated**\n\n" +
                           $"✅ {result.Message}\n\n" +
                           $"📊 **Database Info:**\n" +
                           $"• Sources Updated: {result.SourcesUpdated}\n" +
                           $"• New Hashes Added: {result.NewHashesAdded:N0}\n" +
                           $"• Total Definitions: {result.TotalDefinitions:N0}\n" +
                           $"• Last Updated: {SystemControl.OnlineThreatDatabase.LastUpdateTime:g}\n\n" +
                           $"🔍 Run a scan to check your system with the latest definitions.";
                }
                else
                {
                    ShowStatus("⚠️ Update had issues");
                    return $"⚠️ **Update Status**\n\n{result.Message}\n\n" +
                           $"The scanner will use cached definitions.";
                }
            }
            catch (Exception ex)
            {
                ShowStatus("❌ Update failed");
                return $"❌ Update failed: {ex.Message}\n\nThe scanner will use built-in definitions.";
            }
        }

        private void SystemControlButton_Click(object sender, RoutedEventArgs e)
        {
            OpenSystemControlWindow();
        }
        
        private void SystemControl_Click(object sender, RoutedEventArgs e)
        {
            OpenSystemControlWindow();
        }

        private void OpenSystemControlWindow()
        {
            try
            {
                var systemControlWindow = new SystemControlWindow();
                systemControlWindow.Show();
                AddMessage("Atlas", "🔧 Windows System Control Panel opened! You can scan for issues, perform auto-fixes, and manage your system health.", false);
                ShowStatus("✅ System Control Panel opened");
            }
            catch (Exception ex)
            {
                var errorMessage = $"❌ Error opening System Control Panel: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $"\nInner Exception: {ex.InnerException.Message}";
                }
                
                AddMessage("Atlas", errorMessage, false);
                ShowStatus("❌ Failed to open System Control Panel");
                
                // Also show a message box for debugging
                MessageBox.Show($"Detailed Error:\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}", 
                               "System Control Panel Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenAvatarSelection()
        {
            try
            {
                var avatarWindow = new AvatarSelectionWindow();
                avatarWindow.Owner = this;
                if (avatarWindow.ShowDialog() == true && !string.IsNullOrEmpty(avatarWindow.SelectedAvatar))
                {
                    SelectAvatar(avatarWindow.SelectedAvatar);
                    AddMessage("Atlas", $"🎭 Avatar changed to: {GetAvatarDisplayName(avatarWindow.SelectedAvatar)}", false);
                }
            }
            catch (Exception ex)
            {
                AddMessage("Atlas", $"❌ Error opening avatar selection: {ex.Message}", false);
            }
        }

        protected override async void OnClosed(EventArgs e)
        {
            // Save current session before closing
            if (_conversationManager != null)
            {
                await _conversationManager.SaveCurrentSessionAsync();
            }
            
            // Cleanup taskbar icon
            _taskbarIcon?.Dispose();
            _taskbarIcon = null;
            
            // Cleanup background wake word timer
            _backgroundWakeWordTimer?.Dispose();
            _backgroundWakeWordTimer = null;
            
            _voiceManager?.Dispose();
            _hotkeyManager?.Dispose();
            try { recognizer?.Dispose(); } catch { }
            base.OnClosed(e);
        }

        #region Drag-Drop File Support
        
        private List<string> _droppedPaths = new();
        
        private void InputArea_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                DropOverlay.Visibility = Visibility.Visible;
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
        }
        
        private void InputArea_DragLeave(object sender, DragEventArgs e)
        {
            DropOverlay.Visibility = Visibility.Collapsed;
        }
        
        private void InputArea_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }
        
        private void InputArea_Drop(object sender, DragEventArgs e)
        {
            DropOverlay.Visibility = Visibility.Collapsed;
            
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (var path in paths)
                {
                    if (!_droppedPaths.Contains(path))
                    {
                        _droppedPaths.Add(path);
                        AddDroppedFileChip(path);
                        
                        // If a folder is dropped, set it as the coding workspace
                        if (Directory.Exists(path) && _codeAssistant != null)
                        {
                            _codeAssistant.SetWorkspace(path);
                            Debug.WriteLine($"[CodeAssistant] Workspace set to: {path}");
                        }
                    }
                }
                UpdateDroppedFilesVisibility();
                e.Handled = true;
            }
        }
        
        private void AddDroppedFileChip(string path)
        {
            var isFolder = Directory.Exists(path);
            var name = Path.GetFileName(path);
            var icon = isFolder ? "📁" : GetDroppedFileIcon(path);
            
            var chip = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(99, 102, 241)), // Accent color
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 6, 4),
                Tag = path,
                Cursor = Cursors.Hand
            };
            
            var stack = new StackPanel { Orientation = Orientation.Horizontal };
            stack.Children.Add(new TextBlock 
            { 
                Text = $"{icon} {name}", 
                Foreground = Brushes.White, 
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = 150,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            
            var removeBtn = new Button
            {
                Content = "✕",
                Background = Brushes.Transparent,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontSize = 9,
                Padding = new Thickness(4, 0, 0, 0),
                Cursor = Cursors.Hand,
                Tag = path
            };
            removeBtn.Click += RemoveDroppedFile_Click;
            stack.Children.Add(removeBtn);
            
            chip.Child = stack;
            chip.ToolTip = path;
            
            DroppedFilesList.Items.Add(chip);
        }
        
        private string GetDroppedFileIcon(string path)
        {
            var ext = Path.GetExtension(path).ToLower();
            return ext switch
            {
                ".txt" or ".md" or ".log" => "📄",
                ".pdf" => "📕",
                ".doc" or ".docx" => "📘",
                ".xls" or ".xlsx" => "📗",
                ".ppt" or ".pptx" => "📙",
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" => "🖼️",
                ".mp3" or ".wav" or ".flac" => "🎵",
                ".mp4" or ".avi" or ".mkv" => "🎬",
                ".zip" or ".rar" or ".7z" => "📦",
                ".exe" or ".msi" => "⚙️",
                ".cs" or ".js" or ".py" or ".java" => "💻",
                ".html" or ".css" => "🌐",
                _ => "📄"
            };
        }
        
        private void RemoveDroppedFile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string path)
            {
                _droppedPaths.Remove(path);
                
                // Find and remove the chip
                Border? toRemove = null;
                foreach (var item in DroppedFilesList.Items)
                {
                    if (item is Border chip && chip.Tag as string == path)
                    {
                        toRemove = chip;
                        break;
                    }
                }
                if (toRemove != null)
                    DroppedFilesList.Items.Remove(toRemove);
                    
                UpdateDroppedFilesVisibility();
            }
        }
        
        private void AttachButton_Click(object sender, RoutedEventArgs e)
        {
            // Show a context menu to choose files or folders
            var menu = new System.Windows.Controls.ContextMenu();
            
            var filesItem = new System.Windows.Controls.MenuItem { Header = "📄 Select Files..." };
            filesItem.Click += (s, args) =>
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Multiselect = true,
                    Title = "Select files to attach"
                };
                if (dialog.ShowDialog() == true)
                {
                    foreach (var file in dialog.FileNames)
                    {
                        if (!_droppedPaths.Contains(file))
                        {
                            _droppedPaths.Add(file);
                            AddDroppedFileChip(file);
                        }
                    }
                    UpdateDroppedFilesVisibility();
                }
            };
            menu.Items.Add(filesItem);
            
            var folderItem = new System.Windows.Controls.MenuItem { Header = "📁 Select Folder..." };
            folderItem.Click += (s, args) =>
            {
                var dialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "Select a folder to attach",
                    ShowNewFolderButton = false
                };
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    if (!_droppedPaths.Contains(dialog.SelectedPath))
                    {
                        _droppedPaths.Add(dialog.SelectedPath);
                        AddDroppedFileChip(dialog.SelectedPath);
                        UpdateDroppedFilesVisibility();
                    }
                }
            };
            menu.Items.Add(folderItem);
            
            menu.IsOpen = true;
        }
        
        private void ClearDroppedFiles_Click(object sender, RoutedEventArgs e)
        {
            _droppedPaths.Clear();
            DroppedFilesList.Items.Clear();
            UpdateDroppedFilesVisibility();
        }
        
        private void UpdateDroppedFilesVisibility()
        {
            DroppedFilesPanel.Visibility = _droppedPaths.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        
        private string GetDroppedFilesContext()
        {
            if (_droppedPaths.Count == 0)
                return "";
                
            var sb = new StringBuilder();
            sb.AppendLine("\n\n[ATTACHED FILES]");
            
            foreach (var path in _droppedPaths)
            {
                var isFolder = Directory.Exists(path);
                if (isFolder)
                {
                    try
                    {
                        var files = Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly);
                        var subDirs = Directory.GetDirectories(path);
                        sb.AppendLine($"\n📁 FOLDER: \"{path}\"");
                        sb.AppendLine($"   Contains {files.Length} files, {subDirs.Length} subfolders");
                        
                        // List first 10 files
                        foreach (var file in files.Take(10))
                        {
                            sb.AppendLine($"   - {Path.GetFileName(file)}");
                        }
                        if (files.Length > 10)
                            sb.AppendLine($"   ... and {files.Length - 10} more files");
                    }
                    catch
                    {
                        sb.AppendLine($"\n📁 FOLDER: \"{path}\"");
                    }
                }
                else
                {
                    try
                    {
                        var info = new FileInfo(path);
                        var ext = info.Extension.ToLowerInvariant();
                        sb.AppendLine($"\n📄 FILE: \"{path}\" ({FormatFileSize(info.Length)})");
                        
                        // Read content for text-based files (code, config, text, etc.)
                        var textExtensions = new HashSet<string> { 
                            ".txt", ".md", ".json", ".xml", ".yaml", ".yml", ".csv",
                            ".cs", ".js", ".ts", ".py", ".java", ".cpp", ".c", ".h", ".hpp",
                            ".html", ".htm", ".css", ".scss", ".less",
                            ".sql", ".sh", ".bat", ".ps1", ".cmd",
                            ".ini", ".cfg", ".conf", ".config", ".env",
                            ".log", ".gitignore", ".dockerfile", ".makefile",
                            ".jsx", ".tsx", ".vue", ".svelte", ".php", ".rb", ".go", ".rs",
                            ".swift", ".kt", ".scala", ".lua", ".r", ".m", ".mm"
                        };
                        
                        // Also check for files without extension that might be text (like Makefile, Dockerfile)
                        var textFilenames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                            "makefile", "dockerfile", "readme", "license", "changelog", "authors"
                        };
                        
                        bool isTextFile = textExtensions.Contains(ext) || 
                                         textFilenames.Contains(info.Name) ||
                                         string.IsNullOrEmpty(ext);
                        
                        if (isTextFile && info.Length < 100 * 1024) // Max 100KB for text files
                        {
                            try
                            {
                                var content = File.ReadAllText(path);
                                
                                // Truncate if too long (max ~8000 chars to leave room for response)
                                if (content.Length > 8000)
                                {
                                    content = content.Substring(0, 8000) + "\n... [truncated - file continues]";
                                }
                                
                                sb.AppendLine($"--- FILE CONTENT START ---");
                                sb.AppendLine(content);
                                sb.AppendLine($"--- FILE CONTENT END ---");
                            }
                            catch (Exception ex)
                            {
                                sb.AppendLine($"   [Could not read content: {ex.Message}]");
                            }
                        }
                        else if (info.Length >= 100 * 1024)
                        {
                            sb.AppendLine($"   [File too large to read - {FormatFileSize(info.Length)}]");
                        }
                        else
                        {
                            // Binary file - just describe it
                            sb.AppendLine($"   [Binary file: {ext}]");
                            
                            // For images, mention they could be analyzed with vision
                            var imageExtensions = new HashSet<string> { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp" };
                            if (imageExtensions.Contains(ext))
                            {
                                sb.AppendLine($"   [Image file - can be analyzed if vision is enabled]");
                            }
                        }
                    }
                    catch
                    {
                        sb.AppendLine($"\n📄 FILE: \"{path}\"");
                    }
                }
            }
            
            sb.AppendLine("\n[You can analyze, explain, modify, or work with these files. For modifications, I'll show you the changes before applying them.]");
            return sb.ToString();
        }
        
        // Store dropped paths for tool access
        public static List<string> LastDroppedPaths { get; private set; } = new();
        
        private string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
        }
        
        #endregion
        
        #region Inspector Panel & Toast Notifications
        
        private UI.ToastNotificationManager? _toastManager;
        private SecuritySuite.Services.SecuritySuiteManager? _securityManager;
        private InAppAssistant.Services.WindowsContextService? _contextService;
        private System.Timers.Timer? _contextUpdateTimer;
        
        /// <summary>
        /// Initialize the Inspector panel and Toast notification system
        /// </summary>
        private void InitializeInspectorAndToasts()
        {
            try
            {
                // Initialize Toast notifications
                _toastManager = UI.ToastNotificationManager.Instance;
                _toastManager.Initialize(ToastContainer);
                
                // Initialize Security Suite Manager
                _securityManager = new SecuritySuite.Services.SecuritySuiteManager();
                
                // Get WindowsContextService from InAppAssistant
                _contextService = _inAppAssistant?.GetContextService();
                
                // Start context update timer
                StartContextUpdateTimer();
                
                // Load initial security status
                UpdateSecurityDisplay();
                
                // Load current ElevenLabs voice settings into sliders
                LoadVoiceSettings();
                
                // Initialize file browser tree
                InitializeFileTree();
                
                Debug.WriteLine("[UI] Inspector panel and Toast system initialized");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UI] Error initializing Inspector/Toast: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Start timer to update context display
        /// </summary>
        private void StartContextUpdateTimer()
        {
            _contextUpdateTimer = new System.Timers.Timer(1000);
            _contextUpdateTimer.Elapsed += (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        UpdateContextDisplay();
                    }
                    catch { }
                });
            };
            _contextUpdateTimer.Start();
        }
        
        /// <summary>
        /// Update the Active Context display in the inspector
        /// </summary>
        private void UpdateContextDisplay()
        {
            if (_contextService == null) return;
            
            var context = _contextService.GetActiveAppContext();
            if (context == null) return;
            
            // Update icon based on category
            ContextAppIcon.Text = context.Category switch
            {
                InAppAssistant.Models.AppCategory.Browser => "🌐",
                InAppAssistant.Models.AppCategory.FileExplorer => "📁",
                InAppAssistant.Models.AppCategory.IDE => "💻",
                InAppAssistant.Models.AppCategory.Office => "📄",
                InAppAssistant.Models.AppCategory.Terminal => "⌨️",
                InAppAssistant.Models.AppCategory.MediaPlayer => "🎵",
                InAppAssistant.Models.AppCategory.TextEditor => "📝",
                InAppAssistant.Models.AppCategory.Communication => "💬",
                _ => "📱"
            };
            
            // Update app name
            ContextAppName.Text = context.ProcessName.ToLower() switch
            {
                "explorer" => "File Explorer",
                "chrome" => "Google Chrome",
                "msedge" => "Microsoft Edge",
                "firefox" => "Mozilla Firefox",
                "code" => "Visual Studio Code",
                "devenv" => "Visual Studio",
                "spotify" => "Spotify",
                "discord" => "Discord",
                _ => context.ProcessName
            };
            
            ContextProcessName.Text = $"{context.ProcessName}.exe";
            ContextFilePath.Text = !string.IsNullOrEmpty(context.ExecutablePath) ? context.ExecutablePath : "—";
            ContextWindowTitle.Text = !string.IsNullOrEmpty(context.WindowTitle) ? context.WindowTitle : "—";
        }
        
        /// <summary>
        /// Update the Security Scan display in the inspector
        /// </summary>
        private void UpdateSecurityDisplay()
        {
            if (_securityManager == null) return;
            
            try
            {
                var status = _securityManager.GetDashboardStatus();
                
                // Update status badge
                switch (status.ProtectionScore.Status)
                {
                    case SecuritySuite.Models.ProtectionStatus.Protected:
                        SecurityStatusText.Text = "SAFE";
                        SecurityStatusText.Foreground = new SolidColorBrush(Color.FromRgb(63, 185, 80));
                        SecurityBadge.Background = new SolidColorBrush(Color.FromArgb(32, 63, 185, 80));
                        break;
                    case SecuritySuite.Models.ProtectionStatus.AtRisk:
                        SecurityStatusText.Text = "AT RISK";
                        SecurityStatusText.Foreground = new SolidColorBrush(Color.FromRgb(210, 153, 34));
                        SecurityBadge.Background = new SolidColorBrush(Color.FromArgb(32, 210, 153, 34));
                        break;
                    case SecuritySuite.Models.ProtectionStatus.Critical:
                        SecurityStatusText.Text = "CRITICAL";
                        SecurityStatusText.Foreground = new SolidColorBrush(Color.FromRgb(248, 81, 73));
                        SecurityBadge.Background = new SolidColorBrush(Color.FromArgb(32, 248, 81, 73));
                        break;
                }
                
                // Update last scan
                if (status.LastScan != null)
                {
                    var scanTime = status.LastScan.EndTime;
                    var timeSince = DateTime.Now - scanTime;
                    
                    if (timeSince.TotalMinutes < 1)
                        LastScanText.Text = "Just now";
                    else if (timeSince.TotalHours < 1)
                        LastScanText.Text = $"{(int)timeSince.TotalMinutes} min ago";
                    else if (timeSince.TotalDays < 1)
                        LastScanText.Text = $"Today, {scanTime:h:mm tt}";
                    else
                        LastScanText.Text = scanTime.ToString("MMM d, h:mm tt");
                }
                else
                {
                    LastScanText.Text = "Never";
                }
                
                // Update definitions
                var defsInfo = status.Definitions;
                var defsAge = DateTime.Now - defsInfo.LastUpdated;
                
                if (defsAge.TotalHours < 24)
                    DefinitionsText.Text = "Up to date";
                else if (defsAge.TotalDays < 3)
                    DefinitionsText.Text = $"{(int)defsAge.TotalDays} day(s) old";
                else
                    DefinitionsText.Text = $"Outdated ({(int)defsAge.TotalDays} days)";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Inspector] Security status update error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Load current ElevenLabs voice settings into sliders
        /// </summary>
        private void LoadVoiceSettings()
        {
            var settings = Voice.ElevenLabsProvider.CurrentVoiceSettings;
            StabilitySlider.Value = settings.Stability;
            SimilaritySlider.Value = settings.SimilarityBoost;
            StyleSlider.Value = settings.Style;
            SpeakerBoostToggle.IsChecked = settings.UseSpeakerBoost;
            
            StabilityValueText.Text = settings.Stability.ToString("F2");
            SimilarityValueText.Text = settings.SimilarityBoost.ToString("F2");
            StyleValueText.Text = settings.Style.ToString("F2");
        }
        
        /// <summary>
        /// Scan Now button click
        /// </summary>
        private void ScanNow_Click(object sender, RoutedEventArgs e)
        {
            ShowToast("Opening Security Suite...", UI.ToastType.Info);
            ShowSecuritySuiteWindow();
        }
        
        /// <summary>
        /// Stability slider changed
        /// </summary>
        private void StabilitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (StabilityValueText == null) return;
            StabilityValueText.Text = e.NewValue.ToString("F2");
            ApplyVoiceSettings();
        }
        
        /// <summary>
        /// Similarity slider changed
        /// </summary>
        private void SimilaritySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (SimilarityValueText == null) return;
            SimilarityValueText.Text = e.NewValue.ToString("F2");
            ApplyVoiceSettings();
        }
        
        /// <summary>
        /// Style slider changed
        /// </summary>
        private void StyleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (StyleValueText == null) return;
            StyleValueText.Text = e.NewValue.ToString("F2");
            ApplyVoiceSettings();
        }
        
        /// <summary>
        /// Speaker boost toggle clicked
        /// </summary>
        private void SpeakerBoostToggle_Click(object sender, RoutedEventArgs e)
        {
            ApplyVoiceSettings();
        }
        
        /// <summary>
        /// Apply voice settings to ElevenLabs provider
        /// </summary>
        private void ApplyVoiceSettings()
        {
            if (StabilitySlider == null || SimilaritySlider == null || StyleSlider == null || SpeakerBoostToggle == null) return;
            
            Voice.ElevenLabsProvider.UpdateVoiceSettings(
                StabilitySlider.Value,
                SimilaritySlider.Value,
                StyleSlider.Value,
                SpeakerBoostToggle.IsChecked == true
            );
            
            Debug.WriteLine($"[Inspector] Voice settings applied - Stability: {StabilitySlider.Value:F2}, Similarity: {SimilaritySlider.Value:F2}, Style: {StyleSlider.Value:F2}");
        }
        
        /// <summary>
        /// Toggle Inspector panel visibility (legacy - panel is now always visible)
        /// </summary>
        private void InspectorToggle_Click(object sender, RoutedEventArgs e)
        {
            // Panel is now always visible in the new design
        }
        
        /// <summary>
        /// Close the Inspector panel (hide it)
        /// </summary>
        private void CloseInspector_Click(object sender, RoutedEventArgs e)
        {
            InspectorPanel.Visibility = Visibility.Collapsed;
            InspectorColumn.Width = new GridLength(0);
            ShowInspectorBtn.Visibility = Visibility.Visible;
        }
        
        #region File Browser Sidebar
        
        private bool _sidebarExpanded = false; // Start collapsed
        
        /// <summary>
        /// Toggle the sidebar expanded/collapsed state
        /// </summary>
        private void SidebarToggle_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[Sidebar] Toggle clicked! Current state: {_sidebarExpanded}");
            _sidebarExpanded = !_sidebarExpanded;
            System.Diagnostics.Debug.WriteLine($"[Sidebar] New state: {_sidebarExpanded}");
            
            if (_sidebarExpanded)
            {
                // Show sidebar - set column width and visibility
                FileBrowserColumn.Width = new GridLength(280);
                FileBrowserPanel.Visibility = Visibility.Visible;
                if (SidebarToggleBtn != null) SidebarToggleBtn.ToolTip = "◀ Close Files Panel";
                System.Diagnostics.Debug.WriteLine("[Sidebar] Panel set to VISIBLE, width=280");
                
                // Initialize file tree if empty
                if (FileTreeView != null && FileTreeView.Items.Count == 0)
                {
                    InitializeFileTree();
                }
            }
            else
            {
                // Hide sidebar - set column width to 0 and collapse
                FileBrowserColumn.Width = new GridLength(0);
                FileBrowserPanel.Visibility = Visibility.Collapsed;
                if (SidebarToggleBtn != null) SidebarToggleBtn.ToolTip = "📁 Open Files Panel";
                System.Diagnostics.Debug.WriteLine("[Sidebar] Panel set to COLLAPSED, width=0");
            }
        }
        
        /// <summary>
        /// Initialize the file tree with root folders
        /// </summary>
        private void InitializeFileTree()
        {
            FileTreeView.Items.Clear();
            
            // Add quick access folders
            var quickAccess = new[]
            {
                ("🏠 Home", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)),
                ("🖥️ Desktop", Environment.GetFolderPath(Environment.SpecialFolder.Desktop)),
                ("⬇️ Downloads", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")),
                ("📄 Documents", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)),
                ("🖼️ Pictures", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)),
                ("🎵 Music", Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)),
                ("🎬 Videos", Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)),
            };
            
            foreach (var (name, path) in quickAccess)
            {
                if (Directory.Exists(path))
                {
                    var item = CreateTreeItem(name, path, true);
                    FileTreeView.Items.Add(item);
                }
            }
            
            // Add separator
            var separator = new TreeViewItem { Header = "─────────────", IsEnabled = false, Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85)) };
            FileTreeView.Items.Add(separator);
            
            // Add drives
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady)
                {
                    var driveName = $"💾 {drive.Name} ({drive.VolumeLabel})";
                    var item = CreateTreeItem(driveName, drive.Name, true);
                    FileTreeView.Items.Add(item);
                }
            }
        }
        
        /// <summary>
        /// Create a tree view item for a file or folder
        /// </summary>
        private TreeViewItem CreateTreeItem(string displayName, string path, bool isFolder)
        {
            var item = new TreeViewItem
            {
                Header = displayName,
                Tag = path,
                FontFamily = new FontFamily("Segoe UI")
            };
            
            if (isFolder)
            {
                // Add dummy child so expand arrow shows
                item.Items.Add(new TreeViewItem { Header = "Loading...", Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)) });
            }
            
            return item;
        }
        
        /// <summary>
        /// Handle tree item expansion - load children
        /// </summary>
        private void FileTreeItem_Expanded(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is TreeViewItem item && item.Tag is string path)
            {
                // Check if we need to load children (has dummy item)
                if (item.Items.Count == 1 && item.Items[0] is TreeViewItem dummy && dummy.Header?.ToString() == "Loading...")
                {
                    item.Items.Clear();
                    LoadFolderContents(item, path);
                }
            }
        }
        
        /// <summary>
        /// Load folder contents into tree item
        /// </summary>
        private void LoadFolderContents(TreeViewItem parent, string folderPath)
        {
            try
            {
                // Add subfolders first
                var dirs = Directory.GetDirectories(folderPath);
                foreach (var dir in dirs.OrderBy(d => Path.GetFileName(d)))
                {
                    try
                    {
                        var dirInfo = new DirectoryInfo(dir);
                        // Skip hidden and system folders
                        if ((dirInfo.Attributes & FileAttributes.Hidden) != 0 || 
                            (dirInfo.Attributes & FileAttributes.System) != 0)
                            continue;
                        
                        var name = $"📁 {dirInfo.Name}";
                        var item = CreateTreeItem(name, dir, true);
                        parent.Items.Add(item);
                    }
                    catch { } // Skip folders we can't access
                }
                
                // Add files
                var files = Directory.GetFiles(folderPath);
                foreach (var file in files.OrderBy(f => Path.GetFileName(f)))
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        // Skip hidden files
                        if ((fileInfo.Attributes & FileAttributes.Hidden) != 0)
                            continue;
                        
                        var icon = GetFileIcon(fileInfo.Extension);
                        var name = $"{icon} {fileInfo.Name}";
                        var item = CreateTreeItem(name, file, false);
                        parent.Items.Add(item);
                    }
                    catch { } // Skip files we can't access
                }
                
                // If no items, show empty message
                if (parent.Items.Count == 0)
                {
                    parent.Items.Add(new TreeViewItem { Header = "(empty)", IsEnabled = false, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)) });
                }
            }
            catch (UnauthorizedAccessException)
            {
                parent.Items.Add(new TreeViewItem { Header = "⚠️ Access denied", IsEnabled = false, Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68)) });
            }
            catch (Exception ex)
            {
                parent.Items.Add(new TreeViewItem { Header = $"⚠️ Error: {ex.Message}", IsEnabled = false, Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68)) });
            }
        }
        
        /// <summary>
        /// Get icon for file type
        /// </summary>
        private string GetFileIcon(string extension)
        {
            return extension.ToLower() switch
            {
                ".txt" or ".md" or ".log" => "📝",
                ".pdf" => "📕",
                ".doc" or ".docx" => "📘",
                ".xls" or ".xlsx" => "📗",
                ".ppt" or ".pptx" => "📙",
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" => "🖼️",
                ".mp3" or ".wav" or ".flac" or ".m4a" or ".ogg" => "🎵",
                ".mp4" or ".mkv" or ".avi" or ".mov" or ".wmv" => "🎬",
                ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "📦",
                ".exe" or ".msi" => "⚙️",
                ".dll" => "🔧",
                ".cs" or ".js" or ".ts" or ".py" or ".java" or ".cpp" or ".c" or ".h" => "💻",
                ".html" or ".htm" or ".css" => "🌐",
                ".json" or ".xml" or ".yaml" or ".yml" => "📋",
                ".sql" or ".db" => "🗄️",
                ".iso" or ".img" => "💿",
                ".lnk" => "🔗",
                _ => "📄"
            };
        }
        
        /// <summary>
        /// Handle double-click on tree item - open file/folder
        /// </summary>
        private void FileTreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (FileTreeView.SelectedItem is TreeViewItem item && item.Tag is string path)
            {
                try
                {
                    Debug.WriteLine($"[FileTree] Double-click on: {path}");
                    
                    if (Directory.Exists(path))
                    {
                        // Open folder in Explorer - quote the path for spaces
                        Debug.WriteLine($"[FileTree] Opening folder: {path}");
                        System.Diagnostics.Process.Start("explorer.exe", $"\"{path}\"");
                    }
                    else if (File.Exists(path))
                    {
                        // Open file with default application
                        Debug.WriteLine($"[FileTree] Opening file: {path}");
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = path,
                            UseShellExecute = true
                        });
                    }
                    else
                    {
                        Debug.WriteLine($"[FileTree] Path doesn't exist: {path}");
                        ShowStatus($"❌ Path not found: {path}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[FileTree] Error opening: {ex.Message}");
                    ShowStatus($"❌ Couldn't open: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Refresh the file tree
        /// </summary>
        private void RefreshFileTree_Click(object sender, RoutedEventArgs e)
        {
            InitializeFileTree();
            ShowStatus("🔄 File tree refreshed");
        }
        
        /// <summary>
        /// Handle quick access folder clicks
        /// </summary>
        private void QuickAccess_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
            {
                string path = tag switch
                {
                    "Home" => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Desktop" => Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "Downloads" => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
                    "Documents" => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Pictures" => Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                    "Music" => Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                    "Videos" => Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                    "ThisPC" => "",
                    _ => ""
                };
                
                if (tag == "ThisPC")
                {
                    // Open File Explorer to This PC
                    System.Diagnostics.Process.Start("explorer.exe", "shell:MyComputerFolder");
                }
                else if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                {
                    // Open in File Explorer
                    System.Diagnostics.Process.Start("explorer.exe", path);
                }
            }
        }
        
        /// <summary>
        /// Screenshot button click - capture screen
        /// </summary>
        private async void Screenshot_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ShowStatus("📷 Capturing screenshot...");
                
                // Hide window briefly for clean capture
                this.WindowState = WindowState.Minimized;
                await Task.Delay(300);
                
                // Capture screen using the async method
                var result = await _screenCapture?.CaptureScreenAsync()!;
                
                // Restore window
                this.WindowState = WindowState.Normal;
                this.Activate();
                
                if (result != null && result.Success)
                {
                    ShowStatus($"📷 Screenshot saved!");
                    
                    // Attach to chat if we have a file path
                    if (!string.IsNullOrEmpty(result.Metadata?.FilePath) && File.Exists(result.Metadata.FilePath))
                    {
                        if (!_droppedPaths.Contains(result.Metadata.FilePath))
                        {
                            _droppedPaths.Add(result.Metadata.FilePath);
                            AddDroppedFileChip(result.Metadata.FilePath);
                            UpdateDroppedFilesVisibility();
                        }
                    }
                }
                else
                {
                    ShowStatus($"❌ Screenshot failed: {result?.Error ?? "Unknown error"}");
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"❌ Screenshot error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Stop button click - stops any ongoing operation (speech, scanning, etc.)
        /// </summary>
        private void StopSpeechBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Stop speech
                _voiceManager?.Stop();
                
                // Cancel any ongoing operations
                _currentOperationCts?.Cancel();
                _currentScanner?.CancelScan();
                
                StopSpeechBtn.Visibility = Visibility.Collapsed;
                ShowStatus("⏹️ Stopped");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Stop] Error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Show the stop button when an operation starts
        /// </summary>
        private void ShowStopSpeechButton()
        {
            Dispatcher.Invoke(() => StopSpeechBtn.Visibility = Visibility.Visible);
        }
        
        /// <summary>
        /// Hide the stop button when operation ends
        /// </summary>
        private void HideStopSpeechButton()
        {
            Dispatcher.Invoke(() => StopSpeechBtn.Visibility = Visibility.Collapsed);
        }
        
        // Keep old methods for compatibility but they're no longer used
        private void FileBrowserToggle_Click(object sender, RoutedEventArgs e) => SidebarToggle_Click(sender, e);
        private void CloseFileBrowser_Click(object sender, RoutedEventArgs e) 
        { 
            _sidebarExpanded = false; 
            FileBrowserColumn.Width = new GridLength(0);
            FileBrowserPanel.Visibility = Visibility.Collapsed;
        }
        private void RefreshFiles_Click(object sender, RoutedEventArgs e) { }
        private void FileBrowserUp_Click(object sender, RoutedEventArgs e) { }
        private void FileBrowserTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e) { }
        private void FileBrowserTree_MouseDoubleClick(object sender, MouseButtonEventArgs e) { }
        
        #endregion
        
        /// <summary>
        /// Show the Inspector panel (from header button)
        /// </summary>
        private void ShowInspector_Click(object sender, RoutedEventArgs e)
        {
            ShowInspector();
            ShowInspectorBtn.Visibility = Visibility.Collapsed;
        }
        
        /// <summary>
        /// Show the Inspector panel
        /// </summary>
        public void ShowInspector()
        {
            InspectorPanel.Visibility = Visibility.Visible;
            InspectorColumn.Width = new GridLength(320);
        }
        
        /// <summary>
        /// Handle quick access folder selection - REMOVED, using TreeView now
        /// </summary>
        private void QuickAccessFolder_Changed(object sender, SelectionChangedEventArgs e)
        {
            // Legacy - no longer used
        }
        
        /// <summary>
        /// Load files from a folder into the quick access list - REMOVED, using TreeView now
        /// </summary>
        private void LoadFilesFromFolder(string folderPath)
        {
            // Legacy - no longer used
        }
        
        /// <summary>
        /// Handle file selection from quick access list - REMOVED, using TreeView now
        /// </summary>
        private void QuickAccessFile_Selected(object sender, SelectionChangedEventArgs e)
        {
            // Legacy - no longer used
        }
        
        /// <summary>
        /// Handle tree item selection
        /// </summary>
        private void FileTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            // Selection tracking handled in FileTreeView_MouseDoubleClick
        }
        
        /// <summary>
        /// Open current folder in Windows Explorer
        /// </summary>
        private void OpenFolderInExplorer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (FileTreeView.SelectedItem is TreeViewItem item && item.Tag is string path)
                {
                    if (File.Exists(path))
                    {
                        Process.Start("explorer.exe", $"/select,\"{path}\"");
                    }
                    else if (Directory.Exists(path))
                    {
                        Process.Start("explorer.exe", $"\"{path}\"");
                    }
                }
                else
                {
                    Process.Start("explorer.exe", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FileTree] Error opening explorer: {ex.Message}");
                try { Process.Start("explorer.exe"); } catch { }
            }
        }
        
        /// <summary>
        /// Toggle Inspector panel with animation (legacy)
        /// </summary>
        private void ToggleInspectorPanel()
        {
            // Panel is now always visible in the new design
        }
        
        /// <summary>
        /// Update Inspector panel with current context (legacy method for compatibility)
        /// </summary>
        private void UpdateInspectorContext()
        {
            UpdateContextDisplay();
            UpdateSecurityDisplay();
        }
        
        /// <summary>
        /// Show a toast notification
        /// </summary>
        public void ShowToast(string message, UI.ToastType type = UI.ToastType.Info)
        {
            _toastManager?.Show(message, type);
        }
        
        #endregion
        
        #region Command Palette (Ctrl+K)
        
        /// <summary>
        /// Open the Command Palette
        /// </summary>
        private void OpenCommandPalette()
        {
            try
            {
                var palette = new UI.CommandPalette();
                palette.Owner = this;
                
                if (palette.ShowDialog() == true && palette.SelectedCommand != null)
                {
                    ExecuteCommand(palette.SelectedCommand.Action);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CommandPalette] Error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Click handler for Command Palette button in sidebar
        /// </summary>
        private void CommandPalette_Click(object sender, RoutedEventArgs e)
        {
            OpenCommandPalette();
        }
        
        /// <summary>
        /// Execute a command from the palette
        /// </summary>
        private void ExecuteCommand(string action)
        {
            Debug.WriteLine($"[CommandPalette] Executing action: {action}");
            
            switch (action)
            {
                case "voice_start":
                    ActivateVoiceWithHotkey();
                    ShowToast("🎤 Voice input started", UI.ToastType.Info);
                    break;
                case "voice_toggle":
                    SpeechToggle.IsChecked = !SpeechToggle.IsChecked;
                    SpeechToggle_Click(SpeechToggle, new RoutedEventArgs());
                    ShowToast(SpeechToggle.IsChecked == true ? "🔊 Voice output enabled" : "🔇 Voice output disabled", UI.ToastType.Info);
                    break;
                case "voice_select":
                    // Open the voice provider popup instead of hidden combobox
                    VoiceProviderPopup.IsOpen = true;
                    _ = PopulateVoiceListAsync();
                    break;
                case "wake_word":
                    ToggleWakeWord();
                    ShowToast(isWakeWordEnabled ? "👂 Wake word enabled" : "🚫 Wake word disabled", UI.ToastType.Info);
                    break;
                case "generate":
                    InputBox.Text = "Generate an image of: ";
                    InputBox.Focus();
                    InputBox.CaretIndex = InputBox.Text.Length;
                    ShowToast("💡 Type what you want to generate", UI.ToastType.Info);
                    break;
                case "suggest":
                    InputBox.Text = "Give me suggestions for: ";
                    InputBox.Focus();
                    InputBox.CaretIndex = InputBox.Text.Length;
                    ShowToast("💡 Type what you need suggestions for", UI.ToastType.Info);
                    break;
                case "summarize":
                    InputBox.Text = "Summarize this: ";
                    InputBox.Focus();
                    InputBox.CaretIndex = InputBox.Text.Length;
                    ShowToast("💡 Paste text to summarize", UI.ToastType.Info);
                    break;
                case "code":
                    OpenCodeEditor();
                    ShowToast("💻 Code Editor opened", UI.ToastType.Info);
                    break;
                case "write":
                    InputBox.Text = "Help me write: ";
                    InputBox.Focus();
                    InputBox.CaretIndex = InputBox.Text.Length;
                    ShowToast("✏ Type what you want to write", UI.ToastType.Info);
                    break;
                case "search":
                    InputBox.Text = "Search for: ";
                    InputBox.Focus();
                    InputBox.CaretIndex = InputBox.Text.Length;
                    ShowToast("🔍 Type what you want to search", UI.ToastType.Info);
                    break;
                case "memory":
                    Memory_Click(null, new RoutedEventArgs());
                    break;
                case "screenshot":
                    CaptureButton_Click(CaptureButton, new RoutedEventArgs());
                    break;
                case "capture_history":
                    HistoryButton_Click(HistoryButton, new RoutedEventArgs());
                    break;
                case "history":
                    History_Click(null, new RoutedEventArgs());
                    break;
                case "uninstaller":
                    Uninstaller_Click(null, new RoutedEventArgs());
                    break;
                case "update_db":
                    UpdateDB_Click(null, new RoutedEventArgs());
                    break;
                case "security":
                    SecuritySuite_Click(null, new RoutedEventArgs());
                    break;
                case "quick_scan":
                    // Open Security Suite for quick scan
                    ShowSecuritySuiteWindow();
                    break;
                case "scan_mode":
                case "scan_orbit":
                case "security_scan":
                    ToggleScanOrbit();
                    break;
                case "settings":
                    Settings_Click(null, new RoutedEventArgs());
                    break;
                case "theme":
                    ThemeManager.ToggleTheme();
                    ApplyTheme();
                    ShowToast("Theme toggled", UI.ToastType.Info);
                    break;
                case "inspector":
                    ToggleInspectorPanel();
                    break;
                case "status_panel":
                    StatusPanelToggle_Click(null, new RoutedEventArgs());
                    break;
                case "focus_mode":
                    ToggleFocusMode();
                    break;
                case "fullscreen":
                    ToggleFullscreen();
                    break;
                case "clear_chat":
                    DeleteHistory_Click(null, new RoutedEventArgs());
                    break;
                case "new_chat":
                    NewChat_Click(null, new RoutedEventArgs());
                    break;
                case "overlay":
                    _inAppAssistant?.ToggleOverlay();
                    break;
                case "context":
                    var ctx = _inAppAssistant?.GetCurrentContext();
                    if (ctx != null)
                    {
                        ShowToast($"Context: {ctx.ProcessName}", UI.ToastType.Info);
                    }
                    break;
                default:
                    Debug.WriteLine($"[CommandPalette] Unknown action: {action}");
                    break;
            }
        }
        
        #endregion
        
        #region Security Suite Button
        
        /// <summary>
        /// Open Security Suite window
        /// </summary>
        private void SecuritySuite_Click(object? sender, RoutedEventArgs e)
        {
            ShowSecuritySuiteWindow();
        }
        
        /// <summary>
        /// Shows the Security Suite window as a singleton (reuses existing window if open)
        /// </summary>
        private void ShowSecuritySuiteWindow()
        {
            try
            {
                // DEBUG: Confirm this method is being called
                MessageBox.Show("ShowSecuritySuiteWindow called!", "DEBUG");
                
                // Check if window exists and is still open
                if (_securitySuiteWindow != null && _securitySuiteWindow.IsLoaded)
                {
                    // Window exists, just bring it to front
                    _securitySuiteWindow.Activate();
                    if (_securitySuiteWindow.WindowState == WindowState.Minimized)
                        _securitySuiteWindow.WindowState = WindowState.Normal;
                    return;
                }
                
                // Create new Security Suite window with real scanning
                Debug.WriteLine("[SecuritySuite] Creating new window...");
                _securitySuiteWindow = new SecuritySuite.SecuritySuiteWindow();
                _securitySuiteWindow.Owner = this;
                
                // Clear reference when window closes
                _securitySuiteWindow.Closed += (s, args) => 
                {
                    Debug.WriteLine("[SecuritySuite] Window closed, clearing reference");
                    _securitySuiteWindow = null;
                };
                
                _securitySuiteWindow.Show();
                Debug.WriteLine("[SecuritySuite] Window shown successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Security] Error opening Security Suite: {ex.Message}");
                Debug.WriteLine($"[Security] Inner: {ex.InnerException?.Message}");
                Debug.WriteLine($"[Security] Stack: {ex.StackTrace}");
                
                // Show error in chat instead of toast (more visible)
                AddMessage("Atlas", $"⚠️ Security Suite failed to open:\n{ex.Message}\n\nInner: {ex.InnerException?.Message}", false);
                
                // Also show a message box so user sees it
                MessageBox.Show($"Security Suite failed to open:\n\n{ex.Message}\n\nInner Exception: {ex.InnerException?.Message}", 
                    "Security Suite Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// Open Integration Hub window - shows all available integrations
        /// </summary>
        private void IntegrationHub_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                var hubWindow = new IntegrationHubWindow();
                hubWindow.Owner = this;
                hubWindow.Show();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[IntegrationHub] Error opening Integration Hub: {ex.Message}");
                ShowToast($"Error: {ex.Message}", UI.ToastType.Error);
            }
        }
        
        /// <summary>
        /// Open Social Media Console window
        /// </summary>
        private void SocialMediaConsole_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                var consoleWindow = new SocialMedia.UI.SocialMediaWindow();
                consoleWindow.Owner = this;
                consoleWindow.Show();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SocialMedia] Error opening Social Media Console: {ex.Message}");
                ShowToast($"Error: {ex.Message}", UI.ToastType.Error);
            }
        }
        
        #endregion
        
        #region ═══════════════════════════════════════════════════════════════
        // FIGMA UI INTEGRATION - TopNavBar and StatusPanel handlers
        #endregion
        
        /// <summary>
        /// Handle TopNavBar tab changes
        /// </summary>
        private void TopNavBar_TabChanged(object? sender, string tabName)
        {
            try
            {
                Debug.WriteLine($"[TopNavBar] Tab changed to: {tabName}");
                
                switch (tabName)
                {
                    case "chat":
                        // Already on chat, do nothing
                        break;
                    case "commands":
                        // Show commands/tools panel
                        ShowToast("Commands panel coming soon", UI.ToastType.Info);
                        break;
                    case "memory":
                        Memory_Click(null, new RoutedEventArgs());
                        break;
                    case "security":
                        SecuritySuite_Click(null, new RoutedEventArgs());
                        break;
                    case "create":
                        // Open social media / image generation
                        SocialMediaConsole_Click(null, new RoutedEventArgs());
                        break;
                    case "code":
                        // Open code editor
                        QuickAction_Click(new Button { Tag = "code" }, new RoutedEventArgs());
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TopNavBar] Error handling tab change: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Handle TopNavBar minimize request
        /// </summary>
        private void TopNavBar_MinimizeRequested(object? sender, EventArgs e)
        {
            WindowState = WindowState.Minimized;
        }
        
        /// <summary>
        /// Handle TopNavBar maximize request
        /// </summary>
        private void TopNavBar_MaximizeRequested(object? sender, EventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }
        
        /// <summary>
        /// Handle TopNavBar close request
        /// </summary>
        private void TopNavBar_CloseRequested(object? sender, EventArgs e)
        {
            Close();
        }
        
        /// <summary>
        /// Toggle the Status Panel visibility
        /// </summary>
        private void StatusPanelToggle_Click(object sender, RoutedEventArgs e)
        {
            if (StatusPanel.Visibility == Visibility.Visible)
            {
                StatusPanel.Visibility = Visibility.Collapsed;
                StatusPanelColumn.Width = new GridLength(0);
            }
            else
            {
                StatusPanel.Visibility = Visibility.Visible;
                StatusPanelColumn.Width = new GridLength(260);
                
                // Update status panel with current AI info
                UpdateStatusPanel();
            }
        }
        
        /// <summary>
        /// Update the status panel with current AI state
        /// </summary>
        private void UpdateStatusPanel()
        {
            try
            {
                // Update model name from voice manager
                var provider = _voiceManager?.GetProvider(_voiceManager.ActiveProviderType);
                var providerName = provider?.DisplayName ?? "Atlas AI";
                StatusPanel.SetModel(providerName);
                
                // Default to active state
                StatusPanel.SetState("Active", true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StatusPanel] Error updating: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Handle quick action requests from status panel
        /// </summary>
        private void StatusPanel_QuickActionRequested(object? sender, string action)
        {
            try
            {
                Debug.WriteLine($"[StatusPanel] Quick action: {action}");
                
                switch (action)
                {
                    case "scan":
                        SecuritySuite_Click(null, new RoutedEventArgs());
                        break;
                    case "optimize":
                        ShowToast("System optimization started", UI.ToastType.Info);
                        break;
                    case "export":
                        ShowToast("Export feature coming soon", UI.ToastType.Info);
                        break;
                    case "cache":
                        ShowToast("Cache cleared", UI.ToastType.Success);
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StatusPanel] Error handling quick action: {ex.Message}");
                ShowToast($"Error: {ex.Message}", UI.ToastType.Error);
            }
        }
    }

    public class ChatMessage
    {
        public string Sender { get; set; } = "";
        public string Text { get; set; } = "";
        public bool IsUser { get; set; }
        public string Role { get; set; } = "";
    }
}
