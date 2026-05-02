#nullable disable

using System;
using PC_inspect_beta.Core.Platform;

namespace PC_inspect_beta
{
    internal static class Program
    {
        static int Main(string[] args)
        {
            Console.WriteLine($"Inspectify Scanner — Platform: {PlatformDetector.PlatformName}");
            return UI.Avalonia.AvaloniaApp.Start(args);
        }
    }
}
