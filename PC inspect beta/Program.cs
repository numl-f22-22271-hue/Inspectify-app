#nullable disable

using System;
using PC_inspect_beta.Core.Platform;

namespace PC_inspect_beta
{
    internal static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            Console.WriteLine($"Inspectify Scanner — Platform: {PlatformDetector.PlatformName}");

#if WINDOWS
            // Windows: Run the full WinForms UI (existing experience)
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
            System.Windows.Forms.Application.Run(new UI.Forms.Form1());
            return 0;
#else
            // Mac/Linux: Run Avalonia UI
            return UI.Avalonia.AvaloniaApp.Start(args);
#endif
        }
    }
}
