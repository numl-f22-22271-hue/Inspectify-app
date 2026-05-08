using System;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using PC_inspect_beta.Config;

namespace PC_inspect_beta.UI.Avalonia
{
    public class EmailReportWindow : Window
    {
        private TextBox _txtEmail = null!;
        private TextBox _txtName = null!;
        private TextBlock _statusText = null!;
        private Button _sendBtn = null!;
        private readonly string _reportText;

        public EmailReportWindow(string reportText)
        {
            _reportText = reportText;
            Title = "Email Inspection Report";
            Width = 460;
            Height = 400;
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
                        new GradientStop(Color.Parse("#2b72d4"), 0),
                        new GradientStop(Color.Parse("#5ea0ff"), 1)
                    }
                },
                Height = 86,
                Padding = new Thickness(20, 16)
            };
            DockPanel.SetDock(header, Dock.Top);

            var hs = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            hs.Children.Add(new TextBlock
            {
                Text = "Email Report",
                FontSize = 22,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            hs.Children.Add(new TextBlock
            {
                Text = "Send the inspection report to a buyer or yourself",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.Parse("#e2e8f0")),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            });
            header.Child = hs;
            root.Children.Add(header);

            var form = new StackPanel { Margin = new Thickness(36, 24), Spacing = 12 };

            form.Children.Add(MakeLabel("Recipient Name"));
            _txtName = MakeInput("e.g. Buyer Name");
            form.Children.Add(_txtName);

            form.Children.Add(MakeLabel("Recipient Email"));
            _txtEmail = MakeInput("e.g. buyer@email.com");
            form.Children.Add(_txtEmail);

            _statusText = new TextBlock
            {
                FontSize = 12,
                IsVisible = false,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            };
            form.Children.Add(_statusText);

            _sendBtn = new Button
            {
                Content = "  Send Report  ",
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
            _sendBtn.Click += async (_, _) => await SendAsync();
            form.Children.Add(_sendBtn);

            root.Children.Add(form);
            Content = root;
        }

        private async Task SendAsync()
        {
            var name = (_txtName.Text ?? "").Trim();
            var email = (_txtEmail.Text ?? "").Trim();

            if (string.IsNullOrWhiteSpace(email))
            {
                ShowStatus("Please enter an email address.", "#ef4444");
                return;
            }

            if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                ShowStatus("Please enter a valid email address.", "#ef4444");
                return;
            }

            if (string.IsNullOrWhiteSpace(AppConfig.SmtpPassword))
            {
                ShowStatus("SMTP password not configured. Set it in AppConfig.cs.", "#ef4444");
                return;
            }

            _sendBtn.IsEnabled = false;
            _sendBtn.Content = "  Sending...  ";
            ShowStatus("Connecting to mail server...", "#5ea0ff");

            try
            {
                var displayName = string.IsNullOrWhiteSpace(name) ? "there" : name;

                var reportHtml = WebUtility.HtmlEncode(_reportText)
                    .Replace("\n", "<br/>")
                    .Replace(" ", "&nbsp;");

                var html = BuildEmailHtml(displayName, reportHtml);
                var plain = $"Hi {displayName},\n\n" +
                            "Please find the hardware inspection report from Inspectify LapStore below.\n\n" +
                            "────────────────────────────────────\n" +
                            _reportText + "\n" +
                            "────────────────────────────────────\n\n" +
                            "— Inspectify LapStore\nhttps://inspectifylapstore.com";

                var msg = new MimeMessage();
                msg.From.Add(new MailboxAddress(AppConfig.SmtpFromName, AppConfig.SmtpUsername));
                msg.To.Add(new MailboxAddress(name ?? email, email));
                msg.Subject = "Inspectify — Hardware Inspection Report";

                var builder = new BodyBuilder { HtmlBody = html, TextBody = plain };
                msg.Body = builder.ToMessageBody();

                await Task.Run(async () =>
                {
                    using var smtp = new SmtpClient();
                    await smtp.ConnectAsync(AppConfig.SmtpHost, AppConfig.SmtpPort, SecureSocketOptions.StartTls);
                    await smtp.AuthenticateAsync(AppConfig.SmtpUsername, AppConfig.SmtpPassword);
                    await smtp.SendAsync(msg);
                    await smtp.DisconnectAsync(quit: true);
                });

                ShowStatus("Report sent successfully!", "#10b981");
                _sendBtn.Content = "  Sent ✔  ";

                await Task.Delay(1500);
                Close(true);
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed: {ex.Message}", "#ef4444");
                _sendBtn.IsEnabled = true;
                _sendBtn.Content = "  Send Report  ";
            }
        }

        private static string BuildEmailHtml(string name, string reportHtml)
        {
            return $@"<!DOCTYPE html>
<html><body style=""margin:0;padding:0;background:#f3f4f6;font-family:Segoe UI,Arial,sans-serif;color:#16212e;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""padding:32px 16px;"">
    <tr><td align=""center"">
      <table width=""640"" cellpadding=""0"" cellspacing=""0"" style=""background:#ffffff;border-radius:14px;overflow:hidden;box-shadow:0 4px 18px rgba(0,0,0,.06);"">
        <tr><td style=""background:#16212e;padding:22px 28px;color:#fafaf7;font-size:20px;font-weight:700;letter-spacing:-.5px;"">
          Inspectify<span style=""color:#5ea0ff;"">.</span> <span style=""font-size:11px;letter-spacing:3px;color:rgba(250,250,247,.7);"">LAPSTORE</span>
        </td></tr>
        <tr><td style=""padding:30px 28px 8px;"">
          <h1 style=""margin:0 0 14px;font-size:22px;color:#16212e;"">Hardware Inspection Report</h1>
          <p style=""margin:0 0 14px;line-height:1.55;font-size:15px;color:#374151;"">Hi {WebUtility.HtmlEncode(name)},</p>
          <p style=""margin:0 0 18px;line-height:1.55;font-size:15px;color:#374151;"">
            Here is the hardware inspection report generated by <strong>Inspectify LapStore</strong>.
            This report was scanned directly on the device to verify its condition.
          </p>
          <div style=""background:#0d1620;border-radius:10px;padding:18px 20px;margin:18px 0;overflow-x:auto;"">
            <pre style=""margin:0;font-family:Cascadia Mono,Consolas,monospace;font-size:12px;color:#e2e8f0;white-space:pre-wrap;word-break:break-word;"">{reportHtml}</pre>
          </div>
          <hr style=""border:none;border-top:1px solid #e5e7eb;margin:24px 0;""/>
          <p style=""margin:0;font-size:12px;color:#9ca3af;line-height:1.5;"">
            This report was generated by the Inspectify desktop scanner. For questions, contact
            <a href=""mailto:inspectifylapstore@gmail.com"" style=""color:#2b72d4;"">inspectifylapstore@gmail.com</a>.
          </p>
        </td></tr>
        <tr><td style=""padding:18px 28px;background:#f9fafb;border-top:1px solid #e5e7eb;font-size:11px;color:#9ca3af;text-align:center;"">
          Inspectify LapStore — Pakistan's scan-verified laptop marketplace.
        </td></tr>
      </table>
    </td></tr>
  </table>
</body></html>";
        }

        private void ShowStatus(string msg, string color)
        {
            _statusText.Text = msg;
            _statusText.Foreground = new SolidColorBrush(Color.Parse(color));
            _statusText.IsVisible = true;
        }

        private static TextBlock MakeLabel(string text) => new()
        {
            Text = text,
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.Parse("#94a3b8")),
            Margin = new Thickness(0, 4, 0, 2)
        };

        private static TextBox MakeInput(string watermark) => new()
        {
            Watermark = watermark,
            Background = new SolidColorBrush(Color.Parse("#1e2a3a")),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#243447")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 10),
            FontSize = 14,
            Height = 44
        };
    }
}
