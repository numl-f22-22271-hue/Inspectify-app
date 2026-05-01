#nullable disable

using System.Collections.Generic;

namespace PC_inspect_beta.Core.Models
{
    /// <summary>
    /// Immutable data container produced by <see cref="DiagnosticsEngine"/>.
    /// Holds the full plain-text report and a structured metadata dictionary
    /// used by <see cref="UI.Forms.AdPostForm"/> when publishing a marketplace listing.
    /// </summary>
    public class ScanResult
    {
        /// <summary>Full formatted report text (shown in the scrollable viewer and exported to PDF).</summary>
        public string ReportText { get; set; } = string.Empty;

        /// <summary>
        /// Key/value pairs extracted during the scan (cpu_name, ram_total_gb, etc.).
        /// Keys follow snake_case convention so they map directly to Firebase JSON fields.
        /// </summary>
        public Dictionary<string, object> Metadata { get; } = new();
    }
}
