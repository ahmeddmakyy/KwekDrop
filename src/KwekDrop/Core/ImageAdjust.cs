using System;
using System.Threading.Tasks;

namespace KwekDrop;

internal static class ImageAdjust
{
    public static double ComputeMeanLuminance(byte[] bgra)
    {
        if (bgra == null || bgra.Length < 4)
            return 128.0;

        long sum = 0;
        int pixels = bgra.Length / 4;
        for (int i = 0; i < bgra.Length; i += 4)
        {
            sum += bgra[i + 2] * 299L + bgra[i + 1] * 587L + bgra[i] * 114L;
        }
        return (double)sum / (pixels * 1000.0);
    }

    public static void Apply(
        byte[] src,
        byte[] dst,
        int width,
        int height,
        double exposure = 0.0,
        double contrast = 1.0,
        double highlights = 0.0,
        double shadows = 0.0,
        double saturation = 1.0,
        double temperature = 0.0,
        double? baseMean = null)
    {
        if (src == null || dst == null)
            throw new ArgumentNullException(src == null ? nameof(src) : nameof(dst));
        if (width <= 0 || height <= 0)
            return;

        int totalBytes = width * height * 4;
        if (src.Length < totalBytes || dst.Length < totalBytes)
            throw new ArgumentException("Buffer size is smaller than width * height * 4");

        // Identity fast-path
        if (Math.Abs(exposure) < 1e-4 &&
            Math.Abs(contrast - 1.0) < 1e-4 &&
            Math.Abs(highlights) < 1e-4 &&
            Math.Abs(shadows) < 1e-4 &&
            Math.Abs(saturation - 1.0) < 1e-4 &&
            Math.Abs(temperature) < 1e-4)
        {
            Buffer.BlockCopy(src, 0, dst, 0, totalBytes);
            return;
        }

        double expFactor = Math.Pow(2.0, exposure);
        double mean = (baseMean ?? 128.0) * expFactor;
        mean = Math.Clamp(mean, 0.0, 255.0);

        byte[] lut = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            double bright = Math.Clamp(i * expFactor, 0.0, 255.0);
            double val = mean + contrast * (bright - mean);
            lut[i] = (byte)Math.Clamp(Math.Round(val), 0.0, 255.0);
        }

        double tempCoeff = temperature / 100.0;
        double rTempScale = 1.0 + tempCoeff * 0.25;
        double bTempScale = 1.0 - tempCoeff * 0.25;

        double shFactor = (shadows / 100.0) * 0.4 * 255.0;
        double hlFactor = (highlights / 100.0) * 0.3 * 255.0;

        int stride = width * 4;

        Parallel.For(0, height, y =>
        {
            int rowStart = y * stride;
            int rowEnd = rowStart + stride;

            for (int i = rowStart; i < rowEnd; i += 4)
            {
                double b = lut[src[i]];
                double g = lut[src[i + 1]];
                double r = lut[src[i + 2]];

                // Saturation (Color balance)
                if (Math.Abs(saturation - 1.0) >= 1e-4)
                {
                    double gray = 0.299 * r + 0.587 * g + 0.114 * b;
                    r = gray + saturation * (r - gray);
                    g = gray + saturation * (g - gray);
                    b = gray + saturation * (b - gray);
                }

                // Temperature
                if (Math.Abs(temperature) >= 1e-4)
                {
                    r *= rTempScale;
                    b *= bTempScale;
                }

                // Shadows and Highlights
                if (Math.Abs(shadows) >= 1e-4 || Math.Abs(highlights) >= 1e-4)
                {
                    double lum = (r + g + b) / (3.0 * 255.0);
                    double invLum = 1.0 - lum;
                    double delta = shFactor * invLum * invLum + hlFactor * lum * lum;
                    r += delta;
                    g += delta;
                    b += delta;
                }

                dst[i] = (byte)Math.Clamp(Math.Round(b), 0.0, 255.0);
                dst[i + 1] = (byte)Math.Clamp(Math.Round(g), 0.0, 255.0);
                dst[i + 2] = (byte)Math.Clamp(Math.Round(r), 0.0, 255.0);
                dst[i + 3] = src[i + 3];
            }
        });
    }
}
