using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using PC_inspect_beta.Config;

namespace PC_inspect_beta.Core
{
    public static class EmailHelper
    {
        public static async Task SendAsync(string toEmail, string toName, string subject, string htmlBody, string plainBody)
        {
            if (string.IsNullOrWhiteSpace(AppConfig.SmtpPassword))
                throw new InvalidOperationException("SMTP password not configured.");

            using var msg = new MailMessage();
            msg.From = new MailAddress(AppConfig.SmtpUsername, AppConfig.SmtpFromName);
            msg.To.Add(new MailAddress(toEmail, toName ?? toEmail));
            msg.Subject = subject;
            msg.Body = htmlBody;
            msg.IsBodyHtml = true;

            using var smtp = new SmtpClient(AppConfig.SmtpHost, AppConfig.SmtpPort);
            smtp.Credentials = new NetworkCredential(AppConfig.SmtpUsername, AppConfig.SmtpPassword);
            smtp.EnableSsl = true;
            await smtp.SendMailAsync(msg);
        }

        public static async Task SendVerificationCodeAsync(string toEmail, string displayName, string code)
        {
            var safeName = string.IsNullOrWhiteSpace(displayName) ? "there" : WebUtility.HtmlEncode(displayName);
            var subject = "Inspectify — Password Reset Code";

            var html = $@"<!DOCTYPE html>
<html><body style=""margin:0;padding:0;background:#f3f4f6;font-family:Segoe UI,Arial,sans-serif;color:#16212e;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""padding:32px 16px;"">
    <tr><td align=""center"">
      <table width=""560"" cellpadding=""0"" cellspacing=""0"" style=""background:#ffffff;border-radius:14px;overflow:hidden;box-shadow:0 4px 18px rgba(0,0,0,.06);"">
        <tr><td style=""background:#16212e;padding:22px 28px;color:#fafaf7;font-size:20px;font-weight:700;letter-spacing:-.5px;"">
          Inspectify<span style=""color:#5ea0ff;"">.</span> <span style=""font-size:11px;letter-spacing:3px;color:rgba(250,250,247,.7);"">LAPSTORE</span>
        </td></tr>
        <tr><td style=""padding:30px 28px 8px;"">
          <h1 style=""margin:0 0 14px;font-size:22px;color:#16212e;"">Password Reset</h1>
          <p style=""margin:0 0 14px;line-height:1.55;font-size:15px;color:#374151;"">Hi {safeName},</p>
          <p style=""margin:0 0 18px;line-height:1.55;font-size:15px;color:#374151;"">
            Use the code below to reset your password in the Inspectify Scanner app.
            This code expires in <strong>10 minutes</strong>.
          </p>
          <div style=""background:#f3f4f6;border-radius:10px;padding:20px;margin:18px 0;text-align:center;"">
            <span style=""font-size:36px;font-weight:700;letter-spacing:8px;color:#2b72d4;font-family:Cascadia Mono,Consolas,monospace;"">{code}</span>
          </div>
          <p style=""margin:18px 0 0;font-size:13px;color:#6b7280;line-height:1.5;"">
            If you didn't request this, you can safely ignore this email.
          </p>
          <hr style=""border:none;border-top:1px solid #e5e7eb;margin:24px 0;""/>
          <p style=""margin:0;font-size:12px;color:#9ca3af;line-height:1.5;"">
            For help, contact <a href=""mailto:inspectifylapstore@gmail.com"" style=""color:#2b72d4;"">inspectifylapstore@gmail.com</a>.
          </p>
        </td></tr>
        <tr><td style=""padding:18px 28px;background:#f9fafb;border-top:1px solid #e5e7eb;font-size:11px;color:#9ca3af;text-align:center;"">
          Inspectify LapStore — Pakistan's scan-verified laptop marketplace.
        </td></tr>
      </table>
    </td></tr>
  </table>
</body></html>";

            var plain = $"Hi {displayName ?? "there"},\n\n" +
                        $"Your password reset code is: {code}\n\n" +
                        "This code expires in 10 minutes.\n\n" +
                        "If you didn't request this, ignore this email.\n" +
                        "— Inspectify LapStore";

            await SendAsync(toEmail, displayName ?? toEmail, subject, html, plain);
        }

        public static async Task SendListingConfirmationAsync(string toEmail, string displayName, string adTitle, string price)
        {
            var safeName = string.IsNullOrWhiteSpace(displayName) ? "there" : WebUtility.HtmlEncode(displayName);
            var subject = "Inspectify — Your listing is live!";

            var html = $@"<!DOCTYPE html>
<html><body style=""margin:0;padding:0;background:#f3f4f6;font-family:Segoe UI,Arial,sans-serif;color:#16212e;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""padding:32px 16px;"">
    <tr><td align=""center"">
      <table width=""560"" cellpadding=""0"" cellspacing=""0"" style=""background:#ffffff;border-radius:14px;overflow:hidden;box-shadow:0 4px 18px rgba(0,0,0,.06);"">
        <tr><td style=""background:#16212e;padding:22px 28px;color:#fafaf7;font-size:20px;font-weight:700;letter-spacing:-.5px;"">
          Inspectify<span style=""color:#5ea0ff;"">.</span> <span style=""font-size:11px;letter-spacing:3px;color:rgba(250,250,247,.7);"">LAPSTORE</span>
        </td></tr>
        <tr><td style=""padding:30px 28px 8px;"">
          <h1 style=""margin:0 0 14px;font-size:22px;color:#10b981;"">Listing Published!</h1>
          <p style=""margin:0 0 14px;line-height:1.55;font-size:15px;color:#374151;"">Hi {safeName},</p>
          <p style=""margin:0 0 18px;line-height:1.55;font-size:15px;color:#374151;"">
            Your device has been listed on Inspectify LapStore. Buyers can now find it on the marketplace.
          </p>
          <div style=""background:#f3f4f6;border-radius:10px;padding:18px 20px;margin:18px 0;"">
            <p style=""margin:0 0 6px;font-size:13px;color:#6b7280;"">LISTING</p>
            <p style=""margin:0 0 4px;font-size:17px;font-weight:600;color:#16212e;"">{WebUtility.HtmlEncode(adTitle)}</p>
            <p style=""margin:0;font-size:20px;font-weight:700;color:#2b72d4;"">PKR {WebUtility.HtmlEncode(price)}</p>
          </div>
          <hr style=""border:none;border-top:1px solid #e5e7eb;margin:24px 0;""/>
          <p style=""margin:0;font-size:12px;color:#9ca3af;line-height:1.5;"">
            Manage your listings at <a href=""https://inspectifylapstore.me"" style=""color:#2b72d4;"">inspectifylapstore.me</a>
          </p>
        </td></tr>
        <tr><td style=""padding:18px 28px;background:#f9fafb;border-top:1px solid #e5e7eb;font-size:11px;color:#9ca3af;text-align:center;"">
          Inspectify LapStore — Pakistan's scan-verified laptop marketplace.
        </td></tr>
      </table>
    </td></tr>
  </table>
</body></html>";

            var plain = $"Hi {displayName ?? "there"},\n\n" +
                        $"Your listing \"{adTitle}\" at PKR {price} is now live on Inspectify LapStore!\n\n" +
                        "Manage your listings at https://inspectifylapstore.me\n\n" +
                        "— Inspectify LapStore";

            await SendAsync(toEmail, displayName ?? toEmail, subject, html, plain);
        }

        public static string GenerateCode()
        {
            return new Random().Next(100000, 999999).ToString();
        }
    }
}
