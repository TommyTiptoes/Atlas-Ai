using System.Windows;
using System.Windows.Controls;

namespace AtlasAI.UI.Pages
{
    public partial class CodePage : UserControl
    {
        private static CodeEditorWindow? _codeEditorWindow;
        
        public CodePage()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Automatically open the Code Editor window when this page loads
            OpenCodeEditorWindow();
        }
        
        private void OpenCodeEditorWindow()
        {
            // Reuse existing window if it's still open
            if (_codeEditorWindow != null && _codeEditorWindow.IsLoaded)
            {
                _codeEditorWindow.Activate();
                _codeEditorWindow.WindowState = WindowState.Normal;
                return;
            }
            
            _codeEditorWindow = new CodeEditorWindow();
            _codeEditorWindow.Closed += (s, e) => _codeEditorWindow = null;
            _codeEditorWindow.Show();
        }

        private void OpenCodeEditor_Click(object sender, RoutedEventArgs e)
        {
            OpenCodeEditorWindow();
        }
    }
}
