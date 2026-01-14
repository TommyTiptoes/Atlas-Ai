using System.Windows;
using System.Windows.Controls;

namespace AtlasAI.Views
{
    /// <summary>
    /// System Control view for the shell mode.
    /// This is a simplified view that directs users to the full System Control window.
    /// </summary>
    public partial class SystemControlView : UserControl
    {
        public SystemControlView()
        {
            InitializeComponent();
        }
        
        private void OpenSystemControlWindow_Click(object sender, RoutedEventArgs e)
        {
            // Open the full SystemControlWindow via MainWindow
            var mainWindow = Application.Current.MainWindow as MainWindow;
            mainWindow?.OpenSystemControlWindow();
        }
    }
}
