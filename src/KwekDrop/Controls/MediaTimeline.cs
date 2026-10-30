using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace KwekDrop;

/// <summary>A text field holding a time such as 0:12.50; typing a time and leaving the field sets it.</summary>
public sealed class TimeField : TextBox
{
    private Action<double>? _onChanged;

    public TimeField(double value = 0, Action<double>? onChanged = null)
    {
        _onChanged = onChanged;
        Width = 78;
        Height = 24;
        VerticalContentAlignment = VerticalAlignment.Center;
        FontFamily = new FontFamily("Cascadia Mono, Consolas");
        FontSize = 12;
        Text = FormatTime(value);
        FlowDirection = FlowDirection.LeftToRight;
        LostFocus += (_, _) => Commit();
        KeyDown += (_, e) => { if (e.Key == Key.Enter) Commit(); };
    }

    public void SetValue(double value)
    {
        if (!IsKeyboardFocused)
            Text = FormatTime(value);
    }

    public void Commit()
    {
        if (ParseTime(Text) is { } seconds)
            _onChanged?.Invoke(seconds);
    }

    public static string FormatTime(double seconds)
    {
        seconds = Math.Max(0, seconds);
        int minutes = (int)seconds / 60;
        double rem = seconds - minutes * 60;
        return $"{minutes}:{rem:00.00}".Replace(',', '.');
    }

    public static double? ParseTime(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        double total = 0;
        foreach (var part in text.Trim().Split(':'))
        {
            if (!double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return null;
            total = total * 60 + value;
        }
        return total;
    }
}

/// <summary>Thumbnails or a waveform across the whole file, with range handles and playhead.</summary>
internal sealed class MediaTimeline : FrameworkElement
{
    private double duration = 1, position, start, end = 1;
    private int dragging; // 0: none, 1: start handle, 2: end handle, 3: seek
    private bool customDragging;

    public List<BitmapSource> Thumbnails { get; } = [];
    public float[]? WaveformBars { get; set; }
    public Brush WaveBrush { get; set; } = new SolidColorBrush(Color.FromArgb(0xD9, UiTheme.AccentColor.R, UiTheme.AccentColor.G, UiTheme.AccentColor.B));
    public bool ShowPlayhead { get; set; } = true;
    public Action<DrawingContext, double, double>? Overlay { get; set; }
    public Func<double, Point, bool>? Pressed { get; set; }
    public Action<double>? Dragged { get; set; }
    public Action? Released { get; set; }

    public event Action<double>? SeekRequested;
    public event Action<double, double>? RangeChanged;
    public bool RangeEnabled { get; set; }
    public bool SelectionEnabled { get; set; } = true;
    public ImageSource? Waveform { get; set; }

    public double Duration => duration;
    public double Position => position;
    public double StartTime => start;
    public double EndTime => end;

    public MediaTimeline()
    {
        Height = 56;
        Cursor = Cursors.Hand;
        Focusable = true;
        FlowDirection = FlowDirection.LeftToRight;
        ToolTip = L.T("点击定位；拖动橙色节点设置开始和结束");
        System.Windows.Automation.AutomationProperties.SetName(this, L.T("播放进度与裁剪范围"));
    }

    public void SetPosition(double value, double total)
    {
        duration = Math.Max(.001, total);
        position = Math.Clamp(value, 0, duration);
        InvalidateVisual();
    }

    public void SetRange(double first, double last)
    {
        start = Math.Clamp(first, 0, duration);
        end = Math.Clamp(last, 0, duration);
        InvalidateVisual();
    }

    public double XAt(double time) => time / Math.Max(duration, 0.001) * ActualWidth;
    public double TimeAt(double x) => Math.Clamp(x / Math.Max(1, ActualWidth), 0, 1) * duration;

    // Backward-compatibility aliases
    private double X(double value) => XAt(value);
    private double Time(double x) => TimeAt(x);

    public async Task LoadThumbnails(string file, double dur, CancellationToken cancellation = default)
    {
        Thumbnails.Clear();
        int count = 10;
        duration = Math.Max(0.001, dur);
        for (int i = 0; i < count; i++)
        {
            if (cancellation.IsCancellationRequested) break;
            double t = duration * (i + 0.5) / count;
            byte[] bytes = await Backend.FfmpegCapture(["-ss", t.ToString("0.000", CultureInfo.InvariantCulture), "-i", file, "-frames:v", "1", "-vf", "scale=-2:90", "-f", "image2pipe", "-vcodec", "png", "-"], cancellation).ConfigureAwait(true);
            if (bytes.Length > 0 && !cancellation.IsCancellationRequested)
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.StreamSource = new MemoryStream(bytes);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    Thumbnails.Add(bmp);
                    InvalidateVisual();
                }
                catch { }
            }
        }
    }

    public async Task LoadWaveform(string file, int bars = 200, CancellationToken cancellation = default, int? channel = null)
    {
        string[] mix = channel is { } c ? ["-af", $"pan=mono|c0=c{c}"] : ["-ac", "1"];
        byte[] data = await Backend.FfmpegCapture(["-i", file, "-vn", .. mix, "-ar", "4000", "-f", "f32le", "-"], cancellation).ConfigureAwait(true);
        if (data.Length < 4 || cancellation.IsCancellationRequested) return;
        var samples = new float[data.Length / 4];
        Buffer.BlockCopy(data, 0, samples, 0, samples.Length * 4);
        if (samples.Length == 0 || bars <= 0) return;
        var chunk = Math.Max(1, samples.Length / bars);
        var peaks = new List<float>();
        for (var index = 0; index < samples.Length && peaks.Count < bars; index += chunk)
        {
            var peak = 0f;
            for (var i = index; i < Math.Min(index + chunk, samples.Length); i++) peak = Math.Max(peak, Math.Abs(samples[i]));
            peaks.Add(peak);
        }
        var top = peaks.Count > 0 ? peaks.Max() : 0f;
        WaveformBars = top > 0 ? peaks.Select(p => p / top).ToArray() : [.. peaks];
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double w = ActualWidth;
        double h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        var clip = new RectangleGeometry(new Rect(0, 0, w, h), 8, 8);
        dc.PushClip(clip);

        if (Thumbnails.Count > 0)
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(0xCC, 0, 0, 0)), null, new Rect(0, 0, w, h));
            double slot = w / 10.0;
            for (int i = 0; i < Thumbnails.Count; i++)
            {
                var img = Thumbnails[i];
                if (img.PixelWidth > 0 && img.PixelHeight > 0)
                {
                    double scale = Math.Max(slot / img.PixelWidth, h / img.PixelHeight);
                    var size = new Size(img.PixelWidth * scale, img.PixelHeight * scale);
                    dc.PushClip(new RectangleGeometry(new Rect(i * slot, 0, slot, h)));
                    dc.DrawImage(img, new Rect(i * slot + (slot - size.Width) / 2, (h - size.Height) / 2, size.Width, size.Height));
                    dc.Pop();
                }
            }
        }
        else if (WaveformBars != null && WaveformBars.Length > 0)
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(0x14, UiTheme.AccentColor.R, UiTheme.AccentColor.G, UiTheme.AccentColor.B)), null, new Rect(0, 0, w, h));
            DrawWave(dc, WaveformBars, new Rect(0, 0, w, h), WaveBrush);
        }
        else if (Waveform != null)
        {
            dc.DrawImage(Waveform, new Rect(0, 0, w, h));
        }
        else
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(0x14, 0, 0, 0)), null, new Rect(0, 0, w, h));
        }

        // Range overlays: dimmed cuts outside [x1, x2], solid 3px border, 12px handles with grip capsules
        if (RangeEnabled && end >= start)
        {
            double x1 = XAt(start), x2 = XAt(end);
            var scrim = new SolidColorBrush(Color.FromArgb(0xA6, 0xFF, 0xFF, 0xFF));
            if (x1 > 0)
                dc.DrawRectangle(scrim, null, new Rect(0, 0, Math.Min(x1, w), h));
            if (x2 < w)
                dc.DrawRectangle(scrim, null, new Rect(Math.Max(0, x2), 0, Math.Max(0, w - x2), h));

            double rangeW = Math.Max(20, x2 - x1);
            var borderPen = new Pen(UiTheme.Accent, 3);
            dc.DrawRoundedRectangle(null, borderPen, new Rect(x1, 0, rangeW, h), 7, 7);

            // Left handle (start)
            dc.DrawRoundedRectangle(UiTheme.Accent, null, new Rect(x1, 0, 12, h), 4, 4);
            dc.DrawRoundedRectangle(Brushes.White, null, new Rect(x1 + (12 - 2.5) / 2, (h - 18) / 2, 2.5, 18), 1.25, 1.25);

            // Right handle (end)
            dc.DrawRoundedRectangle(UiTheme.Accent, null, new Rect(Math.Max(0, x2 - 12), 0, 12, h), 4, 4);
            dc.DrawRoundedRectangle(Brushes.White, null, new Rect(Math.Max(0, x2 - 12) + (12 - 2.5) / 2, (h - 18) / 2, 2.5, 18), 1.25, 1.25);
        }

        dc.Pop(); // pop clip

        Overlay?.Invoke(dc, w, h);

        if (ShowPlayhead)
        {
            double px = XAt(position);
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(0xD9, 0x28, 0x2B, 0x29)), null, new Rect(px - 0.75, 0, 1.5, h));
        }
    }

    public static void DrawWave(DrawingContext dc, float[] levels, Rect rect, Brush brush)
    {
        if (levels.Length == 0 || rect.Width <= 0 || rect.Height <= 0) return;
        double step = rect.Width / levels.Length;
        for (int i = 0; i < levels.Length; i++)
        {
            double bar = Math.Max(1.5, levels[i] * rect.Height * 0.85);
            dc.DrawRectangle(brush, null, new Rect(rect.X + i * step, rect.Y + (rect.Height - bar) / 2, Math.Max(1, step * 0.55), bar));
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        var p = e.GetPosition(this);
        double t = TimeAt(p.X);

        if (Pressed != null && Pressed(t, p))
        {
            customDragging = true;
            CaptureMouse();
            e.Handled = true;
            return;
        }

        if (RangeEnabled && SelectionEnabled)
        {
            double x1 = XAt(start), x2 = XAt(end);
            if (Math.Abs(p.X - x1) < 14) dragging = 1;
            else if (Math.Abs(p.X - x2) < 14) dragging = 2;
            else dragging = 3;
        }
        else
        {
            dragging = 3;
        }

        CaptureMouse();
        Apply(p.X);
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var p = e.GetPosition(this);
        double t = TimeAt(p.X);

        if (customDragging)
        {
            Dragged?.Invoke(t);
            InvalidateVisual();
            return;
        }

        if (dragging != 0)
        {
            Apply(p.X);
        }
        else
        {
            if (RangeEnabled)
            {
                double x1 = XAt(start), x2 = XAt(end);
                if (Math.Abs(p.X - x1) < 14) ToolTip = L.T("拖动开始节点");
                else if (Math.Abs(p.X - x2) < 14) ToolTip = L.T("拖动结束节点");
                else ToolTip = MediaSegment.Format(t);
            }
            else
            {
                ToolTip = MediaSegment.Format(t);
            }
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (customDragging)
        {
            customDragging = false;
            Released?.Invoke();
        }
        dragging = 0;
        ReleaseMouseCapture();
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        if (customDragging)
        {
            customDragging = false;
            Released?.Invoke();
        }
        dragging = 0;
    }

    private void Apply(double x)
    {
        double val = TimeAt(x);
        if (dragging == 1)
        {
            start = Math.Clamp(val, 0, Math.Max(0, end - 0.1));
            position = start;
            SeekRequested?.Invoke(start);
            RangeChanged?.Invoke(start, end);
        }
        else if (dragging == 2)
        {
            end = Math.Clamp(val, Math.Min(duration, start + 0.1), duration);
            position = end;
            SeekRequested?.Invoke(end);
            RangeChanged?.Invoke(start, end);
        }
        else
        {
            position = val;
            SeekRequested?.Invoke(val);
        }
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key is Key.Left or Key.Right)
        {
            double step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? .1 : 1;
            position = Math.Clamp(position + (e.Key == Key.Left ? -step : step), 0, duration);
            SeekRequested?.Invoke(position);
            InvalidateVisual();
            e.Handled = true;
        }
    }
}