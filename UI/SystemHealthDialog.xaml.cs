using System;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace AtlasAI.UI
{
    public partial class SystemHealthDialog : Window
    {
        private PerformanceCounter? _cpuCounter;
        
        public SystemHealthDialog()
        {
            InitializeComponent();
            
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _cpuCounter.NextValue(); // Prime it
            }
            catch { }
            
            Loaded += async (s, e) => await RefreshStats();
        }
        
        private async Task RefreshStats()
        {
            StatusText.Text = "Scanning...";
            await Task.Delay(100); // Let UI update
            
            try
            {
                // CPU
                float cpuUsage = 0;
                try
                {
                    await Task.Delay(500); // Need delay for accurate reading
                    cpuUsage = _cpuCounter?.NextValue() ?? 0;
                }
                catch { }
                
                CpuValue.Text = $"{(int)cpuUsage}%";
                CpuBar.Value = cpuUsage;
                CpuDetails.Text = $"{Environment.ProcessorCount} cores available";
                SetBarColor(CpuBar, CpuValue, cpuUsage);
                
                // Memory
                var memInfo = GetMemoryInfo();
                MemValue.Text = $"{memInfo.PercentUsed}%";
                MemBar.Value = memInfo.PercentUsed;
                MemDetails.Text = $"{memInfo.UsedGB:F1} GB used of {memInfo.TotalGB:F1} GB";
                SetBarColor(MemBar, MemValue, memInfo.PercentUsed, "#8b5cf6");
                
                // Disk
                var diskInfo = GetDiskInfo();
                var diskUsedPercent = 100 - (diskInfo.FreeGB / diskInfo.TotalGB * 100);
                DiskValue.Text = $"{(int)diskUsedPercent}%";
                DiskBar.Value = diskUsedPercent;
                DiskDetails.Text = $"C: {diskInfo.FreeGB:F0} GB free of {diskInfo.TotalGB:F0} GB";
                SetBarColor(DiskBar, DiskValue, diskUsedPercent, "#2dd4bf");
                
                // Network
                await CheckNetwork();
                
                // Overall status
                UpdateOverallStatus(cpuUsage, memInfo.PercentUsed, diskUsedPercent);
                
                StatusText.Text = "Scan complete";
                LastUpdated.Text = $"Last updated: {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error: {ex.Message}";
            }
        }
        
        private void SetBarColor(System.Windows.Controls.ProgressBar bar, System.Windows.Controls.TextBlock text, 
                                  double value, string defaultColor = "#22d3ee")
        {
            Color color;
            if (value > 90)
                color = Color.FromRgb(239, 68, 68); // Red
            else if (value > 75)
                color = Color.FromRgb(249, 115, 22); // Orange
            else
                color = (Color)ColorConverter.ConvertFromString(defaultColor);
            
            bar.Foreground = new SolidColorBrush(color);
            text.Foreground = new SolidColorBrush(color);
        }
        
        private async Task CheckNetwork()
        {
            try
            {
                if (!NetworkInterface.GetIsNetworkAvailable())
                {
                    NetworkStatus.Text = "Offline";
                    NetworkStatus.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                    NetworkDot.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                    NetworkDetails.Text = "No network connection";
                    return;
                }
                
                using var ping = new Ping();
                var reply = await ping.SendPingAsync("8.8.8.8", 2000);
                
                if (reply.Status == IPStatus.Success)
                {
                    var latency = reply.RoundtripTime;
                    NetworkStatus.Text = "Connected";
                    NetworkDetails.Text = $"Latency: {latency}ms";
                    
                    Color color;
                    if (latency < 50)
                        color = Color.FromRgb(34, 197, 94); // Green
                    else if (latency < 100)
                        color = Color.FromRgb(249, 115, 22); // Orange
                    else
                        color = Color.FromRgb(239, 68, 68); // Red
                    
                    NetworkStatus.Foreground = new SolidColorBrush(color);
                    NetworkDot.Fill = new SolidColorBrush(color);
                }
                else
                {
                    NetworkStatus.Text = "Limited";
                    NetworkStatus.Foreground = new SolidColorBrush(Color.FromRgb(249, 115, 22));
                    NetworkDot.Fill = new SolidColorBrush(Color.FromRgb(249, 115, 22));
                    NetworkDetails.Text = "Cannot reach internet";
                }
            }
            catch
            {
                NetworkStatus.Text = "Error";
                NetworkDetails.Text = "Could not check network";
            }
        }
        
        private void UpdateOverallStatus(double cpu, double mem, double disk)
        {
            bool hasWarning = cpu > 75 || mem > 75 || disk > 90;
            bool hasCritical = cpu > 90 || mem > 90 || disk > 95;
            
            if (hasCritical)
            {
                OverallStatusBorder.Background = new SolidColorBrush(Color.FromArgb(0x20, 239, 68, 68));
                OverallStatusBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 239, 68, 68));
                OverallIcon.Text = "⚠";
                OverallIcon.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                OverallText.Text = "System Under Stress";
                OverallText.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            }
            else if (hasWarning)
            {
                OverallStatusBorder.Background = new SolidColorBrush(Color.FromArgb(0x20, 249, 115, 22));
                OverallStatusBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 249, 115, 22));
                OverallIcon.Text = "!";
                OverallIcon.Foreground = new SolidColorBrush(Color.FromRgb(249, 115, 22));
                OverallText.Text = "Elevated Usage";
                OverallText.Foreground = new SolidColorBrush(Color.FromRgb(249, 115, 22));
            }
            else
            {
                OverallStatusBorder.Background = new SolidColorBrush(Color.FromArgb(0x20, 34, 197, 94));
                OverallStatusBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 34, 197, 94));
                OverallIcon.Text = "✓";
                OverallIcon.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                OverallText.Text = "System Healthy";
                OverallText.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
            }
        }
        
        private (double TotalGB, double UsedGB, int PercentUsed) GetMemoryInfo()
        {
            try
            {
                var gcMemory = GC.GetGCMemoryInfo();
                var totalBytes = gcMemory.TotalAvailableMemoryBytes;
                
                // Use performance counter for more accurate reading
                using var ramCounter = new PerformanceCounter("Memory", "Available MBytes");
                var availableMB = ramCounter.NextValue();
                var totalMB = totalBytes / 1024.0 / 1024.0;
                var usedMB = totalMB - availableMB;
                
                return (totalMB / 1024.0, usedMB / 1024.0, (int)((usedMB / totalMB) * 100));
            }
            catch
            {
                return (0, 0, 0);
            }
        }
        
        private (double TotalGB, double FreeGB) GetDiskInfo()
        {
            try
            {
                var drive = new DriveInfo("C");
                return (drive.TotalSize / 1024.0 / 1024.0 / 1024.0, 
                        drive.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0);
            }
            catch
            {
                return (0, 0);
            }
        }
        
        private void Close_Click(object sender, RoutedEventArgs e) => Close();
        
        private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshStats();
        
        protected override void OnClosed(EventArgs e)
        {
            _cpuCounter?.Dispose();
            base.OnClosed(e);
        }
    }
}
