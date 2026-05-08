#if WINDOWS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;

namespace PC_inspect_beta.Core.Platform
{
    /// <summary>
    /// Windows hardware scanner. Uses WMI (System.Management) — same APIs as
    /// the original DiagnosticsEngine, but exposed through the cross-platform
    /// IHardwareScanner contract.
    /// </summary>
    public class WindowsHardwareScanner : IHardwareScanner
    {
        public OsInfo GetOsInfo()
        {
            var info = new OsInfo
            {
                Name         = "Windows",
                Version      = Environment.OSVersion.VersionString,
                Architecture = Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit"
            };
            try
            {
                using var mos = new ManagementObjectSearcher("SELECT Caption FROM Win32_OperatingSystem");
                foreach (ManagementObject obj in mos.Get())
                {
                    info.Name = obj["Caption"]?.ToString()?.Trim() ?? "Windows";
                    break;
                }
            } catch { }
            var up = TimeSpan.FromMilliseconds(Environment.TickCount64);
            info.Uptime = $"{(int)up.TotalDays}d {up.Hours:D2}:{up.Minutes:D2}:{up.Seconds:D2}";
            return info;
        }

        public CpuInfo GetCpuInfo()
        {
            var info = new CpuInfo { Architecture = Environment.Is64BitOperatingSystem ? "x64" : "x86" };
            try
            {
                using var mos = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
                foreach (ManagementObject obj in mos.Get())
                {
                    info.Name        = obj["Name"]?.ToString()?.Trim() ?? "";
                    info.Cores       = Convert.ToInt32(obj["NumberOfCores"] ?? 0);
                    info.Threads     = Convert.ToInt32(obj["NumberOfLogicalProcessors"] ?? 0);
                    info.MaxClockMhz = Convert.ToInt32(obj["MaxClockSpeed"] ?? 0);
                    break;
                }
            } catch { }
            return info;
        }

        public RamInfo GetRamInfo()
        {
            var info = new RamInfo();
            try
            {
                using var mos = new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMemory");
                foreach (ManagementObject obj in mos.Get())
                {
                    int memType = Convert.ToInt32(obj["SMBIOSMemoryType"] ?? 0);
                    var stick = new RamStick
                    {
                        CapacityMb   = Convert.ToInt64(obj["Capacity"] ?? 0) / 1024 / 1024,
                        Manufacturer = CleanManufacturer(obj["Manufacturer"]?.ToString() ?? ""),
                        SpeedMhz     = Convert.ToInt32(obj["Speed"] ?? 0),
                        Type         = MemoryTypeToString(memType)
                    };
                    info.Sticks.Add(stick);
                    info.TotalMb += stick.CapacityMb;
                }
            } catch { }
            try
            {
                using var os = new ManagementObjectSearcher("SELECT FreePhysicalMemory FROM Win32_OperatingSystem");
                foreach (ManagementObject obj in os.Get())
                {
                    info.AvailableMb = Convert.ToInt64(obj["FreePhysicalMemory"] ?? 0) / 1024;
                    break;
                }
            } catch { }
            return info;
        }

        private static string MemoryTypeToString(int smbiosType) => smbiosType switch
        {
            20 => "DDR",
            21 => "DDR2",
            22 => "DDR2",
            24 => "DDR3",
            26 => "DDR4",
            30 => "LPDDR4",
            34 => "DDR5",
            35 => "LPDDR5",
            _  => "DDR"
        };

        private static string CleanManufacturer(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            var trimmed = raw.Trim();
            if (trimmed.All(c => char.IsLetterOrDigit(c)) && trimmed.Length > 10)
            {
                return trimmed.ToUpperInvariant() switch
                {
                    var s when s.StartsWith("80AD") => "SK Hynix",
                    var s when s.StartsWith("80CE") => "Samsung",
                    var s when s.StartsWith("802C") => "Micron",
                    var s when s.StartsWith("8551") => "Qimonda",
                    var s when s.StartsWith("014F") => "Transcend",
                    var s when s.StartsWith("859B") => "Crucial",
                    _ => trimmed
                };
            }
            return trimmed;
        }

        public List<StorageInfo> GetStorageInfo()
        {
            var list = new List<StorageInfo>();

            var diskTypes = new Dictionary<int, string>();
            try
            {
                using var pd = new ManagementObjectSearcher("root\\Microsoft\\Windows\\Storage",
                    "SELECT DeviceId, MediaType FROM MSFT_PhysicalDisk");
                foreach (ManagementObject obj in pd.Get())
                {
                    int id = Convert.ToInt32(obj["DeviceId"] ?? -1);
                    int mt = Convert.ToInt32(obj["MediaType"] ?? 0);
                    diskTypes[id] = mt == 4 ? "SSD" : mt == 3 ? "HDD" : "Unknown";
                }
            } catch { }

            var driveFreeSpace = new Dictionary<string, long>();
            try
            {
                foreach (var d in System.IO.DriveInfo.GetDrives()
                             .Where(d => d.IsReady && d.DriveType == System.IO.DriveType.Fixed))
                    driveFreeSpace[d.Name.TrimEnd('\\')] = d.AvailableFreeSpace / 1024 / 1024 / 1024;
            } catch { }

            try
            {
                using var mos = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");
                foreach (ManagementObject obj in mos.Get())
                {
                    string model = obj["Model"]?.ToString() ?? "";
                    string deviceId = obj["DeviceID"]?.ToString() ?? "";
                    int diskIndex = Convert.ToInt32(obj["Index"] ?? -1);

                    string type = "HDD";
                    if (diskTypes.TryGetValue(diskIndex, out var detected))
                        type = detected;
                    else if (InferSsdFromModel(model))
                        type = "SSD";

                    string mount = GetDriveLetters(deviceId);
                    long freeGb = 0;
                    foreach (var letter in mount.Split(',', StringSplitOptions.TrimEntries))
                    {
                        var key = letter.TrimEnd('\\');
                        if (driveFreeSpace.TryGetValue(key, out var free))
                            freeGb += free;
                    }

                    list.Add(new StorageInfo
                    {
                        Model      = model,
                        Type       = type,
                        CapacityGb = Convert.ToInt64(obj["Size"] ?? 0) / 1024 / 1024 / 1024,
                        FreeGb     = freeGb,
                        Mount      = string.IsNullOrEmpty(mount) ? deviceId : mount
                    });
                }
            } catch { }
            return list;
        }

        private static bool InferSsdFromModel(string model)
        {
            var m = model.ToUpperInvariant();
            return m.Contains("SSD") || m.Contains("NVME") || m.Contains("KINGSTON")
                || m.Contains("SAMSUNG EVO") || m.Contains("SAMSUNG PRO")
                || m.Contains("WD BLUE SN") || m.Contains("WD BLACK SN")
                || m.Contains("CRUCIAL") || m.Contains("SANDISK");
        }

        private static string GetDriveLetters(string deviceId)
        {
            try
            {
                var letters = new List<string>();
                using var dp = new ManagementObjectSearcher(
                    $"ASSOCIATORS OF {{Win32_DiskDrive.DeviceID='{deviceId}'}} WHERE AssocClass=Win32_DiskDriveToDiskPartition");
                foreach (ManagementObject partition in dp.Get())
                {
                    using var ld = new ManagementObjectSearcher(
                        $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partition["DeviceID"]}'}} WHERE AssocClass=Win32_LogicalDiskToPartition");
                    foreach (ManagementObject logical in ld.Get())
                    {
                        var letter = logical["DeviceID"]?.ToString();
                        if (!string.IsNullOrEmpty(letter))
                            letters.Add(letter + "\\");
                    }
                }
                return string.Join(", ", letters);
            }
            catch { return ""; }
        }

        public List<GpuInfo> GetGpuInfo()
        {
            var list = new List<GpuInfo>();
            try
            {
                using var mos = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
                foreach (ManagementObject obj in mos.Get())
                {
                    list.Add(new GpuInfo
                    {
                        Name          = obj["Name"]?.ToString() ?? "",
                        VramMb        = Convert.ToInt64(obj["AdapterRAM"] ?? 0) / 1024 / 1024,
                        DriverVersion = obj["DriverVersion"]?.ToString() ?? ""
                    });
                }
            } catch { }
            return list;
        }

        public BatteryInfo? GetBatteryInfo()
        {
            try
            {
                using var bq = new ManagementObjectSearcher("SELECT * FROM Win32_Battery");
                foreach (ManagementObject obj in bq.Get())
                {
                    int status = Convert.ToInt32(obj["BatteryStatus"] ?? 0);
                    // BatteryStatus: 2 = AC power (charging), 1 = discharging
                    bool isCharging = status == 2 || status == 6 || status == 7 || status == 8 || status == 9;
                    int percent = Convert.ToInt32(obj["EstimatedChargeRemaining"] ?? 0);

                    var info = new BatteryInfo
                    {
                        PercentRemaining = percent,
                        IsCharging       = isCharging
                    };

                    try
                    {
                        using var fc = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM BatteryFullChargedCapacity");
                        foreach (ManagementObject o in fc.Get()) info.FullChargeCapacityMwh = Convert.ToInt32(o["FullChargedCapacity"] ?? 0);
                        using var sd = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM BatteryStaticData");
                        foreach (ManagementObject o in sd.Get()) info.DesignCapacityMwh = Convert.ToInt32(o["DesignedCapacity"] ?? 0);
                    } catch { }

                    return info;
                }
            } catch { }
            return null;
        }

        public List<DisplayInfo> GetDisplayInfo()
        {
            var list = new List<DisplayInfo>();
            try
            {
                using var mos = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
                foreach (ManagementObject obj in mos.Get())
                {
                    list.Add(new DisplayInfo
                    {
                        Width      = Convert.ToInt32(obj["CurrentHorizontalResolution"] ?? 0),
                        Height     = Convert.ToInt32(obj["CurrentVerticalResolution"] ?? 0),
                        RefreshHz  = Convert.ToInt32(obj["CurrentRefreshRate"] ?? 0)
                    });
                }
            } catch { }
            return list;
        }

        public BiosInfo GetBiosInfo()
        {
            var info = new BiosInfo();
            try
            {
                using var bs = new ManagementObjectSearcher("SELECT * FROM Win32_BIOS");
                foreach (ManagementObject obj in bs.Get())
                {
                    info.Manufacturer = obj["Manufacturer"]?.ToString() ?? "";
                    info.Version      = obj["Version"]?.ToString() ?? "";
                    info.SerialNumber = obj["SerialNumber"]?.ToString() ?? "";
                    break;
                }
                using var mb = new ManagementObjectSearcher("SELECT * FROM Win32_BaseBoard");
                foreach (ManagementObject obj in mb.Get())
                {
                    info.MotherboardModel = obj["Product"]?.ToString() ?? "";
                    break;
                }
            } catch { }
            return info;
        }

        public List<NetworkInfo> GetNetworkInfo()
        {
            var list = new List<NetworkInfo>();
            try
            {
                using var mos = new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapter WHERE PhysicalAdapter=true");
                foreach (ManagementObject obj in mos.Get())
                {
                    list.Add(new NetworkInfo
                    {
                        Name       = obj["Name"]?.ToString() ?? "",
                        MacAddress = obj["MACAddress"]?.ToString() ?? "",
                        Type       = (obj["Name"]?.ToString() ?? "").ToLower().Contains("wireless") ? "WiFi" : "Ethernet"
                    });
                }
            } catch { }
            return list;
        }
    }
}
#endif
