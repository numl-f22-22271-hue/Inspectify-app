using System;
using System.Collections.Generic;

namespace PC_inspect_beta.Core
{
    /// <summary>
    /// Cross-platform laptop price estimator. Pure heuristic rules — no ML.NET.
    /// Used as fallback on macOS/Linux where ML.NET (FastTree) doesn't run on ARM64.
    /// On Windows the more accurate <see cref="PricePredictor"/> is preferred.
    /// </summary>
    public static class PriceEstimator
    {
        /// <summary>
        /// Estimate a fair PKR asking price from scan metadata + condition.
        /// Returns 0 if there isn't enough info to estimate.
        /// </summary>
        public static int Estimate(Dictionary<string, object>? meta, string condition)
        {
            if (meta == null) return 0;

            // ── CPU base price ──────────────────────────────────────────────
            int cpuPrice = 40_000; // default fallback
            string cpu = (meta.TryGetValue("cpu_name", out var c) ? c?.ToString() : "")?.ToLower() ?? "";

            if      (cpu.Contains("i9")) cpuPrice = 130_000;
            else if (cpu.Contains("i7")) cpuPrice = 85_000;
            else if (cpu.Contains("i5")) cpuPrice = 55_000;
            else if (cpu.Contains("i3")) cpuPrice = 35_000;
            else if (cpu.Contains("ryzen 9")) cpuPrice = 125_000;
            else if (cpu.Contains("ryzen 7")) cpuPrice = 80_000;
            else if (cpu.Contains("ryzen 5")) cpuPrice = 55_000;
            else if (cpu.Contains("ryzen 3")) cpuPrice = 35_000;
            else if (cpu.Contains("apple m4")) cpuPrice = 200_000;
            else if (cpu.Contains("apple m3")) cpuPrice = 170_000;
            else if (cpu.Contains("apple m2")) cpuPrice = 140_000;
            else if (cpu.Contains("apple m1")) cpuPrice = 110_000;

            // ── CPU generation bump (Intel) ─────────────────────────────────
            // Detect generation token like "12th Gen" or "i7-13700"
            int gen = ExtractIntelGen(cpu);
            if (gen >= 12) cpuPrice += 20_000;
            else if (gen >= 10) cpuPrice += 10_000;
            else if (gen >= 8) cpuPrice += 5_000;
            else if (gen > 0 && gen < 6) cpuPrice -= 10_000;

            // ── RAM bonus ──────────────────────────────────────────────────
            int ramBonus = 0;
            if (meta.TryGetValue("ram_total_gb", out var r) && long.TryParse(r?.ToString(), out var ramGb))
            {
                if (ramGb >= 32) ramBonus = 25_000;
                else if (ramGb >= 16) ramBonus = 12_000;
                else if (ramGb >= 8)  ramBonus = 4_000;
                else if (ramGb >= 4)  ramBonus = 0;
                else                  ramBonus = -8_000;
            }

            // ── Storage bonus ──────────────────────────────────────────────
            int storageBonus = 0;
            string storageType = (meta.TryGetValue("storage_type", out var st) ? st?.ToString() : "")?.ToLower() ?? "";
            if (meta.TryGetValue("storage_total_gb", out var sg) && long.TryParse(sg?.ToString(), out var gb))
            {
                if (storageType.Contains("ssd") || storageType.Contains("nvme"))
                {
                    if (gb >= 1000) storageBonus = 15_000;
                    else if (gb >= 512) storageBonus = 8_000;
                    else if (gb >= 256) storageBonus = 3_000;
                }
                else if (gb >= 500)
                    storageBonus = 2_000;
            }

            // ── Battery health adjustment ─────────────────────────────────
            int batteryAdj = 0;
            if (meta.TryGetValue("battery_health", out var bh) && int.TryParse(bh?.ToString(), out var health))
            {
                if (health >= 90) batteryAdj = 5_000;
                else if (health >= 70) batteryAdj = 0;
                else if (health >= 50) batteryAdj = -5_000;
                else batteryAdj = -12_000;
            }

            int basePrice = cpuPrice + ramBonus + storageBonus + batteryAdj;

            // ── Condition multiplier ───────────────────────────────────────
            double conditionMultiplier = condition?.ToLower() switch
            {
                var s when s != null && s.Contains("10/10") => 1.00,
                var s when s != null && s.Contains("9/10")  => 0.92,
                var s when s != null && s.Contains("8/10")  => 0.82,
                var s when s != null && s.Contains("7/10")  => 0.72,
                var s when s != null && s.Contains("6/10")  => 0.60,
                var s when s != null && s.Contains("5/10")  => 0.48,
                _ => 0.85
            };

            int estimated = (int)(basePrice * conditionMultiplier);

            // Round to nearest thousand for cleaner display
            estimated = (estimated / 1000) * 1000;

            return Math.Max(estimated, 5_000);  // never show less than 5k
        }

        // Extract Intel generation from CPU string. e.g. "i7-12700H" -> 12, "10th Gen" -> 10
        private static int ExtractIntelGen(string cpu)
        {
            if (string.IsNullOrEmpty(cpu)) return 0;

            // Pattern 1: "i?-XXYYY" where XX is the gen (e.g., i7-12700)
            int dash = cpu.IndexOf('-');
            if (dash > 0 && dash + 4 < cpu.Length)
            {
                var s = cpu.Substring(dash + 1, Math.Min(2, cpu.Length - dash - 1));
                if (int.TryParse(s, out var g) && g > 0 && g < 20) return g;
            }

            // Pattern 2: "Nth Gen"
            for (int g = 14; g >= 1; g--)
                if (cpu.Contains($"{g}th gen") || cpu.Contains($"{g}st gen") ||
                    cpu.Contains($"{g}nd gen") || cpu.Contains($"{g}rd gen"))
                    return g;

            return 0;
        }
    }
}
