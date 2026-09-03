#nullable enable
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Serilog;

namespace LocoSwap
{
    /// <summary>
    /// Interaction logic for ConsistScanWindow.xaml. Scans every installed route and scenario
    /// for consists that reference vehicles which are not installed, and lists them with the
    /// file path of the offending Scenario.bin (or .ap archive).
    /// </summary>
    public partial class ConsistScanWindow : Wpf.Ui.Controls.FluentWindow, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public System.Collections.ObjectModel.ObservableCollection<BrokenScenario> Results { get; } = new();

        private bool _isScanning;
        public bool IsScanning
        {
            get => _isScanning;
            private set { _isScanning = value; Raise(nameof(IsScanning)); Raise(nameof(ShowEmptyState)); }
        }

        private bool _hasScanned;

        /// <summary>Show the "nothing broken" hint only after a completed scan that found nothing.</summary>
        public bool ShowEmptyState => _hasScanned && !IsScanning && Results.Count == 0;

        private CancellationTokenSource? _cts;

        public ConsistScanWindow()
        {
            InitializeComponent();
            ThemeManager.Watch(this);
            DataContext = this;
            StatusText.Text = L("consist_scan_idle");
        }

        private static string L(string key) => LocalizationSource.Instance[key];

        private void Raise(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private async void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsScanning) return;

            Results.Clear();
            _hasScanned = true;
            IsScanning = true;
            ScanButton.IsEnabled = false;
            CancelButton.IsEnabled = true;
            CopyButton.IsEnabled = false;
            ScanProgress.Value = 0;
            StatusText.Text = L("consist_scan_running");

            _cts = new CancellationTokenSource();
            var progress = new Progress<ConsistScanner.Progress>(p =>
            {
                ScanProgress.Value = p.Total == 0 ? 0 : (double)p.Done / p.Total;
                StatusText.Text = string.Format("{0}  ({1}/{2})  {3}",
                    L("consist_scan_running"), p.Done, p.Total, p.CurrentRoute);
            });

            try
            {
                var results = await ConsistScanner.ScanAsync(progress, _cts.Token);
                foreach (BrokenScenario r in results) Results.Add(r);
                StatusText.Text = string.Format(L("consist_scan_summary"), Results.Count, results.Count);
            }
            catch (OperationCanceledException)
            {
                StatusText.Text = L("consist_scan_cancelled");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Consist scan failed");
                StatusText.Text = ex.Message;
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                IsScanning = false;
                ScanButton.IsEnabled = true;
                CancelButton.IsEnabled = false;
                CopyButton.IsEnabled = Results.Count > 0;
                Raise(nameof(ShowEmptyState));
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (Results.Count == 0) return;

            var sb = new StringBuilder();
            foreach (BrokenScenario r in Results)
            {
                sb.AppendLine(string.Format("{0}  /  {1}  [{2}]", r.RouteName, r.ScenarioName, r.ScenarioId));
                sb.AppendLine("  " + r.Location);
                foreach (string line in r.DetailText.Split('\n'))
                {
                    sb.AppendLine("    " + line.TrimEnd('\r'));
                }
                sb.AppendLine();
            }

            try
            {
                Clipboard.SetText(sb.ToString());
            }
            catch (Exception ex)
            {
                Log.Debug("Could not copy consist report: {0}", ex.Message);
            }
        }

        private void OpenLocation_Click(object sender, RoutedEventArgs e)
        {
            if (ResultList.SelectedItem is not BrokenScenario r) return;
            try
            {
                string arg = File.Exists(r.Location)
                    ? "/select,\"" + r.Location + "\""
                    : "\"" + Path.GetDirectoryName(r.Location) + "\"";
                Process.Start(new ProcessStartInfo("explorer.exe", arg) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Log.Debug("Could not open location {0}: {1}", r.Location, ex.Message);
            }
        }

        private void EditScenario_Click(object sender, RoutedEventArgs e)
        {
            if (ResultList.SelectedItem is not BrokenScenario r) return;
            new ScenarioEditWindow(r.RouteId, r.Scenario) { Owner = this }.Show();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _cts?.Cancel();
            base.OnClosing(e);
        }
    }
}
