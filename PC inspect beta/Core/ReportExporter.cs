#nullable disable

using System;
using System.IO;
using iText.IO.Font.Constants;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;

namespace PC_inspect_beta.Core
{
    /// <summary>
    /// Exports a diagnostic report to a PDF file using iText 7.
    /// Lines prefixed with "##" (section headers) are rendered in bold.
    /// No UI dependency — pure file I/O.
    /// </summary>
    public static class ReportExporter
    {
        private const string HeadingPrefix = "##";

        /// <summary>
        /// Writes <paramref name="reportText"/> as a formatted PDF to the user's Desktop.
        /// </summary>
        /// <returns>Full file path of the saved PDF.</returns>
        public static string SaveToPdf(string reportText)
        {
            string fileName = $"PCReport_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            string path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads",
            fileName);

            using var writer = new PdfWriter(path);
            using var pdf    = new PdfDocument(writer);
            using var doc    = new Document(pdf);

            PdfFont monoFont = PdfFontFactory.CreateFont(StandardFonts.COURIER);
            PdfFont boldFont = PdfFontFactory.CreateFont(StandardFonts.COURIER_BOLD);

            // Main title
            doc.Add(new Paragraph("PC INSPECT – FULL SYSTEM REPORT")
                .SetFont(boldFont).SetFontSize(14).SetMarginBottom(4));

            doc.Add(new Paragraph($"Generated: {DateTime.Now:dddd, dd MMMM yyyy  HH:mm:ss}")
                .SetFont(monoFont).SetFontSize(9).SetMarginBottom(12));

            // Body — bold section headings, mono body text
            foreach (string rawLine in reportText.Split('\n'))
            {
                bool   isHeading = rawLine.TrimStart().StartsWith(HeadingPrefix);
                string display   = isHeading
                    ? rawLine.Replace(HeadingPrefix, "")   // strip ## marker
                    : rawLine;

                doc.Add(new Paragraph(display)
                    .SetFont(isHeading ? boldFont : monoFont)
                    .SetFontSize(isHeading ? 9.5f : 8.5f)
                    .SetMarginBottom(0)
                    .SetMultipliedLeading(1.15f));
            }

            return path;
        }
    }
}
