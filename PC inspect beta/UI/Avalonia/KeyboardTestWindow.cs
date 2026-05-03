using System;
using System.Collections.Generic;
using System.Linq;
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
        public List<string> FailedKeys { get; private set; } = new();
        public bool Finished { get; private set; }

        private readonly Dictionary<string, Border> _keyMap = new();
        private readonly HashSet<string> _pressed = new();
        private TextBlock _counter = null!;

        private static readonly bool IsMac = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        private static readonly SolidColorBrush ColUnpressed = new(Color.Parse("#243447"));
        private static readonly SolidColorBrush ColPressed = new(Color.Parse("#10b981"));
        private static readonly SolidColorBrush ColMissed = new(Color.Parse("#dc3545"));
        private static readonly SolidColorBrush ColSkipped = new(Color.Parse("#282832"));

        private static readonly HashSet<string> OptionalKeys = new()
        {
            "INS", "DEL", "HOME", "END", "PGUP", "PGDN",
            "LCTRL", "RCTRL", "LSHIFT", "RSHIFT", "LWIN", "LALT", "RALT",
            "FN", "⌃", "⌥", "⌘"
        };

        private static readonly (string Label, string Id, int Width)[][] WindowsRows =
        {
            new[] {
                ("Esc","ESC",0),("F1","F1",0),("F2","F2",0),("F3","F3",0),
                ("F4","F4",0),("F5","F5",0),("F6","F6",0),("F7","F7",0),
                ("F8","F8",0),("F9","F9",0),("F10","F10",0),("F11","F11",0),("F12","F12",0)
            },
            new[] {
                ("`","OEMTILDE",0),("1","D1",0),("2","D2",0),("3","D3",0),
                ("4","D4",0),("5","D5",0),("6","D6",0),("7","D7",0),
                ("8","D8",0),("9","D9",0),("0","D0",0),("-","OEMMINUS",0),
                ("=","OEMPLUS",0),("Backspace","BACK",100)
            },
            new[] {
                ("Tab","TAB",75),("Q","Q",0),("W","W",0),("E","E",0),
                ("R","R",0),("T","T",0),("Y","Y",0),("U","U",0),
                ("I","I",0),("O","O",0),("P","P",0),("[","OEMOPENBRACK",0),
                ("]","OEMCLOSEBRACK",0),("\\","OEMPIPE",0)
            },
            new[] {
                ("Caps","CAPSLOCK",80),("A","A",0),("S","S",0),("D","D",0),
                ("F","F",0),("G","G",0),("H","H",0),("J","J",0),
                ("K","K",0),("L","L",0),(";","OEMSEMICOLON",0),
                ("'","OEMQUOTES",0),("Enter","ENTER",100)
            },
            new[] {
                ("LShift","LSHIFT",110),("Z","Z",0),("X","X",0),("C","C",0),
                ("V","V",0),("B","B",0),("N","N",0),("M","M",0),
                (",","OEMCOMMA",0),(".","OEMPERIOD",0),("/","OEMQUESTION",0),
                ("RShift","RSHIFT",110)
            },
            new[] {
                ("LCtrl","LCTRL",85),("LWin","LWIN",65),("LAlt","LALT",65),
                ("Space","SPACE",360),
                ("RAlt","RALT",65),("Fn","FN",65),("RCtrl","RCTRL",85)
            },
            new[] {
                ("Up","UP",58),("Down","DOWN",58),("Left","LEFT",58),("Right","RIGHT",58),
                ("Ins","INS",0),("Del","DEL",0),
                ("Home","HOME",0),("End","END",0),("PgUp","PGUP",0),("PgDn","PGDN",0)
            }
        };

        private static readonly (string Label, string Id, int Width)[][] MacRows =
        {
            new[] {
                ("Esc","ESC",0),("F1","F1",0),("F2","F2",0),("F3","F3",0),
                ("F4","F4",0),("F5","F5",0),("F6","F6",0),("F7","F7",0),
                ("F8","F8",0),("F9","F9",0),("F10","F10",0),("F11","F11",0),("F12","F12",0)
            },
            new[] {
                ("`","OEMTILDE",0),("1","D1",0),("2","D2",0),("3","D3",0),
                ("4","D4",0),("5","D5",0),("6","D6",0),("7","D7",0),
                ("8","D8",0),("9","D9",0),("0","D0",0),("-","OEMMINUS",0),
                ("=","OEMPLUS",0),("Delete","BACK",100)
            },
            new[] {
                ("Tab","TAB",75),("Q","Q",0),("W","W",0),("E","E",0),
                ("R","R",0),("T","T",0),("Y","Y",0),("U","U",0),
                ("I","I",0),("O","O",0),("P","P",0),("[","OEMOPENBRACK",0),
                ("]","OEMCLOSEBRACK",0),("\\","OEMPIPE",0)
            },
            new[] {
                ("Caps","CAPSLOCK",80),("A","A",0),("S","S",0),("D","D",0),
                ("F","F",0),("G","G",0),("H","H",0),("J","J",0),
                ("K","K",0),("L","L",0),(";","OEMSEMICOLON",0),
                ("'","OEMQUOTES",0),("Return","ENTER",100)
            },
            new[] {
                ("LShift","LSHIFT",110),("Z","Z",0),("X","X",0),("C","C",0),
                ("V","V",0),("B","B",0),("N","N",0),("M","M",0),
                (",","OEMCOMMA",0),(".","OEMPERIOD",0),("/","OEMQUESTION",0),
                ("RShift","RSHIFT",110)
            },
            new[] {
                ("Fn","FN",65),("⌃","LCTRL",65),("⌥","LALT",65),
                ("⌘","LWIN",72),("Space","SPACE",280),
                ("⌘","RWIN",72),("⌥","RALT",65)
            },
            new[] {
                ("Up","UP",58),("Down","DOWN",58),("Left","LEFT",58),("Right","RIGHT",58)
            }
        };

        public KeyboardTestWindow()
        {
            Title = "Keyboard Test — Press every key";
            Width = 1100;
            Height = 560;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.Parse("#16212e"));
            Focusable = true;
            BuildUi();
            KeyDown += OnKeyDown;
        }

        private void BuildUi()
        {
            var root = new DockPanel();

            var header = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#1e2a3a")),
                Padding = new Thickness(12, 10)
            };
            DockPanel.SetDock(header, Dock.Top);
            var headStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 20 };
            headStack.Children.Add(new TextBlock
            {
                Text = "Press every key on your keyboard. Green = detected. Grey = not pressed.",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.Parse("#94a3b8")),
                VerticalAlignment = VerticalAlignment.Center
            });
            _counter = new TextBlock
            {
                Text = "0 keys pressed",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.Parse("#5ea0ff")),
                VerticalAlignment = VerticalAlignment.Center
            };
            headStack.Children.Add(_counter);
            header.Child = headStack;
            root.Children.Add(header);

            var finishBtn = new Button
            {
                Content = "  ✔  Finish Keyboard Test  ",
                Background = new SolidColorBrush(Color.Parse("#2b72d4")),
                Foreground = Brushes.White,
                FontWeight = FontWeight.SemiBold,
                FontSize = 14,
                Height = 46,
                Margin = new Thickness(8),
                CornerRadius = new CornerRadius(8),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            finishBtn.Click += (_, _) => FinishTest();
            DockPanel.SetDock(finishBtn, Dock.Bottom);
            root.Children.Add(finishBtn);

            var rows = IsMac ? MacRows : WindowsRows;
            var keysPanel = new StackPanel { Spacing = 4, Margin = new Thickness(8, 8) };
            foreach (var row in rows)
            {
                var rowPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
                foreach (var (label, id, widthOverride) in row)
                {
                    var keyBox = MakeKey(label, widthOverride);
                    if (!_keyMap.ContainsKey(id))
                        _keyMap[id] = keyBox;
                    rowPanel.Children.Add(keyBox);
                }
                keysPanel.Children.Add(rowPanel);
            }
            root.Children.Add(keysPanel);

            Content = root;
        }

        private static Border MakeKey(string label, int widthOverride)
        {
            int w = widthOverride > 0 ? widthOverride : 58;
            return new Border
            {
                Width = w,
                Height = 50,
                Background = ColUnpressed,
                BorderBrush = new SolidColorBrush(Color.Parse("#556575")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Child = new TextBlock
                {
                    Text = label,
                    Foreground = Brushes.White,
                    FontSize = label.Length > 3 ? 9 : 12,
                    FontWeight = FontWeight.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            var id = KeyToId(e.Key);
            if (id != null && _keyMap.TryGetValue(id, out var box))
            {
                box.Background = ColPressed;
                _pressed.Add(id);
                _counter.Text = $"{_pressed.Count} key(s) pressed";
            }
            e.Handled = true;
        }

        private void FinishTest()
        {
            FailedKeys = _keyMap
                .Where(kv => !_pressed.Contains(kv.Key) && !OptionalKeys.Contains(kv.Key))
                .Select(kv => kv.Key)
                .ToList();

            foreach (var kv in _keyMap)
            {
                if (_pressed.Contains(kv.Key)) continue;
                kv.Value.Background = OptionalKeys.Contains(kv.Key) ? ColSkipped : ColMissed;
            }

            Finished = true;

            var msgWindow = new Window
            {
                Title = "Keyboard Result",
                Width = 420,
                Height = 180,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = new SolidColorBrush(Color.Parse("#16212e"))
            };

            string msg = FailedKeys.Count == 0
                ? "All keys detected — Keyboard PASSED ✔"
                : $"{FailedKeys.Count} key(s) not detected:\n{string.Join(", ", FailedKeys)}";

            var stack = new StackPanel { Margin = new Thickness(20), Spacing = 16 };
            stack.Children.Add(new TextBlock
            {
                Text = msg,
                Foreground = FailedKeys.Count == 0
                    ? new SolidColorBrush(Color.Parse("#10b981"))
                    : new SolidColorBrush(Color.Parse("#f59e0b")),
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap
            });
            var okBtn = new Button
            {
                Content = "OK",
                Background = new SolidColorBrush(Color.Parse("#2b72d4")),
                Foreground = Brushes.White,
                Width = 100,
                Height = 36,
                CornerRadius = new CornerRadius(6),
                HorizontalAlignment = HorizontalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            okBtn.Click += (_, _) => { msgWindow.Close(); Close(); };
            stack.Children.Add(okBtn);
            msgWindow.Content = stack;
            msgWindow.ShowDialog(this);
        }

        private static string? KeyToId(Key key)
        {
            return key switch
            {
                Key.Escape => "ESC",
                Key.F1 => "F1", Key.F2 => "F2", Key.F3 => "F3", Key.F4 => "F4",
                Key.F5 => "F5", Key.F6 => "F6", Key.F7 => "F7", Key.F8 => "F8",
                Key.F9 => "F9", Key.F10 => "F10", Key.F11 => "F11", Key.F12 => "F12",
                Key.OemTilde => "OEMTILDE",
                Key.D1 => "D1", Key.D2 => "D2", Key.D3 => "D3", Key.D4 => "D4",
                Key.D5 => "D5", Key.D6 => "D6", Key.D7 => "D7", Key.D8 => "D8",
                Key.D9 => "D9", Key.D0 => "D0",
                Key.OemMinus => "OEMMINUS", Key.OemPlus => "OEMPLUS",
                Key.Back => "BACK",
                Key.Tab => "TAB",
                Key.Q => "Q", Key.W => "W", Key.E => "E", Key.R => "R", Key.T => "T",
                Key.Y => "Y", Key.U => "U", Key.I => "I", Key.O => "O", Key.P => "P",
                Key.OemOpenBrackets => "OEMOPENBRACK",
                Key.OemCloseBrackets => "OEMCLOSEBRACK",
                Key.OemPipe or Key.OemBackslash => "OEMPIPE",
                Key.CapsLock => "CAPSLOCK",
                Key.A => "A", Key.S => "S", Key.D => "D", Key.F => "F", Key.G => "G",
                Key.H => "H", Key.J => "J", Key.K => "K", Key.L => "L",
                Key.OemSemicolon => "OEMSEMICOLON",
                Key.OemQuotes => "OEMQUOTES",
                Key.Enter => "ENTER",
                Key.LeftShift => "LSHIFT", Key.RightShift => "RSHIFT",
                Key.Z => "Z", Key.X => "X", Key.C => "C", Key.V => "V",
                Key.B => "B", Key.N => "N", Key.M => "M",
                Key.OemComma => "OEMCOMMA", Key.OemPeriod => "OEMPERIOD",
                Key.OemQuestion => "OEMQUESTION",
                Key.LeftCtrl => "LCTRL", Key.RightCtrl => "RCTRL",
                Key.LeftAlt => "LALT", Key.RightAlt => "RALT",
                Key.LWin => IsMac ? "LWIN" : "LWIN",
                Key.RWin => IsMac ? "RWIN" : "LWIN",
                Key.Space => "SPACE",
                Key.Up => "UP", Key.Down => "DOWN", Key.Left => "LEFT", Key.Right => "RIGHT",
                Key.Insert => "INS", Key.Delete => "DEL",
                Key.Home => "HOME", Key.End => "END",
                Key.PageUp => "PGUP", Key.PageDown => "PGDN",
                _ => null
            };
        }
    }
}
