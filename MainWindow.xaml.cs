using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Interop;
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
        private NotifyIcon? trayIcon;
        
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
            
            SetupTrayIcon();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Position window in right-hand corner
            PositionWindowInRightCorner();
            
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
                OpenChatWindow();
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

        private void Avatar_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var menu = new ContextMenu();
            
            var chatItem = new MenuItem { Header = "Open Chat" };
            chatItem.Click += (s, ev) => OpenChatWindow();
            menu.Items.Add(chatItem);

            var hideItem = new MenuItem { Header = "Hide Avatar" };
            hideItem.Click += (s, ev) => Hide();
            menu.Items.Add(hideItem);

            menu.Items.Add(new Separator());

            var exitItem = new MenuItem { Header = "Exit" };
            exitItem.Click += (s, ev) => ExitApplication();
            menu.Items.Add(exitItem);

            menu.IsOpen = true;
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
