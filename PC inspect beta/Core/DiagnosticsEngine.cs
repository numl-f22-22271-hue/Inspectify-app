#if WINDOWS
#nullable disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.VisualBasic.Devices;
using PC_inspect_beta.Core.Models;

namespace PC_inspect_beta.Core
{
    public static class DiagnosticsEngine
    {
        // ── Public entry point ────────────────────────────────────────────────

        public static async Task<ScanResult> RunFullScan(Action<string> statusCallback)
            => await Task.Run(() => RunScanInternal(statusCallback));

        // ── Internal orchestrator ─────────────────────────────────────────────

        private static ScanResult RunScanInternal(Action<string> status)
        {
            var sb = new StringBuilder();
            var result = new ScanResult();

            sb.AppendLine("╔══════════════════════════════════════════╗");
            sb.AppendLine("║        PC INSPECT – FULL SYSTEM REPORT   ║");
            sb.AppendLine("╚══════════════════════════════════════════╝");
            sb.AppendLine($"Generated : {DateTime.Now:dddd, dd MMMM yyyy  HH:mm:ss}");
            sb.AppendLine();

            status("Scanning OS & Power..."); ScanOsInfo(sb, result.Metadata);
            status("Scanning CPU..."); ScanCpu(sb, result.Metadata);
            status("Scanning RAM..."); ScanRam(sb, result.Metadata);
            status("Scanning Storage..."); ScanStorage(sb, result.Metadata);
            status("Scanning GPU..."); ScanGpu(sb, result.Metadata);
            status("Scanning Network..."); ScanNetwork(sb, result.Metadata);
            status("Scanning BIOS & Board..."); ScanBiosBoard(sb, result.Metadata);
            status("Scanning Pointing Devices..."); ScanPointingDevices(sb, result.Metadata);

            status("RAM stress test..."); StressTestRam(sb, result.Metadata);
            status("CPU stress test..."); StressTestCpu(sb, result.Metadata);
            status("GPU stress test..."); StressTestGpu(sb, result.Metadata);
            status("Storage R/W speed test..."); StressTestStorage(sb, result.Metadata);

            result.ReportText = sb.ToString();
            return result;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  HARDWARE SCANNERS
        // ══════════════════════════════════════════════════════════════════════

        // ── OS / Power / Battery ──────────────────────────────────────────────
        private static void ScanOsInfo(StringBuilder sb, Dictionary<string, object> meta)
        {
            sb.AppendLine(Section("OS & POWER"));
            try
            {
                string os = Environment.OSVersion.ToString();
                string arch = Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit";
                var up = TimeSpan.FromMilliseconds(Environment.TickCount64);
                var pw = SystemInformation.PowerStatus;

                sb.AppendLine($"  OS          : {os}");
                sb.AppendLine($"  Architecture: {arch}");
                sb.AppendLine($"  Uptime      : {(int)up.TotalDays}d {up.Hours:D2}:{up.Minutes:D2}:{up.Seconds:D2}");

                bool hasBattery = pw.BatteryChargeStatus != BatteryChargeStatus.NoSystemBattery
                               && pw.BatteryChargeStatus != BatteryChargeStatus.Unknown
                               && pw.BatteryLifePercent >= 0f
                               && pw.BatteryLifePercent <= 1f;

                if (hasBattery)
                {
                    int pct = (int)(pw.BatteryLifePercent * 100);
                    bool online = pw.PowerLineStatus == PowerLineStatus.Online;
                    string charger = online
                        ? "Connected ⚡"
                        : "Disconnected – Running on Battery 🔋";

                    string healthStr = "N/A";
                    try
                    {
                        uint fullCharge = 0, designCap = 0;

                        using (var bq = new ManagementObjectSearcher("root\\WMI",
                            "SELECT * FROM BatteryFullChargedCapacity"))
                            foreach (ManagementObject obj in bq.Get())
                                fullCharge = Convert.ToUInt32(obj["FullChargedCapacity"]);

                        using (var bd = new ManagementObjectSearcher("root\\WMI",
                            "SELECT * FROM BatteryStaticData"))
                            foreach (ManagementObject obj in bd.Get())
                                designCap = Convert.ToUInt32(obj["DesignedCapacity"]);

                        if (designCap > 0 && fullCharge > 0)
                        {
                            int health = (int)((fullCharge * 100.0) / designCap);
                            string grade = health >= 80 ? "Good ✔"
                                          : health >= 50 ? "Fair ⚠"
                                          : "Poor ✖ – Replacement recommended";
                            healthStr = $"{health}% – {grade}";
                            meta["battery_health"] = healthStr;
                        }
                    }
                    catch { healthStr = "N/A (WMI battery provider unavailable)"; }

                    sb.AppendLine($"  Battery     : {pct}%");
                    sb.AppendLine($"  Charger     : {charger}");
                    sb.AppendLine($"  Batt. Health: {healthStr}");

                    meta["battery_percent"] = pct;
                    meta["charger_status"] = charger;
                }
                else
                {
                    sb.AppendLine("  Battery     : No battery detected (Desktop PC)");
                }

                meta["os_version"] = os;
                meta["architecture"] = arch;
            }
            catch (Exception ex) { sb.AppendLine($"  [Error] {ex.Message}"); }
        }

        // ── CPU ───────────────────────────────────────────────────────────────
        private static void ScanCpu(StringBuilder sb, Dictionary<string, object> meta)
        {
            sb.AppendLine(Section("CPU"));
            try
            {
                using (var mos = new ManagementObjectSearcher("SELECT * FROM Win32_Processor"))
                {
                    foreach (ManagementObject obj in mos.Get())
                    {
                        string name = obj["Name"]?.ToString()?.Trim();
                        string cores = obj["NumberOfCores"]?.ToString();
                        string threads = obj["NumberOfLogicalProcessors"]?.ToString();
                        string speed = obj["MaxClockSpeed"]?.ToString();
                        string socket = obj["SocketDesignation"]?.ToString();

                        sb.AppendLine($"  CPU         : {name}");
                        sb.AppendLine($"  Cores       : {cores}  |  Threads: {threads}");
                        sb.AppendLine($"  Max Speed   : {speed} MHz  |  Socket: {socket}");

                        meta["cpu_name"] = name;
                        meta["cpu_cores"] = cores;
                        meta["cpu_threads"] = threads;
                    }
                }

                using (var cpuPc = new PerformanceCounter("Processor", "% Processor Time", "_Total"))
                {
                    cpuPc.NextValue();
                    Thread.Sleep(500);
                    float usage = cpuPc.NextValue();
                    sb.AppendLine($"  Live Usage  : {usage:F1}%");
                    meta["cpu_usage_percent"] = usage.ToString("F1");
                }

                try
                {
                    using var ts = new ManagementObjectSearcher(@"root\WMI",
                        "SELECT * FROM MSAcpi_ThermalZoneTemperature");
                    foreach (ManagementObject t in ts.Get())
                    {
                        double temp = (Convert.ToDouble(t["CurrentTemperature"]) - 2732) / 10.0;
                        sb.AppendLine($"  Temperature : {temp:F1} °C");
                        meta["cpu_temp_c"] = temp.ToString("F1");
                        break;
                    }
                }
                catch { sb.AppendLine("  Temperature : N/A (WMI thermal provider unavailable)"); }
            }
            catch (Exception ex) { sb.AppendLine($"  [Error] {ex.Message}"); }
        }

        // ── RAM ───────────────────────────────────────────────────────────────
        private static void ScanRam(StringBuilder sb, Dictionary<string, object> meta)
        {
            sb.AppendLine(Section("RAM"));
            try
            {
                var ci = new ComputerInfo();
                double tot = ci.TotalPhysicalMemory / 1024.0 / 1024.0 / 1024.0;
                double avl = ci.AvailablePhysicalMemory / 1024.0 / 1024.0 / 1024.0;
                int usg = 100 - (int)((ci.AvailablePhysicalMemory * 100.0) / ci.TotalPhysicalMemory);

                sb.AppendLine($"  Total       : {tot:F2} GB");
                sb.AppendLine($"  Available   : {avl:F2} GB");
                sb.AppendLine($"  Usage       : {usg}%");

                meta["ram_total_gb"] = tot.ToString("F2");
                meta["ram_usage_percent"] = usg;

                // ── Count total physical slots on the motherboard ─────────────
                int totalSlots = 0;
                using (var arr = new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMemoryArray"))
                    foreach (ManagementObject obj in arr.Get())
                        totalSlots += Convert.ToInt32(obj["MemoryDevices"] ?? 0);

                // ── List each populated slot ──────────────────────────────────
                int slot = 0;
                using (var mos = new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMemory"))
                {
                    foreach (ManagementObject obj in mos.Get())
                    {
                        slot++;
                        ulong sizeMB = Convert.ToUInt64(obj["Capacity"]) / 1024 / 1024;
                        ushort mtype = (ushort)(obj["MemoryType"] ?? 0);
                        uint spd = Convert.ToUInt32(obj["Speed"]);
                        string manuf = obj["Manufacturer"]?.ToString()?.Trim();
                        string partN = obj["PartNumber"]?.ToString()?.Trim();
                        string bankLabel = obj["BankLabel"]?.ToString()?.Trim();

                        sb.AppendLine(
                            $"  Slot {slot,-3}     : {sizeMB / 1024.0:F0} GB  " +
                            $"{MemTypeStr(mtype)} @ {spd} MHz  " +
                            $"[{manuf} {partN}]  Bank: {bankLabel}");
                    }
                }

                // ── Slot summary ──────────────────────────────────────────────
                int emptySlots = totalSlots - slot;
                sb.AppendLine($"  Slots       : {slot} used / {totalSlots} total  ({emptySlots} empty)");

                meta["ram_slots_used"] = slot;
                meta["ram_slots_total"] = totalSlots;
                meta["ram_slots_empty"] = emptySlots;
            }
            catch (Exception ex) { sb.AppendLine($"  [Error] {ex.Message}"); }
        }

        // ── Storage ───────────────────────────────────────────────────────────
        private static void ScanStorage(StringBuilder sb, Dictionary<string, object> meta)
        {
            sb.AppendLine(Section("STORAGE – Logical Drives"));
            try
            {
                foreach (DriveInfo d in DriveInfo.GetDrives().Where(x => x.IsReady))
                {
                    double tot = d.TotalSize / 1024.0 / 1024.0 / 1024.0;
                    double free = d.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0;
                    int use = (int)((1.0 - (free / tot)) * 100);
                    sb.AppendLine($"  Drive {d.Name}  Total: {tot:F1} GB  Free: {free:F1} GB  Used: {use}%  [{d.DriveType}]");
                    string key = $"disk_{d.Name.Replace(":\\", "").ToLower()}";
                    meta[$"{key}_total_gb"] = tot.ToString("F1");
                    meta[$"{key}_free_gb"] = free.ToString("F1");
                }

                sb.AppendLine();
                sb.AppendLine("  ── Physical Disks ──");
                using (var pd = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive"))
                {
                    foreach (ManagementObject obj in pd.Get())
                    {
                        string model = obj["Model"]?.ToString()?.Trim();
                        ulong sizeGB = Convert.ToUInt64(obj["Size"]) / 1024 / 1024 / 1024;
                        string iface = obj["InterfaceType"]?.ToString()?.Trim();
                        string serial = obj["SerialNumber"]?.ToString()?.Trim();
                        string dtype = DetectDriveType(model, iface);
                        sb.AppendLine($"  {model}  |  {sizeGB} GB  |  {iface}  |  Type: {dtype}  |  SN: {serial}");
                    }
                }

                sb.AppendLine();
                sb.AppendLine("  ── SMART Health ──");
                long ssdTotalGb = 0, hddTotalGb = 0;
                try
                {
                    using var hp = new ManagementObjectSearcher(@"root\Microsoft\Windows\Storage",
                        "SELECT * FROM MSFT_PhysicalDisk");
                    foreach (ManagementObject obj in hp.Get())
                    {
                        string fName = obj["FriendlyName"]?.ToString()?.Trim();
                        ushort healthStatus = Convert.ToUInt16(obj["HealthStatus"]);
                        ushort mediaType = Convert.ToUInt16(obj["MediaType"]);
                        ulong szGB = Convert.ToUInt64(obj["Size"]) / 1024 / 1024 / 1024;

                        string health = healthStatus switch
                        {
                            0 => "✔ Healthy",
                            1 => "⚠ Warning",
                            2 => "✖ Unhealthy",
                            _ => "Unknown"
                        };
                        string mtype = mediaType switch
                        {
                            3 => "HDD",
                            4 => "SSD",
                            5 => "SCM",
                            _ => DetectDriveType(fName, "")
                        };
                        sb.AppendLine($"  [{mtype}] {fName}  {szGB} GB  →  {health}");
                        meta[$"disk_health_{fName}"] = health;

                        if (mtype == "SSD" || mtype == "SCM") ssdTotalGb += (long)szGB;
                        else if (mtype == "HDD") hddTotalGb += (long)szGB;
                    }
                }
                catch { sb.AppendLine("  SMART: N/A (Storage Spaces WMI not available)"); }

                meta["ssd_total_gb"] = ssdTotalGb;
                meta["hdd_total_gb"] = hddTotalGb;
            }
            catch (Exception ex) { sb.AppendLine($"  [Error] {ex.Message}"); }
        }

        // ── Drive type detection ──────────────────────────────────────────────
        private static string DetectDriveType(string model, string interfaceType)
        {
            // Step 1: SpindleSpeed — HDD > 0, SSD = 0
            try
            {
                string safeModel = model?.Replace("'", "\\'") ?? "";
                using var searcher = new ManagementObjectSearcher(
                    $"SELECT SpindleSpeed FROM Win32_DiskDrive WHERE Model = '{safeModel}'");
                foreach (ManagementObject obj in searcher.Get())
                {
                    uint spindle = 0;
                    try { spindle = Convert.ToUInt32(obj["SpindleSpeed"]); } catch { }
                    if (spindle > 0) return "HDD";
                }
            }
            catch { }

            // Step 2: Model name keywords
            string lower = model?.ToLower() ?? "";

            if (lower.Contains("hdd") || lower.Contains("hard disk") ||
                lower.Contains("toshiba mq") || lower.Contains("toshiba dt") ||
                lower.Contains("toshiba al") || lower.Contains("wd blue") ||
                lower.Contains("wd green") || lower.Contains("wd red") ||
                lower.Contains("wd black") || lower.Contains("wd purple") ||
                lower.Contains("barracuda") || lower.Contains("seagate") ||
                lower.Contains("hitachi") || lower.Contains("hgst") ||
                lower.Contains("ironwolf") || lower.Contains("skyhawk") ||
                lower.Contains("exos"))
                return "HDD";

            if (lower.Contains("ssd") || lower.Contains("nvme") ||
                lower.Contains("kingston") || lower.Contains("samsung evo") ||
                lower.Contains("samsung pro") || lower.Contains("samsung qvo") ||
                lower.Contains("crucial") || lower.Contains("sandisk") ||
                lower.Contains("wd sn") || lower.Contains("sk hynix") ||
                lower.Contains("micron") || lower.Contains("intel ssd") ||
                lower.Contains("sabrent") || lower.Contains("pny cs"))
                return "SSD";

            // Step 3: Interface fallback
            string iface = interfaceType?.ToLower() ?? "";
            if (iface == "scsi" || iface == "nvme") return "SSD";

            return "Unknown";
        }

        // ── GPU ───────────────────────────────────────────────────────────────
        private static void ScanGpu(StringBuilder sb, Dictionary<string, object> meta)
        {
            sb.AppendLine(Section("GPU"));
            try
            {
                using var mos = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
                foreach (ManagementObject obj in mos.Get())
                {
                    string name = obj["Name"]?.ToString()?.Trim();
                    string driver = obj["DriverVersion"]?.ToString();
                    string res = $"{obj["CurrentHorizontalResolution"]}×{obj["CurrentVerticalResolution"]}";
                    uint vram = 0;
                    try { vram = Convert.ToUInt32(obj["AdapterRAM"]); } catch { }
                    double vramGB = vram / 1024.0 / 1024.0 / 1024.0;

                    sb.AppendLine($"  GPU         : {name}");
                    sb.AppendLine($"  VRAM        : {(vram > 0 ? $"{vramGB:F2} GB" : "N/A")}");
                    sb.AppendLine($"  Driver      : {driver}");
                    sb.AppendLine($"  Resolution  : {res}");

                    meta["gpu_name"] = name;
                    meta["gpu_vram_gb"] = vramGB.ToString("F2");
                }
            }
            catch (Exception ex) { sb.AppendLine($"  [Error] {ex.Message}"); }
        }

        // ── Network ───────────────────────────────────────────────────────────
        private static void ScanNetwork(StringBuilder sb, Dictionary<string, object> meta)
        {
            sb.AppendLine(Section("NETWORK"));
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                               (n.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                                n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)))
                {
                    string ip = "N/A";
                    foreach (UnicastIPAddressInformation ua in ni.GetIPProperties().UnicastAddresses)
                        if (ua.Address.AddressFamily == AddressFamily.InterNetwork)
                        { ip = ua.Address.ToString(); break; }

                    sb.AppendLine($"  [{ni.NetworkInterfaceType}] {ni.Name}");
                    sb.AppendLine($"    IP    : {ip}");
                    sb.AppendLine($"    MAC   : {ni.GetPhysicalAddress()}");
                    sb.AppendLine($"    Speed : {ni.Speed / 1_000_000} Mbps");
                }
            }
            catch (Exception ex) { sb.AppendLine($"  [Error] {ex.Message}"); }
        }

        // ── BIOS / Motherboard ────────────────────────────────────────────────
        private static void ScanBiosBoard(StringBuilder sb, Dictionary<string, object> meta)
        {
            sb.AppendLine(Section("BIOS & MOTHERBOARD"));
            try
            {
                using (var bios = new ManagementObjectSearcher("SELECT * FROM Win32_BIOS"))
                {
                    foreach (ManagementObject obj in bios.Get())
                    {
                        string ver = obj["SMBIOSBIOSVersion"]?.ToString()?.Trim();
                        string manuf = obj["Manufacturer"]?.ToString()?.Trim();
                        string date = obj["ReleaseDate"]?.ToString()?.Trim();
                        sb.AppendLine($"  BIOS        : {manuf}  v{ver}  (Released: {date?[..8]})");
                    }
                }

                using (var board = new ManagementObjectSearcher("SELECT * FROM Win32_BaseBoard"))
                {
                    foreach (ManagementObject obj in board.Get())
                    {
                        string m = obj["Manufacturer"]?.ToString()?.Trim();
                        string p = obj["Product"]?.ToString()?.Trim();
                        sb.AppendLine($"  Motherboard : {m}  {p}");
                        meta["motherboard"] = $"{m} {p}";
                    }
                }
            }
            catch (Exception ex) { sb.AppendLine($"  [Error] {ex.Message}"); }
        }

        // ── Laptop vs Desktop detection ───────────────────────────────────────
        public static bool IsLaptop()
        {
            try
            {
                var pw = SystemInformation.PowerStatus;
                return pw.BatteryChargeStatus != BatteryChargeStatus.NoSystemBattery
                    && pw.BatteryChargeStatus != BatteryChargeStatus.Unknown
                    && pw.BatteryLifePercent >= 0f
                    && pw.BatteryLifePercent <= 1f;
            }
            catch { return false; }
        }

        // ── Pointing Devices ──────────────────────────────────────────────────
        private static void ScanPointingDevices(StringBuilder sb, Dictionary<string, object> meta)
        {
            sb.AppendLine(Section("POINTING DEVICES"));
            try
            {
                using var mos = new ManagementObjectSearcher(
                    "SELECT Name, PNPDeviceID FROM Win32_PointingDevice");

                bool touchpadFound = false;
                var externalDevices = new List<string>();

                foreach (ManagementObject obj in mos.Get())
                {
                    string name = obj["Name"]?.ToString()?.Trim() ?? "";
                    string pnpId = obj["PNPDeviceID"]?.ToString()?.Trim() ?? "";
                    string nameLow = name.ToLower();
                    string pnpLow = pnpId.ToLower();

                    bool isTouchpad =
                        nameLow.Contains("touchpad") ||
                        nameLow.Contains("synaptics") ||
                        nameLow.Contains("elan") ||
                        nameLow.Contains("precision touchpad") ||
                        nameLow.Contains("alps") ||
                        nameLow.Contains("trackpad") ||
                        nameLow.Contains("clickpad") ||
                        pnpLow.Contains("hid\\vid_04f3") ||
                        pnpLow.Contains("hid\\vid_06cb") ||
                        pnpLow.Contains("hid\\vid_044e");

                    bool isExternalUsb = pnpLow.StartsWith("usb\\") && !isTouchpad;
                    bool isExternalBt = pnpLow.StartsWith("bthenum\\") && !isTouchpad;

                    if (isTouchpad)
                    {
                        touchpadFound = true;
                        meta["has_touchpad"] = true;
                        sb.AppendLine($"  Touchpad    : {name}  ✔");
                    }
                    else if (isExternalUsb || isExternalBt)
                    {
                        externalDevices.Add(name);
                    }
                }

                if (!touchpadFound)
                    sb.AppendLine("  Touchpad    : Not detected");

                if (externalDevices.Count > 0)
                {
                    sb.AppendLine($"  External Mouse: {externalDevices.Count} device(s) connected");
                    foreach (var d in externalDevices)
                        sb.AppendLine($"    -> {d}");
                    meta["external_mouse_detected"] = true;
                    meta["external_mouse_count"] = externalDevices.Count;
                }
                else
                {
                    sb.AppendLine("  External Mouse: None ✔");
                    meta["external_mouse_detected"] = false;
                }
            }
            catch (Exception ex) { sb.AppendLine($"  [Error] {ex.Message}"); }
        }

        // ── Append keyboard result ────────────────────────────────────────────
        public static void AppendKeyboardResult(ScanResult result, List<string> failedKeys)
        {
            var sb = new StringBuilder(result.ReportText);
            sb.AppendLine(Section("KEYBOARD TEST RESULT"));

            if (failedKeys == null || failedKeys.Count == 0)
                sb.AppendLine("  Result      : ✔ PASSED – All keys detected");
            else
            {
                sb.AppendLine($"  Result      : ✖ ISSUES – {failedKeys.Count} key(s) not detected");
                sb.AppendLine($"  Failed Keys : {string.Join(", ", failedKeys)}");
            }

            result.ReportText = sb.ToString();
            result.Metadata["keyboard_status"] = failedKeys?.Count == 0 ? "Passed" : "Issues";
            result.Metadata["keyboard_failed_keys"] = failedKeys == null ? "" : string.Join(", ", failedKeys);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  STRESS TESTS
        // ══════════════════════════════════════════════════════════════════════

        private static void StressTestRam(StringBuilder sb, Dictionary<string, object> meta)
        {
            sb.AppendLine(Section("STRESS TEST – RAM (60% Fill + Write Speed)"));
            try
            {
                var ci = new ComputerInfo();
                long target = (long)(ci.TotalPhysicalMemory * 0.60);
                var blocks = new List<byte[]>();
                long allocated = 0;
                int blockSz = 100 * 1024 * 1024;

                var sw = Stopwatch.StartNew();
                while (allocated < target &&
                       ci.AvailablePhysicalMemory > (ulong)(200 * 1024 * 1024))
                {
                    var blk = new byte[blockSz];
                    for (int i = 0; i < blk.Length; i += 4096) blk[i] = 0xAB;
                    blocks.Add(blk);
                    allocated += blockSz;
                }
                sw.Stop();

                double allocGB = allocated / 1024.0 / 1024.0 / 1024.0;
                double bandwidth = (allocated / 1024.0 / 1024.0) / sw.Elapsed.TotalSeconds;

                sb.AppendLine($"  Allocated   : {allocGB:F2} GB (~60% of total)");
                sb.AppendLine($"  Write Speed : {bandwidth:F0} MB/s");
                sb.AppendLine($"  Duration    : {sw.ElapsedMilliseconds} ms");
                sb.AppendLine($"  Result      : ✔ PASSED");

                meta["stress_ram"] = "Passed";
                meta["ram_write_speed_mbs"] = bandwidth.ToString("F0");

                blocks.Clear();
                GC.Collect();
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  Result      : ✖ FAILED – {ex.Message}");
                meta["stress_ram"] = "Failed";
            }
        }

        private static void StressTestCpu(StringBuilder sb, Dictionary<string, object> meta)
        {
            sb.AppendLine(Section("STRESS TEST – CPU (All-Core Parallel Burst)"));
            try
            {
                int threadCount = Environment.ProcessorCount;
                var tasks = new Task[threadCount];
                var sw = Stopwatch.StartNew();

                for (int t = 0; t < threadCount; t++)
                    tasks[t] = Task.Run(() =>
                    {
                        while (sw.ElapsedMilliseconds < 3000)
                            Math.Sqrt(new Random().NextDouble());
                    });

                Task.WaitAll(tasks);
                sw.Stop();

                sb.AppendLine($"  Threads     : {threadCount} logical cores loaded");
                sb.AppendLine($"  Duration    : {sw.ElapsedMilliseconds} ms");
                sb.AppendLine($"  Result      : ✔ PASSED");

                meta["stress_cpu"] = "Passed";
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  Result      : ✖ FAILED – {ex.Message}");
                meta["stress_cpu"] = "Failed";
            }
        }

        private static void StressTestGpu(StringBuilder sb, Dictionary<string, object> meta)
        {
            sb.AppendLine(Section("STRESS TEST – GPU (GDI+ Off-Screen Render Load)"));
            try
            {
                const int W = 1920, H = 1080, FRAMES = 30;
                var sw = Stopwatch.StartNew();
                var rng = new Random(42);

                using var bmp = new Bitmap(W, H);
                using var g = Graphics.FromImage(bmp);

                for (int f = 0; f < FRAMES; f++)
                {
                    g.Clear(Color.Black);
                    for (int i = 0; i < 2000; i++)
                    {
                        using var pen = new Pen(Color.FromArgb(rng.Next(256), rng.Next(256), rng.Next(256)));
                        using var brush = new SolidBrush(Color.FromArgb(rng.Next(256), rng.Next(256), rng.Next(256)));
                        int x = rng.Next(W - 100), y = rng.Next(H - 100),
                            w = rng.Next(10, 100), h2 = rng.Next(10, 100);
                        g.FillEllipse(brush, x, y, w, h2);
                        g.DrawLine(pen, rng.Next(W), rng.Next(H), rng.Next(W), rng.Next(H));
                    }
                }
                sw.Stop();

                double fps = FRAMES / sw.Elapsed.TotalSeconds;

                sb.AppendLine($"  Resolution  : {W}×{H} (off-screen GDI+)");
                sb.AppendLine($"  Simul. FPS  : {fps:F1}");
                sb.AppendLine($"  Duration    : {sw.ElapsedMilliseconds} ms");
                sb.AppendLine($"  Note        : GDI+ load is CPU-bound; reflects GDI performance, not GPU shader speed.");
                sb.AppendLine($"  Result      : ✔ PASSED");

                meta["stress_gpu"] = "Passed";
                meta["gpu_sim_fps"] = fps.ToString("F1");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  Result      : ✖ FAILED – {ex.Message}");
                meta["stress_gpu"] = "Failed";
            }
        }

        private static void StressTestStorage(StringBuilder sb, Dictionary<string, object> meta)
        {
            sb.AppendLine(Section("STRESS TEST – STORAGE (Sequential Read / Write Speed)"));

            const int FILE_SIZE_MB = 256;
            const long BYTES = (long)FILE_SIZE_MB * 1024 * 1024;
            var writeBuffer = new byte[4 * 1024 * 1024];
            new Random(1).NextBytes(writeBuffer);

            foreach (DriveInfo drive in DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
            {
                string tmpFile = Path.Combine(drive.RootDirectory.FullName, "__pctest_tmp.bin");
                sb.AppendLine($"  Drive {drive.Name}");
                try
                {
                    // ── Write test ────────────────────────────────────────────
                    long written = 0;
                    var wSw = Stopwatch.StartNew();
                    using (var fs = new FileStream(tmpFile, FileMode.Create, FileAccess.Write,
                        FileShare.None, 64 * 1024, FileOptions.WriteThrough))
                    {
                        while (written < BYTES)
                        {
                            int chunk = (int)Math.Min(writeBuffer.Length, BYTES - written);
                            fs.Write(writeBuffer, 0, chunk);
                            written += chunk;
                        }
                        fs.Flush();
                    }
                    wSw.Stop();
                    double writeMBs = FILE_SIZE_MB / wSw.Elapsed.TotalSeconds;

                    // ── Read test ─────────────────────────────────────────────
                    var readBuf = new byte[4 * 1024 * 1024];
                    var rSw = Stopwatch.StartNew();
                    using (var fs = new FileStream(tmpFile, FileMode.Open, FileAccess.Read,
                        FileShare.None, 64 * 1024))
                        while (fs.Read(readBuf, 0, readBuf.Length) > 0) { }
                    rSw.Stop();
                    double readMBs = FILE_SIZE_MB / rSw.Elapsed.TotalSeconds;

                    // ── Drive classification ──────────────────────────────────
                    string driveModel = GetDriveModelForPath(drive.RootDirectory.FullName);
                    string driveClass = DetectDriveType(driveModel, "");
                    if (driveClass == "Unknown")
                    {
                        driveClass = readMBs >= 400 ? "NVMe SSD"
                                   : readMBs >= 100 ? "SATA SSD"
                                   : "HDD";
                    }

                    string readNote = readMBs > 1000 ? " ⚠ (may reflect OS cache)" : "";

                    sb.AppendLine($"    Write Speed : {writeMBs:F0} MB/s");
                    sb.AppendLine($"    Read  Speed : {readMBs:F0} MB/s{readNote}");
                    sb.AppendLine($"    Drive Type  : {driveClass}");
                    sb.AppendLine($"    Result      : ✔ PASSED");

                    string key = drive.Name.Replace(":\\", "").ToLower();
                    meta[$"disk_{key}_read_mbs"] = readMBs.ToString("F0");
                    meta[$"disk_{key}_write_mbs"] = writeMBs.ToString("F0");
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"    Result      : ✖ SKIPPED – {ex.Message}");
                    if (ex.Message.Contains("denied"))
                        sb.AppendLine("    Tip         : Run the app as Administrator to test this drive.");
                }
                finally
                {
                    try { if (File.Exists(tmpFile)) File.Delete(tmpFile); } catch { }
                }
            }
        }

        // ── Get physical disk model for a drive path ──────────────────────────
        private static string GetDriveModelForPath(string drivePath)
        {
            try
            {
                string driveLetter = drivePath.TrimEnd('\\').TrimEnd(':');
                using var partSearcher = new ManagementObjectSearcher(
                    $"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{driveLetter}:'}} " +
                    $"WHERE AssocClass=Win32_LogicalDiskToPartition");

                foreach (ManagementObject part in partSearcher.Get())
                {
                    using var diskSearcher = new ManagementObjectSearcher(
                        $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{part["DeviceID"]}'}} " +
                        $"WHERE AssocClass=Win32_DiskDriveToDiskPartition");
                    foreach (ManagementObject disk in diskSearcher.Get())
                        return disk["Model"]?.ToString()?.Trim() ?? "";
                }
            }
            catch { }
            return "";
        }

        // ══════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════════════════

        private static string Section(string title) =>
            $"\n{"─",50}\n##{title}\n{"─",50}";

        private static string MemTypeStr(ushort t) => t switch
        {
            20 => "DDR",
            21 => "DDR2",
            24 => "DDR3",
            26 => "DDR4",
            30 => "DDR5",
            0 => "Unknown",
            _ => "Other"
        };
    }
}
#endif