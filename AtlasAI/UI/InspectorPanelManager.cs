using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using MinimalApp.InAppAssistant;

namespace MinimalApp.UI
{
    /// <summary>
    /// Manages the collapsible right-side Inspector panel
    /// Shows: Active Context, Security Status, Voice Settings
    /// </summary>
    public class InspectorPanelManager
    {
        private Border? _panel;
        private StackPanel? _content;
        private bool _isVisible = false;
        private const double PanelWidth = 280;

        // UI Elements for updating
        private TextBlock? _activeAppText;
        private TextBlock? _windowTitleText;
        private TextBlock? _modeText;
        private TextBlock? _securityStatusText;
        private TextBlock? _lastScanText;
        private TextBlock? _definitionsAgeText;
        private Slider? _voiceSpeedSlider;
        private Slider? _voiceStabilitySlider;

        public bool IsVisible => _isVisible;
        public event Action<bool>? VisibilityChanged;

        /// <summary>
        /// Create the inspector panel UI
        /// </summary>
        public Border CreatePanel()
        {
            _panel = new Border
            {
                Width = 0, // Start collapsed
                Background = new LinearGradientBrush(
                    Color.FromRgb(18, 22, 28),
                    Color.FromRgb(13, 17, 23),
                    90),
                BorderBrush = new SolidColorBrush(Color.FromRgb(48, 54, 61)),
                BorderThickness = new Thickness(1, 0, 0, 0),
                ClipToBounds = true
            };

            _content = new StackPanel
            {
                Width = PanelWidth,
                Margin = new Thickness(12)
            };

            // Header
            var header = new Grid { Margin = new Thickness(0, 0, 0, 16) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var title = new TextBlock
            {
                Text = "INSPECTOR",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(110, 118, 129)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(title, 0);
            header.Children.Add(title);

            var closeBtn = CreateIconButton("✕", () => Toggle());
            Grid.SetColumn(closeBtn, 1);
            header.Children.Add(closeBtn);

            _content.Children.Add(header);

            // Add sections
            _content.Children.Add(CreateModeSection());
            _content.Children.Add(CreateActiveContextSection());
            _content.Children.Add(CreateSecuritySection());
            _content.Children.Add(CreateVoiceSettingsSection());

            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = _content
            };

            _panel.Child = scrollViewer;
            return _panel;
        }

        private Border CreateModeSection()
        {
            var section = CreateSection("CURRENT MODE");
            var content = (StackPanel)section.Child;

            _modeText = new TextBlock
            {
                Text = "Chat",
                FontSize = 14,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 212, 255))
            };
            content.Children.Add(_modeText);

            return section;
        }

        private Border CreateActiveContextSection()
        {
            var section = CreateSection("ACTIVE CONTEXT");
            var content = (StackPanel)section.Child;

            // App name
            var appLabel = new TextBlock
            {
                Text = "Application",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(110, 118, 129)),
                Margin = new Thickness(0, 0, 0, 2)
            };
            content.Children.Add(appLabel);

            _activeAppText = new TextBlock
            {
                Text = "None",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(230, 237, 243)),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            content.Children.Add(_activeAppText);

            // Window title
            var titleLabel = new TextBlock
            {
                Text = "Window",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(110, 118, 129)),
                Margin = new Thickness(0, 8, 0, 2)
            };
            content.Children.Add(titleLabel);

            _windowTitleText = new TextBlock
            {
                Text = "—",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158)),
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap
            };
            content.Children.Add(_windowTitleText);

            return section;
        }

        private Border CreateSecuritySection()
        {
            var section = CreateSection("SECURITY STATUS");
            var content = (StackPanel)section.Child;

            // Status indicator
            var statusRow = new StackPanel { Orientation = Orientation.Horizontal };
            var statusDot = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = new SolidColorBrush(Color.FromRgb(63, 185, 80)),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            statusRow.Children.Add(statusDot);

            _securityStatusText = new TextBlock
            {
                Text = "Protected",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(63, 185, 80)),
                VerticalAlignment = VerticalAlignment.Center
            };
            statusRow.Children.Add(_securityStatusText);
            content.Children.Add(statusRow);

            // Last scan
            _lastScanText = new TextBlock
            {
                Text = "Last scan: Never",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158)),
                Margin = new Thickness(0, 8, 0, 0)
            };
            content.Children.Add(_lastScanText);

            // Definitions age
            _definitionsAgeText = new TextBlock
            {
                Text = "Definitions: Up to date",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158)),
                Margin = new Thickness(0, 4, 0, 8)
            };
            content.Children.Add(_definitionsAgeText);

            // Quick scan button
            var scanBtn = new Button
            {
                Content = "🔍 Quick Scan",
                Background = new SolidColorBrush(Color.FromRgb(33, 38, 45)),
                Foreground = new SolidColorBrush(Color.FromRgb(0, 212, 255)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(48, 54, 61)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 6, 12, 6),
                Cursor = System.Windows.Input.Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            scanBtn.Click += (s, e) => OnQuickScanClicked?.Invoke();
            content.Children.Add(scanBtn);

            return section;
        }

        private Border CreateVoiceSettingsSection()
        {
            var section = CreateSection("VOICE SETTINGS");
            var content = (StackPanel)section.Child;

            // Speed slider
            var speedLabel = new TextBlock
            {
                Text = "Speed",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158)),
                Margin = new Thickness(0, 0, 0, 4)
            };
            content.Children.Add(speedLabel);

            _voiceSpeedSlider = new Slider
            {
                Minimum = 0.5,
                Maximum = 2.0,
                Value = 1.0,
                Margin = new Thickness(0, 0, 0, 12)
            };
            _voiceSpeedSlider.ValueChanged += (s, e) => OnVoiceSpeedChanged?.Invoke(e.NewValue);
            content.Children.Add(_voiceSpeedSlider);

            // Stability slider
            var stabilityLabel = new TextBlock
            {
                Text = "Stability",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158)),
                Margin = new Thickness(0, 0, 0, 4)
            };
            content.Children.Add(stabilityLabel);

            _voiceStabilitySlider = new Slider
            {
                Minimum = 0,
                Maximum = 1,
                Value = 0.5,
                Margin = new Thickness(0, 0, 0, 8)
            };
            _voiceStabilitySlider.ValueChanged += (s, e) => OnVoiceStabilityChanged?.Invoke(e.NewValue);
            content.Children.Add(_voiceStabilitySlider);

            return section;
        }

        private Border CreateSection(string title)
        {
            var section = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(28, 33, 40)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(48, 54, 61)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var content = new StackPanel();

            var header = new TextBlock
            {
                Text = title,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(110, 118, 129)),
                Margin = new Thickness(0, 0, 0, 8)
            };
            content.Children.Add(header);

            section.Child = content;
            return section;
        }

        private Button CreateIconButton(string icon, Action onClick)
        {
            var btn = new Button
            {
                Content = icon,
                Width = 28,
                Height = 28,
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158)),
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                FontSize = 12
            };
            btn.Click += (s, e) => onClick();
            return btn;
        }

        /// <summary>
        /// Toggle panel visibility with animation
        /// </summary>
        public void Toggle()
        {
            if (_panel == null) return;

            _isVisible = !_isVisible;
            var targetWidth = _isVisible ? PanelWidth : 0;

            var animation = new DoubleAnimation(targetWidth, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };

            _panel.BeginAnimation(FrameworkElement.WidthProperty, animation);
            VisibilityChanged?.Invoke(_isVisible);
        }

        /// <summary>
        /// Update active context display
        /// </summary>
        public void UpdateActiveContext(string appName, string windowTitle)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (_activeAppText != null)
                    _activeAppText.Text = string.IsNullOrEmpty(appName) ? "None" : appName;
                if (_windowTitleText != null)
                    _windowTitleText.Text = string.IsNullOrEmpty(windowTitle) ? "—" : windowTitle;
            });
        }

        /// <summary>
        /// Update current mode display
        /// </summary>
        public void UpdateMode(string mode)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (_modeText != null)
                    _modeText.Text = mode;
            });
        }

        /// <summary>
        /// Update security status display
        /// </summary>
        public void UpdateSecurityStatus(string status, DateTime? lastScan, string definitionsAge)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (_securityStatusText != null)
                    _securityStatusText.Text = status;
                if (_lastScanText != null)
                    _lastScanText.Text = lastScan.HasValue 
                        ? $"Last scan: {lastScan.Value:g}" 
                        : "Last scan: Never";
                if (_definitionsAgeText != null)
                    _definitionsAgeText.Text = $"Definitions: {definitionsAge}";
            });
        }

        // Events for external handling
        public event Action? OnQuickScanClicked;
        public event Action<double>? OnVoiceSpeedChanged;
        public event Action<double>? OnVoiceStabilityChanged;
        
        /// <summary>
        /// Invoke the quick scan event
        /// </summary>
        public void InvokeQuickScan() => OnQuickScanClicked?.Invoke();
    }
}