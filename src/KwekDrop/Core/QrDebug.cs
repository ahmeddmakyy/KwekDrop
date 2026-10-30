using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace KwekDrop;

public static class QrDebug
{
    public static bool TryRun(string[] args) => TryRun(args, out _);

    public static bool TryRun(string[] args, out int exitCode)
    {
        exitCode = 0;
        if (args.Length < 3 || args[0] != "--debug-qr-render")
            return false;

        try
        {
            string text = args[1];
            string outPath = Path.GetFullPath(args[2]);
            string? dir = Path.GetDirectoryName(outPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var level = args.Length > 3 ? ParseCorrection(args[3]) : QrCorrection.M;
            var shape = args.Length > 4 ? ParseShape(args[4]) : QrModuleShape.Square;
            var eyeShape = args.Length > 5 ? ParseEyeShape(args[5]) : QrEyeShape.Square;
            Color fg = args.Length > 6 ? ParseColor(args[6], Colors.Black) : Colors.Black;
            Color bg = args.Length > 7 ? ParseColor(args[7], Colors.White) : Colors.White;
            Color? grad = args.Length > 8 && !string.IsNullOrWhiteSpace(args[8]) && args[8] != "none" ? ParseColor(args[8], Colors.Black) : null;
            byte[]? logo = args.Length > 9 && File.Exists(args[9]) ? File.ReadAllBytes(args[9]) : null;

            if (logo != null && level < QrCorrection.Q)
                level = QrCorrection.Q;

            var matrix = QrMatrix.Encode(text, level);
            if (matrix == null)
            {
                Console.Error.WriteLine($"Text too long to encode in QR: {text.Length} chars");
                exitCode = 1;
                return true;
            }

            var design = new QrDesign
            {
                ModuleShape = shape,
                EyeShape = eyeShape,
                ForegroundColor = fg,
                BackgroundColor = bg,
                GradientEndColor = grad,
                LogoBytes = logo,
                OutputSize = 512,
                Margin = 4
            };

            var bitmap = QrRender.ToBitmapSource(matrix, design);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(outPath))
            {
                encoder.Save(stream);
            }

            exitCode = 0;
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Error in --debug-qr-render: " + ex);
            exitCode = 1;
            return true;
        }
    }

    private static QrCorrection ParseCorrection(string s) => s?.Trim().ToUpperInvariant() switch
    {
        "L" => QrCorrection.L,
        "Q" => QrCorrection.Q,
        "H" => QrCorrection.H,
        _ => QrCorrection.M
    };

    private static QrModuleShape ParseShape(string s) => s?.Trim().ToLowerInvariant() switch
    {
        "rounded" => QrModuleShape.Rounded,
        "dots" or "dot" => QrModuleShape.Dots,
        "diamond" => QrModuleShape.Diamond,
        "bars" or "bar" => QrModuleShape.Bars,
        _ => QrModuleShape.Square
    };

    private static QrEyeShape ParseEyeShape(string s) => s?.Trim().ToLowerInvariant() switch
    {
        "rounded" => QrEyeShape.Rounded,
        "circle" => QrEyeShape.Circle,
        "leaf" => QrEyeShape.Leaf,
        _ => QrEyeShape.Square
    };

    private static Color ParseColor(string s, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(s)) return fallback;
        try
        {
            return (Color)ColorConverter.ConvertFromString(s.StartsWith('#') ? s : "#" + s);
        }
        catch
        {
            return fallback;
        }
    }
}
