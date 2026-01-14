using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace AtlasAI.UI
{
    /// <summary>
    /// Toast notification types for different visual styles
    /// </summary>
    public enum ToastType
    {
        Info,
        Success,
        Warning,
        Error
    }

    /// <summary>
    /// Non-blocking toast notification system for Atlas AI
    /// Displays notifications in the bottom-right corner with auto-dismiss
    /// </summary>
    public class ToastNotificationManager
    {
        private static ToastNotificationManager? _instance;
        private StackPanel? _container;
        private readonly Queue<ToastItem> _queue = new();
        private const int MaxVisible = 3;
        private int _visibleCount = 0;

        public static ToastNotificationManager Instance => _instance ??= new ToastNotificationManager();

        /// <summary>
        /// Initialize the toast system with a container panel
        /// </summary>
        public void Initialize(StackPanel container)
        {
            _container = container;
        }

        /// <summary>
        /// Show a toast notification
        /// </summary>
        public void Show(string message, ToastType type = ToastType.Info, int durationMs = 3000)
        {
            if (_container == null) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                var toast = CreateToast(message, type);
                
                if (_visibleCount >= MaxVisible)
                {
                    _queue.Enqueue(new ToastItem { Message = message, Type = type, Duration = durationMs });
                    return;
                }

                ShowToast(toast, durationMs);
            });
        }

        /// <summary>
        /// Show success toast
        /// </summary>
        public void ShowSuccess(string message, int durationMs = 3000)
            => Show(message, ToastType.Success, durationMs);

        /// <summary>
        /// Show warning toast
        /// </summary>
        public void ShowWarning(string message, int durationMs = 4000)
            => Show(message, ToastType.Warning, durationMs);

        /// <summary>
        /// Show error toast
        /// </summary>
        public void ShowError(string message, int durationMs = 5000)
            => Show(message, ToastType.Error, durationMs);

        private Border CreateToast(string message, ToastType type)
        {
            var (icon, borderColor) = type switch
            {
                ToastType.Success => ("✓", Color.FromRgb(63, 185, 80)),
                ToastType.Warning => ("⚠", Color.FromRgb(210, 153, 34)),
                ToastType.Error => ("✕", Color.FromRgb(248, 81, 73)),
                _ => ("ℹ", Color.FromRgb(0, 212, 255))
            };

            var toast = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(28, 33, 40)),
                BorderBrush = new SolidColorBrush(borderColor),
                BorderThickness = new Thickness(1, 1, 1, 1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 8),
                MinWidth = 200,
                MaxWidth = 320,
                Opacity = 0,
                RenderTransform = new TranslateTransform(50, 0),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 16,
                    ShadowDepth = 4,
                    Opacity = 0.3
                }
            };

            var content = new StackPanel { Orientation = Orientation.Horizontal };
            
            // Icon
            content.Children.Add(new TextBlock
            {
                Text = icon,
                FontSize = 14,
                Foreground = new SolidColorBrush(borderColor),
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            });

            // Message
            content.Children.Add(new TextBlock
            {
                Text = message,
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(230, 237, 243)),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 260,
                VerticalAlignment = VerticalAlignment.Center
            });

            toast.Child = content;
            return toast;
        }

        private async void ShowToast(Border toast, int durationMs)
        {
            if (_container == null) return;

            _visibleCount++;
            _container.Children.Insert(0, toast);

            // Slide in animation
            var slideIn = new DoubleAnimation(50, 0, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));

            toast.RenderTransform.BeginAnimation(TranslateTransform.XProperty, slideIn);
            toast.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            // Wait for duration
            await Task.Delay(durationMs);

            // Slide out animation
            var slideOut = new DoubleAnimation(0, 50, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));

            toast.RenderTransform.BeginAnimation(TranslateTransform.XProperty, slideOut);
            toast.BeginAnimation(UIElement.OpacityProperty, fadeOut);

            await Task.Delay(200);

            _container.Children.Remove(toast);
            _visibleCount--;

            // Show queued toast if any
            if (_queue.Count > 0)
            {
                var next = _queue.Dequeue();
                Show(next.Message, next.Type, next.Duration);
            }
        }

        private class ToastItem
        {
            public string Message { get; set; } = "";
            public ToastType Type { get; set; }
            public int Duration { get; set; }
        }
    }
}