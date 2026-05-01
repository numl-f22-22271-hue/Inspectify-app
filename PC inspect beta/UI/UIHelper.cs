#nullable disable

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PC_inspect_beta.UI
{
    /// <summary>
    /// Factory methods plus custom modern WinForms controls
    /// (rounded buttons, rounded panels, gradient header) used across every form.
    /// </summary>
    public static class UIHelper
    {
        // ══════════════════════════════════════════════════════════════════════
        //  FACTORY METHODS
        // ══════════════════════════════════════════════════════════════════════

        // ── Button (rounded, modern flat) ─────────────────────────────────────
        public static Button CreateButton(string text, Color baseColor,
            Point loc, int w = 200, int h = 50)
        {
            return new RoundedButton
            {
                Text       = text,
                BaseColor  = baseColor,
                ForeColor  = Color.White,
                Font       = Theme.ButtonFont,
                Size       = new Size(w, h),
                Location   = loc,
                Cursor     = Cursors.Hand
            };
        }

        // ── Label ─────────────────────────────────────────────────────────────
        public static Label CreateLabel(string text, Point loc,
            Font font = null, Color? color = null)
        {
            return new Label
            {
                Text      = text,
                Location  = loc,
                Font      = font  ?? Theme.NormalFont,
                ForeColor = color ?? Theme.TextColor,
                AutoSize  = true,
                BackColor = Color.Transparent
            };
        }

        // ── TextBox (flat, light, with rounded soft border via host panel) ───
        public static TextBox CreateTextBox(Point loc, int width,
            bool readOnly = false, bool multiline = false,
            int height = 36, bool isPassword = false)
        {
            var tb = new TextBox
            {
                Location    = loc,
                Width       = width,
                BackColor   = Theme.InputBackground,
                ForeColor   = Theme.TextColor,
                BorderStyle = BorderStyle.FixedSingle,
                Font        = Theme.NormalFont,
                ReadOnly    = readOnly,
                Multiline   = multiline,
                Height      = multiline ? height : 36
            };
            if (isPassword) tb.PasswordChar = '●';
            return tb;
        }

        // ── Card panel (rounded, with subtle border) ──────────────────────────
        public static Panel CreateCard(Point loc, Size size, Color? bgColor = null)
        {
            return new RoundedPanel
            {
                Location     = loc,
                Size         = size,
                BackColor    = bgColor ?? Theme.CardBackground,
                BorderColor  = Theme.BorderColorSoft,
                CornerRadius = 14,
                Padding      = new Padding(10)
            };
        }

        // ── Separator (1px hairline) ──────────────────────────────────────────
        public static Label CreateSeparator(int y, int panelWidth)
        {
            return new Label
            {
                Location  = new Point(20, y),
                Size      = new Size(panelWidth - 40, 1),
                BackColor = Theme.BorderColorSoft,
                Text      = string.Empty
            };
        }

        // ── ProgressBar ───────────────────────────────────────────────────────
        public static ProgressBar CreateProgressBar(Point loc, int width, int height = 8)
        {
            return new ProgressBar
            {
                Location = loc,
                Size     = new Size(width, height),
                Minimum  = 0,
                Maximum  = 100,
                Value    = 0,
                Style    = ProgressBarStyle.Continuous
            };
        }

        // ── Gradient header band (window-wide title bar) ──────────────────────
        public static GradientPanel CreateGradientHeader(int width, int height = 110)
        {
            return new GradientPanel
            {
                Size           = new Size(width, height),
                Location       = new Point(0, 0),
                GradientStart  = Theme.GradientStart,
                GradientMid    = Theme.GradientMid,
                GradientEnd    = Theme.GradientEnd,
                Angle          = 0f
            };
        }

        // ══════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════════════════

        internal static GraphicsPath RoundedRectPath(Rectangle r, int radius)
        {
            var p = new GraphicsPath();
            int d = Math.Max(1, radius * 2);
            if (d > r.Width)  d = r.Width;
            if (d > r.Height) d = r.Height;

            p.AddArc(r.X,             r.Y,             d, d, 180, 90);
            p.AddArc(r.Right - d - 1, r.Y,             d, d, 270, 90);
            p.AddArc(r.Right - d - 1, r.Bottom - d - 1, d, d,   0, 90);
            p.AddArc(r.X,             r.Bottom - d - 1, d, d,  90, 90);
            p.CloseFigure();
            return p;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  CUSTOM CONTROLS
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Flat rounded button with hover-lighten and pressed-darken.</summary>
    public class RoundedButton : Button
    {
        public int   CornerRadius { get; set; } = 12;
        public Color BaseColor    { get; set; } = Theme.AccentColor;

        private bool _hover, _pressed;

        public RoundedButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            FlatAppearance.MouseOverBackColor = Color.Transparent;
            FlatAppearance.MouseDownBackColor = Color.Transparent;
            BackColor = Color.Transparent;
            ForeColor = Color.White;
            Font      = Theme.ButtonFont;
            Cursor    = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.UserPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.ResizeRedraw
                   | ControlStyles.SupportsTransparentBackColor, true);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover  = true;  Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover  = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown (MouseEventArgs e) { _pressed = true;  Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp   (MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.PixelOffsetMode   = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Color bg = BaseColor;
            if (!Enabled)   bg = Color.FromArgb(203, 213, 225); // slate-300 — light-theme disabled
            else if (_pressed) bg = ControlPaint.Dark(BaseColor, 0.10f);
            else if (_hover)   bg = ControlPaint.Light(BaseColor, 0.18f);

            var rect = new Rectangle(0, 0, Width, Height);
            using var path = UIHelper.RoundedRectPath(rect, CornerRadius);

            using (var brush = new SolidBrush(bg))
                g.FillPath(brush, path);

            // Subtle inner highlight on top — gives a gentle 3D feel
            if (Enabled && !_pressed)
            {
                using var hi = new LinearGradientBrush(
                    rect,
                    Color.FromArgb(38, 255, 255, 255),
                    Color.FromArgb(0,  255, 255, 255),
                    LinearGradientMode.Vertical);
                g.FillPath(hi, path);
            }

            // Text
            var fg = Enabled ? ForeColor : Color.FromArgb(100, 116, 139); // slate-500 for disabled label
            TextRenderer.DrawText(g, Text, Font, rect, fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    /// <summary>Rounded surface panel, useful as a card.</summary>
    public class RoundedPanel : Panel
    {
        public int   CornerRadius { get; set; } = 12;
        public Color BorderColor  { get; set; } = Theme.BorderColor;
        public int   BorderWidth  { get; set; } = 1;

        public RoundedPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.UserPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = UIHelper.RoundedRectPath(rect, CornerRadius);

            using (var brush = new SolidBrush(BackColor))
                g.FillPath(brush, path);

            if (BorderWidth > 0)
            {
                using var pen = new Pen(BorderColor, BorderWidth);
                g.DrawPath(pen, path);
            }
            base.OnPaint(e);
        }
    }

    /// <summary>Linear-gradient panel used for form headers.</summary>
    public class GradientPanel : Panel
    {
        public Color GradientStart { get; set; } = Theme.GradientStart;
        public Color GradientMid   { get; set; } = Theme.GradientMid;
        public Color GradientEnd   { get; set; } = Theme.GradientEnd;
        public float Angle         { get; set; } = 0f;

        public GradientPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.UserPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width <= 0 || Height <= 0) return;
            var rect = ClientRectangle;
            using var brush = new LinearGradientBrush(rect, GradientStart, GradientEnd, Angle);
            var blend = new ColorBlend
            {
                Colors    = new[] { GradientStart, GradientMid, GradientEnd },
                Positions = new[] { 0f, 0.5f, 1f }
            };
            brush.InterpolationColors = blend;
            e.Graphics.FillRectangle(brush, rect);
            base.OnPaint(e);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  MENU CARD — large clickable action tile with icon, title, subtitle
    //  (used on the main dashboard to match the mockup layout)
    // ══════════════════════════════════════════════════════════════════════════

    public class MenuCard : Control
    {
        public string Title       { get; set; } = "Title";
        public string Subtitle    { get; set; } = "Subtitle";
        public string IconGlyph   { get; set; } = "★";
        public Color  IconColor   { get; set; } = Theme.AccentColor;
        public Color  IconBgColor { get; set; } = Color.FromArgb(28, 37, 60);
        public int    CornerRadius { get; set; } = 14;

        private bool _hover, _pressed;

        public MenuCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.UserPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.ResizeRedraw
                   | ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            BackColor      = Color.Transparent;
            Cursor         = Cursors.Hand;
            Size           = new Size(420, 88);
            Font           = Theme.CardTitleFont;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover  = true;  Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover  = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown (MouseEventArgs e) { _pressed = true;  Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp   (MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); Cursor = Enabled ? Cursors.Hand : Cursors.Default; }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.PixelOffsetMode   = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // ── Card surface ───────────────────────────────────────────────
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = UIHelper.RoundedRectPath(rect, CornerRadius);

            Color surface;
            if (!Enabled)            surface = Color.FromArgb(12, 17, 30);
            else if (_pressed)       surface = Color.FromArgb(22, 32, 56);
            else if (_hover)         surface = Color.FromArgb(28, 39, 66);
            else                     surface = Color.FromArgb(15, 22, 40);

            using (var b = new SolidBrush(surface)) g.FillPath(b, path);

            // Subtle border — brighter on hover, softer when disabled
            Color border = !Enabled
                ? Color.FromArgb(22, 30, 50)
                : _hover ? Color.FromArgb(70, 85, 130) : Color.FromArgb(40, 52, 82);
            using (var pen = new Pen(border, 1f)) g.DrawPath(pen, path);

            // ── Icon square (left-padded) ──────────────────────────────────
            const int pad      = 18;
            const int iconSize = 48;
            var iconRect = new Rectangle(pad, (Height - iconSize) / 2, iconSize, iconSize);
            using var iconPath = UIHelper.RoundedRectPath(iconRect, 10);

            Color iconBg = Enabled
                ? IconBgColor
                : Color.FromArgb(20, 26, 42);
            using (var b = new SolidBrush(iconBg)) g.FillPath(b, iconPath);

            Color iconStroke = Enabled
                ? Color.FromArgb(60, IconColor)
                : Color.FromArgb(30, 40, 65);
            using (var pen = new Pen(iconStroke, 1f)) g.DrawPath(pen, iconPath);

            // Icon glyph (Unicode symbol, centred)
            using var iconFont = new Font("Segoe UI Symbol", 20, FontStyle.Regular);
            var iconFg = Enabled ? IconColor : Color.FromArgb(80, 90, 115);
            TextRenderer.DrawText(g, IconGlyph, iconFont, iconRect, iconFg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            // ── Text block ─────────────────────────────────────────────────
            int textX = iconRect.Right + 16;
            int textW = Width - textX - pad;

            Color titleFg = Enabled ? Theme.TextColor : Color.FromArgb(110, 125, 150);
            Color subFg   = Enabled ? Theme.MutedText : Color.FromArgb( 80,  95, 120);

            var titleRect = new Rectangle(textX, 16, textW, 26);
            TextRenderer.DrawText(g, Title, Theme.CardTitleFont, titleRect, titleFg,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);

            var subRect = new Rectangle(textX, 44, textW, 22);
            TextRenderer.DrawText(g, Subtitle, Theme.CardSubFont, subRect, subFg,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  PILL — small rounded badge for status indicators (e.g., "v2.4.1 · Live")
    // ══════════════════════════════════════════════════════════════════════════

    public class PillPanel : Control
    {
        public Color  FillColor   { get; set; } = Color.FromArgb(24, 32, 54);
        public Color  BorderColor { get; set; } = Color.FromArgb(44, 56, 86);
        public int    CornerRadius { get; set; } = 14;

        public PillPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.UserPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.ResizeRedraw
                   | ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            BackColor      = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = UIHelper.RoundedRectPath(rect, CornerRadius);
            using (var b = new SolidBrush(FillColor)) g.FillPath(b, path);
            using (var p = new Pen(BorderColor, 1f)) g.DrawPath(p, path);
            base.OnPaint(e);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  LOGO BADGE — rounded square with a brand-tinted icon glyph
    // ══════════════════════════════════════════════════════════════════════════

    public class LogoBadge : Control
    {
        public string Glyph { get; set; } = "💻";
        public Color  AccentColor { get; set; } = Theme.AccentColor;

        public LogoBadge()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.UserPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.ResizeRedraw
                   | ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            BackColor      = Color.Transparent;
            Size           = new Size(52, 52);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = UIHelper.RoundedRectPath(rect, 14);

            using (var b = new SolidBrush(Color.FromArgb(22, 30, 52))) g.FillPath(b, path);
            using (var pen = new Pen(Color.FromArgb(120, AccentColor), 1.6f)) g.DrawPath(pen, path);

            using var iconFont = new Font("Segoe UI Symbol", 18, FontStyle.Regular);
            TextRenderer.DrawText(g, Glyph, iconFont, rect, AccentColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }
}
