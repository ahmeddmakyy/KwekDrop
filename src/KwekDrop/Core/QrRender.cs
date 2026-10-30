using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace KwekDrop;

public enum QrModuleShape
{
    Square,
    Rounded,
    Dots,
    Diamond,
    Bars
}

public enum QrEyeShape
{
    Square,
    Rounded,
    Circle,
    Leaf
}

public sealed class QrDesign
{
    public QrModuleShape ModuleShape { get; init; } = QrModuleShape.Square;
    public QrEyeShape EyeShape { get; init; } = QrEyeShape.Square;
    public Color ForegroundColor { get; init; } = Colors.Black;
    public Color? GradientEndColor { get; init; } = null;
    public Color? EyeColor { get; init; } = null;
    public Color BackgroundColor { get; init; } = Colors.White;
    public bool IsTransparentBackground { get; init; } = false;
    public int Margin { get; init; } = 4;
    public int OutputSize { get; init; } = 512;
    public byte[]? LogoBytes { get; init; } = null;
}

public static class QrRender
{
    public static BitmapSource ToBitmapSource(QrMatrix matrix, QrDesign design)
    {
        int size = Math.Clamp(design.OutputSize, 128, 4096);
        var visual = ToVisual(matrix, design, size);
        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    public static DrawingVisual ToVisual(QrMatrix matrix, QrDesign design, int? sizeOverride = null)
    {
        int size = sizeOverride ?? Math.Clamp(design.OutputSize, 128, 4096);
        int margin = Math.Clamp(design.Margin, 1, 8);
        int totalModules = matrix.Size + 2 * margin;
        double moduleSize = (double)size / totalModules;

        var visual = new DrawingVisual();
        using var dc = visual.RenderOpen();

        // 1. Background
        if (!design.IsTransparentBackground)
        {
            var bgBrush = new SolidColorBrush(design.BackgroundColor);
            bgBrush.Freeze();
            dc.DrawRectangle(bgBrush, null, new Rect(0, 0, size, size));
        }

        // Brushes
        Brush fgBrush;
        if (design.GradientEndColor.HasValue)
        {
            var grad = new LinearGradientBrush(design.ForegroundColor, design.GradientEndColor.Value, new Point(0, 0), new Point(1, 1));
            grad.Freeze();
            fgBrush = grad;
        }
        else
        {
            var solid = new SolidColorBrush(design.ForegroundColor);
            solid.Freeze();
            fgBrush = solid;
        }

        Brush eyeBrush;
        if (design.EyeColor.HasValue)
        {
            var solidEye = new SolidColorBrush(design.EyeColor.Value);
            solidEye.Freeze();
            eyeBrush = solidEye;
        }
        else
        {
            eyeBrush = fgBrush;
        }

        // Logo region in module space
        bool hasLogo = design.LogoBytes != null && design.LogoBytes.Length > 0;
        Rect logoRectPx = Rect.Empty;
        if (hasLogo)
        {
            double maxLogoPx = size * 0.21;
            double logoX = (size - maxLogoPx) / 2.0;
            double logoY = (size - maxLogoPx) / 2.0;
            logoRectPx = new Rect(logoX, logoY, maxLogoPx, maxLogoPx);
        }

        // 2. Draw Eyes
        DrawEyesWpf(dc, eyeBrush, margin, matrix.Size, moduleSize, design.EyeShape);

        // 3. Draw Data & Function Modules (excluding Finder patterns and logo region)
        var moduleGeometry = new StreamGeometry { FillRule = FillRule.Nonzero };
        using (var ctx = moduleGeometry.Open())
        {
            for (int y = 0; y < matrix.Size; y++)
            {
                for (int x = 0; x < matrix.Size; x++)
                {
                    if (matrix.IsFinder(x, y)) continue;
                    if (!matrix.IsDark(x, y)) continue;

                    double px = (x + margin) * moduleSize;
                    double py = (y + margin) * moduleSize;
                    var modRect = new Rect(px, py, moduleSize, moduleSize);

                    if (hasLogo && logoRectPx.IntersectsWith(new Rect(px - 1, py - 1, moduleSize + 2, moduleSize + 2)))
                        continue;

                    AppendModuleGeometry(ctx, px, py, moduleSize, design.ModuleShape, matrix, x, y);
                }
            }
        }
        moduleGeometry.Freeze();
        dc.DrawGeometry(fgBrush, null, moduleGeometry);

        // 4. Draw Logo plate and image
        if (hasLogo)
        {
            DrawLogoWpf(dc, logoRectPx, design);
        }

        return visual;
    }

    private static void DrawEyesWpf(DrawingContext dc, Brush eyeBrush, int margin, int matrixSize, double modSize, QrEyeShape shape)
    {
        Point[] eyeOrigins = [
            new(margin * modSize, margin * modSize),
            new((margin + matrixSize - 7) * modSize, margin * modSize),
            new(margin * modSize, (margin + matrixSize - 7) * modSize)
        ];

        for (int i = 0; i < eyeOrigins.Length; i++)
        {
            var origin = eyeOrigins[i];
            var eyeGeo = new StreamGeometry { FillRule = FillRule.EvenOdd };
            using (var ctx = eyeGeo.Open())
            {
                AppendEyeFrameGeometry(ctx, origin.X, origin.Y, modSize * 7, modSize, shape, i);
                AppendEyeCoreGeometry(ctx, origin.X + 2 * modSize, origin.Y + 2 * modSize, modSize * 3, shape, i);
            }
            eyeGeo.Freeze();
            dc.DrawGeometry(eyeBrush, null, eyeGeo);
        }
    }

    private static void AppendEyeFrameGeometry(StreamGeometryContext ctx, double x, double y, double outerSize, double stroke, QrEyeShape shape, int eyeIndex)
    {
        double innerX = x + stroke;
        double innerY = y + stroke;
        double innerSize = outerSize - 2 * stroke;

        if (shape == QrEyeShape.Circle)
        {
            AppendCircle(ctx, x + outerSize / 2, y + outerSize / 2, outerSize / 2);
            AppendCircle(ctx, x + outerSize / 2, y + outerSize / 2, innerSize / 2);
        }
        else if (shape == QrEyeShape.Rounded)
        {
            double rOut = stroke * 1.8;
            double rIn = stroke * 0.9;
            AppendRoundedRect(ctx, new Rect(x, y, outerSize, outerSize), rOut);
            AppendRoundedRect(ctx, new Rect(innerX, innerY, innerSize, innerSize), rIn);
        }
        else if (shape == QrEyeShape.Leaf)
        {
            double rOut = stroke * 2.8;
            double rIn = stroke * 1.5;
            AppendLeafRect(ctx, new Rect(x, y, outerSize, outerSize), rOut, eyeIndex);
            AppendLeafRect(ctx, new Rect(innerX, innerY, innerSize, innerSize), rIn, eyeIndex);
        }
        else // Square
        {
            AppendRect(ctx, new Rect(x, y, outerSize, outerSize));
            AppendRect(ctx, new Rect(innerX, innerY, innerSize, innerSize));
        }
    }

    private static void AppendEyeCoreGeometry(StreamGeometryContext ctx, double x, double y, double coreSize, QrEyeShape shape, int eyeIndex)
    {
        if (shape == QrEyeShape.Circle)
        {
            AppendCircle(ctx, x + coreSize / 2, y + coreSize / 2, coreSize / 2);
        }
        else if (shape == QrEyeShape.Rounded)
        {
            AppendRoundedRect(ctx, new Rect(x, y, coreSize, coreSize), coreSize * 0.28);
        }
        else if (shape == QrEyeShape.Leaf)
        {
            AppendLeafRect(ctx, new Rect(x, y, coreSize, coreSize), coreSize * 0.45, eyeIndex);
        }
        else // Square
        {
            AppendRect(ctx, new Rect(x, y, coreSize, coreSize));
        }
    }

    private static void AppendModuleGeometry(StreamGeometryContext ctx, double x, double y, double s, QrModuleShape shape, QrMatrix matrix, int mx, int my)
    {
        switch (shape)
        {
            case QrModuleShape.Dots:
                AppendCircle(ctx, x + s * 0.5, y + s * 0.5, s * 0.44);
                break;

            case QrModuleShape.Diamond:
                ctx.BeginFigure(new Point(x + s * 0.5, y + s * 0.05), true, true);
                ctx.LineTo(new Point(x + s * 0.95, y + s * 0.5), true, false);
                ctx.LineTo(new Point(x + s * 0.5, y + s * 0.95), true, false);
                ctx.LineTo(new Point(x + s * 0.05, y + s * 0.5), true, false);
                break;

            case QrModuleShape.Rounded:
                AppendRoundedRect(ctx, new Rect(x + s * 0.06, y + s * 0.06, s * 0.88, s * 0.88), s * 0.32);
                break;

            case QrModuleShape.Bars:
                bool top = my > 0 && matrix.IsDark(mx, my - 1) && !matrix.IsFinder(mx, my - 1);
                bool bottom = my < matrix.Size - 1 && matrix.IsDark(mx, my + 1) && !matrix.IsFinder(mx, my + 1);
                double r = s * 0.42;
                AppendBarRect(ctx, new Rect(x + s * 0.08, y, s * 0.84, s), r, top, bottom);
                break;

            default: // Square
                AppendRect(ctx, new Rect(x, y, s, s));
                break;
        }
    }

    private static void AppendRect(StreamGeometryContext ctx, Rect r)
    {
        ctx.BeginFigure(r.TopLeft, true, true);
        ctx.LineTo(r.TopRight, true, false);
        ctx.LineTo(r.BottomRight, true, false);
        ctx.LineTo(r.BottomLeft, true, false);
    }

    private static void AppendRoundedRect(StreamGeometryContext ctx, Rect r, double radius)
    {
        radius = Math.Min(radius, Math.Min(r.Width / 2, r.Height / 2));
        ctx.BeginFigure(new Point(r.Left + radius, r.Top), true, true);
        ctx.LineTo(new Point(r.Right - radius, r.Top), true, false);
        ctx.ArcTo(new Point(r.Right, r.Top + radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
        ctx.LineTo(new Point(r.Right, r.Bottom - radius), true, false);
        ctx.ArcTo(new Point(r.Right - radius, r.Bottom), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
        ctx.LineTo(new Point(r.Left + radius, r.Bottom), true, false);
        ctx.ArcTo(new Point(r.Left, r.Bottom - radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
        ctx.LineTo(new Point(r.Left, r.Top + radius), true, false);
        ctx.ArcTo(new Point(r.Left + radius, r.Top), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
    }

    private static void AppendLeafRect(StreamGeometryContext ctx, Rect r, double radius, int eyeIndex)
    {
        // eyeIndex: 0 = Top-left, 1 = Top-right, 2 = Bottom-left
        // Top-left: round top-left and bottom-right
        // Top-right: round top-right and bottom-left
        // Bottom-left: round bottom-left and top-right
        bool roundTL = eyeIndex == 0;
        bool roundTR = eyeIndex == 1;
        bool roundBR = eyeIndex == 0 || eyeIndex == 2;
        bool roundBL = eyeIndex == 1 || eyeIndex == 2;

        double rtl = roundTL ? radius : 0;
        double rtr = roundTR ? radius : 0;
        double rbr = roundBR ? radius : 0;
        double rbl = roundBL ? radius : 0;

        ctx.BeginFigure(new Point(r.Left + rtl, r.Top), true, true);
        ctx.LineTo(new Point(r.Right - rtr, r.Top), true, false);
        if (rtr > 0) ctx.ArcTo(new Point(r.Right, r.Top + rtr), new Size(rtr, rtr), 0, false, SweepDirection.Clockwise, true, false);
        else ctx.LineTo(new Point(r.Right, r.Top), true, false);

        ctx.LineTo(new Point(r.Right, r.Bottom - rbr), true, false);
        if (rbr > 0) ctx.ArcTo(new Point(r.Right - rbr, r.Bottom), new Size(rbr, rbr), 0, false, SweepDirection.Clockwise, true, false);
        else ctx.LineTo(new Point(r.Right, r.Bottom), true, false);

        ctx.LineTo(new Point(r.Left + rbl, r.Bottom), true, false);
        if (rbl > 0) ctx.ArcTo(new Point(r.Left, r.Bottom - rbl), new Size(rbl, rbl), 0, false, SweepDirection.Clockwise, true, false);
        else ctx.LineTo(new Point(r.Left, r.Bottom), true, false);

        ctx.LineTo(new Point(r.Left, r.Top + rtl), true, false);
        if (rtl > 0) ctx.ArcTo(new Point(r.Left + rtl, r.Top), new Size(rtl, rtl), 0, false, SweepDirection.Clockwise, true, false);
        else ctx.LineTo(new Point(r.Left, r.Top), true, false);
    }

    private static void AppendBarRect(StreamGeometryContext ctx, Rect r, double radius, bool connectedTop, bool connectedBottom)
    {
        double rTop = connectedTop ? 0 : radius;
        double rBottom = connectedBottom ? 0 : radius;

        ctx.BeginFigure(new Point(r.Left + rTop, r.Top), true, true);
        ctx.LineTo(new Point(r.Right - rTop, r.Top), true, false);
        if (rTop > 0) ctx.ArcTo(new Point(r.Right, r.Top + rTop), new Size(rTop, rTop), 0, false, SweepDirection.Clockwise, true, false);
        else ctx.LineTo(new Point(r.Right, r.Top), true, false);

        ctx.LineTo(new Point(r.Right, r.Bottom - rBottom), true, false);
        if (rBottom > 0) ctx.ArcTo(new Point(r.Right - rBottom, r.Bottom), new Size(rBottom, rBottom), 0, false, SweepDirection.Clockwise, true, false);
        else ctx.LineTo(new Point(r.Right, r.Bottom), true, false);

        ctx.LineTo(new Point(r.Left + rBottom, r.Bottom), true, false);
        if (rBottom > 0) ctx.ArcTo(new Point(r.Left, r.Bottom - rBottom), new Size(rBottom, rBottom), 0, false, SweepDirection.Clockwise, true, false);
        else ctx.LineTo(new Point(r.Left, r.Bottom), true, false);

        ctx.LineTo(new Point(r.Left, r.Top + rTop), true, false);
        if (rTop > 0) ctx.ArcTo(new Point(r.Left + rTop, r.Top), new Size(rTop, rTop), 0, false, SweepDirection.Clockwise, true, false);
        else ctx.LineTo(new Point(r.Left, r.Top), true, false);
    }

    private static void AppendCircle(StreamGeometryContext ctx, double cx, double cy, double radius)
    {
        ctx.BeginFigure(new Point(cx - radius, cy), true, true);
        ctx.ArcTo(new Point(cx + radius, cy), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
        ctx.ArcTo(new Point(cx - radius, cy), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
    }

    private static void DrawLogoWpf(DrawingContext dc, Rect plateRect, QrDesign design)
    {
        try
        {
            using var ms = new MemoryStream(design.LogoBytes!);
            var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames[0];

            // Plate
            var plateBrush = new SolidColorBrush(design.IsTransparentBackground ? Colors.White : design.BackgroundColor);
            plateBrush.Freeze();
            var plateBorder = new Pen(new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)), 1);
            plateBorder.Freeze();
            double cornerRadius = plateRect.Width * 0.20;
            dc.DrawRoundedRectangle(plateBrush, plateBorder, plateRect, cornerRadius, cornerRadius);

            // Inner image
            double pad = plateRect.Width * 0.12;
            double availW = plateRect.Width - 2 * pad;
            double availH = plateRect.Height - 2 * pad;
            double aspect = (double)frame.PixelWidth / Math.Max(1, frame.PixelHeight);

            double imgW = availW;
            double imgH = availH;
            if (aspect > 1.0) imgH = imgW / aspect;
            else imgW = imgH * aspect;

            double imgX = plateRect.Left + (plateRect.Width - imgW) / 2.0;
            double imgY = plateRect.Top + (plateRect.Height - imgH) / 2.0;

            dc.DrawImage(frame, new Rect(imgX, imgY, imgW, imgH));
        }
        catch
        {
            // If image decoding fails, ignore logo
        }
    }

    public static string ToSvg(QrMatrix matrix, QrDesign design)
    {
        int size = Math.Clamp(design.OutputSize, 128, 4096);
        int margin = Math.Clamp(design.Margin, 1, 8);
        int totalModules = matrix.Size + 2 * margin;
        double moduleSize = (double)size / totalModules;

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {size} {size}\" width=\"{size}\" height=\"{size}\">");

        // Defs: Gradients
        string fgHex = ToHex(design.ForegroundColor);
        string fillRef = fgHex;
        if (design.GradientEndColor.HasValue)
        {
            string gradHex = ToHex(design.GradientEndColor.Value);
            sb.AppendLine("  <defs>");
            sb.AppendLine("    <linearGradient id=\"qr-grad\" x1=\"0%\" y1=\"0%\" x2=\"100%\" y2=\"100%\">");
            sb.AppendLine($"      <stop offset=\"0%\" stop-color=\"{fgHex}\"/>");
            sb.AppendLine($"      <stop offset=\"100%\" stop-color=\"{gradHex}\"/>");
            sb.AppendLine("    </linearGradient>");
            sb.AppendLine("  </defs>");
            fillRef = "url(#qr-grad)";
        }

        // Background
        if (!design.IsTransparentBackground)
        {
            string bgHex = ToHex(design.BackgroundColor);
            sb.AppendLine($"  <rect width=\"100%\" height=\"100%\" fill=\"{bgHex}\"/>");
        }

        // Logo region
        bool hasLogo = design.LogoBytes != null && design.LogoBytes.Length > 0;
        Rect logoRectPx = Rect.Empty;
        if (hasLogo)
        {
            double maxLogoPx = size * 0.21;
            double logoX = (size - maxLogoPx) / 2.0;
            double logoY = (size - maxLogoPx) / 2.0;
            logoRectPx = new Rect(logoX, logoY, maxLogoPx, maxLogoPx);
        }

        // Eyes
        string eyeFill = design.EyeColor.HasValue ? ToHex(design.EyeColor.Value) : fillRef;
        var eyePathData = new StringBuilder();
        Point[] eyeOrigins = [
            new(margin * moduleSize, margin * moduleSize),
            new((margin + matrix.Size - 7) * moduleSize, margin * moduleSize),
            new(margin * moduleSize, (margin + matrix.Size - 7) * moduleSize)
        ];

        for (int i = 0; i < eyeOrigins.Length; i++)
        {
            var origin = eyeOrigins[i];
            AppendEyeSvg(eyePathData, origin.X, origin.Y, moduleSize * 7, moduleSize, design.EyeShape, i);
        }
        sb.AppendLine($"  <path fill=\"{eyeFill}\" fill-rule=\"evenodd\" d=\"{eyePathData}\"/>");

        // Data modules
        var modPathData = new StringBuilder();
        for (int y = 0; y < matrix.Size; y++)
        {
            for (int x = 0; x < matrix.Size; x++)
            {
                if (matrix.IsFinder(x, y)) continue;
                if (!matrix.IsDark(x, y)) continue;

                double px = (x + margin) * moduleSize;
                double py = (y + margin) * moduleSize;

                if (hasLogo && logoRectPx.IntersectsWith(new Rect(px - 1, py - 1, moduleSize + 2, moduleSize + 2)))
                    continue;

                AppendModuleSvg(modPathData, px, py, moduleSize, design.ModuleShape, matrix, x, y);
            }
        }
        sb.AppendLine($"  <path fill=\"{fillRef}\" d=\"{modPathData}\"/>");

        // Logo
        if (hasLogo)
        {
            string plateBg = design.IsTransparentBackground ? "#FFFFFF" : ToHex(design.BackgroundColor);
            double rx = logoRectPx.Width * 0.20;
            sb.AppendLine($"  <rect x=\"{F(logoRectPx.X)}\" y=\"{F(logoRectPx.Y)}\" width=\"{F(logoRectPx.Width)}\" height=\"{F(logoRectPx.Height)}\" rx=\"{F(rx)}\" ry=\"{F(rx)}\" fill=\"{plateBg}\" stroke=\"rgba(0,0,0,0.15)\" stroke-width=\"1\"/>");

            string b64 = Convert.ToBase64String(design.LogoBytes!);
            double pad = logoRectPx.Width * 0.12;
            double imgW = logoRectPx.Width - 2 * pad;
            double imgH = logoRectPx.Height - 2 * pad;
            double imgX = logoRectPx.X + pad;
            double imgY = logoRectPx.Y + pad;
            sb.AppendLine($"  <image x=\"{F(imgX)}\" y=\"{F(imgY)}\" width=\"{F(imgW)}\" height=\"{F(imgH)}\" href=\"data:image/png;base64,{b64}\"/>");
        }

        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    private static void AppendEyeSvg(StringBuilder sb, double x, double y, double outerSize, double stroke, QrEyeShape shape, int eyeIndex)
    {
        double innerX = x + stroke;
        double innerY = y + stroke;
        double innerSize = outerSize - 2 * stroke;
        double coreX = x + 2 * stroke;
        double coreY = y + 2 * stroke;
        double coreSize = outerSize - 4 * stroke;

        if (shape == QrEyeShape.Circle)
        {
            AppendCircleSvg(sb, x + outerSize / 2, y + outerSize / 2, outerSize / 2);
            AppendCircleSvg(sb, x + outerSize / 2, y + outerSize / 2, innerSize / 2);
            AppendCircleSvg(sb, x + outerSize / 2, y + outerSize / 2, coreSize / 2);
        }
        else if (shape == QrEyeShape.Rounded)
        {
            AppendRoundedRectSvg(sb, x, y, outerSize, outerSize, stroke * 1.8);
            AppendRoundedRectSvg(sb, innerX, innerY, innerSize, innerSize, stroke * 0.9);
            AppendRoundedRectSvg(sb, coreX, coreY, coreSize, coreSize, coreSize * 0.28);
        }
        else if (shape == QrEyeShape.Leaf)
        {
            AppendLeafRectSvg(sb, x, y, outerSize, outerSize, stroke * 2.8, eyeIndex);
            AppendLeafRectSvg(sb, innerX, innerY, innerSize, innerSize, stroke * 1.5, eyeIndex);
            AppendLeafRectSvg(sb, coreX, coreY, coreSize, coreSize, coreSize * 0.45, eyeIndex);
        }
        else // Square
        {
            AppendRectSvg(sb, x, y, outerSize, outerSize);
            AppendRectSvg(sb, innerX, innerY, innerSize, innerSize);
            AppendRectSvg(sb, coreX, coreY, coreSize, coreSize);
        }
    }

    private static void AppendModuleSvg(StringBuilder sb, double x, double y, double s, QrModuleShape shape, QrMatrix matrix, int mx, int my)
    {
        switch (shape)
        {
            case QrModuleShape.Dots:
                AppendCircleSvg(sb, x + s * 0.5, y + s * 0.5, s * 0.44);
                break;
            case QrModuleShape.Diamond:
                sb.Append($"M{F(x + s * 0.5)} {F(y + s * 0.05)} L{F(x + s * 0.95)} {F(y + s * 0.5)} L{F(x + s * 0.5)} {F(y + s * 0.95)} L{F(x + s * 0.05)} {F(y + s * 0.5)} Z ");
                break;
            case QrModuleShape.Rounded:
                AppendRoundedRectSvg(sb, x + s * 0.06, y + s * 0.06, s * 0.88, s * 0.88, s * 0.32);
                break;
            case QrModuleShape.Bars:
                bool top = my > 0 && matrix.IsDark(mx, my - 1) && !matrix.IsFinder(mx, my - 1);
                bool bottom = my < matrix.Size - 1 && matrix.IsDark(mx, my + 1) && !matrix.IsFinder(mx, my + 1);
                AppendBarRectSvg(sb, x + s * 0.08, y, s * 0.84, s, s * 0.42, top, bottom);
                break;
            default:
                AppendRectSvg(sb, x, y, s, s);
                break;
        }
    }

    private static void AppendRectSvg(StringBuilder sb, double x, double y, double w, double h)
    {
        sb.Append($"M{F(x)} {F(y)} H{F(x + w)} V{F(y + h)} H{F(x)} Z ");
    }

    private static void AppendCircleSvg(StringBuilder sb, double cx, double cy, double r)
    {
        sb.Append($"M{F(cx - r)} {F(cy)} A{F(r)} {F(r)} 0 1 0 {F(cx + r)} {F(cy)} A{F(r)} {F(r)} 0 1 0 {F(cx - r)} {F(cy)} Z ");
    }

    private static void AppendRoundedRectSvg(StringBuilder sb, double x, double y, double w, double h, double r)
    {
        r = Math.Min(r, Math.Min(w / 2, h / 2));
        sb.Append($"M{F(x + r)} {F(y)} H{F(x + w - r)} A{F(r)} {F(r)} 0 0 1 {F(x + w)} {F(y + r)} V{F(y + h - r)} A{F(r)} {F(r)} 0 0 1 {F(x + w - r)} {F(y + h)} H{F(x + r)} A{F(r)} {F(r)} 0 0 1 {F(x)} {F(y + h - r)} V{F(y + r)} A{F(r)} {F(r)} 0 0 1 {F(x + r)} {F(y)} Z ");
    }

    private static void AppendLeafRectSvg(StringBuilder sb, double x, double y, double w, double h, double r, int eyeIndex)
    {
        bool roundTL = eyeIndex == 0;
        bool roundTR = eyeIndex == 1;
        bool roundBR = eyeIndex == 0 || eyeIndex == 2;
        bool roundBL = eyeIndex == 1 || eyeIndex == 2;

        double rtl = roundTL ? r : 0;
        double rtr = roundTR ? r : 0;
        double rbr = roundBR ? r : 0;
        double rbl = roundBL ? r : 0;

        sb.Append($"M{F(x + rtl)} {F(y)} H{F(x + w - rtr)} ");
        if (rtr > 0) sb.Append($"A{F(rtr)} {F(rtr)} 0 0 1 {F(x + w)} {F(y + rtr)} ");
        else sb.Append($"L{F(x + w)} {F(y)} ");

        sb.Append($"V{F(y + h - rbr)} ");
        if (rbr > 0) sb.Append($"A{F(rbr)} {F(rbr)} 0 0 1 {F(x + w - rbr)} {F(y + h)} ");
        else sb.Append($"L{F(x + w)} {F(y + h)} ");

        sb.Append($"H{F(x + rbl)} ");
        if (rbl > 0) sb.Append($"A{F(rbl)} {F(rbl)} 0 0 1 {F(x)} {F(y + h - rbl)} ");
        else sb.Append($"L{F(x)} {F(y + h)} ");

        sb.Append($"V{F(y + rtl)} ");
        if (rtl > 0) sb.Append($"A{F(rtl)} {F(rtl)} 0 0 1 {F(x + rtl)} {F(y)} ");
        else sb.Append($"L{F(x)} {F(y)} ");
        sb.Append("Z ");
    }

    private static void AppendBarRectSvg(StringBuilder sb, double x, double y, double w, double h, double r, bool top, bool bottom)
    {
        double rTop = top ? 0 : r;
        double rBottom = bottom ? 0 : r;

        sb.Append($"M{F(x + rTop)} {F(y)} H{F(x + w - rTop)} ");
        if (rTop > 0) sb.Append($"A{F(rTop)} {F(rTop)} 0 0 1 {F(x + w)} {F(y + rTop)} ");
        else sb.Append($"L{F(x + w)} {F(y)} ");

        sb.Append($"V{F(y + h - rBottom)} ");
        if (rBottom > 0) sb.Append($"A{F(rBottom)} {F(rBottom)} 0 0 1 {F(x + w - rBottom)} {F(y + h)} ");
        else sb.Append($"L{F(x + w)} {F(y + h)} ");

        sb.Append($"H{F(x + rBottom)} ");
        if (rBottom > 0) sb.Append($"A{F(rBottom)} {F(rBottom)} 0 0 1 {F(x)} {F(y + h - rBottom)} ");
        else sb.Append($"L{F(x)} {F(y + h)} ");

        sb.Append($"V{F(y + rTop)} ");
        if (rTop > 0) sb.Append($"A{F(rTop)} {F(rTop)} 0 0 1 {F(x + rTop)} {F(y)} ");
        else sb.Append($"L{F(x)} {F(y)} ");
        sb.Append("Z ");
    }

    private static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    private static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    public static bool VerifyScannability(QrMatrix matrix, QrDesign design, out string warning)
    {
        warning = "";

        // 1. Non-square eyes may fail on standard barcode / OpenCV detectors
        if (design.EyeShape != QrEyeShape.Square)
        {
            warning = "May not scan. Use a smaller logo, more contrast or higher correction.";
            return false;
        }

        // 2. Check contrast & inverted colors
        double lFg = RelativeLuminance(design.ForegroundColor);
        if (design.GradientEndColor.HasValue)
        {
            double lGrad = RelativeLuminance(design.GradientEndColor.Value);
            lFg = Math.Min(lFg, lGrad); // worst-case
        }

        double lBg = design.IsTransparentBackground ? 1.0 : RelativeLuminance(design.BackgroundColor);

        // Inverted: light on dark
        if (lFg >= lBg)
        {
            warning = "May not scan. Use a smaller logo, more contrast or higher correction.";
            return false;
        }

        double contrast = (Math.Max(lFg, lBg) + 0.05) / (Math.Min(lFg, lBg) + 0.05);
        if (contrast < 3.2)
        {
            warning = "May not scan. Use a smaller logo, more contrast or higher correction.";
            return false;
        }

        // 3. Check Logo size and EC level
        bool hasLogo = design.LogoBytes != null && design.LogoBytes.Length > 0;
        if (hasLogo && matrix.Correction < QrCorrection.Q)
        {
            warning = "May not scan. Use a smaller logo, more contrast or higher correction.";
            return false;
        }

        return true;
    }

    private static double RelativeLuminance(Color c)
    {
        double Linear(double v)
        {
            double s = v / 255.0;
            return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Linear(c.R) + 0.7152 * Linear(c.G) + 0.0722 * Linear(c.B);
    }
}
