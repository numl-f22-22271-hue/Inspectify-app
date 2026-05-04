using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PC_inspect_beta.Core.Platform
{
    public class MacOSHardwareScanner : IHardwareScanner
    {
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

        public OsInfo GetOsInfo()
        {
            var info = new OsInfo
            {
                Name         = "macOS",
                Version      = Run("/usr/bin/sw_vers", "-productVersion"),
                Architecture = Sysctl("hw.machine")
            };

            // kern.boottime returns something like: { sec = 1716000000, usec = 0 }
            var bootRaw = Sysctl("kern.boottime");
            // Extract the sec value
            var secIdx = bootRaw.IndexOf("sec =", StringComparison.Ordinal);
            if (secIdx >= 0)
            {
                var afterSec = bootRaw[(secIdx + 5)..].Trim();
                var numStr = new string(afterSec.TakeWhile(c => char.IsDigit(c)).ToArray());
                if (long.TryParse(numStr, out var bootSec))
                {
                    var uptime = TimeSpan.FromSeconds(DateTimeOffset.Now.ToUnixTimeSeconds() - bootSec);
                    info.Uptime = $"{(int)uptime.TotalDays}d {uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";
                }
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

            // Apple Silicon: brand_string may be empty, use hw.model
            if (string.IsNullOrWhiteSpace(info.Name))
                info.Name = Sysctl("hw.model");

            int.TryParse(Sysctl("hw.physicalcpu"), out var cores);
            int.TryParse(Sysctl("hw.logicalcpu"),  out var threads);
            info.Cores   = cores;
            info.Threads = threads;

            // Intel Macs expose clock speed via sysctl
            if (long.TryParse(Sysctl("hw.cpufrequency_max"), out var hz) && hz > 0)
                info.MaxClockMhz = (int)(hz / 1_000_000);
            else
            {
                // Apple Silicon: try to get performance core freq from ioreg
                var ioreg = Run("/usr/sbin/ioreg", "-r -d1 -c IOPlatformDevice");
                foreach (var line in ioreg.Split('\n'))
                {
                    if (line.Contains("\"nominal-frequency\"") || line.Contains("\"clock-frequency\""))
                    {
                        var val = ParseInt(line);
                        if (val > 0) { info.MaxClockMhz = val / 1_000_000; break; }
                    }
                }
            }

            return info;
        }

        public RamInfo GetRamInfo()
        {
            var info = new RamInfo();
            if (long.TryParse(Sysctl("hw.memsize"), out var bytes))
                info.TotalMb = bytes / 1024 / 1024;

            var vmStat = Run("/usr/bin/vm_stat", "");
            const long pageSize = 16384; // Apple Silicon uses 16 KB pages
            long freePages = 0, inactivePages = 0;
            foreach (var line in vmStat.Split('\n'))
            {
                if (line.StartsWith("Pages free:"))
                    long.TryParse(line.Split(':')[1].Trim().TrimEnd('.'), out freePages);
                if (line.StartsWith("Pages inactive:"))
                    long.TryParse(line.Split(':')[1].Trim().TrimEnd('.'), out inactivePages);
            }
            // Detect actual page size from vm_stat header
            var headerLine = vmStat.Split('\n').FirstOrDefault() ?? "";
            long actualPageSize = pageSize;
            if (headerLine.Contains("page size of"))
            {
                var pageStr = new string(headerLine.Where(char.IsDigit).ToArray());
                if (long.TryParse(pageStr, out var ps) && ps > 0)
                    actualPageSize = ps;
            }
            info.AvailableMb = (freePages + inactivePages) * actualPageSize / 1024 / 1024;

            // Stick details from system_profiler SPMemoryDataType
            var mem = SystemProfilerJson("SPMemoryDataType");
            if (mem.ValueKind == JsonValueKind.Object &&
                mem.TryGetProperty("SPMemoryDataType", out var arr) &&
                arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in arr.EnumerateArray())
                {
                    // Apple Silicon reports unified memory at the top level
                    if (entry.TryGetProperty("SPMemoryDataType", out _) ||
                        entry.TryGetProperty("_items", out var items))
                    {
                        // Nested sticks
                        if (entry.TryGetProperty("_items", out var sticks) &&
                            sticks.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var stick in sticks.EnumerateArray())
                                info.Sticks.Add(ParseStick(stick));
                        }
                    }

                    // Direct stick entries
                    if (entry.TryGetProperty("dimm_type", out _) || entry.TryGetProperty("dimm_size", out _))
                    {
                        info.Sticks.Add(ParseStick(entry));
                    }

                    // Unified memory (Apple Silicon) — single entry with size at top level
                    if (info.Sticks.Count == 0 && entry.TryGetProperty("_name", out _))
                    {
                        var s = new RamStick { CapacityMb = info.TotalMb };
                        if (entry.TryGetProperty("dimm_type", out var ty)) s.Type = ty.GetString() ?? "";
                        if (entry.TryGetProperty("dimm_manufacturer", out var mfr)) s.Manufacturer = mfr.GetString() ?? "";
                        if (string.IsNullOrEmpty(s.Type))
                        {
                            // Try to detect type from SPMemoryDataType properties
                            if (entry.TryGetProperty("dimm_speed", out var sp))
                                s.Type = sp.GetString() ?? "";
                        }
                        info.Sticks.Add(s);
                    }
                }

                // If we still have sticks with 0 capacity on unified memory, fill in the total
                if (info.Sticks.Count == 1 && info.Sticks[0].CapacityMb == 0)
                    info.Sticks[0].CapacityMb = info.TotalMb;
            }

            if (info.Sticks.Count == 0)
            {
                // Fallback: use sysctl to determine type
                var memType = Sysctl("hw.optional.arm64") == "1" ? "LPDDR5" : "DDR4";
                info.Sticks.Add(new RamStick { CapacityMb = info.TotalMb, Type = memType, Manufacturer = "Apple" });
            }

            return info;
        }

        private static RamStick ParseStick(JsonElement stick)
        {
            var s = new RamStick();
            if (stick.TryGetProperty("dimm_size", out var sz))
                s.CapacityMb = ParseSize(sz.GetString() ?? "");
            if (stick.TryGetProperty("dimm_manufacturer", out var mfr))
                s.Manufacturer = mfr.GetString() ?? "";
            if (stick.TryGetProperty("dimm_type", out var ty))
                s.Type = ty.GetString() ?? "";
            if (stick.TryGetProperty("dimm_speed", out var sp))
            {
                var spStr = sp.GetString() ?? "";
                var digits = new string(spStr.TakeWhile(char.IsDigit).ToArray());
                if (int.TryParse(digits, out var spd))
                    s.SpeedMhz = spd;
            }
            return s;
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

                    // Filter out disk images, system volumes, and 0 GB volumes
                    bool isDiskImage = (s.Model ?? "").Contains("Disk Image", StringComparison.OrdinalIgnoreCase);
                    bool isSystemVol = (s.Mount ?? "").StartsWith("com.apple.", StringComparison.OrdinalIgnoreCase);
                    if (isDiskImage || isSystemVol) continue;
                    if (s.CapacityGb <= 0 && s.FreeGb <= 0) continue;

                    list.Add(s);
                }
            }

            // Deduplicate: keep only the entry with most free space per physical model
            var deduped = list
                .GroupBy(d => d.Model ?? "")
                .Select(g => g.OrderByDescending(d => d.FreeGb).First())
                .ToList();

            return deduped.Count > 0 ? deduped : list;
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
            foreach (var token in pmset.Split(new[] { ';', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var t = token.Trim();
                if (t.EndsWith("%") && int.TryParse(t.TrimEnd('%'), out var pct))
                    info.PercentRemaining = pct;
                if (t.Contains("charging") && !t.Contains("not charging"))
                    info.IsCharging = true;
            }

            // Battery health from ioreg — values are in mAh, not mWh on macOS
            // We still store them in DesignCapacityMwh/FullChargeCapacityMwh fields
            // as the health % calculation is what matters
            var ioreg = Run("/usr/sbin/ioreg", "-l -w0 -r -c AppleSmartBattery");
            foreach (var line in ioreg.Split('\n'))
            {
                if (line.Contains("\"DesignCapacity\" =") && !line.Contains("DesignCapacityLabel"))
                    info.DesignCapacityMwh = ParseInt(line);
                if (line.Contains("\"MaxCapacity\" =") && !line.Contains("MaxCapacityLabel"))
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
                            // Try multiple resolution property names
                            string res = "";
                            if (mon.TryGetProperty("_spdisplays_resolution", out var r))
                                res = r.GetString() ?? "";
                            else if (mon.TryGetProperty("spdisplays_resolution", out var r2))
                                res = r2.GetString() ?? "";
                            else if (mon.TryGetProperty("_spdisplays_pixels", out var r3))
                                res = r3.GetString() ?? "";

                            // Parse "3024 x 1964" or "3024x1964" or "3024 x 1964 @ 120 Hz"
                            if (!string.IsNullOrEmpty(res))
                            {
                                res = res.Replace(" ", "");
                                var atIdx = res.IndexOf('@');
                                if (atIdx > 0)
                                {
                                    var hzPart = res[(atIdx + 1)..].Replace("Hz", "").Trim();
                                    if (int.TryParse(hzPart, out var hz))
                                        d.RefreshHz = hz;
                                    res = res[..atIdx];
                                }
                                var xIdx = res.IndexOfAny(new[] { 'x', 'X' });
                                if (xIdx > 0)
                                {
                                    var wStr = new string(res[..xIdx].Where(char.IsDigit).ToArray());
                                    var hStr = new string(res[(xIdx + 1)..].Where(char.IsDigit).ToArray());
                                    if (int.TryParse(wStr, out var w)) d.Width = w;
                                    if (int.TryParse(hStr, out var h)) d.Height = h;
                                }
                            }

                            // Fallback: try to get refresh rate from separate property
                            if (d.RefreshHz == 0 && mon.TryGetProperty("spdisplays_refresh", out var rr))
                            {
                                var rrStr = rr.GetString() ?? "";
                                var digits = new string(rrStr.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray());
                                if (int.TryParse(digits, out var refreshHz))
                                    d.RefreshHz = refreshHz;
                            }

                            if (d.Width > 0 && d.Height > 0)
                            {
                                if (string.IsNullOrEmpty(d.DisplayType))
                                    d.DisplayType = InferDisplayType(mon);
                                list.Add(d);
                            }
                        }
                    }
                }
            }

            // Fallback: use system_profiler SPDisplaysDataType text mode for resolution
            if (list.Count == 0)
            {
                var text = Run("/usr/sbin/system_profiler", "SPDisplaysDataType");
                foreach (var line in text.Split('\n'))
                {
                    if (line.Contains("Resolution:") || line.Contains("UI Looks like:"))
                    {
                        var parts = line.Split(':').LastOrDefault()?.Trim() ?? "";
                        parts = parts.Replace("Retina", "").Replace("HiDPI", "").Trim();
                        var xIdx = parts.IndexOfAny(new[] { 'x', 'X' });
                        if (xIdx > 0)
                        {
                            var wStr = new string(parts[..xIdx].Trim().Where(char.IsDigit).ToArray());
                            var hStr = new string(parts[(xIdx + 1)..].Trim().TakeWhile(c => char.IsDigit(c) || c == ' ').ToArray()).Trim();
                            if (int.TryParse(wStr, out var w) && int.TryParse(hStr, out var h))
                                list.Add(new DisplayInfo { Width = w, Height = h, DisplayType = "IPS LCD" });
                        }
                    }
                }
            }

            return list;
        }

        public BiosInfo GetBiosInfo()
        {
            // Use system_profiler text mode for Boot ROM — JSON doesn't expose it easily
            var hwText = Run("/usr/sbin/system_profiler", "SPHardwareDataType");
            string bootRom = "";
            foreach (var line in hwText.Split('\n'))
            {
                if (line.Contains("Boot ROM Version") || line.Contains("System Firmware"))
                {
                    bootRom = line.Split(':').LastOrDefault()?.Trim() ?? "";
                    break;
                }
            }

            return new BiosInfo
            {
                Manufacturer     = "Apple Inc.",
                Version          = bootRom,
                MotherboardModel = Sysctl("hw.model"),
                SerialNumber     = ""
            };
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
            var numStr = new string(line[(idx + 1)..].Trim().TakeWhile(char.IsDigit).ToArray());
            return int.TryParse(numStr, out var n) ? n : 0;
        }

        private static string InferDisplayType(JsonElement mon)
        {
            var name = "";
            if (mon.TryGetProperty("_name", out var n))
                name = n.GetString() ?? "";

            if (name.Contains("XDR", StringComparison.OrdinalIgnoreCase))
                return "Liquid Retina XDR (Mini-LED)";
            if (name.Contains("Liquid Retina", StringComparison.OrdinalIgnoreCase))
                return "Liquid Retina (IPS LCD)";
            if (name.Contains("Retina", StringComparison.OrdinalIgnoreCase))
                return "Retina IPS LCD";

            var isBuiltIn = name.Contains("Built-in", StringComparison.OrdinalIgnoreCase)
                         || name.Contains("Color LCD", StringComparison.OrdinalIgnoreCase);
            return isBuiltIn ? "IPS LCD" : "LCD";
        }

        public static bool HasTouchId()
        {
            var hw = Run("/usr/sbin/system_profiler", "SPHardwareDataType");
            return hw.Contains("Touch ID", StringComparison.OrdinalIgnoreCase);
        }
    }
}
