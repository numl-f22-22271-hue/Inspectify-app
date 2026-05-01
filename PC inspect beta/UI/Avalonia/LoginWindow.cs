#if !WINDOWS
using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PC_inspect_beta.Core;

namespace PC_inspect_beta.UI.Avalonia
{
    public class LoginWindow : Window
    {
        public string? AuthenticatedUsername { get; private set; }

        private TextBox _txtUser = null!;
        private TextBox _txtPass = null!;
        private Button _btnLogin = null!;
        private TextBlock _errorText = null!;

        public LoginWindow()
        {
            Title = "Sign In";
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

            // Header gradient
            var header = new Border
            {
                Background = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(Color.Parse("#2b72d4"), 0),
                        new GradientStop(Color.Parse("#5ea0ff"), 1)
                    }
                },
                Height = 96,
                Padding = new Thickness(20, 20)
            };
            DockPanel.SetDock(header, Dock.Top);

            var headerStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            headerStack.Children.Add(new TextBlock
            {
                Text = "Welcome Back",
                FontSize = 22,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            headerStack.Children.Add(new TextBlock
            {
                Text = "Sign in to publish your listing",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.Parse("#e6e6fa")),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            });
            header.Child = headerStack;
            root.Children.Add(header);

            // Form
            var form = new StackPanel { Margin = new Thickness(36, 24), Spacing = 14 };

            form.Children.Add(new TextBlock
            {
                Text = "Sign in with your Inspectify LapStore account",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.Parse("#94a3b8")),
                HorizontalAlignment = HorizontalAlignment.Center
            });

            // Username
            form.Children.Add(MakeLabel("Username"));
            _txtUser = MakeInput();
            form.Children.Add(_txtUser);

            // Password
            form.Children.Add(MakeLabel("Password"));
            _txtPass = MakeInput(isPassword: true);
            form.Children.Add(_txtPass);

            // Error
            _errorText = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.Parse("#ef4444")),
                FontSize = 12,
                IsVisible = false,
                TextWrapping = TextWrapping.Wrap
            };
            form.Children.Add(_errorText);

            // Login button
            _btnLogin = new Button
            {
                Content = "Log In",
                Background = new SolidColorBrush(Color.Parse("#2b72d4")),
                Foreground = Brushes.White,
                FontWeight = FontWeight.SemiBold,
                FontSize = 14,
                Height = 48,
                Margin = new Thickness(0, 8, 0, 0),
                CornerRadius = new CornerRadius(8),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            _btnLogin.Click += async (_, _) => await DoLoginAsync();
            form.Children.Add(_btnLogin);

            // Links
            var signUpLink = new Button
            {
                Content = "No account?  Create one →",
                Foreground = new SolidColorBrush(Color.Parse("#5ea0ff")),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 0)
            };
            signUpLink.Click += async (_, _) =>
            {
                var w = new SignUpWindow();
                await w.ShowDialog(this);
            };
            form.Children.Add(signUpLink);

            var forgotLink = new Button
            {
                Content = "Forgot Password?",
                Foreground = new SolidColorBrush(Color.Parse("#94a3b8")),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            forgotLink.Click += async (_, _) =>
            {
                var w = new ForgotPasswordWindow();
                await w.ShowDialog(this);
            };
            form.Children.Add(forgotLink);

            root.Children.Add(form);
            Content = root;
        }

        private async Task DoLoginAsync()
        {
            _errorText.IsVisible = false;
            _btnLogin.IsEnabled = false;
            _btnLogin.Content = "Verifying…";

            try
            {
                var user = await DbHelper.GetUser(_txtUser.Text?.Trim() ?? "");
                if (user != null && user["password"]?.ToString() == _txtPass.Text)
                {
                    AuthenticatedUsername = _txtUser.Text?.Trim();
                    Close(true);
                }
                else
                {
                    ShowError("Invalid username or password.");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Network error: {ex.Message}");
            }
            finally
            {
                _btnLogin.IsEnabled = true;
                _btnLogin.Content = "Log In";
            }
        }

        private void ShowError(string msg)
        {
            _errorText.Text = msg;
            _errorText.IsVisible = true;
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
#endif
