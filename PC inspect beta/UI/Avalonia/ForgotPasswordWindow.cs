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
        private TextBox _txtUser = null!, _txtEmail = null!, _txtNewPass = null!;
        private DatePicker _dtpBirth = null!;
        private TextBlock _errorText = null!;
        private Button _btnReset = null!;

        public ForgotPasswordWindow()
        {
            Title = "Reset Password";
            Width = 440;
            Height = 660;
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
            hs.Children.Add(new TextBlock { Text = "Verify your details to update your password", FontSize = 13, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) });
            header.Child = hs;
            root.Children.Add(header);

            var form = new StackPanel { Margin = new Thickness(36, 24), Spacing = 14 };

            form.Children.Add(MakeLabel("Username"));
            _txtUser = MakeInput(); form.Children.Add(_txtUser);

            form.Children.Add(MakeLabel("Verify Email"));
            _txtEmail = MakeInput(); form.Children.Add(_txtEmail);

            form.Children.Add(MakeLabel("Verify Date of Birth (DD/MM/YYYY)"));
            _dtpBirth = new DatePicker
            {
                Background = new SolidColorBrush(Color.Parse("#1e2a3a")),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.Parse("#243447")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Height = 44
            };
            form.Children.Add(_dtpBirth);

            form.Children.Add(MakeLabel("New Password"));
            _txtNewPass = MakeInput(isPassword: true); form.Children.Add(_txtNewPass);

            _errorText = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.Parse("#ef4444")),
                FontSize = 12,
                IsVisible = false,
                TextWrapping = TextWrapping.Wrap
            };
            form.Children.Add(_errorText);

            _btnReset = new Button
            {
                Content = "Update Password",
                Background = new SolidColorBrush(Color.Parse("#f59e0b")),
                Foreground = Brushes.White,
                FontWeight = FontWeight.SemiBold,
                FontSize = 14,
                Height = 48,
                Margin = new Thickness(0, 8, 0, 0),
                CornerRadius = new CornerRadius(8),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            _btnReset.Click += async (_, _) => await DoResetAsync();
            form.Children.Add(_btnReset);

            root.Children.Add(form);
            Content = root;
        }

        private async Task DoResetAsync()
        {
            _errorText.IsVisible = false;

            var user = (_txtUser.Text ?? "").Trim();
            var email = (_txtEmail.Text ?? "").Trim();
            var newPass = _txtNewPass.Text ?? "";
            var dob = _dtpBirth.SelectedDate?.ToString("yyyy-MM-dd") ?? "";

            if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(email))
            { ShowError("Username and email are required."); return; }
            if (string.IsNullOrEmpty(dob))
            { ShowError("Please select your date of birth."); return; }
            if (newPass.Length < 6)
            { ShowError("Password must be at least 6 characters."); return; }

            _btnReset.IsEnabled = false;
            _btnReset.Content = "Verifying...";

            try
            {
                var u = await DbHelper.GetUser(user);
                if (u != null &&
                    u["email"]?.ToString() == email &&
                    u["dob"]?.ToString() == dob)
                {
                    u["password"] = newPass;
                    await DbHelper.SaveUser(user, u);

                    ShowError("Password updated!");
                    _errorText.Foreground = new SolidColorBrush(Color.Parse("#10b981"));
                    await Task.Delay(1200);
                    Close();
                }
                else
                {
                    ShowError("Verification failed. Check username, email, and date of birth.");
                }
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

        private void ShowError(string msg) { _errorText.Text = msg; _errorText.IsVisible = true; }

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
    }
}
