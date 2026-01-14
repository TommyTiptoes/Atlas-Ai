using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Interop;
using AtlasAI.Core;
using Application = System.Windows.Application;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseButtonState = System.Windows.Input.MouseButtonState;

namespace AtlasAI
{
    public partial class MainWindow : Window
    {
        private bool isDragging = false;
        private Point dragStart;
        private ChatWindow? chatWindow;
        private SettingsWindow? settingsWindow;
        private SystemControlWindow? systemControlWindow;
        private NotifyIcon? trayIcon;
        private NavigationService? _navigationService;
        
        // Hotkey registration
        private const int HOTKEY_ID = 9000;
        private const uint MOD_CTRL = 0x0002;
        private const uint MOD_ALT = 0x0001;
        private const uint VK_A = 0x41;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public MainWindow()
        {
            InitializeComponent();
            
            // Initialize clipboard manager
            ClipboardManager.Initialize();
            
            // Initialize NavigationService and AppState
            InitializeNavigationService();
            
            SetupTrayIcon();
            Loaded += MainWindow_Loaded;
        }
        
        private void InitializeNavigationService()
        {
            _navigationService = new NavigationService();
            
            // Set DataContext for binding
            DataContext = AppState.Instance;
            if (ShellContent != null)
            {
                ShellContent.DataContext = _navigationService;
            }
            
            // Register routes
            RegisterRoutes();
            
            // Register modules
            RegisterModules();
        }
        
        private void RegisterRoutes()
        {
            if (_navigationService == null) return;
            
            // Register routes for major features
            // For now, we'll just create placeholder TextBlocks
            // In a full implementation, these would be proper UserControls
            _navigationService.RegisterRoute("chat", () =>
            {
                var placeholder = new TextBlock
                {
                    Text = "Chat View - Feature windows are still separate for now.\nDouble-click the avatar or use Ctrl+Alt+A to open the Chat window.",
                    FontSize = 16,
                    Foreground = System.Windows.Media.Brushes.White,
                    Margin = new Thickness(20),
                    TextWrapping = TextWrapping.Wrap
                };
                return placeholder;
            });
            
            _navigationService.RegisterRoute("settings", () =>
            {
                var placeholder = new TextBlock
                {
                    Text = "Settings View - Opening Settings Window...",
                    FontSize = 16,
                    Foreground = System.Windows.Media.Brushes.White,
                    Margin = new Thickness(20)
                };
                return placeholder;
            });
            
            _navigationService.RegisterRoute("system", () =>
            {
                var placeholder = new TextBlock
                {
                    Text = "System Control View - Opening System Control Window...",
                    FontSize = 16,
                    Foreground = System.Windows.Media.Brushes.White,
                    Margin = new Thickness(20)
                };
                return placeholder;
            });
        }
        
        private void RegisterModules()
        {
            var registry = ModuleRegistry.Instance;
            registry.RegisterModule("chat", "Chat", "💬", "AI Chat Interface");
            registry.RegisterModule("settings", "Settings", "⚙️", "Application Settings");
            registry.RegisterModule("system", "System Control", "🛡️", "System Scanner & Control");
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Position window in right-hand corner for floating mode
            if (!AppState.Instance.IsShellMode)
            {
                PositionWindowInRightCorner();
            }
            
            var helper = new WindowInteropHelper(this);
            var source = HwndSource.FromHwnd(helper.Handle);
            source?.AddHook(HwndHook);
            
            // Register Ctrl+Alt+A hotkey
            RegisterHotKey(helper.Handle, HOTKEY_ID, MOD_CTRL | MOD_ALT, VK_A);
            
            // Auto-open chat window on startup so voice recognition starts immediately
            OpenChatWindow();
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                if (AppState.Instance.IsShellMode && _navigationService != null)
                {
                    // In shell mode, navigate to chat
                    _navigationService.Navigate("chat");
                    Activate();
                }
                else
                {
                    // In floating mode, open chat window
                    OpenChatWindow();
                }
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void SetupTrayIcon()
        {
            // Tray icon is now handled by ChatWindow's TaskbarIconHelper
            // Just create a minimal reference for the context menu
            trayIcon = new NotifyIcon
            {
                Visible = false // Don't show - ChatWindow handles this
            };
        }

        private void Avatar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                OpenChatWindow();
            }
            else
            {
                isDragging = true;
                dragStart = e.GetPosition(this);
                AvatarContainer.CaptureMouse();
            }
        }

        private void OpenChatWindow()
        {
            if (chatWindow == null || !chatWindow.IsLoaded)
            {
                chatWindow = new ChatWindow();
                chatWindow.Closed += (s, e) => chatWindow = null;
                chatWindow.Show();
            }
            else
            {
                chatWindow.Activate();
            }
        }
        
        private void OpenSettingsWindow()
        {
            if (settingsWindow == null || !settingsWindow.IsLoaded)
            {
                settingsWindow = new SettingsWindow();
                settingsWindow.Closed += (s, e) => settingsWindow = null;
                settingsWindow.Show();
            }
            else
            {
                settingsWindow.Activate();
            }
        }
        
        private void OpenSystemControlWindow()
        {
            if (systemControlWindow == null || !systemControlWindow.IsLoaded)
            {
                systemControlWindow = new SystemControlWindow();
                systemControlWindow.Closed += (s, e) => systemControlWindow = null;
                systemControlWindow.Show();
            }
            else
            {
                systemControlWindow.Activate();
            }
        }

        private void Avatar_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var menu = new ContextMenu();
            
            var chatItem = new MenuItem { Header = "Open Chat" };
            chatItem.Click += (s, ev) => OpenChatWindow();
            menu.Items.Add(chatItem);
            
            var settingsItem = new MenuItem { Header = "Open Settings" };
            settingsItem.Click += (s, ev) => OpenSettingsWindow();
            menu.Items.Add(settingsItem);
            
            var systemItem = new MenuItem { Header = "Open System Control" };
            systemItem.Click += (s, ev) => OpenSystemControlWindow();
            menu.Items.Add(systemItem);

            var hideItem = new MenuItem { Header = "Hide Avatar" };
            hideItem.Click += (s, ev) => Hide();
            menu.Items.Add(hideItem);
            
            menu.Items.Add(new Separator());
            
            var shellModeItem = new MenuItem { Header = "Toggle Shell Mode (Experimental)" };
            shellModeItem.Click += (s, ev) => ToggleShellMode();
            menu.Items.Add(shellModeItem);

            menu.Items.Add(new Separator());

            var exitItem = new MenuItem { Header = "Exit" };
            exitItem.Click += (s, ev) => ExitApplication();
            menu.Items.Add(exitItem);

            menu.IsOpen = true;
        }
        
        private void ToggleShellMode()
        {
            var newMode = !AppState.Instance.IsShellMode;
            AppState.Instance.IsShellMode = newMode;
            
            if (newMode)
            {
                // Switch to shell mode
                WindowStyle = WindowStyle.None;
                AllowsTransparency = false;
                Background = System.Windows.Media.Brushes.Black;
                Width = 1200;
                Height = 800;
                WindowState = WindowState.Maximized;
                ShowInTaskbar = true;
                ResizeMode = ResizeMode.CanResize;
                
                // Navigate to chat by default
                if (_navigationService != null && _navigationService.CanNavigate("chat"))
                {
                    _navigationService.Navigate("chat");
                }
            }
            else
            {
                // Switch back to floating avatar mode
                WindowStyle = WindowStyle.None;
                AllowsTransparency = true;
                Background = System.Windows.Media.Brushes.Transparent;
                Width = 200;
                Height = 200;
                WindowState = WindowState.Normal;
                ShowInTaskbar = false;
                ResizeMode = ResizeMode.NoResize;
                PositionWindowInRightCorner();
            }
        }
        
        // Navigation button handlers
        private void NavChat_Click(object sender, RoutedEventArgs e)
        {
            if (_navigationService != null)
            {
                _navigationService.Navigate("chat");
                OpenChatWindow(); // For now, still open the window
            }
        }
        
        private void NavSettings_Click(object sender, RoutedEventArgs e)
        {
            if (_navigationService != null)
            {
                _navigationService.Navigate("settings");
                OpenSettingsWindow(); // For now, still open the window
            }
        }
        
        private void NavSystem_Click(object sender, RoutedEventArgs e)
        {
            if (_navigationService != null)
            {
                _navigationService.Navigate("system");
                OpenSystemControlWindow(); // For now, still open the window
            }
        }
        
        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }
        
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ExitApplication()
        {
            trayIcon?.Dispose();
            Application.Current.Shutdown();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (isDragging && e.LeftButton == MouseButtonState.Pressed)
            {
                var pos = e.GetPosition(this);
                Left += pos.X - dragStart.X;
                Top += pos.Y - dragStart.Y;
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (isDragging)
            {
                isDragging = false;
                AvatarContainer.ReleaseMouseCapture();
            }
            base.OnMouseLeftButtonUp(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            var helper = new WindowInteropHelper(this);
            UnregisterHotKey(helper.Handle, HOTKEY_ID);
            trayIcon?.Dispose();
            ClipboardManager.Dispose();
            base.OnClosed(e);
        }

        private void PositionWindowInRightCorner()
        {
            try
            {
                // Get the working area of the primary screen (excludes taskbar)
                var workingArea = SystemParameters.WorkArea;
                
                // Position window in the right corner with some padding
                var padding = 20;
                Left = workingArea.Right - Width - padding;
                Top = workingArea.Top + padding;
                
                // Ensure window stays within screen bounds
                if (Left < workingArea.Left) Left = workingArea.Left + padding;
                if (Top < workingArea.Top) Top = workingArea.Top + padding;
            }
            catch
            {
                // Fallback to center if positioning fails
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }
    }
}
