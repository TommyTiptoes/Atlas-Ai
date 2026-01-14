using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using AtlasAI.SecuritySuite.Models;
using AtlasAI.SecuritySuite.Services;

namespace AtlasAI.SecuritySuite.UI
{
    /// <summary>
    /// View model for a finding item in the remediation list
    /// </summary>
    public class FindingItemViewModel : INotifyPropertyChanged
    {
        private bool _isSelected = true;
        
        public SecurityFinding Finding { get; }
        
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
        }
        
        public string FileName => Path.GetFileName(Finding.FilePath);
        public string FilePath => Finding.FilePath;
        public string Category => Finding.Category.ToString();
        public string SeverityText => Finding.Severity.ToString().ToUpper();
        
        public Brush SeverityBackground => Finding.Severity switch
        {
            ThreatSeverity.Critical => new SolidColorBrush(Color.FromArgb(0x33, 0xEF, 0x44, 0x44)),
            ThreatSeverity.High => new SolidColorBrush(Color.FromArgb(0x33, 0xF5, 0x9E, 0x0B)),
            ThreatSeverity.Medium => new SolidColorBrush(Color.FromArgb(0x33, 0xF5, 0x9E, 0x0B)),
            ThreatSeverity.Low => new SolidColorBrush(Color.FromArgb(0x33, 0x22, 0xD3, 0xEE)),
            _ => new SolidColorBrush(Color.FromArgb(0x33, 0x6B, 0x72, 0x80))
        };
        
        public Brush SeverityForeground => Finding.Severity switch
        {
            ThreatSeverity.Critical => new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)),
            ThreatSeverity.High => new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)),
            ThreatSeverity.Medium => new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)),
            ThreatSeverity.Low => new SolidColorBrush(Color.FromRgb(0x22, 0xD3, 0xEE)),
            _ => new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80))
        };
        
        public FindingItemViewModel(SecurityFinding finding) => Finding = finding;
        
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
    
    /// <summary>
    /// Result of the remediation modal
    /// </summary>
    public class RemediationResult
    {
        public int Quarantined { get; set; }
        public int Deleted { get; set; }
        public int Kept { get; set; }
        public List<string> Errors { get; set; } = new();
        
        public string Summary
        {
            get
            {
                var parts = new List<string>();
                if (Quarantined > 0) parts.Add($"Quarantined {Quarantined} item{(Quarantined == 1 ? "" : "s")}");
                if (Deleted > 0) parts.Add($"Deleted {Deleted} item{(Deleted == 1 ? "" : "s")}");
                if (Kept > 0) parts.Add($"Kept {Kept} item{(Kept == 1 ? "" : "s")} for monitoring");
                return parts.Count > 0 ? string.Join(". ", parts) + "." : "No actions taken.";
            }
        }
    }
    
    public partial class ThreatRemediationModal : Window
    {
        private readonly List<FindingItemViewModel> _items = new();
        private readonly QuarantineManager _quarantineManager;
        
        public RemediationResult Result { get; } = new();
        
        /// <summary>
        /// Event raised when AI assistant should speak
        /// </summary>
        public event Action<string>? AISpeak;
        
        public ThreatRemediationModal(IEnumerable<SecurityFinding> findings, QuarantineManager quarantineManager)
        {
            InitializeComponent();
            _quarantineManager = quarantineManager;
            
            // Filter to only show actionable findings (above Info)
            var actionableFindings = findings
                .Where(f => f.Severity > ThreatSeverity.Info)
                .OrderByDescending(f => f.Severity)
                .ToList();
            
            foreach (var finding in actionableFindings)
            {
                _items.Add(new FindingItemViewModel(finding));
            }
            
            FindingsList.ItemsSource = _items;
            UpdateUI();
        }
        
        private void UpdateUI()
        {
            var selected = _items.Count(i => i.IsSelected);
            SelectionCount.Text = $"{selected} item{(selected == 1 ? "" : "s")} selected";
            
            // Update header based on severity
            var maxSeverity = _items.Any() ? _items.Max(i => i.Finding.Severity) : ThreatSeverity.Info;
            
            HeaderTitle.Text = maxSeverity switch
            {
                ThreatSeverity.Critical => "CRITICAL THREATS DETECTED",
                ThreatSeverity.High => "HIGH SEVERITY THREATS DETECTED",
                _ => "THREATS DETECTED"
            };
            
            HeaderTitle.Foreground = maxSeverity switch
            {
                ThreatSeverity.Critical => new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)),
                ThreatSeverity.High => new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)),
                _ => new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B))
            };
            
            SeverityText.Text = $"Severity: {maxSeverity} • {_items.Count} threat{(_items.Count == 1 ? "" : "s")} found";
            SeverityText.Foreground = HeaderTitle.Foreground;
            
            // Update AI message based on severity
            AIMessage.Text = maxSeverity switch
            {
                ThreatSeverity.Critical => "⚠️ I found CRITICAL threats that require immediate action. I strongly recommend quarantining these items now.",
                ThreatSeverity.High => "I found high-severity threats that should not be ignored. Do you want me to quarantine them?",
                _ => "I found some items that may pose a risk. Would you like me to help you deal with them?"
            };
            
            // Enable/disable buttons based on selection
            var hasSelection = selected > 0;
            KeepBtn.IsEnabled = hasSelection;
            QuarantineBtn.IsEnabled = hasSelection;
            DeleteBtn.IsEnabled = hasSelection;
        }
        
        private void SelectAll_Checked(object sender, RoutedEventArgs e)
        {
            foreach (var item in _items) item.IsSelected = true;
            UpdateUI();
        }
        
        private void SelectAll_Unchecked(object sender, RoutedEventArgs e)
        {
            foreach (var item in _items) item.IsSelected = false;
            UpdateUI();
        }
        
        private void Item_CheckChanged(object sender, RoutedEventArgs e)
        {
            UpdateUI();
            // Update select all checkbox state
            var allSelected = _items.All(i => i.IsSelected);
            var noneSelected = _items.All(i => !i.IsSelected);
            SelectAllCheckbox.IsChecked = allSelected ? true : (noneSelected ? false : null);
        }
        
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
        
        private async void Keep_Click(object sender, RoutedEventArgs e)
        {
            var selected = _items.Where(i => i.IsSelected).ToList();
            if (!selected.Any()) return;
            
            // Show warning for high/critical items
            var highSeverity = selected.Where(i => i.Finding.Severity >= ThreatSeverity.High).ToList();
            if (highSeverity.Any())
            {
                var result = ShowConfirmation(
                    "Keep High-Severity Threats?",
                    $"You're choosing to keep {highSeverity.Count} high-severity item{(highSeverity.Count == 1 ? "" : "s")}.\n\n" +
                    "Atlas will continue monitoring these files, but they may pose a risk.\n\n" +
                    "Are you sure you want to keep them?",
                    "Keep Anyway", "Cancel");
                
                if (!result) return;
            }
            
            Result.Kept = selected.Count;
            AISpeak?.Invoke($"Understood. I'll keep monitoring {selected.Count} item{(selected.Count == 1 ? "" : "s")}. Stay vigilant.");
            
            // Remove kept items from list
            foreach (var item in selected)
            {
                _items.Remove(item);
            }
            
            if (!_items.Any())
            {
                DialogResult = true;
                Close();
            }
            else
            {
                FindingsList.ItemsSource = null;
                FindingsList.ItemsSource = _items;
                UpdateUI();
            }
        }
        
        private async void Quarantine_Click(object sender, RoutedEventArgs e)
        {
            var selected = _items.Where(i => i.IsSelected).ToList();
            if (!selected.Any()) return;
            
            // Disable buttons during operation
            SetButtonsEnabled(false);
            
            int success = 0;
            var errors = new List<string>();
            
            foreach (var item in selected)
            {
                try
                {
                    var (ok, msg) = await _quarantineManager.QuarantineFileAsync(item.FilePath, item.Finding);
                    if (ok)
                    {
                        success++;
                        _items.Remove(item);
                    }
                    else
                    {
                        errors.Add($"{item.FileName}: {msg}");
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"{item.FileName}: {ex.Message}");
                }
            }
            
            Result.Quarantined += success;
            Result.Errors.AddRange(errors);
            
            if (errors.Any())
            {
                ShowInfo("Quarantine Results", 
                    $"✅ Quarantined: {success}\n❌ Failed: {errors.Count}\n\n" +
                    string.Join("\n", errors.Take(5)));
            }
            
            AISpeak?.Invoke(success > 0 
                ? $"Done. I've quarantined {success} item{(success == 1 ? "" : "s")}. They're safely isolated now."
                : "I couldn't quarantine those items. Check the error details.");
            
            if (!_items.Any())
            {
                DialogResult = true;
                Close();
            }
            else
            {
                FindingsList.ItemsSource = null;
                FindingsList.ItemsSource = _items;
                UpdateUI();
                SetButtonsEnabled(true);
            }
        }
        
        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            var selected = _items.Where(i => i.IsSelected).ToList();
            if (!selected.Any()) return;
            
            // Extra confirmation for high/critical
            var highSeverity = selected.Where(i => i.Finding.Severity >= ThreatSeverity.High).ToList();
            var confirmMsg = highSeverity.Any()
                ? $"You're about to PERMANENTLY DELETE {selected.Count} item{(selected.Count == 1 ? "" : "s")} " +
                  $"including {highSeverity.Count} high-severity threat{(highSeverity.Count == 1 ? "" : "s")}.\n\n" +
                  "This action CANNOT be undone.\n\nAre you absolutely sure?"
                : $"You're about to permanently delete {selected.Count} item{(selected.Count == 1 ? "" : "s")}.\n\n" +
                  "This action cannot be undone. Continue?";
            
            var result = ShowConfirmation("Confirm Permanent Deletion", confirmMsg, "Delete Permanently", "Cancel");
            if (!result) return;
            
            // Second confirmation for critical items
            var critical = selected.Where(i => i.Finding.Severity == ThreatSeverity.Critical).ToList();
            if (critical.Any())
            {
                var result2 = ShowConfirmation("Final Confirmation",
                    $"⚠️ FINAL WARNING ⚠️\n\n" +
                    $"You are deleting {critical.Count} CRITICAL threat{(critical.Count == 1 ? "" : "s")}.\n\n" +
                    "Type 'DELETE' to confirm this action.",
                    "I Understand, Delete", "Cancel");
                if (!result2) return;
            }
            
            SetButtonsEnabled(false);
            
            int success = 0;
            var errors = new List<string>();
            
            foreach (var item in selected)
            {
                try
                {
                    if (File.Exists(item.FilePath))
                    {
                        File.Delete(item.FilePath);
                        success++;
                        _items.Remove(item);
                    }
                    else
                    {
                        // File already gone
                        success++;
                        _items.Remove(item);
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"{item.FileName}: {ex.Message}");
                }
            }
            
            Result.Deleted += success;
            Result.Errors.AddRange(errors);
            
            if (errors.Any())
            {
                ShowInfo("Deletion Results",
                    $"✅ Deleted: {success}\n❌ Failed: {errors.Count}\n\n" +
                    string.Join("\n", errors.Take(5)));
            }
            
            AISpeak?.Invoke(success > 0
                ? $"Done. I've permanently deleted {success} item{(success == 1 ? "" : "s")}. They're gone for good."
                : "I couldn't delete those items. They may be in use or protected.");
            
            if (!_items.Any())
            {
                DialogResult = true;
                Close();
            }
            else
            {
                FindingsList.ItemsSource = null;
                FindingsList.ItemsSource = _items;
                UpdateUI();
                SetButtonsEnabled(true);
            }
        }
        
        private void SetButtonsEnabled(bool enabled)
        {
            KeepBtn.IsEnabled = enabled;
            QuarantineBtn.IsEnabled = enabled;
            DeleteBtn.IsEnabled = enabled;
        }
        
        private bool ShowConfirmation(string title, string message, string yesText, string noText)
        {
            // Create a themed confirmation dialog
            var dialog = new Window
            {
                Title = title,
                Width = 450,
                Height = 250,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Topmost = true
            };
            
            var result = false;
            
            var border = new System.Windows.Controls.Border
            {
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(Color.FromRgb(0x0A, 0x0C, 0x14)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x4D, 0x22, 0xD3, 0xEE)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(24)
            };
            
            var grid = new System.Windows.Controls.Grid();
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
            
            var titleBlock = new System.Windows.Controls.TextBlock
            {
                Text = title,
                Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)),
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 12)
            };
            System.Windows.Controls.Grid.SetRow(titleBlock, 0);
            grid.Children.Add(titleBlock);
            
            var msgBlock = new System.Windows.Controls.TextBlock
            {
                Text = message,
                Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)),
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap
            };
            System.Windows.Controls.Grid.SetRow(msgBlock, 1);
            grid.Children.Add(msgBlock);
            
            var btnPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 16, 0, 0)
            };
            System.Windows.Controls.Grid.SetRow(btnPanel, 2);
            
            var noBtn = new System.Windows.Controls.Button
            {
                Content = noText,
                Padding = new Thickness(16, 8, 16, 8),
                Margin = new Thickness(0, 0, 12, 0),
                Background = new SolidColorBrush(Color.FromArgb(0x1A, 0x6B, 0x72, 0x80)),
                Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x4D, 0x6B, 0x72, 0x80)),
                Cursor = Cursors.Hand
            };
            noBtn.Click += (s, e) => { result = false; dialog.Close(); };
            btnPanel.Children.Add(noBtn);
            
            var yesBtn = new System.Windows.Controls.Button
            {
                Content = yesText,
                Padding = new Thickness(16, 8, 16, 8),
                Background = new SolidColorBrush(Color.FromArgb(0x1A, 0xEF, 0x44, 0x44)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x4D, 0xEF, 0x44, 0x44)),
                Cursor = Cursors.Hand
            };
            yesBtn.Click += (s, e) => { result = true; dialog.Close(); };
            btnPanel.Children.Add(yesBtn);
            
            grid.Children.Add(btnPanel);
            border.Child = grid;
            dialog.Content = border;
            
            dialog.ShowDialog();
            return result;
        }
        
        private void ShowInfo(string title, string message)
        {
            var dialog = new Window
            {
                Title = title,
                Width = 400,
                Height = 220,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Topmost = true
            };
            
            var border = new System.Windows.Controls.Border
            {
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(Color.FromRgb(0x0A, 0x0C, 0x14)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x4D, 0x22, 0xD3, 0xEE)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(24)
            };
            
            var stack = new System.Windows.Controls.StackPanel();
            
            stack.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = title,
                Foreground = new SolidColorBrush(Color.FromRgb(0x22, 0xD3, 0xEE)),
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 12)
            });
            
            stack.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = message,
                Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            });
            
            var okBtn = new System.Windows.Controls.Button
            {
                Content = "OK",
                Padding = new Thickness(24, 8, 24, 8),
                Margin = new Thickness(0, 16, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                Background = new SolidColorBrush(Color.FromArgb(0x1A, 0x22, 0xD3, 0xEE)),
                Foreground = new SolidColorBrush(Color.FromRgb(0x22, 0xD3, 0xEE)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x4D, 0x22, 0xD3, 0xEE)),
                Cursor = Cursors.Hand
            };
            okBtn.Click += (s, e) => dialog.Close();
            stack.Children.Add(okBtn);
            
            border.Child = stack;
            dialog.Content = border;
            dialog.ShowDialog();
        }
    }
}
