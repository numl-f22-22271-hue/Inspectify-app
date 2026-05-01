#if !WINDOWS
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PC_inspect_beta.Core;
using PC_inspect_beta.Core.Models;
using PC_inspect_beta.Core.Platform;

namespace PC_inspect_beta.UI.Avalonia
{
    public class MainWindow : Window
    {
        private TextBox _output = null!;
        private TextBlock _status = null!;
        private ProgressBar _progress = null!;
        private Button _scanBtn = null!;
        private Button _signInBtn = null!;
        private Button _postAdBtn = null!;
        private Button _exportPdfBtn = null!;
        private Button _kbTestBtn = null!;
        private Button _tpTestBtn = null!;
        private Button _signOutBtn = null!;
        private TextBlock _userLabel = null!;

        private string? _username;
        private ScanResult? _lastScan;

        public MainWindow()
        {
            Title  = "Inspectify Scanner";
            Width  = 1100;
            Height = 760;
            MinWidth  = 720;
            MinHeight = 520;
            Background = new SolidColorBrush(Color.Parse("#16212e"));
            BuildUi();
            UpdateAuthUi();
        }

        private void BuildUi()
        {
            var root = new DockPanel();

            // ── Header ───────────────────────────────────────────────
            var header = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#1e2a3a")),
                Padding    = new Thickness(20, 14),
                BorderBrush = new SolidColorBrush(Color.Parse("#243447")),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            DockPanel.SetDock(header, Dock.Top);

            var headerGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*, Auto") };

            var titleStack = new StackPanel();
            titleStack.Children.Add(new TextBlock
            {
                Text = "Inspectify Scanner",
                FontSize = 22,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White
            });
            titleStack.Children.Add(new TextBlock
            {
                Text = $"Platform: {PlatformDetector.PlatformName}  •  inspectifylapstore.me",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.Parse("#94a3b8")),
                Margin = new Thickness(0, 4, 0, 0)
            });
            Grid.SetColumn(titleStack, 0);
            headerGrid.Children.Add(titleStack);

            // User indicator + sign in/out
            var userPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
            _userLabel = new TextBlock
            {
                Text = "Not signed in",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.Parse("#94a3b8")),
                VerticalAlignment = VerticalAlignment.Center
            };
            userPanel.Children.Add(_userLabel);

            _signInBtn = MakeBtn("Sign In", "#2b72d4", small: true);
            _signInBtn.Click += async (_, _) => await DoSignInAsync();
            userPanel.Children.Add(_signInBtn);

            _signOutBtn = MakeBtn("Sign Out", "#243447", small: true);
            _signOutBtn.Click += (_, _) => SignOut();
            _signOutBtn.IsVisible = false;
            userPanel.Children.Add(_signOutBtn);

            Grid.SetColumn(userPanel, 1);
            headerGrid.Children.Add(userPanel);

            header.Child = headerGrid;
            root.Children.Add(header);

            // ── Action bar ───────────────────────────────────────────
            var actionBar = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#1e2a3a")),
                Padding    = new Thickness(20, 12)
            };
            DockPanel.SetDock(actionBar, Dock.Top);

            var actionStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

            _scanBtn = MakeBtn("Run Hardware Scan", "#2b72d4");
            _scanBtn.Click += async (_, _) => await RunScanAsync();
            actionStack.Children.Add(_scanBtn);

            _postAdBtn = MakeBtn("Post Ad", "#10b981");
            _postAdBtn.Click += async (_, _) => await PostAdAsync();
            actionStack.Children.Add(_postAdBtn);

            _exportPdfBtn = MakeBtn("Export PDF", "#f59e0b");
            _exportPdfBtn.Click += (_, _) => ExportPdf();
            actionStack.Children.Add(_exportPdfBtn);

            _kbTestBtn = MakeBtn("Keyboard Test", "#243447");
            _kbTestBtn.Click += async (_, _) =>
            {
                var w = new KeyboardTestWindow();
                await w.ShowDialog(this);
            };
            actionStack.Children.Add(_kbTestBtn);

            _tpTestBtn = MakeBtn("Touchpad Test", "#243447");
            _tpTestBtn.Click += async (_, _) =>
            {
                var w = new TouchpadTestWindow();
                await w.ShowDialog(this);
            };
            actionStack.Children.Add(_tpTestBtn);

            _status = new TextBlock
            {
                Text = "Ready.",
                Foreground = new SolidColorBrush(Color.Parse("#94a3b8")),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0),
                FontSize = 13
            };
            actionStack.Children.Add(_status);
            actionBar.Child = actionStack;
            root.Children.Add(actionBar);

            // ── Progress ─────────────────────────────────────────────
            _progress = new ProgressBar
            {
                Height = 3,
                IsVisible = false,
                IsIndeterminate = true,
                Foreground = new SolidColorBrush(Color.Parse("#5ea0ff"))
            };
            DockPanel.SetDock(_progress, Dock.Top);
            root.Children.Add(_progress);

            // ── Report area ──────────────────────────────────────────
            _output = new TextBox
            {
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.NoWrap,
                FontFamily = new FontFamily("JetBrains Mono, Menlo, Consolas, monospace"),
                FontSize = 12,
                Background = new SolidColorBrush(Color.Parse("#0d1620")),
                Foreground = new SolidColorBrush(Color.Parse("#e2e8f0")),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(20),
                Text =
                    "Welcome to Inspectify Scanner.\n\n" +
                    "1. Sign in (top right) with your Inspectify LapStore account\n" +
                    "2. Click 'Run Hardware Scan' to inspect this Mac\n" +
                    "3. Click 'Post Ad' to publish a marketplace listing\n\n" +
                    "Optional:\n" +
                    "  • 'Export PDF' — save the scan as a PDF report\n" +
                    "  • 'Keyboard Test' — verify all keys work\n" +
                    "  • 'Touchpad Test' — verify touchpad coverage\n"
            };
            root.Children.Add(_output);

            Content = root;
        }

        private void UpdateAuthUi()
        {
            bool signedIn = !string.IsNullOrEmpty(_username);
            _userLabel.Text = signedIn ? $"Signed in as @{_username}" : "Not signed in";
            _userLabel.Foreground = signedIn
                ? new SolidColorBrush(Color.Parse("#5ea0ff"))
                : new SolidColorBrush(Color.Parse("#94a3b8"));

            _signInBtn.IsVisible = !signedIn;
            _signOutBtn.IsVisible = signedIn;
            _postAdBtn.IsEnabled = signedIn && _lastScan != null;
            _exportPdfBtn.IsEnabled = _lastScan != null;
        }

        private async Task DoSignInAsync()
        {
            var w = new LoginWindow();
            var result = await w.ShowDialog<bool>(this);
            if (result && !string.IsNullOrEmpty(w.AuthenticatedUsername))
            {
                _username = w.AuthenticatedUsername;
                _status.Text = $"Welcome, @{_username}!";
                UpdateAuthUi();
            }
        }

        private void SignOut()
        {
            _username = null;
            _status.Text = "Signed out.";
            UpdateAuthUi();
        }

        private async Task PostAdAsync()
        {
            if (string.IsNullOrEmpty(_username))
            {
                _status.Text = "Please sign in first.";
                return;
            }
            if (_lastScan == null)
            {
                _status.Text = "Please run a hardware scan first.";
                return;
            }

            var w = new AdPostWindow(_username, _lastScan);
            var posted = await w.ShowDialog<bool>(this);
            if (posted) _status.Text = "Listing published successfully.";
        }

        private void ExportPdf()
        {
            if (_lastScan == null) { _status.Text = "Run a scan first."; return; }
            try
            {
                var path = ReportExporter.SaveToPdf(_lastScan.ReportText);
                _status.Text = $"PDF saved: {path}";
            }
            catch (Exception ex)
            {
                _status.Text = $"Export failed: {ex.Message}";
            }
        }

        private async Task RunScanAsync()
        {
            _scanBtn.IsEnabled = false;
            _progress.IsVisible = true;
            _status.Text = "Scanning hardware…";
            _output.Text = "";

            var sb = new StringBuilder();
            sb.AppendLine("╔══════════════════════════════════════════╗");
            sb.AppendLine("║      INSPECTIFY — HARDWARE REPORT        ║");
            sb.AppendLine("╚══════════════════════════════════════════╝");
            sb.AppendLine($"Generated : {DateTime.Now:dddd, dd MMMM yyyy  HH:mm:ss}");
            sb.AppendLine($"Platform  : {PlatformDetector.PlatformName}");
            sb.AppendLine();

            var result = new ScanResult();

            try
            {
                var scanner = await Task.Run(() => PlatformDetector.CreateScanner());

                await UpdateAsync("Scanning OS…", sb);
                var os = await Task.Run(scanner.GetOsInfo);
                AppendSection(sb, "OS & POWER");
                sb.AppendLine($"  Name        : {os.Name}");
                sb.AppendLine($"  Version     : {os.Version}");
                sb.AppendLine($"  Architecture: {os.Architecture}");
                sb.AppendLine($"  Uptime      : {os.Uptime}");
                sb.AppendLine();
                result.Metadata["os_version"] = os.Version;
                result.Metadata["architecture"] = os.Architecture;

                await UpdateAsync("Scanning CPU…", sb);
                var cpu = await Task.Run(scanner.GetCpuInfo);
                AppendSection(sb, "CPU");
                sb.AppendLine($"  Model       : {cpu.Name}");
                sb.AppendLine($"  Cores       : {cpu.Cores}");
                sb.AppendLine($"  Threads     : {cpu.Threads}");
                sb.AppendLine($"  Max Clock   : {cpu.MaxClockMhz} MHz");
                sb.AppendLine();
                result.Metadata["cpu_name"] = cpu.Name;
                result.Metadata["cpu_cores"] = cpu.Cores;
                result.Metadata["cpu_threads"] = cpu.Threads;

                await UpdateAsync("Scanning RAM…", sb);
                var ram = await Task.Run(scanner.GetRamInfo);
                AppendSection(sb, "RAM");
                sb.AppendLine($"  Total       : {ram.TotalMb / 1024.0:F1} GB ({ram.TotalMb} MB)");
                sb.AppendLine($"  Available   : {ram.AvailableMb / 1024.0:F1} GB");
                foreach (var s in ram.Sticks)
                    sb.AppendLine($"  Stick       : {s.CapacityMb} MB {s.Type} {s.Manufacturer}");
                sb.AppendLine();
                result.Metadata["ram_total_gb"] = ram.TotalMb / 1024;

                await UpdateAsync("Scanning storage…", sb);
                var storage = await Task.Run(scanner.GetStorageInfo);
                AppendSection(sb, "STORAGE");
                long totalStorage = 0;
                foreach (var d in storage)
                {
                    sb.AppendLine($"  {d.Mount,-10} {d.CapacityGb} GB ({d.FreeGb} GB free) — {d.Type} — {d.Model}");
                    totalStorage += d.CapacityGb;
                }
                sb.AppendLine();
                result.Metadata["storage_total_gb"] = totalStorage;
                result.Metadata["storage_type"] = storage.FirstOrDefault()?.Type ?? "";

                await UpdateAsync("Scanning GPU…", sb);
                var gpus = await Task.Run(scanner.GetGpuInfo);
                AppendSection(sb, "GPU");
                foreach (var g in gpus)
                    sb.AppendLine($"  {g.Name}  {(g.VramMb > 0 ? $"({g.VramMb} MB VRAM)" : "")}");
                sb.AppendLine();
                result.Metadata["gpu_name"] = gpus.FirstOrDefault()?.Name ?? "";

                await UpdateAsync("Scanning battery…", sb);
                var bat = await Task.Run(scanner.GetBatteryInfo);
                AppendSection(sb, "BATTERY");
                if (bat == null)
                    sb.AppendLine("  No battery (Desktop)");
                else
                {
                    sb.AppendLine($"  Charge      : {bat.PercentRemaining}% {(bat.IsCharging ? "(charging ⚡)" : "(on battery)")}");
                    if (bat.DesignCapacityMwh > 0)
                        sb.AppendLine($"  Health      : {bat.HealthPercent}% ({bat.FullChargeCapacityMwh} / {bat.DesignCapacityMwh} mWh)");
                    result.Metadata["battery_percent"] = bat.PercentRemaining;
                    result.Metadata["battery_health"] = bat.HealthPercent;
                }
                sb.AppendLine();

                await UpdateAsync("Scanning displays…", sb);
                var displays = await Task.Run(scanner.GetDisplayInfo);
                AppendSection(sb, "DISPLAY");
                foreach (var d in displays)
                    sb.AppendLine($"  {d.Width} × {d.Height}  {(d.RefreshHz > 0 ? $"@ {d.RefreshHz} Hz" : "")}");
                sb.AppendLine();

                await UpdateAsync("Scanning BIOS…", sb);
                var bios = await Task.Run(scanner.GetBiosInfo);
                AppendSection(sb, "BIOS / MOTHERBOARD");
                sb.AppendLine($"  Manufacturer: {bios.Manufacturer}");
                sb.AppendLine($"  Version     : {bios.Version}");
                sb.AppendLine($"  Model       : {bios.MotherboardModel}");
                sb.AppendLine();

                await UpdateAsync("Scanning network…", sb);
                var nets = await Task.Run(scanner.GetNetworkInfo);
                AppendSection(sb, "NETWORK");
                foreach (var n in nets)
                    sb.AppendLine($"  {n.Name,-12} {n.Type,-10} {n.MacAddress}  {n.IpAddress}");

                result.ReportText = sb.ToString();
                _lastScan = result;
                _status.Text = "Scan complete ✓";
            }
            catch (Exception ex)
            {
                sb.AppendLine();
                sb.AppendLine($"[ERROR] {ex.Message}");
                _status.Text = "Scan failed.";
            }
            finally
            {
                _output.Text = sb.ToString();
                _scanBtn.IsEnabled = true;
                _progress.IsVisible = false;
                UpdateAuthUi();
            }
        }

        private static void AppendSection(StringBuilder sb, string name)
        {
            sb.AppendLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine($"  {name}");
            sb.AppendLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        }

        private async Task UpdateAsync(string status, StringBuilder sb)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _status.Text = status;
                _output.Text = sb.ToString();
            });
        }

        private static Button MakeBtn(string text, string color, bool small = false)
        {
            return new Button
            {
                Content = $"  {text}  ",
                Background = new SolidColorBrush(Color.Parse(color)),
                Foreground = Brushes.White,
                FontWeight = FontWeight.SemiBold,
                FontSize = small ? 12 : 13,
                Padding = small ? new Thickness(14, 6) : new Thickness(16, 9),
                CornerRadius = new CornerRadius(8)
            };
        }
    }
}
#endif
