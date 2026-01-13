using System.Windows;
using System.Windows.Controls;
using AtlasAI.SecuritySuite;

namespace AtlasAI.UI.Pages
{
    public partial class SecurityPage : UserControl
    {
        public SecurityPage()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Page is loaded and ready
        }

        private void OpenSecuritySuite_Click(object sender, RoutedEventArgs e)
        {
            // Open the full SecuritySuiteWindow
            var securityWindow = new SecuritySuiteWindow();
            securityWindow.Show();
        }
    }
}
