using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace AtlasAI.Controls
{
    public partial class TopNavBarControl : UserControl
    {
        private DispatcherTimer? _clockTimer;
        private string _activeTab = "chat";
        
        /// <summary>
        /// Event fired when a navigation tab is clicked
        /// </summary>
        public event EventHandler<string>? TabChanged;
        
        /// <summary>
        /// Event fired when minimize is clicked
        /// </summary>
        public event EventHandler? MinimizeRequested;
        
        /// <summary>
        /// Event fired when maximize/restore is clicked
        /// </summary>
        public event EventHandler? MaximizeRequested;
        
        /// <summary>
        /// Event fired when close is clicked
        /// </summary>
        public event EventHandler? CloseRequested;
        
        public TopNavBarControl()
        {
            InitializeComponent();
            Loaded += TopNavBarControl_Loaded;
            Unloaded += TopNavBarControl_Unloaded;
        }
        
        private void TopNavBarControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Start clock timer
            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += ClockTimer_Tick;
            _clockTimer.Start();
            
            UpdateDateTime();
        }
        
        private void TopNavBarControl_Unloaded(object sender, RoutedEventArgs e)
        {
            _clockTimer?.Stop();
        }
        
        private void ClockTimer_Tick(object? sender, EventArgs e)
        {
            UpdateDateTime();
        }
        
        private void UpdateDateTime()
        {
            var now = DateTime.Now;
            DateTimeText.Text = now.ToString("ddd, MMM d · h:mm tt");
        }

        /// <summary>
        /// Handle navigation tab clicks
        /// </summary>
        private void NavTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tabName)
            {
                SetActiveTab(tabName);
                TabChanged?.Invoke(this, tabName);
            }
        }
        
        /// <summary>
        /// Set the active tab visually
        /// </summary>
        public void SetActiveTab(string tabName)
        {
            _activeTab = tabName;
            
            // Get styles
            var activeStyle = FindResource("NavTabBtnActive") as Style;
            var normalStyle = FindResource("NavTabBtn") as Style;
            
            // Update all tabs
            ChatTab.Style = tabName == "chat" ? activeStyle : normalStyle;
            CommandsTab.Style = tabName == "commands" ? activeStyle : normalStyle;
            MemoryTab.Style = tabName == "memory" ? activeStyle : normalStyle;
            SecurityTab.Style = tabName == "security" ? activeStyle : normalStyle;
            CreateTab.Style = tabName == "create" ? activeStyle : normalStyle;
            CodeTab.Style = tabName == "code" ? activeStyle : normalStyle;
        }
        
        /// <summary>
        /// Get the currently active tab
        /// </summary>
        public string ActiveTab => _activeTab;
        
        /// <summary>
        /// Set the status indicator (online/offline/busy)
        /// </summary>
        public void SetStatus(string status)
        {
            StatusText.Text = status.ToUpper();
            
            switch (status.ToLower())
            {
                case "online":
                case "active":
                    StatusDot.Fill = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(34, 197, 94)); // Green
                    StatusText.Foreground = StatusDot.Fill;
                    break;
                case "busy":
                case "processing":
                    StatusDot.Fill = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(249, 115, 22)); // Orange
                    StatusText.Foreground = StatusDot.Fill;
                    break;
                case "offline":
                case "error":
                    StatusDot.Fill = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(239, 68, 68)); // Red
                    StatusText.Foreground = StatusDot.Fill;
                    break;
                default:
                    StatusDot.Fill = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(100, 116, 139)); // Gray
                    StatusText.Foreground = StatusDot.Fill;
                    break;
            }
        }
        
        // Window control handlers
        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            MinimizeRequested?.Invoke(this, EventArgs.Empty);
            
            // Fallback: find parent window and minimize
            if (MinimizeRequested == null)
            {
                var window = Window.GetWindow(this);
                if (window != null)
                    window.WindowState = WindowState.Minimized;
            }
        }
        
        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            MaximizeRequested?.Invoke(this, EventArgs.Empty);
            
            // Fallback: find parent window and toggle maximize
            if (MaximizeRequested == null)
            {
                var window = Window.GetWindow(this);
                if (window != null)
                {
                    window.WindowState = window.WindowState == WindowState.Maximized 
                        ? WindowState.Normal 
                        : WindowState.Maximized;
                }
            }
        }
        
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
            
            // Fallback: find parent window and close
            if (CloseRequested == null)
            {
                var window = Window.GetWindow(this);
                window?.Close();
            }
        }
    }
}
