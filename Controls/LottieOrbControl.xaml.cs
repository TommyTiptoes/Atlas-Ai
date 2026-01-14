using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;

namespace AtlasAI.Controls
{
    public partial class LottieOrbControl : UserControl
    {
        private AtlasVisualState _currentState = AtlasVisualState.Idle;
        private string _animationsFolder;
        private string _currentAnimationFile = "Siri Animation.json"; // Track user's selected animation
        
        // Default animation (used if no custom selection)
        private const string DEFAULT_ANIMATION = "Siri Animation.json";
        
        public LottieOrbControl()
        {
            InitializeComponent();
            
            // Find animations folder
            _animationsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Animations");
            
            // If not in bin folder, try relative path
            if (!Directory.Exists(_animationsFolder))
            {
                _animationsFolder = Path.Combine(Directory.GetCurrentDirectory(), "Animations");
            }
            
            Loaded += LottieOrbControl_Loaded;
        }
        
        private void LottieOrbControl_Loaded(object sender, RoutedEventArgs e)
        {
            LoadAnimation(_currentAnimationFile);
        }
        
        private void LoadAnimation(string fileName)
        {
            try
            {
                var filePath = Path.Combine(_animationsFolder, fileName);
                
                Debug.WriteLine($"[LottieOrb] Attempting to load animation from: {filePath}");
                Debug.WriteLine($"[LottieOrb] Animations folder: {_animationsFolder}");
                Debug.WriteLine($"[LottieOrb] Folder exists: {Directory.Exists(_animationsFolder)}");
                
                if (File.Exists(filePath))
                {
                    LottieAnimation.FileName = filePath;
                    Debug.WriteLine($"[LottieOrb] Successfully loaded animation: {fileName}");
                }
                else
                {
                    Debug.WriteLine($"[LottieOrb] Animation file not found: {filePath}");
                    
                    // Try to list available animations
                    if (Directory.Exists(_animationsFolder))
                    {
                        var availableFiles = Directory.GetFiles(_animationsFolder, "*.json");
                        Debug.WriteLine($"[LottieOrb] Available animations in folder: {string.Join(", ", availableFiles.Select(Path.GetFileName))}");
                        
                        // Try to load the first available animation as fallback
                        if (availableFiles.Length > 0)
                        {
                            LottieAnimation.FileName = availableFiles[0];
                            Debug.WriteLine($"[LottieOrb] Loaded fallback animation: {Path.GetFileName(availableFiles[0])}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LottieOrb] Error loading animation: {ex.Message}");
                Debug.WriteLine($"[LottieOrb] Stack trace: {ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// Load a specific animation by filename (called from settings)
        /// This becomes the user's selected animation and won't be overridden by state changes
        /// </summary>
        public void LoadAnimationByName(string fileName)
        {
            _currentAnimationFile = fileName;
            LoadAnimation(fileName);
            Debug.WriteLine($"[LottieOrb] User selected animation: {fileName}");
        }
        
        public void SetState(AtlasVisualState state)
        {
            if (_currentState == state) return;
            _currentState = state;
            
            Debug.WriteLine($"[LottieOrb] State changed to: {state}");
            
            // Update the state label only - keep the user's selected animation playing
            // The Lottie animation loops continuously, so we just change the label
            switch (state)
            {
                case AtlasVisualState.Idle:
                    StateLabel.Text = "";
                    break;
                    
                case AtlasVisualState.Listening:
                    StateLabel.Text = "LISTENING";
                    break;
                    
                case AtlasVisualState.Thinking:
                    StateLabel.Text = "PROCESSING";
                    break;
                    
                case AtlasVisualState.Speaking:
                    StateLabel.Text = "SPEAKING";
                    break;
            }
        }
        
        /// <summary>
        /// Update animation based on voice energy (0.0 to 1.0)
        /// LottieSharp doesn't support playback rate, so we just ensure animation is playing
        /// </summary>
        public void UpdateSpeakingEnergy(double energy)
        {
            // LottieSharp auto-plays, energy could be used for other effects if needed
        }
        
        // Convenience methods
        public void SetIdle() => SetState(AtlasVisualState.Idle);
        public void SetListening() => SetState(AtlasVisualState.Listening);
        public void SetThinking() => SetState(AtlasVisualState.Thinking);
        public void SetSpeaking() => SetState(AtlasVisualState.Speaking);
    }
}
