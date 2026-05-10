using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using PC_inspect_beta.Core;
using PC_inspect_beta.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using SixImage = SixLabors.ImageSharp.Image;
using Color = Avalonia.Media.Color;

namespace PC_inspect_beta.UI.Avalonia
{
    public class AdPostWindow : Window
    {
        private readonly string _username;
        private readonly ScanResult _scan;
        private readonly List<string> _selectedFiles = new();

        private TextBox _txtTitle = null!, _txtPrice = null!, _txtDesc = null!;
        private NumericUpDown _numYearsUsed = null!;
        private ComboBox _cmbCondition = null!;
        private TextBlock _lblFileCount = null!, _statusText = null!, _lblPrediction = null!;
        private ProgressBar _pBar = null!;
        private Button _btnPost = null!, _btnPhoto = null!;

        private const int MAX_PHOTOS = 5;

        public AdPostWindow(string username, ScanResult scan)
        {
            _username = username;
            _scan = scan;
            Title = "Create Marketplace Listing";
            Width = 560;
            MinWidth = 420;
            MinHeight = 480;
            CanResize = true;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.Parse("#16212e"));
            BuildUi();
            Opened += (_, _) =>
            {
                var screen = Screens.ScreenFromWindow(this);
                double available = (screen?.WorkingArea.Height ?? 800) / (screen?.Scaling ?? 1.0);
                Height = Math.Min(750, available - 40);
            };
        }

        private void BuildUi()
        {
            var root = new DockPanel();

            // Header
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
                Height = 80,
                Padding = new Thickness(20, 14)
            };
            DockPanel.SetDock(header, Dock.Top);

            var hs = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            hs.Children.Add(new TextBlock
            {
                Text = "Sell Your Device",
                FontSize = 18,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            hs.Children.Add(new TextBlock
            {
                Text = "List your scanned device on Inspectify LapStore",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.Parse("#e6e6fa")),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            });
            header.Child = hs;
            root.Children.Add(header);

            var bottomPanel = new StackPanel { Margin = new Thickness(20, 6, 20, 10) };
            DockPanel.SetDock(bottomPanel, Dock.Bottom);

            _statusText = new TextBlock
            {
                Text = "",
                Foreground = new SolidColorBrush(Color.Parse("#94a3b8")),
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 4)
            };
            bottomPanel.Children.Add(_statusText);

            _pBar = new ProgressBar
            {
                Height = 6,
                Minimum = 0,
                Maximum = 100,
                IsVisible = false,
                Foreground = new SolidColorBrush(Color.Parse("#2b72d4")),
                Margin = new Thickness(0, 0, 0, 8)
            };
            bottomPanel.Children.Add(_pBar);

            _btnPost = new Button
            {
                Content = "Publish Listing",
                Background = new SolidColorBrush(Color.Parse("#10b981")),
                Foreground = Brushes.White,
                FontWeight = FontWeight.SemiBold,
                FontSize = 14,
                Height = 46,
                CornerRadius = new CornerRadius(8),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            _btnPost.Click += async (_, _) => await OnPublishAsync();
            bottomPanel.Children.Add(_btnPost);

            root.Children.Add(bottomPanel);

            // Scrollable form
            var scroll = new ScrollViewer { Padding = new Thickness(20, 12) };
            var form = new StackPanel { Spacing = 8 };

            // Title (auto-filled)
            form.Children.Add(MakeLabel("Product Title"));
            _txtTitle = MakeInput();
#if WINDOWS
            _txtTitle.Text = PricePredictor.BuildProductTitle(_scan?.Metadata);
#else
            _txtTitle.Text = BuildFallbackTitle(_scan?.Metadata);
#endif
            form.Children.Add(_txtTitle);

            // Price
            form.Children.Add(MakeLabel("Price (PKR)"));
            _txtPrice = MakeInput();
            form.Children.Add(_txtPrice);

            // Price prediction hint
            _lblPrediction = new TextBlock
            {
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.Parse("#94a3b8")),
                IsVisible = false,
                Margin = new Thickness(0, -4, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            form.Children.Add(_lblPrediction);
            ShowPricePrediction();

            // Condition + Years Used side by side
            var condRow = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*, 12, *"),
                Margin = new Thickness(0, 4, 0, 0)
            };

            var condStack = new StackPanel();
            condStack.Children.Add(MakeLabel("Condition"));
            _cmbCondition = new ComboBox
            {
                Background = new SolidColorBrush(Color.Parse("#1e2a3a")),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.Parse("#243447")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 8),
                Height = 44,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                ItemsSource = new[] { "New", "Like New", "Used", "For Parts" },
                SelectedIndex = 0
            };
            _cmbCondition.SelectionChanged += (_, _) =>
            {
                if (_cmbCondition.SelectedItem?.ToString() == "New")
                    _numYearsUsed.Value = 0;
            };
            condStack.Children.Add(_cmbCondition);
            Grid.SetColumn(condStack, 0);
            condRow.Children.Add(condStack);

            var yearsStack = new StackPanel();
            yearsStack.Children.Add(MakeLabel("Years Used"));
            _numYearsUsed = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 20,
                Value = 0,
                Background = new SolidColorBrush(Color.Parse("#1e2a3a")),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.Parse("#243447")),
                BorderThickness = new Thickness(1),
                Height = 44,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            yearsStack.Children.Add(_numYearsUsed);
            Grid.SetColumn(yearsStack, 2);
            condRow.Children.Add(yearsStack);

            form.Children.Add(condRow);

            // Description
            form.Children.Add(MakeLabel("Description"));
            _txtDesc = MakeInput(multiline: true);
            form.Children.Add(_txtDesc);

            // Detected specs (read-only)
            string specs = BuildSpecSnippet();
            if (!string.IsNullOrEmpty(specs))
            {
                form.Children.Add(MakeLabel("Detected Specs (from scan)", accent: true));
                var specsBox = new TextBox
                {
                    Text = specs,
                    IsReadOnly = true,
                    AcceptsReturn = true,
                    Height = 64,
                    Background = new SolidColorBrush(Color.Parse("#0d1620")),
                    Foreground = new SolidColorBrush(Color.Parse("#e2e8f0")),
                    BorderBrush = new SolidColorBrush(Color.Parse("#243447")),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(12, 10),
                    FontSize = 12,
                    FontFamily = new FontFamily("JetBrains Mono, Menlo, Consolas, monospace")
                };
                form.Children.Add(specsBox);
            }

            // Divider
            form.Children.Add(new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Color.Parse("#243447")),
                Margin = new Thickness(0, 6)
            });

            // Photo selector
            form.Children.Add(MakeLabel($"Photos  (max {MAX_PHOTOS} · auto-compressed to 100 KB)"));

            var photoRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            _btnPhoto = new Button
            {
                Content = "Select Photos",
                Background = new SolidColorBrush(Color.Parse("#2b72d4")),
                Foreground = Brushes.White,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 10),
                Height = 40
            };
            _btnPhoto.Click += async (_, _) => await OnSelectPhotosAsync();
            photoRow.Children.Add(_btnPhoto);

            _lblFileCount = new TextBlock
            {
                Text = "No photos selected (optional)",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.Parse("#94a3b8")),
                VerticalAlignment = VerticalAlignment.Center
            };
            photoRow.Children.Add(_lblFileCount);
            form.Children.Add(photoRow);

            scroll.Content = form;
            root.Children.Add(scroll);
            Content = root;
        }

        private async Task OnSelectPhotosAsync()
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = $"Select up to {MAX_PHOTOS} photos",
                AllowMultiple = true,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Images") { Patterns = new[] { "*.jpg", "*.jpeg", "*.png" } }
                }
            });

            if (files == null || files.Count == 0) return;

            _selectedFiles.Clear();
            foreach (var f in files.Take(MAX_PHOTOS))
            {
                var path = f.TryGetLocalPath();
                if (!string.IsNullOrEmpty(path))
                    _selectedFiles.Add(path);
            }

            _lblFileCount.Text = $"{_selectedFiles.Count} photo(s) selected";
            _lblFileCount.Foreground = new SolidColorBrush(Color.Parse("#10b981"));
        }

        private async Task OnPublishAsync()
        {
            if (string.IsNullOrWhiteSpace(_txtTitle.Text) ||
                string.IsNullOrWhiteSpace(_txtPrice.Text))
            {
                ShowStatus("Title and Price are required.", true);
                return;
            }

            _btnPost.IsEnabled = false;
            _btnPost.Content = "Uploading...";
            _pBar.IsVisible = true;
            _pBar.IsIndeterminate = true;

            try
            {
                var imageUrls = new List<string>();

                if (_selectedFiles.Count > 0)
                {
                    _pBar.IsIndeterminate = false;
                    _pBar.Value = 0;

                    for (int i = 0; i < _selectedFiles.Count; i++)
                    {
                        string file = _selectedFiles[i];

                        ShowStatus($"Compressing photo {i + 1} of {_selectedFiles.Count}...", false);
                        using var compressed = await CompressImageAsync(file, targetKb: 100);

                        double kb = compressed.Length / 1024.0;
                        ShowStatus($"Uploading photo {i + 1} of {_selectedFiles.Count}  ({kb:F0} KB)...", false);

                        string key = $"ads/{_username}_{DateTime.Now.Ticks}_{Path.GetFileName(file)}.jpg";
                        string url = await DbHelper.UploadStream(_username, compressed, key);
                        imageUrls.Add(url);

                        _pBar.Value = (int)((i + 1) / (double)_selectedFiles.Count * 80);
                    }
                }

                ShowStatus("Saving listing...", false);
                _pBar.Value = 85;

                var ad = new Dictionary<string, object>();
                if (_scan?.Metadata != null)
                {
                    foreach (var kv in _scan.Metadata)
                        ad["sys_" + kv.Key] = kv.Value;
                }

                ad["title"] = _txtTitle.Text.Trim();
                ad["price"] = _txtPrice.Text.Trim();
                ad["desc"] = _txtDesc.Text?.Trim() ?? "";
                ad["condition"] = _cmbCondition.SelectedItem?.ToString() ?? "";
                ad["years_used"] = (int)(_numYearsUsed.Value ?? 0);
                ad["user"] = _username;
                ad["date"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                ad["images"] = imageUrls;

#if HAS_ML
                try
                {
                    float predicted = PricePredictor.Predict(
                        _scan?.Metadata, _cmbCondition.SelectedItem?.ToString());
                    if (predicted > 0f)
                    {
                        ad["predicted_price_min"] = (long)Math.Round(predicted * 0.90f);
                        ad["predicted_price_max"] = (long)Math.Round(predicted * 1.10f);
                    }
                }
                catch { }
#endif

                await DbHelper.SaveAdPost(_username, ad);

                _pBar.Value = 95;

                try
                {
                    var user = await DbHelper.GetUser(_username);
                    var email = user?["email"]?.ToString();
                    var displayName = user?["fullName"]?.ToString() ?? user?["full_name"]?.ToString() ?? _username;
                    if (!string.IsNullOrWhiteSpace(email))
                    {
                        ShowStatus("Sending confirmation email...", false);
                        await Task.Run(() => EmailHelper.SendListingConfirmationAsync(
                            email, displayName, _txtTitle.Text!.Trim(), _txtPrice.Text!.Trim()));
                    }
                }
                catch { }

                _pBar.Value = 100;
                ShowStatus($"Done — {imageUrls.Count} photo(s) uploaded", false);
                _statusText.Foreground = new SolidColorBrush(Color.Parse("#10b981"));

                await Task.Delay(1200);
                Close(true);
            }
            catch (Exception ex)
            {
                ShowStatus($"Upload failed: {ex.Message}", true);
                _btnPost.IsEnabled = true;
                _btnPost.Content = "Publish Listing";
                _pBar.IsVisible = false;
            }
        }

        private static async Task<MemoryStream> CompressImageAsync(string path, int targetKb)
        {
            int targetBytes = targetKb * 1024;
            using var img = await SixImage.LoadAsync(path);

            foreach (var q in new[] { 85, 75, 65, 55, 45, 35, 25, 15, 10 })
            {
                var ms = new MemoryStream();
                await img.SaveAsJpegAsync(ms, new JpegEncoder { Quality = q });
                if (ms.Length <= targetBytes) { ms.Position = 0; return ms; }
                ms.Dispose();
            }

            foreach (var scale in new[] { 0.75, 0.60, 0.50, 0.40, 0.30, 0.20 })
            {
                using var scaled = img.Clone(x => x.Resize(
                    (int)(img.Width * scale), (int)(img.Height * scale)));
                foreach (var q in new[] { 70, 40, 15 })
                {
                    var ms = new MemoryStream();
                    await scaled.SaveAsJpegAsync(ms, new JpegEncoder { Quality = q });
                    if (ms.Length <= targetBytes) { ms.Position = 0; return ms; }
                    ms.Dispose();
                }
            }

            using var tiny = img.Clone(x => x.Resize(
                (int)(img.Width * 0.15), (int)(img.Height * 0.15)));
            var final = new MemoryStream();
            await tiny.SaveAsJpegAsync(final, new JpegEncoder { Quality = 10 });
            final.Position = 0;
            return final;
        }

        private void ShowStatus(string msg, bool isError)
        {
            _statusText.Text = msg;
            _statusText.Foreground = isError
                ? new SolidColorBrush(Color.Parse("#ef4444"))
                : new SolidColorBrush(Color.Parse("#94a3b8"));
        }

        private string BuildSpecSnippet()
        {
            var meta = _scan?.Metadata;
            if (meta == null) return "";

            var lines = new List<string>();
            if (meta.TryGetValue("cpu_name", out var cpu)) lines.Add($"CPU  : {cpu}");
            if (meta.TryGetValue("ram_total_gb", out var ram)) lines.Add($"RAM  : {ram} GB");
            if (meta.TryGetValue("gpu_name", out var gpu)) lines.Add($"GPU  : {gpu}");
            if (meta.TryGetValue("motherboard", out var mbb)) lines.Add($"Board: {mbb}");
            return string.Join("\n", lines);
        }

        private static string BuildFallbackTitle(Dictionary<string, object>? meta)
        {
            if (meta == null) return "";
            var parts = new List<string>();
            if (meta.TryGetValue("cpu_name", out var cpu))
                parts.Add(cpu.ToString() ?? "");
            if (meta.TryGetValue("ram_total_gb", out var ram))
                parts.Add($"{ram} GB RAM");
            if (meta.TryGetValue("gpu_name", out var gpu))
                parts.Add(gpu.ToString() ?? "");
            return string.Join(" / ", parts.Where(p => !string.IsNullOrEmpty(p)));
        }

        private void ShowPricePrediction()
        {
            try
            {
                float predicted = 0;
#if HAS_ML
                predicted = PricePredictor.Predict(_scan?.Metadata, "Used");
#else
                predicted = (float)PriceEstimator.Estimate(_scan?.Metadata, "Used");
#endif
                if (predicted > 0)
                {
                    long min = (long)Math.Round(predicted * 0.90f);
                    long max = (long)Math.Round(predicted * 1.10f);
                    _lblPrediction.Text = $"⚡ Suggested price: PKR {min:N0} – {max:N0}";
                    _lblPrediction.Foreground = new SolidColorBrush(Color.Parse("#5ea0ff"));
                    _lblPrediction.IsVisible = true;
                }
            }
            catch { }
        }

        private static TextBlock MakeLabel(string text, bool accent = false) => new()
        {
            Text = text,
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.Parse(accent ? "#5ea0ff" : "#94a3b8")),
            Margin = new Thickness(0, 4, 0, 2)
        };

        private static TextBox MakeInput(bool multiline = false) => new()
        {
            Background = new SolidColorBrush(Color.Parse("#1e2a3a")),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#243447")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 10),
            FontSize = 14,
            Height = multiline ? 76 : 44,
            AcceptsReturn = multiline,
            TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap
        };
    }
}
