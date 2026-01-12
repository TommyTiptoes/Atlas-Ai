using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using MinimalApp.Agent;

namespace MinimalApp.UI
{
    public partial class SecurityAlertWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private readonly InstallationAlert _alert;
        private readonly SecurityInsight _insight;
        private string _userDecision = "";
        private string _fileName = "";
        private string _filePath = "";
        private string _fileSize = "";
        private string _fileType = "";
        private string _publisherName = "Unknown";
        private string _signedIcon = "?";
        private string _signatureStatus = "";
        private Brush _signatureColor = Brushes.Gray;
        private string _severityText = "ANALYZING...";
        private Brush _threatColor = new SolidColorBrush(Color.FromRgb(34, 211, 238));
        private Brush _threatBgColor = new SolidColorBrush(Color.FromArgb(40, 34, 211, 238));
        private Color _threatColorValue = Color.FromRgb(34, 211, 238);
        private string _recommendation = "Analyzing file...";
        private Brush _recommendationBg = new SolidColorBrush(Color.FromRgb(12, 12, 18));
        private Visibility _virusTotalVisibility = Visibility.Collapsed;
        private Visibility _scanningVisibility = Visibility.Visible;
        private Visibility _bloatwareVisibility = Visibility.Collapsed;
        private Visibility _safeVisibility = Visibility.Collapsed;
        private Visibility _trustButtonVisibility = Visibility.Collapsed;
        private string _vtClean = "0 Clean";
        private string _vtMalicious = "0 Threats";
        private string _scanStatus = "Scanning file...";
        private int _scanProgress = 0;
        private string _bloatwareReason = "";
        public string FileName { get => _fileName; set { _fileName = value; OnPropertyChanged(); } }
        public string FilePath { get => _filePath; set { _filePath = value; OnPropertyChanged(); } }
        public string FileSize { get => _fileSize; set { _fileSize = value; OnPropertyChanged(); } }
        public string FileType { get => _fileType; set { _fileType = value; OnPropertyChanged(); } }
        public string PublisherName { get => _publisherName; set { _publisherName = value; OnPropertyChanged(); } }
        public string SignedIcon { get => _signedIcon; set { _signedIcon = value; OnPropertyChanged(); } }
        public string SignatureStatus { get => _signatureStatus; set { _signatureStatus = value; OnPropertyChanged(); } }
        public Brush SignatureColor { get => _signatureColor; set { _signatureColor = value; OnPropertyChanged(); } }
        public string SeverityText { get => _severityText; set { _severityText = value; OnPropertyChanged(); } }
        public Brush ThreatColor { get => _threatColor; set { _threatColor = value; OnPropertyChanged(); } }
        public Brush ThreatBgColor { get => _threatBgColor; set { _threatBgColor = value; OnPropertyChanged(); } }
        public Color ThreatColorValue { get => _threatColorValue; set { _threatColorValue = value; OnPropertyChanged(); } }
        public string Recommendation { get => _recommendation; set { _recommendation = value; OnPropertyChanged(); } }
        public Brush RecommendationBg { get => _recommendationBg; set { _recommendationBg = value; OnPropertyChanged(); } }
        public Visibility VirusTotalVisibility { get => _virusTotalVisibility; set { _virusTotalVisibility = value; OnPropertyChanged(); } }
        public Visibility ScanningVisibility { get => _scanningVisibility; set { _scanningVisibility = value; OnPropertyChanged(); } }
        public Visibility BloatwareVisibility { get => _bloatwareVisibility; set { _bloatwareVisibility = value; OnPropertyChanged(); } }
        public Visibility SafeVisibility { get => _safeVisibility; set { _safeVisibility = value; OnPropertyChanged(); } }
        public Visibility TrustButtonVisibility { get => _trustButtonVisibility; set { _trustButtonVisibility = value; OnPropertyChanged(); } }
        public string VTClean { get => _vtClean; set { _vtClean = value; OnPropertyChanged(); } }
        public string VTMalicious { get => _vtMalicious; set { _vtMalicious = value; OnPropertyChanged(); } }
        public string ScanStatus { get => _scanStatus; set { _scanStatus = value; OnPropertyChanged(); } }
        public int ScanProgress { get => _scanProgress; set { _scanProgress = value; OnPropertyChanged(); } }
        public string BloatwareReason { get => _bloatwareReason; set { _bloatwareReason = value; OnPropertyChanged(); } }
        public string UserDecision => _userDecision;
        public SecurityAlertWindow(InstallationAlert alert, SecurityInsight insight = null)
        {
            InitializeComponent();
            DataContext = this;
            _alert = alert;
            _insight = insight;
            Closing += (s, e) => { if (string.IsNullOrEmpty(_userDecision)) { e.Cancel = true; MessageBox.Show("Please make a decision.", "Required", MessageBoxButton.OK, MessageBoxImage.Warning); } };
            LoadFileInfo();
            _ = AnalyzeFileAsync();
        }
        private void LoadFileInfo()
        {
            FileName = _alert.FileName;
            FilePath = _alert.FilePath;
            try { if (_alert.FileSize > 0) { var sizeMB = _alert.FileSize / (1024.0 * 1024.0); FileSize = sizeMB >= 1 ? sizeMB.ToString("F1") + " MB" : (_alert.FileSize / 1024.0).ToString("F0") + " KB"; } else if (File.Exists(_alert.FilePath)) { var info = new FileInfo(_alert.FilePath); var sizeMB = info.Length / (1024.0 * 1024.0); FileSize = sizeMB >= 1 ? sizeMB.ToString("F1") + " MB" : (info.Length / 1024.0).ToString("F0") + " KB"; } } catch { FileSize = "Unknown size"; }
            var ext = Path.GetExtension(_alert.FileName).ToLower();
            FileType = ext == ".exe" ? "Executable" : ext == ".msi" ? "Windows Installer" : ext == ".msix" ? "App Package" : (ext == ".bat" || ext == ".cmd") ? "Script" : ext == ".dll" ? "Library" : ext.ToUpper().TrimStart('.');
            if (!string.IsNullOrEmpty(_alert.Publisher) && _alert.Publisher != "Unknown") { PublisherName = CleanPublisherName(_alert.Publisher); if (_alert.IsSigned) { SignedIcon = "V"; SignatureStatus = "Digitally signed"; SignatureColor = new SolidColorBrush(Color.FromRgb(34, 197, 94)); TrustButtonVisibility = _alert.IsTrusted ? Visibility.Collapsed : Visibility.Visible; } else { SignedIcon = "!"; SignatureStatus = "Not digitally signed"; SignatureColor = new SolidColorBrush(Color.FromRgb(245, 158, 11)); } }
            else { PublisherName = "Unknown Publisher"; SignedIcon = "X"; SignatureStatus = "No signature"; SignatureColor = new SolidColorBrush(Color.FromRgb(239, 68, 68)); }
        }
        private string CleanPublisherName(string publisher) { if (publisher.Contains("CN=")) { var start = publisher.IndexOf("CN=") + 3; var end = publisher.IndexOf(',', start); if (end == -1) end = publisher.Length; return publisher.Substring(start, end - start).Trim(); } return publisher; }
        private async Task AnalyzeFileAsync()
        {
            try { ScanStatus = "Checking digital signature..."; ScanProgress = 20; await Task.Delay(300); ScanStatus = "Checking for bloatware patterns..."; ScanProgress = 40; await Task.Delay(300); CheckBloatware(); ScanStatus = "Checking VirusTotal database..."; ScanProgress = 60; if (_alert.OnlineVerified) { VirusTotalVisibility = Visibility.Visible; VTClean = _alert.VirusTotalClean + " Clean"; VTMalicious = _alert.VirusTotalMalicious + " Threats"; } await Task.Delay(300); ScanStatus = "Generating recommendation..."; ScanProgress = 80; await Task.Delay(200); ScanProgress = 100; ScanningVisibility = Visibility.Collapsed; UpdateSeverityUI(); GenerateRecommendation(); }
            catch (Exception ex) { Debug.WriteLine("[SecurityAlert] Analysis error: " + ex.Message); ScanningVisibility = Visibility.Collapsed; UpdateSeverityUI(); GenerateRecommendation(); }
        }
        private void CheckBloatware() { if (_alert.IsBloatware) { BloatwareVisibility = Visibility.Visible; BloatwareReason = "This file matches known bloatware/PUP patterns."; return; } var lowerName = _alert.FileName.ToLower(); if (lowerName.Contains("toolbar")) { BloatwareVisibility = Visibility.Visible; BloatwareReason = "May install browser toolbars"; } else if (lowerName.Contains("optimizer")) { BloatwareVisibility = Visibility.Visible; BloatwareReason = "Fake optimization software"; } }
        private void UpdateSeverityUI()
        {
            var severity = _alert.RiskLevel;
            if (_alert.VirusTotalMalicious > 5) severity = SecurityRiskLevel.High;
            else if (_alert.VirusTotalMalicious > 0) severity = SecurityRiskLevel.Medium;
            if (_alert.IsBloatware) severity = SecurityRiskLevel.High;
            if (severity == SecurityRiskLevel.High) { SeverityText = "HIGH RISK"; ThreatColorValue = Color.FromRgb(239, 68, 68); RecommendationBg = new SolidColorBrush(Color.FromRgb(46, 26, 26)); }
            else if (severity == SecurityRiskLevel.Medium) { SeverityText = "MEDIUM RISK"; ThreatColorValue = Color.FromRgb(245, 158, 11); RecommendationBg = new SolidColorBrush(Color.FromRgb(46, 42, 26)); }
            else if (severity == SecurityRiskLevel.Low) { SeverityText = "LOW RISK"; ThreatColorValue = Color.FromRgb(34, 197, 94); RecommendationBg = new SolidColorBrush(Color.FromRgb(26, 46, 26)); }
            else { SeverityText = "UNKNOWN"; ThreatColorValue = Color.FromRgb(34, 211, 238); RecommendationBg = new SolidColorBrush(Color.FromRgb(12, 12, 18)); }
            ThreatColor = new SolidColorBrush(ThreatColorValue);
            ThreatBgColor = new SolidColorBrush(Color.FromArgb(40, ThreatColorValue.R, ThreatColorValue.G, ThreatColorValue.B));
        }
        private void GenerateRecommendation()
        {
            if (_alert.VirusTotalMalicious > 5) Recommendation = "DANGER: " + _alert.VirusTotalMalicious + " security vendors flagged this as malicious. DELETE immediately.";
            else if (_alert.VirusTotalMalicious > 0) Recommendation = "CAUTION: " + _alert.VirusTotalMalicious + " vendor(s) flagged this. Consider quarantining.";
            else if (_alert.IsBloatware) Recommendation = "This appears to be bloatware/PUP. I recommend deleting it.";
            else if (!_alert.IsSigned) Recommendation = "Not digitally signed. Only proceed if from a trusted source.";
            else if (_alert.IsTrusted) Recommendation = "From " + CleanPublisherName(_alert.Publisher) + ", a trusted publisher. Safe to run.";
            else Recommendation = "Signed by " + CleanPublisherName(_alert.Publisher) + ". Should be safe if expected.";
        }
        private void OnBlockClick(object sender, RoutedEventArgs e) { _userDecision = "Delete"; ExecuteAction(); }
        private void OnQuarantineClick(object sender, RoutedEventArgs e) { _userDecision = "Quarantine"; ExecuteAction(); }
        private void OnAllowClick(object sender, RoutedEventArgs e) { _userDecision = "Allow"; ExecuteAction(); }
        private void OnTrustClick(object sender, RoutedEventArgs e) { _userDecision = "Trust"; ExecuteAction(); }
        private void ExecuteAction()
        {
            try { if (_insight != null) SecurityIntelligence.Instance.RecordUserDecision(_insight, _userDecision); if (_userDecision == "Delete" && File.Exists(_alert.FilePath)) { File.Delete(_alert.FilePath); MessageBox.Show("Deleted: " + _alert.FileName, "File Deleted", MessageBoxButton.OK, MessageBoxImage.Information); } else if (_userDecision == "Quarantine" && File.Exists(_alert.FilePath)) { var quarantineDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AtlasAI", "Quarantine"); Directory.CreateDirectory(quarantineDir); File.Move(_alert.FilePath, Path.Combine(quarantineDir, Guid.NewGuid() + "_" + _alert.FileName)); MessageBox.Show("Quarantined: " + _alert.FileName, "File Quarantined", MessageBoxButton.OK, MessageBoxImage.Information); } else if (_userDecision == "Trust") { MessageBox.Show("Added " + CleanPublisherName(_alert.Publisher) + " to trusted publishers.", "Publisher Trusted", MessageBoxButton.OK, MessageBoxImage.Information); } }
            catch (Exception ex) { MessageBox.Show("Error: " + ex.Message, "Action Failed", MessageBoxButton.OK, MessageBoxImage.Error); }
            Close();
        }
        protected void OnPropertyChanged([CallerMemberName] string name = null) { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); }
    }
}
