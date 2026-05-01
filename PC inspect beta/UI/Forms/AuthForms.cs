#nullable disable

using System;
using System.Drawing;
using System.Windows.Forms;
using PC_inspect_beta.Core;

namespace PC_inspect_beta.UI.Forms
{
    // ══════════════════════════════════════════════════════════════════════════
    //  SHARED HELPERS — gradient-header form chrome
    // ══════════════════════════════════════════════════════════════════════════

    internal static class AuthChrome
    {
        public const int FormW = 440;
        public const int FormPad = 36;
        public const int HeaderH = 96;

        public static void ApplyShell(Form f, string title, int height)
        {
            f.Text            = title;
            f.Size            = new Size(FormW, height);
            f.StartPosition   = FormStartPosition.CenterParent;
            f.BackColor       = Theme.PanelBackground;
            f.ForeColor       = Theme.TextColor;
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.MaximizeBox     = false;
            f.MinimizeBox     = false;
            f.Font            = Theme.NormalFont;
        }

        public static void AddHeader(Form f, string title, string subtitle)
        {
            var header = new GradientPanel
            {
                Location = new Point(0, 0),
                Size     = new Size(f.ClientSize.Width, HeaderH),
            };
            f.Controls.Add(header);

            var lblTitle = new Label
            {
                Text      = title,
                Location  = new Point(0, 22),
                Size      = new Size(header.Width, 32),
                Font      = new Font("Segoe UI Semibold", 18, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter
            };
            header.Controls.Add(lblTitle);

            var lblSub = new Label
            {
                Text      = subtitle,
                Location  = new Point(0, 56),
                Size      = new Size(header.Width, 22),
                Font      = Theme.SubTitleFont,
                ForeColor = Color.FromArgb(230, 230, 250),
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter
            };
            header.Controls.Add(lblSub);
        }

        public static void AddField(Form f, string label, ref TextBox box, ref int y,
            bool isPassword = false, string hint = null)
        {
            int x = FormPad;
            int w = f.ClientSize.Width - FormPad * 2;

            f.Controls.Add(UIHelper.CreateLabel(label, new Point(x, y), Theme.SmallFont, Theme.MutedText));
            box = UIHelper.CreateTextBox(new Point(x, y + 22), w, isPassword: isPassword);
            f.Controls.Add(box);

            if (isPassword)
            {
                // Eye toggle — floats on the right side of the text box
                var eye = new Label
                {
                    Text      = "👁",
                    Location  = new Point(box.Right - 28, box.Top + 2),
                    Size      = new Size(26, box.Height - 4),
                    Font      = new Font("Segoe UI", 12),
                    ForeColor = Theme.AccentColor,
                    BackColor = Theme.InputBackground,
                    Cursor    = Cursors.Hand,
                    TextAlign = ContentAlignment.MiddleCenter
                };
                bool shown = false;
                var capturedBox = box;
                eye.Click += (s, e) =>
                {
                    shown                  = !shown;
                    capturedBox.PasswordChar = shown ? '\0' : '●';
                    eye.Text               = shown ? "🙈" : "👁";
                };
                f.Controls.Add(eye);
                eye.BringToFront();
            }

            if (!string.IsNullOrEmpty(hint))
            {
                f.Controls.Add(UIHelper.CreateLabel(hint,
                    new Point(x, y + 60), Theme.SmallFont, Theme.SubtleText));
                y += 78 + 16;
            }
            else
            {
                y += 78;
            }
        }

        // Validate email — @ required, plus at least one '.' after the @
        public static bool IsValidEmail(string email)
        {
            int at = email.IndexOf('@');
            if (at <= 0) return false;
            int dot = email.IndexOf('.', at + 2);
            return dot > 0 && dot < email.Length - 1;
        }

        // Validate phone — 10 to 15 digits, allowing dashes / spaces / + prefix
        public static bool IsValidPhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return false;
            int digits = 0;
            foreach (char c in phone)
            {
                if (char.IsDigit(c)) digits++;
                else if (c != ' ' && c != '-' && c != '+' && c != '(' && c != ')') return false;
            }
            return digits >= 10 && digits <= 15;
        }

        public static DateTimePicker AddDatePicker(Form f, string label, ref int y,
            DateTime? maxDate = null)
        {
            int x = FormPad;
            int w = f.ClientSize.Width - FormPad * 2;

            f.Controls.Add(UIHelper.CreateLabel(label, new Point(x, y), Theme.SmallFont, Theme.MutedText));
            var dtp = new DateTimePicker
            {
                Location     = new Point(x, y + 22),
                Width        = w,
                Format       = DateTimePickerFormat.Custom,
                CustomFormat = "dd/MM/yyyy",
                Font         = Theme.NormalFont,
                CalendarMonthBackground = Color.White,
                CalendarForeColor       = Theme.TextColor,
                CalendarTitleBackColor  = Theme.AccentColor,
                CalendarTitleForeColor  = Color.White,
                CalendarTrailingForeColor = Theme.SubtleText
            };
            if (maxDate.HasValue) dtp.MaxDate = maxDate.Value;
            f.Controls.Add(dtp);
            y += 78;
            return dtp;
        }

        public static LinkLabel AddLink(Form f, string text, int y, Color color)
        {
            var link = new LinkLabel
            {
                Text      = text,
                AutoSize  = true,
                Font      = Theme.SmallFont,
                LinkColor = color,
                ActiveLinkColor = ControlPaint.Light(color, 0.3f),
                BackColor = Color.Transparent
            };
            f.Controls.Add(link);
            int lx = (f.ClientSize.Width - link.PreferredWidth) / 2;
            link.Location = new Point(lx, y);
            return link;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  LOGIN FORM
    // ══════════════════════════════════════════════════════════════════════════

    public class LoginForm : Form
    {
        public string AuthenticatedUsername { get; private set; }

        private TextBox _txtUser, _txtPass;

        public LoginForm()
        {
            AuthChrome.ApplyShell(this, "Sign In", 520);
            AuthChrome.AddHeader(this, "Welcome Back", "Sign in to publish your listing");

            int y = AuthChrome.HeaderH + 14;

            // Friendly credential reminder above both input bars
            var hint = new Label
            {
                Text      = "Sign in with your Inspectify LapStore account credentials",
                Location  = new Point(AuthChrome.FormPad, y),
                Size      = new Size(this.ClientSize.Width - AuthChrome.FormPad * 2, 34),
                Font      = Theme.SmallFont,
                ForeColor = Theme.MutedText,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter
            };
            this.Controls.Add(hint);
            y += 40;

            AuthChrome.AddField(this, "Username", ref _txtUser, ref y);
            AuthChrome.AddField(this, "Password", ref _txtPass, ref y, isPassword: true);

            int btnW = this.ClientSize.Width - AuthChrome.FormPad * 2;
            var btnLogin = UIHelper.CreateButton("Log In", Theme.AccentColor,
                new Point(AuthChrome.FormPad, y + 6), btnW, 48);
            btnLogin.Click += async (s, e) =>
            {
                btnLogin.Text = "Verifying…"; btnLogin.Enabled = false;
                try
                {
                    var user = await DbHelper.GetUser(_txtUser.Text.Trim());
                    if (user != null && user["password"]?.ToString() == _txtPass.Text)
                    {
                        AuthenticatedUsername = _txtUser.Text.Trim();
                        DialogResult          = DialogResult.OK;
                    }
                    else
                    {
                        MessageBox.Show("Invalid username or password.", "Login Failed",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        btnLogin.Text = "Log In"; btnLogin.Enabled = true;
                    }
                }
                catch
                {
                    MessageBox.Show("Network error – check your connection.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    btnLogin.Text = "Log In"; btnLogin.Enabled = true;
                }
            };
            this.Controls.Add(btnLogin);
            y += 74;

            var lnkReg = AuthChrome.AddLink(this, "No account?  Create one →", y, Theme.AccentColor);
            lnkReg.Click += (s, e) => { this.Hide(); new SignUpForm().ShowDialog(); this.Show(); };

            var lnkForgot = AuthChrome.AddLink(this, "Forgot Password?", y + 28, Theme.MutedText);
            lnkForgot.Click += (s, e) => { this.Hide(); new ForgotPasswordForm().ShowDialog(); this.Show(); };
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  SIGN UP FORM
    // ══════════════════════════════════════════════════════════════════════════

    public class SignUpForm : Form
    {
        private TextBox        _txtUser, _txtPass, _txtEmail, _txtPhone;
        private DateTimePicker _dtpBirth;

        public SignUpForm()
        {
            AuthChrome.ApplyShell(this, "Create Account", 790);
            AuthChrome.AddHeader(this, "Create Account",
                "Join Inspectify LapStore to start selling");

            int y = AuthChrome.HeaderH + 28;
            AuthChrome.AddField(this, "Username", ref _txtUser, ref y);
            AuthChrome.AddField(this, "Password", ref _txtPass, ref y,
                isPassword: true, hint: "Must be at least 6 characters");
            AuthChrome.AddField(this, "Email / Gmail", ref _txtEmail, ref y);
            AuthChrome.AddField(this, "Phone Number", ref _txtPhone, ref y,
                hint: "Format: 03xx-xxxxxxx");
            _dtpBirth = AuthChrome.AddDatePicker(this, "Date of Birth (DD/MM/YYYY)",
                ref y, maxDate: DateTime.Today.AddYears(-10));

            int btnW = this.ClientSize.Width - AuthChrome.FormPad * 2;
            var btnReg = UIHelper.CreateButton("Sign Up", Theme.SuccessColor,
                new Point(AuthChrome.FormPad, y + 6), btnW, 48);
            btnReg.Click += async (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_txtUser.Text))
                { MessageBox.Show("Username is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                if (_txtPass.Text.Length < 6)
                { MessageBox.Show("Password must be at least 6 characters long.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                if (!AuthChrome.IsValidEmail(_txtEmail.Text.Trim()))
                { MessageBox.Show("Please enter a valid email address.\nFormat: example@domain.com", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                if (!AuthChrome.IsValidPhone(_txtPhone.Text.Trim()))
                { MessageBox.Show("Please enter a valid phone number.\nUse 10 – 15 digits (e.g., 03xx-xxxxxxx).",
                    "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

                btnReg.Text = "Creating…"; btnReg.Enabled = false;
                try
                {
                    if (await DbHelper.GetUser(_txtUser.Text.Trim()) != null)
                    {
                        MessageBox.Show("Username already taken.");
                        btnReg.Text = "Sign Up"; btnReg.Enabled = true;
                        return;
                    }

                    await DbHelper.SaveUser(_txtUser.Text.Trim(), new
                    {
                        username = _txtUser.Text.Trim(),
                        password = _txtPass.Text,
                        email    = _txtEmail.Text.Trim(),
                        phone    = _txtPhone.Text.Trim(),
                        dob      = _dtpBirth.Value.ToString("yyyy-MM-dd")
                    });
                    MessageBox.Show("Account created! Please log in.", "Success",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: " + ex.Message);
                    btnReg.Text = "Sign Up"; btnReg.Enabled = true;
                }
            };
            this.Controls.Add(btnReg);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  FORGOT PASSWORD FORM
    // ══════════════════════════════════════════════════════════════════════════

    public class ForgotPasswordForm : Form
    {
        private TextBox        _txtUser, _txtEmail, _txtNewPass;
        private DateTimePicker _dtpBirth;

        public ForgotPasswordForm()
        {
            AuthChrome.ApplyShell(this, "Reset Password", 700);
            AuthChrome.AddHeader(this, "Reset Password",
                "Verify your details to update your password");

            int y = AuthChrome.HeaderH + 28;
            AuthChrome.AddField(this, "Username",     ref _txtUser, ref y);
            AuthChrome.AddField(this, "Verify Email", ref _txtEmail, ref y);
            _dtpBirth = AuthChrome.AddDatePicker(this, "Verify Date of Birth (DD/MM/YYYY)", ref y);
            AuthChrome.AddField(this, "New Password", ref _txtNewPass, ref y, isPassword: true);

            int btnW = this.ClientSize.Width - AuthChrome.FormPad * 2;
            var btnReset = UIHelper.CreateButton("Update Password", Theme.WarningColor,
                new Point(AuthChrome.FormPad, y + 6), btnW, 48);
            btnReset.ForeColor = Color.Black;
            btnReset.Click += async (s, e) =>
            {
                btnReset.Text = "Verifying…"; btnReset.Enabled = false;
                try
                {
                    var u = await DbHelper.GetUser(_txtUser.Text.Trim());
                    if (u != null &&
                        u["email"]?.ToString() == _txtEmail.Text.Trim() &&
                        u["dob"]?.ToString()   == _dtpBirth.Value.ToString("yyyy-MM-dd"))
                    {
                        u["password"] = _txtNewPass.Text;
                        await DbHelper.SaveUser(_txtUser.Text.Trim(), u);
                        MessageBox.Show("Password updated!", "Success",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                        this.Close();
                    }
                    else
                    {
                        MessageBox.Show("Verification failed.\nCheck username, email, and date of birth.",
                            "Mismatch", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        btnReset.Text = "Update Password"; btnReset.Enabled = true;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: " + ex.Message);
                    btnReset.Text = "Update Password"; btnReset.Enabled = true;
                }
            };
            this.Controls.Add(btnReset);
        }
    }
}
