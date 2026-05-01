using System.Collections.Generic;

namespace PC_inspect_beta.Core.Platform
{
    /// <summary>
    /// Cross-platform hardware-scanning contract.
    /// Each operating system implements this differently
    /// (Windows: WMI, macOS: sysctl/system_profiler, Linux: /proc, /sys).
    /// </summary>
    public interface IHardwareScanner
    {
        /// <summary>OS name + version, architecture, uptime.</summary>
        OsInfo GetOsInfo();

        /// <summary>CPU model, cores, threads, clock speed.</summary>
        CpuInfo GetCpuInfo();

        /// <summary>Total RAM and per-stick details where available.</summary>
        RamInfo GetRamInfo();

        /// <summary>All disks with capacity, free space, and type (SSD/HDD).</summary>
        List<StorageInfo> GetStorageInfo();

        /// <summary>GPU model and VRAM where available.</summary>
        List<GpuInfo> GetGpuInfo();

        /// <summary>Battery percent + design vs current capacity (health %).</summary>
        BatteryInfo? GetBatteryInfo();

        /// <summary>Display resolution + refresh rate.</summary>
        List<DisplayInfo> GetDisplayInfo();

        /// <summary>BIOS/firmware version, motherboard model.</summary>
        BiosInfo GetBiosInfo();

        /// <summary>Network adapters (Wi-Fi, Ethernet) with MAC.</summary>
        List<NetworkInfo> GetNetworkInfo();
    }

    // ── Result POCOs (platform-agnostic) ─────────────────────────────────

    public class OsInfo
    {
        public string Name { get; set; } = "";
        public string Version { get; set; } = "";
        public string Architecture { get; set; } = "";
        public string Uptime { get; set; } = "";
    }

    public class CpuInfo
    {
        public string Name { get; set; } = "";
        public int Cores { get; set; }
        public int Threads { get; set; }
        public int MaxClockMhz { get; set; }
        public string Architecture { get; set; } = "";
    }

    public class RamInfo
    {
        public long TotalMb { get; set; }
        public long AvailableMb { get; set; }
        public List<RamStick> Sticks { get; set; } = new();
    }

    public class RamStick
    {
        public long CapacityMb { get; set; }
        public string Manufacturer { get; set; } = "";
        public int SpeedMhz { get; set; }
        public string Type { get; set; } = "";
    }

    public class StorageInfo
    {
        public string Model { get; set; } = "";
        public string Type { get; set; } = ""; // SSD / HDD / NVMe
        public long CapacityGb { get; set; }
        public long FreeGb { get; set; }
        public string Mount { get; set; } = "";
    }

    public class GpuInfo
    {
        public string Name { get; set; } = "";
        public long VramMb { get; set; }
        public string DriverVersion { get; set; } = "";
    }

    public class BatteryInfo
    {
        public int PercentRemaining { get; set; }
        public bool IsCharging { get; set; }
        public int DesignCapacityMwh { get; set; }
        public int FullChargeCapacityMwh { get; set; }
        public int HealthPercent => DesignCapacityMwh > 0
            ? (int)((double)FullChargeCapacityMwh / DesignCapacityMwh * 100)
            : 0;
    }

    public class DisplayInfo
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int RefreshHz { get; set; }
        public string Manufacturer { get; set; } = "";
    }

    public class BiosInfo
    {
        public string Manufacturer { get; set; } = "";
        public string Version { get; set; } = "";
        public string MotherboardModel { get; set; } = "";
        public string SerialNumber { get; set; } = "";
    }

    public class NetworkInfo
    {
        public string Name { get; set; } = "";
        public string MacAddress { get; set; } = "";
        public string Type { get; set; } = ""; // WiFi / Ethernet
        public string IpAddress { get; set; } = "";
    }
}
