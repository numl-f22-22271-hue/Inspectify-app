#nullable disable

using System;
using System.Drawing;
using System.Management;
using System.Windows.Forms;
using PC_inspect_beta.Config;
using PC_inspect_beta.Core;
using PC_inspect_beta.Core.Models;

namespace PC_inspect_beta.UI.Forms
{
    public partial class Form1 : Form
    {
        private ScanResult _lastScan;

        private Panel    _mainPanel;
        private Panel    _statusBar;
        private Label    _lblStatus;
        private Label    _lblStatusDot;
        private ProgressBar _progressBar;
        private Label    _lblProgress;

        private MenuCard _cardStart, _cardStop, _cardDownload, _cardSubmit,
                         _cardKeyboard, _cardTouchpad;

        public Form1()
        {
            InitializeComponent();
            BuildUI();
        }

        // ══════════════════════════════════════════════════════════════════════
        // UI CONSTRUCTION — dashboard layout (logo header · 2×3 cards · status bar)
        // ══════════════════════════════════════════════════════════════════════

        private void BuildUI()
        {
            this.Controls.Clear();
            this.Text            = AppConfig.AppName;
            this.WindowState     = FormWindowState.Maximized;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.BackColor       = Theme.DarkBackground;
            this.ForeColor       = Theme.TextColor;
            this.Font            = Theme.NormalFont;
            this.MinimumSize     = new Size(1120, 680);

            const int PW = 1100, PH = 680;
            _mainPanel = new RoundedPanel
            {
                Size         = new Size(PW, PH),
                BackColor    = Theme.PanelBackground,
                BorderColor  = Theme.BorderColorSoft,
                CornerRadius = 22,
                Anchor       = AnchorStyles.None
            };
            this.Controls.Add(_mainPanel);

            // ── Header row ─────────────────────────────────────────────────
            const int HEADER_H = 88;
            const int PAD = 36;

            var logo = new LogoBadge
            {
                Location    = new Point(PAD, (HEADER_H - 52) / 2),
                Glyph       = "🗔",
                AccentColor = Theme.PurpleAccent
            };
            _mainPanel.Controls.Add(logo);

            var lblBrand = new Label
            {
                Text      = "Inspectify LapStore",
                Location  = new Point(logo.Right + 18, 30),
                AutoSize  = true,
                Font      = new Font("Segoe UI Semibold", 18, FontStyle.Bold),
                ForeColor = Theme.TextColor,
                BackColor = Color.Transparent
            };
            _mainPanel.Controls.Add(lblBrand);

            var lblTagline = new Label
            {
                Text      = "Diagnose · Verify · Sell",
                Location  = new Point(logo.Right + 18, 62),
                AutoSize  = true,
                Font      = Theme.SubTitleFont,
                ForeColor = Theme.MutedText,
                BackColor = Color.Transparent
            };
            _mainPanel.Controls.Add(lblTagline);

            // Version pill (top-right)
            var versionPill = new PillPanel
            {
                Size = new Size(92, 34),
                FillColor   = Color.FromArgb(24, 32, 54),
                BorderColor = Color.FromArgb(44, 56, 86)
            };
            _mainPanel.Controls.Add(versionPill);

            var lblVer = new Label
            {
                Text      = $"v{AppConfig.Version}",
                Dock      = DockStyle.Fill,
                Font      = Theme.PillFont,
                ForeColor = Theme.MutedText,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter
            };
            versionPill.Controls.Add(lblVer);

            // Live indicator
            var liveDot = new Label
            {
                Text      = "●",
                AutoSize  = true,
                Font      = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Theme.SuccessColor,
                BackColor = Color.Transparent
            };
            var lblLive = new Label
            {
                Text      = "Live",
                AutoSize  = true,
                Font      = new Font("Segoe UI Semibold", 10, FontStyle.Bold),
                ForeColor = Theme.SuccessColor,
                BackColor = Color.Transparent
            };
            _mainPanel.Controls.Add(liveDot);
            _mainPanel.Controls.Add(lblLive);

            // Hairline divider under the header
            var divider = new Panel
            {
                Size      = new Size(PW - PAD * 2, 1),
                Location  = new Point(PAD, HEADER_H),
                BackColor = Theme.BorderColorSoft
            };
            _mainPanel.Controls.Add(divider);

            // ── Card grid (2 × 3) ──────────────────────────────────────────
            int gridTop = HEADER_H + 22;
            const int GAP = 14;
            int cardW = (PW - PAD * 2 - GAP) / 2;
            const int cardH = 78;

            int col1 = PAD;
            int col2 = PAD + cardW + GAP;
            int row1 = gridTop;
            int row2 = row1 + cardH + GAP;
            int row3 = row2 + cardH + GAP;

            _cardStart    = AddCard("Start system scan",   "Full hardware analysis",
                                    "★", Theme.AccentColor,
                                    col1, row1, cardW, cardH, Btn_Start_Click);

            _cardStop     = AddCard("Stop · clear memory", "Reset all processes",
                                    "⊘", Theme.ErrorColor,
                                    col2, row1, cardW, cardH, Btn_Stop_Click);

            _cardDownload = AddCard("Save PDF report",     "Requires scan first",
                                    "📄", Theme.WarningColor,
                                    col1, row2, cardW, cardH, Btn_Download_Click);

            _cardSubmit   = AddCard("Upload · sell device","List on marketplace",
                                    "⇧", Theme.SuccessColor,
                                    col2, row2, cardW, cardH, Btn_Submit_Click);

            _cardKeyboard = AddCard("Keyboard test",       "Check all keys",
                                    "⌨", Theme.PurpleAccent,
                                    col1, row3, cardW, cardH, Btn_Keyboard_Click);

            _cardTouchpad = AddCard("Touchpad test",       "Gesture detection",
                                    "◘", Theme.CyanAccent,
                                    col2, row3, cardW, cardH, Btn_Touchpad_Click);

            _cardDownload.Enabled = false;
            _cardSubmit.Enabled   = false;

            // Hide touchpad on desktop; expand keyboard to span both columns
            bool isLaptop = DiagnosticsEngine.IsLaptop();
            _cardTouchpad.Visible = isLaptop;
            if (!isLaptop)
                _cardKeyboard.Width = cardW * 2 + GAP;

            // ── Progress (shown during scan, just above the status bar) ────
            int progressY = row3 + cardH + 22;
            _lblProgress = new Label
            {
                Text      = "",
                Location  = new Point(PAD, progressY),
                Size      = new Size(PW - PAD * 2, 18),
                Font      = Theme.SmallFont,
                ForeColor = Theme.MutedText,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
                Visible   = false
            };
            _mainPanel.Controls.Add(_lblProgress);

            _progressBar = new ProgressBar
            {
                Location  = new Point(PAD, progressY + 20),
                Size      = new Size(PW - PAD * 2, 10),
                Minimum   = 0,
                Maximum   = 100,
                Style     = ProgressBarStyle.Continuous,
                ForeColor = Theme.AccentColor,
                BackColor = Theme.HoverBackground,
                Visible   = false
            };
            _mainPanel.Controls.Add(_progressBar);

            // ── Status bar (bottom) ────────────────────────────────────────
            const int statusH = 58;
            _statusBar = new RoundedPanel
            {
                Location     = new Point(PAD, PH - PAD - statusH),
                Size         = new Size(PW - PAD * 2, statusH),
                BackColor    = Color.FromArgb(12, 30, 26),           // deep emerald tint
                BorderColor  = Color.FromArgb(22, 60, 48),
                CornerRadius = 12
            };
            _mainPanel.Controls.Add(_statusBar);

            _lblStatusDot = new Label
            {
                Text      = "●",
                Location  = new Point(22, (statusH - 22) / 2),
                AutoSize  = true,
                Font      = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Theme.SuccessColor,
                BackColor = Color.Transparent
            };
            _statusBar.Controls.Add(_lblStatusDot);

            _lblStatus = new Label
            {
                Text      = isLaptop ? "Ready to scan. (Laptop detected)"
                                     : "Ready to scan. (Desktop detected)",
                Location  = new Point(54, 0),
                Size      = new Size(_statusBar.Width - 70, statusH),
                Font      = Theme.StatusFont,
                ForeColor = Theme.SuccessColor,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft
            };
            _statusBar.Controls.Add(_lblStatus);

            // Recenter & reposition floating elements on every resize
            this.Resize += (s, e) => LayoutChrome(PW, PH, versionPill, liveDot, lblLive);
            _mainPanel.Resize += (s, e) => LayoutChrome(PW, PH, versionPill, liveDot, lblLive);
            LayoutChrome(PW, PH, versionPill, liveDot, lblLive);
        }

        private void LayoutChrome(int PW, int PH, Control versionPill, Control liveDot, Control lblLive)
        {
            // Centre main panel
            int x = Math.Max(0, (this.ClientSize.Width  - _mainPanel.Width)  / 2);
            int y = Math.Max(0, (this.ClientSize.Height - _mainPanel.Height) / 2);
            _mainPanel.Location = new Point(x, y);

            // Right-align the version pill + live indicator within the header
            const int PAD = 36;
            int liveW = ((Label)lblLive).PreferredWidth;
            lblLive.Location     = new Point(PW - PAD - liveW, 38);
            liveDot.Location     = new Point(lblLive.Left - 16, 38);
            versionPill.Location = new Point(liveDot.Left - versionPill.Width - 10, 36);
        }

        private MenuCard AddCard(string title, string subtitle, string glyph,
                                 Color accent, int x, int y, int w, int h,
                                 EventHandler handler)
        {
            var card = new MenuCard
            {
                Title      = title,
                Subtitle   = subtitle,
                IconGlyph  = glyph,
                IconColor  = accent,
                Location   = new Point(x, y),
                Size       = new Size(w, h)
            };
            card.Click += handler;
            _mainPanel.Controls.Add(card);
            return card;
        }

        // ══════════════════════════════════════════════════════════════════════
        // PROGRESS HELPERS
        // ══════════════════════════════════════════════════════════════════════

        private static readonly (string Message, int Progress)[] ScanSteps =
        {
            ("Detecting CPU…",           10),
            ("Reading RAM…",             22),
            ("Scanning storage…",        35),
            ("Checking GPU…",            48),
            ("Reading battery info…",    58),
            ("Scanning network cards…",  68),
            ("Checking temperatures…",   78),
            ("Reading BIOS info…",       86),
            ("Finalising report…",       95),
        };

        private void ShowProgress(bool visible)
        {
            _progressBar.Visible = visible;
            _lblProgress.Visible = visible;
            if (!visible)
            {
                _progressBar.Value = 0;
                _lblProgress.Text = "";
            }
            // Hide status bar while scanning — progress bar takes its spot
            _statusBar.Visible = !visible;
        }

        private void UpdateProgress(string message, int percent)
        {
            _lblProgress.Text = message;
            _progressBar.Value = Math.Min(percent, 100);
            _mainPanel.Refresh();
        }

        // ══════════════════════════════════════════════════════════════════════
        // BUTTON HANDLERS
        // ══════════════════════════════════════════════════════════════════════

        private async void Btn_Start_Click(object sender, EventArgs e)
        {
            _cardStart.Enabled    = false;
            _cardDownload.Enabled = false;
            _cardSubmit.Enabled   = false;
            ShowProgress(true);
            UpdateProgress("Starting scan…", 0);

            int stepIndex = 0;

            _lastScan = await DiagnosticsEngine.RunFullScan(msg =>
            {
                if (this.InvokeRequired)
                {
                    this.Invoke((Action)(() =>
                    {
                        SetStatus(msg, Theme.WarningColor);
                        if (stepIndex < ScanSteps.Length)
                        {
                            var (stepMsg, pct) = ScanSteps[stepIndex++];
                            UpdateProgress(stepMsg, pct);
                        }
                    }));
                }
                else
                {
                    SetStatus(msg, Theme.WarningColor);
                    if (stepIndex < ScanSteps.Length)
                    {
                        var (stepMsg, pct) = ScanSteps[stepIndex++];
                        UpdateProgress(stepMsg, pct);
                    }
                }
            });

            UpdateProgress("Scan complete ✔", 100);
            await System.Threading.Tasks.Task.Delay(600);
            ShowProgress(false);

            // Warm the price-prediction model in the background so the first
            // "Upload & Sell" click doesn't pay the training cost on the UI thread.
            _ = System.Threading.Tasks.Task.Run(() => PricePredictor.EnsureTrained());

            SetStatus("Scan complete \u2714", Theme.SuccessColor);
            _cardStart.Enabled    = true;
            _cardDownload.Subtitle = "Export report as PDF";
            _cardSubmit.Subtitle   = "Ready to list";
            _cardDownload.Enabled = true;
            _cardSubmit.Enabled   = true;
            _cardDownload.Invalidate();
            _cardSubmit.Invalidate();

            // Auto-prompt keyboard test after scan
            bool isLaptop = DiagnosticsEngine.IsLaptop();
            try
            {
                using var kbs = new ManagementObjectSearcher("SELECT * FROM Win32_Keyboard");
                if (kbs.Get().Count > 0 &&
                    MessageBox.Show("Scan complete. Run the Keyboard Test now?",
                        "Keyboard Test", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    bool keyboardPassed = RunKeyboardTest();

                    if (isLaptop &&
                        MessageBox.Show("Run the Touchpad Test now?",
                            "Touchpad Test", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        RunTouchpadTest();
                    }
                }
            }
            catch { /* WMI optional */ }

            ScrollableReportForm.Show("System Report",
                _lastScan.ReportText.Replace("##", ""), this);
        }

        private void Btn_Stop_Click(object sender, EventArgs e)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            ShowProgress(false);
            SetStatus("Memory released.", Theme.MutedText);
            MessageBox.Show("Background tasks stopped and memory cleared.",
                "Stopped", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void Btn_Download_Click(object sender, EventArgs e)
        {
            if (_lastScan == null) { MessageBox.Show("Please run a scan first."); return; }
            try
            {
                string path = ReportExporter.SaveToPdf(_lastScan.ReportText);
                MessageBox.Show($"PDF saved to Desktop:\n{System.IO.Path.GetFileName(path)}",
                    "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not save PDF:\n" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Btn_Submit_Click(object sender, EventArgs e)
        {
            if (_lastScan == null) { MessageBox.Show("Please run a scan first."); return; }
            using var login = new LoginForm();
            if (login.ShowDialog() != DialogResult.OK) return;
            this.Hide();
            using var ad = new AdPostForm(login.AuthenticatedUsername, _lastScan.Metadata);
            ad.ShowDialog();
            this.Show();
        }

        private void Btn_Keyboard_Click(object sender, EventArgs e) => RunKeyboardTest();
        private void Btn_Touchpad_Click(object sender, EventArgs e) => RunTouchpadTest();

        // ══════════════════════════════════════════════════════════════════════
        // TEST RUNNERS
        // ══════════════════════════════════════════════════════════════════════

        private bool RunKeyboardTest()
        {
            using var kt = new KeyboardTestForm();
            if (kt.ShowDialog() == DialogResult.OK)
            {
                if (_lastScan != null)
                    DiagnosticsEngine.AppendKeyboardResult(_lastScan, kt.FailedKeys);

                bool passed = kt.FailedKeys.Count == 0;
                SetStatus(passed ? "Keyboard: PASSED \u2714" : $"Keyboard: {kt.FailedKeys.Count} key(s) failed",
                          passed ? Theme.SuccessColor : Theme.WarningColor);
                return passed;
            }
            return false;
        }

        private void RunTouchpadTest()
        {
            if (!DiagnosticsEngine.IsLaptop())
            {
                MessageBox.Show("Touchpad test is only available on laptops.",
                    "Desktop PC", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            bool extMouse = _lastScan?.Metadata.ContainsKey("external_mouse_detected") == true
                         && _lastScan.Metadata["external_mouse_detected"] is true;

            using var tt = new TouchpadTestForm(extMouse);
            if (tt.ShowDialog() == DialogResult.OK && _lastScan != null)
            {
                bool passed = tt.LeftButtonPressed && tt.RightButtonPressed;
                string result = passed
                    ? "Both buttons PASSED \u2714"
                    : $"Partial: Left={tt.LeftButtonPressed}, Right={tt.RightButtonPressed}";

                _lastScan.Metadata["touchpad_status"] = result;

                var sb = new System.Text.StringBuilder(_lastScan.ReportText);
                sb.AppendLine($"\n{"─",50}\n##TOUCHPAD TEST RESULT\n{"─",50}");
                sb.AppendLine($"  Result      : {result}");
                if (extMouse)
                    sb.AppendLine("  Note        : External mouse was connected during test.");
                _lastScan.ReportText = sb.ToString();

                SetStatus(passed ? "Touchpad: PASSED \u2714" : "Touchpad: PARTIAL",
                          passed ? Theme.SuccessColor : Theme.WarningColor);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private void SetStatus(string msg, Color color)
        {
            _lblStatus.Text      = msg;
            _lblStatus.ForeColor = color;
            _lblStatusDot.ForeColor = color;

            // Shift the status-bar surface tint based on the severity colour
            if (color == Theme.SuccessColor)
            {
                _statusBar.BackColor = Color.FromArgb(12, 30, 26);
                ((RoundedPanel)_statusBar).BorderColor = Color.FromArgb(22, 60, 48);
            }
            else if (color == Theme.WarningColor)
            {
                _statusBar.BackColor = Color.FromArgb(36, 28, 10);
                ((RoundedPanel)_statusBar).BorderColor = Color.FromArgb(72, 52, 16);
            }
            else if (color == Theme.ErrorColor)
            {
                _statusBar.BackColor = Color.FromArgb(36, 14, 18);
                ((RoundedPanel)_statusBar).BorderColor = Color.FromArgb(72, 26, 32);
            }
            else
            {
                _statusBar.BackColor = Color.FromArgb(20, 26, 44);
                ((RoundedPanel)_statusBar).BorderColor = Theme.BorderColorSoft;
            }
            _statusBar.Invalidate();
        }
    }
}