#nullable disable

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PC_inspect_beta.UI.Forms
{
    public partial class KeyboardTestForm : Form
    {
        // ── Public results ────────────────────────────────────────────────────
        public List<string> FailedKeys { get; private set; } = new();

        // ── State ─────────────────────────────────────────────────────────────
        private readonly Dictionary<Keys, Button> _keyMap = new();

        private static readonly Color ColUnpressed = Color.FromArgb(60, 60, 60);
        private static readonly Color ColPressed = Color.FromArgb(40, 167, 69);
        private static readonly Color ColMissed = Color.FromArgb(220, 53, 69);
        private static readonly Color ColSkipped = Color.FromArgb(40, 40, 50);

        // ── Optional keys (not failed if missed) ──────────────────────────────
        private static readonly HashSet<Keys> OptionalKeys = new()
        {
            Keys.Insert,       Keys.Delete,
            Keys.Home,         Keys.End,
            Keys.Prior,        Keys.Next,
            Keys.LControlKey,  Keys.RControlKey,
            Keys.LShiftKey,    Keys.RShiftKey,
            Keys.LWin,
            Keys.LMenu,        Keys.RMenu
        };

        // ── Key layout ────────────────────────────────────────────────────────
        private static readonly (string Label, Keys Key, int Width)[][] Rows =
        {
            new[] {
                ("Esc",Keys.Escape,0),("F1",Keys.F1,0),("F2",Keys.F2,0),("F3",Keys.F3,0),
                ("F4",Keys.F4,0),("F5",Keys.F5,0),("F6",Keys.F6,0),("F7",Keys.F7,0),
                ("F8",Keys.F8,0),("F9",Keys.F9,0),("F10",Keys.F10,0),("F11",Keys.F11,0),("F12",Keys.F12,0)
            },
            new[] {
                ("`",Keys.Oemtilde,0),("1",Keys.D1,0),("2",Keys.D2,0),("3",Keys.D3,0),
                ("4",Keys.D4,0),("5",Keys.D5,0),("6",Keys.D6,0),("7",Keys.D7,0),
                ("8",Keys.D8,0),("9",Keys.D9,0),("0",Keys.D0,0),("-",Keys.OemMinus,0),
                ("=",Keys.Oemplus,0),("Backspace",Keys.Back,100)
            },
            new[] {
                ("Tab",Keys.Tab,75),("Q",Keys.Q,0),("W",Keys.W,0),("E",Keys.E,0),
                ("R",Keys.R,0),("T",Keys.T,0),("Y",Keys.Y,0),("U",Keys.U,0),
                ("I",Keys.I,0),("O",Keys.O,0),("P",Keys.P,0),("[",Keys.OemOpenBrackets,0),
                ("]",Keys.Oem6,0),("\\",Keys.OemPipe,0)
            },
            new[] {
                ("Caps",Keys.CapsLock,80),("A",Keys.A,0),("S",Keys.S,0),("D",Keys.D,0),
                ("F",Keys.F,0),("G",Keys.G,0),("H",Keys.H,0),("J",Keys.J,0),
                ("K",Keys.K,0),("L",Keys.L,0),(";",Keys.OemSemicolon,0),
                ("'",Keys.OemQuotes,0),("Enter",Keys.Enter,100)
            },
            new[] {
                ("LShift",Keys.LShiftKey,110),("Z",Keys.Z,0),("X",Keys.X,0),("C",Keys.C,0),
                ("V",Keys.V,0),("B",Keys.B,0),("N",Keys.N,0),("M",Keys.M,0),
                (",",Keys.Oemcomma,0),(".",Keys.OemPeriod,0),("/",Keys.OemQuestion,0),
                ("RShift",Keys.RShiftKey,110)
            },
            new[] {
                ("LCtrl",Keys.LControlKey,85),("LWin",Keys.LWin,65),("LAlt",Keys.LMenu,65),
                ("Space",Keys.Space,360),
                ("RAlt",Keys.RMenu,65),("RCtrl",Keys.RControlKey,85)
            },
            new[] {
                ("Up",Keys.Up,58),("Down",Keys.Down,58),("Left",Keys.Left,58),("Right",Keys.Right,58),
                ("Ins",Keys.Insert,0),("Del",Keys.Delete,0),
                ("Home",Keys.Home,0),("End",Keys.End,0),("PgUp",Keys.Prior,0),("PgDn",Keys.Next,0)
            }
        };

        // ── Constructor ───────────────────────────────────────────────────────
        public KeyboardTestForm()
        {
            InitializeComponent();
            BuildLayout();
            this.KeyDown += Form_KeyDown;
        }

        private void InitializeComponent()
        {
            this.Text = "Keyboard Test – Press every key";
            this.Size = new Size(1040, 560);
            this.BackColor = Theme.DarkBackground;
            this.ForeColor = Theme.TextColor;
            this.StartPosition = FormStartPosition.CenterParent;
            this.KeyPreview = true;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
        }

        // ── Layout ────────────────────────────────────────────────────────────
        private void BuildLayout()
        {
            this.Controls.Add(UIHelper.CreateLabel(
                "Press every key on your keyboard. Green = detected.  Grey = not pressed.",
                new Point(12, 10), Theme.SmallFont, Theme.MutedText));

            var pnl = new Panel
            {
                Location = new Point(8, 34),
                Size = new Size(1012, 440),
                AutoScroll = true,
                BackColor = Color.Transparent
            };
            this.Controls.Add(pnl);

            int y = 8;
            foreach (var row in Rows)
            {
                int x = 8;
                foreach (var (label, key, widthOverride) in row)
                {
                    int w = widthOverride > 0 ? widthOverride : 58;

                    var btn = new Button
                    {
                        Text = label,
                        Size = new Size(w, 50),
                        Location = new Point(x, y),
                        BackColor = ColUnpressed,
                        ForeColor = Color.White,
                        FlatStyle = FlatStyle.Flat,
                        Font = new Font("Segoe UI", 8, FontStyle.Bold),
                        Cursor = Cursors.Default,
                        TabStop = false
                    };
                    btn.FlatAppearance.BorderSize = 1;
                    btn.FlatAppearance.BorderColor = Color.FromArgb(85, 85, 90);

                    pnl.Controls.Add(btn);
                    _keyMap[key] = btn;
                    x += w + 4;
                }
                y += 56;
            }

            var btnFinish = UIHelper.CreateButton(
                "\u2714  Finish Keyboard Test", Theme.AccentColor,
                new Point(8, 486), 1012, 46);
            btnFinish.TabStop = false;
            btnFinish.Click += BtnFinish_Click;
            this.Controls.Add(btnFinish);
        }

        // ── Arrow key fix — ProcessCmdKey catches arrow/tab keys before WinForms ──
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys baseKey = keyData & ~Keys.Modifiers;

            if (baseKey == Keys.Up || baseKey == Keys.Down ||
                baseKey == Keys.Left || baseKey == Keys.Right)
            {
                MarkKeyPressed(baseKey);
                return true; // consumed
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ── Key detection ─────────────────────────────────────────────────────
        private void Form_KeyDown(object sender, KeyEventArgs e)
        {
            MarkKeyPressed(e.KeyCode);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        // ── Shared helper ─────────────────────────────────────────────────────
        private void MarkKeyPressed(Keys key)
        {
            if (_keyMap.TryGetValue(key, out var btn))
                btn.BackColor = ColPressed;
        }

        // ── Finish ────────────────────────────────────────────────────────────
        private void BtnFinish_Click(object sender, EventArgs e)
        {
            FailedKeys = _keyMap
                .Where(kv => kv.Value.BackColor == ColUnpressed && !OptionalKeys.Contains(kv.Key))
                .Select(kv => kv.Key.ToString())
                .ToList();

            foreach (var kv in _keyMap.Where(kv => kv.Value.BackColor == ColUnpressed))
            {
                kv.Value.BackColor = OptionalKeys.Contains(kv.Key)
                    ? ColSkipped
                    : ColMissed;
            }

            string msg = FailedKeys.Count == 0
                ? "All keys detected — Keyboard PASSED \u2714"
                : $"{FailedKeys.Count} key(s) not detected:\n{string.Join(", ", FailedKeys)}";

            MessageBox.Show(msg, "Keyboard Result", MessageBoxButtons.OK,
                FailedKeys.Count == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

            this.DialogResult = DialogResult.OK;
        }
    }
}