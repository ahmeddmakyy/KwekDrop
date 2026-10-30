using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Forms = System.Windows.Forms;
using ShapePath = System.Windows.Shapes.Path;

namespace KwekDrop;

// KwekDrop: The zero-click offline file converter wheel for Windows.
// Styled after Tangerine for Mac with live frosted glass backdrop, citrus wedge petals,
// floating center capsule pill, bloom animation, and mechanical tick sound on sector transitions.
internal sealed class DropWheel : Window
{
    public static string? GetToolIcon(string id)
    {
        if (string.IsNullOrEmpty(id) || id.StartsWith("convert:") || id.StartsWith("pack:"))
            return null;

        string key = id.ToLowerInvariant();

        if (key.Contains("compress") || key.Contains("size") || key.Contains("resolution"))
            return "M4 14h6v6m10-10h-6V4m0 6 7-7M4 20l7-7";
        if (key.Contains("trim") || key.Contains("split"))
            return "M6 9a3 3 0 1 0 0-6 3 3 0 0 0 0 6zM6 21a3 3 0 1 0 0-6 3 3 0 0 0 0 6zM20 4L8.12 15.88M14.47 14.48L20 20M8.12 8.12L12 12";
        if (key.Contains("crop") || key.Contains("rotate"))
            return "M6 2v14a2 2 0 0 0 2 2h14M18 22V8a2 2 0 0 0-2-2H2";
        if (key.Contains("resize") || key.Contains("settings"))
            return "M15 3h6v6M9 21H3v-6M21 3l-7 7M3 21l7-7";
        if (key.Contains("subtitles"))
            return "M4 5h16a1 1 0 0 1 1 1v12a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V6a1 1 0 0 1 1-1zM7 12h4M13 12h4M7 16h10";
        if (key.Contains("watermark") || key.Contains("redact") || key.Contains("password"))
            return "M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z";
        if (key.Contains("location") || key.Contains("gps") || key.Contains("map"))
            return "M21 10c0 7-9 13-9 13S3 17 3 10a9 9 0 0 1 18 0zM12 13a3 3 0 1 0 0-6 3 3 0 0 0 0 6z";
        if (key.Contains("metadata"))
            return "M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8zM14 2v6h6M16 13H8M16 17H8";
        if (key.Contains("ocr") || key.Contains("text") || key.Contains("pdfnumbers"))
            return "M3 7V5a2 2 0 0 1 2-2h2M17 3h2a2 2 0 0 1 2 2v2M21 17v2a2 2 0 0 1-2 2h-2M7 21H5a2 2 0 0 1-2-2v-2M9 9h6M9 13h6";
        if (key.Contains("edit") || key.Contains("effects"))
            return "M12 20h9M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4L16.5 3.5z";
        if (key.Contains("frame") || key.Contains("collage"))
            return "M3 3h18v18H3zM9 3v18M15 3v18M3 9h18M3 15h18";
        if (key.Contains("icon"))
            return "M12 2l3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14 2 9.27l6.91-1.01L12 2z";
        if (key.Contains("video") || key.Contains("gif") || key.Contains("animation") || key.Contains("snapshots"))
            return "M23 7l-7 5 7 5V7zM14 5H3a2 2 0 0 0-2 2v10a2 2 0 0 0 2 2h11a2 2 0 0 0 2-2V7a2 2 0 0 0-2-2z";
        if (key.Contains("audio") || key.Contains("sound") || key.Contains("ringtone") || key.Contains("mute"))
            return "M9 18V5l12-2v13M9 9l12-2M6 21a3 3 0 1 0 0-6 3 3 0 0 0 0 6zM18 19a3 3 0 1 0 0-6 3 3 0 0 0 0 6z";
        if (key.Contains("pdf"))
            return "M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8zM14 2v6h6";
        if (key.Contains("pack") || key.Contains("archive") || key.Contains("extract"))
            return "M21 8v13H3V8M1 3h22v5H1zM10 12h4";
        if (key.Contains("share"))
            return "M4 12v8a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-8M16 6l-4-4-4 4M12 2v13";
        if (key.Contains("copypath"))
            return "M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71";
        if (key.Contains("copy"))
            return "M9 9h11a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2H9a2 2 0 0 1-2-2v-9a2 2 0 0 1 2-2zM5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1";
        if (key.Contains("float"))
            return "M12 2v8M12 18v4M4.93 4.93l5.66 5.66M19.07 4.93l-5.66 5.66";
        if (key.Contains("move"))
            return "M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2zM12 11l4 4m0 0l-4 4m4-4H8";
        if (key.Contains("terminal"))
            return "M4 17l6-6-6-6M12 19h8";

        return "M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z";
    }

    private sealed class Ring
    {
        public readonly List<ShapePath> Petals = [];
        public readonly List<FrameworkElement> Labels = [];
        public int Highlighted = -1;
    }
    private readonly Action<string[], Operation> submit;
    private readonly Grid stage = new();
    private readonly Canvas canvas = new() { Width = 380, Height = 380 };
    private readonly TextBlock count = new(), hint = new();
    private readonly Ring first = new(), second = new();
    private readonly Border center;
    private readonly Border centerPill;
    private bool centerSelected;
    private sealed record Look(Brush RestingFace, Brush SelectedFace, Brush OpenFace, Brush RestingOutline, Brush SelectedOutline, Brush OpenOutline, Brush OuterFace, Brush Disk, Brush Hub, Brush HubEdge);
    
    // Translucent glass colors so background desktop & wallpaper softly blur through (matching Image 4)
    // Exact Tangerine Citrus Glass styling matching Tangerine for Mac (Screenshot 2026-10-20 213228.png)
    private static readonly Look LightLook = new(
        RestingFace: Solid("#EBF8F9FA"),       // 92% white translucent glass petals
        SelectedFace: Solid("#FFFF5200"),      // vivid radiant Tangerine orange
        OpenFace: Solid("#C0FFF1E6"),
        RestingOutline: Solid("#385D6666"),    // 22% slate hairline stroke
        SelectedOutline: Solid("#FFFF8833"),   // glowing orange rim
        OpenOutline: Solid("#C0FFC48A"),
        OuterFace: Solid("#99FFFFFF"),
        Disk: Solid("#40C0C9CD"),              // 25% cool grey-cyan disc tint
        Hub: Solid("#B8FFFFFF"),               // 72% white center capsule pill
        HubEdge: Solid("#B3FFFFFF")            // 70% white specular rim
    );
    private static readonly Look DarkLook = new(
        RestingFace: Solid("#C820242C"),       // sleek translucent dark frosted glass
        SelectedFace: Solid("#FFFF5200"),      // vivid radiant Tangerine orange
        OpenFace: Solid("#D0303642"),          // soft warm slate
        RestingOutline: Solid("#40FFFFFF"),    // crisp frosted rim
        SelectedOutline: Solid("#FFFF8833"),
        OpenOutline: Solid("#80FFA066"),
        OuterFace: Solid("#B01C2026"),
        Disk: Solid("#30101216"),              // deep frosted backing disk
        Hub: Solid("#E02C3038"),               // slate center capsule
        HubEdge: Solid("#50FFFFFF")
    );

    private static Look Shade => UiTheme.Dark ? DarkLook : LightLook;
    private static Brush RestingFace => Shade.RestingFace;
    private static Brush SelectedFace => Shade.SelectedFace;
    private static Brush OpenFace => Shade.OpenFace;
    private static Brush RestingOutline => Shade.RestingOutline;
    private static Brush SelectedOutline => Shade.SelectedOutline;
    private static Brush OpenOutline => Shade.OpenOutline;
    private static Brush OuterFace => Shade.OuterFace;
    private readonly Ellipse disk;
    private readonly Ellipse backdropDisk;
    private readonly Ellipse ambientGlow;
    private readonly Ellipse innerRing;
    private List<Operation> operations = [];
    private List<ToolGroup>? groups;
    private int openGroup = -1;
    private string[] paths = [];
    private bool entered, eligible, allowedCopy;
    private int mode; // 0 = Convert, 1 = Tools, 2 = Actions
    public int WheelMode => mode;
    public bool ToolsMode => mode == 1;
    public bool ActionsMode => mode == 2;
    public long Instance { get; private set; }
    
    private static string[] cachedPaths = [];
    public static bool HasCachedDrag => cachedPaths.Length > 0 && Native.Down(1);
    public static void ClearCachedPaths() => cachedPaths = [];
    public bool HasFileDrag => (entered && paths.Length > 0) || (cachedPaths.Length > 0 && Native.Down(1));
    public double StageSize { get; private set; } = SingleSize;
    private const double SingleSize = 380, DoubleSize = 640;
    private const double Center = 190, Outer = 162, Inner = 52, OuterRingInner = 176, OuterRingOuter = 290;
    private readonly ScaleTransform wheelScale = new(1, 1, Center, Center);

    public DropWheel(Action<string[], Operation> submit)
    {
        this.submit = submit;
        Title = "KwekDrop";
        Icon = AppIcon.Window;
        Width = Height = SingleSize;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        AllowDrop = true;
        FontFamily = UiTheme.Font;

        // Frosted glass circular backdrop
                // Ambient Backdrop Underlay: rich radial ambient glow matching macOS frosted glass
        // Backdrop circular blur: captures live screen directly behind wheel, strictly clipped to circle
        backdropDisk = new Ellipse
        {
            Width = 340,
            Height = 340,
            IsHitTestVisible = false,
            Clip = new EllipseGeometry(new Point(170, 170), 170, 170)
        };
        Canvas.SetLeft(backdropDisk, 20);
        Canvas.SetTop(backdropDisk, 20);
        Canvas.SetZIndex(backdropDisk, 0);
        canvas.RenderTransform = wheelScale;
        canvas.Children.Add(backdropDisk);

        ambientGlow = new Ellipse
        {
            Width = 340,
            Height = 340,
            Clip = new EllipseGeometry(new Point(170, 170), 170, 170),
            Fill = GlowBrush(UiTheme.Dark),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(ambientGlow, 20);
        Canvas.SetTop(ambientGlow, 20);
        Canvas.SetZIndex(ambientGlow, 1);
        canvas.Children.Add(ambientGlow);

        disk = new Ellipse
        {
            Width = 340,
            Height = 340,
            Fill = Shade.Disk,
            Stroke = UiTheme.Dark ? Solid("#40FFA066") : Solid("#55FFFFFF"),
            StrokeThickness = 1.0,
            IsHitTestVisible = false
        };
        // A soft shadow made of a plain gradient: window effects (blur, drop shadow) are drawn in software and stall the wheel while it animates.
        shadowDisk = new Ellipse
        {
            Width = 372,
            Height = 372,
            IsHitTestVisible = false,
            Visibility = UiTheme.Dark ? Visibility.Collapsed : Visibility.Visible,
            Fill = new RadialGradientBrush(new GradientStopCollection { new GradientStop(Color.FromArgb(44, 0x26, 0x34, 0x38), 0.0), new GradientStop(Color.FromArgb(48, 0x26, 0x34, 0x38), 0.90), new GradientStop(Color.FromArgb(0, 0x26, 0x34, 0x38), 1.0) })
        };
        Canvas.SetLeft(shadowDisk, 14);
        Canvas.SetTop(shadowDisk, 19);
        canvas.Children.Add(shadowDisk);
        Canvas.SetLeft(disk, 20);
        Canvas.SetTop(disk, 20);
        canvas.Children.Add(disk);

        // Inner divider ring framing the floating capsule (exact Tangerine style)
        innerRing = new Ellipse
        {
            Width = Inner * 2,
            Height = Inner * 2,
            Fill = Brushes.Transparent,
            Stroke = UiTheme.Dark ? Solid("#50FFA066") : Solid("#6693A6BA"),
            StrokeThickness = 1.0,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(innerRing, Center - Inner);
        Canvas.SetTop(innerRing, Center - Inner);
        Canvas.SetZIndex(innerRing, 10);
        canvas.Children.Add(innerRing);

        // Center hub: large circular hub
        center = new Border
        {
            Width = 96,
            Height = 96,
            CornerRadius = new CornerRadius(48),
            Background = Shade.Hub,
            BorderBrush = Shade.HubEdge,
            BorderThickness = new Thickness(1.0)
        };

        centerPill = new Border
        {
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(12, 5, 12, 5),
            Background = Brushes.Transparent,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 90
        };

        count.Text = "CONVERT";
        count.TextAlignment = TextAlignment.Center;
        count.FontSize = FitLabelSize(count.Text, 76, 13, 8.5);
        count.FontWeight = FontWeights.Bold;
        count.Foreground = UiTheme.Ink;
        count.VerticalAlignment = VerticalAlignment.Center;
        count.HorizontalAlignment = HorizontalAlignment.Center;
        count.TextWrapping = TextWrapping.Wrap;
        count.LineHeight = 15;
        count.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;

        centerPill.Child = count;
        center.Child = centerPill;
        center.SizeChanged += (_, _) => PositionCenter();
        Canvas.SetLeft(center, Center - 48);
        Canvas.SetTop(center, Center - 48);
        Canvas.SetZIndex(center, 25);
        canvas.Children.Add(center);

        stage.Children.Add(canvas);
        Content = stage;
        PreviewDragEnter += Enter;
        PreviewDragOver += Over;
        PreviewDragLeave += (_, e) =>
        {
            var p = e.GetPosition(this);
            if (p.X < 0 || p.Y < 0 || p.X > ActualWidth || p.Y > ActualHeight)
            {
                Highlight(first, -1);
                Highlight(second, -1);
            }
            e.Handled = true;
        };
        PreviewDrop += OnDrop;
    }

    private void Recolor()
    {
        UiTheme.Refresh();
        disk.Fill = Shade.Disk;
        disk.Stroke = UiTheme.Dark ? Solid("#40FFA066") : Solid("#55FFFFFF");
        shadowDisk.Visibility = UiTheme.Dark ? Visibility.Collapsed : Visibility.Visible;
        ambientGlow.Fill = GlowBrush(UiTheme.Dark);
        innerRing.Stroke = UiTheme.Dark ? Solid("#50FFA066") : Solid("#6693A6BA");
        center.Background = Shade.Hub;
        center.BorderBrush = Shade.HubEdge;
        centerPill.Background = centerSelected ? UiTheme.Accent : Brushes.Transparent;
        count.Foreground = centerSelected ? Brushes.White : UiTheme.Ink;
        hint.Foreground = UiTheme.Muted;
    }

    private void PositionCenter()
    {
        double width = center.ActualWidth > 0 ? center.ActualWidth : center.Width;
        double height = center.ActualHeight > 0 ? center.ActualHeight : center.MinHeight;
        Canvas.SetLeft(center, Center - width / 2);
        Canvas.SetTop(center, Center - height / 2);
    }

    private void SetCenterText(string text, bool selected = false)
    {
        centerSelected = selected;
        count.Text = text;
        count.FontSize = FitLabelSize(text, selected ? 66 : 76, 13, 8.5);
        centerPill.Background = selected ? UiTheme.Accent : Brushes.Transparent;
        count.Foreground = selected ? Brushes.White : UiTheme.Ink;
    }

    private double FitLabelSize(string text, double width, double preferred, double minimum = 8.5)
    {
        string[] words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            return preferred;

        var typeface = new Typeface(UiTheme.Font, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        double pixels = 1.0;
        try { pixels = VisualTreeHelper.GetDpi(this).PixelsPerDip; } catch { }
        var direction = L.Arabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        double size = preferred;
        while (size > minimum)
        {
            bool fits = true;
            foreach (string word in words)
            {
                var measure = new FormattedText(word, CultureInfo.CurrentUICulture, direction, typeface, size, Brushes.Black, pixels);
                if (measure.WidthIncludingTrailingWhitespace > width - 1)
                {
                    fits = false;
                    break;
                }
            }
            if (fits)
                break;
            size = Math.Max(minimum, size - 0.25);
        }
        return size;
    }

    private double FitWheelTitleSize(IEnumerable<string> labels, double width, double preferred)
    {
        double size = preferred;
        foreach (string label in labels)
            size = Math.Min(size, FitLabelSize(label, width, size));
        return size;
    }

    public void OpenAt(Native.POINT point, int wheelMode)
    {
        Recolor();
        Instance++;
        this.mode = wheelMode;
        if (cachedPaths.Length > 0 && Native.Down(1))
        {
            paths = cachedPaths;
            entered = allowedCopy = true;
            Populate();
        }
        else
        {
            paths = [];
            entered = false;
            eligible = allowedCopy = false;
            groups = null;
            SetCenterText(wheelMode == 2 ? (L.Arabic ? "إجراءات" : "ACTIONS") :
                          wheelMode == 1 ? (L.Arabic ? "أدوات" : "TOOLS") :
                          (L.Arabic ? "تحويل" : "CONVERT"));
            hint.Text = wheelMode == 2 ? (L.Arabic ? "إجراءات الملفات" : "FILE ACTIONS") :
                        wheelMode == 1 ? (L.Arabic ? "أدوات الملفات" : "TOOLS") :
                        (L.Arabic ? "تحويل الصيغ" : "CONVERT");
            Render([]);
            Resize(SingleSize);
        }

        // Keep the window fully invisible until it has been placed and the desktop
        // behind it has been captured. Showing first is what left the wheel itself
        // in the backdrop (the large ghost circle) and produced a misplaced first frame.
        wheelScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        wheelScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        canvas.BeginAnimation(OpacityProperty, null);
        stage.BeginAnimation(OpacityProperty, null);
        BeginAnimation(OpacityProperty, null);
        stage.RenderTransformOrigin = new Point(0.5, 0.5);
        var scale = new ScaleTransform(0.08, 0.08);
        stage.RenderTransform = scale;
        stage.Opacity = 0.0;
        canvas.Opacity = 0.0;
        Opacity = 0.0;
        if (!IsVisible)
            Show();

        var hwnd = new WindowInteropHelper(this).Handle;
        var area = Forms.Screen.FromPoint(new System.Drawing.Point(point.X, point.Y)).WorkingArea;
        double dpiScale = Native.GetDpiForWindow(hwnd) / 96.0;
        int size = (int)Math.Round(SingleSize * dpiScale);
        int x = Math.Clamp(point.X - size / 2, area.Left, Math.Max(area.Left, area.Right - size)), y = Math.Clamp(point.Y - size / 2, area.Top, Math.Max(area.Top, area.Bottom - size));
        int backdropOffset = (int)Math.Round(20 * dpiScale), backdropSize = (int)Math.Round(340 * dpiScale);
        var captureWatch = System.Diagnostics.Stopwatch.StartNew();
        CaptureBackdrop(x + backdropOffset, y + backdropOffset, backdropSize);
        LastCaptureMs = captureWatch.ElapsedMilliseconds;
        Native.SetWindowPos(hwnd, new IntPtr(-1), x, y, size, size, 0x0010 | 0x0040);
        WindowBlur.ClipToCircle(this);
        UpdateLayout();

        // The wheel only shows once a real file drag is confirmed (the drag enters this window carrying files, or the
        // paths were cached from the current drag). Until then it is armed but practically invisible: still hit-testable
        // so the drag can enter it, but a rubber-band selection, a text selection or a window drag never flashes it.
        if (entered)
            Reveal();
        else
        {
            revealed = false;
            stage.RenderTransform = new ScaleTransform(1, 1);
            stage.Opacity = 0.1;
            canvas.Opacity = 0.1;
            Opacity = 1.0;
        }
    }

    private bool revealed;
    public void RevealNow() { if (IsVisible) Reveal(); }
    private Ellipse shadowDisk = null!;
    public long LastCaptureMs { get; private set; }

    private void Reveal()
    {
        if (revealed)
            return;
        revealed = true;
        // Appears the way the reference wheel does: fade in over 120 ms while growing from 90% to full size in 220 ms
        // with a small overshoot. No size change from a point, so the pop feels like the wheel was already there.
        stage.BeginAnimation(OpacityProperty, null);
        canvas.BeginAnimation(OpacityProperty, null);
        stage.Opacity = 1.0;
        canvas.Opacity = 1.0;
        wheelScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        wheelScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        var scale = new ScaleTransform(0.9, 0.9);
        stage.RenderTransformOrigin = new Point(0.5, 0.5);
        stage.RenderTransform = scale;
        var grow = new DoubleAnimation(0.9, 1.0, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new BackEase { Amplitude = 0.3, EasingMode = EasingMode.EaseOut }
        };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        BeginAnimation(OpacityProperty, new DoubleAnimation(0.2, 1.0, TimeSpan.FromMilliseconds(120)));
    }

    private void Resize(double size)
    {
        if (Math.Abs(size - StageSize) < .5)
            return;
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero || !IsVisible)
        { StageSize = size; Width = Height = size; return; }
        Native.GetWindowRect(hwnd, out var current);
        double scale = Native.GetDpiForWindow(hwnd) / 96.0;
        int pixels = (int)Math.Round(size * scale), midX = (current.Left + current.Right) / 2, midY = (current.Top + current.Bottom) / 2;
        var area = Forms.Screen.FromPoint(new System.Drawing.Point(midX, midY)).WorkingArea;
        int x = Math.Clamp(midX - pixels / 2, area.Left, Math.Max(area.Left, area.Right - pixels)), y = Math.Clamp(midY - pixels / 2, area.Top, Math.Max(area.Top, area.Bottom - pixels));
        StageSize = size;
        Native.SetWindowPos(hwnd, new IntPtr(-1), x, y, pixels, pixels, 0x0010 | 0x0040);
        WindowBlur.ClipToCircle(this);
        if (backdropDisk != null)
        {
            if (size > SingleSize + 10)
            {
                backdropDisk.Width = backdropDisk.Height = 580;
                Canvas.SetLeft(backdropDisk, 30);
                Canvas.SetTop(backdropDisk, 30);
                backdropDisk.Clip = new EllipseGeometry(new Point(290, 290), 290, 290);
            }
            else
            {
                backdropDisk.Width = backdropDisk.Height = 340;
                Canvas.SetLeft(backdropDisk, 20);
                Canvas.SetTop(backdropDisk, 20);
                backdropDisk.Clip = new EllipseGeometry(new Point(170, 170), 170, 170);
            }
        }
        UpdateLayout();
    }

    private void CaptureBackdrop(int screenX, int screenY, int size)
    {
        try
        {
            using var bmp = new System.Drawing.Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(screenX, screenY, 0, 0, new System.Drawing.Size(size, size), System.Drawing.CopyPixelOperation.SourceCopy);
            }
            // Blur once on the CPU (shrink, soften, let WPF scale it back up) instead of a live BlurEffect that is redrawn
            // in software on every animation frame.
            double radius = Math.Max(2, UiTheme.BlurAmount * 0.55);
            int factor = Math.Clamp((int)Math.Round(radius / 3), 2, 14);
            int small = Math.Max(8, size / factor);
            using var shrunk = new System.Drawing.Bitmap(small, small, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g2 = System.Drawing.Graphics.FromImage(shrunk))
            {
                g2.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
                g2.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                g2.DrawImage(bmp, new System.Drawing.Rectangle(0, 0, small, small));
            }
            var data = shrunk.LockBits(new System.Drawing.Rectangle(0, 0, small, small), System.Drawing.Imaging.ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try
            {
                var pixels = new byte[Math.Abs(data.Stride) * small];
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
                BoxBlur(pixels, small, small, data.Stride, 2);
                BoxBlur(pixels, small, small, data.Stride, 2);
                System.Runtime.InteropServices.Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
            }
            finally { shrunk.UnlockBits(data); }
            var hBitmap = shrunk.GetHbitmap();
            try
            {
                var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap, IntPtr.Zero, Int32Rect.Empty,
                    System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                RenderOptions.SetBitmapScalingMode(backdropDisk, BitmapScalingMode.Fant);
                backdropDisk.Fill = new ImageBrush(source) { Stretch = Stretch.UniformToFill };
            }
            finally
            {
                Native.DeleteObject(hBitmap);
            }
        }
        catch { }
    }

    private static void BoxBlur(byte[] px, int width, int height, int stride, int radius)
    {
        var tmp = new byte[px.Length];
        for (int pass = 0; pass < 2; pass++)
        {
            var src = pass == 0 ? px : tmp;
            var dst = pass == 0 ? tmp : px;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int r = 0, gC = 0, b = 0, n = 0;
                    for (int k = -radius; k <= radius; k++)
                    {
                        int xx = pass == 0 ? Math.Clamp(x + k, 0, width - 1) : x, yy = pass == 0 ? y : Math.Clamp(y + k, 0, height - 1);
                        int i = yy * stride + xx * 4;
                        b += src[i]; gC += src[i + 1]; r += src[i + 2]; n++;
                    }
                    int o = y * stride + x * 4;
                    dst[o] = (byte)(b / n); dst[o + 1] = (byte)(gC / n); dst[o + 2] = (byte)(r / n); dst[o + 3] = 255;
                }
        }
    }

    public void Dismiss(string reason)
    {
        if (!IsVisible)
            return;
        Hide();
        revealed = false;
        if (backdropDisk != null) backdropDisk.Fill = null;
        paths = [];
        eligible = false;
        entered = false;
        Journal.Write("WheelDismissed", new { reason });
    }

    public void Preview(string[] files, int wheelMode, string highlightId)
    {
        mode = wheelMode;
        paths = files;
        entered = allowedCopy = true;
        Populate();
        if (groups != null)
        {
            int group = groups.FindIndex(g => IndexOf(g.Items, highlightId) >= 0);
            if (group >= 0)
            {
                OpenGroup(group);
                Highlight(second, IndexOf(groups[group].Items, highlightId));
            }
        }
        else
            Highlight(first, IndexOf(operations, highlightId));
    }

    private static int IndexOf(List<Operation> actions, string id) =>
        actions.FindIndex(action => action.Id == id || (action.Parts?.Contains(id) ?? false));

    public void ChangeMode(int nextMode)
    {
        mode = nextMode;
        if (!entered)
        {
            SetCenterText(mode == 2 ? (L.Arabic ? "إجراءات" : "ACTIONS") :
                          mode == 1 ? (L.Arabic ? "أدوات" : "TOOLS") :
                          (L.Arabic ? "تحويل" : "CONVERT"));
            hint.Text = mode == 2 ? (L.Arabic ? "إجراءات الملفات" : "FILE ACTIONS") :
                        mode == 1 ? (L.Arabic ? "أدوات الملفات" : "TOOLS") :
                        (L.Arabic ? "تحويل الصيغ" : "CONVERT");
            return;
        }
        Populate();
    }

    private void Populate()
    {
        var all = paths.Length <= 200 ? (mode == 2 ? Catalog.FileActions.ToList() : Catalog.Options(paths, mode == 1)) : [];
        groups = (mode == 1) && paths.Length > 0 && Catalog.TwoWheels ? Catalog.Grouped(all, Catalog.Category(paths[0])) : null;
        openGroup = -1;
        RenderSecond(null);
        operations = groups == null ? all : groups.Select((g, i) => new Operation("__group:" + i, g.Label)).ToList();
        eligible = allowedCopy && all.Count > 0;
        bool packing = Catalog.PackingOnly(paths);
        SetCenterText(eligible ? (mode == 2 ? (L.Arabic ? "إجراءات" : "ACTIONS") : (packing && mode != 1 ? (L.Arabic ? "أرشيف" : "ARCHIVE") : (paths.Length == 1 ? (L.Arabic ? "ملف 1" : "1 FILE") : (L.Arabic ? $"{paths.Length} ملفات" : $"{paths.Length} FILES")))) : (L.Arabic ? "غير مدعوم" : "UNSUPPORTED"));
        hint.Text = mode == 2 ? (L.Arabic ? "اختر إجراء لتنفيذه" : "Choose action") : (packing && mode != 1 ? (L.Arabic ? "لا يدعم التحويل المباشر" : "Direct conversion not supported") : (eligible ? Prompt() : (L.Arabic ? "إلغاء" : "Cancel")));
        Render(operations);
        Resize(groups != null ? DoubleSize : SingleSize);
    }

    private string Prompt() => groups != null ? (L.Arabic ? "مرر للتصنيف ثم اختر" : "Hover to browse categories") : (L.Arabic ? "أفلت الماوس لبدء التحويل" : "Release mouse to convert");

    private void Enter(object sender, DragEventArgs e)
    {
        if (!entered)
        {
            entered = true;
            try
            {
                paths = e.Data.GetDataPresent(DataFormats.FileDrop) ? e.Data.GetData(DataFormats.FileDrop) as string[] ?? [] : [];
                if (paths.Length > 0) cachedPaths = paths;
            }
            catch { paths = []; }
            allowedCopy = (e.AllowedEffects & DragDropEffects.Copy) != 0;
            Populate();
            if (paths.Length > 0)
                Reveal();
            Journal.Write("DragEntered", new { count = paths.Length, eligible, category = paths.Length > 0 ? Catalog.Category(paths[0]) : "none", allowed = e.AllowedEffects.ToString(), operations = (groups?.SelectMany(g => g.Items) ?? operations).Select(x => x.Id) });
        }
        e.Effects = eligible ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Over(object sender, DragEventArgs e)
    {
        if (!entered)
            Enter(sender, e);
        bool actionable = false;
        if (eligible)
        {
            var point = e.GetPosition(canvas);
            int tool = openGroup >= 0 ? Hit(second, point, OuterRingInner, OuterRingOuter + 10) : -1;
            int category = tool >= 0 ? -1 : Hit(first, point, Inner, Outer);
            if (tool >= 0)
            { Highlight(second, tool); actionable = true; }
            else if (groups != null && category >= 0)
            { OpenGroup(category); Highlight(second, -1); actionable = true; }
            else if (groups == null)
            { Highlight(first, category); actionable = category >= 0; }
            else
                Highlight(second, -1);
        }
        e.Effects = actionable ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (!entered)
            Enter(sender, e);
        Operation? selected = null;
        if (eligible)
        {
            var point = e.GetPosition(canvas);
            if (openGroup >= 0 && Hit(second, point, OuterRingInner, OuterRingOuter + 10) is var tool and >= 0)
                selected = groups![openGroup].Items[tool];
            else if (groups == null && Hit(first, point, Inner, Outer) is var index and >= 0)
                selected = operations[index];
        }
        if (selected == null)
        { e.Effects = DragDropEffects.None; e.Handled = true; Dismiss("InvalidDrop"); return; }
        var files = paths.ToArray();
        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
        Hide();
        paths = [];
        entered = false;
        eligible = false;
        Journal.Write("DropAccepted", new { Action = selected.Id, count = files.Length, effect = "Copy" });
        Dispatcher.InvokeAsync(() => submit(files, selected));
    }

    private static Point Polar(double radius, double angle) => new(Center + radius * Math.Cos(angle), Center + radius * Math.Sin(angle));

    private static Brush Solid(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }

    private static Brush GlowBrush(bool dark)
    {
        var brush = new RadialGradientBrush
        {
            GradientOrigin = new Point(0.5, 0.5),
            Center = new Point(0.5, 0.5),
            RadiusX = 0.5,
            RadiusY = 0.5,
            GradientStops = new GradientStopCollection
            {
                dark ? new GradientStop(Color.FromArgb(56, 255, 100, 30), 0.0) : new GradientStop(Color.FromArgb(64, 186, 166, 255), 0.0),
                dark ? new GradientStop(Color.FromArgb(46, 120, 160, 255), 0.50) : new GradientStop(Color.FromArgb(44, 255, 176, 196), 0.50),
                new GradientStop(Color.FromArgb(0, 255, 255, 255), 0.85)
            }
        };
        brush.Freeze();
        return brush;
    }

    // Tangerine exact rounded sector geometry: curved outer and inner boundaries with rounded corners
    private static Geometry TangerineSector(double start, double end, double inner = Inner, double outer = Outer, double gap = 4.5, double corner = 20.0)
    {
        double r(double R) => Math.Asin(Math.Min(0.95, gap / (2.0 * Math.Max(R, gap / 2.0 + 0.01))));

        double d = start + r(outer);
        double i = end - r(outer);
        double c = start + r(inner);
        double g = end - r(inner);

        if (i <= d || g <= c)
        {
            d = start; i = end; c = start; g = end;
        }

        double spanOuter = i - d;
        double spanInner = g - c;
        double f = Math.Min(0.18, spanOuter * 0.22);
        double u = Math.Min(0.18, spanInner * 0.18);

        double v = inner / Math.Max(0.01, Math.Cos(u));
        double y = v + inner * Math.Tan(u);
        double O = outer - corner;

        Point PolarLocal(double radius, double angle) =>
            new(Center + radius * Math.Cos(angle), Center + radius * Math.Sin(angle));

        Point p1 = PolarLocal(y, start + r(y));
        Point p2 = PolarLocal(O, start + r(O));
        Point q1_ctrl = PolarLocal(outer, d);
        Point q1_end = PolarLocal(outer, d + f);

        Point arc_outer_end = PolarLocal(outer, i - f);
        bool large_arc_outer = (i - f - (d + f)) > Math.PI;

        Point q2_ctrl = PolarLocal(outer, i);
        Point q2_end = PolarLocal(O, end - r(O));

        Point p3 = PolarLocal(y, end - r(y));
        Point q3_ctrl = PolarLocal(v, end - r(v));
        Point q3_end = PolarLocal(inner, g - u);

        Point arc_inner_end = PolarLocal(inner, c + u);
        bool large_arc_inner = (g - u - (c + u)) > Math.PI;

        Point q4_ctrl = PolarLocal(v, start + r(v));

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(p1, true, true);
            context.LineTo(p2, true, false);
            context.QuadraticBezierTo(q1_ctrl, q1_end, true, false);
            context.ArcTo(arc_outer_end, new Size(outer, outer), 0, large_arc_outer, SweepDirection.Clockwise, true, false);
            context.QuadraticBezierTo(q2_ctrl, q2_end, true, false);
            context.LineTo(p3, true, false);
            context.QuadraticBezierTo(q3_ctrl, q3_end, true, false);
            context.ArcTo(arc_inner_end, new Size(inner, inner), 0, large_arc_inner, SweepDirection.Counterclockwise, true, false);
            context.QuadraticBezierTo(q4_ctrl, p1, true, false);
        }
        geometry.Freeze();
        return geometry;
    }

    private void Clear(Ring ring)
    {
        foreach (var shape in ring.Petals)
            canvas.Children.Remove(shape);
        foreach (var label in ring.Labels)
            canvas.Children.Remove(label);
        ring.Petals.Clear();
        ring.Labels.Clear();
        ring.Highlighted = -1;
    }

    private void AddPetal(Ring ring, double start, double end, double inner, double outer, double labelRadius, double labelWidth, string title, string? icon, string? preview, double titleSize, Brush face, int layer)
    {
        var geometry = TangerineSector(start, end, inner, outer, gap: 4.5, corner: (outer - inner) > 90 ? 20.0 : 16.0);
        var shape = new ShapePath
        {
            Data = geometry,
            Fill = face,
            Stroke = RestingOutline,
            StrokeThickness = 0.95,
            Opacity = 0.95
        };
        Canvas.SetZIndex(shape, layer + 1);
        ring.Petals.Add(shape);
        canvas.Children.Add(shape);

        var label = new StackPanel { Width = labelWidth, IsHitTestVisible = false };
        if (!string.IsNullOrEmpty(icon))
        {
            try
            {
                var iconShape = new ShapePath
                {
                    Data = Geometry.Parse(icon),
                    Stroke = UiTheme.Dark ? Solid("#FFF0E2") : Solid("#361E0E"),
                    StrokeThickness = 1.6,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    StrokeLineJoin = PenLineJoin.Round,
                    Width = 17,
                    Height = 17,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 2)
                };
                label.Children.Add(iconShape);
            }
            catch { }
        }

        label.Children.Add(new TextBlock
        {
            Text = title,
            FontFamily = UiTheme.Font,
            Foreground = UiTheme.Dark ? Solid("#FFF0E2") : Solid("#361E0E"),
            FontSize = titleSize,
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        });
        if (preview != null)
            label.Children.Add(new TextBlock
            {
                Text = preview,
                FontFamily = UiTheme.Font,
                Foreground = UiTheme.Muted,
                FontSize = 9,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxHeight = 26,
                Margin = new Thickness(0, 2, 0, 0)
            });
        label.Measure(new Size(labelWidth, double.PositiveInfinity));
        Point p = Polar(labelRadius, (start + end) / 2);
        Canvas.SetLeft(label, p.X - labelWidth / 2);
        Canvas.SetTop(label, p.Y - label.DesiredSize.Height / 2);
        Canvas.SetZIndex(label, layer + 2);
        ring.Labels.Add(label);
        canvas.Children.Add(label);
    }

    private void Render(List<Operation> actions)
    {
        Clear(first);
        if (actions.Count == 0)
        {
            UpdateLayout();
            return;
        }
        double half = Math.PI / actions.Count;
        double labelRadius = (Inner + Outer) / 2;
        double labelWidth = Math.Clamp(2 * labelRadius * Math.Sin(half) - 8, 58, groups != null ? 92 : 104);
        string?[] icons = actions.Select((action, i) => groups != null
            ? (GetToolIcon(groups[i].Items.FirstOrDefault()?.Id ?? "") ?? GetToolIcon(groups[i].Label))
            : GetToolIcon(action.Id)).ToArray();
        double baseSize = (groups != null ? 13 : actions.Count > 7 ? 12 : 13.5) - 1.5;
        double titleSize = FitWheelTitleSize(actions.Select(a => Shown(a.Label)), labelWidth,
            icons.Any(icon => !string.IsNullOrEmpty(icon)) ? Math.Max(8.5, baseSize - 1.5) : Math.Max(8.5, baseSize));
        for (int i = 0; i < actions.Count; i++)
        {
            double angle = -Math.PI / 2 + i * 2 * Math.PI / actions.Count;
            string? preview = groups != null ? Preview(groups[i]) : null;
            AddPetal(first, angle - half, angle + half, Inner, Outer, labelRadius, labelWidth, Shown(actions[i].Label), icons[i], preview, titleSize, RestingFace, 1);
        }
        UpdateLayout();
    }

    private static string Preview(ToolGroup group) => string.Join(" · ", group.Items.Take(2).Select(o => o.Label)) + (group.Items.Count > 2 ? " …" : "");

    private void OpenGroup(int index)
    {
        if (groups == null || index == openGroup)
            return;
        openGroup = index;
        RenderSecond(groups[index]);
        for (int i = 0; i < first.Petals.Count; i++)
            Paint(first, i, i == index ? OpenFace : RestingFace, i == index ? OpenOutline : RestingOutline, UiTheme.Ink);
        hint.Text = L.Arabic ? "اختر أداة ثم أفلت الماوس" : "Select a tool and release";
    }

    private void RenderSecond(ToolGroup? group)
    {
        Clear(second);
        if (group == null || groups == null)
            return;
        int groupIndex = groups.IndexOf(group), n = group.Items.Count;
        double direction = -Math.PI / 2 + groupIndex * 2 * Math.PI / groups.Count;
        double width = Math.Min(Math.PI * 2 * .92 / n, 32 * Math.PI / 180), total = width * n;
        double labelWidth = Math.Clamp(2 * 232 * Math.Sin(width / 2) - 16, 70, 104);
        double titleSize = FitWheelTitleSize(group.Items.Select(item => Shown(item.Label)), labelWidth,
            group.Items.Any(item => !string.IsNullOrEmpty(GetToolIcon(item.Id))) ? Math.Max(8.5, 11.5 - 1.5) : 11.5);
        for (int i = 0; i < n; i++)
        {
            double middle = direction - total / 2 + width * (i + .5), half = width / 2;
            string? icon2 = GetToolIcon(group.Items[i].Id);
            AddPetal(second, middle - half, middle + half, OuterRingInner, OuterRingOuter, 232, labelWidth, Shown(group.Items[i].Label), icon2, null, titleSize, OuterFace, 20);
        }
        UpdateLayout();
    }

    private static int Hit(Ring ring, Point point, double inner, double outer)
    {
        double distance = (point - new Point(Center, Center)).Length;
        if (distance <= inner || distance > outer)
            return -1;
        for (int i = 0; i < ring.Petals.Count; i++)
            if (ring.Petals[i].Data.FillContains(point))
                return i;
        return -1;
    }

    private static void Paint(Ring ring, int index, Brush face, Brush outline, Brush text)
    {
        ring.Petals[index].Fill = face;
        ring.Petals[index].Stroke = outline;
        var blocks = ((StackPanel)ring.Labels[index]).Children.OfType<TextBlock>().ToList();
        for (int i = 0; i < blocks.Count; i++)
            blocks[i].Foreground = i == 0 || text == Brushes.White ? text : UiTheme.Muted;
    }

    private void Highlight(Ring ring, int index)
    {
        if (index == ring.Highlighted)
            return;

        // Authentic mechanical tick sound plays EXCLUSIVELY on sector transitions!
        if (index >= 0)
            SoundEffects.PlayTick();

        ring.Highlighted = index;
        for (int i = 0; i < ring.Petals.Count; i++)
        {
            bool selected = i == index;
            bool openCategory = ring == first && groups != null && i == openGroup;
            Brush face = selected ? SelectedFace : openCategory ? OpenFace : ring == second ? OuterFace : RestingFace;
            Brush outline = selected ? SelectedOutline : openCategory ? OpenOutline : RestingOutline;
            Paint(ring, i, face, outline, selected ? Brushes.White : (UiTheme.Dark ? Solid("#FFF0E2") : Solid("#361E0E")));
        }

        // Center floating capsule dynamically displays hovered tool / format in bold text
        if (ring == second && index >= 0 && openGroup >= 0)
        {
            SetCenterText(CenterLabel(groups![openGroup].Items[index]), selected: true);
            hint.Text = groups![openGroup].Items[index].Label;
        }
        else if (ring == first)
        {
            if (index >= 0 && index < operations.Count)
            {
                SetCenterText(CenterLabel(operations[index]), selected: true);
                hint.Text = operations[index].Label;
            }
            else
            {
                SetCenterText(paths.Length == 1 ? (L.Arabic ? "ملف 1" : "1 FILE") : paths.Length > 1 ? (L.Arabic ? $"{paths.Length} ملفات" : $"{paths.Length} FILES") : (mode == 2 ? (L.Arabic ? "إجراءات" : "ACTIONS") : mode == 1 ? (L.Arabic ? "أدوات" : "TOOLS") : (L.Arabic ? "تحويل" : "CONVERT")), selected: false);
                hint.Text = Catalog.HasPages(paths) ? L.T("Apple Pages 文件暂不支持；请先在 Pages 中导出为 PDF。") : Catalog.PackingOnly(paths) ? (L.Arabic ? "لا يدعم التحويل المباشر" : "Direct conversion not supported") : Prompt();
            }
        }
        else if (openGroup >= 0)
        {
            SetCenterText(CenterLabel(groups![openGroup].Label), selected: true);
            hint.Text = groups![openGroup].Label;
        }
    }

    private static string Shown(string label) => L.Arabic ? label : label.ToUpperInvariant();

    private static string CenterLabel(Operation operation) => Shown(operation.Label);

    private static string CenterLabel(string label) => Shown(label);
}
