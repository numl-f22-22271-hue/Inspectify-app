#nullable disable

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace PC_inspect_beta.UI.Forms
{
    public class TouchpadTestForm : Form
    {
        [DllImport("user32.dll")]
        static extern uint GetRawInputDeviceList(
            [Out] RAWINPUTDEVICELIST[] pRawInputDeviceList, ref uint puiNumDevices, uint cbSize);

        [DllImport("user32.dll")]
        static extern uint GetRawInputDeviceInfo(
            IntPtr hDevice, uint uiCommand, IntPtr pData, ref uint pcbSize);

        [DllImport("user32.dll")]
        static extern bool RegisterRawInputDevices(
            RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

        [DllImport("user32.dll")]
        static extern uint GetRawInputData(
            IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

        [StructLayout(LayoutKind.Sequential)]
        struct RAWINPUTDEVICELIST { public IntPtr hDevice; public uint dwType; }

        [StructLayout(LayoutKind.Sequential)]
        struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct RAWINPUTHEADER
        {
            public uint dwType;
            public uint dwSize;
            public IntPtr hDevice;
            public IntPtr wParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct RAWMOUSE
        {
            public ushort usFlags;
            public uint ulButtons;
            public uint ulRawButtons;
            public int lLastX;
            public int lLastY;
            public uint ulExtraInformation;
        }

        [StructLayout(LayoutKind.Explicit)]
        struct RAWINPUT
        {
            [FieldOffset(0)] public RAWINPUTHEADER header;
            [FieldOffset(24)] public RAWMOUSE mouse;
        }

        private const uint RIDI_DEVICENAME = 0x20000007;
        private const uint RIM_TYPEMOUSE = 0;
        private const int WM_INPUT = 0x00FF;
        private const uint RIDEV_INPUTSINK = 0x00000100;
        private const uint RI_MOUSE_LEFT_BUTTON_DOWN = 0x0001;
        private const uint RI_MOUSE_RIGHT_BUTTON_DOWN = 0x0004;
        private const uint RID_INPUT = 0x10000003;
        private const ushort HID_USAGE_PAGE_GENERIC = 0x01;
        private const ushort HID_USAGE_GENERIC_MOUSE = 0x02;

        public bool LeftButtonPressed { get; private set; }
        public bool RightButtonPressed { get; private set; }
        public bool Skipped { get; private set; }

        private readonly HashSet<IntPtr> _touchpadHandles = new();
        private readonly Panel _pnlLeft, _pnlRight;
        private readonly Label _lblLeftStatus, _lblRightStatus, _lblInfo;
        private readonly bool _externalMouseDetected;

        private static readonly Color ColIdle = Color.FromArgb(50, 50, 58);
        private static readonly Color ColSuccess = Color.FromArgb(40, 167, 69);
        private static readonly Color ColBorder = Color.FromArgb(90, 90, 105);

        public TouchpadTestForm(bool externalMouseDetected = false)
        {
            _externalMouseDetected = externalMouseDetected;

            this.Text = "Touchpad Test";
            this.Size = new Size(560, 510);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Theme.DarkBackground;
            this.ForeColor = Theme.TextColor;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            FindTouchpadHandles();

            BuildUI(out _pnlLeft, out _pnlRight,
                    out _lblLeftStatus, out _lblRightStatus, out _lblInfo);

            RegisterRawInputDevices(new[]
            {
                new RAWINPUTDEVICE
                {
                    usUsagePage = HID_USAGE_PAGE_GENERIC,
                    usUsage     = HID_USAGE_GENERIC_MOUSE,
                    dwFlags     = RIDEV_INPUTSINK,
                    hwndTarget  = this.Handle
                }
            }, 1, (uint)Marshal.SizeOf<RAWINPUTDEVICE>());

            if (_touchpadHandles.Count == 0)
            {
                _lblInfo.Text = "⚠ Touchpad device not found via Raw Input.\n" +
                                "Try pressing the touchpad buttons anyway — some drivers\n" +
                                "report as generic mouse and may still be detected.";
                _lblInfo.ForeColor = Theme.WarningColor;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_INPUT)
                ProcessRawInput(m.LParam);
            base.WndProc(ref m);
        }

        private void ProcessRawInput(IntPtr hRawInput)
        {
            uint dwSize = 0;
            GetRawInputData(hRawInput, RID_INPUT, IntPtr.Zero, ref dwSize,
                            (uint)Marshal.SizeOf<RAWINPUTHEADER>());

            if (dwSize == 0) return;

            IntPtr buf = Marshal.AllocHGlobal((int)dwSize);
            try
            {
                if (GetRawInputData(hRawInput, RID_INPUT, buf, ref dwSize,
                                    (uint)Marshal.SizeOf<RAWINPUTHEADER>()) != dwSize) return;

                var raw = Marshal.PtrToStructure<RAWINPUT>(buf);

                if (raw.header.dwType != RIM_TYPEMOUSE) return;

                IntPtr deviceHandle = raw.header.hDevice;
                uint buttonFlags = raw.mouse.ulButtons & 0xFFFF;

                bool fromTouchpad = _touchpadHandles.Count == 0
                                 || _touchpadHandles.Contains(deviceHandle);

                if (!fromTouchpad) return;

                bool leftDown = (buttonFlags & RI_MOUSE_LEFT_BUTTON_DOWN) != 0;
                bool rightDown = (buttonFlags & RI_MOUSE_RIGHT_BUTTON_DOWN) != 0;

                if (leftDown && !LeftButtonPressed)
                {
                    LeftButtonPressed = true;
                    this.Invoke((Action)(() =>
                    {
                        _pnlLeft.BackColor = ColSuccess;
                        _lblLeftStatus.Text = "\u2714 Detected";
                        _lblLeftStatus.ForeColor = ColSuccess;
                        SetBoxText(_pnlLeft, "LEFT\n\u2714 Pressed!");
                        CheckBothPressed();
                    }));
                }

                if (rightDown && !RightButtonPressed)
                {
                    RightButtonPressed = true;
                    this.Invoke((Action)(() =>
                    {
                        _pnlRight.BackColor = ColSuccess;
                        _lblRightStatus.Text = "\u2714 Detected";
                        _lblRightStatus.ForeColor = ColSuccess;
                        SetBoxText(_pnlRight, "RIGHT\n\u2714 Pressed!");
                        CheckBothPressed();
                    }));
                }
            }
            finally { Marshal.FreeHGlobal(buf); }
        }

        private void FindTouchpadHandles()
        {
            uint deviceCount = 0;
            uint structSize = (uint)Marshal.SizeOf<RAWINPUTDEVICELIST>();
            GetRawInputDeviceList(null, ref deviceCount, structSize);
            if (deviceCount == 0) return;

            var list = new RAWINPUTDEVICELIST[deviceCount];
            GetRawInputDeviceList(list, ref deviceCount, structSize);

            foreach (var dev in list)
            {
                if (dev.dwType != RIM_TYPEMOUSE) continue;

                string devName = GetRawDeviceName(dev.hDevice);
                if (devName == null) continue;

                string lower = devName.ToLower();

                if (lower.Contains("touchpad") ||
                    lower.Contains("synaptics") ||
                    lower.Contains("elan") ||
                    lower.Contains("alps") ||
                    lower.Contains("precision") ||
                    lower.Contains("trackpad") ||
                    lower.Contains("clickpad") ||
                    lower.Contains("vid_04f3") ||
                    lower.Contains("vid_06cb") ||
                    lower.Contains("vid_044e") ||
                    lower.Contains("vid_2808") ||
                    lower.Contains("vid_04b4"))
                {
                    _touchpadHandles.Add(dev.hDevice);
                }
            }
        }

        private static string GetRawDeviceName(IntPtr hDevice)
        {
            uint size = 0;
            GetRawInputDeviceInfo(hDevice, RIDI_DEVICENAME, IntPtr.Zero, ref size);
            if (size == 0) return null;

            IntPtr buf = Marshal.AllocHGlobal((int)(size * 2));
            try
            {
                GetRawInputDeviceInfo(hDevice, RIDI_DEVICENAME, buf, ref size);
                return Marshal.PtrToStringAnsi(buf);
            }
            finally { Marshal.FreeHGlobal(buf); }
        }

        private void BuildUI(out Panel pnlLeft, out Panel pnlRight,
                             out Label lblLeft, out Label lblRight, out Label lblInfo)
        {
            int y = 20;

            var lblTitle = new Label
            {
                Text = "Touchpad Button Test",
                Location = new Point(0, y),
                Size = new Size(560, 30),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = Theme.TitleFont,
                ForeColor = Theme.CyanAccent,
                BackColor = Color.Transparent
            };
            this.Controls.Add(lblTitle); y += 38;

            if (_externalMouseDetected)
            {
                var w = new Label
                {
                    Text = "⚠  External mouse connected. Disconnect it for accurate results.",
                    Location = new Point(10, y),
                    Size = new Size(540, 30),
                    Font = new Font("Segoe UI", 9, FontStyle.Bold),
                    ForeColor = Theme.WarningColor,
                    BackColor = Color.FromArgb(50, 40, 0),
                    TextAlign = ContentAlignment.MiddleCenter
                };
                this.Controls.Add(w); y += 38;
            }

            lblInfo = new Label
            {
                Text = "Press the LEFT then RIGHT physical touchpad button.\nOnly touchpad clicks are counted — mouse clicks are ignored.",
                Location = new Point(10, y),
                Size = new Size(540, 40),
                Font = Theme.SmallFont,
                ForeColor = Theme.MutedText,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            this.Controls.Add(lblInfo); y += 52;

            const int BW = 200, BH = 130;
            int x1 = 50, x2 = 560 - 50 - BW;

            pnlLeft = MakeBox("LEFT\nButton", new Point(x1, y), BW, BH);
            pnlRight = MakeBox("RIGHT\nButton", new Point(x2, y), BW, BH);
            this.Controls.Add(pnlLeft);
            this.Controls.Add(pnlRight);
            y += BH + 8;

            lblLeft = new Label
            {
                Text = "Waiting for press…",
                Location = new Point(x1, y),
                Size = new Size(BW, 22),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = Theme.SmallFont,
                ForeColor = Theme.MutedText,
                BackColor = Color.Transparent
            };
            this.Controls.Add(lblLeft);

            lblRight = new Label
            {
                Text = "Waiting for press…",
                Location = new Point(x2, y),
                Size = new Size(BW, 22),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = Theme.SmallFont,
                ForeColor = Theme.MutedText,
                BackColor = Color.Transparent
            };
            this.Controls.Add(lblRight);
            y += 34;

            var btnDone = UIHelper.CreateButton("Mark as Done", Theme.AccentColor,
                new Point(x1, y), BW, 40);
            btnDone.Click += (s, e) =>
            {
                string r = (LeftButtonPressed && RightButtonPressed)
                    ? "Both buttons PASSED \u2714"
                    : $"Partial — Left: {(LeftButtonPressed ? "OK" : "Not pressed")}  " +
                      $"Right: {(RightButtonPressed ? "OK" : "Not pressed")}";
                MessageBox.Show(r, "Touchpad Result", MessageBoxButtons.OK,
                    (LeftButtonPressed && RightButtonPressed)
                        ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                this.DialogResult = DialogResult.OK;
            };
            this.Controls.Add(btnDone);

            var btnSkip = UIHelper.CreateButton("Skip", Color.FromArgb(70, 70, 75),
                new Point(x2, y), BW, 40);
            btnSkip.Click += (s, e) => { Skipped = true; this.DialogResult = DialogResult.Cancel; };
            this.Controls.Add(btnSkip);
        }

        private void CheckBothPressed()
        {
            if (LeftButtonPressed && RightButtonPressed)
            {
                MessageBox.Show("Both touchpad buttons detected!\nTouchpad PASSED \u2714",
                    "Touchpad Result", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.DialogResult = DialogResult.OK;
            }
        }

        private static Panel MakeBox(string text, Point loc, int w, int h)
        {
            var pnl = new Panel { Location = loc, Size = new Size(w, h), BackColor = ColIdle };
            var lbl = new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent
            };
            pnl.Controls.Add(lbl);
            pnl.Paint += (s, e) =>
            {
                using var pen = new Pen(ColBorder, 2);
                e.Graphics.DrawRectangle(pen, 1, 1, pnl.Width - 2, pnl.Height - 2);
            };
            return pnl;
        }

        private static void SetBoxText(Panel pnl, string text)
        {
            if (pnl.Controls.Count > 0 && pnl.Controls[0] is Label lbl)
                lbl.Text = text;
            pnl.Invalidate();
        }
    }
}