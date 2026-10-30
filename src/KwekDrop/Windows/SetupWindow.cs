using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace KwekDrop;

internal sealed class SetupWindow : Window
{
    private static SetupWindow? current;
    public static SetupWindow Present()
    {
        if (current == null)
        {
            current = new SetupWindow();
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

    private int currentPage = 1;
    private readonly Border pageHost = new();
    private readonly Button backButton = new();
    private readonly Button nextButton = new();
    private readonly StackPanel dotsPanel = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    private readonly Ellipse[] dots = new Ellipse[4];
    private DispatcherTimer? keyCapTimer;
    private Border? convertCap, toolsCap, actionsCap;
    private TextBlock? convertText, toolsText, actionsText;
    private TextBlock? convertSub, toolsSub, actionsSub;
    private string? practiceFile;
    private Point dragStartPoint;
    private CheckBox? startupCheck;

    public SetupWindow()
    {
        UiTheme.Apply(this);
        Width = 580;
        Height = 560;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Title = "KwekDrop";

        Loaded += (_, _) => WindowBlur.ApplyRoundedRectBlur(this, (int)UiTheme.WindowCornerRadius, UiTheme.Dark);
        SizeChanged += (_, _) => WindowBlur.ApplyRoundedRectBlur(this, (int)UiTheme.WindowCornerRadius, UiTheme.Dark);

        L.Changed += Rebuild;
        StartupManager.Changed += RefreshStartup;
        Closed += OnClosed;

        BuildChrome();
        ShowPage(1);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        Settings.Set("setupDone", "yes");
        L.Changed -= Rebuild;
        StartupManager.Changed -= RefreshStartup;
        keyCapTimer?.Stop();
        if (!string.IsNullOrEmpty(practiceFile) && File.Exists(practiceFile))
        {
            try { File.Delete(practiceFile); } catch { }
        }
    }

    private void Rebuild()
    {
        BuildChrome();
        ShowPage(currentPage);
    }

    private void BuildChrome()
    {
        (pageHost.Parent as Panel)?.Children.Remove(pageHost);
        (backButton.Parent as Panel)?.Children.Remove(backButton);
        (dotsPanel.Parent as Panel)?.Children.Remove(dotsPanel);
        (nextButton.Parent as Panel)?.Children.Remove(nextButton);

        var main = new Grid { Margin = new Thickness(24, 8, 24, 20) };
        main.RowDefinitions.Add(new RowDefinition()); // Page content
        main.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer

        Grid.SetRow(pageHost, 0);
        main.Children.Add(pageHost);

        var footer = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 16, 0, 0) };
        Grid.SetRow(footer, 1);

        var leftGroup = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(leftGroup, Dock.Left);

        backButton.Content = L.T("Back");
        backButton.FontSize = 12;
        backButton.Padding = new Thickness(16, 6, 16, 6);
        backButton.MinHeight = 32;
        backButton.Background = Brushes.Transparent;
        backButton.Foreground = UiTheme.Ink;
        backButton.BorderBrush = UiTheme.Line;
        backButton.BorderThickness = new Thickness(1);
        backButton.Margin = new Thickness(0, 0, 16, 0);
        backButton.Click -= OnBackClick;
        backButton.Click += OnBackClick;
        leftGroup.Children.Add(backButton);

        dotsPanel.Children.Clear();
        for (int i = 0; i < 4; i++)
        {
            dots[i] = new Ellipse
            {
                Width = 7,
                Height = 7,
                Fill = i == (currentPage - 1) ? UiTheme.Accent : UiTheme.Line,
                Margin = new Thickness(i == 0 ? 0 : 6, 0, 0, 0)
            };
            dotsPanel.Children.Add(dots[i]);
        }
        leftGroup.Children.Add(dotsPanel);
        footer.Children.Add(leftGroup);

        nextButton.FontSize = 13;
        nextButton.FontWeight = FontWeights.Bold;
        nextButton.Padding = new Thickness(22, 8, 22, 8);
        nextButton.MinHeight = 36;
        nextButton.Background = UiTheme.Accent;
        nextButton.Foreground = Brushes.White;
        nextButton.BorderThickness = new Thickness(0);
        nextButton.Click -= OnNextClick;
        nextButton.Click += OnNextClick;
        DockPanel.SetDock(nextButton, Dock.Right);
        footer.Children.Add(nextButton);

        main.Children.Add(footer);
        Content = UiTheme.Frame(this, main, null, toolChrome: true);
    }

    private void OnBackClick(object? sender, RoutedEventArgs e)
    {
        if (currentPage > 1)
            ShowPage(currentPage - 1);
    }

    private void OnNextClick(object? sender, RoutedEventArgs e)
    {
        if (currentPage < 4)
            ShowPage(currentPage + 1);
        else
            Close();
    }

    public void GoToPage(int page) => ShowPage(Math.Clamp(page, 1, 4));

    private void ShowPage(int page)
    {
        currentPage = page;
        backButton.Visibility = page == 1 ? Visibility.Hidden : Visibility.Visible;
        nextButton.Content = page == 4 ? L.T("Get Started") : L.T("Continue");

        for (int i = 0; i < 4; i++)
            dots[i].Fill = i == (page - 1) ? UiTheme.Accent : UiTheme.Line;

        if (page != 2)
            keyCapTimer?.Stop();

        pageHost.Child = page switch
        {
            1 => BuildPage1(),
            2 => BuildPage2(),
            3 => BuildPage3(),
            4 => BuildPage4(),
            _ => new Grid()
        };
    }

    // Page 1: Welcome
    private UIElement BuildPage1()
    {
        var stack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var appImg = new Image
        {
            Source = AppIcon.Window,
            Stretch = Stretch.Uniform,
            Width = 96,
            Height = 96,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 24)
        };
        RenderOptions.SetBitmapScalingMode(appImg, BitmapScalingMode.HighQuality);
        stack.Children.Add(appImg);

        stack.Children.Add(new TextBlock
        {
            Text = L.T("Welcome to KwekDrop"),
            FontSize = 26,
            FontWeight = FontWeights.Bold,
            Foreground = UiTheme.Ink,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 14)
        });

        stack.Children.Add(new TextBlock
        {
            Text = L.T("Convert, compress and edit any file while you drag it. Everything stays on your PC."),
            FontSize = 13.5,
            Foreground = UiTheme.Muted,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            MaxWidth = 420,
            HorizontalAlignment = HorizontalAlignment.Center,
            LineHeight = 20
        });

        return stack;
    }

    // Page 2: Try it now
    private UIElement BuildPage2()
    {
        var main = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 12, 0)
        };

        var row = new Grid { Margin = new Thickness(0, 0, 0, 24) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.6, GridUnitType.Star) });

        // Step 1: Drag photo
        var col1 = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        col1.Children.Add(new TextBlock
        {
            Text = L.T("Drag the photo."),
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = UiTheme.Ink,
            Margin = new Thickness(0, 0, 0, 12),
            HorizontalAlignment = HorizontalAlignment.Center
        });

        EnsurePracticeImage();
        var thumbBorder = new Border
        {
            Width = 92,
            Height = 92,
            CornerRadius = new CornerRadius(14),
            Background = UiTheme.Surface,
            BorderBrush = UiTheme.Line,
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        if (File.Exists(practiceFile))
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(practiceFile);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            thumbBorder.Child = new Image { Source = bmp, Stretch = Stretch.UniformToFill };
        }
        thumbBorder.PreviewMouseLeftButtonDown += (s, e) => dragStartPoint = e.GetPosition(null);
        thumbBorder.PreviewMouseMove += (s, e) =>
        {
            if (e.LeftButton == MouseButtonState.Pressed && File.Exists(practiceFile))
            {
                var diff = dragStartPoint - e.GetPosition(null);
                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance || Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    var data = new DataObject(DataFormats.FileDrop, new string[] { practiceFile });
                    DragDrop.DoDragDrop(thumbBorder, data, DragDropEffects.Copy);
                }
            }
        };
        col1.Children.Add(thumbBorder);
        Grid.SetColumn(col1, 0);
        row.Children.Add(col1);

        // Step 2: Keys
        var col2 = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        col2.Children.Add(new TextBlock
        {
            Text = L.T("While dragging, press a key."),
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = UiTheme.Ink,
            Margin = new Thickness(0, 0, 0, 12),
            HorizontalAlignment = HorizontalAlignment.Center
        });

        var keysRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };

        Border MakeKeyCap(string label, string keyName, out TextBlock lbl, out TextBlock sub)
        {
            var b = new Border
            {
                Width = 70,
                Height = 60,
                CornerRadius = new CornerRadius(10),
                Background = UiTheme.Surface,
                BorderBrush = UiTheme.Line,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(4, 0, 4, 0)
            };
            var st = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            lbl = new TextBlock { Text = label, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = UiTheme.Ink, HorizontalAlignment = HorizontalAlignment.Center };
            sub = new TextBlock { Text = $"({DragKeys.DisplayName(keyName)})", FontSize = 10.5, Foreground = UiTheme.Muted, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 0) };
            st.Children.Add(lbl);
            st.Children.Add(sub);
            b.Child = st;
            return b;
        }

        string convKey = DragKeys.For(DragIntent.Convert);
        string toolKey = DragKeys.For(DragIntent.Tools);
        string actKey = DragKeys.For(DragIntent.Actions);

        convertCap = MakeKeyCap(L.T("Convert"), convKey, out convertText, out convertSub);
        toolsCap = MakeKeyCap(L.T("Tools"), toolKey, out toolsText, out toolsSub);
        actionsCap = MakeKeyCap(L.T("Actions"), actKey, out actionsText, out actionsSub);

        keysRow.Children.Add(convertCap);
        keysRow.Children.Add(toolsCap);
        keysRow.Children.Add(actionsCap);
        col2.Children.Add(keysRow);

        Grid.SetColumn(col2, 2);
        row.Children.Add(col2);
        main.Children.Add(row);

        // Hint Card
        var hintCard = new Border
        {
            CornerRadius = new CornerRadius(14),
            Background = UiTheme.Surface,
            BorderBrush = UiTheme.Line,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(16, 12, 16, 12),
            HorizontalAlignment = HorizontalAlignment.Center,
            MaxWidth = 440
        };
        hintCard.Child = new TextBlock
        {
            Text = L.T("The wheel stays open until you drop."),
            FontSize = 12.5,
            Foreground = UiTheme.Ink,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        main.Children.Add(hintCard);

        StartKeyCapTimer();
        return main;
    }

    private void StartKeyCapTimer()
    {
        keyCapTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        keyCapTimer.Tick -= OnKeyCapTick;
        keyCapTimer.Tick += OnKeyCapTick;
        keyCapTimer.Start();
    }

    private void OnKeyCapTick(object? sender, EventArgs e)
    {
        void UpdateCap(Border? cap, TextBlock? lbl, TextBlock? sub, string keyName)
        {
            if (cap == null || lbl == null || sub == null) return;
            bool down = DragKeys.IsKeyDown(keyName);
            cap.Background = down ? UiTheme.Accent : UiTheme.Surface;
            cap.BorderBrush = down ? UiTheme.Accent : UiTheme.Line;
            lbl.Foreground = down ? Brushes.White : UiTheme.Ink;
            sub.Foreground = down ? Brushes.White : UiTheme.Muted;
        }

        UpdateCap(convertCap, convertText, convertSub, DragKeys.For(DragIntent.Convert));
        UpdateCap(toolsCap, toolsText, toolsSub, DragKeys.For(DragIntent.Tools));
        UpdateCap(actionsCap, actionsText, actionsSub, DragKeys.For(DragIntent.Actions));
    }

    private void EnsurePracticeImage()
    {
        if (!string.IsNullOrEmpty(practiceFile) && File.Exists(practiceFile))
            return;
        try
        {
            practiceFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"kwekdrop-practice-{Guid.NewGuid():N}.png");
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                var gradient = new LinearGradientBrush(Color.FromRgb(255, 122, 48), Color.FromRgb(255, 82, 0), new Point(0, 0), new Point(1, 1));
                dc.DrawRoundedRectangle(gradient, null, new Rect(0, 0, 180, 180), 28, 28);
                var text = new FormattedText("KwekDrop", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface(UiTheme.Font, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                    22, Brushes.White, 96);
                dc.DrawText(text, new Point((180 - text.Width) / 2, (180 - text.Height) / 2));
            }
            var rtb = new RenderTargetBitmap(180, 180, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using var fs = File.Create(practiceFile);
            encoder.Save(fs);
        }
        catch { }
    }

    // Page 3: Make it yours
    private UIElement BuildPage3()
    {
        var main = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 420,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        // Language
        main.Children.Add(new TextBlock
        {
            Text = L.T("Language"),
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = UiTheme.Ink,
            Margin = new Thickness(0, 0, 0, 8)
        });
        var langSeg = new SegmentedControl(new (string, string)[] { ("ar", "العربية"), ("en", "English") })
        {
            Selected = L.Language,
            Margin = new Thickness(0, 0, 0, 20)
        };
        langSeg.Chosen += code =>
        {
            L.Language = code;
            Rebuild();
        };
        main.Children.Add(langSeg);

        // Appearance
        main.Children.Add(new TextBlock
        {
            Text = L.T("Appearance"),
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = UiTheme.Ink,
            Margin = new Thickness(0, 0, 0, 8)
        });
        var themeSeg = new SegmentedControl(new (string, string)[]
        {
            ("light", L.T("Light")),
            ("dark", L.T("Dark")),
            ("system", L.T("Auto"))
        })
        {
            Selected = UiTheme.Choice,
            Margin = new Thickness(0, 0, 0, 20)
        };
        themeSeg.Chosen += choice =>
        {
            UiTheme.Choose(choice);
            Rebuild();
        };
        main.Children.Add(themeSeg);

        // Startup Checkbox
        startupCheck = new CheckBox
        {
            Content = L.T("Start KwekDrop when Windows starts"),
            FontSize = 12.5,
            Foreground = UiTheme.Ink,
            Margin = new Thickness(0, 0, 0, 16)
        };
        RefreshStartup();
        startupCheck.Click += async (_, _) =>
        {
            startupCheck.IsEnabled = false;
            var result = await StartupManager.SetAsync(startupCheck.IsChecked == true);
            startupCheck.IsChecked = result.Enabled;
            startupCheck.IsEnabled = result.CanChange;
        };
        main.Children.Add(startupCheck);

        // Caption
        main.Children.Add(new TextBlock
        {
            Text = L.T("KwekDrop lives next to the clock, so it is ready whenever you drag a file."),
            FontSize = 12,
            Foreground = UiTheme.Muted,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 18
        });

        return main;
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

    // Page 4: You're all set
    private UIElement BuildPage4()
    {
        var stack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var circle = new Border
        {
            Width = 72,
            Height = 72,
            CornerRadius = new CornerRadius(36),
            Background = new SolidColorBrush(Color.FromArgb(40, 255, 82, 0)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 20)
        };
        var checkPath = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M 20,36 L 31,47 L 52,24"),
            Stroke = UiTheme.Accent,
            StrokeThickness = 3.5,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        circle.Child = checkPath;
        stack.Children.Add(circle);

        stack.Children.Add(new TextBlock
        {
            Text = L.T("You're all set"),
            FontSize = 22,
            FontWeight = FontWeights.Bold,
            Foreground = UiTheme.Ink,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 12)
        });

        stack.Children.Add(new TextBlock
        {
            Text = L.T("Find the KwekDrop icon next to the clock for Settings, the guide and more."),
            FontSize = 13,
            Foreground = UiTheme.Muted,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            MaxWidth = 380,
            HorizontalAlignment = HorizontalAlignment.Center,
            LineHeight = 19
        });

        return stack;
    }
}
