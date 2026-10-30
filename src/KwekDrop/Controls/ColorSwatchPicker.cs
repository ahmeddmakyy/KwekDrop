using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace KwekDrop;

/// <summary>
/// A compact, elegant in-app color picker with a swatch button, popup hue/sat/val sliders, preset palette and hex box.
/// Avoids WinForms ColorDialog and matches UiTheme seamlessly.
/// </summary>
internal sealed class ColorSwatchPicker : StackPanel
{
    private readonly Border swatch;
    private readonly TextBox hexBox;
    private readonly Popup popup;
    private readonly Slider hueSlider;
    private readonly Slider satSlider;
    private readonly Slider valSlider;
    private bool updating;
    private Color currentColor;

    public string HexColor
    {
        get => $"#{currentColor.R:X2}{currentColor.G:X2}{currentColor.B:X2}";
        set
        {
            if (TryParseHex(value, out var col))
            {
                SetColor(col, true);
            }
        }
    }

    public event Action<string>? ColorChanged;

    public ColorSwatchPicker(string initialHex = "#FF6500", bool showHexBox = true)
    {
        Orientation = Orientation.Horizontal;
        FlowDirection = FlowDirection.LeftToRight;
        VerticalAlignment = VerticalAlignment.Center;

        TryParseHex(initialHex, out currentColor);

        swatch = new Border
        {
            Width = 26,
            Height = 24,
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 128, 128, 128)),
            Background = new SolidColorBrush(currentColor),
            Cursor = Cursors.Hand,
            ToolTip = L.T("Pick color")
        };

        popup = new Popup
        {
            StaysOpen = false,
            AllowsTransparency = true,
            PlacementTarget = swatch,
            Placement = PlacementMode.Bottom,
            PopupAnimation = PopupAnimation.Fade
        };

        // Popup Content Container
        var popupCard = new Border
        {
            Background = UiTheme.Surface,
            BorderBrush = UiTheme.Line,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12),
            Width = 220,
            Margin = new Thickness(0, 4, 0, 0),
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 12, Opacity = 0.25, ShadowDepth = 2 }
        };

        var pnl = new StackPanel { FlowDirection = FlowDirection.LeftToRight };

        // Preset swatches row
        var presetRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
        string[] presets = ["#FF6500", "#FFFFFF", "#22D3EE", "#8B5CF6", "#22C55E", "#EC4899", "#EAB308", "#EF4444", "#3B82F6", "#111827"];
        foreach (string p in presets)
        {
            if (TryParseHex(p, out var pc))
            {
                var dot = new Border
                {
                    Width = 16,
                    Height = 16,
                    CornerRadius = new CornerRadius(8),
                    Background = new SolidColorBrush(pc),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x30, 0, 0, 0)),
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(2),
                    Cursor = Cursors.Hand
                };
                dot.MouseDown += (_, _) =>
                {
                    SetColor(pc, false);
                    UpdateSlidersFromColor();
                    NotifyChanged();
                };
                presetRow.Children.Add(dot);
            }
        }
        pnl.Children.Add(presetRow);

        // Hue Slider
        pnl.Children.Add(new TextBlock { Text = L.T("Hue"), FontSize = 10, Foreground = UiTheme.Muted, Margin = new Thickness(0, 0, 0, 2) });
        hueSlider = new Slider
        {
            Minimum = 0,
            Maximum = 360,
            Value = 24,
            SmallChange = 1,
            LargeChange = 10,
            Margin = new Thickness(0, 0, 0, 6)
        };
        var hueGradient = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5) };
        hueGradient.GradientStops.Add(new GradientStop(Color.FromRgb(255, 0, 0), 0.0));
        hueGradient.GradientStops.Add(new GradientStop(Color.FromRgb(255, 255, 0), 0.17));
        hueGradient.GradientStops.Add(new GradientStop(Color.FromRgb(0, 255, 0), 0.33));
        hueGradient.GradientStops.Add(new GradientStop(Color.FromRgb(0, 255, 255), 0.50));
        hueGradient.GradientStops.Add(new GradientStop(Color.FromRgb(0, 0, 255), 0.67));
        hueGradient.GradientStops.Add(new GradientStop(Color.FromRgb(255, 0, 255), 0.83));
        hueGradient.GradientStops.Add(new GradientStop(Color.FromRgb(255, 0, 0), 1.0));
        hueSlider.Background = hueGradient;
        pnl.Children.Add(hueSlider);

        // Saturation Slider
        pnl.Children.Add(new TextBlock { Text = L.T("Saturation"), FontSize = 10, Foreground = UiTheme.Muted, Margin = new Thickness(0, 0, 0, 2) });
        satSlider = new Slider { Minimum = 0, Maximum = 100, Value = 100, SmallChange = 1, LargeChange = 10, Margin = new Thickness(0, 0, 0, 6) };
        pnl.Children.Add(satSlider);

        // Brightness Slider
        pnl.Children.Add(new TextBlock { Text = L.T("Brightness"), FontSize = 10, Foreground = UiTheme.Muted, Margin = new Thickness(0, 0, 0, 2) });
        valSlider = new Slider { Minimum = 0, Maximum = 100, Value = 100, SmallChange = 1, LargeChange = 10, Margin = new Thickness(0, 0, 0, 4) };
        pnl.Children.Add(valSlider);

        popupCard.Child = pnl;
        popup.Child = popupCard;

        void OnSliderChanged(object? sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (updating) return;
            var c = HsvToRgb(hueSlider.Value, satSlider.Value / 100.0, valSlider.Value / 100.0);
            SetColor(c, false);
            NotifyChanged();
        }

        hueSlider.ValueChanged += OnSliderChanged;
        satSlider.ValueChanged += OnSliderChanged;
        valSlider.ValueChanged += OnSliderChanged;

        swatch.MouseDown += (_, e) =>
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                popup.IsOpen = !popup.IsOpen;
                if (popup.IsOpen) UpdateSlidersFromColor();
            }
        };

        Children.Add(swatch);

        hexBox = new TextBox
        {
            Text = HexColor,
            Width = 68,
            Height = 24,
            Margin = new Thickness(6, 0, 0, 0),
            FontSize = 11,
            VerticalContentAlignment = VerticalAlignment.Center,
            FontFamily = new FontFamily("Cascadia Mono, Consolas, monospace"),
            Visibility = showHexBox ? Visibility.Visible : Visibility.Collapsed
        };

        hexBox.TextChanged += (_, _) =>
        {
            if (updating) return;
            if (TryParseHex(hexBox.Text, out var col))
            {
                SetColor(col, false);
                UpdateSlidersFromColor();
                NotifyChanged();
            }
        };

        Children.Add(hexBox);
        UpdateSlidersFromColor();
    }

    private void SetColor(Color col, bool updateHex)
    {
        currentColor = col;
        swatch.Background = new SolidColorBrush(col);
        if (updateHex)
        {
            updating = true;
            hexBox.Text = HexColor;
            updating = false;
        }
    }

    private void NotifyChanged()
    {
        updating = true;
        hexBox.Text = HexColor;
        updating = false;
        ColorChanged?.Invoke(HexColor);
    }

    private void UpdateSlidersFromColor()
    {
        updating = true;
        RgbToHsv(currentColor, out double h, out double s, out double v);
        hueSlider.Value = h;
        satSlider.Value = s * 100.0;
        valSlider.Value = v * 100.0;
        updating = false;
    }

    private static bool TryParseHex(string? hex, out Color color)
    {
        color = Colors.Black;
        if (string.IsNullOrWhiteSpace(hex)) return false;
        string s = hex.Trim().TrimStart('#');
        if (s.Length == 6 && uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint val))
        {
            byte r = (byte)((val >> 16) & 0xFF);
            byte g = (byte)((val >> 8) & 0xFF);
            byte b = (byte)(val & 0xFF);
            color = Color.FromRgb(r, g, b);
            return true;
        }
        return false;
    }

    private static Color HsvToRgb(double h, double s, double v)
    {
        h = (h % 360 + 360) % 360;
        double c = v * s;
        double x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
        double m = v - c;
        double r1 = 0, g1 = 0, b1 = 0;
        if (h < 60) { r1 = c; g1 = x; }
        else if (h < 120) { r1 = x; g1 = c; }
        else if (h < 180) { g1 = c; b1 = x; }
        else if (h < 240) { g1 = x; b1 = c; }
        else if (h < 300) { r1 = x; b1 = c; }
        else { r1 = c; b1 = x; }
        return Color.FromRgb((byte)Math.Round((r1 + m) * 255), (byte)Math.Round((g1 + m) * 255), (byte)Math.Round((b1 + m) * 255));
    }

    private static void RgbToHsv(Color col, out double h, out double s, out double v)
    {
        double r = col.R / 255.0, g = col.G / 255.0, b = col.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;
        v = max;
        s = max == 0 ? 0 : delta / max;
        if (delta == 0) h = 0;
        else if (max == r) h = 60 * (((g - b) / delta) % 6);
        else if (max == g) h = 60 * (((b - r) / delta) + 2);
        else h = 60 * (((r - g) / delta) + 4);
        if (h < 0) h += 360;
    }
}
