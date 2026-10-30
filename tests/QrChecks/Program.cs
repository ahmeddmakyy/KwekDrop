using System;
using System.IO;
using System.Windows;
using KwekDrop;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };

        if (args.Length > 0 && args[0] == "--debug-qr-render")
        {
            if (QrDebug.TryRun(args, out int exitCode))
                return exitCode;
            return 1;
        }

        // Smoke tests
        Console.WriteLine("Running QrChecks smoke tests...");
        var m1 = QrMatrix.Encode("HELLO WORLD", QrCorrection.Q);
        if (m1 == null || m1.Size != 21)
        {
            Console.Error.WriteLine($"HELLO WORLD failed: size={m1?.Size}");
            return 1;
        }

        var m2 = QrMatrix.Encode("https://example.com", QrCorrection.M);
        if (m2 == null || m2.Size != 25)
        {
            Console.Error.WriteLine($"https://example.com failed: size={m2?.Size}");
            return 1;
        }

        var design = new QrDesign { ModuleShape = QrModuleShape.Square, OutputSize = 256 };
        var bmp = QrRender.ToBitmapSource(m1, design);
        if (bmp.PixelWidth != 256 || bmp.PixelHeight != 256)
        {
            Console.Error.WriteLine($"Render failed: {bmp.PixelWidth}x{bmp.PixelHeight}");
            return 1;
        }

        string svg = QrRender.ToSvg(m1, design);
        if (!svg.Contains("<svg") || !svg.Contains("</svg>"))
        {
            Console.Error.WriteLine("SVG generation failed");
            return 1;
        }

        Console.WriteLine("QrChecks smoke tests passed!");
        return 0;
    }
}
