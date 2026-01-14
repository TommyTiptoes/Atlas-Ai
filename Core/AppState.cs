using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AtlasAI.Core
{
    /// <summary>
    /// Global application state for sharing data across the shell.
    /// Provides observable properties for UI binding.
    /// </summary>
    public sealed class AppState : INotifyPropertyChanged
    {
        private static AppState? _instance;
        public static AppState Instance => _instance ??= new AppState();

        private bool _isShellMode;
        private string? _currentUser;

        /// <summary>
        /// Indicates whether the app is in shell mode (true) or floating avatar mode (false).
        /// </summary>
        public bool IsShellMode
        {
            get => _isShellMode;
            set
            {
                if (_isShellMode != value)
                {
                    _isShellMode = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Current user name or profile identifier.
        /// </summary>
        public string? CurrentUser
        {
            get => _currentUser;
            set
            {
                if (_currentUser != value)
                {
                    _currentUser = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
