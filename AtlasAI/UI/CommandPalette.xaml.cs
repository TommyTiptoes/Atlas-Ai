using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace AtlasAI.UI
{
    /// <summary>
    /// Command Palette - Quick action launcher (Ctrl+K)
    /// </summary>
    public partial class CommandPalette : Window
    {
        private List<CommandItem> _allCommands = new();
        private bool _isClosing = false;
        public CommandItem? SelectedCommand { get; private set; }

        public CommandPalette()
        {
            // Add converter for visibility
            Resources.Add("BoolToVisibility", new BooleanToVisibilityConverter());
            
            InitializeComponent();
            InitializeCommands();
            RefreshResults();
            
            Loaded += (s, e) => SearchBox.Focus();
            Closing += (s, e) => _isClosing = true;
        }

        private void InitializeCommands()
        {
            _allCommands = new List<CommandItem>
            {
                // Voice Commands
                new CommandItem("🎤", "Start Voice Input", "Begin listening for voice commands", "Ctrl+Shift+A", "voice_start"),
                new CommandItem("🔊", "Toggle Voice Output", "Enable/disable text-to-speech", "", "voice_toggle"),
                new CommandItem("🎧", "Select Voice", "Choose TTS voice", "", "voice_select"),
                new CommandItem("👂", "Wake Word", "Toggle wake word detection", "", "wake_word"),
                
                // Quick Actions
                new CommandItem("🎨", "Generate Image", "Create an AI-generated image", "", "generate"),
                new CommandItem("💡", "Get Suggestions", "Ask Atlas for suggestions", "", "suggest"),
                new CommandItem("📝", "Summarize", "Summarize text or content", "", "summarize"),
                new CommandItem("💻", "Code Assistant", "Get coding help", "", "code"),
                new CommandItem("✏", "Write", "Help with writing", "", "write"),
                new CommandItem("🔍", "Search", "Search for something", "", "search"),
                
                // Tools (moved from sidebar)
                new CommandItem("🧠", "Memory", "View and manage Atlas memory", "", "memory"),
                new CommandItem("📸", "Take Screenshot", "Capture screen", "", "screenshot"),
                new CommandItem("🖼", "Capture History", "View capture history", "", "capture_history"),
                new CommandItem("📂", "View History", "Open chat history", "Ctrl+H", "history"),
                new CommandItem("📦", "App Manager", "Manage installed apps", "", "uninstaller"),
                new CommandItem("🔬", "Inspector", "Toggle inspector panel", "Ctrl+I", "inspector"),
                new CommandItem("📊", "Status Panel", "Toggle status panel", "", "status_panel"),
                
                // Security
                new CommandItem("🛡️", "Security Suite", "Open security scanner", "", "security"),
                new CommandItem("🔍", "Quick Scan", "Run a quick security scan", "", "quick_scan"),
                new CommandItem("🔄", "Update Database", "Refresh security definitions", "", "update_db"),
                
                // Settings
                new CommandItem("⚙️", "Settings", "Open settings", "", "settings"),
                new CommandItem("🎨", "Toggle Theme", "Switch dark/light theme", "", "theme"),
                new CommandItem("🎯", "Focus Mode", "Toggle focus mode", "Ctrl+F", "focus_mode"),
                
                // Window
                new CommandItem("⛶", "Toggle Fullscreen", "Enter/exit fullscreen", "F11", "fullscreen"),
                new CommandItem("🗑️", "Clear Chat", "Delete chat history", "", "clear_chat"),
                new CommandItem("📜", "New Chat", "Start a new conversation", "", "new_chat"),
                
                // In-App Assistant
                new CommandItem("🌐", "Toggle Overlay", "Show/hide Atlas overlay", "Ctrl+Alt+A", "overlay"),
                new CommandItem("📋", "Get Context", "Capture active window context", "", "context"),
            };
        }

        private void RefreshResults(string? filter = null)
        {
            var results = string.IsNullOrWhiteSpace(filter)
                ? _allCommands
                : _allCommands.Where(c => 
                    c.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    c.Description.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

            ResultsList.ItemsSource = results;
            if (results.Any())
                ResultsList.SelectedIndex = 0;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshResults(SearchBox.Text);
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    SelectedCommand = null;
                    _isClosing = true;
                    Close();
                    break;
                    
                case Key.Enter:
                    ExecuteSelected();
                    break;
                    
                case Key.Down:
                    if (ResultsList.SelectedIndex < ResultsList.Items.Count - 1)
                        ResultsList.SelectedIndex++;
                    e.Handled = true;
                    break;
                    
                case Key.Up:
                    if (ResultsList.SelectedIndex > 0)
                        ResultsList.SelectedIndex--;
                    e.Handled = true;
                    break;
            }
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            if (!_isClosing)
                Close();
        }

        private void ResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Just track selection visually - execute on click or Enter
        }

        private void ResultsList_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Execute on single click
            ExecuteSelected();
        }

        private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Also support double-click
            ExecuteSelected();
        }

        private void ExecuteSelected()
        {
            if (ResultsList.SelectedItem is CommandItem cmd)
            {
                SelectedCommand = cmd;
                DialogResult = true;
                _isClosing = true;
                Close();
            }
        }
    }

    /// <summary>
    /// Represents a command in the palette
    /// </summary>
    public class CommandItem
    {
        public string Icon { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Shortcut { get; set; }
        public string Action { get; set; }
        
        public bool HasDescription => !string.IsNullOrEmpty(Description);
        public bool HasShortcut => !string.IsNullOrEmpty(Shortcut);

        public CommandItem(string icon, string name, string description, string shortcut, string action)
        {
            Icon = icon;
            Name = name;
            Description = description;
            Shortcut = shortcut;
            Action = action;
        }
    }
}