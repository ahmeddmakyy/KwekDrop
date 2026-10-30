using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace KwekDrop;

/// <summary>
/// A segmented control showing a row of mutually exclusive options side by side.
/// Used for aspect ratios, channels, and other small choice sets.
/// </summary>
internal sealed class SegmentedControl : Border
{
    private readonly IReadOnlyList<(string Value, string Label)> items;
    private readonly List<Button> buttons = [];
    private readonly List<Border> separators = [];
    private string? selected;

    public string? Selected
    {
        get => selected;
        set
        {
            if (selected == value)
                return;
            selected = value;
            UpdateVisuals();
        }
    }

    public event Action<string>? Chosen;

    public SegmentedControl(IReadOnlyList<(string Value, string Label)> items)
    {
        this.items = items;
        FlowDirection = FlowDirection.LeftToRight;
        CornerRadius = new CornerRadius(8);
        Padding = new Thickness(2);

        var grid = new UniformGrid { Rows = 1, Columns = items.Count };
        Child = grid;

        var buttonTemplate = CreateButtonTemplate();

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var cell = new Grid();

            var button = new Button
            {
                Template = buttonTemplate,
                Content = item.Label,
                Focusable = true,
                Cursor = Cursors.Hand,
                FontSize = 12,
                MinHeight = 26,
                Height = 26,
                Margin = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            AutomationProperties.SetName(button, item.Label);

            string val = item.Value;
            button.Click += (_, _) =>
            {
                Selected = val;
                Chosen?.Invoke(val);
            };

            button.MouseEnter += (_, _) =>
            {
                if (selected != val)
                    button.Background = GetHoverBrush();
            };

            button.MouseLeave += (_, _) =>
            {
                if (selected != val)
                    button.Background = Brushes.Transparent;
            };

            buttons.Add(button);
            cell.Children.Add(button);

            if (i < items.Count - 1)
            {
                var sep = new Border
                {
                    Width = 1,
                    Height = 14,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                    IsHitTestVisible = false
                };
                separators.Add(sep);
                cell.Children.Add(sep);
            }

            grid.Children.Add(cell);
        }

        UpdateVisuals();
        UiTheme.VisualsChanged += UpdateVisuals;
    }

    private static ControlTemplate CreateButtonTemplate()
    {
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border), "border");
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding(nameof(Button.Background))
        {
            RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
        });
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(content);
        template.VisualTree = border;
        return template;
    }

    private static Color InkColor => (UiTheme.Ink as SolidColorBrush)?.Color ?? Colors.Black;
    private static Brush GetContainerBrush() => new SolidColorBrush(Color.FromArgb((byte)(255 * 0.12), InkColor.R, InkColor.G, InkColor.B));
    private static Brush GetSeparatorBrush() => new SolidColorBrush(Color.FromArgb((byte)(255 * 0.25), InkColor.R, InkColor.G, InkColor.B));
    private static Brush GetHoverBrush() => new SolidColorBrush(Color.FromArgb((byte)(255 * 0.08), InkColor.R, InkColor.G, InkColor.B));

    private void UpdateVisuals()
    {
        Background = GetContainerBrush();

        for (int i = 0; i < items.Count; i++)
        {
            var btn = buttons[i];
            bool isSel = items[i].Value == selected;
            if (isSel)
            {
                btn.Background = UiTheme.Accent;
                btn.Foreground = Brushes.White;
                btn.FontWeight = FontWeights.Bold;
            }
            else
            {
                btn.Background = btn.IsMouseOver ? GetHoverBrush() : Brushes.Transparent;
                btn.Foreground = UiTheme.Ink;
                btn.FontWeight = FontWeights.Normal;
            }
        }

        var sepBrush = GetSeparatorBrush();
        for (int i = 0; i < separators.Count; i++)
        {
            separators[i].Background = sepBrush;
            bool hide = (selected != null && (selected == items[i].Value || selected == items[i + 1].Value));
            separators[i].Visibility = hide ? Visibility.Hidden : Visibility.Visible;
        }
    }
}
