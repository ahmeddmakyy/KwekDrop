using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace KwekDrop;

internal sealed class SettingsWindow : Window
{
    private static SettingsWindow? current;
    public static SettingsWindow Present()
    {
        if (current == null)
        {
            current = new SettingsWindow();
            current.Closed += (_, _) => current = null;
            current.Show();
        }
        if (current.WindowState == WindowState.Minimized)
            current.WindowState = WindowState.Normal;
        current.Topmost = true;
        current.Activate();
        current.Topmost = false;
        return current;
    }

    public static void SetShortcut(string intent, string newKey)
    {
        newKey = newKey.ToLowerInvariant();
        string convert = Settings.GetString(DragKeys.ConvertSetting)?.ToLowerInvariant() ?? "shift";
        string tools = Settings.GetString(DragKeys.ToolsSetting)?.ToLowerInvariant() ?? "alt";
        string actions = Settings.GetString(DragKeys.ActionsSetting)?.ToLowerInvariant() ?? "ctrl";

        if (Settings.GetString(DragKeys.ConvertSetting) == null) Settings.Set(DragKeys.ConvertSetting, convert);
        if (Settings.GetString(DragKeys.ToolsSetting) == null) Settings.Set(DragKeys.ToolsSetting, tools);
        if (Settings.GetString(DragKeys.ActionsSetting) == null) Settings.Set(DragKeys.ActionsSetting, actions);

        string oldKey = intent switch
        {
            "convert" => convert,
            "tools" => tools,
            "actions" => actions,
            _ => "none"
        };

        if (oldKey == newKey)
            return;

        if (newKey != "none")
        {
            if (intent != "convert" && convert == newKey)
                Settings.Set(DragKeys.ConvertSetting, oldKey);
            if (intent != "tools" && tools == newKey)
                Settings.Set(DragKeys.ToolsSetting, oldKey);
            if (intent != "actions" && actions == newKey)
                Settings.Set(DragKeys.ActionsSetting, oldKey);
        }

        Settings.Set(intent switch
        {
            "convert" => DragKeys.ConvertSetting,
            "tools" => DragKeys.ToolsSetting,
            "actions" => DragKeys.ActionsSetting,
            _ => intent + "Key"
        }, newKey);
    }

    public static void RestoreDefaults()
    {
        UiTheme.Choose("light");
        Settings.Set(UiTheme.BlurAmountKey, "80");
        Settings.Set(UiTheme.GlassOpacityKey, "88");
        Settings.Set(Catalog.LayoutKey, "combined");
        Settings.Set(DragKeys.ConvertSetting, "shift");
        Settings.Set(DragKeys.ToolsSetting, "alt");
        Settings.Set(DragKeys.ActionsSetting, "ctrl");
        UiTheme.Refresh();
    }

    private CheckBox? startupCheck;

    public SettingsWindow()
    {
        UiTheme.Apply(this);
        Width = 600;
        Height = 580;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Title = "KwekDrop Settings";

        if (Settings.GetString(DragKeys.ConvertSetting) == null) Settings.Set(DragKeys.ConvertSetting, "shift");
        if (Settings.GetString(DragKeys.ToolsSetting) == null) Settings.Set(DragKeys.ToolsSetting, "alt");
        if (Settings.GetString(DragKeys.ActionsSetting) == null) Settings.Set(DragKeys.ActionsSetting, "ctrl");

        Loaded += (_, _) => WindowBlur.ApplyRoundedRectBlur(this, (int)UiTheme.WindowCornerRadius, UiTheme.Dark);
        SizeChanged += (_, _) => WindowBlur.ApplyRoundedRectBlur(this, (int)UiTheme.WindowCornerRadius, UiTheme.Dark);

        L.Changed += Rebuild;
        StartupManager.Changed += RefreshStartup;
        Closed += OnClosed;

        Rebuild();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        L.Changed -= Rebuild;
        StartupManager.Changed -= RefreshStartup;
    }

    public void Rebuild()
    {
        var main = new Grid { Margin = new Thickness(24, 6, 24, 18) };
        main.RowDefinitions.Add(new RowDefinition()); // Content columns
        main.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer

        var cols = new Grid();
        cols.ColumnDefinitions.Add(new ColumnDefinition());
        cols.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
        cols.ColumnDefinitions.Add(new ColumnDefinition());

        var leftCol = BuildLeftColumn();
        Grid.SetColumn(leftCol, 0);
        cols.Children.Add(leftCol);

        var rightCol = BuildRightColumn();
        Grid.SetColumn(rightCol, 2);
        cols.Children.Add(rightCol);

        Grid.SetRow(cols, 0);
        main.Children.Add(cols);

        // Footer: Restore Defaults (leading) & Close (trailing)
        var footer = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 14, 0, 0) };
        Grid.SetRow(footer, 1);

        var restoreBtn = new Button
        {
            Content = L.T("Restore Defaults"),
            FontSize = 12,
            Padding = new Thickness(14, 6, 14, 6),
            MinHeight = 32,
            Background = Brushes.Transparent,
            Foreground = UiTheme.Ink,
            BorderBrush = UiTheme.Line,
            BorderThickness = new Thickness(1)
        };
        restoreBtn.Click += (_, _) =>
        {
            RestoreDefaults();
            Rebuild();
        };
        DockPanel.SetDock(restoreBtn, Dock.Left);
        footer.Children.Add(restoreBtn);

        var closeBtn = new Button
        {
            Content = L.T("Close"),
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Padding = new Thickness(24, 6, 24, 6),
            MinHeight = 34,
            Background = UiTheme.Accent,
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0)
        };
        closeBtn.Click += (_, _) => Close();
        DockPanel.SetDock(closeBtn, Dock.Right);
        footer.Children.Add(closeBtn);

        main.Children.Add(footer);
        Content = UiTheme.Frame(this, main, L.T("KwekDrop Settings"), toolChrome: true);
    }

    private static FrameworkElement SectionHeader(string title)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = UiTheme.Ink,
            Margin = new Thickness(0, 0, 0, 4)
        });
        panel.Children.Add(new Border
        {
            Height = 1,
            Background = UiTheme.Line,
            HorizontalAlignment = HorizontalAlignment.Stretch
        });
        return panel;
    }

    private UIElement BuildLeftColumn()
    {
        var stack = new StackPanel();

        // 1. Appearance
        stack.Children.Add(SectionHeader(L.T("Appearance")));
        var themeSeg = new SegmentedControl(new (string, string)[]
        {
            ("light", L.T("Light")),
            ("dark", L.T("Dark")),
            ("system", L.T("Auto"))
        })
        {
            Selected = UiTheme.Choice,
            Margin = new Thickness(0, 0, 0, 16)
        };
        themeSeg.Chosen += choice =>
        {
            UiTheme.Choose(choice);
            Rebuild();
        };
        stack.Children.Add(themeSeg);

        // 2. Language
        stack.Children.Add(SectionHeader(L.T("Language")));
        var langSeg = new SegmentedControl(new (string, string)[]
        {
            ("ar", "العربية"),
            ("en", "English")
        })
        {
            Selected = L.Language,
            Margin = new Thickness(0, 0, 0, 16)
        };
        langSeg.Chosen += code =>
        {
            L.Language = code;
            Rebuild();
        };
        stack.Children.Add(langSeg);

        // 3. Glass
        stack.Children.Add(SectionHeader(L.T("Glass")));

        // Blur slider
        var blurRow = new DockPanel { Margin = new Thickness(0, 0, 0, 2), LastChildFill = false };
        var blurTitle = new TextBlock { Text = L.T("Blur strength"), FontSize = 11.5, Foreground = UiTheme.Ink, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(blurTitle, Dock.Left);
        blurRow.Children.Add(blurTitle);

        var blurValStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var blurValText = new TextBlock { Text = $"{UiTheme.BlurAmount}%", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = UiTheme.Accent, VerticalAlignment = VerticalAlignment.Center };
        blurValStack.Children.Add(blurValText);
        var blurReset = new Button { Content = "↺", FontSize = 11, Padding = new Thickness(3, 0, 3, 0), Margin = new Thickness(4, 0, 0, 0), Background = Brushes.Transparent, Foreground = UiTheme.Muted, BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
        blurValStack.Children.Add(blurReset);
        DockPanel.SetDock(blurValStack, Dock.Right);
        blurRow.Children.Add(blurValStack);
        stack.Children.Add(blurRow);

        var blurSlider = new Slider { Minimum = 0, Maximum = 100, Value = UiTheme.BlurAmount, TickFrequency = 5, IsSnapToTickEnabled = true, Margin = new Thickness(0, 0, 0, 10) };
        blurSlider.ValueChanged += (_, e) =>
        {
            int val = (int)Math.Round(e.NewValue);
            blurValText.Text = $"{val}%";
            Settings.Set(UiTheme.BlurAmountKey, val.ToString());
            UiTheme.Refresh();
        };
        blurReset.Click += (_, _) =>
        {
            Settings.Set(UiTheme.BlurAmountKey, "80");
            UiTheme.Refresh();
            blurSlider.Value = 80;
            blurValText.Text = "80%";
        };
        stack.Children.Add(blurSlider);

        // Opacity slider
        var opacRow = new DockPanel { Margin = new Thickness(0, 0, 0, 2), LastChildFill = false };
        var opacTitle = new TextBlock { Text = L.T("Window opacity"), FontSize = 11.5, Foreground = UiTheme.Ink, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(opacTitle, Dock.Left);
        opacRow.Children.Add(opacTitle);

        var opacValStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var opacValText = new TextBlock { Text = $"{UiTheme.GlassOpacity}%", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = UiTheme.Accent, VerticalAlignment = VerticalAlignment.Center };
        opacValStack.Children.Add(opacValText);
        var opacReset = new Button { Content = "↺", FontSize = 11, Padding = new Thickness(3, 0, 3, 0), Margin = new Thickness(4, 0, 0, 0), Background = Brushes.Transparent, Foreground = UiTheme.Muted, BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
        opacValStack.Children.Add(opacReset);
        DockPanel.SetDock(opacValStack, Dock.Right);
        opacRow.Children.Add(opacValStack);
        stack.Children.Add(opacRow);

        var opacSlider = new Slider { Minimum = 0, Maximum = 100, Value = UiTheme.GlassOpacity, TickFrequency = 5, IsSnapToTickEnabled = true, Margin = new Thickness(0, 0, 0, 16) };
        opacSlider.ValueChanged += (_, e) =>
        {
            int val = (int)Math.Round(e.NewValue);
            opacValText.Text = $"{val}%";
            Settings.Set(UiTheme.GlassOpacityKey, val.ToString());
            UiTheme.Refresh();
        };
        opacReset.Click += (_, _) =>
        {
            Settings.Set(UiTheme.GlassOpacityKey, "88");
            UiTheme.Refresh();
            opacSlider.Value = 88;
            opacValText.Text = "88%";
        };
        stack.Children.Add(opacSlider);

        // 4. General
        stack.Children.Add(SectionHeader(L.T("General")));
        startupCheck = new CheckBox
        {
            Content = L.T("Start KwekDrop when Windows starts"),
            FontSize = 11.5,
            Foreground = UiTheme.Ink,
            Margin = new Thickness(0, 2, 0, 0)
        };
        RefreshStartup();
        startupCheck.Click += async (_, _) =>
        {
            startupCheck.IsEnabled = false;
            var result = await StartupManager.SetAsync(startupCheck.IsChecked == true);
            startupCheck.IsChecked = result.Enabled;
            startupCheck.IsEnabled = result.CanChange;
        };
        stack.Children.Add(startupCheck);

        return stack;
    }

    private async void RefreshStartup()
    {
        if (startupCheck != null)
        {
            var status = await StartupManager.GetAsync();
            startupCheck.IsChecked = status.Enabled;
            startupCheck.IsEnabled = status.CanChange;
        }
    }

    private UIElement BuildRightColumn()
    {
        var stack = new StackPanel();

        // 1. Shortcuts while dragging
        stack.Children.Add(SectionHeader(L.T("Shortcuts while dragging")));

        FrameworkElement ShortcutRow(string label, string intent)
        {
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            var lbl = new TextBlock
            {
                Text = label,
                FontSize = 11.5,
                Foreground = UiTheme.Ink,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(lbl, Dock.Left);
            row.Children.Add(lbl);

            string currentKey = intent switch
            {
                "convert" => Settings.GetString(DragKeys.ConvertSetting)?.ToLowerInvariant() ?? "shift",
                "tools" => Settings.GetString(DragKeys.ToolsSetting)?.ToLowerInvariant() ?? "alt",
                "actions" => Settings.GetString(DragKeys.ActionsSetting)?.ToLowerInvariant() ?? "ctrl",
                _ => "none"
            };

            var seg = new SegmentedControl(new (string, string)[]
            {
                ("shift", L.T("Shift")),
                ("ctrl", L.T("Ctrl")),
                ("alt", L.T("Alt")),
                ("none", L.T("None"))
            })
            {
                Selected = currentKey,
                MinWidth = 150,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            seg.Chosen += key =>
            {
                SetShortcut(intent, key);
                Rebuild();
            };
            DockPanel.SetDock(seg, Dock.Right);
            row.Children.Add(seg);
            return row;
        }

        stack.Children.Add(ShortcutRow(L.T("Convert wheel"), "convert"));
        stack.Children.Add(ShortcutRow(L.T("Tools wheel"), "tools"));
        stack.Children.Add(ShortcutRow(L.T("Actions wheel"), "actions"));

        stack.Children.Add(new TextBlock
        {
            Text = L.T("Drag files anywhere, then press the key to open its wheel right away."),
            FontSize = 10.5,
            Foreground = UiTheme.Muted,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 16),
            LineHeight = 15
        });

        // 2. Wheel layout
        stack.Children.Add(SectionHeader(L.T("Wheel")));
        bool twoWheels = Catalog.TwoWheels;
        var wheelSeg = new SegmentedControl(new (string, string)[]
        {
            ("combined", L.T("Combine related tools")),
            ("twoWheels", L.T("Two wheels (categories)"))
        })
        {
            Selected = twoWheels ? "twoWheels" : "combined",
            Margin = new Thickness(0, 0, 0, 6)
        };
        wheelSeg.Chosen += layout =>
        {
            Settings.Set(Catalog.LayoutKey, layout);
            Rebuild();
        };
        stack.Children.Add(wheelSeg);

        string wheelCaption = twoWheels
            ? L.T("Pick a category first, then choose tool on a second outer wheel.")
            : L.T("Related tools share one window, so one wheel shows everything.");
        stack.Children.Add(new TextBlock
        {
            Text = wheelCaption,
            FontSize = 10.5,
            Foreground = UiTheme.Muted,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16),
            LineHeight = 15
        });

        // 3. Guide
        stack.Children.Add(SectionHeader(L.T("Guide")));
        var openGuideBtn = new Button
        {
            Content = L.T("Open the guide"),
            FontSize = 12,
            Padding = new Thickness(14, 6, 14, 6),
            MinHeight = 32,
            Background = Brushes.Transparent,
            Foreground = UiTheme.Ink,
            BorderBrush = UiTheme.Line,
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        openGuideBtn.Click += (_, _) => SetupWindow.Present();
        stack.Children.Add(openGuideBtn);

        return stack;
    }
}
