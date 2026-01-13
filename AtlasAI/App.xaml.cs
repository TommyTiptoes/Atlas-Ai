using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace AtlasAI
{
    public partial class App : Application
    {
        private static BitmapImage? _appIcon;
        private static Mutex? _singleInstanceMutex;
        private static readonly string CrashLogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AtlasAI", "crash_log.txt");
        
        public static BitmapImage AppIcon
        {
            get
            {
                if (_appIcon == null)
                {
                    try
                    {
                        _appIcon = new BitmapImage(new Uri("pack://application:,,,/atlas.ico"));
                    }
                    catch
                    {
                        // Fallback - icon not found
                    }
                }
                return _appIcon!;
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            // Force hardware GPU rendering for smooth animations
            RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.Default;
            
            // Single instance check
            bool createdNew;
            _singleInstanceMutex = new Mutex(true, "AtlasAI_SingleInstance_Mutex", out createdNew);
            
            if (!createdNew)
            {
                // Another instance is already running
                MessageBox.Show("Atlas AI is already running.", "Atlas AI", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }
            
            base.OnStartup(e);
            
            // Override WPF-UI accent color to cyan AFTER base startup loads resources
            ApplyCyanAccentColor();
            
            // Global exception handlers
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            
            // Set default icon for all windows
            EventManager.RegisterClassHandler(typeof(Window), Window.LoadedEvent, new RoutedEventHandler(OnWindowLoaded));
        }
        
        private void ApplyCyanAccentColor()
        {
            try
            {
                var cyan = Color.FromRgb(0x22, 0xd3, 0xee); // #22d3ee
                var cyanLight1 = Color.FromRgb(0x67, 0xe8, 0xf9);
                var cyanLight2 = Color.FromRgb(0xa5, 0xf3, 0xfc);
                var cyanLight3 = Color.FromRgb(0xcf, 0xfa, 0xfe);
                var cyanDark1 = Color.FromRgb(0x06, 0xb6, 0xd4);
                var cyanDark2 = Color.FromRgb(0x08, 0x91, 0xb2);
                var cyanDark3 = Color.FromRgb(0x0e, 0x74, 0x90);
                
                // Apply theme first
                Wpf.Ui.Appearance.ApplicationThemeManager.Apply(Wpf.Ui.Appearance.ApplicationTheme.Dark);
                
                // Apply accent color with UpdateAccents = true to force all controls to update
                Wpf.Ui.Appearance.ApplicationAccentColorManager.Apply(
                    cyan, 
                    Wpf.Ui.Appearance.ApplicationTheme.Dark,
                    true // updateAccents - forces all accent-colored elements to update
                );
                
                // Force update ALL resource dictionaries (including merged ones)
                UpdateAccentInAllDictionaries(Application.Current.Resources, cyan, cyanLight1, cyanLight2, cyanLight3, cyanDark1, cyanDark2, cyanDark3);
                
                // Schedule another update after window loads (WPF-UI sometimes resets)
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
                {
                    try
                    {
                        Wpf.Ui.Appearance.ApplicationAccentColorManager.Apply(cyan, Wpf.Ui.Appearance.ApplicationTheme.Dark, true);
                        UpdateAccentInAllDictionaries(Application.Current.Resources, cyan, cyanLight1, cyanLight2, cyanLight3, cyanDark1, cyanDark2, cyanDark3);
                    }
                    catch { }
                }));
                
                // Also schedule for after render
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, new Action(() =>
                {
                    try
                    {
                        Wpf.Ui.Appearance.ApplicationAccentColorManager.Apply(cyan, Wpf.Ui.Appearance.ApplicationTheme.Dark, true);
                    }
                    catch { }
                }));
            }
            catch { }
        }
        
        private void UpdateAccentInAllDictionaries(ResourceDictionary dict, Color cyan, Color cyanLight1, Color cyanLight2, Color cyanLight3, Color cyanDark1, Color cyanDark2, Color cyanDark3)
        {
            // Update this dictionary
            SetAccentResources(dict, cyan, cyanLight1, cyanLight2, cyanLight3, cyanDark1, cyanDark2, cyanDark3);
            
            // Recursively update all merged dictionaries
            foreach (var merged in dict.MergedDictionaries)
            {
                UpdateAccentInAllDictionaries(merged, cyan, cyanLight1, cyanLight2, cyanLight3, cyanDark1, cyanDark2, cyanDark3);
            }
        }
        
        private void SetAccentResources(ResourceDictionary res, Color cyan, Color cyanLight1, Color cyanLight2, Color cyanLight3, Color cyanDark1, Color cyanDark2, Color cyanDark3)
        {
            // Colors
            TrySetResource(res, "SystemAccentColor", cyan);
            TrySetResource(res, "SystemAccentColorLight1", cyanLight1);
            TrySetResource(res, "SystemAccentColorLight2", cyanLight2);
            TrySetResource(res, "SystemAccentColorLight3", cyanLight3);
            TrySetResource(res, "SystemAccentColorDark1", cyanDark1);
            TrySetResource(res, "SystemAccentColorDark2", cyanDark2);
            TrySetResource(res, "SystemAccentColorDark3", cyanDark3);
            
            // Primary brushes
            TrySetResource(res, "SystemAccentColorPrimaryBrush", new SolidColorBrush(cyan));
            TrySetResource(res, "SystemAccentColorSecondaryBrush", new SolidColorBrush(cyanDark1));
            TrySetResource(res, "SystemAccentColorTertiaryBrush", new SolidColorBrush(cyanDark2));
            
            // Text accent brushes
            TrySetResource(res, "AccentTextFillColorPrimaryBrush", new SolidColorBrush(cyan));
            TrySetResource(res, "AccentTextFillColorSecondaryBrush", new SolidColorBrush(cyanDark1));
            TrySetResource(res, "AccentTextFillColorTertiaryBrush", new SolidColorBrush(cyanDark2));
            
            // Fill brushes (buttons, toggles, hover states)
            TrySetResource(res, "AccentFillColorDefaultBrush", new SolidColorBrush(cyan));
            TrySetResource(res, "AccentFillColorSecondaryBrush", new SolidColorBrush(cyanDark1));
            TrySetResource(res, "AccentFillColorTertiaryBrush", new SolidColorBrush(cyanDark2));
            TrySetResource(res, "AccentFillColorDisabledBrush", new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69)));
            
            // Subtle fill (hover backgrounds) - semi-transparent cyan
            TrySetResource(res, "SubtleFillColorSecondaryBrush", new SolidColorBrush(Color.FromArgb(0x15, 0x22, 0xd3, 0xee)));
            TrySetResource(res, "SubtleFillColorTertiaryBrush", new SolidColorBrush(Color.FromArgb(0x20, 0x22, 0xd3, 0xee)));
            
            // Control stroke colors
            TrySetResource(res, "ControlStrokeColorDefaultBrush", new SolidColorBrush(Color.FromArgb(0x30, 0x22, 0xd3, 0xee)));
            TrySetResource(res, "ControlStrokeColorSecondaryBrush", new SolidColorBrush(Color.FromArgb(0x20, 0x22, 0xd3, 0xee)));
            
            // Focus stroke
            TrySetResource(res, "FocusStrokeColorOuterBrush", new SolidColorBrush(cyan));
            TrySetResource(res, "FocusStrokeColorInnerBrush", new SolidColorBrush(Color.FromRgb(0x05, 0x05, 0x08)));
            
            // Text on accent
            TrySetResource(res, "TextOnAccentFillColorPrimaryBrush", new SolidColorBrush(Color.FromRgb(0x05, 0x05, 0x08)));
            TrySetResource(res, "TextOnAccentFillColorSecondaryBrush", new SolidColorBrush(Color.FromRgb(0x0a, 0x0a, 0x12)));
            TrySetResource(res, "TextOnAccentFillColorSelectedTextBrush", new SolidColorBrush(Color.FromRgb(0x05, 0x05, 0x08)));
        }
        
        private void TrySetResource(ResourceDictionary dict, string key, object value)
        {
            try
            {
                if (dict.Contains(key))
                    dict[key] = value;
                else
                    dict.Add(key, value);
            }
            catch { }
        }
        
        protected override void OnExit(ExitEventArgs e)
        {
            _singleInstanceMutex?.ReleaseMutex();
            _singleInstanceMutex?.Dispose();
            base.OnExit(e);
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is Window window)
            {
                // Set icon
                if (window.Icon == null)
                {
                    try { window.Icon = AppIcon; } catch { }
                }
                
                // Force cyan accent on every window load
                try
                {
                    var cyan = Color.FromRgb(0x22, 0xd3, 0xee);
                    Wpf.Ui.Appearance.ApplicationAccentColorManager.Apply(cyan, Wpf.Ui.Appearance.ApplicationTheme.Dark);
                    
                    // Also update window-specific resources
                    if (window.Resources != null)
                    {
                        var cyanLight1 = Color.FromRgb(0x67, 0xe8, 0xf9);
                        var cyanLight2 = Color.FromRgb(0xa5, 0xf3, 0xfc);
                        var cyanLight3 = Color.FromRgb(0xcf, 0xfa, 0xfe);
                        var cyanDark1 = Color.FromRgb(0x06, 0xb6, 0xd4);
                        var cyanDark2 = Color.FromRgb(0x08, 0x91, 0xb2);
                        var cyanDark3 = Color.FromRgb(0x0e, 0x74, 0x90);
                        UpdateAccentInAllDictionaries(window.Resources, cyan, cyanLight1, cyanLight2, cyanLight3, cyanDark1, cyanDark2, cyanDark3);
                    }
                }
                catch { }
            }
        }
        
        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            LogCrash("AppDomain.UnhandledException", e.ExceptionObject as Exception);
        }
        
        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogCrash("Dispatcher.UnhandledException", e.Exception);
            e.Handled = true; // Prevent app from crashing
            
            MessageBox.Show($"An error occurred:\n\n{e.Exception.Message}\n\nInner: {e.Exception.InnerException?.Message}\n\nCheck crash_log.txt for details.",
                "Atlas Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        
        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            LogCrash("TaskScheduler.UnobservedTaskException", e.Exception);
            e.SetObserved();
        }
        
        private static void LogCrash(string source, Exception? ex)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CrashLogPath)!);
                var log = $"\n\n=== CRASH [{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ===\n" +
                          $"Source: {source}\n" +
                          $"Message: {ex?.Message}\n" +
                          $"Inner: {ex?.InnerException?.Message}\n" +
                          $"Stack:\n{ex?.StackTrace}\n";
                File.AppendAllText(CrashLogPath, log);
            }
            catch { }
        }
    }
}
