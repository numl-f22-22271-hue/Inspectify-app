#if WINDOWS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Windows.Forms;

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
                Name         = Environment.OSVersion.Platform.ToString(),
                Version      = Environment.OSVersion.VersionString,
                Architecture = Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit"
            };
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
                    var stick = new RamStick
                    {
                        CapacityMb   = Convert.ToInt64(obj["Capacity"] ?? 0) / 1024 / 1024,
                        Manufacturer = obj["Manufacturer"]?.ToString() ?? "",
                        SpeedMhz     = Convert.ToInt32(obj["Speed"] ?? 0),
                        Type         = obj["MemoryType"]?.ToString() ?? ""
                    };
                    info.Sticks.Add(stick);
                    info.TotalMb += stick.CapacityMb;
                }
            } catch { }
            return info;
        }

        public List<StorageInfo> GetStorageInfo()
        {
            var list = new List<StorageInfo>();
            try
            {
                using var mos = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");
                foreach (ManagementObject obj in mos.Get())
                {
                    list.Add(new StorageInfo
                    {
                        Model      = obj["Model"]?.ToString() ?? "",
                        Type       = (obj["MediaType"]?.ToString() ?? "").Contains("SSD") ? "SSD" : "HDD",
                        CapacityGb = Convert.ToInt64(obj["Size"] ?? 0) / 1024 / 1024 / 1024,
                        Mount      = obj["DeviceID"]?.ToString() ?? ""
                    });
                }
            } catch { }
            return list;
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
            var pw = SystemInformation.PowerStatus;
            bool hasBattery = pw.BatteryChargeStatus != BatteryChargeStatus.NoSystemBattery
                           && pw.BatteryChargeStatus != BatteryChargeStatus.Unknown;
            if (!hasBattery) return null;

            var info = new BatteryInfo
            {
                PercentRemaining = (int)(pw.BatteryLifePercent * 100),
                IsCharging       = pw.PowerLineStatus == PowerLineStatus.Online
            };
            try
            {
                using var bq = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM BatteryFullChargedCapacity");
                foreach (ManagementObject obj in bq.Get()) info.FullChargeCapacityMwh = Convert.ToInt32(obj["FullChargedCapacity"] ?? 0);
                using var bd = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM BatteryStaticData");
                foreach (ManagementObject obj in bd.Get()) info.DesignCapacityMwh = Convert.ToInt32(obj["DesignedCapacity"] ?? 0);
            } catch { }
            return info;
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
