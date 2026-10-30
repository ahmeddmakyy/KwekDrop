using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace KwekDrop;

internal sealed class QrWindow : Window
{
    private static QrWindow? current;

    public static void Open()
    {
        if (current == null)
        {
            current = new QrWindow();
            current.Closed += (_, _) => current = null;
            current.Show();
        }
        if (current.WindowState == WindowState.Minimized)
            current.WindowState = WindowState.Normal;
        current.Topmost = true;
        current.Activate();
        current.Topmost = false;
    }

    private readonly DispatcherTimer debounceTimer;
    private readonly DispatcherTimer copiedFeedbackTimer;

    // Controls
    private readonly SegmentedControl modeControl;
    private readonly TextBox inputTextBox;
    private readonly StackPanel textInputPanel;

    private readonly StackPanel wifiInputPanel;
    private readonly TextBox ssidBox;
    private readonly PasswordBox passwordBox;
    private readonly ComboBox securityCombo;
    private readonly CheckBox hiddenCheck;

    private readonly Border previewBorder;
    private readonly Image previewImage;
    private readonly TextBlock statusBlock;
    private readonly DrawingBrush checkerboardBrush;

    // Customise section
    private readonly StackPanel customisePanel;
    private readonly Border customiseToggle;
    private readonly TextBlock customiseArrow;
    private bool customiseExpanded;

    private readonly SegmentedControl moduleShapeControl;
    private readonly SegmentedControl eyeShapeControl;
    private readonly ColorSwatchPicker fgColorPicker;
    private readonly ColorSwatchPicker gradColorPicker;
    private readonly CheckBox gradEnableCheck;
    private readonly ColorSwatchPicker eyeColorPicker;
    private readonly CheckBox eyeColorCheck;
    private readonly ColorSwatchPicker bgColorPicker;
    private readonly CheckBox transparentCheck;
    private readonly SegmentedControl correctionControl;
    private readonly Slider marginSlider;
    private readonly TextBlock marginLabel;
    private readonly ComboBox sizeCombo;

    private readonly Button chooseLogoButton;
    private readonly Button clearLogoButton;
    private readonly TextBlock logoNameBlock;
    private byte[]? logoBytes;

    private readonly Button saveButton;
    private readonly Button copyButton;

    private QrMatrix? currentMatrix;

    public QrWindow()
    {
        UiTheme.Apply(this);
        Title = L.T("QR Code Maker");
        Width = 420;
        SizeToContent = SizeToContent.Height;
        MaxHeight = SystemParameters.WorkArea.Height * 0.92;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;

        Loaded += (_, _) => WindowBlur.ApplyRoundedRectBlur(this, (int)UiTheme.WindowCornerRadius, UiTheme.Dark);
        SizeChanged += (_, _) => WindowBlur.ApplyRoundedRectBlur(this, (int)UiTheme.WindowCornerRadius, UiTheme.Dark);

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
            }
        };

        debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
        debounceTimer.Tick += (_, _) =>
        {
            debounceTimer.Stop();
            RefreshPreview();
        };

        copiedFeedbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        copiedFeedbackTimer.Tick += (_, _) =>
        {
            copiedFeedbackTimer.Stop();
            if (copyButton != null) copyButton.Content = L.T("Copy image");
        };

        checkerboardBrush = CreateCheckerboardBrush();

        // 1. Inputs
        textInputPanel = new StackPanel();
        inputTextBox = new TextBox
        {
            Text = "https://github.com/ahmeddmakyy/KwekDrop",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 60,
            MaxHeight = 90,
            Padding = new Thickness(8),
            FontSize = 12.5
        };
        inputTextBox.TextChanged += (_, _) => ScheduleRefresh();
        textInputPanel.Children.Add(inputTextBox);

        wifiInputPanel = new StackPanel { Visibility = Visibility.Collapsed };
        ssidBox = new TextBox { Padding = new Thickness(8), FontSize = 12.5, Margin = new Thickness(0, 0, 0, 6) };
        ssidBox.TextChanged += (_, _) => ScheduleRefresh();
        passwordBox = new PasswordBox { Padding = new Thickness(8), FontSize = 12.5, Margin = new Thickness(0, 0, 0, 6) };
        passwordBox.PasswordChanged += (_, _) => ScheduleRefresh();

        securityCombo = new ComboBox { Margin = new Thickness(0, 0, 0, 6) };
        securityCombo.Items.Add(new ComboBoxItem { Content = "WPA / WPA2", Tag = "WPA" });
        securityCombo.Items.Add(new ComboBoxItem { Content = "WEP", Tag = "WEP" });
        securityCombo.Items.Add(new ComboBoxItem { Content = "None", Tag = "None" });
        securityCombo.SelectedIndex = 0;
        securityCombo.SelectionChanged += (_, _) => ScheduleRefresh();

        hiddenCheck = new CheckBox { Content = L.T("Hidden network"), Margin = new Thickness(2, 2, 0, 0) };
        hiddenCheck.Click += (_, _) => ScheduleRefresh();

        wifiInputPanel.Children.Add(MakeFieldRow(L.T("Network name (SSID)"), ssidBox));
        wifiInputPanel.Children.Add(MakeFieldRow(L.T("Password"), passwordBox));
        wifiInputPanel.Children.Add(MakeFieldRow(L.T("Security"), securityCombo));
        wifiInputPanel.Children.Add(hiddenCheck);

        // Mode segmented control
        modeControl = new SegmentedControl([
            ("text", L.T("Text / Link")),
            ("wifi", L.T("Wi-Fi"))
        ]) { Selected = "text", Margin = new Thickness(0, 0, 0, 10) };
        modeControl.Chosen += mode =>
        {
            textInputPanel.Visibility = mode == "text" ? Visibility.Visible : Visibility.Collapsed;
            wifiInputPanel.Visibility = mode == "wifi" ? Visibility.Visible : Visibility.Collapsed;
            ScheduleRefresh();
        };

        // 3. Preview Square (300 px)
        previewImage = new Image { Width = 300, Height = 300, Stretch = Stretch.Uniform };
        previewBorder = new Border
        {
            Width = 300,
            Height = 300,
            CornerRadius = new CornerRadius(10),
            BorderBrush = UiTheme.Line,
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 10, 0, 6),
            Background = Brushes.White,
            ClipToBounds = true,
            Child = previewImage,
            AllowDrop = true
        };

        previewBorder.DragOver += (s, e) =>
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        };
        previewBorder.Drop += (s, e) =>
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files?.Length > 0) LoadLogoFile(files[0]);
            }
        };

        statusBlock = new TextBlock
        {
            Text = L.T("Scans correctly"),
            FontSize = 11.5,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8),
            Foreground = UiTheme.Muted
        };

        // 4. Customise collapsible section
        customiseArrow = new TextBlock
        {
            Text = "›",
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = UiTheme.Muted,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0)
        };

        var customiseHeader = new DockPanel { LastChildFill = false };
        var headerLabel = new TextBlock
        {
            Text = L.T("Customise"),
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = UiTheme.Ink,
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(customiseArrow, Dock.Left);
        DockPanel.SetDock(headerLabel, Dock.Left);
        customiseHeader.Children.Add(customiseArrow);
        customiseHeader.Children.Add(headerLabel);

        customiseToggle = new Border
        {
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            Padding = new Thickness(4, 6, 4, 6),
            Child = customiseHeader
        };
        customiseToggle.MouseLeftButtonDown += (_, _) => ToggleCustomise();

        customisePanel = new StackPanel { Margin = new Thickness(4, 6, 4, 0), Visibility = Visibility.Collapsed };

        // Module Shape
        moduleShapeControl = new SegmentedControl([
            ("Square", L.T("Square")),
            ("Rounded", L.T("Rounded")),
            ("Dots", L.T("Dots")),
            ("Diamond", L.T("Diamond")),
            ("Bars", L.T("Bars"))
        ]) { Selected = "Square", Margin = new Thickness(0, 4, 0, 8) };
        moduleShapeControl.Chosen += _ => ScheduleRefresh();
        customisePanel.Children.Add(MakeSectionLabel(L.T("Module shape")));
        customisePanel.Children.Add(moduleShapeControl);

        // Eye Shape
        eyeShapeControl = new SegmentedControl([
            ("Square", L.T("Square")),
            ("Rounded", L.T("Rounded")),
            ("Circle", L.T("Circle")),
            ("Leaf", L.T("Leaf"))
        ]) { Selected = "Square", Margin = new Thickness(0, 4, 0, 8) };
        eyeShapeControl.Chosen += _ => ScheduleRefresh();
        customisePanel.Children.Add(MakeSectionLabel(L.T("Eye shape")));
        customisePanel.Children.Add(eyeShapeControl);

        // Colors
        customisePanel.Children.Add(MakeSectionLabel(L.T("Colors")));
        var colorsGrid = new Grid { Margin = new Thickness(0, 4, 0, 8) };
        colorsGrid.ColumnDefinitions.Add(new ColumnDefinition());
        colorsGrid.ColumnDefinitions.Add(new ColumnDefinition());
        colorsGrid.RowDefinitions.Add(new RowDefinition());
        colorsGrid.RowDefinitions.Add(new RowDefinition());

        fgColorPicker = new ColorSwatchPicker("#000000", showHexBox: false);
        fgColorPicker.ColorChanged += _ => ScheduleRefresh();
        var fgRow = MakePickerItem(L.T("Foreground"), fgColorPicker);
        Grid.SetRow(fgRow, 0); Grid.SetColumn(fgRow, 0);
        colorsGrid.Children.Add(fgRow);

        bgColorPicker = new ColorSwatchPicker("#FFFFFF", showHexBox: false);
        bgColorPicker.ColorChanged += _ => ScheduleRefresh();
        var bgRow = MakePickerItem(L.T("Background"), bgColorPicker);
        Grid.SetRow(bgRow, 0); Grid.SetColumn(bgRow, 1);
        colorsGrid.Children.Add(bgRow);

        gradColorPicker = new ColorSwatchPicker("#FF5200", showHexBox: false) { IsEnabled = false, Opacity = 0.5 };
        gradColorPicker.ColorChanged += _ => ScheduleRefresh();
        gradEnableCheck = new CheckBox { Content = L.T("Gradient"), Margin = new Thickness(0, 4, 0, 0), FontSize = 11.5 };
        gradEnableCheck.Click += (_, _) =>
        {
            bool on = gradEnableCheck.IsChecked == true;
            gradColorPicker.IsEnabled = on;
            gradColorPicker.Opacity = on ? 1.0 : 0.5;
            ScheduleRefresh();
        };
        var gradRow = new StackPanel { Margin = new Thickness(0, 4, 8, 4) };
        gradRow.Children.Add(gradEnableCheck);
        gradRow.Children.Add(gradColorPicker);
        Grid.SetRow(gradRow, 1); Grid.SetColumn(gradRow, 0);
        colorsGrid.Children.Add(gradRow);

        eyeColorPicker = new ColorSwatchPicker("#FF5200", showHexBox: false) { IsEnabled = false, Opacity = 0.5 };
        eyeColorPicker.ColorChanged += _ => ScheduleRefresh();
        eyeColorCheck = new CheckBox { Content = L.T("Custom eye color"), Margin = new Thickness(0, 4, 0, 0), FontSize = 11.5 };
        eyeColorCheck.Click += (_, _) =>
        {
            bool on = eyeColorCheck.IsChecked == true;
            eyeColorPicker.IsEnabled = on;
            eyeColorPicker.Opacity = on ? 1.0 : 0.5;
            ScheduleRefresh();
        };
        var eyeRow = new StackPanel { Margin = new Thickness(8, 4, 0, 4) };
        eyeRow.Children.Add(eyeColorCheck);
        eyeRow.Children.Add(eyeColorPicker);
        Grid.SetRow(eyeRow, 1); Grid.SetColumn(eyeRow, 1);
        colorsGrid.Children.Add(eyeRow);

        customisePanel.Children.Add(colorsGrid);

        transparentCheck = new CheckBox { Content = L.T("Transparent background"), Margin = new Thickness(2, 0, 0, 8), FontSize = 11.5 };
        transparentCheck.Click += (_, _) => ScheduleRefresh();
        customisePanel.Children.Add(transparentCheck);

        // Error Correction
        correctionControl = new SegmentedControl([
            ("L", "L (7%)"),
            ("M", "M (15%)"),
            ("Q", "Q (25%)"),
            ("H", "H (30%)")
        ]) { Selected = "M", Margin = new Thickness(0, 4, 0, 8) };
        correctionControl.Chosen += _ => ScheduleRefresh();
        customisePanel.Children.Add(MakeSectionLabel(L.T("Error correction")));
        customisePanel.Children.Add(correctionControl);

        // Margin & Size
        var marginSizeGrid = new Grid { Margin = new Thickness(0, 4, 0, 8) };
        marginSizeGrid.ColumnDefinitions.Add(new ColumnDefinition());
        marginSizeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        marginSizeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });

        var marginPanel = new StackPanel();
        marginLabel = new TextBlock { Text = $"{L.T("Margin")}: 4", FontSize = 11.5, Foreground = UiTheme.Muted, Margin = new Thickness(0, 0, 0, 2) };
        marginSlider = new Slider { Minimum = 1, Maximum = 8, Value = 4, IsSnapToTickEnabled = true, TickFrequency = 1 };
        marginSlider.ValueChanged += (_, _) =>
        {
            marginLabel.Text = $"{L.T("Margin")}: {(int)marginSlider.Value}";
            ScheduleRefresh();
        };
        marginPanel.Children.Add(marginLabel);
        marginPanel.Children.Add(marginSlider);
        Grid.SetColumn(marginPanel, 0);
        marginSizeGrid.Children.Add(marginPanel);

        var sizePanel = new StackPanel();
        sizePanel.Children.Add(new TextBlock { Text = L.T("Output size"), FontSize = 11.5, Foreground = UiTheme.Muted, Margin = new Thickness(0, 0, 0, 2) });
        sizeCombo = new ComboBox();
        sizeCombo.Items.Add("256 px");
        sizeCombo.Items.Add("512 px");
        sizeCombo.Items.Add("1024 px");
        sizeCombo.Items.Add("2048 px");
        sizeCombo.SelectedIndex = 1; // 512
        sizeCombo.SelectionChanged += (_, _) => ScheduleRefresh();
        sizePanel.Children.Add(sizeCombo);
        Grid.SetColumn(sizePanel, 2);
        marginSizeGrid.Children.Add(sizePanel);

        customisePanel.Children.Add(marginSizeGrid);

        // Logo row
        customisePanel.Children.Add(MakeSectionLabel(L.T("Logo")));
        var logoRow = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 4, 0, 4) };
        chooseLogoButton = new Button
        {
            Content = L.T("Choose Logo..."),
            Padding = new Thickness(12, 5, 12, 5),
            FontSize = 11.5,
            Background = Brushes.Transparent,
            Foreground = UiTheme.Ink,
            BorderBrush = UiTheme.Line,
            BorderThickness = new Thickness(1)
        };
        chooseLogoButton.Click += (_, _) => PickLogoFile();
        DockPanel.SetDock(chooseLogoButton, Dock.Left);
        logoRow.Children.Add(chooseLogoButton);

        clearLogoButton = new Button
        {
            Content = L.T("Clear Logo"),
            Padding = new Thickness(10, 5, 10, 5),
            FontSize = 11.5,
            Margin = new Thickness(8, 0, 0, 0),
            Background = Brushes.Transparent,
            Foreground = UiTheme.Muted,
            BorderBrush = UiTheme.Line,
            BorderThickness = new Thickness(1),
            Visibility = Visibility.Collapsed
        };
        clearLogoButton.Click += (_, _) => ClearLogo();
        DockPanel.SetDock(clearLogoButton, Dock.Left);
        logoRow.Children.Add(clearLogoButton);

        logoNameBlock = new TextBlock
        {
            Text = L.T("Drop PNG or JPG logo on preview"),
            FontSize = 11,
            Foreground = UiTheme.Muted,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0)
        };
        DockPanel.SetDock(logoNameBlock, Dock.Left);
        logoRow.Children.Add(logoNameBlock);

        customisePanel.Children.Add(logoRow);

        // 5. Footer: Save (accent) & Copy
        var footer = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 14, 0, 0) };

        copyButton = new Button
        {
            Content = L.T("Copy image"),
            FontSize = 12.5,
            Padding = new Thickness(16, 7, 16, 7),
            Background = Brushes.Transparent,
            Foreground = UiTheme.Ink,
            BorderBrush = UiTheme.Line,
            BorderThickness = new Thickness(1),
            MinHeight = 34
        };
        copyButton.Click += (_, _) => CopyToClipboard();
        DockPanel.SetDock(copyButton, Dock.Left);
        footer.Children.Add(copyButton);

        saveButton = new Button
        {
            Content = L.T("Save"),
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Padding = new Thickness(26, 7, 26, 7),
            Background = UiTheme.Accent,
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            MinHeight = 34
        };
        saveButton.Click += (_, _) => SaveToFile();
        DockPanel.SetDock(saveButton, Dock.Right);
        footer.Children.Add(saveButton);

        // Assemble root panel
        var rootPanel = new StackPanel { Margin = new Thickness(18, 8, 18, 14) };
        rootPanel.Children.Add(modeControl);
        rootPanel.Children.Add(textInputPanel);
        rootPanel.Children.Add(wifiInputPanel);
        rootPanel.Children.Add(previewBorder);
        rootPanel.Children.Add(statusBlock);
        rootPanel.Children.Add(customiseToggle);
        rootPanel.Children.Add(customisePanel);
        rootPanel.Children.Add(footer);

        var scrollViewer = new ScrollViewer
        {
            Content = rootPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        Content = UiTheme.Frame(this, scrollViewer, L.T("QR Code Maker"), toolChrome: true);

        RefreshPreview();
    }

    private void ToggleCustomise()
    {
        customiseExpanded = !customiseExpanded;
        customisePanel.Visibility = customiseExpanded ? Visibility.Visible : Visibility.Collapsed;
        customiseArrow.Text = customiseExpanded ? "⌄" : "›";
    }

    private void ScheduleRefresh()
    {
        debounceTimer.Stop();
        debounceTimer.Start();
    }

    private string GetPayload()
    {
        if (modeControl.Selected == "wifi")
        {
            string sec = (securityCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "WPA";
            return QrContent.Wifi(ssidBox.Text, passwordBox.Password, sec, hiddenCheck.IsChecked == true);
        }
        return inputTextBox.Text;
    }

    private QrDesign BuildDesign(int size)
    {
        var shape = Enum.TryParse<QrModuleShape>(moduleShapeControl.Selected, out var ms) ? ms : QrModuleShape.Square;
        var eye = Enum.TryParse<QrEyeShape>(eyeShapeControl.Selected, out var es) ? es : QrEyeShape.Square;

        Color fg = TryParseHex(fgColorPicker.HexColor, Colors.Black);
        Color bg = TryParseHex(bgColorPicker.HexColor, Colors.White);
        Color? grad = gradEnableCheck.IsChecked == true ? TryParseHex(gradColorPicker.HexColor, Colors.Black) : null;
        Color? eyeCol = eyeColorCheck.IsChecked == true ? TryParseHex(eyeColorPicker.HexColor, Colors.Black) : null;

        return new QrDesign
        {
            ModuleShape = shape,
            EyeShape = eye,
            ForegroundColor = fg,
            BackgroundColor = bg,
            GradientEndColor = grad,
            EyeColor = eyeCol,
            IsTransparentBackground = transparentCheck.IsChecked == true,
            Margin = (int)marginSlider.Value,
            OutputSize = size,
            LogoBytes = logoBytes
        };
    }

    private void RefreshPreview()
    {
        string payload = GetPayload();
        if (string.IsNullOrWhiteSpace(payload))
        {
            previewImage.Source = null;
            statusBlock.Text = L.T("Enter text or link");
            statusBlock.Foreground = UiTheme.Muted;
            currentMatrix = null;
            return;
        }

        // Error correction level
        var level = Enum.TryParse<QrCorrection>(correctionControl.Selected, out var ec) ? ec : QrCorrection.M;

        // If logo is set, force at least Q
        if (logoBytes != null && logoBytes.Length > 0 && level < QrCorrection.Q)
        {
            level = QrCorrection.Q;
            correctionControl.Selected = "Q";
            statusBlock.Text = L.T("Correction set to Q for logo support");
        }

        var matrix = QrMatrix.Encode(payload, level);
        if (matrix == null)
        {
            previewImage.Source = null;
            statusBlock.Text = L.T("Text too long to encode in QR code");
            statusBlock.Foreground = new SolidColorBrush(Color.FromRgb(216, 67, 21));
            currentMatrix = null;
            return;
        }

        currentMatrix = matrix;
        var design = BuildDesign(300);

        previewBorder.Background = design.IsTransparentBackground ? checkerboardBrush : new SolidColorBrush(design.BackgroundColor);
        var bmp = QrRender.ToBitmapSource(matrix, design);
        previewImage.Source = bmp;

        // Scannability check
        bool ok = QrRender.VerifyScannability(matrix, design, out string warning);
        if (ok)
        {
            statusBlock.Text = L.T("Scans correctly");
            statusBlock.Foreground = new SolidColorBrush(Color.FromRgb(46, 125, 50));
        }
        else
        {
            statusBlock.Text = L.T(warning);
            statusBlock.Foreground = new SolidColorBrush(Color.FromRgb(216, 67, 21));
        }
    }

    private void PickLogoFile()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Images (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg"
        };
        if (dlg.ShowDialog() == true)
        {
            LoadLogoFile(dlg.FileName);
        }
    }

    private void LoadLogoFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                logoBytes = File.ReadAllBytes(path);
                logoNameBlock.Text = Path.GetFileName(path);
                clearLogoButton.Visibility = Visibility.Visible;
                if (correctionControl.Selected is "L" or "M")
                    correctionControl.Selected = "Q";
                ScheduleRefresh();
            }
        }
        catch
        {
            // Ignore corrupted logo file
        }
    }

    private void ClearLogo()
    {
        logoBytes = null;
        logoNameBlock.Text = L.T("Drop PNG or JPG logo on preview");
        clearLogoButton.Visibility = Visibility.Collapsed;
        ScheduleRefresh();
    }

    private void CopyToClipboard()
    {
        if (currentMatrix == null) return;
        int size = GetChosenSize();
        var design = BuildDesign(size);
        var bmp = QrRender.ToBitmapSource(currentMatrix, design);
        Clipboard.SetImage(bmp);

        copyButton.Content = L.T("Copied!");
        copiedFeedbackTimer.Stop();
        copiedFeedbackTimer.Start();
    }

    private void SaveToFile()
    {
        if (currentMatrix == null) return;
        var dlg = new SaveFileDialog
        {
            FileName = "qr-code",
            Filter = "PNG Image (*.png)|*.png|SVG Image (*.svg)|*.svg",
            DefaultExt = ".png"
        };

        if (dlg.ShowDialog() == true)
        {
            int size = GetChosenSize();
            var design = BuildDesign(size);
            if (dlg.FileName.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            {
                string svg = QrRender.ToSvg(currentMatrix, design);
                File.WriteAllText(dlg.FileName, svg, Encoding.UTF8);
            }
            else
            {
                var bmp = QrRender.ToBitmapSource(currentMatrix, design);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bmp));
                using var stream = File.Create(dlg.FileName);
                encoder.Save(stream);
            }
        }
    }

    private int GetChosenSize() => sizeCombo.SelectedIndex switch
    {
        0 => 256,
        1 => 512,
        2 => 1024,
        3 => 2048,
        _ => 512
    };

    private static Color TryParseHex(string hex, Color fallback)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return fallback; }
    }

    private static DrawingBrush CreateCheckerboardBrush()
    {
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(Brushes.White, null, new RectangleGeometry(new Rect(0, 0, 16, 16))));
        var gray = new SolidColorBrush(Color.FromRgb(235, 235, 235));
        gray.Freeze();
        group.Children.Add(new GeometryDrawing(gray, null, new RectangleGeometry(new Rect(0, 0, 8, 8))));
        group.Children.Add(new GeometryDrawing(gray, null, new RectangleGeometry(new Rect(8, 8, 8, 8))));
        var brush = new DrawingBrush(group)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, 16, 16),
            ViewportUnits = BrushMappingMode.Absolute
        };
        brush.Freeze();
        return brush;
    }

    private static FrameworkElement MakeSectionLabel(string label) =>
        new TextBlock { Text = label, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = UiTheme.Ink, Margin = new Thickness(0, 4, 0, 2) };

    private static FrameworkElement MakeFieldRow(string label, FrameworkElement field)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 4) };
        panel.Children.Add(new TextBlock { Text = label, FontSize = 11.5, Foreground = UiTheme.Muted, Margin = new Thickness(0, 0, 0, 2) });
        panel.Children.Add(field);
        return panel;
    }

    private static FrameworkElement MakePickerItem(string label, ColorSwatchPicker picker)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 2, 8, 2) };
        panel.Children.Add(new TextBlock { Text = label, FontSize = 11.5, Foreground = UiTheme.Muted, Margin = new Thickness(0, 0, 0, 2) });
        panel.Children.Add(picker);
        return panel;
    }
}
