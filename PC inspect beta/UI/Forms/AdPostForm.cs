#nullable disable

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using PC_inspect_beta.Core;

namespace PC_inspect_beta.UI.Forms
{
    // ══════════════════════════════════════════════════════════════════════════
    //  IMAGE COMPRESSOR  (lives here so you only need one file)
    //  Compresses any jpg/png to under a target KB before Firebase upload.
    // ══════════════════════════════════════════════════════════════════════════

    internal static class ImageCompressor
    {
        /// <summary>
        /// Compress an image file to under <paramref name="targetKb"/> kilobytes.
        /// Returns a MemoryStream (JPEG) with Position = 0, ready for upload.
        /// </summary>
        public static MemoryStream Compress(string filePath, int targetKb = 100)
        {
            int limit = targetKb * 1024;
            using var src = Image.FromFile(filePath);
            return Encode(src, limit);
        }

        // ── Core ──────────────────────────────────────────────────────────────

        private static MemoryStream Encode(Image img, int limit)
        {
            var codec = GetJpeg();
            var ep = new EncoderParameters(1);

            // Pass 1 — quality reduction, original dimensions
            foreach (int q in new[] { 85, 75, 65, 55, 45, 35, 25, 15, 10 })
            {
                var ms = Save(img, codec, ep, q);
                if (ms.Length <= limit) return ms;
                ms.Dispose();
            }

            // Pass 2 — shrink dimensions then compress
            foreach (double scale in new[] { 0.75, 0.60, 0.50, 0.40, 0.30, 0.20 })
            {
                using var small = Scale(img, scale);
                foreach (int q in new[] { 70, 40, 15 })
                {
                    var ms = Save(small, codec, ep, q);
                    if (ms.Length <= limit) return ms;
                    ms.Dispose();
                }
            }

            // Fallback — absolute minimum
            using var tiny = Scale(img, 0.15);
            return Save(tiny, codec, ep, 10);
        }

        private static MemoryStream Save(Image img, ImageCodecInfo codec,
                                         EncoderParameters ep, int quality)
        {
            ep.Param[0] = new EncoderParameter(Encoder.Quality, (long)quality);
            var ms = new MemoryStream();
            img.Save(ms, codec, ep);
            ms.Position = 0;
            return ms;
        }

        private static Bitmap Scale(Image img, double factor)
        {
            int w = Math.Max(1, (int)(img.Width * factor));
            int h = Math.Max(1, (int)(img.Height * factor));
            var bmp = new Bitmap(w, h);
            using var g = Graphics.FromImage(bmp);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            g.DrawImage(img, 0, 0, w, h);
            return bmp;
        }

        private static ImageCodecInfo GetJpeg()
            => ImageCodecInfo.GetImageEncoders()
                             .First(c => c.FormatID == ImageFormat.Jpeg.Guid);
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  AD POST FORM
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Marketplace listing form. Lets the authenticated user describe and
    /// publish their scanned device with photos to Firebase.
    /// Photos are automatically compressed to ≤100 KB before upload.
    /// </summary>
    public class AdPostForm : Form
    {
        // ── State ─────────────────────────────────────────────────────────────
        private readonly string _user;
        private readonly Dictionary<string, object> _scanMeta;
        private readonly List<string> _selectedFiles = new();

        // ── Controls ──────────────────────────────────────────────────────────
        private TextBox _txtTitle, _txtPrice, _txtDesc;
        private NumericUpDown _numYearsUsed;
        private ComboBox _cmbCondition;
        private Label _lblFileCount, _lblUploadStatus, _lblPriceHint;
        private ProgressBar _pBar;
        private Button _btnPost;

        private const int MAX_PHOTOS = 5;

        // ── Constructor ───────────────────────────────────────────────────────
        public AdPostForm(string username, Dictionary<string, object> scanMeta)
        {
            _user = username;
            _scanMeta = scanMeta;
            Build();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  LAYOUT
        // ══════════════════════════════════════════════════════════════════════

        private void Build()
        {
            Text            = "Create Marketplace Listing";
            Size            = new Size(560, 920);
            StartPosition   = FormStartPosition.CenterScreen;
            BackColor       = Theme.PanelBackground;
            ForeColor       = Theme.TextColor;
            AutoScroll      = true;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;
            Font            = Theme.NormalFont;

            const int HEADER_H = 110;
            const int X = 36, W = 480;

            // ── Gradient header ───────────────────────────────────────────────
            var header = new GradientPanel
            {
                Location = new Point(0, 0),
                Size     = new Size(ClientSize.Width, HEADER_H)
            };
            Controls.Add(header);

            header.Controls.Add(new Label
            {
                Text      = "Sell Your Device",
                Location  = new Point(0, 24),
                Size      = new Size(header.Width, 34),
                Font      = new Font("Segoe UI Semibold", 18, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter
            });
            header.Controls.Add(new Label
            {
                Text      = "List your scanned device on Inspectify LapStore",
                Location  = new Point(0, 60),
                Size      = new Size(header.Width, 22),
                Font      = Theme.SubTitleFont,
                ForeColor = Color.FromArgb(230, 230, 250),
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter
            });

            int y = HEADER_H + 24;

            // ── Product title (auto-filled) ───────────────────────────────────
            AddField("Product Title", ref _txtTitle, ref y, X, W);
            _txtTitle.Text = Core.PricePredictor.BuildProductTitle(_scanMeta);

            // ── Price ─────────────────────────────────────────────────────────
            AddField("Price (PKR)", ref _txtPrice, ref y, X, W);

            // Predicted price hint shown UNDER the price field, in brackets
            _lblPriceHint = new Label
            {
                Location = new Point(X, y - 50),
                Size     = new Size(W, 18),
                Font     = new Font(Theme.NormalFont.FontFamily, 9, FontStyle.Italic),
                ForeColor = Color.FromArgb(94, 160, 255),
                BackColor = Color.Transparent,
                Text = ""
            };
            Controls.Add(_lblPriceHint);
            // Adjust y down a bit so condition doesn't overlap the hint
            y += 4;

            // ── Condition + Years Used (always interactive) ───────────────────
            int halfW = (W - 12) / 2;
            Controls.Add(UIHelper.CreateLabel("Condition",
                new Point(X, y), Theme.SmallFont, Theme.MutedText));

            _cmbCondition = new ComboBox
            {
                Location      = new Point(X, y + 22),
                Width         = halfW,
                Height        = 36,
                BackColor     = Theme.InputBackground,
                ForeColor     = Theme.TextColor,
                FlatStyle     = FlatStyle.Flat,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font          = Theme.NormalFont
            };
            _cmbCondition.Items.AddRange(new[] { "New", "Like New", "Used", "For Parts" });
            _cmbCondition.SelectedIndex = 0;
            Controls.Add(_cmbCondition);

            Controls.Add(UIHelper.CreateLabel("Years Used",
                new Point(X + halfW + 12, y), Theme.SmallFont, Theme.MutedText));

            _numYearsUsed = new NumericUpDown
            {
                Location  = new Point(X + halfW + 12, y + 22),
                Width     = halfW,
                Height    = 36,
                Minimum   = 0,
                Maximum   = 20,
                Value     = 0,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Theme.InputBackground,
                ForeColor = Theme.TextColor,
                Font      = Theme.NormalFont
            };
            Controls.Add(_numYearsUsed);

            // Auto-zero years when "New" is chosen — but never disable.
            _cmbCondition.SelectedIndexChanged += (s, e) =>
            {
                if (_cmbCondition.SelectedItem?.ToString() == "New")
                    _numYearsUsed.Value = 0;
                UpdatePredictedPriceLabel();
            };
            _numYearsUsed.ValueChanged += (s, e) => UpdatePredictedPriceLabel();

            // Initial prediction
            UpdatePredictedPriceLabel();

            y += 80;

            // ── Description ───────────────────────────────────────────────────
            Controls.Add(UIHelper.CreateLabel("Description",
                new Point(X, y), Theme.SmallFont, Theme.MutedText));
            _txtDesc = UIHelper.CreateTextBox(new Point(X, y + 22), W, multiline: true, height: 100);
            Controls.Add(_txtDesc);
            y += 140;

            // ── Detected specs (read-only) ────────────────────────────────────
            string snippet = BuildSpecSnippet();
            if (!string.IsNullOrEmpty(snippet))
            {
                Controls.Add(UIHelper.CreateLabel("Detected Specs (from scan)",
                    new Point(X, y), Theme.SmallFont, Theme.CyanAccent));

                var specsBox = UIHelper.CreateTextBox(
                    new Point(X, y + 22), W,
                    readOnly: true, multiline: true, height: 80);
                specsBox.Text      = snippet;
                specsBox.BackColor = Theme.DarkBackground;
                Controls.Add(specsBox);
                y += 120;
            }

            // ── Divider ───────────────────────────────────────────────────────
            Controls.Add(new Panel
            {
                Location  = new Point(X, y),
                Size      = new Size(W, 1),
                BackColor = Theme.BorderColorSoft
            });
            y += 16;

            // ── Photo selector ────────────────────────────────────────────────
            Controls.Add(UIHelper.CreateLabel(
                $"Photos  (max {MAX_PHOTOS} · auto-compressed to 100 KB)",
                new Point(X, y), Theme.SmallFont, Theme.MutedText));
            y += 24;

            var btnPhoto = UIHelper.CreateButton("📷   Select Photos",
                Theme.AccentColor, new Point(X, y), 200, 40);
            btnPhoto.Click += OnSelectPhotos;
            Controls.Add(btnPhoto);

            _lblFileCount = UIHelper.CreateLabel("No photos selected (optional)",
                new Point(X + 212, y + 12), Theme.SmallFont, Theme.MutedText);
            Controls.Add(_lblFileCount);
            y += 56;

            // ── Upload status / progress ──────────────────────────────────────
            _lblUploadStatus = UIHelper.CreateLabel("",
                new Point(X, y), Theme.SmallFont, Theme.MutedText);
            Controls.Add(_lblUploadStatus);
            y += 22;

            _pBar = UIHelper.CreateProgressBar(new Point(X, y), W, 10);
            Controls.Add(_pBar);
            y += 26;

            // ── Publish button ────────────────────────────────────────────────
            _btnPost = UIHelper.CreateButton("🚀   Publish Listing",
                Theme.SuccessColor, new Point(X, y), W, 56);
            _btnPost.Font = Theme.BigButtonFont;
            _btnPost.Click += OnPublish;
            Controls.Add(_btnPost);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  EVENT HANDLERS
        // ══════════════════════════════════════════════════════════════════════

        private void OnSelectPhotos(object sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Multiselect = true,
                Filter      = "Images|*.jpg;*.jpeg;*.png",
                Title       = $"Select up to {MAX_PHOTOS} photos"
            };

            if (ofd.ShowDialog() != DialogResult.OK) return;

            _selectedFiles.Clear();
            _selectedFiles.AddRange(ofd.FileNames.Take(MAX_PHOTOS));
            _lblFileCount.Text      = $"{_selectedFiles.Count} photo(s) selected";
            _lblFileCount.ForeColor = Theme.SuccessColor;
        }

        private async void OnPublish(object sender, EventArgs e)
        {
            // ── Validation ────────────────────────────────────────────────────
            if (string.IsNullOrWhiteSpace(_txtTitle.Text) ||
                string.IsNullOrWhiteSpace(_txtPrice.Text))
            {
                MessageBox.Show("Title and Price are required.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // ── Lock UI ───────────────────────────────────────────────────────
            _btnPost.Enabled = false;
            _btnPost.Text = "Uploading…";
            _pBar.Visible = true;
            _pBar.Style = ProgressBarStyle.Marquee;

            try
            {
                // ── Upload photos (with compression) ──────────────────────────
                var imageUrls = new List<string>();

                if (_selectedFiles.Count > 0)
                {
                    _pBar.Style = ProgressBarStyle.Continuous;
                    _pBar.Value = 0;

                    for (int i = 0; i < _selectedFiles.Count; i++)
                    {
                        string file = _selectedFiles[i];

                        // 1. Compress to ≤100 KB in memory
                        _lblUploadStatus.Text = $"Compressing photo {i + 1} of {_selectedFiles.Count}…";
                        Application.DoEvents();

                        using var compressed = ImageCompressor.Compress(file, targetKb: 100);

                        // Show compressed size for transparency
                        double kb = compressed.Length / 1024.0;
                        _lblUploadStatus.Text = $"Uploading photo {i + 1} of {_selectedFiles.Count}  ({kb:F0} KB)…";
                        Application.DoEvents();

                        // 2. Upload compressed stream to Firebase Storage
                        string key = $"ads/{_user}_{DateTime.Now.Ticks}_{Path.GetFileName(file)}.jpg";
                        string url = await DbHelper.UploadStream(_user, compressed, key);
                        imageUrls.Add(url);

                        // 3. Update progress bar
                        _pBar.Value = (int)((i + 1) / (double)_selectedFiles.Count * 80);
                    }
                }

                // ── Build listing payload ─────────────────────────────────────
                _lblUploadStatus.Text = "Saving listing…";
                _pBar.Value = 85;
                Application.DoEvents();

                var ad = new Dictionary<string, object>();
                foreach (var kv in _scanMeta) ad["sys_" + kv.Key] = kv.Value;

                ad["title"] = _txtTitle.Text.Trim();
                ad["price"] = _txtPrice.Text.Trim();
                ad["desc"] = _txtDesc.Text.Trim();
                ad["condition"] = _cmbCondition.SelectedItem.ToString();
                ad["years_used"] = (int)_numYearsUsed.Value;
                ad["user"] = _user;
                ad["date"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                ad["images"] = imageUrls;

                // ── Silent ML-based price prediction (never shown to user) ────
                // ±10 % margin stored because physical condition affects real value.
                try
                {
                    float predicted = PricePredictor.Predict(
                        _scanMeta, _cmbCondition.SelectedItem?.ToString());
                    if (predicted > 0f)
                    {
                        ad["predicted_price_min"] = (long)Math.Round(predicted * 0.90f);
                        ad["predicted_price_max"] = (long)Math.Round(predicted * 1.10f);
                    }
                }
                catch { /* prediction is best-effort — never block publishing */ }

                await DbHelper.SaveAdPost(_user, ad);

                _pBar.Value = 100;
                _lblUploadStatus.Text = $"✔ Done — {imageUrls.Count} photo(s) uploaded";
                _lblUploadStatus.ForeColor = Theme.SuccessColor;

                MessageBox.Show("Listing published successfully! 🎉", "Done",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Upload failed:\n" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);

                _btnPost.Enabled = true;
                _btnPost.Text = "🚀  Publish Listing";
                _pBar.Visible = false;
                _lblUploadStatus.Text = "";
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════════════════

        private void AddField(string label, ref TextBox tb, ref int y, int x, int w)
        {
            Controls.Add(UIHelper.CreateLabel(label, new Point(x, y), Theme.SmallFont, Theme.MutedText));
            tb = UIHelper.CreateTextBox(new Point(x, y + 22), w);
            Controls.Add(tb);
            y += 68;
        }

        /// <summary>
        /// Updates the predicted-price hint shown directly under the price field.
        /// Tries the ML.NET PricePredictor first; falls back to PriceEstimator on errors.
        /// </summary>
        private void UpdatePredictedPriceLabel()
        {
            if (_lblPriceHint == null) return;
            try
            {
                var condition = _cmbCondition?.SelectedItem?.ToString() ?? "Used";
                int predicted = 0;
                try
                {
                    predicted = (int)Core.PricePredictor.Predict(_scanMeta, condition);
                }
                catch
                {
                    predicted = Core.PriceEstimator.Estimate(_scanMeta, condition);
                }
                _lblPriceHint.Text = predicted > 0
                    ? $"(Estimated fair price: PKR {predicted:N0})"
                    : "";
            }
            catch
            {
                _lblPriceHint.Text = "";
            }
        }

        private string BuildSpecSnippet()
        {
            var lines = new List<string>();
            if (_scanMeta.TryGetValue("cpu_name", out var cpu)) lines.Add($"CPU  : {cpu}");
            if (_scanMeta.TryGetValue("ram_total_gb", out var ram)) lines.Add($"RAM  : {ram} GB");
            if (_scanMeta.TryGetValue("gpu_name", out var gpu)) lines.Add($"GPU  : {gpu}");
            if (_scanMeta.TryGetValue("motherboard", out var mbb)) lines.Add($"Board: {mbb}");
            return string.Join("\r\n", lines);
        }
    }
}