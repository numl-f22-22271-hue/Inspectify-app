using System;
using System.Runtime.InteropServices;

namespace PC_inspect_beta.Core.Platform
{
    public static class PlatformDetector
    {
        public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        public static bool IsMacOS   => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        public static bool IsLinux   => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        public static string PlatformName =>
            IsWindows ? "Windows" :
            IsMacOS   ? "macOS"   :
            IsLinux   ? "Linux"   :
            "Unknown";

        /// <summary>Returns a hardware scanner appropriate for the current OS.</summary>
        public static IHardwareScanner CreateScanner()
        {
#if WINDOWS
            if (IsWindows) return new WindowsHardwareScanner();
#endif
            if (IsMacOS) return new MacOSHardwareScanner();
            if (IsLinux) return new LinuxHardwareScanner();

            throw new PlatformNotSupportedException($"Platform '{PlatformName}' is not supported.");
        }
    }
}
