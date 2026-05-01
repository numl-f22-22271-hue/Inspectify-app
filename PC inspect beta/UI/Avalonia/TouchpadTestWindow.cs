#if !WINDOWS
using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace PC_inspect_beta.UI.Avalonia
{
    /// <summary>
    /// Touchpad test window. The user moves the cursor over a canvas to draw
    /// a trail; coverage is tracked via a grid of cells. Helps verify touchpad
    /// works edge-to-edge with no dead zones.
    /// </summary>
    public class TouchpadTestWindow : Window
    {
        private const int GridCols = 24;
        private const int GridRows = 12;

        private Canvas _canvas = null!;
        private TextBlock _coverageText = null!;
        private TextBlock _clickText = null!;
        private Button _resetBtn = null!;
        private readonly bool[,] _covered = new bool[GridCols, GridRows];
        private int _leftClicks, _rightClicks;
        private double _cellW, _cellH;

        public TouchpadTestWindow()
        {
            Title = "Touchpad Test";
            Width = 900;
            Height = 600;
            CanResize = true;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.Parse("#16212e"));
            BuildUi();
        }

        private void BuildUi()
        {
            var root = new DockPanel();

            // Header
            var header = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#1e2a3a")),
                Padding = new Thickness(20, 14),
                BorderBrush = new SolidColorBrush(Color.Parse("#243447")),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            DockPanel.SetDock(header, Dock.Top);
            var headStack = new StackPanel { Spacing = 4 };
            headStack.Children.Add(new TextBlock
            {
                Text = "Touchpad Test",
                FontSize = 20,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White
            });
            headStack.Children.Add(new TextBlock
            {
                Text = "Move your finger across every part of the touchpad. Cells turn green where the cursor passed.",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.Parse("#94a3b8"))
            });
            header.Child = headStack;
            root.Children.Add(header);

            // Footer with stats
            var footer = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#1e2a3a")),
                Padding = new Thickness(20, 12),
                BorderBrush = new SolidColorBrush(Color.Parse("#243447")),
                BorderThickness = new Thickness(0, 1, 0, 0)
            };
            DockPanel.SetDock(footer, Dock.Bottom);

            var footStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 24 };
            _coverageText = new TextBlock
            {
                Text = "Coverage: 0%",
                Foreground = new SolidColorBrush(Color.Parse("#5ea0ff")),
                FontSize = 13,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            footStack.Children.Add(_coverageText);

            _clickText = new TextBlock
            {
                Text = "Left clicks: 0  •  Right clicks: 0",
                Foreground = new SolidColorBrush(Color.Parse("#94a3b8")),
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center
            };
            footStack.Children.Add(_clickText);

            _resetBtn = new Button
            {
                Content = "Reset",
                Background = new SolidColorBrush(Color.Parse("#243447")),
                Foreground = Brushes.White,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(20, 8),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            _resetBtn.Click += (_, _) => Reset();
            DockPanel.SetDock(_resetBtn, Dock.Right);
            footer.Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*, Auto"),
                Children = { footStack, AddTo(_resetBtn, col: 1) }
            };
            root.Children.Add(footer);

            // Canvas
            _canvas = new Canvas
            {
                Background = new SolidColorBrush(Color.Parse("#0d1620"))
            };
            _canvas.PointerMoved += OnPointerMoved;
            _canvas.PointerPressed += OnPointerPressed;
            _canvas.LayoutUpdated += (_, _) => DrawGrid();
            root.Children.Add(_canvas);

            Content = root;
        }

        private static Control AddTo(Control c, int col)
        {
            Grid.SetColumn(c, col);
            return c;
        }

        private void DrawGrid()
        {
            _canvas.Children.Clear();
            if (_canvas.Bounds.Width <= 0 || _canvas.Bounds.Height <= 0) return;

            _cellW = _canvas.Bounds.Width / GridCols;
            _cellH = _canvas.Bounds.Height / GridRows;

            for (int x = 0; x < GridCols; x++)
            {
                for (int y = 0; y < GridRows; y++)
                {
                    var cell = new Rectangle
                    {
                        Width = _cellW - 1,
                        Height = _cellH - 1,
                        Fill = _covered[x, y]
                            ? new SolidColorBrush(Color.Parse("#10b981"))
                            : new SolidColorBrush(Color.Parse("#1e2a3a")),
                        Stroke = new SolidColorBrush(Color.Parse("#243447")),
                        StrokeThickness = 0.5
                    };
                    Canvas.SetLeft(cell, x * _cellW);
                    Canvas.SetTop(cell, y * _cellH);
                    _canvas.Children.Add(cell);
                }
            }
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            var pt = e.GetPosition(_canvas);
            int gx = Math.Clamp((int)(pt.X / _cellW), 0, GridCols - 1);
            int gy = Math.Clamp((int)(pt.Y / _cellH), 0, GridRows - 1);

            if (!_covered[gx, gy])
            {
                _covered[gx, gy] = true;
                DrawGrid();
                UpdateCoverage();
            }
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var props = e.GetCurrentPoint(_canvas).Properties;
            if (props.IsLeftButtonPressed) _leftClicks++;
            if (props.IsRightButtonPressed) _rightClicks++;
            _clickText.Text = $"Left clicks: {_leftClicks}  •  Right clicks: {_rightClicks}";
        }

        private void UpdateCoverage()
        {
            int total = GridCols * GridRows;
            int hit = 0;
            for (int x = 0; x < GridCols; x++)
                for (int y = 0; y < GridRows; y++)
                    if (_covered[x, y]) hit++;
            _coverageText.Text = $"Coverage: {hit * 100 / total}%";
        }

        private void Reset()
        {
            for (int x = 0; x < GridCols; x++)
                for (int y = 0; y < GridRows; y++)
                    _covered[x, y] = false;
            _leftClicks = _rightClicks = 0;
            DrawGrid();
            UpdateCoverage();
            _clickText.Text = "Left clicks: 0  •  Right clicks: 0";
        }
    }
}
#endif
