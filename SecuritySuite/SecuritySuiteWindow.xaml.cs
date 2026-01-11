using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using MinimalApp.SecuritySuite.Models;
using MinimalApp.SecuritySuite.Services;
using MinimalApp.SecuritySuite.UI;

namespace MinimalApp.SecuritySuite
{
    public partial class SecuritySuiteWindow : Window
    {
        private DispatcherTimer _animationTimer;
        private double _sweepAngle = 0;
        private ScanEngine _scanEngine;
        private List<SecurityFinding> _findings = new List<SecurityFinding>();
        private QuarantineManager _quarantineManager = new QuarantineManager();
        private PerformanceCounter _cpuCounter;
        private int _cpuUpdateCounter = 0;

        public SecuritySuiteWindow()
        {
            InitializeComponent();
            InitializePerformanceCounters();
            InitializeScanEngine();
            StartAnimations();
        }

        private void InitializePerformanceCounters()
        {
            try { _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total"); _cpuCounter.NextValue(); }
            catch { _cpuCounter = null; }
        }

        private void InitializeScanEngine()
        {
            _scanEngine = new ScanEngine();
            _scanEngine.JobUpdated += OnScanJobUpdated;
        }

        private void OnScanJobUpdated(ScanJob job)
        {
            Dispatcher.Invoke(() =>
            {
                ScanProgress.Text = job.ProgressPercent + "%";
                ScanStatusText.Text = job.Status.ToString().ToUpper();
                if (job.ThreatsFound > 0)
                {
                    ThreatCount.Text = job.ThreatsFound.ToString();
                    ThreatAlert.Visibility = Visibility.Visible;
                    ThreatDetails.Text = "Found " + job.ThreatsFound + " potential threat(s).";
                }
            });
        }

        private void StartAnimations()
        {
            // Slower animation to reduce CPU - 100ms instead of 50ms
            _animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _animationTimer.Tick += AnimationTick;
            _animationTimer.Start();
        }

        private void AnimationTick(object sender, EventArgs e)
        {
            _sweepAngle = (_sweepAngle + 3) % 360;
            UpdateScannerAnimation();
            
            // Only update CPU every 5 ticks (500ms) to reduce overhead
            _cpuUpdateCounter++;
            if (_cpuUpdateCounter >= 5)
            {
                _cpuUpdateCounter = 0;
                UpdateCpuMetrics();
            }
        }

        private void UpdateScannerAnimation()
        {
            OuterRing1Rotate.Angle = _sweepAngle;
            OuterRing2Rotate.Angle = -_sweepAngle * 0.7;
            InnerRingRotate.Angle = _sweepAngle * 1.5;
            CubeRotate.Angle = _sweepAngle * 2;
            double cx = 0, cy = 0, r = 140;
            double startAngle = _sweepAngle * Math.PI / 180;
            double endAngle = ((_sweepAngle + 60) % 360) * Math.PI / 180;
            double x1 = cx + r * Math.Cos(startAngle), y1 = cy + r * Math.Sin(startAngle);
            double x2 = cx + r * Math.Cos(endAngle), y2 = cy + r * Math.Sin(endAngle);
            ScanSweep.Data = Geometry.Parse("M " + cx + "," + cy + " L " + x1.ToString("F2") + "," + y1.ToString("F2") + " A " + r + "," + r + " 0 0 1 " + x2.ToString("F2") + "," + y2.ToString("F2") + " Z");
        }

        private void UpdateCpuMetrics()
        {
            try { if (_cpuCounter != null) CpuText.Text = _cpuCounter.NextValue().ToString("F0") + "%"; } catch { }
        }

        private void RunScan_Click(object sender, RoutedEventArgs e)
        {
            if (_scanEngine.IsScanning) return;
            _findings.Clear();
            ThreatAlert.Visibility = Visibility.Collapsed;
            ThreatList.Children.Clear();
            ThreatList.Children.Add(new TextBlock { Text = "Scanning...", Foreground = new SolidColorBrush(Color.FromRgb(0, 212, 255)), FontSize = 10 });
            _ = RunRealScanAsync();
        }

        private void QuickScan_Click(object sender, RoutedEventArgs e) { RunScan_Click(sender, e); }

        private async Task RunRealScanAsync()
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var job = await _scanEngine.StartScanAsync(ScanType.Quick);
                sw.Stop();
                _findings.Clear();
                foreach (var f in job.Findings) _findings.Add(f);
                UpdateThreatList(sw.Elapsed, job);
                if (_findings.Any(f => f.Severity > ThreatSeverity.Info)) await ShowRemediationModalAsync();
                else ShowScanCompleteMessage(sw.Elapsed, job);
            }
            catch (Exception ex) { Dispatcher.Invoke(() => ScanStatusText.Text = "ERROR"); Debug.WriteLine("Scan error: " + ex.Message); }
        }

        private void UpdateThreatList(TimeSpan elapsed, ScanJob job)
        {
            Dispatcher.Invoke(() =>
            {
                ThreatList.Children.Clear();
                if (_findings.Count == 0)
                    ThreatList.Children.Add(new TextBlock { Text = "No threats detected", Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 136)), FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 20, 0, 0) });
                else
                    foreach (var f in _findings.Take(10))
                    {
                        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
                        row.Children.Add(new Ellipse { Width = 6, Height = 6, Fill = new SolidColorBrush(Color.FromRgb(255, 51, 102)), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
                        row.Children.Add(new TextBlock { Text = System.IO.Path.GetFileName(f.FilePath), Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)), FontSize = 9, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 200 });
                        ThreatList.Children.Add(row);
                    }
                ThreatCount.Text = _findings.Count.ToString();
            });
        }

        private async Task ShowRemediationModalAsync()
        {
            await Dispatcher.InvokeAsync(() => { var modal = new ThreatRemediationModal(_findings, _quarantineManager); modal.Owner = this; modal.ShowDialog(); });
        }

        private void ShowScanCompleteMessage(TimeSpan elapsed, ScanJob job)
        {
            Dispatcher.Invoke(() => MessageBox.Show("Scan Complete!\n\nItems Scanned: " + job.FilesScanned.ToString("N0") + "\nTime: " + elapsed.TotalSeconds.ToString("F1") + "s\nThreats: " + job.ThreatsFound, "Atlas AI Security", MessageBoxButton.OK, MessageBoxImage.Information));
        }

        private void SecureNow_Click(object sender, RoutedEventArgs e) { _ = ShowRemediationModalAsync(); }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left) DragMove(); }

        private void Close_Click(object sender, RoutedEventArgs e) { if (_scanEngine.IsScanning) _scanEngine.CancelScan(); _animationTimer?.Stop(); Close(); }
    }
}
