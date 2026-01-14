using System.Windows;
using System.Windows.Controls;

namespace AtlasAI.Views
{
    /// <summary>
    /// Settings view for the shell mode.
    /// This is a simplified view that directs users to the full Settings window.
    /// </summary>
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
        }
        
        private void OpenSettingsWindow_Click(object sender, RoutedEventArgs e)
        {
            // Open the full SettingsWindow via MainWindow
            var mainWindow = Application.Current.MainWindow as MainWindow;
            mainWindow?.OpenSettingsWindow();
        }
    }
}
