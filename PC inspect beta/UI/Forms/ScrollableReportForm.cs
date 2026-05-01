#nullable disable

using System.Drawing;
using System.Windows.Forms;

namespace PC_inspect_beta.UI.Forms
{
    /// <summary>
    /// A scrollable, read-only monospace text viewer used to display the diagnostic report.
    /// </summary>
    public class ScrollableReportForm : Form
    {
        public ScrollableReportForm(string title, string body, Form owner = null)
        {
            this.Text            = title;
            this.Size            = new Size(720, 580);
            this.StartPosition   = FormStartPosition.CenterParent;
            this.BackColor       = Theme.DarkBackground;
            this.ForeColor       = Theme.TextColor;
            this.FormBorderStyle = FormBorderStyle.Sizable;

            var txt = new TextBox
            {
                Multiline   = true,
                Dock        = DockStyle.Fill,
                ScrollBars  = ScrollBars.Vertical,
                Text        = body,
                ReadOnly    = true,
                Font        = Theme.MonoFont,
                BackColor   = Theme.PanelBackground,
                ForeColor   = Color.White,
                BorderStyle = BorderStyle.None,
                WordWrap    = false
            };
            this.Controls.Add(txt);

            if (owner != null) this.ShowDialog(owner);
            else               this.ShowDialog();
        }

        /// <summary>Static convenience wrapper — opens the form immediately.</summary>
        public static void Show(string title, string body, Form owner = null)
            => new ScrollableReportForm(title, body, owner);
    }
}
