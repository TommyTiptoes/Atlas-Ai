using System.Windows;
using System.Windows.Controls;

namespace AtlasAI.Views
{
    /// <summary>
    /// Chat view for the shell mode.
    /// This is a simplified view that directs users to the full Chat window.
    /// </summary>
    public partial class ChatView : UserControl
    {
        public ChatView()
        {
            InitializeComponent();
        }
        
        private void OpenChatWindow_Click(object sender, RoutedEventArgs e)
        {
            // Open the full ChatWindow via MainWindow
            var mainWindow = Application.Current.MainWindow as MainWindow;
            mainWindow?.OpenChatWindow();
        }
    }
}
