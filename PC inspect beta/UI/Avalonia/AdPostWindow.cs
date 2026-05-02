using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using PC_inspect_beta.Core;
using PC_inspect_beta.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using SixImage = SixLabors.ImageSharp.Image;
using AvImage = Avalonia.Controls.Image;
using Color = Avalonia.Media.Color;

namespace PC_inspect_beta.UI.Avalonia
{
    /// <summary>
    /// Cross-platform ad post window. Lets the user describe their device,
    /// attach photos, and publish the listing to MongoDB.
    /// </summary>
    public class AdPostWindow : Window
    {
        private readonly string _username;
        private readonly ScanResult _scan;

        private TextBox _title = null!, _description = null!, _price = null!, _location = null!;
        private ComboBox _condition = null!;
        private TextBlock _priceHint = null!;
        private StackPanel _imagePreview = null!;
        private TextBlock _statusText = null!;
        private Button _btnSubmit = null!, _btnAddImages = null!;
        private List<string> _imagePaths = new();

        public AdPostWindow(string username, ScanResult scan)
        {
            _username = username;
            _scan = scan;

            Title = "Post Ad";
            Width = 640;
            Height = 800;
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
                Padding = new Thickness(20, 16),
                BorderBrush = new SolidColorBrush(Color.Parse("#243447")),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            DockPanel.SetDock(header, Dock.Top);

            var hs = new StackPanel();
            hs.Children.Add(new TextBlock
            {
                Text = "Post Your Listing",
                FontSize = 20,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White
            });
            hs.Children.Add(new TextBlock
            {
                Text = $"Signed in as @{_username}",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.Parse("#94a3b8")),
                Margin = new Thickness(0, 4, 0, 0)
            });
            header.Child = hs;
            root.Children.Add(header);

            // Status bar (bottom)
            _statusText = new TextBlock
            {
                Text = "",
                Foreground = new SolidColorBrush(Color.Parse("#94a3b8")),
                FontSize = 12,
                Margin = new Thickness(20, 12),
                TextWrapping = TextWrapping.Wrap
            };
            DockPanel.SetDock(_statusText, Dock.Bottom);
            root.Children.Add(_statusText);

            // Submit button (bottom)
            _btnSubmit = new Button
            {
                Content = "Publish Listing",
                Background = new SolidColorBrush(Color.Parse("#2b72d4")),
                Foreground = Brushes.White,
                FontWeight = FontWeight.SemiBold,
                FontSize = 14,
                Height = 48,
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(20, 0, 20, 12),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            _btnSubmit.Click += async (_, _) => await PublishAsync();
            DockPanel.SetDock(_btnSubmit, Dock.Bottom);
            root.Children.Add(_btnSubmit);

            // Form (scrollable)
            var scroll = new ScrollViewer { Padding = new Thickness(20) };
            var form = new StackPanel { Spacing = 12 };

            form.Children.Add(MakeLabel("Title"));
            _title = MakeInput("e.g., Dell XPS 15 — i7 12th gen — 16GB / 512GB SSD");
            form.Children.Add(_title);

            form.Children.Add(MakeLabel("Description"));
            _description = MakeInput("Describe condition, included accessories, reason for sale...", multiline: true);
            form.Children.Add(_description);

            form.Children.Add(MakeLabel("Asking Price (PKR)"));
            _price = MakeInput("e.g., 150000");
            form.Children.Add(_price);

            // Predicted price hint (shown under the price field in brackets)
            _priceHint = new TextBlock
            {
                Text = "",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.Parse("#5ea0ff")),
                FontStyle = FontStyle.Italic,
                Margin = new Thickness(0, 4, 0, 0)
            };
            form.Children.Add(_priceHint);

            form.Children.Add(MakeLabel("Location"));
            _location = MakeInput("e.g., Karachi, Pakistan");
            form.Children.Add(_location);

            form.Children.Add(MakeLabel("Condition"));
            _condition = new ComboBox
            {
                Background = new SolidColorBrush(Color.Parse("#1e2a3a")),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.Parse("#243447")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 8),
                Height = 44,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                ItemsSource = new[] { "10/10 (Like new)", "9/10 (Excellent)", "8/10 (Very good)", "7/10 (Good)", "6/10 (Fair)" },
                SelectedIndex = 1
            };
            _condition.SelectionChanged += (_, _) => UpdatePredictedPrice();
            form.Children.Add(_condition);

            // Initial prediction based on current scan + default condition
            UpdatePredictedPrice();

            form.Children.Add(MakeLabel("Photos (up to 8 images)"));

            _btnAddImages = new Button
            {
                Content = "+ Add Photos",
                Background = new SolidColorBrush(Color.Parse("#243447")),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.Parse("#2b72d4")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 10),
                Height = 44,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            _btnAddImages.Click += async (_, _) => await PickImagesAsync();
            form.Children.Add(_btnAddImages);

            _imagePreview = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Margin = new Thickness(0, 8, 0, 0)
            };
            var imageScroll = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = _imagePreview
            };
            form.Children.Add(imageScroll);

            scroll.Content = form;
            root.Children.Add(scroll);
            Content = root;
        }

        private void UpdatePredictedPrice()
        {
            try
            {
                var conditionStr = _condition.SelectedItem?.ToString() ?? "9/10";
                var predicted = PriceEstimator.Estimate(_scan?.Metadata, conditionStr);
                if (predicted > 0)
                {
                    _priceHint.Text = $"(Estimated fair price: PKR {predicted:N0})";
                    _priceHint.IsVisible = true;
                }
                else
                {
                    _priceHint.Text = "(Run a hardware scan first to see an estimated price)";
                    _priceHint.IsVisible = true;
                }
            }
            catch
            {
                _priceHint.IsVisible = false;
            }
        }

        private async Task PickImagesAsync()
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select photos (up to 8)",
                AllowMultiple = true,
                FileTypeFilter = new[] { new FilePickerFileType("Images") { Patterns = new[] { "*.jpg", "*.jpeg", "*.png" } } }
            });

            if (files == null || files.Count == 0) return;

            foreach (var file in files.Take(8 - _imagePaths.Count))
            {
                var path = file.TryGetLocalPath();
                if (string.IsNullOrEmpty(path)) continue;

                _imagePaths.Add(path);

                try
                {
                    await using var stream = File.OpenRead(path);
                    var bmp = new Bitmap(stream);

                    var thumb = new Border
                    {
                        Width = 80,
                        Height = 80,
                        CornerRadius = new CornerRadius(8),
                        BorderBrush = new SolidColorBrush(Color.Parse("#243447")),
                        BorderThickness = new Thickness(1),
                        Child = new AvImage
                        {
                            Source = bmp,
                            Stretch = Stretch.UniformToFill
                        }
                    };
                    _imagePreview.Children.Add(thumb);
                }
                catch { /* ignore broken files */ }
            }

            _statusText.Text = $"{_imagePaths.Count} photo(s) selected.";
        }

        private async Task PublishAsync()
        {
            if (string.IsNullOrWhiteSpace(_title.Text)) { ShowStatus("Please enter a title.", true); return; }
            if (string.IsNullOrWhiteSpace(_price.Text) || !double.TryParse(_price.Text, out var price)) { ShowStatus("Please enter a valid price.", true); return; }
            if (_imagePaths.Count == 0) { ShowStatus("Please add at least one photo.", true); return; }

            _btnSubmit.IsEnabled = false;
            _btnAddImages.IsEnabled = false;
            ShowStatus("Compressing and uploading photos…", false);

            try
            {
                // Compress images cross-platform with ImageSharp
                var imageIds = new List<string>();
                int idx = 0;
                foreach (var path in _imagePaths)
                {
                    using var ms = await CompressImageAsync(path, targetKb: 100);
                    var name = $"ads/{_username}_{DateTime.UtcNow.Ticks}_{Path.GetFileName(path)}";
                    var id = await DbHelper.UploadStream(_username, ms, name);
                    imageIds.Add(id);

                    idx++;
                    ShowStatus($"Uploaded {idx} of {_imagePaths.Count} photo(s)…", false);
                }

                ShowStatus("Saving listing…", false);

                // ML-based price prediction is Windows-only (ML.NET architecture restriction)
                int? predictedPrice = null;

                await DbHelper.SaveAdPost(_username, new
                {
                    seller = _username,
                    title = _title.Text?.Trim(),
                    description = _description.Text?.Trim(),
                    price = (int)price,
                    location = _location.Text?.Trim(),
                    condition = _condition.SelectedItem?.ToString(),
                    images = imageIds,
                    metadata = _scan?.Metadata,
                    predictedPrice = predictedPrice,
                    publishedAt = DateTime.UtcNow.ToString("o"),
                    sold = false
                });

                ShowStatus("✓ Listing published successfully!", false);
                _statusText.Foreground = new SolidColorBrush(Color.Parse("#10b981"));

                await Task.Delay(1500);
                Close(true);
            }
            catch (Exception ex)
            {
                ShowStatus($"Error: {ex.Message}", true);
                _btnSubmit.IsEnabled = true;
                _btnAddImages.IsEnabled = true;
            }
        }

        private static async Task<MemoryStream> CompressImageAsync(string path, int targetKb)
        {
            int targetBytes = targetKb * 1024;
            using var img = await SixImage.LoadAsync(path);

            // Try various quality levels
            foreach (var q in new[] { 85, 70, 55, 40, 25 })
            {
                var ms = new MemoryStream();
                await img.SaveAsJpegAsync(ms, new JpegEncoder { Quality = q });
                if (ms.Length <= targetBytes) { ms.Position = 0; return ms; }
                ms.Dispose();
            }

            // Try shrinking dimensions
            foreach (var scale in new[] { 0.75, 0.5, 0.35, 0.2 })
            {
                using var scaled = img.Clone(x => x.Resize((int)(img.Width * scale), (int)(img.Height * scale)));
                foreach (var q in new[] { 60, 35, 15 })
                {
                    var ms = new MemoryStream();
                    await scaled.SaveAsJpegAsync(ms, new JpegEncoder { Quality = q });
                    if (ms.Length <= targetBytes) { ms.Position = 0; return ms; }
                    ms.Dispose();
                }
            }

            // Final fallback
            using var tiny = img.Clone(x => x.Resize((int)(img.Width * 0.15), (int)(img.Height * 0.15)));
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

        private static TextBlock MakeLabel(string text) => new()
        {
            Text = text,
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.Parse("#94a3b8")),
            Margin = new Thickness(0, 4, 0, 2)
        };

        private static TextBox MakeInput(string placeholder = "", bool multiline = false) => new()
        {
            Background = new SolidColorBrush(Color.Parse("#1e2a3a")),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#243447")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 10),
            FontSize = 14,
            Height = multiline ? 100 : 44,
            AcceptsReturn = multiline,
            TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
            Watermark = placeholder
        };
    }
}
