using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PC_inspect_beta.Core.Platform
{
    /// <summary>
    /// macOS hardware scanner.
    /// Uses sysctl, system_profiler, ioreg, and pmset (all built-in to macOS).
    /// </summary>
    public class MacOSHardwareScanner : IHardwareScanner
    {
        // ── Helpers ────────────────────────────────────────────────────────

        private static string Run(string cmd, string args, int timeoutMs = 10000)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName               = cmd,
                    Arguments              = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                };
                using var p = Process.Start(psi);
                if (p == null) return "";

                if (!p.WaitForExit(timeoutMs)) { try { p.Kill(); } catch { } return ""; }
                return p.StandardOutput.ReadToEnd().Trim();
            }
            catch { return ""; }
        }

        private static string Sysctl(string key) => Run("/usr/sbin/sysctl", $"-n {key}");

        private static JsonElement SystemProfilerJson(string dataType)
        {
            var json = Run("/usr/sbin/system_profiler", $"-json {dataType}", 30000);
            if (string.IsNullOrWhiteSpace(json)) return default;
            try { return JsonDocument.Parse(json).RootElement; }
            catch { return default; }
        }

        // ── Implementations ────────────────────────────────────────────────

        public OsInfo GetOsInfo()
        {
            var info = new OsInfo
            {
                Name         = "macOS",
                Version      = Run("/usr/bin/sw_vers", "-productVersion"),
                Architecture = Sysctl("hw.machine")
            };

            // Uptime in seconds
            var bootSec = Sysctl("kern.boottime");
            if (long.TryParse(Sysctl("kern.boottime").Split('=').LastOrDefault()?.Trim(','),
                              NumberStyles.Integer, CultureInfo.InvariantCulture, out var bt))
            {
                var uptime = TimeSpan.FromSeconds(DateTimeOffset.Now.ToUnixTimeSeconds() - bt);
                info.Uptime = $"{(int)uptime.TotalDays}d {uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";
            }
            return info;
        }

        public CpuInfo GetCpuInfo()
        {
            var info = new CpuInfo
            {
                Name         = Sysctl("machdep.cpu.brand_string"),
                Architecture = Sysctl("hw.machine")
            };

            int.TryParse(Sysctl("hw.physicalcpu"), out var cores);
            int.TryParse(Sysctl("hw.logicalcpu"),  out var threads);
            info.Cores   = cores;
            info.Threads = threads;

            // Apple Silicon doesn't expose clock speed via sysctl on modern macOS.
            // Try cpufrequency (works on Intel Macs).
            if (long.TryParse(Sysctl("hw.cpufrequency_max"), out var hz) && hz > 0)
                info.MaxClockMhz = (int)(hz / 1_000_000);

            return info;
        }

        public RamInfo GetRamInfo()
        {
            var info = new RamInfo();
            if (long.TryParse(Sysctl("hw.memsize"), out var bytes))
                info.TotalMb = bytes / 1024 / 1024;

            // available memory: vm_stat is more accurate
            var vmStat = Run("/usr/bin/vm_stat", "");
            const long pageSize = 4096; // typical
            long freePages = 0, inactivePages = 0;
            foreach (var line in vmStat.Split('\n'))
            {
                if (line.StartsWith("Pages free:"))
                    long.TryParse(line.Split(':')[1].Trim().TrimEnd('.'), out freePages);
                if (line.StartsWith("Pages inactive:"))
                    long.TryParse(line.Split(':')[1].Trim().TrimEnd('.'), out inactivePages);
            }
            info.AvailableMb = (freePages + inactivePages) * pageSize / 1024 / 1024;

            // Stick details from system_profiler SPMemoryDataType
            var mem = SystemProfilerJson("SPMemoryDataType");
            if (mem.ValueKind == JsonValueKind.Object &&
                mem.TryGetProperty("SPMemoryDataType", out var arr) &&
                arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var stick in arr.EnumerateArray())
                {
                    var s = new RamStick();
                    if (stick.TryGetProperty("dimm_size", out var sz)) s.CapacityMb = ParseSize(sz.GetString() ?? "");
                    if (stick.TryGetProperty("dimm_manufacturer", out var mfr)) s.Manufacturer = mfr.GetString() ?? "";
                    if (stick.TryGetProperty("dimm_speed", out var sp)) int.TryParse(new string((sp.GetString() ?? "").TakeWhile(char.IsDigit).ToArray()), out var spd) ; s.SpeedMhz = 0;
                    if (stick.TryGetProperty("dimm_type",  out var ty)) s.Type = ty.GetString() ?? "";
                    info.Sticks.Add(s);
                }
            }
            return info;
        }

        public List<StorageInfo> GetStorageInfo()
        {
            var list = new List<StorageInfo>();
            var storage = SystemProfilerJson("SPStorageDataType");
            if (storage.ValueKind == JsonValueKind.Object &&
                storage.TryGetProperty("SPStorageDataType", out var arr) &&
                arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var disk in arr.EnumerateArray())
                {
                    var s = new StorageInfo();
                    if (disk.TryGetProperty("_name", out var n)) s.Mount = n.GetString() ?? "";
                    if (disk.TryGetProperty("size_in_bytes", out var sz)) s.CapacityGb = sz.GetInt64() / 1024 / 1024 / 1024;
                    if (disk.TryGetProperty("free_space_in_bytes", out var fs)) s.FreeGb = fs.GetInt64() / 1024 / 1024 / 1024;
                    if (disk.TryGetProperty("physical_drive", out var pd) && pd.ValueKind == JsonValueKind.Object)
                    {
                        if (pd.TryGetProperty("device_name", out var dn)) s.Model = dn.GetString() ?? "";
                        if (pd.TryGetProperty("medium_type", out var mt)) s.Type  = mt.GetString() ?? "";
                    }
                    list.Add(s);
                }
            }
            return list;
        }

        public List<GpuInfo> GetGpuInfo()
        {
            var list = new List<GpuInfo>();
            var disp = SystemProfilerJson("SPDisplaysDataType");
            if (disp.ValueKind == JsonValueKind.Object &&
                disp.TryGetProperty("SPDisplaysDataType", out var arr) &&
                arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var gpu in arr.EnumerateArray())
                {
                    var g = new GpuInfo();
                    if (gpu.TryGetProperty("_name", out var n)) g.Name = n.GetString() ?? "";
                    if (gpu.TryGetProperty("spdisplays_vram", out var v)) g.VramMb = ParseSize(v.GetString() ?? "");
                    if (gpu.TryGetProperty("spdisplays_metal_family", out var dv)) g.DriverVersion = dv.GetString() ?? "";
                    list.Add(g);
                }
            }
            return list;
        }

        public BatteryInfo? GetBatteryInfo()
        {
            var pmset = Run("/usr/bin/pmset", "-g batt");
            if (string.IsNullOrWhiteSpace(pmset) || pmset.Contains("No batteries"))
                return null;

            var info = new BatteryInfo();
            // Example line: "-InternalBattery-0 (id=12345)	93%; charging; 1:23 remaining present: true"
            foreach (var token in pmset.Split(new[] { ';', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var t = token.Trim();
                if (t.EndsWith("%") && int.TryParse(t.TrimEnd('%'), out var pct))
                    info.PercentRemaining = pct;
                if (t.Contains("charging") && !t.Contains("not charging"))
                    info.IsCharging = true;
            }

            // Battery health from ioreg
            var ioreg = Run("/usr/sbin/ioreg", "-l -w0 -r -c AppleSmartBattery");
            foreach (var line in ioreg.Split('\n'))
            {
                if (line.Contains("\"DesignCapacity\" ="))
                    info.DesignCapacityMwh = ParseInt(line);
                if (line.Contains("\"MaxCapacity\" ="))
                    info.FullChargeCapacityMwh = ParseInt(line);
            }
            return info;
        }

        public List<DisplayInfo> GetDisplayInfo()
        {
            var list = new List<DisplayInfo>();
            var disp = SystemProfilerJson("SPDisplaysDataType");
            if (disp.ValueKind == JsonValueKind.Object &&
                disp.TryGetProperty("SPDisplaysDataType", out var arr) &&
                arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var gpu in arr.EnumerateArray())
                {
                    if (gpu.TryGetProperty("spdisplays_ndrvs", out var monitors) &&
                        monitors.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var mon in monitors.EnumerateArray())
                        {
                            var d = new DisplayInfo();
                            if (mon.TryGetProperty("_spdisplays_resolution", out var r))
                            {
                                var res = r.GetString() ?? "";
                                var parts = res.Split('x', ' ');
                                if (parts.Length >= 2 && int.TryParse(parts[0], out var w) && int.TryParse(parts[1], out var h))
                                {
                                    d.Width = w;
                                    d.Height = h;
                                }
                            }
                            list.Add(d);
                        }
                    }
                }
            }
            return list;
        }

        public BiosInfo GetBiosInfo()
        {
            var info = new BiosInfo
            {
                Manufacturer     = "Apple Inc.",
                Version          = Run("/usr/bin/system_profiler", "SPHardwareDataType | grep 'Boot ROM' | awk '{print $4}'"),
                MotherboardModel = Sysctl("hw.model"),
                SerialNumber     = Run("/usr/sbin/ioreg", "-l | grep IOPlatformSerialNumber | awk '{print $4}'").Trim('"')
            };
            return info;
        }

        public List<NetworkInfo> GetNetworkInfo()
        {
            var list = new List<NetworkInfo>();
            var ifconfig = Run("/sbin/ifconfig", "-a");
            string? currentName = null, currentMac = null, currentIp = null, currentType = null;

            foreach (var line in ifconfig.Split('\n'))
            {
                if (!line.StartsWith("\t") && line.Contains(":") && !line.StartsWith(" "))
                {
                    if (currentName != null && currentMac != null)
                        list.Add(new NetworkInfo { Name = currentName, MacAddress = currentMac, IpAddress = currentIp ?? "", Type = currentType ?? "" });

                    currentName = line.Split(':')[0];
                    currentType = currentName.StartsWith("en") ? "Ethernet/WiFi" : currentName.StartsWith("wl") ? "WiFi" : "Other";
                    currentMac = currentIp = null;
                }
                else if (line.Contains("ether "))
                    currentMac = line.Trim().Split(' ').LastOrDefault();
                else if (line.Contains("inet ") && !line.Contains("inet6"))
                    currentIp = line.Trim().Split(' ').Skip(1).FirstOrDefault();
            }
            if (currentName != null && currentMac != null)
                list.Add(new NetworkInfo { Name = currentName, MacAddress = currentMac, IpAddress = currentIp ?? "", Type = currentType ?? "" });

            return list.Where(n => !n.Name.StartsWith("lo")).ToList();
        }

        // ── Parse helpers ──────────────────────────────────────────────────

        private static long ParseSize(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0;
            s = s.Trim().ToUpperInvariant();
            double mult = 1;
            if (s.EndsWith("GB")) { mult = 1024;  s = s[..^2].Trim(); }
            else if (s.EndsWith("MB")) { mult = 1; s = s[..^2].Trim(); }
            else if (s.EndsWith("TB")) { mult = 1024 * 1024; s = s[..^2].Trim(); }
            return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? (long)(n * mult) : 0;
        }

        private static int ParseInt(string line)
        {
            var idx = line.LastIndexOf('=');
            if (idx < 0) return 0;
            return int.TryParse(line[(idx + 1)..].Trim(), out var n) ? n : 0;
        }
    }
}
