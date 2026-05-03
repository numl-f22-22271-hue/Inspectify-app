using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace PC_inspect_beta.UI.Avalonia
{
    public class KeyboardTestWindow : Window
    {
        private readonly Dictionary<string, Border> _keys = new();
        private readonly HashSet<string> _pressed = new();
        private TextBlock _counter = null!;
        private TextBlock _hint = null!;

        private static readonly bool IsMac = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        private static readonly string[][] WindowsRows = new[]
        {
            new[] { "Esc", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12" },
            new[] { "`", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-", "=", "Back" },
            new[] { "Tab", "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P", "[", "]", "\\" },
            new[] { "Caps", "A", "S", "D", "F", "G", "H", "J", "K", "L", ";", "'", "Enter" },
            new[] { "Shift", "Z", "X", "C", "V", "B", "N", "M", ",", ".", "/", "Shift" },
            new[] { "Ctrl", "Win", "Alt", "Space", "Alt", "Fn", "Ctrl", "←", "↑", "↓", "→" }
        };

        private static readonly string[][] MacRows = new[]
        {
            new[] { "Esc", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12" },
            new[] { "`", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-", "=", "Delete" },
            new[] { "Tab", "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P", "[", "]", "\\" },
            new[] { "Caps", "A", "S", "D", "F", "G", "H", "J", "K", "L", ";", "'", "Return" },
            new[] { "Shift", "Z", "X", "C", "V", "B", "N", "M", ",", ".", "/", "Shift" },
            new[] { "Fn", "⌃", "⌥", "⌘", "Space", "⌘", "⌥", "←", "↑", "↓", "→" }
        };

        public KeyboardTestWindow()
        {
            Title = "Keyboard Test";
            Width = 1100;
            Height = 460;
            CanResize = true;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.Parse("#16212e"));
            Focusable = true;
            BuildUi();
            KeyDown += OnKeyDown;
        }

        private void BuildUi()
        {
            var root = new DockPanel();

            var header = new StackPanel
            {
                Background = new SolidColorBrush(Color.Parse("#1e2a3a")),
                Orientation = Orientation.Vertical,
                Spacing = 4
            };
            DockPanel.SetDock(header, Dock.Top);
            var headBox = new Border { Padding = new Thickness(20, 14), Child = header };
            DockPanel.SetDock(headBox, Dock.Top);

            header.Children.Add(new TextBlock
            {
                Text = "Keyboard Test",
                FontSize = 20,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White
            });

            _hint = new TextBlock
            {
                Text = "Press every key on your keyboard. Pressed keys turn green.",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.Parse("#94a3b8"))
            };
            header.Children.Add(_hint);

            _counter = new TextBlock
            {
                Text = "0 keys pressed",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.Parse("#5ea0ff")),
                Margin = new Thickness(0, 4, 0, 0)
            };
            header.Children.Add(_counter);
            root.Children.Add(headBox);

            var resetBtn = new Button
            {
                Content = "Reset",
                Background = new SolidColorBrush(Color.Parse("#243447")),
                Foreground = Brushes.White,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(20, 8),
                Margin = new Thickness(20, 8),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            resetBtn.Click += (_, _) => Reset();
            DockPanel.SetDock(resetBtn, Dock.Bottom);
            root.Children.Add(resetBtn);

            var rows = IsMac ? MacRows : WindowsRows;
            var keysPanel = new StackPanel { Spacing = 6, Margin = new Thickness(20, 12) };
            foreach (var row in rows)
            {
                var rowPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
                foreach (var key in row)
                {
                    var keyBox = MakeKey(key);
                    _keys[NormalizeKey(key)] = keyBox;
                    rowPanel.Children.Add(keyBox);
                }
                keysPanel.Children.Add(rowPanel);
            }
            root.Children.Add(keysPanel);

            Content = root;
        }

        private Border MakeKey(string label)
        {
            int width = 56;
            if (label == "Space") width = IsMac ? 280 : 320;
            else if (label == "Back" || label == "Delete" || label == "Tab" || label == "Caps"
                     || label == "Enter" || label == "Return" || label == "Shift")
                width = 90;
            else if (label == "⌘") width = 72;

            return new Border
            {
                Width = width,
                Height = 56,
                Background = new SolidColorBrush(Color.Parse("#243447")),
                BorderBrush = new SolidColorBrush(Color.Parse("#34455a")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Child = new TextBlock
                {
                    Text = label,
                    Foreground = Brushes.White,
                    FontSize = 13,
                    FontWeight = FontWeight.Medium,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            var name = NormalizeKey(KeyToLabel(e.Key));
            if (_keys.TryGetValue(name, out var box))
            {
                box.Background = new SolidColorBrush(Color.Parse("#10b981"));
                _pressed.Add(name);
                _counter.Text = $"{_pressed.Count} unique key(s) pressed";
            }
            e.Handled = true;
        }

        private void Reset()
        {
            foreach (var k in _keys.Values)
                k.Background = new SolidColorBrush(Color.Parse("#243447"));
            _pressed.Clear();
            _counter.Text = "0 keys pressed";
        }

        private static string NormalizeKey(string raw) => raw.ToUpperInvariant().Trim();

        private static string KeyToLabel(Key key)
        {
            if (IsMac)
            {
                return key switch
                {
                    Key.Escape => "Esc",
                    Key.Tab => "Tab",
                    Key.CapsLock => "Caps",
                    Key.LeftShift or Key.RightShift => "Shift",
                    Key.LeftCtrl or Key.RightCtrl => "⌃",
                    Key.LeftAlt or Key.RightAlt => "⌥",
                    Key.LWin or Key.RWin => "⌘",
                    Key.Back => "Delete",
                    Key.Enter => "Return",
                    Key.Space => "Space",
                    Key.Up => "↑",
                    Key.Down => "↓",
                    Key.Left => "←",
                    Key.Right => "→",
                    Key.OemMinus => "-",
                    Key.OemPlus => "=",
                    Key.OemOpenBrackets => "[",
                    Key.OemCloseBrackets => "]",
                    Key.OemPipe or Key.OemBackslash => "\\",
                    Key.OemSemicolon => ";",
                    Key.OemQuotes => "'",
                    Key.OemComma => ",",
                    Key.OemPeriod => ".",
                    Key.OemQuestion => "/",
                    Key.OemTilde => "`",
                    Key.D0 or Key.NumPad0 => "0",
                    Key.D1 or Key.NumPad1 => "1",
                    Key.D2 or Key.NumPad2 => "2",
                    Key.D3 or Key.NumPad3 => "3",
                    Key.D4 or Key.NumPad4 => "4",
                    Key.D5 or Key.NumPad5 => "5",
                    Key.D6 or Key.NumPad6 => "6",
                    Key.D7 or Key.NumPad7 => "7",
                    Key.D8 or Key.NumPad8 => "8",
                    Key.D9 or Key.NumPad9 => "9",
                    _ => key.ToString().ToUpperInvariant()
                };
            }

            return key switch
            {
                Key.Escape => "Esc",
                Key.Tab => "Tab",
                Key.CapsLock => "Caps",
                Key.LeftShift or Key.RightShift => "Shift",
                Key.LeftCtrl or Key.RightCtrl => "Ctrl",
                Key.LeftAlt or Key.RightAlt => "Alt",
                Key.LWin or Key.RWin => "Win",
                Key.Back => "Back",
                Key.Enter => "Enter",
                Key.Space => "Space",
                Key.Up => "↑",
                Key.Down => "↓",
                Key.Left => "←",
                Key.Right => "→",
                Key.OemMinus => "-",
                Key.OemPlus => "=",
                Key.OemOpenBrackets => "[",
                Key.OemCloseBrackets => "]",
                Key.OemPipe or Key.OemBackslash => "\\",
                Key.OemSemicolon => ";",
                Key.OemQuotes => "'",
                Key.OemComma => ",",
                Key.OemPeriod => ".",
                Key.OemQuestion => "/",
                Key.OemTilde => "`",
                Key.D0 or Key.NumPad0 => "0",
                Key.D1 or Key.NumPad1 => "1",
                Key.D2 or Key.NumPad2 => "2",
                Key.D3 or Key.NumPad3 => "3",
                Key.D4 or Key.NumPad4 => "4",
                Key.D5 or Key.NumPad5 => "5",
                Key.D6 or Key.NumPad6 => "6",
                Key.D7 or Key.NumPad7 => "7",
                Key.D8 or Key.NumPad8 => "8",
                Key.D9 or Key.NumPad9 => "9",
                _ => key.ToString().ToUpperInvariant()
            };
        }
    }
}
