using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace KwekDrop;

internal static class WindowBlur
{
    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateEllipticRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int cx, int cy);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreatePolygonRgn(POINT[] points, int count, int fillMode);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public int AccentState;
        public int AccentFlags;
        public int GradientColor;
        public int AnimationId;
    }

    public static void ApplyCircularBlur(Window window, int left, int top, int right, int bottom, bool dark = false)
    {
        var helper = new WindowInteropHelper(window);
        IntPtr hwnd = helper.Handle;
        if (hwnd == IntPtr.Zero)
        {
            window.SourceInitialized += (_, _) => ApplyCircularBlur(window, left, top, right, bottom, dark);
            return;
        }

        try
        {
            double scale = Native.GetDpiForWindow(hwnd) / 96.0;
            int pxLeft = (int)Math.Round(left * scale);
            int pxTop = (int)Math.Round(top * scale);
            int pxRight = (int)Math.Round(right * scale);
            int pxBottom = (int)Math.Round(bottom * scale);

            IntPtr hrgn = CreateEllipticRgn(pxLeft, pxTop, pxRight, pxBottom);
            SetWindowRgn(hwnd, hrgn, true);

            ApplyAccent(hwnd, dark);
        }
        catch { }
    }

    // What was applied to a window last. A layered window that gets its region and blur set several times while it is
    // still opening can stay invisible (only the blur is drawn), so the first application waits until the first frame
    // is on screen and later calls debounce to avoid smearing while the window settles to its final size.
    private sealed class GlassState
    {
        public bool Armed;
        public int Width, Height;
        public bool Dark;
        public int Blur;
        public DispatcherTimer? DebounceTimer;
    }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Window, GlassState> States = new();

    public static void ApplyRoundedRectBlur(Window window, int cornerRadius, bool dark = false)
    {
        var helper = new WindowInteropHelper(window);
        IntPtr hwnd = helper.Handle;
        if (hwnd == IntPtr.Zero)
        {
            window.SourceInitialized += (_, _) => ApplyRoundedRectBlur(window, cornerRadius, dark);
            return;
        }

        var state = States.GetOrCreateValue(window);
        if (!state.Armed)
        {
            state.Armed = true;
            window.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
            {
                Apply(window, hwnd, state, cornerRadius, dark, force: true);
                window.InvalidateVisual();
            }));
            return;
        }

        state.DebounceTimer ??= new DispatcherTimer(
            TimeSpan.FromMilliseconds(35),
            DispatcherPriority.ContextIdle,
            (_, _) =>
            {
                state.DebounceTimer?.Stop();
                Apply(window, hwnd, state, cornerRadius, dark, force: false);
            },
            window.Dispatcher);

        state.DebounceTimer.Stop();
        state.DebounceTimer.Start();
    }

    private static void Apply(Window window, IntPtr hwnd, GlassState state, int cornerRadius, bool dark, bool force)
    {
        try
        {
            Native.GetWindowRect(hwnd, out var rect);
            int win32W = rect.Right - rect.Left;
            int win32H = rect.Bottom - rect.Top;
            double scale = Native.GetDpiForWindow(hwnd) / 96.0;
            int wpfW = (int)Math.Ceiling(window.ActualWidth * scale);
            int wpfH = (int)Math.Ceiling(window.ActualHeight * scale);
            int width = Math.Max(win32W, wpfW);
            int height = Math.Max(win32H, wpfH);

            int currentBlur = UiTheme.BlurAmount;
            if (!force && width == state.Width && height == state.Height && dark == state.Dark && currentBlur == state.Blur)
                return;
            state.Width = width;
            state.Height = height;
            state.Dark = dark;
            state.Blur = currentBlur;

            if (width > 0 && height > 0)
            {
                SetWindowRgn(hwnd, OutlineRegion(width, height, cornerRadius, scale), true);
            }

            int preference = 1; // DWMWCP_DONOTROUND: prevents DWM white border streaks on custom SetWindowRgn
            DwmSetWindowAttribute(hwnd, 33, ref preference, sizeof(int));

            ApplyAccent(hwnd, dark);
        }
        catch { }
    }

    // The blur only exists inside the window region, so the region must be the exact outline the frame draws
    // (ContinuousFrame's smooth corners). A plain round-rect region has a different corner curve and lets bits of
    // unveiled blur show outside the frame at the corners.
    private static IntPtr OutlineRegion(int widthPx, int heightPx, double cornerRadius, double scale)
    {
        var outline = ContinuousFrame.Outline(new Rect(.5, .5, Math.Max(1, widthPx / scale - 1), Math.Max(1, heightPx / scale - 1)), cornerRadius);
        var flat = outline.GetFlattenedPathGeometry(0.05, System.Windows.Media.ToleranceType.Absolute);
        var points = new System.Collections.Generic.List<POINT>();
        foreach (var figure in flat.Figures)
        {
            points.Add(new POINT { X = (int)Math.Round(figure.StartPoint.X * scale), Y = (int)Math.Round(figure.StartPoint.Y * scale) });
            foreach (var segment in figure.Segments)
                if (segment is System.Windows.Media.PolyLineSegment line)
                    foreach (var point in line.Points)
                        points.Add(new POINT { X = (int)Math.Round(point.X * scale), Y = (int)Math.Round(point.Y * scale) });
            break;
        }
        return CreatePolygonRgn(points.ToArray(), points.Count, 2);
    }

    // For the round wheel window: anything the system or a shell tweak paints around the window (a backdrop, shadow or
    // corner effect) is cut off at the circle, and no system backdrop is requested for it.
    public static void ClipToCircle(Window window)
    {
        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
            return;
        try
        {
            Native.GetWindowRect(hwnd, out var rect);
            SetWindowRgn(hwnd, CreateEllipticRgn(0, 0, rect.Right - rect.Left + 1, rect.Bottom - rect.Top + 1), true);
            int none = 1; // DWMSBT_NONE
            DwmSetWindowAttribute(hwnd, 38, ref none, sizeof(int));
            int doNotRound = 1; // DWMWCP_DONOTROUND
            DwmSetWindowAttribute(hwnd, 33, ref doNotRound, sizeof(int));
        }
        catch { }
    }

    public static void EnableBlur(Window window, bool dark = false)
    {
        var helper = new WindowInteropHelper(window);
        IntPtr hwnd = helper.Handle;
        if (hwnd == IntPtr.Zero)
        {
            window.SourceInitialized += (_, _) => EnableBlur(window, dark);
            return;
        }

        try
        {
            ApplyAccent(hwnd, dark);
        }
        catch { }
    }

    private static void ApplyAccent(IntPtr hwnd, bool dark)
    {
        // AccentState = 3 (ACCENT_ENABLE_BLURBEHIND): Live frosted glass blur behind window.
        // Fast, hardware-accelerated by DWM, respects custom SetWindowRgn boundaries.
        var policy = new AccentPolicy
        {
            AccentState = UiTheme.BlurAmount <= 0 ? 0 : 3,
            AccentFlags = 0,
            GradientColor = 0
        };

        int size = Marshal.SizeOf(policy);
        IntPtr pPolicy = Marshal.AllocHGlobal(size);
        Marshal.StructureToPtr(policy, pPolicy, false);

        var data = new WindowCompositionAttributeData
        {
            Attribute = 19, // WCA_ACCENT_POLICY
            Data = pPolicy,
            SizeOfData = size
        };

        SetWindowCompositionAttribute(hwnd, ref data);
        Marshal.FreeHGlobal(pPolicy);
    }
}
