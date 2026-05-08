using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PC_inspect_beta.Config;
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
        private Button _stopBtn = null!;
        private Button _postAdBtn = null!;
        private Button _exportPdfBtn = null!;
        private Button _kbTestBtn = null!;
        private Button _tpTestBtn = null!;

        private ScanResult? _lastScan;
        private string _networkSectionText = "";

        public MainWindow()
        {
            Title  = AppConfig.AppName;
            Width  = 1100;
            Height = 760;
            MinWidth  = 720;
            MinHeight = 520;
            Background = new SolidColorBrush(Color.Parse("#16212e"));
            LoadAppIcon();
            BuildUi();
        }

        private void LoadAppIcon()
        {
            try
            {
                var stream = typeof(MainWindow).Assembly.GetManifestResourceStream("AppIcon");
                if (stream != null)
                    Icon = new WindowIcon(stream);
            }
            catch { }
        }

        private void BuildUi()
        {
            var root = new DockPanel();

            // Header
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
                Text = AppConfig.AppName,
                FontSize = 22,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White
            });
            titleStack.Children.Add(new TextBlock
            {
                Text = "Diagnose · Verify · Sell",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.Parse("#94a3b8")),
                Margin = new Thickness(0, 4, 0, 0)
            });
            Grid.SetColumn(titleStack, 0);
            headerGrid.Children.Add(titleStack);

            // Version pill
            var versionLabel = new TextBlock
            {
                Text = $"v{AppConfig.Version}",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.Parse("#94a3b8")),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(versionLabel, 1);
            headerGrid.Children.Add(versionLabel);

            header.Child = headerGrid;
            root.Children.Add(header);

            // Action bar
            var actionBar = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#1e2a3a")),
                Padding    = new Thickness(20, 12)
            };
            DockPanel.SetDock(actionBar, Dock.Top);

            var actionStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

            _scanBtn = MakeBtn("Start System Scan", "#2b72d4");
            _scanBtn.Click += async (_, _) => await RunScanAsync();
            actionStack.Children.Add(_scanBtn);

            _stopBtn = MakeBtn("Stop / Clear Memory", "#ef4444");
            _stopBtn.Click += (_, _) =>
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                _progress.IsVisible = false;
                SetStatus("Memory released.", "#94a3b8");
            };
            actionStack.Children.Add(_stopBtn);

            _exportPdfBtn = MakeBtn("Save PDF Report", "#f59e0b");
            _exportPdfBtn.IsEnabled = false;
            _exportPdfBtn.Click += (_, _) => ExportPdf();
            actionStack.Children.Add(_exportPdfBtn);

            _postAdBtn = MakeBtn("Upload / Sell Device", "#10b981");
            _postAdBtn.IsEnabled = false;
            _postAdBtn.Click += async (_, _) => await SellDeviceAsync();
            actionStack.Children.Add(_postAdBtn);

            _kbTestBtn = MakeBtn("Keyboard Test", "#243447");
            _kbTestBtn.Click += async (_, _) => await RunKeyboardTestAsync();
            actionStack.Children.Add(_kbTestBtn);

            _tpTestBtn = MakeBtn("Touchpad Test", "#243447");
            _tpTestBtn.Click += async (_, _) => await RunTouchpadTestAsync();
            actionStack.Children.Add(_tpTestBtn);

            actionBar.Child = actionStack;
            root.Children.Add(actionBar);

            // Status bar
            var statusBar = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#0c1e1a")),
                Padding = new Thickness(20, 10),
                BorderBrush = new SolidColorBrush(Color.Parse("#163c30")),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            DockPanel.SetDock(statusBar, Dock.Top);

            _status = new TextBlock
            {
                Text = "Ready to scan.",
                Foreground = new SolidColorBrush(Color.Parse("#10b981")),
                FontSize = 13,
                FontWeight = FontWeight.SemiBold
            };
            statusBar.Child = _status;
            root.Children.Add(statusBar);

            // Progress
            _progress = new ProgressBar
            {
                Height = 3,
                IsVisible = false,
                IsIndeterminate = true,
                Foreground = new SolidColorBrush(Color.Parse("#5ea0ff"))
            };
            DockPanel.SetDock(_progress, Dock.Top);
            root.Children.Add(_progress);

            // Report area
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
                    $"Welcome to {AppConfig.AppName}.\n\n" +
                    "1. Click 'Start System Scan' to inspect this device\n" +
                    "2. Click 'Save PDF Report' to export the scan\n" +
                    "3. Click 'Upload / Sell Device' to list on the marketplace\n\n" +
                    "Optional:\n" +
                    "  - Keyboard Test — verify all keys work\n" +
                    "  - Touchpad Test — verify touchpad coverage\n"
            };
            root.Children.Add(_output);

            Content = root;
        }

        private async Task SellDeviceAsync()
        {
            if (_lastScan == null)
            {
                SetStatus("Please run a scan first.", "#f59e0b");
                return;
            }

            var login = new LoginWindow();
            var loggedIn = await login.ShowDialog<bool>(this);
            if (!loggedIn || string.IsNullOrEmpty(login.AuthenticatedUsername))
                return;

            var ad = new AdPostWindow(login.AuthenticatedUsername, _lastScan);
            var posted = await ad.ShowDialog<bool>(this);
            if (posted) SetStatus("Listing published successfully!", "#10b981");
        }

        private void ExportPdf()
        {
            if (_lastScan == null) { SetStatus("Run a scan first.", "#f59e0b"); return; }
            try
            {
                var path = ReportExporter.SaveToPdf(_lastScan.ReportText);
                SetStatus($"PDF saved: {System.IO.Path.GetFileName(path)}", "#10b981");
            }
            catch (Exception ex)
            {
                SetStatus($"Export failed: {ex.Message}", "#ef4444");
            }
        }


        private async Task RunScanAsync()
        {
            _scanBtn.IsEnabled = false;
            _exportPdfBtn.IsEnabled = false;
            _postAdBtn.IsEnabled = false;
            _progress.IsVisible = true;
            SetStatus("Starting scan…", "#f59e0b");
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

                await UpdateAsync("Detecting CPU…", sb);
                var cpu = await Task.Run(scanner.GetCpuInfo);
                AppendSection(sb, "CPU");
                sb.AppendLine($"  Model       : {cpu.Name}");
                sb.AppendLine($"  Cores       : {cpu.Cores}");
                sb.AppendLine($"  Threads     : {cpu.Threads}");
                if (!PlatformDetector.IsMacOS)
                    sb.AppendLine($"  Max Clock   : {cpu.MaxClockMhz} MHz");
                sb.AppendLine();
                result.Metadata["cpu_name"] = cpu.Name;
                result.Metadata["cpu_cores"] = cpu.Cores;
                result.Metadata["cpu_threads"] = cpu.Threads;

                await UpdateAsync("Reading RAM…", sb);
                var ram = await Task.Run(scanner.GetRamInfo);
                AppendSection(sb, "RAM");
                long ramPow2 = NearestPow2(ram.TotalMb / 1024);
                sb.AppendLine($"  Total       : {ram.TotalMb / 1024.0:F1} GB ({ramPow2} GB)");
                sb.AppendLine($"  Available   : {ram.AvailableMb / 1024.0:F1} GB");
                foreach (var s in ram.Sticks)
                {
                    double stickGb = s.CapacityMb / 1024.0;
                    sb.AppendLine($"  RAM Type    : {stickGb:F0} GB {s.Type} {s.Manufacturer}");
                }
                sb.AppendLine();
                result.Metadata["ram_total_gb"] = ram.TotalMb / 1024;

                await UpdateAsync("Scanning storage…", sb);
                var storage = await Task.Run(scanner.GetStorageInfo);
                AppendSection(sb, "STORAGE");
                long totalStorage = 0;
                foreach (var d in storage)
                {
                    long storagePow2 = NearestPow2(d.CapacityGb);
                    string pow2Label = storagePow2 >= 1024 ? $"{storagePow2 / 1024} TB" : $"{storagePow2} GB";
                    sb.AppendLine($"  {d.Mount,-10} {d.CapacityGb} GB ({d.FreeGb} GB free) ({pow2Label}) — {d.Type} — {d.Model}");
                    totalStorage += d.CapacityGb;
                }
                sb.AppendLine();
                result.Metadata["storage_total_gb"] = totalStorage;
                result.Metadata["storage_type"] = storage.FirstOrDefault()?.Type ?? "";

                await UpdateAsync("Checking GPU…", sb);
                var gpus = await Task.Run(scanner.GetGpuInfo);
                AppendSection(sb, "GPU");
                foreach (var g in gpus)
                    sb.AppendLine($"  {g.Name}  {(g.VramMb > 0 ? $"({g.VramMb} MB VRAM)" : "")}");
                sb.AppendLine();
                result.Metadata["gpu_name"] = gpus.FirstOrDefault()?.Name ?? "";

                await UpdateAsync("Reading battery info…", sb);
                var bat = await Task.Run(scanner.GetBatteryInfo);
                AppendSection(sb, "BATTERY");
                if (bat == null)
                    sb.AppendLine("  No battery (Desktop)");
                else
                {
                    sb.AppendLine($"  Charge      : {bat.PercentRemaining}% {(bat.IsCharging ? "(charging)" : "(on battery)")}");
                    if (bat.DesignCapacityMwh > 0 && bat.HealthPercent > 0 && bat.HealthPercent <= 110)
                        sb.AppendLine($"  Health      : {bat.HealthPercent}%");
                    result.Metadata["battery_percent"] = bat.PercentRemaining;
                    result.Metadata["battery_health"] = bat.HealthPercent;
                }
                sb.AppendLine();

                await UpdateAsync("Scanning displays…", sb);
                var displays = await Task.Run(scanner.GetDisplayInfo);
                AppendSection(sb, "DISPLAY");
                foreach (var d in displays)
                {
                    var typeStr = !string.IsNullOrEmpty(d.DisplayType) ? $"  [{d.DisplayType}]" : "";
                    sb.AppendLine($"  {d.Width} x {d.Height}  {(d.RefreshHz > 0 ? $"@ {d.RefreshHz} Hz" : "")}{typeStr}");
                }
                sb.AppendLine();

                await UpdateAsync("Reading BIOS info…", sb);
                var bios = await Task.Run(scanner.GetBiosInfo);
                AppendSection(sb, "BIOS / MOTHERBOARD");
                sb.AppendLine($"  Manufacturer: {bios.Manufacturer}");
                sb.AppendLine($"  Version     : {bios.Version}");
                sb.AppendLine($"  Model       : {bios.MotherboardModel}");
                sb.AppendLine();
                result.Metadata["motherboard"] = bios.MotherboardModel;

                await UpdateAsync("Scanning network cards…", sb);
                var nets = await Task.Run(scanner.GetNetworkInfo);
                var netSb = new StringBuilder();
                AppendSection(netSb, "NETWORK");
                foreach (var n in nets)
                    netSb.AppendLine($"  {n.Name,-12} {n.Type,-10} {n.MacAddress}  {n.IpAddress}");
                netSb.AppendLine();
                _networkSectionText = netSb.ToString();

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

                await UpdateAsync("RAM stress test…", sb);
                var ramStress = await Task.Run(StressTestEngine.RunRamStress);
                AppendSection(sb, "STRESS TEST — RAM");
                sb.AppendLine($"  {ramStress.Summary}");
                sb.AppendLine($"  Result      : {(ramStress.Passed ? "PASSED" : "FAILED")}");
                if (ramStress.Metrics.TryGetValue("write_speed_mbs", out var wsm))
                    result.Metadata["ram_write_speed_mbs"] = wsm;
                result.Metadata["stress_ram"] = ramStress.Passed ? "Passed" : "Failed";
                sb.AppendLine();

                await UpdateAsync("CPU stress test…", sb);
                var cpuStress = await Task.Run(StressTestEngine.RunCpuStress);
                AppendSection(sb, "STRESS TEST — CPU");
                sb.AppendLine($"  {cpuStress.Summary}");
                sb.AppendLine($"  Result      : {(cpuStress.Passed ? "PASSED" : "FAILED")}");
                result.Metadata["stress_cpu"] = cpuStress.Passed ? "Passed" : "Failed";
                sb.AppendLine();

                await UpdateAsync("GPU/compute stress test…", sb);
                var gpuStress = await Task.Run(StressTestEngine.RunGpuStress);
                AppendSection(sb, "STRESS TEST — GPU/COMPUTE");
                sb.AppendLine($"  {gpuStress.Summary}");
                sb.AppendLine($"  Result      : {(gpuStress.Passed ? "PASSED" : "FAILED")}");
                result.Metadata["stress_gpu"] = gpuStress.Passed ? "Passed" : "Failed";
                sb.AppendLine();

                await UpdateAsync("Storage R/W speed test…", sb);
                var storageStress = await Task.Run(StressTestEngine.RunStorageStress);
                AppendSection(sb, "STRESS TEST — STORAGE");
                foreach (var s in storageStress)
                {
                    sb.AppendLine($"  {s.Name}");
                    sb.AppendLine($"    {s.Summary}");
                    sb.AppendLine($"    Result    : {(s.Passed ? "PASSED" : "FAILED")}");
                }
                result.Metadata["stress_storage"] = storageStress.All(s => s.Passed) ? "Passed" : "Failed";

                result.ReportText = sb.ToString();
                _lastScan = result;

#if HAS_ML
                _ = Task.Run(() => PricePredictor.EnsureTrained());
#endif

                SetStatus("Scan complete ✔", "#10b981");
                _exportPdfBtn.IsEnabled = true;
                _postAdBtn.IsEnabled = true;
                _output.Text = sb.ToString();
                _scanBtn.IsEnabled = true;
                _progress.IsVisible = false;

                bool isLaptop = bat != null;
                await PromptTestsAsync(isLaptop);
                return;
            }
            catch (Exception ex)
            {
                sb.AppendLine();
                sb.AppendLine($"[ERROR] {ex.Message}");
                SetStatus("Scan failed.", "#ef4444");
            }
            finally
            {
                _output.Text = sb.ToString();
                _scanBtn.IsEnabled = true;
                _progress.IsVisible = false;
            }
        }

        private async Task PromptTestsAsync(bool isLaptop)
        {
            await RunKeyboardTestAsync();

            if (isLaptop)
            {
                await RunTouchpadTestAsync();
                await CheckFingerprintSensorAsync();
            }

            if (_lastScan != null && !string.IsNullOrEmpty(_networkSectionText))
            {
                _lastScan.ReportText += _networkSectionText;
                _networkSectionText = "";
                _output.Text = _lastScan.ReportText;
            }

            if (_lastScan != null)
            {
                var reportWin = new Window
                {
                    Title = "System Report",
                    Width = 900,
                    Height = 600,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Background = new SolidColorBrush(Color.Parse("#16212e"))
                };
                var reportBox = new TextBox
                {
                    IsReadOnly = true,
                    AcceptsReturn = true,
                    TextWrapping = TextWrapping.NoWrap,
                    Text = _lastScan.ReportText,
                    Background = new SolidColorBrush(Color.Parse("#0d1620")),
                    Foreground = new SolidColorBrush(Color.Parse("#e2e8f0")),
                    FontFamily = new FontFamily("Cascadia Mono, Consolas, monospace"),
                    FontSize = 13,
                    Padding = new Thickness(16)
                };
                reportWin.Content = reportBox;
                await reportWin.ShowDialog(this);
            }
        }

        private async Task CheckFingerprintSensorAsync()
        {
            var (detected, sensorName) = await Task.Run(DetectFingerprintSensor);
            if (!detected || _lastScan == null) return;

            SetStatus($"Testing {sensorName} — place your finger on the sensor…", "#5ea0ff");

            bool passed;
            if (PlatformDetector.IsMacOS)
            {
                passed = await Task.Run(MacOSHardwareScanner.TestTouchId);
            }
            else
            {
                passed = await ShowConfirmAsync("Fingerprint Sensor",
                    $"{sensorName} detected.\nPlace your finger on the sensor and try to unlock.\nDid it work?");
            }

            var sb = new StringBuilder(_lastScan.ReportText);
            AppendSection(sb, "FINGERPRINT SENSOR");
            sb.AppendLine($"  Sensor      : {sensorName}");
            sb.AppendLine(passed
                ? "  Result      : ✔ PASSED — Fingerprint sensor verified"
                : "  Result      : ✖ FAILED — Fingerprint sensor not working");
            sb.AppendLine();
            _lastScan.ReportText = sb.ToString();
            _lastScan.Metadata["fingerprint_status"] = passed ? "Passed" : "Failed";
            _output.Text = _lastScan.ReportText;

            SetStatus(passed ? "Fingerprint: PASSED ✔" : "Fingerprint: FAILED ✖",
                passed ? "#10b981" : "#ef4444");
        }

        private static (bool Detected, string Name) DetectFingerprintSensor()
        {
            try
            {
                if (PlatformDetector.IsMacOS)
                {
                    if (MacOSHardwareScanner.HasTouchId())
                        return (true, "Touch ID");
                    return (false, "");
                }

                if (PlatformDetector.IsWindows)
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "wmic",
                        Arguments = "path Win32_PnPEntity where \"PNPClass='Biometric'\" get Name /format:list",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var p = Process.Start(psi);
                    if (p == null) return (false, "");
                    if (!p.WaitForExit(10000)) return (false, "");
                    var output = p.StandardOutput.ReadToEnd().Trim();
                    foreach (var line in output.Split('\n'))
                    {
                        if (line.StartsWith("Name=", StringComparison.OrdinalIgnoreCase))
                        {
                            var name = line[5..].Trim();
                            if (!string.IsNullOrEmpty(name))
                                return (true, name);
                        }
                    }
                }
            }
            catch { }
            return (false, "");
        }

        private async Task<bool> ShowConfirmAsync(string title, string message)
        {
            var result = false;
            var dlg = new Window
            {
                Title = title,
                Width = 380,
                Height = 160,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = new SolidColorBrush(Color.Parse("#16212e"))
            };
            var stack = new StackPanel { Margin = new Thickness(20), Spacing = 16 };
            stack.Children.Add(new TextBlock
            {
                Text = message,
                Foreground = Brushes.White,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap
            });
            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, HorizontalAlignment = HorizontalAlignment.Center };
            var yesBtn = new Button
            {
                Content = "Yes",
                Width = 80, Height = 34,
                Background = new SolidColorBrush(Color.Parse("#2b72d4")),
                Foreground = Brushes.White,
                CornerRadius = new CornerRadius(6),
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            yesBtn.Click += (_, _) => { result = true; dlg.Close(); };
            var noBtn = new Button
            {
                Content = "No",
                Width = 80, Height = 34,
                Background = new SolidColorBrush(Color.Parse("#46464b")),
                Foreground = Brushes.White,
                CornerRadius = new CornerRadius(6),
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            noBtn.Click += (_, _) => { result = false; dlg.Close(); };
            btnRow.Children.Add(yesBtn);
            btnRow.Children.Add(noBtn);
            stack.Children.Add(btnRow);
            dlg.Content = stack;
            await dlg.ShowDialog(this);
            return result;
        }

        private async Task RunKeyboardTestAsync()
        {
            var kt = new KeyboardTestWindow();
            await kt.ShowDialog(this);

            if (kt.Finished && _lastScan != null)
            {
                bool passed = kt.FailedKeys.Count == 0;
                var sb = new StringBuilder(_lastScan.ReportText);
                AppendSection(sb, "KEYBOARD TEST RESULT");
                if (passed)
                    sb.AppendLine("  Result      : ✔ PASSED — All keys detected");
                else
                {
                    sb.AppendLine($"  Result      : ✖ ISSUES — {kt.FailedKeys.Count} key(s) not detected");
                    sb.AppendLine($"  Failed Keys : {string.Join(", ", kt.FailedKeys)}");
                }
                _lastScan.ReportText = sb.ToString();
                _lastScan.Metadata["keyboard_status"] = passed ? "Passed" : $"{kt.FailedKeys.Count} failed";
                _output.Text = _lastScan.ReportText;

                SetStatus(passed ? "Keyboard: PASSED ✔" : $"Keyboard: {kt.FailedKeys.Count} key(s) failed",
                    passed ? "#10b981" : "#f59e0b");
            }

        }

        private async Task RunTouchpadTestAsync()
        {
            bool extMouse = _lastScan?.Metadata.ContainsKey("external_mouse_detected") == true
                         && _lastScan.Metadata["external_mouse_detected"] is true;

            var tt = new TouchpadTestWindow(extMouse);
            await tt.ShowDialog(this);

            if (tt.Finished && !tt.Skipped && _lastScan != null)
            {
                bool passed = tt.LeftButtonPressed && tt.RightButtonPressed;
                string result = passed
                    ? "Both buttons PASSED ✔"
                    : $"Partial — Left={tt.LeftButtonPressed}, Right={tt.RightButtonPressed}";

                var sb = new StringBuilder(_lastScan.ReportText);
                AppendSection(sb, "TOUCHPAD TEST RESULT");
                sb.AppendLine($"  Result      : {result}");
                if (extMouse)
                    sb.AppendLine("  Note        : External mouse was connected during test.");
                _lastScan.ReportText = sb.ToString();
                _lastScan.Metadata["touchpad_status"] = result;
                _output.Text = _lastScan.ReportText;

                SetStatus(passed ? "Touchpad: PASSED ✔" : "Touchpad: PARTIAL",
                    passed ? "#10b981" : "#f59e0b");
            }

        }

        private void SetStatus(string msg, string color)
        {
            _status.Text = msg;
            _status.Foreground = new SolidColorBrush(Color.Parse(color));
        }

        private static void AppendSection(StringBuilder sb, string name)
        {
            sb.AppendLine($"╔══════════════════════════════════════════════╗");
            sb.AppendLine($"║  {name,-43}║");
            sb.AppendLine($"╚══════════════════════════════════════════════╝");
        }

        private static long NearestPow2(long value)
        {
            if (value <= 0) return 0;
            long p = 1;
            while (p < value) p <<= 1;
            return p;
        }

        private async Task UpdateAsync(string status, StringBuilder sb)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _status.Text = status;
                _output.Text = sb.ToString();
            });
        }

        private static Button MakeBtn(string text, string color)
        {
            return new Button
            {
                Content = $"  {text}  ",
                Background = new SolidColorBrush(Color.Parse(color)),
                Foreground = Brushes.White,
                FontWeight = FontWeight.SemiBold,
                FontSize = 13,
                Padding = new Thickness(16, 9),
                CornerRadius = new CornerRadius(8)
            };
        }
    }
}
