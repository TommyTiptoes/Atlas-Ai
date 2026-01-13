using System.Windows;
using System.Windows.Input;

namespace AtlasAI.UI
{
    public partial class AtlasDialog : Window
    {
        public enum DialogResult { OK, Yes, No, Cancel }
        public new DialogResult Result { get; private set; } = DialogResult.Cancel;

        public AtlasDialog()
        {
            InitializeComponent();
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e) { Result = DialogResult.Cancel; Close(); }
        private void Ok_Click(object sender, RoutedEventArgs e) { Result = DialogResult.OK; Close(); }
        private void Yes_Click(object sender, RoutedEventArgs e) { Result = DialogResult.Yes; Close(); }
        private void No_Click(object sender, RoutedEventArgs e) { Result = DialogResult.No; Close(); }
        private void Cancel_Click(object sender, RoutedEventArgs e) { Result = DialogResult.Cancel; Close(); }

        public static DialogResult ShowInfo(string message, string title = "Atlas", Window? owner = null)
        {
            var dlg = new AtlasDialog();
            dlg.TitleText.Text = title;
            dlg.ContentText.Text = message;
            dlg.IconText.Text = "i";
            dlg.OkBtn.Visibility = Visibility.Visible;
            if (owner != null) dlg.Owner = owner;
            dlg.ShowDialog();
            return dlg.Result;
        }

        public static DialogResult ShowError(string message, string title = "Error", Window? owner = null)
        {
            var dlg = new AtlasDialog();
            dlg.TitleText.Text = title;
            dlg.ContentText.Text = message;
            dlg.IconText.Text = "X";
            dlg.OkBtn.Visibility = Visibility.Visible;
            if (owner != null) dlg.Owner = owner;
            dlg.ShowDialog();
            return dlg.Result;
        }

        public static DialogResult ShowWarning(string message, string title = "Warning", Window? owner = null)
        {
            var dlg = new AtlasDialog();
            dlg.TitleText.Text = title;
            dlg.ContentText.Text = message;
            dlg.IconText.Text = "!";
            dlg.OkBtn.Visibility = Visibility.Visible;
            if (owner != null) dlg.Owner = owner;
            dlg.ShowDialog();
            return dlg.Result;
        }

        public static DialogResult ShowQuestion(string message, string title = "Confirm", Window? owner = null)
        {
            var dlg = new AtlasDialog();
            dlg.TitleText.Text = title;
            dlg.ContentText.Text = message;
            dlg.IconText.Text = "?";
            dlg.OkBtn.Visibility = Visibility.Collapsed;
            dlg.YesBtn.Visibility = Visibility.Visible;
            dlg.NoBtn.Visibility = Visibility.Visible;
            if (owner != null) dlg.Owner = owner;
            dlg.ShowDialog();
            return dlg.Result;
        }

        public static DialogResult ShowYesNoCancel(string message, string title = "Confirm", Window? owner = null)
        {
            var dlg = new AtlasDialog();
            dlg.TitleText.Text = title;
            dlg.ContentText.Text = message;
            dlg.IconText.Text = "?";
            dlg.OkBtn.Visibility = Visibility.Collapsed;
            dlg.YesBtn.Visibility = Visibility.Visible;
            dlg.NoBtn.Visibility = Visibility.Visible;
            dlg.CancelBtn.Visibility = Visibility.Visible;
            if (owner != null) dlg.Owner = owner;
            dlg.ShowDialog();
            return dlg.Result;
        }
    }
}