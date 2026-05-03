using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace PC_inspect_beta.UI.Avalonia
{
    public class TouchpadTestWindow : Window
    {
        public bool LeftButtonPressed { get; private set; }
        public bool RightButtonPressed { get; private set; }
        public bool Skipped { get; private set; }
        public bool Finished { get; private set; }
        public bool SubmitRequested { get; private set; }

        private Border _pnlLeft = null!, _pnlRight = null!;
        private TextBlock _lblLeftBox = null!, _lblRightBox = null!;
        private TextBlock _lblLeftStatus = null!, _lblRightStatus = null!;

        private static readonly SolidColorBrush ColIdle = new(Color.Parse("#323238"));
        private static readonly SolidColorBrush ColSuccess = new(Color.Parse("#10b981"));
        private static readonly SolidColorBrush ColBorder = new(Color.Parse("#5a5a69"));

        private readonly bool _externalMouseDetected;

        public TouchpadTestWindow(bool externalMouseDetected = false)
        {
            _externalMouseDetected = externalMouseDetected;
            Title = "Touchpad Test";
            Width = 560;
            Height = 510;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.Parse("#16212e"));
            BuildUi();
        }

        private void BuildUi()
        {
            var root = new StackPanel { Spacing = 8, Margin = new Thickness(20) };

            root.Children.Add(new TextBlock
            {
                Text = "Touchpad Button Test",
                FontSize = 20,
                FontWeight = FontWeight.Bold,
                Foreground = new SolidColorBrush(Color.Parse("#22d3ee")),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0)
            });

            if (_externalMouseDetected)
            {
                root.Children.Add(new Border
                {
                    Background = new SolidColorBrush(Color.Parse("#322800")),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(10, 8),
                    Margin = new Thickness(0, 8, 0, 0),
                    Child = new TextBlock
                    {
                        Text = "⚠  External mouse connected. Disconnect it for accurate results.",
                        FontSize = 11,
                        FontWeight = FontWeight.Bold,
                        Foreground = new SolidColorBrush(Color.Parse("#f59e0b")),
                        HorizontalAlignment = HorizontalAlignment.Center
                    }
                });
            }

            root.Children.Add(new TextBlock
            {
                Text = "Press the LEFT then RIGHT physical touchpad button.\nOnly touchpad clicks are counted — mouse clicks are ignored.",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.Parse("#94a3b8")),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 8, 0, 16)
            });

            var boxRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 60, HorizontalAlignment = HorizontalAlignment.Center };

            _lblLeftBox = new TextBlock
            {
                Text = "LEFT\nButton",
                FontSize = 15,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
            _pnlLeft = new Border
            {
                Width = 200,
                Height = 130,
                Background = ColIdle,
                BorderBrush = ColBorder,
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(8),
                Child = _lblLeftBox
            };
            _pnlLeft.PointerPressed += OnLeftPressed;

            _lblRightBox = new TextBlock
            {
                Text = "RIGHT\nButton",
                FontSize = 15,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
            _pnlRight = new Border
            {
                Width = 200,
                Height = 130,
                Background = ColIdle,
                BorderBrush = ColBorder,
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(8),
                Child = _lblRightBox
            };
            _pnlRight.PointerPressed += OnRightPressed;

            boxRow.Children.Add(_pnlLeft);
            boxRow.Children.Add(_pnlRight);
            root.Children.Add(boxRow);

            var statusRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 60, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) };
            _lblLeftStatus = new TextBlock
            {
                Text = "Waiting for press…",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.Parse("#94a3b8")),
                Width = 200,
                TextAlignment = TextAlignment.Center
            };
            _lblRightStatus = new TextBlock
            {
                Text = "Waiting for press…",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.Parse("#94a3b8")),
                Width = 200,
                TextAlignment = TextAlignment.Center
            };
            statusRow.Children.Add(_lblLeftStatus);
            statusRow.Children.Add(_lblRightStatus);
            root.Children.Add(statusRow);

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 60, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 24, 0, 0) };

            var btnDone = new Button
            {
                Content = "Mark as Done",
                Width = 200,
                Height = 40,
                Background = new SolidColorBrush(Color.Parse("#2b72d4")),
                Foreground = Brushes.White,
                FontWeight = FontWeight.SemiBold,
                CornerRadius = new CornerRadius(6),
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            btnDone.Click += (_, _) => FinishTest();

            var btnSkip = new Button
            {
                Content = "Skip",
                Width = 200,
                Height = 40,
                Background = new SolidColorBrush(Color.Parse("#46464b")),
                Foreground = Brushes.White,
                FontWeight = FontWeight.SemiBold,
                CornerRadius = new CornerRadius(6),
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            btnSkip.Click += (_, _) => { Skipped = true; Close(); };

            btnRow.Children.Add(btnDone);
            btnRow.Children.Add(btnSkip);
            root.Children.Add(btnRow);

            Content = root;
        }

        private void OnLeftPressed(object? sender, PointerPressedEventArgs e)
        {
            var props = e.GetCurrentPoint(_pnlLeft).Properties;
            if (props.IsLeftButtonPressed && !LeftButtonPressed)
            {
                LeftButtonPressed = true;
                _pnlLeft.Background = ColSuccess;
                _lblLeftBox.Text = "LEFT\n✔ Pressed!";
                _lblLeftStatus.Text = "✔ Detected";
                _lblLeftStatus.Foreground = ColSuccess;
                CheckBothPressed();
            }
        }

        private void OnRightPressed(object? sender, PointerPressedEventArgs e)
        {
            var props = e.GetCurrentPoint(_pnlRight).Properties;
            if (props.IsRightButtonPressed && !RightButtonPressed)
            {
                RightButtonPressed = true;
                _pnlRight.Background = ColSuccess;
                _lblRightBox.Text = "RIGHT\n✔ Pressed!";
                _lblRightStatus.Text = "✔ Detected";
                _lblRightStatus.Foreground = ColSuccess;
                CheckBothPressed();
            }
        }

        private void CheckBothPressed()
        {
            if (LeftButtonPressed && RightButtonPressed)
            {
                ShowResult("Both touchpad buttons detected!\nTouchpad PASSED ✔", true);
            }
        }

        private void FinishTest()
        {
            Finished = true;
            bool passed = LeftButtonPressed && RightButtonPressed;
            string msg = passed
                ? "Both buttons PASSED ✔"
                : $"Partial — Left: {(LeftButtonPressed ? "OK" : "Not pressed")}  Right: {(RightButtonPressed ? "OK" : "Not pressed")}";
            ShowResult(msg, passed);
        }

        private void ShowResult(string msg, bool passed)
        {
            Finished = true;
            var msgWindow = new Window
            {
                Title = "Touchpad Result",
                Width = 400,
                Height = 160,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = new SolidColorBrush(Color.Parse("#16212e"))
            };

            msgWindow.Height = 200;

            var stack = new StackPanel { Margin = new Thickness(20), Spacing = 16 };
            stack.Children.Add(new TextBlock
            {
                Text = msg,
                Foreground = passed
                    ? new SolidColorBrush(Color.Parse("#10b981"))
                    : new SolidColorBrush(Color.Parse("#f59e0b")),
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap
            });
            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, HorizontalAlignment = HorizontalAlignment.Center };
            var okBtn = new Button
            {
                Content = "OK",
                Background = new SolidColorBrush(Color.Parse("#2b72d4")),
                Foreground = Brushes.White,
                Width = 100,
                Height = 36,
                CornerRadius = new CornerRadius(6),
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            okBtn.Click += (_, _) => { msgWindow.Close(); Close(); };
            var submitBtn = new Button
            {
                Content = "Upload / Sell Device",
                Background = new SolidColorBrush(Color.Parse("#10b981")),
                Foreground = Brushes.White,
                FontWeight = FontWeight.SemiBold,
                Height = 36,
                Padding = new Thickness(16, 0),
                CornerRadius = new CornerRadius(6),
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            submitBtn.Click += (_, _) => { SubmitRequested = true; msgWindow.Close(); Close(); };
            btnRow.Children.Add(okBtn);
            btnRow.Children.Add(submitBtn);
            stack.Children.Add(btnRow);
            msgWindow.Content = stack;
            msgWindow.ShowDialog(this);
        }
    }
}
