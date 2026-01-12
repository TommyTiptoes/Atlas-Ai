using System.Windows;
using System.Windows.Controls;

namespace MinimalApp.UI.Pages
{
    public partial class CodePage : UserControl
    {
        public CodePage()
        {
            InitializeComponent();
        }

        private void OpenCodeEditor_Click(object sender, RoutedEventArgs e)
        {
            var codeWindow = new CodeEditorWindow();
            codeWindow.Show();
        }
    }
}
