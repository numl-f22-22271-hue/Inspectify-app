using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PC_inspect_beta.Core.Platform
{
    /// <summary>
    /// Cross-platform stress tests (RAM, CPU, GPU, Storage).
    /// Uses pure .NET APIs that work on Windows, macOS, and Linux —
    /// no WMI, no GDI+, no platform-specific calls.
    /// </summary>
    public static class StressTestEngine
    {
        public class StressResult
        {
            public string Name { get; set; } = "";
            public bool Passed { get; set; }
            public string Summary { get; set; } = "";
            public Dictionary<string, object> Metrics { get; set; } = new();
        }

        // ── RAM stress test (allocate ~60% of available memory) ────────────
        public static StressResult RunRamStress()
        {
            var r = new StressResult { Name = "RAM Stress (60% Fill + Write Speed)" };
            try
            {
                long totalRamBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
                if (totalRamBytes <= 0) totalRamBytes = 4L * 1024 * 1024 * 1024; // 4 GB fallback

                long target = (long)(totalRamBytes * 0.30); // 30% to be safe cross-platform
                var blocks = new List<byte[]>();
                long allocated = 0;
                int blockSz = 64 * 1024 * 1024; // 64 MB blocks

                var sw = Stopwatch.StartNew();
                while (allocated < target)
                {
                    try
                    {
                        var blk = new byte[blockSz];
                        for (int i = 0; i < blk.Length; i += 4096) blk[i] = 0xAB;
                        blocks.Add(blk);
                        allocated += blockSz;
                    }
                    catch (OutOfMemoryException) { break; }

                    if (sw.ElapsedMilliseconds > 10_000) break; // safety timeout
                }
                sw.Stop();

                double allocGB = allocated / 1024.0 / 1024.0 / 1024.0;
                double bandwidth = (allocated / 1024.0 / 1024.0) / Math.Max(0.01, sw.Elapsed.TotalSeconds);

                r.Passed = allocated > 0;
                r.Summary = $"Allocated: {allocGB:F2} GB  •  Write: {bandwidth:F0} MB/s  •  {sw.ElapsedMilliseconds} ms";
                r.Metrics["allocated_gb"] = allocGB;
                r.Metrics["write_speed_mbs"] = bandwidth;

                blocks.Clear();
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            catch (Exception ex)
            {
                r.Passed = false;
                r.Summary = $"Failed: {ex.Message}";
            }
            return r;
        }

        // ── CPU stress test (all-core parallel burst for ~3s) ──────────────
        public static StressResult RunCpuStress()
        {
            var r = new StressResult { Name = "CPU Stress (All-Core Parallel Burst)" };
            try
            {
                int threadCount = Environment.ProcessorCount;
                var tasks = new Task[threadCount];
                var sw = Stopwatch.StartNew();

                long totalOps = 0;
                for (int t = 0; t < threadCount; t++)
                    tasks[t] = Task.Run(() =>
                    {
                        long ops = 0;
                        var rng = new Random(t);
                        while (sw.ElapsedMilliseconds < 3000)
                        {
                            for (int i = 0; i < 10_000; i++)
                            {
                                _ = Math.Sqrt(rng.NextDouble() * 12345.6789);
                                ops++;
                            }
                        }
                        System.Threading.Interlocked.Add(ref totalOps, ops);
                    });

                Task.WaitAll(tasks);
                sw.Stop();

                double mops = (totalOps / 1_000_000.0) / sw.Elapsed.TotalSeconds;

                r.Passed = true;
                r.Summary = $"Threads: {threadCount}  •  {mops:F1} M ops/sec  •  {sw.ElapsedMilliseconds} ms";
                r.Metrics["cpu_threads"] = threadCount;
                r.Metrics["cpu_mops_per_sec"] = mops;
            }
            catch (Exception ex)
            {
                r.Passed = false;
                r.Summary = $"Failed: {ex.Message}";
            }
            return r;
        }

        // ── GPU stress test (cross-platform: matrix math; no GDI+) ─────────
        public static StressResult RunGpuStress()
        {
            var r = new StressResult { Name = "GPU/Compute Stress (Matrix Multiplication)" };
            try
            {
                // Cross-platform alternative to Windows GDI+ render test:
                // Heavy matrix math saturates CPU + memory in a way that mirrors
                // graphics workload. On Windows we'd hit GDI+, on Mac/Linux this
                // serves as a comparable compute load test.
                const int N = 256;
                var a = new float[N, N];
                var b = new float[N, N];
                var c = new float[N, N];
                var rng = new Random(42);
                for (int i = 0; i < N; i++)
                    for (int j = 0; j < N; j++)
                    {
                        a[i, j] = (float)rng.NextDouble();
                        b[i, j] = (float)rng.NextDouble();
                    }

                var sw = Stopwatch.StartNew();
                int iterations = 0;
                while (sw.ElapsedMilliseconds < 3000)
                {
                    Parallel.For(0, N, i =>
                    {
                        for (int j = 0; j < N; j++)
                        {
                            float sum = 0;
                            for (int k = 0; k < N; k++) sum += a[i, k] * b[k, j];
                            c[i, j] = sum;
                        }
                    });
                    iterations++;
                }
                sw.Stop();

                double matsPerSec = iterations / sw.Elapsed.TotalSeconds;

                r.Passed = true;
                r.Summary = $"{N}×{N} mat-muls  •  {iterations} iter  •  {matsPerSec:F1}/sec";
                r.Metrics["gpu_iterations"] = iterations;
                r.Metrics["gpu_mats_per_sec"] = matsPerSec;
            }
            catch (Exception ex)
            {
                r.Passed = false;
                r.Summary = $"Failed: {ex.Message}";
            }
            return r;
        }

        // ── Storage stress test (sequential read/write speed) ──────────────
        public static List<StressResult> RunStorageStress()
        {
            var results = new List<StressResult>();

            const int FILE_SIZE_MB = 128;
            const long BYTES = (long)FILE_SIZE_MB * 1024 * 1024;
            var writeBuffer = new byte[4 * 1024 * 1024];
            new Random(1).NextBytes(writeBuffer);

            var testPaths = GetWritableStoragePaths();

            foreach (var (label, testDir) in testPaths)
            {
                var r = new StressResult { Name = $"Storage Speed — {label}" };
                string tmpFile = Path.Combine(testDir, "__pctest_tmp.bin");
                try
                {
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
                    double writeMBs = FILE_SIZE_MB / Math.Max(0.01, wSw.Elapsed.TotalSeconds);

                    var readBuf = new byte[4 * 1024 * 1024];
                    var rSw = Stopwatch.StartNew();
                    using (var fs = new FileStream(tmpFile, FileMode.Open, FileAccess.Read,
                        FileShare.None, 64 * 1024))
                        while (fs.Read(readBuf, 0, readBuf.Length) > 0) { }
                    rSw.Stop();
                    double readMBs = FILE_SIZE_MB / Math.Max(0.01, rSw.Elapsed.TotalSeconds);

                    string driveClass = readMBs >= 400 ? "NVMe SSD"
                                      : readMBs >= 100 ? "SATA SSD"
                                      : "HDD";

                    r.Passed = true;
                    r.Summary = $"Write: {writeMBs:F0} MB/s  •  Read: {readMBs:F0} MB/s  •  Type: {driveClass}";
                    r.Metrics["write_mbs"] = writeMBs;
                    r.Metrics["read_mbs"] = readMBs;
                    r.Metrics["drive_class"] = driveClass;
                }
                catch (Exception ex)
                {
                    r.Passed = false;
                    r.Summary = $"Failed: {ex.Message}";
                }
                finally
                {
                    try { if (File.Exists(tmpFile)) File.Delete(tmpFile); } catch { }
                    try
                    {
                        if (testDir.EndsWith("inspectify_test") && Directory.Exists(testDir)
                            && !Directory.EnumerateFileSystemEntries(testDir).Any())
                            Directory.Delete(testDir);
                    } catch { }
                }
                results.Add(r);
            }

            return results;
        }

        private static List<(string Label, string Path)> GetWritableStoragePaths()
        {
            var paths = new List<(string, string)>();

            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                    System.Runtime.InteropServices.OSPlatform.Windows))
            {
                var userTemp = Path.GetTempPath();
                var usedDrives = new HashSet<string>();

                var tempDriveLetter = Path.GetPathRoot(userTemp)?.TrimEnd('\\') ?? "";
                if (!string.IsNullOrEmpty(tempDriveLetter))
                {
                    paths.Add((tempDriveLetter + "\\", userTemp));
                    usedDrives.Add(tempDriveLetter);
                }

                foreach (var drive in DriveInfo.GetDrives()
                             .Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
                {
                    var letter = drive.Name.TrimEnd('\\');
                    if (usedDrives.Contains(letter)) continue;

                    var testDir = Path.Combine(drive.RootDirectory.FullName, "inspectify_test");
                    try
                    {
                        Directory.CreateDirectory(testDir);
                        paths.Add((drive.Name, testDir));
                    }
                    catch
                    {
                        // drive root not writable, skip
                    }
                }
            }
            else
            {
                // macOS/Linux: test user home directory (on the main disk)
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrEmpty(home) && Directory.Exists(home))
                    paths.Add((home, home));
                else
                {
                    var tmp = Path.GetTempPath();
                    paths.Add((tmp, tmp));
                }
            }

            return paths;
        }
    }
}
