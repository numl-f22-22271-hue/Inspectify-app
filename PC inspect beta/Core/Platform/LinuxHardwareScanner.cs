using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace PC_inspect_beta.Core.Platform
{
    /// <summary>
    /// Linux hardware scanner. Uses /proc, /sys, and standard CLI tools
    /// (lsblk, lspci, lscpu, dmidecode where available).
    /// </summary>
    public class LinuxHardwareScanner : IHardwareScanner
    {
        private static string Run(string cmd, string args, int timeoutMs = 5000)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = cmd, Arguments = args,
                    RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                if (p == null) return "";
                if (!p.WaitForExit(timeoutMs)) { try { p.Kill(); } catch { } return ""; }
                return p.StandardOutput.ReadToEnd().Trim();
            } catch { return ""; }
        }

        private static string ReadFile(string path) =>
            File.Exists(path) ? File.ReadAllText(path).Trim() : "";

        public OsInfo GetOsInfo()
        {
            var info = new OsInfo
            {
                Name         = "Linux",
                Version      = Run("/usr/bin/uname", "-r"),
                Architecture = Run("/usr/bin/uname", "-m")
            };
            // Pretty name from /etc/os-release
            foreach (var line in (ReadFile("/etc/os-release") ?? "").Split('\n'))
                if (line.StartsWith("PRETTY_NAME=")) info.Name = line.Split('=')[1].Trim('"');

            var uptime = ReadFile("/proc/uptime");
            if (double.TryParse(uptime.Split(' ').FirstOrDefault(), out var sec))
            {
                var ts = TimeSpan.FromSeconds(sec);
                info.Uptime = $"{(int)ts.TotalDays}d {ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
            }
            return info;
        }

        public CpuInfo GetCpuInfo()
        {
            var info = new CpuInfo();
            foreach (var line in ReadFile("/proc/cpuinfo").Split('\n'))
            {
                if (string.IsNullOrEmpty(info.Name) && line.StartsWith("model name"))
                    info.Name = line.Split(':').LastOrDefault()?.Trim() ?? "";
                if (line.StartsWith("cpu cores"))
                    int.TryParse(line.Split(':').LastOrDefault()?.Trim(), out var c) ; if (info.Cores == 0 && int.TryParse(line.Split(':').LastOrDefault()?.Trim(), out var c2)) info.Cores = c2;
            }
            info.Threads      = Environment.ProcessorCount;
            info.Architecture = Run("/usr/bin/uname", "-m");
            return info;
        }

        public RamInfo GetRamInfo()
        {
            var info = new RamInfo();
            foreach (var line in ReadFile("/proc/meminfo").Split('\n'))
            {
                if (line.StartsWith("MemTotal:") && long.TryParse(line.Split(':')[1].Trim().Split(' ')[0], out var t))
                    info.TotalMb = t / 1024;
                if (line.StartsWith("MemAvailable:") && long.TryParse(line.Split(':')[1].Trim().Split(' ')[0], out var a))
                    info.AvailableMb = a / 1024;
            }
            return info;
        }

        public List<StorageInfo> GetStorageInfo()
        {
            var list = new List<StorageInfo>();
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady) continue;
                list.Add(new StorageInfo
                {
                    Mount      = drive.Name,
                    CapacityGb = drive.TotalSize / 1024 / 1024 / 1024,
                    FreeGb     = drive.AvailableFreeSpace / 1024 / 1024 / 1024,
                    Type       = drive.DriveType.ToString(),
                    Model      = drive.VolumeLabel
                });
            }
            return list;
        }

        public List<GpuInfo> GetGpuInfo()
        {
            var list = new List<GpuInfo>();
            var lspci = Run("/usr/bin/lspci", "");
            foreach (var line in lspci.Split('\n'))
            {
                if (line.Contains("VGA") || line.Contains("3D controller"))
                {
                    var parts = line.Split(':', 3);
                    list.Add(new GpuInfo { Name = parts.Length > 2 ? parts[2].Trim() : line });
                }
            }
            return list;
        }

        public BatteryInfo? GetBatteryInfo()
        {
            var batt = "/sys/class/power_supply/BAT0";
            if (!Directory.Exists(batt)) batt = "/sys/class/power_supply/BAT1";
            if (!Directory.Exists(batt)) return null;

            var info = new BatteryInfo();
            int.TryParse(ReadFile($"{batt}/capacity"), out var pct); info.PercentRemaining = pct;
            info.IsCharging = ReadFile($"{batt}/status") == "Charging";
            int.TryParse(ReadFile($"{batt}/charge_full_design"), out var d); info.DesignCapacityMwh = d / 1000;
            int.TryParse(ReadFile($"{batt}/charge_full"),       out var f); info.FullChargeCapacityMwh = f / 1000;
            return info;
        }

        public List<DisplayInfo> GetDisplayInfo()
        {
            var list = new List<DisplayInfo>();
            var xrandr = Run("/usr/bin/xrandr", "");
            foreach (var line in xrandr.Split('\n'))
            {
                if (line.Contains(" connected") && line.Contains("primary"))
                {
                    // Example: "HDMI-1 connected primary 1920x1080+0+0 ..."
                    var parts = line.Split(' ').FirstOrDefault(p => p.Contains('x') && p.Contains('+'));
                    if (parts != null)
                    {
                        var resOnly = parts.Split('+')[0];
                        var dims = resOnly.Split('x');
                        if (dims.Length == 2 && int.TryParse(dims[0], out var w) && int.TryParse(dims[1], out var h))
                            list.Add(new DisplayInfo { Width = w, Height = h });
                    }
                }
            }
            return list;
        }

        public BiosInfo GetBiosInfo()
        {
            return new BiosInfo
            {
                Manufacturer     = ReadFile("/sys/class/dmi/id/sys_vendor"),
                Version          = ReadFile("/sys/class/dmi/id/bios_version"),
                MotherboardModel = ReadFile("/sys/class/dmi/id/product_name"),
                SerialNumber     = ReadFile("/sys/class/dmi/id/product_serial")
            };
        }

        public List<NetworkInfo> GetNetworkInfo()
        {
            var list = new List<NetworkInfo>();
            foreach (var dir in Directory.GetDirectories("/sys/class/net"))
            {
                var name = Path.GetFileName(dir);
                if (name == "lo") continue;
                list.Add(new NetworkInfo
                {
                    Name       = name,
                    MacAddress = ReadFile($"{dir}/address"),
                    Type       = name.StartsWith("wl") ? "WiFi" : name.StartsWith("en") || name.StartsWith("eth") ? "Ethernet" : "Other"
                });
            }
            return list;
        }
    }
}
