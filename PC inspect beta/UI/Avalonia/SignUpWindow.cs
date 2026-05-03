using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PC_inspect_beta.Core;

namespace PC_inspect_beta.UI.Avalonia
{
    public class SignUpWindow : Window
    {
        private TextBox _txtUser = null!, _txtPass = null!, _txtEmail = null!, _txtPhone = null!;
        private DatePicker _dtpBirth = null!;
        private Button _btnReg = null!;
        private TextBlock _errorText = null!;

        public SignUpWindow()
        {
            Title = "Create Account";
            Width = 440;
            Height = 760;
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
                        new GradientStop(Color.Parse("#10b981"), 0),
                        new GradientStop(Color.Parse("#34d399"), 1)
                    }
                },
                Height = 96,
                Padding = new Thickness(20, 20)
            };
            DockPanel.SetDock(header, Dock.Top);

            var headerStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            headerStack.Children.Add(new TextBlock
            {
                Text = "Create Account",
                FontSize = 22,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            headerStack.Children.Add(new TextBlock
            {
                Text = "Join Inspectify LapStore to start selling",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.Parse("#e6fff4")),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            });
            header.Child = headerStack;
            root.Children.Add(header);

            var scroll = new ScrollViewer();
            var form = new StackPanel { Margin = new Thickness(36, 16, 36, 24), Spacing = 12 };

            form.Children.Add(MakeLabel("Username"));
            _txtUser = MakeInput(); form.Children.Add(_txtUser);

            form.Children.Add(MakeLabel("Password"));
            _txtPass = MakeInput(isPassword: true); form.Children.Add(_txtPass);
            form.Children.Add(new TextBlock
            {
                Text = "Must be at least 6 characters",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.Parse("#64748b")),
                Margin = new Thickness(0, -8, 0, 0)
            });

            form.Children.Add(MakeLabel("Email / Gmail"));
            _txtEmail = MakeInput(); form.Children.Add(_txtEmail);

            form.Children.Add(MakeLabel("Phone Number"));
            _txtPhone = MakeInput(); form.Children.Add(_txtPhone);
            form.Children.Add(new TextBlock
            {
                Text = "Format: 03xx-xxxxxxx",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.Parse("#64748b")),
                Margin = new Thickness(0, -8, 0, 0)
            });

            form.Children.Add(MakeLabel("Date of Birth (DD/MM/YYYY)"));
            _dtpBirth = new DatePicker
            {
                MaxYear = DateTimeOffset.Now.AddYears(-10),
                Background = new SolidColorBrush(Color.Parse("#1e2a3a")),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.Parse("#243447")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Height = 44
            };
            form.Children.Add(_dtpBirth);

            _errorText = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.Parse("#ef4444")),
                FontSize = 12,
                IsVisible = false,
                TextWrapping = TextWrapping.Wrap
            };
            form.Children.Add(_errorText);

            _btnReg = new Button
            {
                Content = "Sign Up",
                Background = new SolidColorBrush(Color.Parse("#10b981")),
                Foreground = Brushes.White,
                FontWeight = FontWeight.SemiBold,
                FontSize = 14,
                Height = 48,
                Margin = new Thickness(0, 12, 0, 0),
                CornerRadius = new CornerRadius(8),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            _btnReg.Click += async (_, _) => await DoSignUpAsync();
            form.Children.Add(_btnReg);

            scroll.Content = form;
            root.Children.Add(scroll);
            Content = root;
        }

        private async Task DoSignUpAsync()
        {
            _errorText.IsVisible = false;

            var user = (_txtUser.Text ?? "").Trim();
            var pass = _txtPass.Text ?? "";
            var email = (_txtEmail.Text ?? "").Trim();
            var phone = (_txtPhone.Text ?? "").Trim();

            if (string.IsNullOrWhiteSpace(user)) { ShowError("Username is required."); return; }
            if (pass.Length < 6) { ShowError("Password must be at least 6 characters."); return; }
            if (!IsValidEmail(email)) { ShowError("Please enter a valid email."); return; }
            if (!IsValidPhone(phone)) { ShowError("Please enter a valid phone number (10–15 digits)."); return; }

            _btnReg.IsEnabled = false;
            _btnReg.Content = "Creating…";

            try
            {
                if (await DbHelper.GetUser(user) != null)
                {
                    ShowError("Username already taken.");
                    return;
                }

                await DbHelper.SaveUser(user, new
                {
                    username = user,
                    password = pass,
                    email = email,
                    phone = phone,
                    dob = _dtpBirth.SelectedDate?.ToString("yyyy-MM-dd") ?? ""
                });

                Close(user);
            }
            catch (Exception ex)
            {
                ShowError($"Error: {ex.Message}");
            }
            finally
            {
                _btnReg.IsEnabled = true;
                _btnReg.Content = "Sign Up";
            }
        }

        private void ShowError(string msg) { _errorText.Text = msg; _errorText.IsVisible = true; }

        private static bool IsValidEmail(string s)
        {
            int at = s.IndexOf('@');
            if (at <= 0) return false;
            int dot = s.IndexOf('.', at + 2);
            return dot > 0 && dot < s.Length - 1;
        }

        private static bool IsValidPhone(string s)
        {
            int digits = 0;
            foreach (var c in s)
                if (char.IsDigit(c)) digits++;
                else if (c != ' ' && c != '-' && c != '+' && c != '(' && c != ')') return false;
            return digits >= 10 && digits <= 15;
        }

        private static TextBlock MakeLabel(string text) => new()
        {
            Text = text,
            FontSize = 11,
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
