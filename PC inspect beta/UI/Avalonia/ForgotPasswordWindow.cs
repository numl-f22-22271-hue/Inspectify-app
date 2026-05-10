using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PC_inspect_beta.Core;

namespace PC_inspect_beta.UI.Avalonia
{
    public class ForgotPasswordWindow : Window
    {
        private StackPanel _stepEmail = null!, _stepCode = null!, _stepNewPass = null!;
        private TextBox _txtUser = null!, _txtCode = null!, _txtNewPass = null!, _txtConfirmPass = null!;
        private TextBlock _errorText = null!;
        private Button _btnSend = null!, _btnVerify = null!, _btnReset = null!;

        private string _verificationCode = "";
        private string _userEmail = "";
        private string _userName = "";
        private string _displayName = "";
        private DateTime _codeExpiry;

        public ForgotPasswordWindow()
        {
            Title = "Reset Password";
            Width = 440;
            Height = 520;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.Parse("#16212e"));
            BuildUi();
        }

        private void BuildUi()
        {
            var root = new DockPanel();

            var header = new Border
            {
                Background = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(Color.Parse("#f59e0b"), 0),
                        new GradientStop(Color.Parse("#fbbf24"), 1)
                    }
                },
                Height = 96,
                Padding = new Thickness(20, 20)
            };
            DockPanel.SetDock(header, Dock.Top);

            var hs = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            hs.Children.Add(new TextBlock { Text = "Reset Password", FontSize = 22, FontWeight = FontWeight.Bold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center });
            hs.Children.Add(new TextBlock { Text = "We'll send a verification code to your email", FontSize = 13, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) });
            header.Child = hs;
            root.Children.Add(header);

            var form = new StackPanel { Margin = new Thickness(36, 24), Spacing = 14 };

            _errorText = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.Parse("#ef4444")),
                FontSize = 12,
                IsVisible = false,
                TextWrapping = TextWrapping.Wrap
            };

            // Step 1: Enter username/email
            _stepEmail = new StackPanel { Spacing = 14 };
            _stepEmail.Children.Add(MakeLabel("Username or Email"));
            _txtUser = MakeInput();
            _stepEmail.Children.Add(_txtUser);
            _btnSend = MakeButton("Send Verification Code", "#f59e0b");
            _btnSend.Click += async (_, _) => await SendCodeAsync();
            _stepEmail.Children.Add(_btnSend);
            form.Children.Add(_stepEmail);

            // Step 2: Enter code
            _stepCode = new StackPanel { Spacing = 14, IsVisible = false };
            _stepCode.Children.Add(MakeLabel("Enter 6-digit code sent to your email"));
            _txtCode = MakeInput();
            _txtCode.MaxLength = 6;
            _stepCode.Children.Add(_txtCode);
            _btnVerify = MakeButton("Verify Code", "#2b72d4");
            _btnVerify.Click += (_, _) => VerifyCode();
            _stepCode.Children.Add(_btnVerify);
            var resendBtn = new Button
            {
                Content = "Resend Code",
                Foreground = new SolidColorBrush(Color.Parse("#5ea0ff")),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            resendBtn.Click += async (_, _) => await SendCodeAsync();
            _stepCode.Children.Add(resendBtn);
            form.Children.Add(_stepCode);

            // Step 3: New password
            _stepNewPass = new StackPanel { Spacing = 14, IsVisible = false };
            _stepNewPass.Children.Add(MakeLabel("New Password"));
            _txtNewPass = MakeInput(isPassword: true);
            _stepNewPass.Children.Add(_txtNewPass);
            _stepNewPass.Children.Add(MakeLabel("Confirm Password"));
            _txtConfirmPass = MakeInput(isPassword: true);
            _stepNewPass.Children.Add(_txtConfirmPass);
            _btnReset = MakeButton("Update Password", "#10b981");
            _btnReset.Click += async (_, _) => await ResetPasswordAsync();
            _stepNewPass.Children.Add(_btnReset);
            form.Children.Add(_stepNewPass);

            form.Children.Add(_errorText);
            root.Children.Add(form);
            Content = root;
        }

        private async Task SendCodeAsync()
        {
            _errorText.IsVisible = false;
            var identifier = (_txtUser.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(identifier))
            {
                ShowError("Please enter your username or email.");
                return;
            }

            _btnSend.IsEnabled = false;
            _btnSend.Content = "Looking up account...";

            try
            {
                var user = await DbHelper.GetUser(identifier);
                if (user == null && identifier.Contains("@"))
                    user = await DbHelper.GetUserByEmail(identifier);

                if (user == null)
                {
                    ShowError("Account not found. Check your username or email.");
                    return;
                }

                _userEmail = user["email"]?.ToString() ?? "";
                _userName = user["_id"]?.ToString() ?? identifier;
                _displayName = user["fullName"]?.ToString() ?? user["full_name"]?.ToString() ?? _userName;

                if (string.IsNullOrWhiteSpace(_userEmail))
                {
                    ShowError("No email address linked to this account.");
                    return;
                }

                _verificationCode = EmailHelper.GenerateCode();
                _codeExpiry = DateTime.Now.AddMinutes(10);

                _btnSend.Content = "Sending email...";
                await Task.Run(() => EmailHelper.SendVerificationCodeAsync(_userEmail, _displayName, _verificationCode));

                string maskedEmail = MaskEmail(_userEmail);
                ShowSuccess($"Code sent to {maskedEmail}");
                _stepEmail.IsVisible = false;
                _stepCode.IsVisible = true;
            }
            catch (Exception ex)
            {
                ShowError($"Error: {ex.Message}");
            }
            finally
            {
                _btnSend.IsEnabled = true;
                _btnSend.Content = "Send Verification Code";
            }
        }

        private void VerifyCode()
        {
            _errorText.IsVisible = false;
            var entered = (_txtCode.Text ?? "").Trim();

            if (string.IsNullOrWhiteSpace(entered))
            {
                ShowError("Please enter the 6-digit code.");
                return;
            }

            if (DateTime.Now > _codeExpiry)
            {
                ShowError("Code expired. Click 'Resend Code' to get a new one.");
                return;
            }

            if (entered != _verificationCode)
            {
                ShowError("Invalid code. Please try again.");
                return;
            }

            ShowSuccess("Code verified!");
            _stepCode.IsVisible = false;
            _stepNewPass.IsVisible = true;
        }

        private async Task ResetPasswordAsync()
        {
            _errorText.IsVisible = false;
            var newPass = _txtNewPass.Text ?? "";
            var confirm = _txtConfirmPass.Text ?? "";

            if (newPass.Length < 6)
            {
                ShowError("Password must be at least 6 characters.");
                return;
            }

            if (newPass != confirm)
            {
                ShowError("Passwords do not match.");
                return;
            }

            _btnReset.IsEnabled = false;
            _btnReset.Content = "Updating...";

            try
            {
                var user = await DbHelper.GetUser(_userName);
                if (user == null)
                {
                    ShowError("Account not found.");
                    return;
                }

                user["password"] = newPass;
                await DbHelper.SaveUser(_userName, user);

                ShowSuccess("Password updated successfully!");
                await Task.Delay(1500);
                Close();
            }
            catch (Exception ex)
            {
                ShowError($"Error: {ex.Message}");
            }
            finally
            {
                _btnReset.IsEnabled = true;
                _btnReset.Content = "Update Password";
            }
        }

        private static string MaskEmail(string email)
        {
            var parts = email.Split('@');
            if (parts.Length != 2) return email;
            var name = parts[0];
            var masked = name.Length <= 2
                ? name[0] + "***"
                : name[..2] + new string('*', Math.Min(name.Length - 2, 5));
            return masked + "@" + parts[1];
        }

        private void ShowError(string msg)
        {
            _errorText.Text = msg;
            _errorText.Foreground = new SolidColorBrush(Color.Parse("#ef4444"));
            _errorText.IsVisible = true;
        }

        private void ShowSuccess(string msg)
        {
            _errorText.Text = msg;
            _errorText.Foreground = new SolidColorBrush(Color.Parse("#10b981"));
            _errorText.IsVisible = true;
        }

        private static TextBlock MakeLabel(string text) => new()
        {
            Text = text, FontSize = 11,
            Foreground = new SolidColorBrush(Color.Parse("#94a3b8")),
            Margin = new Thickness(0, 4, 0, 2)
        };

        private static TextBox MakeInput(bool isPassword = false) => new()
        {
            Background = new SolidColorBrush(Color.Parse("#1e2a3a")),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#243447")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 10),
            FontSize = 14,
            Height = 44,
            PasswordChar = isPassword ? '●' : '\0'
        };

        private static Button MakeButton(string text, string color) => new()
        {
            Content = text,
            Background = new SolidColorBrush(Color.Parse(color)),
            Foreground = Brushes.White,
            FontWeight = FontWeight.SemiBold,
            FontSize = 14,
            Height = 48,
            Margin = new Thickness(0, 8, 0, 0),
            CornerRadius = new CornerRadius(8),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
    }
}
