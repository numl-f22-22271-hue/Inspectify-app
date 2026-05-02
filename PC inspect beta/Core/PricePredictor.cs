#nullable disable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace PC_inspect_beta.Core
{
    /// <summary>
    /// Laptop price prediction engine, trained in-process from
    /// <c>Data/laptop_dataset.csv</c> using an ML.NET FastTree regression pipeline.
    /// Model is trained lazily on first use and cached for the life of the process.
    /// The predicted price is consumed silently by <see cref="UI.Forms.AdPostForm"/>
    /// and persisted to MongoDB — it is never surfaced in reports or UI.
    /// </summary>
    public static class PricePredictor
    {
        // ── ML.NET record types ───────────────────────────────────────────────

        public class LaptopRecord
        {
            [LoadColumn(0)] public string Brand { get; set; }
            [LoadColumn(1)] public string Model { get; set; }
            [LoadColumn(2)] public string Processor { get; set; }
            [LoadColumn(3)] public string Generation { get; set; }
            [LoadColumn(4)] public float Ram { get; set; }
            [LoadColumn(5)] public float Ssd { get; set; }
            [LoadColumn(6)] public float Hdd { get; set; }
            [LoadColumn(7)] public string Condition { get; set; }
            [LoadColumn(8), ColumnName("Label")] public float Price { get; set; }
        }

        public class PricePrediction
        {
            [ColumnName("Score")] public float Price { get; set; }
        }

        // ── State ─────────────────────────────────────────────────────────────

        private static readonly object _lock = new();
        private static MLContext _ml;
        private static PredictionEngine<LaptopRecord, PricePrediction> _engine;
        private static bool _trainingFailed;
        private static string _lastError;

        /// <summary>True once the model has been successfully trained.</summary>
        public static bool IsReady => _engine != null;

        /// <summary>Last training error (for diagnostics only — never shown to end user).</summary>
        public static string LastError => _lastError;

        // ══════════════════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Predicts a price (PKR) for the scanned machine. Returns 0 if the model
        /// could not be trained (e.g. dataset missing). Never throws.
        /// </summary>
        public static float Predict(Dictionary<string, object> scanMeta, string condition)
        {
            try
            {
                EnsureTrained();
                if (_engine == null) return 0f;

                var record = BuildFeatures(scanMeta, condition);
                var pred   = _engine.Predict(record);
                float price = pred.Price;
                if (float.IsNaN(price) || float.IsInfinity(price) || price < 0f) return 0f;
                return (float)Math.Round(price);
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                return 0f;
            }
        }

        /// <summary>
        /// Trains the model once (idempotent). Safe to call from any thread.
        /// Kept public so Form1 or Program can warm the model at startup if desired.
        /// </summary>
        public static void EnsureTrained()
        {
            if (_engine != null || _trainingFailed) return;
            lock (_lock)
            {
                if (_engine != null || _trainingFailed) return;
                try
                {
                    Train();
                }
                catch (Exception ex)
                {
                    _trainingFailed = true;
                    _lastError = ex.Message;
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  TRAINING
        // ══════════════════════════════════════════════════════════════════════

        private static void Train()
        {
            string csv = ResolveDatasetPath()
                      ?? ExtractEmbeddedCsv()
                      ?? throw new FileNotFoundException("Training dataset not found.");

            _ml = new MLContext(seed: 42);

            IDataView data = _ml.Data.LoadFromTextFile<LaptopRecord>(
                path: csv,
                hasHeader: true,
                separatorChar: ',',
                allowQuoting: true,
                trimWhitespace: true);

            var pipeline = _ml.Transforms.Categorical.OneHotEncoding(new[]
                {
                    new InputOutputColumnPair("BrandEnc",      nameof(LaptopRecord.Brand)),
                    new InputOutputColumnPair("ModelEnc",      nameof(LaptopRecord.Model)),
                    new InputOutputColumnPair("ProcessorEnc",  nameof(LaptopRecord.Processor)),
                    new InputOutputColumnPair("GenerationEnc", nameof(LaptopRecord.Generation)),
                    new InputOutputColumnPair("ConditionEnc",  nameof(LaptopRecord.Condition)),
                })
                .Append(_ml.Transforms.Concatenate("Features",
                    "BrandEnc", "ModelEnc", "ProcessorEnc", "GenerationEnc", "ConditionEnc",
                    nameof(LaptopRecord.Ram), nameof(LaptopRecord.Ssd), nameof(LaptopRecord.Hdd)))
                .Append(_ml.Regression.Trainers.FastTree(
                    labelColumnName: "Label",
                    featureColumnName: "Features",
                    numberOfLeaves: 32,
                    numberOfTrees: 200,
                    minimumExampleCountPerLeaf: 4,
                    learningRate: 0.12));

            ITransformer model = pipeline.Fit(data);
            _engine = _ml.Model.CreatePredictionEngine<LaptopRecord, PricePrediction>(model);
        }

        private static string ResolveDatasetPath()
        {
            // Used during development when the CSV sits next to the build output
            string[] candidates =
            {
                Path.Combine(AppContext.BaseDirectory, "Data", "laptop_dataset.csv"),
                Path.Combine(Environment.CurrentDirectory, "Data", "laptop_dataset.csv"),
                Path.Combine(AppContext.BaseDirectory, "laptop_dataset.csv"),
            };
            foreach (var p in candidates)
                if (File.Exists(p)) return p;
            return null;
        }

        private static string _tempCsvPath;

        // Extracts the embedded CSV to a temp file so ML.NET's file-based loader can read it.
        // Called only in single-file publish mode where the CSV has no physical side-car path.
        private static string ExtractEmbeddedCsv()
        {
            if (_tempCsvPath != null && File.Exists(_tempCsvPath))
                return _tempCsvPath;

            using var stream = typeof(PricePredictor).Assembly
                .GetManifestResourceStream("PC_inspect_beta.Data.laptop_dataset.csv");
            if (stream == null) return null;

            _tempCsvPath = Path.Combine(Path.GetTempPath(), "inspectify_laptop_dataset.csv");
            using var fs = new FileStream(_tempCsvPath, FileMode.Create, FileAccess.Write, FileShare.None);
            stream.CopyTo(fs);
            return _tempCsvPath;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  FEATURE EXTRACTION  —  ScanResult metadata  →  LaptopRecord
        // ══════════════════════════════════════════════════════════════════════

        private static LaptopRecord BuildFeatures(Dictionary<string, object> meta, string formCondition)
        {
            string cpuName = GetString(meta, "cpu_name");
            string board   = GetString(meta, "motherboard");
            float  ramGb   = RoundRam(GetFloat(meta, "ram_total_gb"));
            float  ssdGb   = GetFloat(meta, "ssd_total_gb");
            float  hddGb   = GetFloat(meta, "hdd_total_gb");

            return new LaptopRecord
            {
                Brand      = DetectBrand(board, cpuName),
                Model      = DetectModel(board),
                Processor  = DetectProcessor(cpuName),
                Generation = DetectGeneration(cpuName),
                Ram        = ramGb,
                Ssd        = ssdGb,
                Hdd        = hddGb,
                Condition  = MapCondition(formCondition),
            };
        }

        // ── Brand ─────────────────────────────────────────────────────────────
        private static string DetectBrand(string board, string cpu)
        {
            string b = (board ?? "").ToLowerInvariant();
            string c = (cpu ?? "").ToLowerInvariant();

            if (b.Contains("apple") || c.Contains("apple m")) return "Apple";
            if (b.Contains("microsoft")) return "Microsoft";
            if (b.Contains("hewlett") || b.Contains("hp ")   || b.StartsWith("hp")) return "HP";
            if (b.Contains("dell")) return "Dell";
            if (b.Contains("lenovo")) return "Lenovo";
            if (b.Contains("asus") || b.Contains("asustek")) return "Asus";
            if (b.Contains("acer")) return "Acer";
            if (b.Contains("msi") || b.Contains("micro-star")) return "MSI";

            return "HP"; // benign fallback (most common in dataset)
        }

        // ── Model (family) ────────────────────────────────────────────────────
        // Best-effort map from motherboard product string to a CSV-known model.
        private static readonly (string Needle, string Model)[] _modelMap =
        {
            ("elitebook 840",   "EliteBook 840 G5"),
            ("elitebook",       "EliteBook 840 G5"),
            ("probook 450",     "ProBook 450 G6"),
            ("probook",         "ProBook 450 G6"),
            ("zbook 15",        "ZBook 15"),
            ("zbook",           "ZBook 15"),
            ("pavilion 15",     "Pavilion 15"),
            ("pavilion",        "Pavilion 15"),
            ("spectre",         "Spectre x360"),
            ("nitro",           "Nitro 5"),
            ("swift 3",         "Swift 3"),
            ("swift",           "Swift 3"),
            ("aspire",          "Aspire 5"),
            ("latitude 7480",   "Latitude 7480"),
            ("latitude 5400",   "Latitude 5400"),
            ("latitude",        "Latitude 7480"),
            ("inspiron",        "Inspiron 15"),
            ("precision",       "Precision 5520"),
            ("xps",             "XPS 13"),
            ("thinkpad x1",     "ThinkPad X1 Carbon"),
            ("thinkpad l480",   "ThinkPad L480"),
            ("thinkpad",        "ThinkPad L480"),
            ("vivobook",        "VivoBook 15"),
            ("gf63",            "GF63 Thin"),
            ("prestige",        "Prestige 15"),
            ("modern 14",       "Modern 14"),
            ("surface book",    "Surface Book 2"),
            ("surface pro",     "Surface Pro 7"),
            ("surface laptop",  "Surface Laptop 3"),
            ("macbook air m1",  "MacBook Air M1"),
            ("macbook air",     "MacBook Air 2017"),
            ("macbook pro m2",  "MacBook Pro M2"),
            ("macbook pro",     "MacBook Pro M2"),
            ("macbook",         "MacBook Air 2017"),
        };

        private static string DetectModel(string board)
        {
            string b = (board ?? "").ToLowerInvariant();
            foreach (var (needle, model) in _modelMap)
                if (b.Contains(needle)) return model;
            return "Pavilion 15"; // mid-range fallback
        }

        // ── Processor family ──────────────────────────────────────────────────
        private static string DetectProcessor(string cpuName)
        {
            string c = (cpuName ?? "").ToLowerInvariant();

            if (Regex.IsMatch(c, @"\bm1\b"))          return "M1";
            if (Regex.IsMatch(c, @"\bm2\b"))          return "M2";
            if (c.Contains("ryzen 3"))                return "Ryzen 3";
            if (c.Contains("ryzen 5"))                return "Ryzen 5";
            if (c.Contains("ryzen 7"))                return "Ryzen 7";
            if (c.Contains("ryzen 9"))                return "Ryzen 7"; // nearest in dataset
            if (c.Contains("celeron"))                return "Celeron";
            if (c.Contains("pentium"))                return "Celeron"; // nearest in dataset
            if (c.Contains(" i3") || c.Contains("-i3") || c.Contains("core(tm) i3") || c.Contains("core i3")) return "Core i3";
            if (c.Contains(" i5") || c.Contains("-i5") || c.Contains("core(tm) i5") || c.Contains("core i5")) return "Core i5";
            if (c.Contains(" i7") || c.Contains("-i7") || c.Contains("core(tm) i7") || c.Contains("core i7")) return "Core i7";
            if (c.Contains(" i9") || c.Contains("-i9") || c.Contains("core(tm) i9") || c.Contains("core i9")) return "Core i7"; // nearest

            return "Core i5";
        }

        // ── Generation ────────────────────────────────────────────────────────
        private static string DetectGeneration(string cpuName)
        {
            string c = (cpuName ?? "").ToLowerInvariant();

            if (Regex.IsMatch(c, @"\bm[12]\b")) return "N/A";

            // Intel Core ix-YYYY[letters] — leading digits of YYYY denote gen.
            var mi = Regex.Match(c, @"i[3579]-(\d{4,5})");
            if (mi.Success)
            {
                string num = mi.Groups[1].Value;
                int g = num.Length == 5 ? int.Parse(num.Substring(0, 2)) : int.Parse(num.Substring(0, 1));
                return ClampGen(g);
            }

            // Ryzen N XXXX — first digit of XXXX is series generation.
            var mr = Regex.Match(c, @"ryzen\s+\d+\s+(\d)(\d{3})");
            if (mr.Success)
            {
                int g = int.Parse(mr.Groups[1].Value);
                return ClampGen(g);
            }

            // Intel "10th Gen" markers sometimes appear as Nxxxx for Celeron etc.
            if (c.Contains("celeron")) return "10th Gen";

            return "8th Gen";
        }

        private static string ClampGen(int g)
        {
            if (g < 6) g = 6;
            if (g > 11) g = 11;
            return $"{g}th Gen";
        }

        // ── Condition mapping (form → CSV vocabulary) ─────────────────────────
        private static string MapCondition(string formCondition)
        {
            string c = (formCondition ?? "").Trim().ToLowerInvariant();
            return c switch
            {
                "new"       => "Like New",
                "like new"  => "Like New",
                "used"      => "Slightly Used",
                "for parts" => "B-Grade",
                _           => "Slightly Used",
            };
        }

        // ── RAM rounding ──────────────────────────────────────────────────────
        private static float RoundRam(float gb)
        {
            float[] buckets = { 4, 8, 16, 32, 64 };
            float best = buckets[0];
            float bestDiff = Math.Abs(gb - best);
            foreach (var b in buckets)
            {
                float d = Math.Abs(gb - b);
                if (d < bestDiff) { best = b; bestDiff = d; }
            }
            return best;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  PRODUCT TITLE  (Brand + Model + iX + Nth Gen)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Builds a clean marketplace title from scan metadata, e.g.
        /// "DELL Vostro i7 7th Gen" or "HP i5 8th Gen".
        /// </summary>
        public static string BuildProductTitle(Dictionary<string, object> meta)
        {
            string cpu   = GetString(meta, "cpu_name");
            string board = GetString(meta, "motherboard");

            string brand    = DetectBrand(board, cpu).ToUpperInvariant();
            string model    = DetectDisplayModel(board);
            string proc     = DetectProcessor(cpu);
            string gen      = DetectGeneration(cpu);

            // "Core i7" → "i7", "Ryzen 7" stays.  Apple M1/M2 stay.
            string procShort = proc.StartsWith("Core ", StringComparison.OrdinalIgnoreCase)
                ? proc.Substring(5)
                : proc;

            var parts = new List<string> { brand };
            if (!string.IsNullOrWhiteSpace(model)) parts.Add(model);
            parts.Add(procShort);
            if (!string.IsNullOrEmpty(gen) && gen != "N/A") parts.Add(gen);

            return string.Join(" ", parts);
        }

        // Display-only model family detection — returns empty when unknown
        // so the title doesn't include a misleading guess.
        private static string DetectDisplayModel(string board)
        {
            string b = (board ?? "").ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(b)) return "";

            (string Needle, string Display)[] map =
            {
                ("vostro",        "Vostro"),
                ("xps",           "XPS"),
                ("inspiron",      "Inspiron"),
                ("latitude",      "Latitude"),
                ("precision",     "Precision"),
                ("alienware",     "Alienware"),

                ("elitebook",     "EliteBook"),
                ("probook",       "ProBook"),
                ("zbook",         "ZBook"),
                ("pavilion",      "Pavilion"),
                ("spectre",       "Spectre"),
                ("envy",          "Envy"),
                ("omen",          "Omen"),

                ("thinkpad",      "ThinkPad"),
                ("ideapad",       "IdeaPad"),
                ("yoga",          "Yoga"),
                ("legion",        "Legion"),

                ("vivobook",      "VivoBook"),
                ("zenbook",       "ZenBook"),
                ("rog",           "ROG"),
                ("tuf",           "TUF"),

                ("nitro",         "Nitro"),
                ("aspire",        "Aspire"),
                ("swift",         "Swift"),
                ("predator",      "Predator"),

                ("gf63",          "GF63 Thin"),
                ("prestige",      "Prestige"),
                ("modern",        "Modern"),

                ("surface book",  "Surface Book"),
                ("surface pro",   "Surface Pro"),
                ("surface laptop","Surface Laptop"),

                ("macbook air",   "MacBook Air"),
                ("macbook pro",   "MacBook Pro"),
                ("macbook",       "MacBook"),
            };

            foreach (var (needle, display) in map)
                if (b.Contains(needle)) return display;
            return "";
        }

        // ── Dictionary accessors ──────────────────────────────────────────────
        private static string GetString(Dictionary<string, object> meta, string key)
            => meta != null && meta.TryGetValue(key, out var v) && v != null ? v.ToString() : "";

        private static float GetFloat(Dictionary<string, object> meta, string key)
        {
            if (meta == null || !meta.TryGetValue(key, out var v) || v == null) return 0f;
            if (v is float f)  return f;
            if (v is double d) return (float)d;
            if (v is int i)    return i;
            if (v is long l)   return l;
            if (float.TryParse(v.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
                return parsed;
            return 0f;
        }
    }
}
