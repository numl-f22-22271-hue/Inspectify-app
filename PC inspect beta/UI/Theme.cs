#nullable disable

using System.Drawing;

namespace PC_inspect_beta.UI
{
    /// <summary>
    /// Polished dark-theme palette — navy/slate surfaces with the
    /// Inspectify indigo → violet → pink accent line. Every form references
    /// only this class, so the whole app rethemes at once.
    /// </summary>
    public static class Theme
    {
        // ── Backgrounds ───────────────────────────────────────────────────────
        public static readonly Color DarkBackground   = Color.FromArgb(  8,  11,  22); // app shell — near-black
        public static readonly Color PanelBackground  = Color.FromArgb( 14,  20,  37); // main panel
        public static readonly Color CardBackground   = Color.FromArgb( 18,  25,  45); // cards
        public static readonly Color InputBackground  = Color.FromArgb( 24,  32,  54); // text fields
        public static readonly Color HoverBackground  = Color.FromArgb( 30,  41,  69); // hover highlight

        // ── Text ──────────────────────────────────────────────────────────────
        public static readonly Color TextColor  = Color.FromArgb(248, 250, 252); // slate-50
        public static readonly Color MutedText  = Color.FromArgb(148, 163, 184); // slate-400
        public static readonly Color SubtleText = Color.FromArgb( 94, 108, 133); // slate-500 dim

        // ── Accents (Inspectify brand line: indigo → violet → pink) ───────────
        public static readonly Color AccentColor   = Color.FromArgb( 99, 102, 241); // indigo-500
        public static readonly Color SuccessColor  = Color.FromArgb( 16, 185, 129); // emerald-500
        public static readonly Color ErrorColor    = Color.FromArgb(239,  68,  68); // red-500
        public static readonly Color WarningColor  = Color.FromArgb(245, 158,  11); // amber-500
        public static readonly Color PurpleAccent  = Color.FromArgb(139,  92, 246); // violet-500
        public static readonly Color CyanAccent    = Color.FromArgb( 34, 211, 238); // cyan-400
        public static readonly Color PinkAccent    = Color.FromArgb(236,  72, 153); // pink-500

        // ── Gradient stops (matches website --gradient) ───────────────────────
        public static readonly Color GradientStart = Color.FromArgb( 79,  70, 229); // indigo-600
        public static readonly Color GradientMid   = Color.FromArgb(124,  58, 237); // violet-600
        public static readonly Color GradientEnd   = Color.FromArgb(236,  72, 153); // pink-500

        // ── Borders ───────────────────────────────────────────────────────────
        public static readonly Color BorderColor      = Color.FromArgb( 42,  53,  80); // visible stroke
        public static readonly Color BorderColorSoft  = Color.FromArgb( 28,  37,  60); // hairline

        // ── Fonts ─────────────────────────────────────────────────────────────
        public static readonly Font HeaderFont    = new Font("Segoe UI Semibold", 22, FontStyle.Bold);
        public static readonly Font TitleFont     = new Font("Segoe UI Semibold", 14, FontStyle.Bold);
        public static readonly Font SubTitleFont  = new Font("Segoe UI", 11, FontStyle.Regular);
        public static readonly Font NormalFont    = new Font("Segoe UI", 10, FontStyle.Regular);
        public static readonly Font SmallFont     = new Font("Segoe UI",  9, FontStyle.Regular);
        public static readonly Font ButtonFont    = new Font("Segoe UI Semibold", 11, FontStyle.Bold);
        public static readonly Font BigButtonFont = new Font("Segoe UI Semibold", 12, FontStyle.Bold);
        public static readonly Font MonoFont      = new Font("Consolas",  9, FontStyle.Regular);

        // Card-specific fonts (new for mockup layout)
        public static readonly Font CardTitleFont = new Font("Segoe UI Semibold", 14, FontStyle.Bold);
        public static readonly Font CardSubFont   = new Font("Segoe UI", 10, FontStyle.Regular);
        public static readonly Font CardIconFont  = new Font("Segoe UI Symbol", 18, FontStyle.Regular);
        public static readonly Font BrandFont     = new Font("Segoe UI Semibold", 16, FontStyle.Bold);
        public static readonly Font PillFont      = new Font("Segoe UI Semibold",  9, FontStyle.Bold);
        public static readonly Font StatusFont    = new Font("Consolas", 11, FontStyle.Regular);
    }
}
