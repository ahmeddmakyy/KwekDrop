using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Path = System.IO.Path;

namespace KwekDrop;

internal sealed class ToolWindow : Window
{
    private readonly string[] paths;
    private readonly Operation operation;
    private readonly Func<ConversionJob, CancellationToken, IProgress<JobProgress>?, Task<BatchResult>> submit;
    private readonly Dictionary<string, FrameworkElement> controls = [];
    private readonly Dictionary<string, TextBlock> fieldLabels = [];
    private readonly TextBlock status = new(), details = new();
    private readonly Image image = new() { Stretch = Stretch.Uniform };
    private readonly Canvas overlay = new() { Background = Brushes.Transparent };
    private readonly Grid previewHost = new();
    private Grid? cropZoom;
    private MatrixTransform? cropZoomTransform;
    private readonly MediaElement player = new() { LoadedBehavior = MediaState.Manual, UnloadedBehavior = MediaState.Stop };
    private readonly ListBox order = new() { Height = 120 };
    private readonly Slider timeline = new() { Minimum = 0, Maximum = 1 };
    private readonly TextBlock positionLabel = new() { Foreground = UiTheme.Muted, Margin = new Thickness(0, 8, 0, 4) };
    private readonly Rectangle selection = new() { Stroke = Brushes.DarkOrange, StrokeThickness = 2, Fill = new SolidColorBrush(Color.FromArgb(35, 255, 120, 20)), Visibility = Visibility.Collapsed };
    private readonly List<Dictionary<string, object>> regions = [];
    private readonly List<Dictionary<string, double>> audioRanges = [];
    private readonly ListBox regionList = new() { Height = 82 };
    private readonly CancellationTokenSource closing = new();
    private readonly SemaphoreSlim previewGate = new(1, 1);
    private readonly DispatcherTimer previewDelay;
    private string folder = Path.Combine(Journal.DirectoryPath, "preview-" + Guid.NewGuid().ToString("N"));
    private string? originalPreview;
    private int originalWidth, originalHeight, pages;
    private double totalDuration, fps = 30;
    private Point? selectionStart;
    private bool loaded, showingOriginal = true;
    private bool rangeEdited;
    private int previewVersion;
    private string metadataText = "";
    private Button saveButton = null!, cancelButton = null!, revealButton = null!, copyButton = null!;
    private Button? trimPlayButton;
    private readonly BusyLine busyLine = new();
    private readonly CropFrame cropFrame = new();
    private string editedField = "";
    private FrameworkElement bodyPanel = null!;
    private CancellationTokenSource? saving, previewCancellation;
    private string? lastOutput;
    private readonly ComboBox framePicker = new() { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 0, 0, 10) };
    private bool syncingRegion;
    private int imageFrame = -1;
    private bool Cropping => operation.Id is "cropImage" or "cropVideo";
    // Rotating is previewed by transforming the picture (or the playing video) itself, so every change shows at once.
    private bool Rotating => operation.Id is "rotateImage" or "rotateVideo";
    private bool TrimAudio => operation.Id == "trimAudio";
    private bool Simple => host == null && !HasMedia && !operation.Ordered && (
        operation.Id is "compress" or "removeMetadata" or "resizeImage" or "makeIcon" or "ocrImage" or "ocrPDF" or "location" or "splitPDF" or "organizePDF" or "pdfPassword" or "pdfNumbers" or "extractPdfImages" or "officePdf" or "extractArchive" or "packArchive" or "createPDF" or "createCollage" or "createAnimation"
        || (operation.Id == "watermark" && Catalog.Category(paths[0]) is "image" or "document")
    );
    private bool Compact => (operation.Id is "editImage" or "frameImage") || Simple;
    private bool MediaCompact => host == null && !TrimAudio && (HasMedia || (operation.Id == "compress" && Catalog.Category(paths[0]) is "audio" or "video"));
    private Action? syncFrameExtras;
    private readonly List<Action> syncSliderCallbacks = [];
    internal bool DebugReady => loaded;
    internal bool DebugCropFrameReady => loaded && cropFrame.HasSource && cropFrame.Visibility == Visibility.Visible;
    private readonly Grid rotateStage = new();
    private readonly Border rotatePlate = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed };
    // Cropping is previewed by the frame itself, so the preview is not regenerated while its settings change.
    private bool LivePreview => RefreshFrame && !HasMedia && operation.Id != "cropImage";
    private bool VisualPreview => ((operation.Id is "compress" or "resizeImage" or "ocrImage" or "watermark") && Catalog.Category(paths[0]) == "image") || operation.Id is "editImage" or "frameImage" or "cropImage" or "cropVideo" or "redactImage" or "redactVideo" or "redactPDF" or "organizePDF" or "createCollage";
    private bool ProcessedPlayback => operation.Id is "normalizeAudio" or "audioChannels" or "audioToVideo" or "redactAudio" or "trimAudio" or "trimVideo" or "cropVideo" or "changeVideoSpeed" or "redactVideo" or "muteVideo" or "audioEffects";
    private bool HasMedia => Catalog.Category(paths[0]) is "audio" or "video";
    private MediaTransport? transport;
    private bool syncingPlayback;
    private bool syncingTrim;
    private bool HasTimeline => operation.Id is "videoToGif" or "cropVideo" or "redactVideo" or "videoSnapshots" or "trimVideo" or "trimAudio" or "redactAudio";
    private bool RefreshFrame => VisualPreview || HasTimeline && Catalog.Category(paths[0]) == "video";
    private Button? compareButton;
    private Slider? cropWidthSlider, cropHeightSlider;
    private TextBlock? cropWidthValue, cropHeightValue, cropSizeLabel;
    private TextBlock? trimSelected;
    private bool syncingCropSliders;
    private byte[]? editSourceBgra;
    private byte[]? editTargetBgra;
    private int editBgraWidth;
    private int editBgraHeight;
    private double editBaseMean = -1;
    private WriteableBitmap? editLiveBitmap;
    private int editLiveVersion;
    private bool editRenderingLive;

    private Button? fxPlayButton;
    private bool fxPlaying;
    private CancellationTokenSource? fxPreviewCancellation;

    private MediaElement? stagePlayer;
    private DispatcherTimer? a2vPreviewTimer;
    private CancellationTokenSource? a2vCancellation;

    // Set when this tool is shown inside another window (a combined window); the tool then provides only its body.
    private readonly Window? host;
    public UIElement? Body { get; }
    public bool Busy => saving != null;
    // In a combined window, a successful save hands the result to the host instead of finishing here.
    public Action<BatchResult>? Saved { get; set; }
    public string SaveLabel { set => saveButton.Content = value; }
    public Task Start() => Initialize();
    private string ReadyText => Saved != null ? L.T("调整参数后点“应用这一步”。") : "";
    // Stops previews and playback and removes this tool's preview folder. Called when the window closes, or by the
    // host when it removes or closes an embedded tool.
    public async void Release()
    {
        closing.Cancel();
        previewCancellation?.Cancel();
        previewDelay.Stop();
        StopAudioEffectsPreview();
        a2vPreviewTimer?.Stop();
        a2vCancellation?.Cancel();
        if (stagePlayer != null) { stagePlayer.Stop(); stagePlayer.Source = null; }
        transport?.Dispose();
        player.Stop();
        player.Source = null;
        // The player and the engine let go of the files a moment after they stop.
        await Task.Delay(1000);
        try
        {
            string root = Path.GetFullPath(Journal.DirectoryPath) + Path.DirectorySeparatorChar;
            if (Path.GetFullPath(folder).StartsWith(root, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(folder).StartsWith("preview-") && Directory.Exists(folder))
                Directory.Delete(folder, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    public ToolWindow(string[] paths, Operation operation, Func<ConversionJob, CancellationToken, IProgress<JobProgress>?, Task<BatchResult>> submit, Window? host = null)
    {
        this.paths = paths;
        this.operation = operation;
        this.submit = submit;
        this.host = host;
        if (host != null)
        {
            previewDelay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(550) };
            previewDelay.Tick += async (_, _) => { previewDelay.Stop(); await UpdatePreview(); };
            Body = Build();
            return;
        }
        Title = operation.Label + " · " + Path.GetFileName(paths[0]);
        if (MediaCompact)
        {
            Width = operation.Id switch
            {
                "trimVideo" => 580,
                "cropVideo" => 560,
                "changeVideoSpeed" => 560,
                "splitVideo" => 580,
                "videoSnapshots" => 580,
                "redactVideo" => 580,
                "rotateVideo" => 480,
                "videoSettings" => 420,
                "videoEffects" => 440,
                "videoToGif" => 580,
                "subtitlesAudio" => 520,
                "muteVideo" => 480,
                "joinVideos" => 400,
                "compress" => 400,
                "normalizeAudio" => 480,
                "audioToVideo" => 580,
                "audioChannels" => 480,
                "redactAudio" => 540,
                "audioEffects" => 420,
                "ringtone" => 440,
                "joinAudio" => 480,
                _ => 480
            };
            if (operation.Id == "joinAudio")
            {
                Height = 460;
            }
            else
            {
                SizeToContent = SizeToContent.Height;
                MaxHeight = SystemParameters.WorkArea.Height * 0.92;
                ResizeMode = ResizeMode.NoResize;
            }
            MinWidth = Width;
        }
        else
        {
            Width = operation.Id == "editImage" ? 360 : operation.Id == "frameImage" ? 340 : Simple ? 400 : Cropping ? 560 : TrimAudio ? 360 : 680;
            if (Simple)
            {
                SizeToContent = SizeToContent.Height;
                MaxHeight = SystemParameters.WorkArea.Height * 0.92;
            }
            else
            {
                Height = Compact ? 700 : Cropping ? 780 : TrimAudio ? 380 : 540;
            }
            MinWidth = operation.Id == "editImage" ? 340 : operation.Id == "frameImage" ? 320 : Simple ? 360 : Cropping ? 440 : TrimAudio ? 320 : 540;
            MinHeight = Simple ? 280 : Compact ? 520 : Cropping ? 640 : TrimAudio ? 340 : 440;
        }
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = UiTheme.WindowBack;
        FontFamily = UiTheme.Font;
        FontSize = 13;
        previewDelay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(550) };
        previewDelay.Tick += async (_, _) => { previewDelay.Stop(); await UpdatePreview(); };
        UiTheme.Apply(this);
        Content = UiTheme.Frame(this, Build(), MediaCompact ? operation.Label : null, toolChrome: true);
        Loaded += async (_, _) => await Initialize();
        Closing += (_, e) => { if (saving != null) { e.Cancel = true; status.Text = L.T("处理中。请先点击取消，等待任务停止后关闭窗口。"); } };
        PreviewKeyDown += async (_, e) =>
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                e.Handled = true;
                await Save();
            }
            else if (e.Key == Key.Space && Keyboard.FocusedElement is not TextBox && transport != null && operation.Id is "trimVideo" or "trimAudio")
            {
                e.Handled = true;
                await PlayTrimRange();
            }
            else if (e.Key == Key.Space && Keyboard.FocusedElement is not TextBox && operation.Id == "audioEffects")
            {
                e.Handled = true;
                await ToggleAudioEffectsPreview();
            }
        };
        Closed += (_, _) => Release();
    }
    private UIElement Build()
    {
        var root = new Grid { Margin = MediaCompact ? new Thickness(16, 6, 16, 12) : TrimAudio ? new Thickness(16, 2, 16, 12) : Compact ? new Thickness(16, 4, 16, 14) : new Thickness(24, 8, 24, 22) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = Simple || MediaCompact ? GridLength.Auto : new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var header = new StackPanel();
        var headerTitle = new TextBlock { Text = operation.Label, FontSize = Cropping ? 22 : TrimAudio ? 19 : Compact ? 20 : 26, FontWeight = FontWeights.SemiBold };
        var headerFile = new TextBlock { Text = paths.Length == 1 ? Path.GetFileName(paths[0]) : L.F("已选 {0} 个文件", paths.Length), Foreground = UiTheme.Muted, Margin = new Thickness(0, Cropping || TrimAudio || Compact ? 4 : 6, 0, Cropping || TrimAudio || Compact ? 8 : 20), Visibility = TrimAudio || Compact ? Visibility.Collapsed : Visibility.Visible };
        header.HorizontalAlignment = HorizontalAlignment.Center;
        headerTitle.HorizontalAlignment = HorizontalAlignment.Center;
        headerFile.HorizontalAlignment = HorizontalAlignment.Center;
        header.Children.Add(headerTitle);
        header.Children.Add(headerFile);
        if (Compact || MediaCompact)
            header.Visibility = Visibility.Collapsed;
        if (host != null)
        { header.Visibility = Visibility.Collapsed; root.Margin = new Thickness(0); }
        root.Children.Add(header);
        var body = new Grid();
        if (Cropping || Rotating || TrimAudio || Compact || MediaCompact)
        {
            // Tangerine layout: Full width centered preview with clean horizontal controls below!
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }
        else
        {
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
        }
        if (Compact || MediaCompact)
        {
            body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            body.RowDefinitions.Add(new RowDefinition { Height = Simple || MediaCompact ? GridLength.Auto : new GridLength(1, GridUnitType.Star) });
        }
        bodyPanel = body;
        Grid.SetRow(body, 1);
        root.Children.Add(body);
        var left = new StackPanel { Margin = new Thickness(0, 0, 0, 0) };
        if (!Cropping && !TrimAudio && !Compact && !MediaCompact)
            left.Children.Add(new TextBlock { Text = VisualPreview || Rotating ? L.T("效果预览") : L.T("源文件预览"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 12) });
        left.Children.Add(framePicker);
        framePicker.SelectionChanged += async (_, _) => { if (loaded && framePicker.SelectedItem is ComboBoxItem item) { imageFrame = (int)item.Tag; regions.Clear(); regionList.Items.Clear(); await Initialize(); if (LivePreview) SchedulePreview(); } };
        // A whole PDF page needs more room to mark small text accurately.
        previewHost.Height = TrimAudio ? 1 : operation.Id == "redactPDF" ? 480 : Compact ? 210 : operation.Id == "cropVideo" ? 360 : operation.Id is "trimVideo" or "changeVideoSpeed" or "videoSnapshots" ? 300 : operation.Id is "redactVideo" or "videoToGif" ? 280 : MediaCompact && (Catalog.Category(paths[0]) == "audio" || operation.Id is "joinVideos" or "compress" or "videoSettings" or "videoEffects" or "subtitlesAudio" or "muteVideo") ? 0 : Cropping ? 250 : Rotating ? 320 : 280;
        if (TrimAudio || (MediaCompact && previewHost.Height == 0))
            previewHost.Visibility = Visibility.Collapsed;
        if (TrimAudio)
            previewHost.Visibility = Visibility.Hidden;
        if (Cropping && !HasMedia)
        {
            previewHost.Width = 300;
            previewHost.HorizontalAlignment = HorizontalAlignment.Center;
        }
        if (Compact)
            previewHost.HorizontalAlignment = HorizontalAlignment.Center;
        previewHost.Background = UiTheme.PreviewBack;
        previewHost.SizeChanged += (_, _) =>
        {
            if (previewHost.ActualWidth > 0 && previewHost.ActualHeight > 0)
            {
                double radius = UiTheme.Dark && Cropping && !HasMedia ? 4 : 12;
                previewHost.Clip = new RectangleGeometry(new Rect(0, 0, previewHost.ActualWidth, previewHost.ActualHeight), radius, radius);
            }
        };
        if (Rotating)
        {
            rotateStage.Children.Add(rotatePlate);
            rotateStage.Children.Add(image);
            rotateStage.Children.Add(player);
            previewHost.Children.Add(rotateStage);
            previewHost.SizeChanged += (_, _) => UpdateRotationPreview();
        }
        else
        {
            if (Cropping)
            {
                // The picture and the frame zoom together; the transport controls sit outside, so they never scale.
                cropZoom = new Grid();
                cropZoom.RenderTransform = cropZoomTransform = new MatrixTransform();
                previewHost.Children.Add(cropZoom);
                previewHost.ClipToBounds = true;
                previewHost.PreviewMouseWheel += ZoomCrop;
                cropZoom.Children.Add(image);
                cropZoom.Children.Add(player);
            }
            else
            {
                previewHost.Children.Add(image);
                previewHost.Children.Add(player);
            }
        }
        player.Visibility = Visibility.Collapsed;
        player.MediaEnded += (_, _) => { if (operation.Id == "audioEffects") StopAudioEffectsPreview(); };
        previewHost.Children.Add(overlay);
        overlay.Children.Add(selection);
        if (Cropping)
            AddCropFrame();
        // A thin line instead of a card: it never hides the picture and only appears if the work takes a moment.
        previewHost.Children.Add(busyLine);
        overlay.IsHitTestVisible = !Cropping;
        busyLine.IsHitTestVisible = false;
        if (Cropping && !HasMedia)
        {
            var shell = new Border
            {
                Child = previewHost,
                BorderBrush = UiTheme.Dark ? new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)) : Brushes.Transparent,
                BorderThickness = UiTheme.Dark ? new Thickness(1) : new Thickness(0),
                CornerRadius = new CornerRadius(UiTheme.Dark ? 4 : 12),
                ClipToBounds = true,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 2)
            };
            left.Children.Add(shell);
        }
        else
            left.Children.Add(previewHost);
        overlay.MouseLeftButtonDown += BeginSelection;
        overlay.MouseMove += MoveSelection;
        overlay.MouseLeftButtonUp += EndSelection;
        var previewButtons = new WrapPanel { Margin = new Thickness(0, 8, 0, 8) };
        if (VisualPreview && !HasMedia && !Cropping && !Compact)
        {
            compareButton = Button(L.T("查看效果"), async () =>
            {
                if (showingOriginal)
                { await UpdatePreview(); }
                else if (originalPreview != null)
                { player.Stop(); player.Visibility = Visibility.Collapsed; SetImage(originalPreview); showingOriginal = true; compareButton!.Content = L.T("查看效果"); status.Text = L.T("正在查看原始画面。"); UpdateCropVisibility(); }
            });
            previewButtons.Children.Add(compareButton);
        }
        if (previewButtons.Children.Count > 0)
            left.Children.Add(previewButtons);
        if (HasMedia)
        {
            previewHost.Background = Brushes.Black;
            transport = new MediaTransport(player, timeline, paths, Catalog.Category(paths[0]) == "audio", ProcessedPlayback && paths.Length == 1, PreparePlayback, closing.Token, text => status.Text = text);
            transport.ViewChanged += () => { player.Visibility = transport.HasVideo ? Visibility.Visible : Visibility.Hidden; image.Visibility = transport.HasVideo ? Visibility.Collapsed : Visibility.Visible; };
            if (TrimAudio)
            {
                previewHost.Visibility = Visibility.Hidden;
                previewHost.Height = 1;
            }
            else if (MediaCompact)
            {
                if (previewHost.Height > 0)
                {
                    left.Children.Remove(previewHost);
                    var stage = new Border { Child = previewHost, CornerRadius = new CornerRadius(12), Background = Brushes.Black, ClipToBounds = true };
                    stage.SizeChanged += (_, _) => stage.Clip = new RectangleGeometry(new Rect(0, 0, stage.ActualWidth, stage.ActualHeight), 12, 12);
                    left.Children.Add(stage);
                }
            }
            else
            {
                left.Children.Remove(previewHost);
                var surface = new StackPanel { Background = Brushes.Black };
                surface.Children.Add(previewHost);
                surface.Children.Add(transport.Controls);
                var mediaSurface = new Border { Child = surface, CornerRadius = new CornerRadius(10), Background = Brushes.Black, ClipToBounds = true };
                mediaSurface.SizeChanged += (_, _) => mediaSurface.Clip = new RectangleGeometry(new Rect(0, 0, mediaSurface.ActualWidth, mediaSurface.ActualHeight), 10, 10);
                left.Children.Add(mediaSurface);
                left.Children.Add(transport);
            }
            if (operation.Id is "trimVideo" or "trimAudio")
            {
                transport.RangeChanged += (first, last) => { syncingTrim = true; Set("start", first.ToString("0.000", CultureInfo.InvariantCulture)); Set("end", last.ToString("0.000", CultureInfo.InvariantCulture)); syncingTrim = false; transport.InvalidateEffect(); UpdateTrimSelection(); };
                transport.PlayStateChanged += RefreshTrimPlayButton;
            }
        }
        if (HasTimeline || HasMedia)
        {
            if (!HasMedia && !MediaCompact)
            { left.Children.Add(positionLabel); left.Children.Add(timeline); }
            System.Windows.Automation.AutomationProperties.SetName(timeline, L.T("播放位置（秒）"));
            timeline.ValueChanged += (_, _) => { if (loaded) { syncingPlayback = transport?.IsSynchronizing == true; Set("time", timeline.Value.ToString("0.000", CultureInfo.InvariantCulture)); syncingPlayback = false; if (LivePreview) SchedulePreview(); } };
            var frameButtons = new WrapPanel();
            if (operation.Id == "videoSnapshots")
            { frameButtons.Children.Add(Button(L.T("上一帧"), () => StepFrame(-1))); frameButtons.Children.Add(Button(L.T("下一帧"), () => StepFrame(1))); }
            if ((operation.Fields ?? []).Any(f => f.Name == "start"))
            { frameButtons.Children.Add(Button(L.T("设为开始"), () => { Set("start", timeline.Value.ToString("0.000", CultureInfo.InvariantCulture)); return Task.CompletedTask; })); frameButtons.Children.Add(Button(L.T("设为结束"), () => { Set("end", timeline.Value.ToString("0.000", CultureInfo.InvariantCulture)); return Task.CompletedTask; })); }
            if (!TrimAudio && !MediaCompact)
                left.Children.Add(frameButtons);
            if (transport != null)
                transport.ModeChanged += () => { frameButtons.IsEnabled = !transport.EffectSelected; UpdateCropVisibility(); };
        }
        StackPanel? rotationPanel = null;
        if (Rotating)
        {
            rotationPanel = new StackPanel { Margin = new Thickness(0, 8, 0, 4) };
            left.Children.Add(rotationPanel);
            if (operation.Id == "rotateVideo")
                left.Children.Add(new TextBlock { Text = L.T("保留完整时长和原有音轨；视频会重新编码。"), Foreground = UiTheme.Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 4) });
        }
        if (operation.Id == "cropVideo" && !MediaCompact)
            left.Children.Add(new TextBlock { Text = L.T("只裁剪画面；保留完整时长和原有音轨。"), Foreground = UiTheme.Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 4) });
        if (operation.Id == "redactPDF")
        {
            var pageButtons = new WrapPanel { Margin = new Thickness(0, 0, 0, 2) };
            pageButtons.Children.Add(Button(L.T("上一页"), () => { TurnPage(-1); return Task.CompletedTask; }));
            pageButtons.Children.Add(Button(L.T("下一页"), () => { TurnPage(1); return Task.CompletedTask; }));
            left.Children.Add(pageButtons);
        }
        if ((operation.Id is "redactImage" or "redactPDF") || (operation.Id == "redactVideo" && !MediaCompact))
        {
            string help = operation.Id == "redactPDF" ? L.T("在页面上拖动框选要遮盖的内容，可添加多个区域。遮盖后，框内的文字和图像会从文件中删除，而不只是被盖住。") : L.T("在预览上拖动选区，可添加多个打码区域。");
            left.Children.Add(new TextBlock { Text = help, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 6) });
            left.Children.Add(Button(L.T("清空覆盖区域"), () =>
            {
                regions.Clear();
                regionList.Items.Clear();
                Set("regions", "");
                selection.Visibility = Visibility.Collapsed;
                Set("width", "0");
                Set("height", "0");
                rangeEdited = false;
                if (originalPreview != null)
                { SetImage(originalPreview); showingOriginal = true; }
                status.Text = L.T("覆盖区域已清空。请拖动添加新区域。");
                return Task.CompletedTask;
            }));
            {
                left.Children.Add(regionList);
                regionList.SelectionChanged += (_, _) =>
                {
                    if (regionList.SelectedItem is ListBoxItem item && item.Tag is Dictionary<string, object> r)
                    {
                        syncingRegion = true; foreach (string key in new[] { "x", "y", "width", "height", "start", "end", "style", "blockSize" }) if (r.TryGetValue(key, out var value)) Set(key, value.ToString()!); syncingRegion = false;
                        // A PDF area belongs to a page: selecting it shows that page.
                        if (r.TryGetValue("page", out var page) && Get("pageNumber") != page.ToString()) Set("pageNumber", page.ToString()!);
                    }
                };
                var regionButtons = new WrapPanel();
                regionButtons.Children.Add(Button(L.T("移除选中区域"), () => { if (regionList.SelectedItem is ListBoxItem item && item.Tag is Dictionary<string, object> r) { regions.Remove(r); regionList.Items.Remove(item); if (regions.Count == 0) { Set("width", "0"); Set("height", "0"); rangeEdited = false; if (originalPreview != null) { SetImage(originalPreview); showingOriginal = true; } } else SchedulePreview(); } return Task.CompletedTask; }));
                left.Children.Add(regionButtons);
            }
        }
        if (operation.Id == "redactAudio" && !MediaCompact)
        {
            left.Children.Add(new TextBlock { Text = L.T("在波形上拖动选择时段；可添加多个蜂鸣范围。"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 6) });
            left.Children.Add(Button(L.T("清空蜂鸣范围"), () => { audioRanges.Clear(); regionList.Items.Clear(); Set("ranges", ""); rangeEdited = false; selection.Visibility = Visibility.Collapsed; status.Text = L.T("蜂鸣时段已清空。"); return Task.CompletedTask; }));
            left.Children.Add(regionList);
            left.Children.Add(Button(L.T("移除选中时段"), () => { if (regionList.SelectedItem is ListBoxItem item && item.Tag is Dictionary<string, double> r) { audioRanges.Remove(r); regionList.Items.Remove(item); rangeEdited = audioRanges.Count > 0; } return Task.CompletedTask; }));
            regionList.SelectionChanged += (_, _) => { if (regionList.SelectedItem is ListBoxItem item && item.Tag is Dictionary<string, double> r) { syncingRegion = true; Set("start", r["start"].ToString(CultureInfo.InvariantCulture)); Set("end", r["end"].ToString(CultureInfo.InvariantCulture)); syncingRegion = false; } };
        }
        if (operation.Id == "normalizeAudio" && !MediaCompact)
            left.Children.Add(Button(L.T("分析原始与处理后响度"), async () =>
        {
            using var busy = busyLine.Begin();
            try
            { status.Text = L.T("分析响度…"); var info = await Backend.Preview(Capture(), folder, closing.Token, "--analyze"); status.Text = L.F("原始 {0} LUFS · 处理后 {1} LUFS", info.GetProperty("Input").GetString(), info.GetProperty("Output").GetString()); }
            catch (Exception ex) { status.Text = L.T("分析：") + ex.Message; }
        }));
        if (!Cropping && !TrimAudio && !Compact && !MediaCompact)
            left.Children.Add(new ScrollViewer { Content = details, MaxHeight = 130, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        details.TextWrapping = TextWrapping.Wrap;
        details.Foreground = UiTheme.Muted;
        details.Margin = new Thickness(0, 8, 0, 8);
        if ((operation.Ordered || operation.Id == "organizePDF") && !MediaCompact)
        {
            left.Children.Add(order);
            var reorder = new WrapPanel();
            reorder.Children.Add(Button(L.T("上移"), () => { MoveItem(-1); return Task.CompletedTask; }));
            reorder.Children.Add(Button(L.T("下移"), () => { MoveItem(1); return Task.CompletedTask; }));
            reorder.Children.Add(Button(L.T("移出选择"), () => { if (order.SelectedIndex >= 0) order.Items.RemoveAt(order.SelectedIndex); return Task.CompletedTask; }));
            if (operation.Id == "organizePDF")
                reorder.Children.Add(Button(L.T("复制页"), () => { if (order.SelectedItem is ListBoxItem item) { int index = order.SelectedIndex; order.Items.Insert(index + 1, new ListBoxItem { Content = item.Content, Tag = item.Tag }); } return Task.CompletedTask; }));
            left.Children.Add(reorder);
            order.SelectionChanged += (_, _) => { if (loaded && VisualPreview) SchedulePreview(); };
        }
        FrameworkElement previewCard;
        if (TrimAudio)
        {
            var trimHost = new StackPanel();
            trimHost.Children.Add(left);
            previewCard = trimHost;
        }
        else if (Compact || MediaCompact)
        {
            previewCard = left;
        }
        else
            previewCard = UiTheme.Card(new ScrollViewer { Content = left, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, new Thickness(18));
        previewCard.Margin = Cropping || Rotating || TrimAudio || Compact || MediaCompact ? new Thickness(0) : new Thickness(0, 0, 16, 0);
        if (Compact || MediaCompact)
            Grid.SetRow(previewCard, 0);
        if (Simple && !VisualPreview)
        {
            previewCard.Visibility = Visibility.Collapsed;
            previewHost.Visibility = Visibility.Collapsed;
            busyLine.Visibility = Visibility.Collapsed;
        }
        body.Children.Add(previewCard);
        var parameters = new StackPanel();
        if (!Compact)
            parameters.Children.Add(new TextBlock { Text = L.T("处理参数"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 10) });
        var advanced = new StackPanel();
        string currentGroup = "";
        var frameSliders = new Dictionary<string, Grid>();
        foreach (var field in operation.Fields ?? [])
        {
            bool isFrameFieldInAdvanced = operation.Id == "frameImage" && field.Name is "canvasWidth" or "canvasHeight" or "background" or "gradient" or "backgroundImage";
            var fieldHost = (field.Kind == "json" || isFrameFieldInAdvanced) ? advanced : parameters;

            if (field.Kind == "slider")
            {
                var box = new TextBox { Text = field.Default, Visibility = Visibility.Collapsed };
                controls[field.Name] = box;
                fieldHost.Children.Add(box);

                string displayLabel = field.Label;
                if (operation.Id == "frameImage" && field.Name == "radius" && (displayLabel.Contains("Corner") || displayLabel.Contains("استدارة")))
                    displayLabel = L.Arabic ? "الزوايا" : "Corners";

                var row = new Grid { Margin = new Thickness(0, 3, 0, 3), FlowDirection = FlowDirection.LeftToRight };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(84) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });

                var label = new TextBlock
                {
                    Text = displayLabel,
                    FontSize = 12,
                    Foreground = UiTheme.Ink,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                Grid.SetColumn(label, 0);
                row.Children.Add(label);
                fieldLabels[field.Name] = label;

                var slider = new Slider
                {
                    Minimum = field.Min,
                    Maximum = field.Max,
                    SmallChange = field.Step,
                    LargeChange = field.Step * 5 > 0 ? field.Step * 5 : 1,
                    IsSnapToTickEnabled = false,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6, 0, 6, 0)
                };
                Grid.SetColumn(slider, 1);
                row.Children.Add(slider);

                var valueText = new TextBlock
                {
                    Width = 64,
                    TextAlignment = TextAlignment.Right,
                    FontSize = 12,
                    Foreground = UiTheme.Ink,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(valueText, 2);
                row.Children.Add(valueText);

                var resetBtn = new TextBlock
                {
                    Text = "↺",
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Foreground = UiTheme.Muted,
                    Width = 16,
                    Height = 16,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Cursor = Cursors.Hand,
                    Margin = new Thickness(4, 0, 0, 0)
                };
                Grid.SetColumn(resetBtn, 3);
                row.Children.Add(resetBtn);

                void UpdateResetState(double currentVal)
                {
                    bool isDef = false;
                    if (double.TryParse(field.Default, NumberStyles.Float, CultureInfo.InvariantCulture, out double defV))
                        isDef = Math.Abs(currentVal - defV) < 0.0001;
                    resetBtn.Opacity = isDef ? 0.35 : 1.0;
                    resetBtn.IsEnabled = !isDef;
                }

                resetBtn.MouseLeftButtonDown += (_, e) =>
                {
                    e.Handled = true;
                    if (resetBtn.IsEnabled)
                    {
                        Set(field.Name, field.Default);
                        ParameterChanged();
                    }
                };

                bool syncing = false;
                slider.ValueChanged += (_, _) =>
                {
                    if (syncing) return;
                    syncing = true;
                    try
                    {
                        string textVal = (field.Step < 1 && field.Step > 0)
                            ? slider.Value.ToString("0.00", CultureInfo.InvariantCulture)
                            : Math.Round(slider.Value).ToString(CultureInfo.InvariantCulture);
                        box.Text = textVal;
                        valueText.Text = FormatSliderValue(slider.Value, field);
                        UpdateResetState(slider.Value);
                        if (loaded)
                        {
                            editedField = field.Name;
                            ParameterChanged();
                        }
                    }
                    finally { syncing = false; }
                };

                box.TextChanged += (_, _) =>
                {
                    if (syncing) return;
                    syncing = true;
                    try
                    {
                        if (double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
                        {
                            slider.Value = Math.Clamp(v, slider.Minimum, slider.Maximum);
                            valueText.Text = FormatSliderValue(slider.Value, field);
                            UpdateResetState(slider.Value);
                        }
                    }
                    finally { syncing = false; }
                    if (loaded)
                    {
                        editedField = field.Name;
                        ParameterChanged();
                    }
                };

                if (double.TryParse(field.Default, NumberStyles.Float, CultureInfo.InvariantCulture, out double initV))
                    slider.Value = initV;
                valueText.Text = FormatSliderValue(slider.Value, field);
                UpdateResetState(slider.Value);

                syncSliderCallbacks.Add(() =>
                {
                    if (double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
                    {
                        slider.Value = Math.Clamp(v, slider.Minimum, slider.Maximum);
                        valueText.Text = FormatSliderValue(slider.Value, field);
                        UpdateResetState(slider.Value);
                    }
                });

                if (operation.Id == "frameImage")
                {
                    frameSliders[field.Name] = row;
                }
                else
                {
                    if (!string.IsNullOrEmpty(field.Group) && field.Group != currentGroup)
                    {
                        currentGroup = field.Group;
                        fieldHost.Children.Add(new TextBlock
                        {
                            Text = field.Group,
                            FontSize = 12,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = UiTheme.Ink,
                            Margin = new Thickness(0, 12, 0, 4)
                        });
                    }
                    fieldHost.Children.Add(row);
                }
                continue;
            }

            var fieldLabel = new TextBlock { Text = field.Label, Margin = new Thickness(0, 9, 0, 4), TextWrapping = TextWrapping.Wrap };
            if (Rotating && field.Name == "background" && Path.GetExtension(paths[0]).ToLowerInvariant() is ".jpg" or ".jpeg" or ".bmp")
                fieldLabel.Text = L.T("空白处颜色（留空为白色）");
            fieldLabels[field.Name] = fieldLabel;
            fieldHost.Children.Add(fieldLabel);
            FrameworkElement control;
            if (field.Name == "metadata")
                control = new MetadataEditor();
            else if (field.Kind == "bool")
                control = new CheckBox { IsChecked = field.Default == "true", Content = L.T("启用") };
            else if (field.Kind == "choice")
            {
                var combo = new ComboBox();
                foreach (string choice in field.Choices ?? [])
                    combo.Items.Add(new ComboBoxItem { Content = Choice(choice), Tag = choice });
                combo.SelectedIndex = Array.IndexOf(field.Choices ?? [], field.Default);
                control = combo;
            }
            else if (field.Kind == "password")
                control = new PasswordBox();
            else
            {
                var box = new TextBox { Text = TimeText(field.Name, field.Default), Padding = new Thickness(10, 8, 10, 8), AcceptsReturn = field.Kind == "json", TextWrapping = field.Kind == "json" ? TextWrapping.Wrap : TextWrapping.NoWrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
                if (IsTimeField(field.Name))
                {
                    box.Tag = box.Text;
                    // Leaving the box either shows the canonical m:ss.xx form or restores the last good value.
                    box.LostKeyboardFocus += (_, _) => CommitTimeField(box, field);
                }
                if (field.Kind == "json")
                { box.Height = 105; box.FontFamily = new FontFamily("Consolas"); }
                box.TextChanged += (_, _) => { if (loaded) { editedField = field.Name; if (new[] { "x", "y", "width", "height", "start", "end" }.Contains(field.Name)) rangeEdited = true; if (field.Name == "search") FilterMetadata(box.Text); else ParameterChanged(); } };
                // While a number is being typed the frame follows it; once the box is left, the box shows what the frame really is.
                if (Cropping && field.Name is "x" or "y" or "width" or "height")
                    box.LostKeyboardFocus += (_, _) => { if (loaded) WriteCropFields(); };
                control = box;
            }
            controls[field.Name] = control;
            fieldHost.Children.Add(control);
            System.Windows.Automation.AutomationProperties.SetName(control, field.Label);
            if (control is ComboBox choiceControl)
                choiceControl.SelectionChanged += (_, _) => { if (loaded && (VisualPreview || Rotating)) { editedField = field.Name; ParameterChanged(); } };
            if (control is CheckBox toggle && field.Name == "remove")
            { toggle.Checked += (_, _) => SetMetadataState(); toggle.Unchecked += (_, _) => SetMetadataState(); }
            if (field.Kind.StartsWith("file"))
            {
                var (pickLabel, pickFilter) = field.Kind switch
                {
                    "file:audio" => (L.T("选择音频…"), L.T("音频|*.mp3;*.m4a;*.wav;*.flac;*.ogg;*.opus;*.aiff;*.wma|所有文件|*.*")),
                    "file:subtitle" => (L.T("选择字幕…"), L.T("字幕|*.srt;*.ass;*.ssa;*.vtt|所有文件|*.*")),
                    _ => (L.T("选择图片…"), L.T("图片|*.jpg;*.jpeg;*.png;*.webp;*.bmp;*.heic;*.avif|所有文件|*.*"))
                };
                fieldHost.Children.Add(Button(pickLabel, () => { var dialog = new Microsoft.Win32.OpenFileDialog { Filter = pickFilter }; if (dialog.ShowDialog(host ?? this) == true) Set(field.Name, dialog.FileName); return Task.CompletedTask; }));
            }
        }

        if (operation.Id == "frameImage")
        {
            var bgRow = new Grid { Margin = new Thickness(0, 4, 0, 8), FlowDirection = FlowDirection.LeftToRight };
            bgRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(84) });
            bgRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var bgLabel = new TextBlock
            {
                Text = L.Arabic ? "الخلفية" : "Background",
                FontSize = 12,
                Foreground = UiTheme.Ink,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 4, 0, 0)
            };
            Grid.SetColumn(bgLabel, 0);
            bgRow.Children.Add(bgLabel);

            var swatchesContainer = new StackPanel { Orientation = Orientation.Vertical, FlowDirection = FlowDirection.LeftToRight };
            Grid.SetColumn(swatchesContainer, 1);
            bgRow.Children.Add(swatchesContainer);

            var row1Gradients = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            var row2Solids = new WrapPanel { Orientation = Orientation.Horizontal };
            swatchesContainer.Children.Add(row1Gradients);
            swatchesContainer.Children.Add(row2Solids);

            var swatchCheckers = new List<(Border Ring, Func<string, string, bool> Match)>();

            var gradients = new (string Start, string End)[]
            {
                ("#00E5FF", "#2979FF"),
                ("#FFB74D", "#FF7043"),
                ("#F48FB1", "#CE93D8"),
                ("#A5D6A7", "#26A69A"),
                ("#FFF59D", "#FFCA28"),
                ("#B0BEC5", "#546E7A"),
                ("#FF8A80", "#D50000"),
                ("#9FA8DA", "#3949AB")
            };

            foreach (var grad in gradients)
            {
                var startC = (Color)ColorConverter.ConvertFromString(grad.Start);
                var endC = (Color)ColorConverter.ConvertFromString(grad.End);
                var brush = new LinearGradientBrush(startC, endC, new Point(0, 0), new Point(1, 1));

                var ring = new Border
                {
                    Width = 22,
                    Height = 22,
                    CornerRadius = new CornerRadius(11),
                    BorderThickness = new Thickness(2),
                    BorderBrush = Brushes.Transparent,
                    Margin = new Thickness(0, 0, 2, 4),
                    Cursor = Cursors.Hand
                };
                var innerCircle = new Border
                {
                    Width = 18,
                    Height = 18,
                    CornerRadius = new CornerRadius(9),
                    Background = brush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                ring.Child = innerCircle;
                string s = grad.Start.ToLowerInvariant(), e = grad.End.ToLowerInvariant();
                ring.MouseLeftButtonDown += (_, ev) =>
                {
                    ev.Handled = true;
                    Set("background", s);
                    Set("gradient", e);
                    Set("backgroundImage", "");
                    syncFrameExtras?.Invoke();
                    if (loaded) ParameterChanged();
                };
                swatchCheckers.Add((ring, (bg, gr) => string.Equals(bg, s, StringComparison.OrdinalIgnoreCase) && string.Equals(gr, e, StringComparison.OrdinalIgnoreCase)));
                row1Gradients.Children.Add(ring);
            }

            var solids = new string[]
            {
                "#FFFFFF", "#F0EBFF", "#B39DDB", "#7C4DFF", "#1F3A5F", "#111111",
                "#EC407A", "#E53935", "#FF7043", "#FFA726", "#8D5A3B", "#2E7D32"
            };

            foreach (string hex in solids)
            {
                var c = (Color)ColorConverter.ConvertFromString(hex);
                var brush = new SolidColorBrush(c);
                var ring = new Border
                {
                    Width = 22,
                    Height = 22,
                    CornerRadius = new CornerRadius(11),
                    BorderThickness = new Thickness(2),
                    BorderBrush = Brushes.Transparent,
                    Margin = new Thickness(0, 0, 2, 4),
                    Cursor = Cursors.Hand
                };
                var innerCircle = new Border
                {
                    Width = 18,
                    Height = 18,
                    CornerRadius = new CornerRadius(9),
                    Background = brush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                ring.Child = innerCircle;
                string h = hex.ToLowerInvariant();
                ring.MouseLeftButtonDown += (_, ev) =>
                {
                    ev.Handled = true;
                    Set("background", h);
                    Set("gradient", "");
                    Set("backgroundImage", "");
                    syncFrameExtras?.Invoke();
                    if (loaded) ParameterChanged();
                };
                swatchCheckers.Add((ring, (bg, gr) => string.Equals(bg, h, StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(gr)));
                row2Solids.Children.Add(ring);
            }

            var imageBtn = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                BorderThickness = new Thickness(1),
                BorderBrush = UiTheme.Muted,
                Background = UiTheme.Tint,
                Margin = new Thickness(0, 0, 4, 4),
                Cursor = Cursors.Hand,
                ToolTip = L.Arabic ? "اختيار صورة خلفية…" : "Choose Background Image…"
            };
            var imagePath = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M 2,4 L 18,4 L 18,16 L 2,16 Z M 5,13 L 9,8 L 13,13 Z M 14,9 A 1.5,1.5 0 1 0 14,6 A 1.5,1.5 0 1 0 14,9 Z"),
                Stroke = UiTheme.Ink,
                StrokeThickness = 1.2,
                Width = 14,
                Height = 12,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            imageBtn.Child = imagePath;
            imageBtn.MouseLeftButtonDown += (_, ev) =>
            {
                ev.Handled = true;
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = L.T("图片|*.jpg;*.jpeg;*.png;*.webp;*.bmp;*.heic;*.avif|所有文件|*.*")
                };
                if (dialog.ShowDialog(host ?? this) == true)
                {
                    Set("backgroundImage", dialog.FileName);
                    syncFrameExtras?.Invoke();
                    if (loaded) ParameterChanged();
                }
            };
            row2Solids.Children.Add(imageBtn);

            var ratioRow = new Grid { Margin = new Thickness(0, 6, 0, 4), FlowDirection = FlowDirection.LeftToRight };
            ratioRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(84) });
            ratioRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var ratioLabel = new TextBlock
            {
                Text = L.Arabic ? "النسبة" : "Ratio",
                FontSize = 12,
                Foreground = UiTheme.Ink,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(ratioLabel, 0);
            ratioRow.Children.Add(ratioLabel);

            var ratioControl = new SegmentedControl(new (string Value, string Label)[]
            {
                ("auto", L.Arabic ? "تلقائي" : "Auto"),
                ("1:1", "1:1"),
                ("16:9", "16:9"),
                ("4:5", "4:5")
            })
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                MinWidth = 200
            };
            Grid.SetColumn(ratioControl, 1);
            ratioRow.Children.Add(ratioControl);

            ratioControl.Chosen += val =>
            {
                double pad = 80;
                if (double.TryParse(Get("padding", "80"), NumberStyles.Float, CultureInfo.InvariantCulture, out double p))
                    pad = p;

                if (val == "1:1")
                {
                    Set("canvasWidth", "1400");
                    Set("canvasHeight", "1400");
                }
                else if (val == "16:9")
                {
                    Set("canvasWidth", "1600");
                    Set("canvasHeight", "900");
                }
                else if (val == "4:5")
                {
                    Set("canvasWidth", "1200");
                    Set("canvasHeight", "1500");
                }
                else if (val == "auto")
                {
                    if (originalWidth > 0 && originalHeight > 0)
                    {
                        Set("canvasWidth", ((int)Math.Round(originalWidth + 2 * pad)).ToString(CultureInfo.InvariantCulture));
                        Set("canvasHeight", ((int)Math.Round(originalHeight + 2 * pad)).ToString(CultureInfo.InvariantCulture));
                    }
                }
                editedField = "canvasWidth";
                syncFrameExtras?.Invoke();
                ParameterChanged();
            };

            syncFrameExtras = () =>
            {
                string curBg = Get("background").Trim();
                string curGrad = Get("gradient").Trim();
                string curImg = Get("backgroundImage").Trim();

                bool hasImage = !string.IsNullOrEmpty(curImg);
                imageBtn.BorderBrush = hasImage ? UiTheme.Accent : UiTheme.Muted;
                imageBtn.BorderThickness = hasImage ? new Thickness(2) : new Thickness(1);

                foreach (var (ring, match) in swatchCheckers)
                {
                    bool sel = !hasImage && match(curBg, curGrad);
                    ring.BorderBrush = sel ? UiTheme.Accent : Brushes.Transparent;
                }

                string w = Get("canvasWidth");
                string h = Get("canvasHeight");
                if (w == "1400" && h == "1400") ratioControl.Selected = "1:1";
                else if (w == "1600" && h == "900") ratioControl.Selected = "16:9";
                else if (w == "1200" && h == "1500") ratioControl.Selected = "4:5";
                else
                {
                    double pad = 80;
                    if (double.TryParse(Get("padding", "80"), NumberStyles.Float, CultureInfo.InvariantCulture, out double p)) pad = p;
                    if (originalWidth > 0 && originalHeight > 0 &&
                        w == ((int)Math.Round(originalWidth + 2 * pad)).ToString() &&
                        h == ((int)Math.Round(originalHeight + 2 * pad)).ToString())
                        ratioControl.Selected = "auto";
                    else
                        ratioControl.Selected = null;
                }
            };
            syncFrameExtras();

            parameters.Children.Clear();
            parameters.Children.Add(bgRow);
            if (frameSliders.TryGetValue("padding", out var pRow)) parameters.Children.Add(pRow);
            if (frameSliders.TryGetValue("radius", out var rRow)) parameters.Children.Add(rRow);
            if (frameSliders.TryGetValue("shadow", out var sRow)) parameters.Children.Add(sRow);
            if (frameSliders.TryGetValue("backgroundBlur", out var bRow)) parameters.Children.Add(bRow);
            parameters.Children.Add(ratioRow);
            if (advanced.Children.Count > 0)
                parameters.Children.Add(new Expander { Header = L.T("高级参数（可选）"), Content = advanced, Margin = new Thickness(0, 10, 0, 0), IsExpanded = false });
        }
        void ShowField(string name, bool visible)
        {
            if (controls.TryGetValue(name, out var shown))
                shown.Visibility = fieldLabels[name].Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }
        void WhenChanged(string name, Action update)
        {
            ((ComboBox)controls[name]).SelectionChanged += (_, _) => update();
            update();
        }
        if (operation.Id == "resizeImage")
            WhenChanged("mode", () => { string mode = Get("mode", "percent"); ShowField("percent", mode == "percent"); ShowField("edge", mode == "edge"); foreach (string name in new[] { "width", "height", "keepRatio" }) ShowField(name, mode == "size"); });
        if (operation.Id == "watermark")
            WhenChanged("layout", () => { bool tiled = Get("layout", "single") == "tiled"; ShowField("position", !tiled); ShowField("margin", !tiled); });
        if (operation.Id == "pdfPassword")
            WhenChanged("mode", () => ShowField("newPassword", Get("mode", "add") == "add"));
        if (operation.Id == "createAnimation")
            WhenChanged("format", () => ShowField("loop", Get("format", "gif") == "gif"));
        if (operation.Id == "location")
        {
            // KwekDrop never goes online, so there is no map here: coordinates are pasted from a map app, or taken from another photo.
            var note = new TextBlock { Text = L.T("在地图应用中右键一个地点即可复制坐标，例如 31.2304, 121.4737。也可以选一张在那里拍的照片。"), Foreground = UiTheme.Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) };
            parameters.Children.Add(note);
            WhenChanged("mode", () =>
            {
                bool set = Get("mode", "strip") == "set";
                ShowField("coordinates", set); ShowField("locationSource", set);
                note.Visibility = set ? Visibility.Visible : Visibility.Collapsed;
                foreach (var pick in parameters.Children.OfType<Button>().Where(b => (string)b.Content == L.T("选择图片…")))
                    pick.Visibility = note.Visibility;
            });
        }
        if (operation.Id == "compress" && controls.ContainsKey("targetKB"))
        {
            // Common upload limits; a click fills the size target in.
            var limits = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            foreach (int megabytes in new[] { 8, 25, 50, 100 })
                limits.Children.Add(Button(megabytes + " MB", () => { Set("targetKB", (megabytes * 1024).ToString()); return Task.CompletedTask; }));
            limits.Children.Add(Button(L.T("不限制"), () => { Set("targetKB", "0"); return Task.CompletedTask; }));
            parameters.Children.Insert(parameters.Children.IndexOf(controls["targetKB"]) + 1, limits);
        }
        if (Presets.Supported(operation.Id) && !Compact)
            AddPresetBar(parameters);
        if (Rotating)
        {
            var angleBox = (TextBox)controls["angle"];
            parameters.Children.Remove(angleBox); parameters.Children.Remove(fieldLabels["angle"]);
            angleBox.Width = 76; angleBox.HorizontalContentAlignment = HorizontalAlignment.Center;
            System.Windows.Automation.AutomationProperties.SetName(angleBox, RotationDial.Caption);
            var heading = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 8) };
            var value = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            value.Children.Add(angleBox); value.Children.Add(new TextBlock { Text = "°", Margin = new Thickness(5, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = UiTheme.Muted });
            DockPanel.SetDock(value, Dock.Right); heading.Children.Add(value);
            Task Turn(int quarters)
            {
                _ = double.TryParse(Get("angle", "0"), NumberStyles.Float, CultureInfo.InvariantCulture, out double angle);
                Set("angle", RotationDial.Signed(angle + 90 * quarters).ToString("0.#", CultureInfo.InvariantCulture)); return Task.CompletedTask;
            }
            var turnButtons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 0, 8, 0) };
            turnButtons.Children.Add(Button(L.T("↺ 向左转 90°"), () => Turn(-1)));
            turnButtons.Children.Add(Button(L.T("↻ 向右转 90°"), () => Turn(1)));
            DockPanel.SetDock(turnButtons, Dock.Right); heading.Children.Add(turnButtons);
            heading.Children.Add(new TextBlock { Text = RotationDial.Caption, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.Medium });
            rotationPanel!.Children.Add(heading);
            var dial = new RotationDial(); rotationPanel.Children.Add(dial);
            rotationPanel.Children.Add(new TextBlock { Text = RotationDial.Help, Foreground = UiTheme.Muted, FontSize = 10, Margin = new Thickness(0, 6, 0, 0), TextAlignment = TextAlignment.Center });
            bool syncingAngle = false;
            void ShowCornerFields()
            {
                _ = double.TryParse(Get("angle", "0"), NumberStyles.Float, CultureInfo.InvariantCulture, out double angle);
                var visible = angle % 90 != 0 ? Visibility.Visible : Visibility.Collapsed;
                foreach (string name in new[] { "expand", "background" })
                    controls[name].Visibility = fieldLabels[name].Visibility = name == "background" && Get("expand") == "crop" ? Visibility.Collapsed : visible;
            }
            dial.ValueChanged += (_, _) => { if (!syncingAngle) { syncingAngle = true; angleBox.Text = dial.Value.ToString("0.#", CultureInfo.InvariantCulture); syncingAngle = false; } };
            angleBox.TextChanged += (_, _) =>
            {
                if (!syncingAngle && double.TryParse(angleBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double typed) && typed is >= -180 and <= 180)
                { syncingAngle = true; dial.Value = typed; syncingAngle = false; }
                ShowCornerFields();
            };
            angleBox.LostKeyboardFocus += (_, _) =>
            {
                if (!double.TryParse(angleBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double typed) || !double.IsFinite(typed)) typed = dial.Value;
                angleBox.Text = Math.Clamp(Math.Round(typed, 1), -180, 180).ToString("0.#", CultureInfo.InvariantCulture);
            };
            ShowCornerFields();
            ((ComboBox)controls["expand"]).SelectionChanged += (_, _) => ShowCornerFields();
        }
        if (operation.Id is "redactImage" or "redactVideo")
        {
            void UpdateRedactionFields()
            { bool mosaic = Get("style") == "pixelate"; controls["blockSize"].Visibility = fieldLabels["blockSize"].Visibility = mosaic ? Visibility.Visible : Visibility.Collapsed; if (controls.TryGetValue("color", out var color)) color.Visibility = fieldLabels["color"].Visibility = Get("style") == "solid" ? Visibility.Visible : Visibility.Collapsed; }
            ((ComboBox)controls["style"]).SelectionChanged += (_, _) => UpdateRedactionFields();
            UpdateRedactionFields();
        }
        if (controls.ContainsKey("ratio"))
        {
            var customRatio = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
            customRatio.Children.Add(new TextBlock { Text = L.T("自定义比例（宽 : 高）"), Margin = new Thickness(0, 0, 0, 6) });
            var inputs = new Grid();
            inputs.ColumnDefinitions.Add(new ColumnDefinition());
            inputs.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
            inputs.ColumnDefinitions.Add(new ColumnDefinition());
            foreach (string key in new[] { "ratioWidth", "ratioHeight" })
            { parameters.Children.Remove(controls[key]); parameters.Children.Remove(fieldLabels[key]); }
            inputs.Children.Add(controls["ratioWidth"]);
            Grid.SetColumn(controls["ratioHeight"], 2);
            inputs.Children.Add(controls["ratioHeight"]);
            var separator = new TextBlock { Text = ":", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(separator, 1);
            inputs.Children.Add(separator);
            customRatio.Children.Add(inputs);
            parameters.Children.Add(customRatio);
            void ShowCustomRatio() => customRatio.Visibility = Get("ratio") == "custom" ? Visibility.Visible : Visibility.Collapsed;
            ((ComboBox)controls["ratio"]).SelectionChanged += (_, _) => ShowCustomRatio();
            ShowCustomRatio();
            if (Cropping && !MediaCompact)
            {
                parameters.Children.Clear();
                var cropPanel = new StackPanel();

                var ratioRow = new Grid { Margin = new Thickness(0, 0, 0, 10) };
                ratioRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                ratioRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
                ratioRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var ratioLabel = fieldLabels["ratio"];
                ratioLabel.Margin = new Thickness(0);
                ratioLabel.VerticalAlignment = VerticalAlignment.Center;
                ratioRow.Children.Add(ratioLabel);

                var ratioItems = new (string Value, string Label)[]
                {
                    ("free", L.Arabic ? "حر" : "Free"),
                    ("1:1", "1:1"),
                    ("16:9", "16:9"),
                    ("9:16", "9:16"),
                    ("4:3", "4:3"),
                    ("3:4", "3:4")
                };
                var segmented = new SegmentedControl(ratioItems)
                {
                    HorizontalAlignment = HorizontalAlignment.Right,
                    MinWidth = 280
                };
                Grid.SetColumn(segmented, 2);
                ratioRow.Children.Add(segmented);

                var ratioCombo = (ComboBox)controls["ratio"];
                ratioCombo.Visibility = Visibility.Collapsed;
                ratioRow.Children.Add(ratioCombo);

                segmented.Chosen += val =>
                {
                    Set("ratio", val);
                    editedField = "ratio";
                    ParameterChanged();
                };
                ratioCombo.SelectionChanged += (_, _) =>
                {
                    string cur = Get("ratio");
                    segmented.Selected = cur == "custom" ? null : cur;
                };
                string initRatio = Get("ratio", "free");
                segmented.Selected = initRatio == "custom" ? null : initRatio;

                cropPanel.Children.Add(ratioRow);

                var whRow = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    FlowDirection = FlowDirection.LeftToRight,
                    Margin = new Thickness(0, 0, 0, 10),
                    VerticalAlignment = VerticalAlignment.Center
                };

                var widthBox = (TextBox)controls["width"];
                var heightBox = (TextBox)controls["height"];
                widthBox.Width = 70;
                widthBox.MinWidth = 0;
                widthBox.MinHeight = 28;
                widthBox.Padding = new Thickness(6, 4, 6, 4);
                widthBox.FontSize = 12;
                widthBox.VerticalAlignment = VerticalAlignment.Center;
                widthBox.HorizontalContentAlignment = HorizontalAlignment.Center;

                heightBox.Width = 70;
                heightBox.MinWidth = 0;
                heightBox.MinHeight = 28;
                heightBox.Padding = new Thickness(6, 4, 6, 4);
                heightBox.FontSize = 12;
                heightBox.VerticalAlignment = VerticalAlignment.Center;
                heightBox.HorizontalContentAlignment = HorizontalAlignment.Center;

                whRow.Children.Add(new TextBlock { Text = "W", Foreground = UiTheme.Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
                whRow.Children.Add(widthBox);
                whRow.Children.Add(new TextBlock { Text = "H", Foreground = UiTheme.Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 8, 0) });
                whRow.Children.Add(heightBox);
                whRow.Children.Add(new TextBlock { Text = "px", Foreground = UiTheme.Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) });

                cropPanel.Children.Add(whRow);

                cropSizeLabel = new TextBlock { Visibility = Visibility.Collapsed };
                cropPanel.Children.Add(cropSizeLabel);

                cropWidthValue = new TextBlock { FontSize = 12, Foreground = UiTheme.Muted, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
                cropHeightValue = new TextBlock { FontSize = 12, Foreground = UiTheme.Muted, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
                var hiddenSliders = new StackPanel { Visibility = Visibility.Collapsed };
                hiddenSliders.Children.Add(CropSliderRow("width", cropWidthValue));
                hiddenSliders.Children.Add(CropSliderRow("height", cropHeightValue));
                cropPanel.Children.Add(hiddenSliders);

                var exact = new StackPanel();
                customRatio.Margin = new Thickness(0, 4, 0, 8);
                exact.Children.Add(customRatio);
                foreach (string name in new[] { "x", "y" })
                {
                    fieldLabels[name].Margin = new Thickness(0, 8, 0, 4);
                    exact.Children.Add(fieldLabels[name]);
                    exact.Children.Add(controls[name]);
                }
                cropPanel.Children.Add(new Expander { Header = L.T("高级参数（可选）"), Content = exact, IsExpanded = false, Margin = new Thickness(0, 10, 0, 0) });

                cropPanel.Children.Add(new TextBlock { Text = L.T("拖动裁剪框移动位置，拖动边角或边缘调整大小；滚动滚轮缩放预览。"), Foreground = UiTheme.Muted, TextWrapping = TextWrapping.Wrap, FontSize = 11, Margin = new Thickness(0, 8, 0, 0) });
                parameters.Children.Add(cropPanel);
            }
        }
        if (operation.Id != "frameImage" && advanced.Children.Count > 0)
            parameters.Children.Add(new Expander { Header = L.T("高级参数（可选）"), Content = advanced, Margin = new Thickness(0, 16, 0, 0) });
        if ((operation.Fields?.Length ?? 0) == 0)
            parameters.Children.Add(new TextBlock { Text = operation.Ordered ? L.T("在左侧调整文件顺序，然后保存。") : L.T("此操作无需参数。预览确认后保存新文件。"), Foreground = UiTheme.Muted, TextWrapping = TextWrapping.Wrap });
        if (operation.Id == "trimVideo")
        {
            parameters.Children.Clear();
            var trimPanel = new StackPanel { FlowDirection = FlowDirection.LeftToRight };
            var trimHeader = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 8, 0, 6) };
            trimSelected = new TextBlock { Foreground = UiTheme.Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, FontFamily = new FontFamily("Cascadia Mono, Consolas") };
            DockPanel.SetDock(trimSelected, Dock.Right);
            trimHeader.Children.Add(trimSelected);
            trimHeader.Children.Add(new TextBlock { Text = L.T("Drag the ends to trim"), Foreground = UiTheme.Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center });
            trimPanel.Children.Add(trimHeader);

            var rail = transport!.TakeRail();
            rail.Height = 64;
            rail.RangeEnabled = true;
            var waveCard = new Border { Background = new SolidColorBrush(Color.FromRgb(19, 21, 26)), CornerRadius = new CornerRadius(8), Child = rail };
            trimPanel.Children.Add(waveCard);

            var timesRow = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 8, 0, 4) };
            var startField = new TimeField(0, val =>
            {
                double last = double.TryParse(Get("end", "0"), NumberStyles.Float, CultureInfo.InvariantCulture, out double ev) && ev > 0 ? ev : totalDuration;
                double clamped = Math.Clamp(val, 0, Math.Max(0, last - 0.1));
                Set("start", clamped.ToString("0.000", CultureInfo.InvariantCulture));
                transport.Seek(clamped);
                UpdateTrimSelection();
            });
            var endField = new TimeField(totalDuration, val =>
            {
                double first = double.TryParse(Get("start", "0"), NumberStyles.Float, CultureInfo.InvariantCulture, out double sv) ? sv : 0;
                double clamped = Math.Clamp(val, first + 0.1, totalDuration > 0 ? totalDuration : 10000);
                Set("end", clamped.ToString("0.000", CultureInfo.InvariantCulture));
                transport.Seek(clamped);
                UpdateTrimSelection();
            });

            var startBoxStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            startBoxStack.Children.Add(new TextBlock { Text = L.T("Start"), Foreground = UiTheme.Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
            startBoxStack.Children.Add(startField);
            DockPanel.SetDock(startBoxStack, Dock.Left);
            timesRow.Children.Add(startBoxStack);

            var endBoxStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            endBoxStack.Children.Add(new TextBlock { Text = L.T("End"), Foreground = UiTheme.Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
            endBoxStack.Children.Add(endField);
            DockPanel.SetDock(endBoxStack, Dock.Right);
            timesRow.Children.Add(endBoxStack);
            trimPanel.Children.Add(timesRow);

            transport.RangeChanged += (first, last) =>
            {
                startField.SetValue(first);
                endField.SetValue(last);
                UpdateTrimSelection();
            };

            transport.DurationChanged += length => { if (length > 0) { endField.SetValue(length); UpdateTrimSelection(); } };
            var compactDock = transport.BuildCompactDock(stepButtons: true);
            compactDock.Margin = new Thickness(0, 4, 0, 0);
            trimPanel.Children.Add(compactDock);

            parameters.Children.Add(trimPanel);
        }
        else if (operation.Id == "cropVideo")
        {
            parameters.Children.Clear();
            var cropPanel = new StackPanel { FlowDirection = FlowDirection.LeftToRight };

            var ratioRow = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 8, 0, 8) };
            var ratioLabel = new TextBlock { Text = L.T("Aspect ratio"), FontSize = 12, Foreground = UiTheme.Muted, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(ratioLabel, Dock.Left);
            ratioRow.Children.Add(ratioLabel);

            var ratioControl = new SegmentedControl(new (string Value, string Label)[]
            {
                ("free", L.T("Free")),
                ("1:1", "1:1"),
                ("16:9", "16:9"),
                ("9:16", "9:16"),
                ("4:3", "4:3"),
                ("3:4", "3:4")
            }) { MinWidth = 280, HorizontalAlignment = HorizontalAlignment.Right };
            DockPanel.SetDock(ratioControl, Dock.Right);
            ratioRow.Children.Add(ratioControl);
            cropPanel.Children.Add(ratioRow);

            ratioControl.Chosen += val =>
            {
                Set("ratio", val);
                editedField = "ratio";
                ParameterChanged();
            };

            var sizeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8), VerticalAlignment = VerticalAlignment.Center };
            var widthBox = (TextBox)controls["width"];
            var heightBox = (TextBox)controls["height"];
            widthBox.Width = 70; widthBox.Height = 26; widthBox.Padding = new Thickness(4, 2, 4, 2); widthBox.FontSize = 12; widthBox.TextAlignment = TextAlignment.Right;
            heightBox.Width = 70; heightBox.Height = 26; heightBox.Padding = new Thickness(4, 2, 4, 2); heightBox.FontSize = 12; heightBox.TextAlignment = TextAlignment.Right;

            sizeRow.Children.Add(new TextBlock { Text = L.T("W"), Foreground = UiTheme.Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
            sizeRow.Children.Add(widthBox);
            sizeRow.Children.Add(new TextBlock { Text = L.T("H"), Foreground = UiTheme.Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 6, 0) });
            sizeRow.Children.Add(heightBox);
            sizeRow.Children.Add(new TextBlock { Text = "px", Foreground = UiTheme.Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) });
            cropPanel.Children.Add(sizeRow);

            timeline.Margin = new Thickness(0, 0, 0, 4);
            cropPanel.Children.Add(timeline);

            var transportRow = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 4) };
            var stepButtons = new StackPanel { Orientation = Orientation.Horizontal };
            Button PlainIcon(string glyph, Action act, string tip)
            {
                var b = new Button
                {
                    Width = 28, Height = 28, Background = new SolidColorBrush(Color.FromArgb(0x14, 0, 0, 0)),
                    BorderThickness = new Thickness(0), Cursor = Cursors.Hand, ToolTip = tip, Margin = new Thickness(0, 0, 6, 0),
                    Content = new TextBlock { Text = glyph, FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets, Segoe UI Symbol"), FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                };
                b.Click += (_, _) => act();
                return b;
            }
            stepButtons.Children.Add(PlainIcon("", () => _ = StepFrame(-1), L.T("上一帧")));
            stepButtons.Children.Add(PlainIcon("", () => _ = StepFrame(1), L.T("下一帧")));
            DockPanel.SetDock(stepButtons, Dock.Left);
            transportRow.Children.Add(stepButtons);

            var timeIndicator = new TextBlock { FontSize = 11, Foreground = UiTheme.Muted, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, FontFamily = new FontFamily("Cascadia Mono, Consolas") };
            DockPanel.SetDock(timeIndicator, Dock.Right);
            transportRow.Children.Add(timeIndicator);

            void UpdateTimeIndicator()
            {
                timeIndicator.Text = $"{TimeField.FormatTime(timeline.Value)} / {TimeField.FormatTime(totalDuration > 0 ? totalDuration : timeline.Maximum)}";
            }
            timeline.ValueChanged += (_, _) =>
            {
                UpdateTimeIndicator();
                if (transport != null && loaded) transport.Seek(timeline.Value);
            };
            UpdateTimeIndicator();
            cropPanel.Children.Add(transportRow);

            parameters.Children.Add(cropPanel);
        }
        else if (operation.Id == "changeVideoSpeed")
        {
            parameters.Children.Clear();
            var speedPanel = new StackPanel { FlowDirection = FlowDirection.LeftToRight };

            var speedHeader = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 8, 0, 2) };
            var speedLabel = new TextBlock { Text = L.T("Playback speed"), FontSize = 12, Foreground = UiTheme.Ink, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(speedLabel, Dock.Left);
            speedHeader.Children.Add(speedLabel);

            var speedValueText = new TextBlock { Text = "1×", FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = UiTheme.Ink, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, FontFamily = new FontFamily("Cascadia Mono, Consolas") };
            DockPanel.SetDock(speedValueText, Dock.Right);
            speedHeader.Children.Add(speedValueText);
            speedPanel.Children.Add(speedHeader);

            double[] marks = [0.25, 0.5, 1.0, 1.5, 2.0, 4.0];
            double SpeedAt(double pos)
            {
                var idx = Math.Clamp(pos, 0, marks.Length - 1);
                var lower = (int)Math.Floor(idx);
                var upper = Math.Min(lower + 1, marks.Length - 1);
                var val = marks[lower] + (marks[upper] - marks[lower]) * (idx - lower);
                foreach (var m in marks) if (Math.Abs(m - val) < 0.04) val = m;
                return Math.Round(val * 100) / 100;
            }
            double PositionOf(double spd)
            {
                for (int i = 0; i < marks.Length - 1; i++)
                {
                    if (spd >= marks[i] && spd <= marks[i + 1])
                        return i + (spd - marks[i]) / (marks[i + 1] - marks[i]);
                }
                return 2.0;
            }

            var speedSlider = new Slider
            {
                Minimum = 0, Maximum = 5, Value = 2.0, SmallChange = 0.05, LargeChange = 1.0,
                IsMoveToPointEnabled = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 4, 0, 0)
            };
            speedPanel.Children.Add(speedSlider);

            var marksGrid = new Grid { Height = 18, Margin = new Thickness(0, 2, 0, 6) };
            var markBlocks = new List<(double Speed, TextBlock Block)>();
            for (int i = 0; i < marks.Length; i++)
            {
                marksGrid.ColumnDefinitions.Add(new ColumnDefinition());
                double m = marks[i];
                var tb = new TextBlock
                {
                    Text = $"{m.ToString("0.##", CultureInfo.InvariantCulture)}×",
                    FontSize = 10, Foreground = UiTheme.Muted, Cursor = Cursors.Hand,
                    HorizontalAlignment = i == 0 ? HorizontalAlignment.Left : i == marks.Length - 1 ? HorizontalAlignment.Right : HorizontalAlignment.Center
                };
                int capturedIndex = i;
                tb.MouseLeftButtonDown += (_, _) => speedSlider.Value = capturedIndex;
                Grid.SetColumn(tb, i);
                marksGrid.Children.Add(tb);
                markBlocks.Add((m, tb));
            }
            speedPanel.Children.Add(marksGrid);

            var speedTransport = transport!.BuildCompactDock(stepButtons: false);
            speedTransport.Margin = new Thickness(0, 2, 0, 4);
            speedPanel.Children.Add(speedTransport);

            var speedCaption = new TextBlock
            {
                Foreground = UiTheme.Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 6)
            };
            speedPanel.Children.Add(speedCaption);

            void UpdateSpeedUI(double spd)
            {
                speedValueText.Text = $"{spd.ToString("0.##", CultureInfo.InvariantCulture)}×";
                foreach (var (m, tb) in markBlocks)
                {
                    bool active = Math.Abs(m - spd) < 0.01;
                    tb.FontWeight = active ? FontWeights.Bold : FontWeights.Normal;
                    tb.Foreground = active ? UiTheme.Ink : UiTheme.Muted;
                }
                double newDuration = totalDuration > 0 ? (totalDuration / spd) : 0;
                speedCaption.Text = L.F("Audio keeps its pitch. The new length is {0}.", TimeField.FormatTime(newDuration));
                Set("speed", spd.ToString("0.##", CultureInfo.InvariantCulture));
                if (loaded) saveButton.IsEnabled = Math.Abs(spd - 1.0) > 0.001;
                try { player.SpeedRatio = spd; } catch { }
            }

            speedSlider.ValueChanged += (_, _) =>
            {
                double spd = SpeedAt(speedSlider.Value);
                UpdateSpeedUI(spd);
            };
            UpdateSpeedUI(1.0);

            parameters.Children.Add(speedPanel);
        }
        else if (operation.Id == "splitVideo")
        {
            parameters.Children.Clear();
            var splitPanel = new StackPanel { FlowDirection = FlowDirection.LeftToRight };
            var splitPoints = new List<double>();

            var subHeader = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 8, 0, 6) };
            var countLabel = new TextBlock { Text = L.F("{0} split points", 0), Foreground = UiTheme.Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(countLabel, Dock.Left);
            subHeader.Children.Add(countLabel);

            var addSplitBtn = Button("+ " + L.T("Add Split"), () =>
            {
                double t = timeline.Value;
                if (t > 0.05 && t < totalDuration - 0.05 && splitPoints.All(p => Math.Abs(p - t) >= 0.05))
                {
                    splitPoints.Add(t);
                    splitPoints.Sort();
                    countLabel.Text = L.F("{0} split points", splitPoints.Count);
                    Set("splitPoints", string.Join(",", splitPoints.Select(p => p.ToString("0.000", CultureInfo.InvariantCulture))));
                    transport?.Rail.InvalidateVisual();
                }
                return Task.CompletedTask;
            });
            addSplitBtn.Padding = new Thickness(10, 4, 10, 4);
            addSplitBtn.FontSize = 11;
            addSplitBtn.HorizontalAlignment = HorizontalAlignment.Right;
            DockPanel.SetDock(addSplitBtn, Dock.Right);
            subHeader.Children.Add(addSplitBtn);
            splitPanel.Children.Add(subHeader);

            var rail = transport!.TakeRail();
            rail.Height = 56;
            rail.Overlay = (dc, w, h) =>
            {
                foreach (var p in splitPoints)
                {
                    double x = rail.XAt(p);
                    dc.DrawRoundedRectangle(Brushes.White, new Pen(UiTheme.Accent, 1), new Rect(x - 1.5, 0, 3, h), 1.5, 1.5);
                }
            };
            var railBorder = new Border { Background = new SolidColorBrush(Color.FromRgb(19, 21, 26)), CornerRadius = new CornerRadius(8), Child = rail };
            splitPanel.Children.Add(railBorder);

            var transportBar = transport.BuildCompactDock(stepButtons: true);
            transportBar.Margin = new Thickness(0, 4, 0, 6);
            splitPanel.Children.Add(transportBar);

            var divider = new Border { Height = 1, Background = UiTheme.Hairline, Margin = new Thickness(0, 4, 0, 8) };
            splitPanel.Children.Add(divider);

            var equalRow = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 0, 0, 6) };
            var equalLabel = new TextBlock { Text = L.T("Equal clips"), FontSize = 12, Foreground = UiTheme.Ink, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(equalLabel, Dock.Left);
            equalRow.Children.Add(equalLabel);

            var stepperStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            int equalCount = 2;
            var equalText = new TextBlock { Text = "2", Width = 28, TextAlignment = TextAlignment.Center, FontSize = 13, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            Button PlainIcon(string glyph, Action act, string tip)
            {
                var b = new Button
                {
                    Width = 24, Height = 24, Background = new SolidColorBrush(Color.FromArgb(0x14, 0, 0, 0)),
                    BorderThickness = new Thickness(0), Cursor = Cursors.Hand, ToolTip = tip, Margin = new Thickness(2, 0, 2, 0),
                    Content = new TextBlock { Text = glyph, FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets, Segoe UI Symbol"), FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                };
                b.Click += (_, _) => act();
                return b;
            }
            stepperStack.Children.Add(PlainIcon("", () => { if (equalCount > 2) { equalCount--; equalText.Text = equalCount.ToString(); } }, "-"));
            stepperStack.Children.Add(equalText);
            stepperStack.Children.Add(PlainIcon("", () => { if (equalCount < 50) { equalCount++; equalText.Text = equalCount.ToString(); } }, "+"));

            var setBtn = Button(L.T("Set"), () =>
            {
                splitPoints.Clear();
                for (int i = 1; i < equalCount; i++)
                    splitPoints.Add(totalDuration * i / equalCount);
                countLabel.Text = L.F("{0} split points", splitPoints.Count);
                Set("parts", equalCount.ToString());
                Set("splitPoints", string.Join(",", splitPoints.Select(p => p.ToString("0.000", CultureInfo.InvariantCulture))));
                rail.InvalidateVisual();
                return Task.CompletedTask;
            });
            setBtn.Padding = new Thickness(12, 4, 12, 4);
            setBtn.FontSize = 11;
            setBtn.Margin = new Thickness(8, 0, 0, 0);
            stepperStack.Children.Add(setBtn);

            DockPanel.SetDock(stepperStack, Dock.Right);
            equalRow.Children.Add(stepperStack);
            splitPanel.Children.Add(equalRow);

            var joinSegmented = new SegmentedControl(new (string Value, string Label)[]
            {
                ("separate", L.T("Export separate")),
                ("join", L.T("Join kept"))
            }) { HorizontalAlignment = HorizontalAlignment.Right, MinWidth = 240, Margin = new Thickness(0, 4, 0, 0) };
            splitPanel.Children.Add(joinSegmented);

            parameters.Children.Add(splitPanel);
        }
        else if (operation.Id == "videoSnapshots")
        {
            parameters.Children.Clear();
            var snapshotsPanel = new StackPanel { FlowDirection = FlowDirection.LeftToRight };
            var snapTimes = new List<double>();

            var rail = transport!.TakeRail();
            rail.Height = 44;
            var railBorder = new Border { Background = new SolidColorBrush(Color.FromRgb(19, 21, 26)), CornerRadius = new CornerRadius(8), Child = rail };
            snapshotsPanel.Children.Add(railBorder);

            var transportBar = transport.BuildCompactDock(stepButtons: true);
            transportBar.Margin = new Thickness(0, 4, 0, 6);
            snapshotsPanel.Children.Add(transportBar);

            var divider = new Border { Height = 1, Background = UiTheme.Hairline, Margin = new Thickness(0, 4, 0, 8) };
            snapshotsPanel.Children.Add(divider);

            var subHeader = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 6) };
            var countLabel = new TextBlock { Text = L.F("{0} selected", 0), Foreground = UiTheme.Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(countLabel, Dock.Left);
            subHeader.Children.Add(countLabel);

            var cardsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
            var scrollCarousel = new ScrollViewer { Content = cardsPanel, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Height = 74 };

            var addFrameBtn = Button("+ " + L.T("Add Frame"), async () =>
            {
                double t = timeline.Value;
                if (!snapTimes.Any(x => Math.Abs(x - t) < 0.05))
                {
                    snapTimes.Add(t);
                    snapTimes.Sort();
                    countLabel.Text = L.F("{0} selected", snapTimes.Count);
                    saveButton.Content = L.F("Save {0} Frames", snapTimes.Count);
                    Set("times", string.Join(",", snapTimes.Select(x => x.ToString("0.000", CultureInfo.InvariantCulture))));

                    var card = new Border
                    {
                        Width = 96, Height = 60, CornerRadius = new CornerRadius(6),
                        Background = new SolidColorBrush(Color.FromRgb(24, 26, 32)),
                        Margin = new Thickness(0, 0, 8, 0), ClipToBounds = true, Cursor = Cursors.Hand
                    };
                    var cardGrid = new Grid();
                    var stillImg = new Image { Stretch = Stretch.UniformToFill };
                    try
                    {
                        byte[] bytes = await Backend.FfmpegCapture(["-ss", t.ToString("0.000", CultureInfo.InvariantCulture), "-i", paths[0], "-frames:v", "1", "-vf", "scale=192:-2", "-f", "image2pipe", "-vcodec", "png", "-"], closing.Token);
                        if (bytes.Length > 0)
                        {
                            var bmp = new BitmapImage();
                            bmp.BeginInit();
                            bmp.StreamSource = new MemoryStream(bytes);
                            bmp.CacheOption = BitmapCacheOption.OnLoad;
                            bmp.EndInit();
                            bmp.Freeze();
                            stillImg.Source = bmp;
                        }
                    }
                    catch { }
                    cardGrid.Children.Add(stillImg);

                    var badge = new Border
                    {
                        Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                        CornerRadius = new CornerRadius(3), Padding = new Thickness(3, 1, 3, 1),
                        HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom,
                        Margin = new Thickness(4)
                    };
                    badge.Child = new TextBlock { Text = TimeField.FormatTime(t), FontSize = 9, Foreground = Brushes.White, FontFamily = new FontFamily("Cascadia Mono, Consolas") };
                    cardGrid.Children.Add(badge);

                    card.Child = cardGrid;
                    card.MouseLeftButtonDown += (_, _) => transport.Seek(t);
                    cardsPanel.Children.Add(card);
                }
            });
            addFrameBtn.Padding = new Thickness(10, 4, 10, 4);
            addFrameBtn.FontSize = 11;
            addFrameBtn.HorizontalAlignment = HorizontalAlignment.Right;
            DockPanel.SetDock(addFrameBtn, Dock.Right);
            subHeader.Children.Add(addFrameBtn);
            snapshotsPanel.Children.Add(subHeader);

            snapshotsPanel.Children.Add(scrollCarousel);
            parameters.Children.Add(snapshotsPanel);
        }
        else if (operation.Id == "redactVideo")
        {
            parameters.Children.Clear();
            var redactPanel = new StackPanel { FlowDirection = FlowDirection.LeftToRight };

            var styleRow = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 6, 0, 8) };
            var styleLabel = new TextBlock { Text = L.Arabic ? "النمط" : "Style", FontSize = 12, Foreground = UiTheme.Muted, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(styleLabel, Dock.Left);
            styleRow.Children.Add(styleLabel);

            var styleControl = new SegmentedControl(new (string Value, string Label)[]
            {
                ("pixelate", Choice("pixelate")),
                ("blur", Choice("blur")),
                ("solid", Choice("solid"))
            }) { MinWidth = 240, HorizontalAlignment = HorizontalAlignment.Right };
            DockPanel.SetDock(styleControl, Dock.Right);
            styleRow.Children.Add(styleControl);
            redactPanel.Children.Add(styleRow);

            styleControl.Chosen += val =>
            {
                Set("style", val);
                editedField = "style";
                ParameterChanged();
            };

            var divider = new Border { Height = 1, Background = UiTheme.Hairline, Margin = new Thickness(0, 4, 0, 6) };
            redactPanel.Children.Add(divider);

            var redactHeader = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 4) };
            redactHeader.Children.Add(new TextBlock { Text = L.T("Redactions"), FontSize = 11, Foreground = UiTheme.Muted, VerticalAlignment = VerticalAlignment.Center });
            var delBtn = Button(L.T("移除选中区域"), () =>
            {
                if (regionList.SelectedItem is ListBoxItem item && item.Tag is Dictionary<string, object> r)
                {
                    regions.Remove(r);
                    regionList.Items.Remove(item);
                    if (regions.Count == 0) { Set("width", "0"); Set("height", "0"); rangeEdited = false; }
                }
                return Task.CompletedTask;
            });
            delBtn.Padding = new Thickness(8, 2, 8, 2);
            delBtn.FontSize = 10;
            delBtn.HorizontalAlignment = HorizontalAlignment.Right;
            DockPanel.SetDock(delBtn, Dock.Right);
            redactHeader.Children.Add(delBtn);
            redactPanel.Children.Add(redactHeader);

            regionList.Height = 70;
            redactPanel.Children.Add(regionList);

            var rail = transport!.TakeRail();
            rail.Height = 36;
            var railBorder = new Border { Background = new SolidColorBrush(Color.FromRgb(19, 21, 26)), CornerRadius = new CornerRadius(6), Child = rail, Margin = new Thickness(0, 6, 0, 0) };
            redactPanel.Children.Add(railBorder);

            var transportBar = transport.BuildCompactDock(stepButtons: true);
            transportBar.Margin = new Thickness(0, 4, 0, 0);
            redactPanel.Children.Add(transportBar);

            parameters.Children.Add(redactPanel);
        }
        else if (operation.Id == "joinVideos")
        {
            parameters.Children.Clear();
            var joinPanel = new StackPanel { FlowDirection = FlowDirection.LeftToRight };

            var captionText = new TextBlock { Text = L.T("Playback order"), FontSize = 12, Foreground = UiTheme.Muted, Margin = new Thickness(0, 4, 0, 8) };
            joinPanel.Children.Add(captionText);

            order.Height = 180;
            joinPanel.Children.Add(order);

            var reorderRow = new WrapPanel { Margin = new Thickness(0, 8, 0, 4) };
            reorderRow.Children.Add(Button(L.T("上移"), () => { MoveItem(-1); return Task.CompletedTask; }));
            reorderRow.Children.Add(Button(L.T("下移"), () => { MoveItem(1); return Task.CompletedTask; }));
            joinPanel.Children.Add(reorderRow);

            parameters.Children.Add(joinPanel);
        }
        else if (operation.Id == "compress" && MediaCompact)
        {
            parameters.Children.Clear();
            var compPanel = new StackPanel { FlowDirection = FlowDirection.LeftToRight };

            long fileBytes = 0;
            try { fileBytes = new FileInfo(paths[0]).Length; } catch { }
            string sizeStr = fileBytes > 1024 * 1024 ? $"{fileBytes / (1024.0 * 1024.0):0.1} MB" : $"{fileBytes / 1024.0:0} KB";
            var summaryText = new TextBlock { Text = L.F("Original size: {0}", sizeStr), FontSize = 12, Foreground = UiTheme.Ink, Margin = new Thickness(0, 4, 0, 8) };
            compPanel.Children.Add(summaryText);

            var presetLabel = new TextBlock { Text = L.T("Compression Preset"), FontSize = 11, Foreground = UiTheme.Muted, Margin = new Thickness(0, 0, 0, 4) };
            compPanel.Children.Add(presetLabel);

            var presetControl = new SegmentedControl(new (string Value, string Label)[]
            {
                ("light", L.T("Light")),
                ("balanced", L.T("Balanced")),
                ("strong", L.T("Strong"))
            }) { MinWidth = 280, HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 0, 0, 8) };
            presetControl.Selected = "balanced";
            compPanel.Children.Add(presetControl);

            presetControl.Chosen += val =>
            {
                int crf = val == "light" ? 23 : val == "strong" ? 32 : 28;
                Set("quality", crf.ToString());
            };

            var heightRow = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 4, 0, 8) };
            var heightLabel = new TextBlock { Text = L.T("Maximum height"), FontSize = 12, Foreground = UiTheme.Ink, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(heightLabel, Dock.Left);
            heightRow.Children.Add(heightLabel);

            var heightCombo = new ComboBox { MinWidth = 120, HorizontalAlignment = HorizontalAlignment.Right };
            heightCombo.Items.Add(new ComboBoxItem { Content = L.T("Original"), Tag = "0" });
            heightCombo.Items.Add(new ComboBoxItem { Content = "1080p", Tag = "1080" });
            heightCombo.Items.Add(new ComboBoxItem { Content = "720p", Tag = "720" });
            heightCombo.Items.Add(new ComboBoxItem { Content = "480p", Tag = "480" });
            heightCombo.Items.Add(new ComboBoxItem { Content = "360p", Tag = "360" });
            heightCombo.SelectedIndex = 0;
            heightCombo.SelectionChanged += (_, _) =>
            {
                if (heightCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag && tag != "0")
                    Set("maxEdge", tag);
            };
            DockPanel.SetDock(heightCombo, Dock.Right);
            heightRow.Children.Add(heightCombo);
            compPanel.Children.Add(heightRow);

            var divider = new Border { Height = 1, Background = UiTheme.Hairline, Margin = new Thickness(0, 4, 0, 8) };
            compPanel.Children.Add(divider);

            var targetCheck = new CheckBox { Content = L.T("Target size"), Margin = new Thickness(0, 0, 0, 6), FontSize = 12 };
            var targetBox = new TextBox { Width = 80, Padding = new Thickness(4, 2, 4, 2), FontSize = 12, Text = "10", Margin = new Thickness(0, 0, 6, 0) };
            var targetUnit = new TextBlock { Text = "MB", FontSize = 12, Foreground = UiTheme.Muted, VerticalAlignment = VerticalAlignment.Center };
            var targetSub = new StackPanel { Orientation = Orientation.Horizontal, Visibility = Visibility.Collapsed, Margin = new Thickness(20, 0, 0, 6) };
            targetSub.Children.Add(targetBox);
            targetSub.Children.Add(targetUnit);

            targetCheck.Checked += (_, _) => {
                targetSub.Visibility = Visibility.Visible;
                if (double.TryParse(targetBox.Text, out double mb)) Set("targetKB", (mb * 1024).ToString());
            };
            targetCheck.Unchecked += (_, _) => {
                targetSub.Visibility = Visibility.Collapsed;
                Set("targetKB", "0");
            };
            targetBox.TextChanged += (_, _) => {
                if (double.TryParse(targetBox.Text, out double mb)) Set("targetKB", (mb * 1024).ToString());
            };
            compPanel.Children.Add(targetCheck);
            compPanel.Children.Add(targetSub);

            parameters.Children.Add(compPanel);
        }
        else if (operation.Id == "normalizeAudio")
        {
            parameters.Children.Clear();
            var normPanel = new StackPanel { FlowDirection = FlowDirection.LeftToRight };

            var fileRow = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 4, 0, 6) };
            var nameBlock = new TextBlock { Text = Path.GetFileName(paths[0]), FontSize = 11, Foreground = UiTheme.Muted, MaxWidth = 320, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(nameBlock, Dock.Left);
            fileRow.Children.Add(nameBlock);
            var durBlock = new TextBlock { Text = TimeField.FormatTime(totalDuration), FontSize = 11, Foreground = UiTheme.Muted, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, FontFamily = new FontFamily("Cascadia Mono, Consolas") };
            DockPanel.SetDock(durBlock, Dock.Right);
            fileRow.Children.Add(durBlock);
            normPanel.Children.Add(fileRow);

            var rail = transport!.TakeRail();
            rail.Height = 150;
            var railBorder = new Border { Background = new SolidColorBrush(Color.FromRgb(19, 21, 26)), CornerRadius = new CornerRadius(8), Child = rail, Margin = new Thickness(0, 0, 0, 4) };
            normPanel.Children.Add(railBorder);

            var transportBar = transport.BuildCompactDock(stepButtons: false);
            transportBar.Margin = new Thickness(0, 2, 0, 6);
            normPanel.Children.Add(transportBar);

            var measuredBlock = new TextBlock { Text = L.F("Now: {0} LUFS · peak {1} dBTP · range {2} LU", "-16", "-1.5", "11"), FontSize = 11, Foreground = UiTheme.Muted, Margin = new Thickness(0, 2, 0, 6) };
            normPanel.Children.Add(measuredBlock);

            FrameworkElement MakeSliderRow(string label, double min, double max, double step, double defVal, string unit, Action<double> onVal)
            {
                var row = new StackPanel { Margin = new Thickness(0, 4, 0, 6) };
                var hdr = new DockPanel { LastChildFill = true };
                var lbl = new TextBlock { Text = label, FontSize = 12, Foreground = UiTheme.Ink, VerticalAlignment = VerticalAlignment.Center };
                DockPanel.SetDock(lbl, Dock.Left);
                hdr.Children.Add(lbl);
                var valTxt = new TextBlock { Text = $"{defVal:0.#} {unit}", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = UiTheme.Ink, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, FontFamily = new FontFamily("Cascadia Mono, Consolas") };
                DockPanel.SetDock(valTxt, Dock.Right);
                hdr.Children.Add(valTxt);
                row.Children.Add(hdr);

                var sld = new Slider { Minimum = min, Maximum = max, Value = defVal, SmallChange = step, LargeChange = step * 2, IsMoveToPointEnabled = true, Margin = new Thickness(0, 2, 0, 0) };
                sld.ValueChanged += (_, _) =>
                {
                    double rounded = Math.Round(sld.Value / step) * step;
                    valTxt.Text = $"{rounded:0.#} {unit}";
                    onVal(rounded);
                };
                row.Children.Add(sld);
                return row;
            }

            normPanel.Children.Add(MakeSliderRow(L.T("Target loudness"), -24, -5, 1, -16, "LUFS", v => Set("loudness", v.ToString(CultureInfo.InvariantCulture))));
            normPanel.Children.Add(MakeSliderRow(L.T("Loudness range"), 1, 20, 1, 11, "LU", v => Set("range", v.ToString(CultureInfo.InvariantCulture))));
            normPanel.Children.Add(MakeSliderRow(L.T("True peak limit"), -9, 0, 0.5, -1.5, "dBTP", v => Set("peak", v.ToString(CultureInfo.InvariantCulture))));

            var caption = new TextBlock
            {
                Text = L.T("−16 LUFS is a balanced default for spoken audio and music. The true peak limit helps prevent clipping after encoding."),
                FontSize = 11, Foreground = UiTheme.Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 8)
            };
            normPanel.Children.Add(caption);

            parameters.Children.Add(normPanel);
        }
        else if (operation.Id == "audioToVideo")
        {
            parameters.Children.Clear();
            var a2vPanel = new StackPanel { FlowDirection = FlowDirection.LeftToRight };

            Set("aspect", "landscape");
            Set("style", "waveform");
            Set("waveStyle", "mirror");
            Set("color", "orange");
            Set("color2", "");
            Set("background", "black");
            Set("intensity", "1.0");
            Set("thickness", "1.0");
            Set("glow", "true");
            Set("title", "");
            Set("overlayWave", "false");
            Set("backgroundImage", "");
            Set("framerate", "30");

            var stageBorder = new Border
            {
                Width = 420, Height = 236,
                CornerRadius = new CornerRadius(12),
                Background = Brushes.Black,
                ClipToBounds = true,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 8)
            };
            var stageGrid = new Grid();
            var waveCanvas = new MediaTimeline { Height = 120, Margin = new Thickness(24, 0, 24, 0), VerticalAlignment = VerticalAlignment.Center };
            waveCanvas.LoadWaveform(paths[0], cancellation: closing.Token);
            stageGrid.Children.Add(waveCanvas);

            var stillImage = new Image { Stretch = Stretch.Uniform, Visibility = Visibility.Collapsed };
            stageGrid.Children.Add(stillImage);

            stagePlayer = new MediaElement { LoadedBehavior = MediaState.Manual, UnloadedBehavior = MediaState.Manual, Stretch = Stretch.Uniform, Visibility = Visibility.Collapsed };
            stagePlayer.MediaEnded += (_, _) => { stagePlayer.Position = TimeSpan.Zero; stagePlayer.Play(); };
            stageGrid.Children.Add(stagePlayer);

            var stageNotice = new TextBlock
            {
                Text = L.T("Rendering preview…"),
                FontSize = 12,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            stageGrid.Children.Add(stageNotice);
            stageBorder.Child = stageGrid;
            a2vPanel.Children.Add(stageBorder);

            a2vPreviewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            void TriggerA2VPreview()
            {
                a2vPreviewTimer.Stop();
                a2vPreviewTimer.Start();
            }

            a2vPreviewTimer.Tick += async (_, _) =>
            {
                a2vPreviewTimer.Stop();
                if (closing.IsCancellationRequested) return;

                string currentStyle = Get("style", "waveform");
                string currentBgImg = Get("backgroundImage", "");
                if (currentStyle == "still" && string.IsNullOrEmpty(currentBgImg))
                {
                    if (stagePlayer != null)
                    {
                        stagePlayer.Stop();
                        stagePlayer.Visibility = Visibility.Collapsed;
                    }
                    stageNotice.Visibility = Visibility.Collapsed;
                    return;
                }

                a2vCancellation?.Cancel();
                var cts = CancellationTokenSource.CreateLinkedTokenSource(closing.Token);
                a2vCancellation = cts;
                stageNotice.Visibility = Visibility.Visible;

                try
                {
                    var info = await Backend.Preview(Capture(), folder, cts.Token, "--playback");
                    if (cts.IsCancellationRequested) return;

                    string? previewMp4 = info.TryGetProperty("Preview", out var p) ? p.GetString() : null;
                    if (!string.IsNullOrEmpty(previewMp4) && File.Exists(previewMp4) && stagePlayer != null)
                    {
                        stageNotice.Visibility = Visibility.Collapsed;
                        stillImage.Visibility = Visibility.Collapsed;
                        waveCanvas.Visibility = Visibility.Collapsed;
                        stagePlayer.Source = new Uri(previewMp4);
                        stagePlayer.Position = TimeSpan.Zero;
                        stagePlayer.Visibility = Visibility.Visible;
                        stagePlayer.Play();
                    }
                }
                catch
                {
                    stageNotice.Visibility = Visibility.Collapsed;
                }
            };

            var controlsPanel = new StackPanel { FlowDirection = FlowDirection.LeftToRight };

            var styleRow = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 2, 0, 6) };
            var styleLbl = new TextBlock { Text = L.T("Style"), FontSize = 12, Foreground = UiTheme.Muted, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(styleLbl, Dock.Left);
            styleRow.Children.Add(styleLbl);

            var styleCtrl = new SegmentedControl(new (string Value, string Label)[]
            {
                ("waveform", L.T("Waveform")),
                ("still", L.T("Still image"))
            }) { MinWidth = 220, HorizontalAlignment = HorizontalAlignment.Right };
            DockPanel.SetDock(styleCtrl, Dock.Right);
            styleRow.Children.Add(styleCtrl);
            controlsPanel.Children.Add(styleRow);

            var shapeRow = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 2, 0, 6) };
            var shapeLbl = new TextBlock { Text = L.T("Shape"), FontSize = 12, Foreground = UiTheme.Muted, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(shapeLbl, Dock.Left);
            shapeRow.Children.Add(shapeLbl);

            var shapeCtrl = new SegmentedControl(new (string Value, string Label)[]
            {
                ("landscape", L.T("Landscape · 16:9")),
                ("portrait", L.T("Portrait · 9:16")),
                ("square", L.T("Square · 1:1"))
            }) { MinWidth = 280, HorizontalAlignment = HorizontalAlignment.Right };
            DockPanel.SetDock(shapeCtrl, Dock.Right);
            shapeRow.Children.Add(shapeCtrl);
            controlsPanel.Children.Add(shapeRow);

            shapeCtrl.Chosen += val =>
            {
                Set("aspect", val);
                double maxH = SystemParameters.WorkArea.Height * 0.92;
                if (val == "portrait")
                {
                    stageBorder.Width = 202; stageBorder.Height = 360;
                    Height = Math.Min(maxH, 720);
                }
                else if (val == "square")
                {
                    stageBorder.Width = 280; stageBorder.Height = 280;
                    Height = Math.Min(maxH, 640);
                }
                else
                {
                    stageBorder.Width = 420; stageBorder.Height = 236;
                    Height = Math.Min(maxH, 600);
                }
                TriggerA2VPreview();
            };

            var imageRow = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 2, 0, 6), Visibility = Visibility.Collapsed };
            var imgLbl = new TextBlock { Text = L.T("Still image"), FontSize = 12, Foreground = UiTheme.Muted, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(imgLbl, Dock.Left);
            imageRow.Children.Add(imgLbl);

            var stillHint = new TextBlock
            {
                Text = L.T("Please choose an image to create the video."),
                FontSize = 11,
                Foreground = UiTheme.Accent,
                Margin = new Thickness(0, 0, 0, 6),
                Visibility = Visibility.Collapsed
            };

            void ValidateStillState()
            {
                bool isStill = Get("style", "waveform") == "still";
                bool hasImage = !string.IsNullOrEmpty(Get("backgroundImage", ""));
                if (isStill && !hasImage)
                {
                    saveButton.IsEnabled = false;
                    stillHint.Visibility = Visibility.Visible;
                }
                else
                {
                    saveButton.IsEnabled = true;
                    stillHint.Visibility = Visibility.Collapsed;
                }
            }

            Button chooseBtn = null!;
            chooseBtn = Button(L.T("Choose Image…"), () =>
            {
                var ofd = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Images|*.png;*.jpg;*.jpeg;*.webp;*.bmp"
                };
                if (ofd.ShowDialog() == true)
                {
                    Set("backgroundImage", ofd.FileName);
                    chooseBtn.Content = Path.GetFileName(ofd.FileName);
                    try
                    {
                        var bmp = new BitmapImage(new Uri(ofd.FileName));
                        stillImage.Source = bmp;
                        stillImage.Visibility = Visibility.Visible;
                        waveCanvas.Visibility = Visibility.Collapsed;
                    } catch { }
                    ValidateStillState();
                    TriggerA2VPreview();
                }
                return Task.CompletedTask;
            });
            chooseBtn.Padding = new Thickness(12, 4, 12, 4);
            chooseBtn.FontSize = 11;
            DockPanel.SetDock(chooseBtn, Dock.Right);
            imageRow.Children.Add(chooseBtn);
            controlsPanel.Children.Add(imageRow);
            controlsPanel.Children.Add(stillHint);

            var overlayRow = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 2, 0, 6), Visibility = Visibility.Collapsed };
            var overlayCheck = new CheckBox { Content = L.T("Overlay waveform on picture"), FontSize = 12 };
            DockPanel.SetDock(overlayCheck, Dock.Left);
            overlayRow.Children.Add(overlayCheck);
            controlsPanel.Children.Add(overlayRow);

            ComboBox MakeCombo((string Value, string Label)[] items, string defVal, Action<string> onChosen)
            {
                var combo = new ComboBox { MinWidth = 160, HorizontalAlignment = HorizontalAlignment.Right };
                foreach (var (val, label) in items)
                {
                    var cbi = new ComboBoxItem { Content = label, Tag = val };
                    combo.Items.Add(cbi);
                    if (val == defVal) combo.SelectedItem = cbi;
                }
                if (combo.SelectedItem == null && combo.Items.Count > 0)
                    combo.SelectedIndex = 0;
                combo.SelectionChanged += (_, _) =>
                {
                    if (combo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
                        onChosen(tag);
                };
                return combo;
            }

            var waveStyleRow = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 2, 0, 6) };
            var waveStyleLbl = new TextBlock { Text = L.T("Wave style"), FontSize = 12, Foreground = UiTheme.Muted, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(waveStyleLbl, Dock.Left);
            waveStyleRow.Children.Add(waveStyleLbl);

            var waveCombo = MakeCombo(new (string, string)[]
            {
                ("mirror", L.T("Mirror bars")),
                ("bars", L.T("Bars")),
                ("line", L.T("Line")),
                ("circle_bars", L.T("Circle bars")),
                ("circle_wave", L.T("Circle wave")),
                ("spectrum", L.T("Spectrum")),
                ("dots", L.T("Dots")),
                ("pulse", L.T("Pulse")),
                ("wave_fill", L.T("Wave fill")),
                ("particles", L.T("Particles"))
            }, "mirror", val =>
            {
                Set("waveStyle", val);
                TriggerA2VPreview();
            });
            DockPanel.SetDock(waveCombo, Dock.Right);
            waveStyleRow.Children.Add(waveCombo);
            controlsPanel.Children.Add(waveStyleRow);

            var colorRow = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 2, 0, 6) };
            var colorLbl = new TextBlock { Text = L.T("Color"), FontSize = 12, Foreground = UiTheme.Muted, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(colorLbl, Dock.Left);
            colorRow.Children.Add(colorLbl);

            var colorPicker = new ColorSwatchPicker("#FF6500", true) { Margin = new Thickness(6, 0, 0, 0), Visibility = Visibility.Collapsed };
            colorPicker.ColorChanged += hex =>
            {
                Set("color", hex);
                TriggerA2VPreview();
            };

            var colorRight = new StackPanel { Orientation = Orientation.Horizontal };
            var colorCombo = MakeCombo(new (string, string)[]
            {
                ("orange", L.T("Orange")),
                ("white", L.T("White")),
                ("cyan", L.T("Cyan")),
                ("violet", L.T("Violet")),
                ("green", L.T("Green")),
                ("pink", L.T("Pink")),
                ("sunset", L.T("Sunset gradient")),
                ("ocean", L.T("Ocean gradient")),
                ("rainbow", L.T("Rainbow")),
                ("custom", L.T("Custom color"))
            }, "orange", val =>
            {
                if (val == "custom")
                {
                    colorPicker.Visibility = Visibility.Visible;
                    Set("color", colorPicker.HexColor);
                }
                else
                {
                    colorPicker.Visibility = Visibility.Collapsed;
                    Set("color", val);
                }
                TriggerA2VPreview();
            });
            colorRight.Children.Add(colorCombo);
            colorRight.Children.Add(colorPicker);
            DockPanel.SetDock(colorRight, Dock.Right);
            colorRow.Children.Add(colorRight);
            controlsPanel.Children.Add(colorRow);

            // Optional second color (gradient)
            var secColorRow = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 2, 0, 6) };
            var secColorCheck = new CheckBox { Content = L.T("Second color (optional)"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(secColorCheck, Dock.Left);
            secColorRow.Children.Add(secColorCheck);

            var secColorPicker = new ColorSwatchPicker("#EC4899", true) { Visibility = Visibility.Collapsed };
            secColorPicker.ColorChanged += hex =>
            {
                if (secColorCheck.IsChecked == true) Set("color2", hex);
                TriggerA2VPreview();
            };
            DockPanel.SetDock(secColorPicker, Dock.Right);
            secColorRow.Children.Add(secColorPicker);

            secColorCheck.Checked += (_, _) =>
            {
                secColorPicker.Visibility = Visibility.Visible;
                Set("color2", secColorPicker.HexColor);
                TriggerA2VPreview();
            };
            secColorCheck.Unchecked += (_, _) =>
            {
                secColorPicker.Visibility = Visibility.Collapsed;
                Set("color2", "");
                TriggerA2VPreview();
            };
            controlsPanel.Children.Add(secColorRow);

            // Background Row
            var bgRow = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 2, 0, 6) };
            var bgLbl = new TextBlock { Text = L.T("Background"), FontSize = 12, Foreground = UiTheme.Muted, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(bgLbl, Dock.Left);
            bgRow.Children.Add(bgLbl);

            var bgPicker = new ColorSwatchPicker("#111827", true) { Margin = new Thickness(6, 0, 0, 0), Visibility = Visibility.Collapsed };
            bgPicker.ColorChanged += hex =>
            {
                Set("background", hex);
                TriggerA2VPreview();
            };

            var bgRight = new StackPanel { Orientation = Orientation.Horizontal };
            var bgCombo = MakeCombo(new (string, string)[]
            {
                ("black", L.T("Black")),
                ("gradient", L.T("Dark gradient")),
                ("blurred", L.T("Blurred")),
                ("still", L.T("Still picture")),
                ("custom", L.T("Custom color"))
            }, "black", val =>
            {
                if (val == "custom")
                {
                    bgPicker.Visibility = Visibility.Visible;
                    Set("background", bgPicker.HexColor);
                }
                else
                {
                    bgPicker.Visibility = Visibility.Collapsed;
                    Set("background", val);
                }
                TriggerA2VPreview();
            });
            bgRight.Children.Add(bgCombo);
            bgRight.Children.Add(bgPicker);
            DockPanel.SetDock(bgRight, Dock.Right);
            bgRow.Children.Add(bgRight);
            controlsPanel.Children.Add(bgRow);

            // Customise Collapsible Section
            var customExpander = new Expander
            {
                Header = new TextBlock { Text = L.T("Customise"), FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = UiTheme.Ink },
                IsExpanded = false,
                Margin = new Thickness(0, 4, 0, 4)
            };
            var customPanel = new StackPanel { Margin = new Thickness(4, 6, 4, 2) };

            FrameworkElement MakeCustomSlider(string label, double min, double max, double step, double defVal, string unit, Action<double> onVal)
            {
                var row = new StackPanel { Margin = new Thickness(0, 2, 0, 4) };
                var hdr = new DockPanel { LastChildFill = true };
                var lbl = new TextBlock { Text = label, FontSize = 11, Foreground = UiTheme.Muted };
                DockPanel.SetDock(lbl, Dock.Left);
                hdr.Children.Add(lbl);
                var valTxt = new TextBlock { Text = $"{defVal:0.0} {unit}".Trim(), FontSize = 11, Foreground = UiTheme.Ink, HorizontalAlignment = HorizontalAlignment.Right };
                DockPanel.SetDock(valTxt, Dock.Right);
                hdr.Children.Add(valTxt);
                row.Children.Add(hdr);
                var sld = new Slider { Minimum = min, Maximum = max, Value = defVal, SmallChange = step, LargeChange = step * 2, IsMoveToPointEnabled = true, Margin = new Thickness(0, 2, 0, 0) };
                sld.ValueChanged += (_, _) =>
                {
                    double rounded = Math.Round(sld.Value / step) * step;
                    valTxt.Text = $"{rounded:0.0} {unit}".Trim();
                    onVal(rounded);
                };
                row.Children.Add(sld);
                return row;
            }

            customPanel.Children.Add(MakeCustomSlider(L.T("Sensitivity"), 0.2, 3.0, 0.1, 1.0, "x", v =>
            {
                Set("intensity", v.ToString("0.0", CultureInfo.InvariantCulture));
                TriggerA2VPreview();
            }));

            customPanel.Children.Add(MakeCustomSlider(L.T("Thickness"), 0.5, 3.0, 0.1, 1.0, "x", v =>
            {
                Set("thickness", v.ToString("0.0", CultureInfo.InvariantCulture));
                TriggerA2VPreview();
            }));

            var glowCheck = new CheckBox { Content = L.T("Soft glow"), IsChecked = true, FontSize = 11, Margin = new Thickness(0, 4, 0, 6) };
            glowCheck.Checked += (_, _) => { Set("glow", "true"); TriggerA2VPreview(); };
            glowCheck.Unchecked += (_, _) => { Set("glow", "false"); TriggerA2VPreview(); };
            customPanel.Children.Add(glowCheck);

            var titleRow = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 4, 0, 4) };
            var titleLbl = new TextBlock { Text = L.T("Title overlay"), FontSize = 11, Foreground = UiTheme.Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            DockPanel.SetDock(titleLbl, Dock.Left);
            titleRow.Children.Add(titleLbl);
            var titleBox = new TextBox { FontSize = 11, Height = 22, VerticalContentAlignment = VerticalAlignment.Center };
            titleBox.TextChanged += (_, _) => { Set("title", titleBox.Text); TriggerA2VPreview(); };
            titleRow.Children.Add(titleBox);
            customPanel.Children.Add(titleRow);

            customExpander.Content = customPanel;
            controlsPanel.Children.Add(customExpander);

            overlayCheck.Checked += (_, _) =>
            {
                Set("overlayWave", "true");
                waveStyleRow.Visibility = Visibility.Visible;
                colorRow.Visibility = Visibility.Visible;
                secColorRow.Visibility = Visibility.Visible;
                customExpander.Visibility = Visibility.Visible;
                TriggerA2VPreview();
            };
            overlayCheck.Unchecked += (_, _) =>
            {
                Set("overlayWave", "false");
                if (Get("style", "waveform") == "still")
                {
                    waveStyleRow.Visibility = Visibility.Collapsed;
                    colorRow.Visibility = Visibility.Collapsed;
                    secColorRow.Visibility = Visibility.Collapsed;
                    customExpander.Visibility = Visibility.Collapsed;
                }
                TriggerA2VPreview();
            };

            styleCtrl.Chosen += val =>
            {
                Set("style", val);
                if (val == "still")
                {
                    imageRow.Visibility = Visibility.Visible;
                    overlayRow.Visibility = Visibility.Visible;
                    bool ov = overlayCheck.IsChecked == true;
                    waveStyleRow.Visibility = ov ? Visibility.Visible : Visibility.Collapsed;
                    colorRow.Visibility = ov ? Visibility.Visible : Visibility.Collapsed;
                    secColorRow.Visibility = ov ? Visibility.Visible : Visibility.Collapsed;
                    customExpander.Visibility = ov ? Visibility.Visible : Visibility.Collapsed;
                    waveCanvas.Visibility = Visibility.Collapsed;
                    stillImage.Visibility = string.IsNullOrEmpty(Get("backgroundImage", "")) ? Visibility.Collapsed : Visibility.Visible;
                    ValidateStillState();
                }
                else
                {
                    imageRow.Visibility = Visibility.Collapsed;
                    overlayRow.Visibility = Visibility.Collapsed;
                    stillHint.Visibility = Visibility.Collapsed;
                    saveButton.IsEnabled = true;
                    waveStyleRow.Visibility = Visibility.Visible;
                    colorRow.Visibility = Visibility.Visible;
                    secColorRow.Visibility = Visibility.Visible;
                    customExpander.Visibility = Visibility.Visible;
                    stillImage.Visibility = Visibility.Collapsed;
                    waveCanvas.Visibility = Visibility.Visible;
                    Set("backgroundImage", "");
                }
                TriggerA2VPreview();
            };

            var scroll = new ScrollViewer
            {
                Content = controlsPanel,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                MaxHeight = 240,
                Margin = new Thickness(0, 2, 0, 0)
            };
            a2vPanel.Children.Add(scroll);

            parameters.Children.Add(a2vPanel);
            TriggerA2VPreview();
        }
        else if (operation.Id == "audioChannels")
        {
            parameters.Children.Clear();
            var chanPanel = new StackPanel { FlowDirection = FlowDirection.LeftToRight };

            var previewHdr = new TextBlock { Text = L.T("Output channel preview"), FontSize = 11, Foreground = UiTheme.Muted, Margin = new Thickness(0, 4, 0, 6) };
            chanPanel.Children.Add(previewHdr);

            void AddChannelRow(string badgeText)
            {
                var cRow = new Grid { Margin = new Thickness(0, 0, 0, 6) };
                cRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
                cRow.ColumnDefinitions.Add(new ColumnDefinition());
                cRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });

                var badge = new Border
                {
                    Width = 24, Height = 24, CornerRadius = new CornerRadius(12),
                    Background = new SolidColorBrush(Color.FromArgb(0x26, 255, 82, 0)),
                    Child = new TextBlock { Text = badgeText, FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = UiTheme.Ink, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                };
                Grid.SetColumn(badge, 0);
                cRow.Children.Add(badge);

                var wave = new MediaTimeline { Height = 44, Margin = new Thickness(4, 0, 4, 0) };
                wave.LoadWaveform(paths[0], cancellation: closing.Token);
                var waveBorder = new Border { Background = new SolidColorBrush(Color.FromArgb(0x0A, 0, 0, 0)), CornerRadius = new CornerRadius(8), Child = wave };
                Grid.SetColumn(waveBorder, 1);
                cRow.Children.Add(waveBorder);

                var soloBtn = Button("", () => Task.CompletedTask);
                soloBtn.Width = 28; soloBtn.Height = 28; soloBtn.Padding = new Thickness(0);
                soloBtn.ToolTip = L.Arabic ? "تشغيل هذه القناة وحدها" : "Play this channel on its own";
                Grid.SetColumn(soloBtn, 2);
                cRow.Children.Add(soloBtn);

                chanPanel.Children.Add(cRow);
            }

            AddChannelRow("L");
            AddChannelRow("R");

            var divider = new Border { Height = 1, Background = UiTheme.Hairline, Margin = new Thickness(0, 4, 0, 8) };
            chanPanel.Children.Add(divider);

            var outRow = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 4, 0, 8) };
            var outLbl = new TextBlock { Text = L.T("Output"), FontSize = 12, Foreground = UiTheme.Muted, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(outLbl, Dock.Left);
            outRow.Children.Add(outLbl);

            var outCtrl = new SegmentedControl(new (string Value, string Label)[]
            {
                ("stereo", L.T("Stereo")),
                ("mono", L.T("Mono"))
            }) { MinWidth = 200, HorizontalAlignment = HorizontalAlignment.Right };
            outCtrl.Chosen += val => Set("channels", val);
            DockPanel.SetDock(outCtrl, Dock.Right);
            outRow.Children.Add(outCtrl);
            chanPanel.Children.Add(outRow);

            FrameworkElement MakeVolumeRow(string label, string key)
            {
                var row = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 4, 0, 6) };
                var lbl = new TextBlock { Text = label, Width = 96, FontSize = 12, Foreground = UiTheme.Ink, VerticalAlignment = VerticalAlignment.Center };
                DockPanel.SetDock(lbl, Dock.Left);
                row.Children.Add(lbl);

                var valTxt = new TextBlock { Text = "100%", Width = 44, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, FontFamily = new FontFamily("Cascadia Mono, Consolas") };
                DockPanel.SetDock(valTxt, Dock.Right);
                row.Children.Add(valTxt);

                var sld = new Slider { Minimum = 0, Maximum = 1.5, Value = 1.0, SmallChange = 0.05, LargeChange = 0.1, IsMoveToPointEnabled = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0) };
                sld.ValueChanged += (_, _) =>
                {
                    int pct = (int)Math.Round(sld.Value * 100);
                    valTxt.Text = $"{pct}%";
                    Set(key, sld.Value.ToString("0.##", CultureInfo.InvariantCulture));
                };
                row.Children.Add(sld);
                return row;
            }

            chanPanel.Children.Add(MakeVolumeRow(L.T("Left volume"), "leftGain"));
            chanPanel.Children.Add(MakeVolumeRow(L.T("Right volume"), "rightGain"));

            parameters.Children.Add(chanPanel);
        }
        else if (operation.Id == "redactAudio")
        {
            parameters.Children.Clear();
            var bleepPanel = new StackPanel { FlowDirection = FlowDirection.LeftToRight };

            var bleeps = new List<(double Start, double End)>();

            var subHeader = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 4, 0, 6) };
            var countLabel = new TextBlock { Text = L.F("Bleeps ({0})", 0), Foreground = UiTheme.Ink, FontSize = 12, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(countLabel, Dock.Left);
            subHeader.Children.Add(countLabel);

            var quickStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var addPlayheadBtn = Button(L.T("Add bleep at playhead"), () =>
            {
                double cur = timeline.Value;
                double s = Math.Max(0, cur - 0.25);
                double e = Math.Min(totalDuration, cur + 0.25);
                bleeps.Add((s, e));
                countLabel.Text = L.F("Bleeps ({0})", bleeps.Count);
                Set("ranges", JsonSerializer.Serialize(bleeps.Select(b => new Dictionary<string, double> { ["start"] = b.Start, ["end"] = b.End })));
                transport?.Rail.InvalidateVisual();
                return Task.CompletedTask;
            });
            addPlayheadBtn.Padding = new Thickness(8, 4, 8, 4);
            addPlayheadBtn.FontSize = 11;
            addPlayheadBtn.Margin = new Thickness(0, 0, 6, 0);
            quickStack.Children.Add(addPlayheadBtn);

            var bleepAllBtn = Button(L.T("Bleep Entire Audio"), () =>
            {
                bleeps.Clear();
                bleeps.Add((0, totalDuration));
                countLabel.Text = L.F("Bleeps ({0})", bleeps.Count);
                Set("ranges", JsonSerializer.Serialize(bleeps.Select(b => new Dictionary<string, double> { ["start"] = b.Start, ["end"] = b.End })));
                transport?.Rail.InvalidateVisual();
                return Task.CompletedTask;
            });
            bleepAllBtn.Padding = new Thickness(8, 4, 8, 4);
            bleepAllBtn.FontSize = 11;
            quickStack.Children.Add(bleepAllBtn);

            DockPanel.SetDock(quickStack, Dock.Right);
            subHeader.Children.Add(quickStack);
            bleepPanel.Children.Add(subHeader);

            var tracksGrid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            tracksGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
            tracksGrid.ColumnDefinitions.Add(new ColumnDefinition());
            tracksGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            tracksGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var bleepLbl = new TextBlock { Text = L.T("Bleep"), FontSize = 11, Foreground = UiTheme.Muted, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(bleepLbl, 0);
            Grid.SetColumn(bleepLbl, 0);
            tracksGrid.Children.Add(bleepLbl);

            var bleepLane = new MediaTimeline { Height = 30, Margin = new Thickness(0, 2, 0, 2) };
            bleepLane.Overlay = (dc, w, h) =>
            {
                foreach (var b in bleeps)
                {
                    double x1 = bleepLane.XAt(b.Start);
                    double x2 = bleepLane.XAt(b.End);
                    double bw = Math.Max(8, x2 - x1);
                    dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(0xD9, 255, 82, 0)), null, new Rect(x1, 2, bw, h - 4), 4, 4);
                }
            };
            var bleepBorder = new Border { Background = new SolidColorBrush(Color.FromArgb(0x0D, 0, 0, 0)), CornerRadius = new CornerRadius(7), Child = bleepLane };
            Grid.SetRow(bleepBorder, 0);
            Grid.SetColumn(bleepBorder, 1);
            tracksGrid.Children.Add(bleepBorder);

            var audioLbl = new TextBlock { Text = L.T("Audio"), FontSize = 11, Foreground = UiTheme.Muted, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(audioLbl, 1);
            Grid.SetColumn(audioLbl, 0);
            tracksGrid.Children.Add(audioLbl);

            var rail = transport!.TakeRail();
            rail.Height = 70;
            var railBorder = new Border { Background = new SolidColorBrush(Color.FromRgb(19, 21, 26)), CornerRadius = new CornerRadius(8), Child = rail, Margin = new Thickness(0, 4, 0, 2) };
            Grid.SetRow(railBorder, 1);
            Grid.SetColumn(railBorder, 1);
            tracksGrid.Children.Add(railBorder);

            bleepPanel.Children.Add(tracksGrid);

            var transportBar = transport.BuildCompactDock(stepButtons: false);
            transportBar.Margin = new Thickness(0, 4, 0, 6);
            bleepPanel.Children.Add(transportBar);

            var caption = new TextBlock
            {
                Text = L.T("Drag across the Bleep layer to add a section, then drag a section or either edge to adjust it. Delete removes the selected one."),
                FontSize = 11, Foreground = UiTheme.Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 6)
            };
            bleepPanel.Children.Add(caption);

            parameters.Children.Add(bleepPanel);
        }
        else if (operation.Id == "videoToGif")
        {
            parameters.Children.Clear();
            var gifPanel = new StackPanel { FlowDirection = FlowDirection.LeftToRight };

            var rail = transport!.TakeRail();
            rail.Height = 44;
            rail.RangeEnabled = true;
            var railBorder = new Border { Background = new SolidColorBrush(Color.FromRgb(19, 21, 26)), CornerRadius = new CornerRadius(8), Child = rail, Margin = new Thickness(0, 4, 0, 4) };
            gifPanel.Children.Add(railBorder);

            var timesRow = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 4, 0, 4) };
            var startField = new TimeField(0, val => { Set("start", val.ToString("0.000", CultureInfo.InvariantCulture)); transport.Seek(val); });
            var endField = new TimeField(totalDuration, val => { Set("end", val.ToString("0.000", CultureInfo.InvariantCulture)); transport.Seek(val); });

            var startStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            startStack.Children.Add(new TextBlock { Text = L.T("Start"), Foreground = UiTheme.Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
            startStack.Children.Add(startField);
            DockPanel.SetDock(startStack, Dock.Left);
            timesRow.Children.Add(startStack);

            var endStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            endStack.Children.Add(new TextBlock { Text = L.T("End"), Foreground = UiTheme.Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
            endStack.Children.Add(endField);
            DockPanel.SetDock(endStack, Dock.Right);
            timesRow.Children.Add(endStack);
            gifPanel.Children.Add(timesRow);

            transport.RangeChanged += (first, last) =>
            {
                startField.SetValue(first);
                endField.SetValue(last);
            };

            transport.DurationChanged += length => { if (length > 0) endField.SetValue(length); };
            var compactDock = transport.BuildCompactDock(stepButtons: true);
            compactDock.Margin = new Thickness(0, 2, 0, 6);
            gifPanel.Children.Add(compactDock);

            var divider = new Border { Height = 1, Background = UiTheme.Hairline, Margin = new Thickness(0, 2, 0, 6) };
            gifPanel.Children.Add(divider);

            FrameworkElement MakeSliderRow(string label, double min, double max, double step, double defVal, string unit, Action<double> onVal)
            {
                var row = new StackPanel { Margin = new Thickness(0, 2, 0, 4) };
                var hdr = new DockPanel { LastChildFill = true };
                var lbl = new TextBlock { Text = label, FontSize = 12, Foreground = UiTheme.Ink, VerticalAlignment = VerticalAlignment.Center };
                DockPanel.SetDock(lbl, Dock.Left);
                hdr.Children.Add(lbl);
                var valTxt = new TextBlock { Text = $"{defVal:0} {unit}", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = UiTheme.Ink, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, FontFamily = new FontFamily("Cascadia Mono, Consolas") };
                DockPanel.SetDock(valTxt, Dock.Right);
                hdr.Children.Add(valTxt);
                row.Children.Add(hdr);

                var sld = new Slider { Minimum = min, Maximum = max, Value = defVal, SmallChange = step, LargeChange = step * 2, IsMoveToPointEnabled = true, Margin = new Thickness(0, 2, 0, 0) };
                sld.ValueChanged += (_, _) =>
                {
                    double rounded = Math.Round(sld.Value / step) * step;
                    valTxt.Text = $"{rounded:0} {unit}";
                    onVal(rounded);
                };
                row.Children.Add(sld);
                return row;
            }

            gifPanel.Children.Add(MakeSliderRow(L.T("FPS"), 1, 30, 1, 12, "fps", v => Set("fps", v.ToString(CultureInfo.InvariantCulture))));
            gifPanel.Children.Add(MakeSliderRow(L.T("Width (px)"), 16, 1920, 10, 480, "px", v => Set("width", v.ToString(CultureInfo.InvariantCulture))));

            var loopCheck = new CheckBox { Content = L.T("Loop"), IsChecked = true, Margin = new Thickness(0, 4, 0, 4), FontSize = 12 };
            loopCheck.Checked += (_, _) => Set("loop", "true");
            loopCheck.Unchecked += (_, _) => Set("loop", "false");
            gifPanel.Children.Add(loopCheck);

            parameters.Children.Add(gifPanel);
        }
        else if (operation.Id == "audioEffects")
        {
            parameters.Children.Clear();
            var fxPanel = new StackPanel { FlowDirection = FlowDirection.LeftToRight };

            var fileRow = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 4, 0, 6) };
            var nameBlock = new TextBlock { Text = Path.GetFileName(paths[0]), FontSize = 11, Foreground = UiTheme.Muted, MaxWidth = 280, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(nameBlock, Dock.Left);
            fileRow.Children.Add(nameBlock);
            var durBlock = new TextBlock { Text = TimeField.FormatTime(totalDuration), FontSize = 11, Foreground = UiTheme.Muted, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, FontFamily = new FontFamily("Cascadia Mono, Consolas") };
            DockPanel.SetDock(durBlock, Dock.Right);
            fileRow.Children.Add(durBlock);
            fxPanel.Children.Add(fileRow);

            var wave = new MediaTimeline { Height = 44, Margin = new Thickness(0, 2, 0, 6) };
            wave.LoadWaveform(paths[0], cancellation: closing.Token);
            var waveBorder = new Border { Background = new SolidColorBrush(Color.FromArgb(0x26, 255, 82, 0)), CornerRadius = new CornerRadius(8), Child = wave };
            fxPanel.Children.Add(waveBorder);

            FrameworkElement MakeSliderRow(string label, double min, double max, double step, double defVal, string unit, string fmt, Action<double> onVal)
            {
                var row = new StackPanel { Margin = new Thickness(0, 2, 0, 4) };
                var hdr = new DockPanel { LastChildFill = true };
                var lbl = new TextBlock { Text = label, FontSize = 12, Foreground = UiTheme.Ink, VerticalAlignment = VerticalAlignment.Center };
                DockPanel.SetDock(lbl, Dock.Left);
                hdr.Children.Add(lbl);
                var valTxt = new TextBlock { Text = string.Format(CultureInfo.InvariantCulture, fmt, defVal) + " " + unit, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = UiTheme.Ink, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, FontFamily = new FontFamily("Cascadia Mono, Consolas") };
                DockPanel.SetDock(valTxt, Dock.Right);
                hdr.Children.Add(valTxt);
                row.Children.Add(hdr);

                var sld = new Slider { Minimum = min, Maximum = max, Value = defVal, SmallChange = step, LargeChange = step * 2, IsMoveToPointEnabled = true, Margin = new Thickness(0, 2, 0, 0) };
                sld.ValueChanged += (_, _) =>
                {
                    double rounded = Math.Round(sld.Value / step) * step;
                    valTxt.Text = string.Format(CultureInfo.InvariantCulture, fmt, rounded) + " " + unit;
                    onVal(rounded);
                };
                row.Children.Add(sld);
                return row;
            }

            fxPanel.Children.Add(MakeSliderRow(L.T("Gain (dB)"), -30, 30, 0.5, 0, "dB", "{0:+0.0;-0.0;0.0}", v => Set("gainDb", v.ToString(CultureInfo.InvariantCulture))));
            fxPanel.Children.Add(MakeSliderRow(L.T("Fade In (s)"), 0, 10, 0.25, 0, "s", "{0:0.0}", v => Set("fadeIn", v.ToString(CultureInfo.InvariantCulture))));
            fxPanel.Children.Add(MakeSliderRow(L.T("Fade Out (s)"), 0, 10, 0.25, 0, "s", "{0:0.0}", v => Set("fadeOut", v.ToString(CultureInfo.InvariantCulture))));

            var denoiseCheck = new CheckBox { Content = L.T("Reduce Background Noise"), Margin = new Thickness(0, 4, 0, 6), FontSize = 12 };
            denoiseCheck.Checked += (_, _) => Set("noiseReduction", "true");
            denoiseCheck.Unchecked += (_, _) => Set("noiseReduction", "false");
            fxPanel.Children.Add(denoiseCheck);

            var previewRow = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 4, 0, 4) };
            fxPlayButton = Button(L.T("Play Preview"), async () => await ToggleAudioEffectsPreview());
            var playBtn = fxPlayButton;
            playBtn.Padding = new Thickness(14, 5, 14, 5);
            playBtn.FontSize = 11;
            DockPanel.SetDock(playBtn, Dock.Left);
            previewRow.Children.Add(playBtn);
            fxPanel.Children.Add(previewRow);

            parameters.Children.Add(fxPanel);
        }
        else if (operation.Id == "ringtone")
        {
            parameters.Children.Clear();
            var ringPanel = new StackPanel { FlowDirection = FlowDirection.LeftToRight };

            var fileRow = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 4, 0, 6) };
            var nameBlock = new TextBlock { Text = Path.GetFileName(paths[0]), FontSize = 11, Foreground = UiTheme.Muted, MaxWidth = 280, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(nameBlock, Dock.Left);
            fileRow.Children.Add(nameBlock);
            var durBlock = new TextBlock { Text = TimeField.FormatTime(totalDuration), FontSize = 11, Foreground = UiTheme.Muted, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, FontFamily = new FontFamily("Cascadia Mono, Consolas") };
            DockPanel.SetDock(durBlock, Dock.Right);
            fileRow.Children.Add(durBlock);
            ringPanel.Children.Add(fileRow);

            var rail = transport!.TakeRail();
            rail.Height = 70;
            rail.RangeEnabled = true;
            var railBorder = new Border { Background = new SolidColorBrush(Color.FromRgb(19, 21, 26)), CornerRadius = new CornerRadius(8), Child = rail, Margin = new Thickness(0, 0, 0, 4) };
            ringPanel.Children.Add(railBorder);

            var lenRow = new StackPanel { Margin = new Thickness(0, 4, 0, 6) };
            var lenHdr = new DockPanel { LastChildFill = true };
            var lenLbl = new TextBlock { Text = L.T("Length (s)"), FontSize = 12, Foreground = UiTheme.Ink, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(lenLbl, Dock.Left);
            lenHdr.Children.Add(lenLbl);
            var lenValTxt = new TextBlock { Text = "30 s", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = UiTheme.Ink, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, FontFamily = new FontFamily("Cascadia Mono, Consolas") };
            DockPanel.SetDock(lenValTxt, Dock.Right);
            lenHdr.Children.Add(lenValTxt);
            lenRow.Children.Add(lenHdr);

            var lenSld = new Slider { Minimum = 1, Maximum = 40, Value = 30, SmallChange = 1, LargeChange = 5, IsMoveToPointEnabled = true, Margin = new Thickness(0, 2, 0, 0) };
            lenSld.ValueChanged += (_, _) =>
            {
                double v = Math.Round(lenSld.Value);
                lenValTxt.Text = $"{v:0} s";
                Set("length", v.ToString(CultureInfo.InvariantCulture));
            };
            lenRow.Children.Add(lenSld);
            ringPanel.Children.Add(lenRow);

            var startRow = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 4, 0, 6) };
            var startLbl = new TextBlock { Text = L.T("Start"), FontSize = 12, Foreground = UiTheme.Ink, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
            DockPanel.SetDock(startLbl, Dock.Left);
            startRow.Children.Add(startLbl);
            var startField = new TimeField(0, v => Set("start", v.ToString("0.000", CultureInfo.InvariantCulture)));
            DockPanel.SetDock(startField, Dock.Left);
            startRow.Children.Add(startField);
            ringPanel.Children.Add(startRow);

            var caption = new TextBlock
            {
                Text = L.T("Creates an iPhone-compatible ringtone (.m4r) with an automatic fade-out at the end."),
                FontSize = 11, Foreground = UiTheme.Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 8)
            };
            ringPanel.Children.Add(caption);

            parameters.Children.Add(ringPanel);
        }
        else if (operation.Id == "joinAudio")
        {
            parameters.Children.Clear();
            var jaPanel = new StackPanel { FlowDirection = FlowDirection.LeftToRight };

            var captionText = new TextBlock { Text = L.T("Playback order"), FontSize = 12, Foreground = UiTheme.Muted, Margin = new Thickness(0, 4, 0, 8) };
            jaPanel.Children.Add(captionText);

            order.Height = 180;
            jaPanel.Children.Add(order);

            var reorderRow = new WrapPanel { Margin = new Thickness(0, 8, 0, 4) };
            reorderRow.Children.Add(Button(L.T("上移"), () => { MoveItem(-1); return Task.CompletedTask; }));
            reorderRow.Children.Add(Button(L.T("下移"), () => { MoveItem(1); return Task.CompletedTask; }));
            jaPanel.Children.Add(reorderRow);

            parameters.Children.Add(jaPanel);
        }        if (TrimAudio)
        {
            var startBox = (TextBox)controls["start"];
            var endBox = (TextBox)controls["end"];
            var silent = (CheckBox)controls["removeSilence"];
            parameters.Children.Clear();
            var trimPanel = new StackPanel();
            var trimHeader = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 4) };
            trimSelected = new TextBlock { Foreground = UiTheme.Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(trimSelected, Dock.Right);
            trimHeader.Children.Add(trimSelected);
            trimHeader.Children.Add(new TextBlock { Text = L.Arabic ? "اسحب الأطراف للقص" : "Drag the ends to trim", Foreground = UiTheme.Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center });
            trimPanel.Children.Add(trimHeader);
            var rail = transport!.TakeRail();
            rail.Height = 72;
            var waveCard = new Border { Background = new SolidColorBrush(Color.FromRgb(19, 21, 26)), CornerRadius = new CornerRadius(10), Padding = new Thickness(0, 2, 0, 2), Child = rail };
            trimPanel.Children.Add(waveCard);
            startBox.MinWidth = 0;
            endBox.MinWidth = 0;
            startBox.MinHeight = 30;
            endBox.MinHeight = 30;
            startBox.Padding = new Thickness(8, 4, 8, 4);
            endBox.Padding = new Thickness(8, 4, 8, 4);
            startBox.FontSize = 12;
            endBox.FontSize = 12;
            var fields = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            fields.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            fields.ColumnDefinitions.Add(new ColumnDefinition());
            fields.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            fields.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            fields.ColumnDefinitions.Add(new ColumnDefinition());
            var startLabel = new TextBlock { Text = L.Arabic ? "البداية" : "Start", Foreground = UiTheme.Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
            fields.Children.Add(startLabel);
            Grid.SetColumn(startBox, 1);
            fields.Children.Add(startBox);
            var endLabel = new TextBlock { Text = L.Arabic ? "النهاية" : "End", Foreground = UiTheme.Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
            Grid.SetColumn(endLabel, 3);
            fields.Children.Add(endLabel);
            Grid.SetColumn(endBox, 4);
            fields.Children.Add(endBox);
            trimPanel.Children.Add(fields);
            startBox.TextChanged += (_, _) => UpdateTrimSelection();
            endBox.TextChanged += (_, _) => UpdateTrimSelection();
            silent.Content = L.Arabic ? "قص الصمت من الطرفين تلقائياً" : "Trim silent ends automatically";
            silent.Margin = new Thickness(0, 8, 0, 0);
            silent.MinHeight = 24;
            silent.Padding = new Thickness(4, 2, 4, 2);
            silent.FontSize = 12;
            trimPanel.Children.Add(silent);
            var play = Button(L.Arabic ? "تشغيل المقطع المحدد" : "Play Selected Range", PlayTrimRange);
            trimPlayButton = play;
            play.Background = Brushes.Transparent;
            play.Foreground = UiTheme.Accent;
            play.BorderBrush = new SolidColorBrush(Color.FromArgb(110, 255, 82, 0));
            play.BorderThickness = new Thickness(1);
            play.HorizontalAlignment = HorizontalAlignment.Right;
            play.Margin = new Thickness(0, 10, 0, 0);
            play.MinHeight = 30;
            play.Padding = new Thickness(10, 5, 10, 5);
            play.FontSize = 12;
            RefreshTrimPlayButton();
            trimPanel.Children.Add(play);
            var trimCard = UiTheme.Card(trimPanel, new Thickness(12));
            trimCard.Margin = new Thickness(0, 8, 0, 0);
            left.Children.Add(trimCard);
        }
        else
        {
            var settingsScroll = new ScrollViewer { Content = parameters, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            if (Simple)
                parameters.Margin = new Thickness(0, 0, 0, 10);
            if (Simple)
                settingsScroll.MaxHeight = Math.Max(120, SystemParameters.WorkArea.Height * 0.92 - (VisualPreview ? 400 : 190));
            if (MediaCompact)
                settingsScroll.MaxHeight = Math.Max(200, SystemParameters.WorkArea.Height * 0.92 - (previewHost.Height > 0 ? previewHost.Height + 140 : 140));
            var scroll = MediaCompact ? (FrameworkElement)settingsScroll : UiTheme.Card(settingsScroll, Compact ? new Thickness(2, 4, 2, 4) : new Thickness(14, 10, 14, 10));
            if (Cropping && !MediaCompact || Rotating)
            {
                // Compact horizontal layout directly beneath the preview!
                scroll.Margin = new Thickness(0, 10, 0, 0);
                left.Children.Add(scroll);
            }
            else if (Compact || MediaCompact)
            {
                scroll.Margin = new Thickness(0, 6, 0, 0);
                Grid.SetRow(scroll, 1);
                body.Children.Add(scroll);
            }
            else
            {
                Grid.SetColumn(scroll, 1);
                body.Children.Add(scroll);
            }
        }
        var bottom = new Grid { Margin = MediaCompact ? new Thickness(0, 10, 0, 0) : TrimAudio ? new Thickness(0, 8, 0, 0) : new Thickness(0, 18, 0, 0) };
        bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bottom.ColumnDefinitions.Add(new ColumnDefinition());
        bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        if (MediaCompact)
        {
            bottom.MinHeight = 52;
            var hairline = new Border { Height = 1, Background = UiTheme.Hairline, VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Stretch };
            Grid.SetColumnSpan(hairline, 3);
            bottom.Children.Add(hairline);
        }
        var resetButton = Button(L.T("重置"), () =>
        {
            if (Cropping)
            {
                ResetCrop();
                status.Text = "";
                return Task.CompletedTask;
            }
            var defaults = (operation.Fields ?? []).Where(f => f.Kind != "password").ToDictionary(f => f.Name, f => f.Default);
            ApplyValues(defaults);
            foreach (var sync in syncSliderCallbacks) sync();
            syncFrameExtras?.Invoke();
            ParameterChanged();
            status.Text = "";
            return Task.CompletedTask;
        });
        resetButton.Background = UiTheme.Tint;
        resetButton.BorderThickness = new Thickness(0);
        resetButton.Padding = new Thickness(16, 8, 16, 8);
        if (MediaCompact)
        {
            if (operation.Id is "compress" or "normalizeAudio")
                resetButton.Content = L.T("Restore Defaults");
            else
                resetButton.Content = L.T("Reset");
            if (operation.Id is "trimVideo" or "changeVideoSpeed" or "videoSnapshots" or "joinVideos" or "joinAudio" or "audioToVideo" or "ringtone" or "subtitlesAudio")
                resetButton.Visibility = Visibility.Collapsed;
        }
        else if (!Cropping && (operation.Fields?.Length ?? 0) == 0)
            resetButton.Visibility = Visibility.Collapsed;
        Grid.SetColumn(resetButton, 0);
        bottom.Children.Add(resetButton);
        var feedback = new StackPanel { Margin = TrimAudio ? new Thickness(0, 0, 12, 0) : new Thickness(0, 0, 20, 0), VerticalAlignment = VerticalAlignment.Center };
        status.Text = "";
        status.TextWrapping = TrimAudio ? TextWrapping.NoWrap : TextWrapping.Wrap;
        status.TextTrimming = TextTrimming.CharacterEllipsis;
        status.Foreground = UiTheme.Muted;
        var statusStyle = new Style(typeof(TextBlock));
        var empty = new Trigger { Property = TextBlock.TextProperty, Value = "" };
        empty.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Collapsed));
        statusStyle.Triggers.Add(empty);
        status.Style = statusStyle;
        feedback.Children.Add(status);
        Grid.SetColumn(feedback, 1);
        bottom.Children.Add(feedback);
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        Grid.SetColumn(actions, 2);
        bottom.Children.Add(actions);
        revealButton = Button(L.T("打开输出位置"), () => { if (lastOutput != null) ExplorerIntegration.Reveal(lastOutput); return Task.CompletedTask; });
        revealButton.Visibility = Visibility.Collapsed;
        actions.Children.Add(revealButton);
        copyButton = Button(L.T("复制结果"), () => { CopyResult(); return Task.CompletedTask; });
        copyButton.Visibility = Visibility.Collapsed;
        actions.Children.Add(copyButton);
        cancelButton = Button(L.T("取消处理"), () => { saving?.Cancel(); cancelButton.IsEnabled = false; status.Text = L.T("正在停止处理…"); return Task.CompletedTask; });
        cancelButton.Visibility = Visibility.Collapsed;
        actions.Children.Add(cancelButton);
        string saveText = MediaCompact ? operation.Id switch
        {
            "trimVideo" or "cropVideo" or "changeVideoSpeed" or "videoSettings" or "videoEffects" or "subtitlesAudio" or "rotateVideo" or "muteVideo" or "audioChannels" => L.T("Apply"),
            "splitVideo" => L.T("Split Video"),
            "videoSnapshots" => L.T("Save Frames"),
            "redactVideo" => L.T("Save Redacted Copy"),
            "joinVideos" => L.T("Join Videos"),
            "compress" => L.T("Compress"),
            "normalizeAudio" => L.T("Normalize Audio"),
            "audioToVideo" => L.T("Audio Visualizer"),
            "redactAudio" => L.T("Save Bleeped Copy"),
            "audioEffects" => L.T("Audio Effects"),
            "ringtone" => L.T("Ringtone"),
            "joinAudio" => L.T("Join Audio"),
            _ => L.T("保存新文件")
        } : L.T("保存新文件");
        saveButton = Button(saveText, Save);
        saveButton.IsEnabled = false;
        saveButton.Foreground = Brushes.White;
        saveButton.Background = UiTheme.Accent;
        saveButton.BorderThickness = new Thickness(0);
        saveButton.Padding = TrimAudio ? new Thickness(16, 8, 16, 8) : MediaCompact ? new Thickness(18, 6, 18, 6) : new Thickness(22, 10, 22, 10);
        if (TrimAudio || MediaCompact)
        {
            saveButton.FontSize = 12;
            if (MediaCompact)
                saveButton.Height = 28;
        }
        saveButton.ToolTip = "Ctrl + Enter";
        actions.Children.Add(saveButton);
        Grid.SetRow(bottom, 2);
        root.Children.Add(bottom);
        return root;
    }
    private FrameworkElement CropSliderRow(string name, TextBlock valueText)
    {
        var row = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });
        row.Children.Add(new TextBlock { Text = name == "width" ? (L.Arabic ? "العرض" : "Width") : (L.Arabic ? "الارتفاع" : "Height"), VerticalAlignment = VerticalAlignment.Center });
        var slider = new Slider { Minimum = 1, Maximum = 100, IsSnapToTickEnabled = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 6, 0) };
        Grid.SetColumn(slider, 1);
        row.Children.Add(slider);
        Grid.SetColumn(valueText, 2);
        row.Children.Add(valueText);
        if (name == "width") cropWidthSlider = slider; else cropHeightSlider = slider;
        slider.ValueChanged += (_, _) =>
        {
            if (!loaded || syncingCropSliders || originalWidth <= 0)
                return;
            editedField = name;
            int full = name == "width" ? originalWidth : originalHeight;
            int pixels = Math.Max(1, (int)Math.Round(slider.Value / 100.0 * full));
            Set(name, pixels.ToString(CultureInfo.InvariantCulture));
            transport?.InvalidateEffect();
        };
        return row;
    }
    private static string Choice(string value) => value switch { "crop" => L.T("自动裁剪，去除空白边角"), "expand" => L.T("放大画布，保留整张画面"), "keep" => L.T("保持原尺寸（允许空白角）"), "none" => L.T("不翻转"), "horizontal" => L.T("左右翻转"), "vertical" => L.T("上下翻转"), "percent" => L.T("按比例"), "edge" => L.T("按最长边"), "size" => L.T("指定宽高"), "single" => L.T("单个"), "tiled" => L.T("平铺"), "original" => L.T("保持原样"), "add" => L.T("添加密码"), "remove" => L.T("移除密码"), "strip" => L.T("移除位置信息"), "set" => L.T("改为新的位置"), "bottomRight" => L.T("右下"), "bottomCenter" => L.T("下中"), "bottomLeft" => L.T("左下"), "middleRight" => L.T("右中"), "center" => L.T("正中"), "middleLeft" => L.T("左中"), "topRight" => L.T("右上"), "topCenter" => L.T("上中"), "topLeft" => L.T("左上"), "solid" => L.T("纯色遮盖"), "blur" => L.T("模糊"), "pixelate" => L.T("马赛克"), "free" => L.T("自由"), "custom" => L.T("自定义"), "grid" => L.T("网格"), "row" => L.T("一行"), "column" => L.T("一列"), "featured" => L.T("主图布局"), "landscape" => L.T("横向"), "portrait" => L.T("纵向"), "square" => L.T("正方形"), "mono" => L.T("单声道"), "stereo" => L.T("双声道"), _ => value };
    private static Button Button(string text, Func<Task> click)
    {
        var button = new Button { Content = text, HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 0, 6, 5) };
        button.Click += async (_, _) => await click();
        return button;
    }
    private async Task Initialize()
    {
        using var busy = busyLine.Begin();
        loaded = false;
        ResetCropZoom();
        editSourceBgra = null;
        editTargetBgra = null;
        editLiveBitmap = null;
        saveButton.IsEnabled = false;
        framePicker.IsEnabled = false;
        previewDelay.Stop();
        previewCancellation?.Cancel();
        previewVersion++;
        rangeEdited = false;
        if (operation.Ordered && order.Items.Count == 0)
            foreach (string path in paths)
                order.Items.Add(new ListBoxItem { Content = Path.GetFileName(path), Tag = path });
        try
        {
            var info = await Backend.Inspect(paths[0], folder, closing.Token, imageFrame);
            originalWidth = info.GetProperty("Width").GetInt32();
            originalHeight = info.GetProperty("Height").GetInt32();
            totalDuration = info.GetProperty("Duration").GetDouble();
            pages = info.GetProperty("Pages").GetInt32();
            double rate = info.GetProperty("FrameRate").GetDouble();
            if (rate > 0)
                fps = rate;
            originalPreview = info.GetProperty("Preview").GetString();
            if (originalPreview != null)
            { SetImage(originalPreview); showingOriginal = true; }
            transport?.SetWaveform(originalPreview);
            if (paths.Length == 1 && info.TryGetProperty("Frames", out var frames) && frames.GetInt32() > 1)
            {
                if (framePicker.Items.Count == 0)
                { for (int i = 0; i < frames.GetInt32(); i++) framePicker.Items.Add(new ComboBoxItem { Content = L.F("第 {0} 帧", i + 1), Tag = i }); framePicker.SelectedIndex = info.GetProperty("Frame").GetInt32(); }
                framePicker.Visibility = Visibility.Visible;
                imageFrame = info.GetProperty("Frame").GetInt32();
            }
            if (originalWidth > 0 && operation.Id != "resizeImage")
            {
                Set("x", "0");
                Set("y", "0");
                Set("width", originalWidth.ToString());
                Set("height", originalHeight.ToString());
                details.Text = L.F("{0} × {1} 像素", originalWidth, originalHeight);
                if (Cropping)
                {
                    cropFrame.SetSource(originalWidth, originalHeight);
                    WriteCropFields();
                    UpdateCropPreviewSize();
                }
            }
            if (totalDuration > 0)
            { timeline.Maximum = Math.Max(.001, totalDuration - .001); transport?.SetDuration(totalDuration); Set("end", totalDuration.ToString("0.000", CultureInfo.InvariantCulture)); details.Text += L.F("  · {0:0.00} 秒", totalDuration); if (Catalog.Category(paths[0]) == "video") details.Text += $" · {fps:0.##} fps"; positionLabel.Text = L.F("预览位置  {0:0.000} / {1:0.000} 秒", timeline.Value, totalDuration); }
            metadataText = JsonSerializer.Serialize(info.GetProperty("Metadata"), new JsonSerializerOptions { WriteIndented = true });
            Set("metadata", metadataText);
            if (controls.TryGetValue("metadata", out var editor) && editor is MetadataEditor table)
                table.Populate(info.GetProperty("Metadata"));
            if (operation.Id == "removeMetadata")
                details.Text = L.T("可搜索字段；关闭“移除全部”后可编辑元数据。");
            if (operation.Id == "location")
            {
                string? place = info.TryGetProperty("Location", out var where) && where.ValueKind == JsonValueKind.String ? where.GetString() : null;
                details.Text = (paths.Length > 1 ? L.T("第一个文件：") : "") + (place == null ? L.T("没有位置信息。") : L.F("当前位置：{0}", place));
            }
            if (operation.Id == "organizePDF")
            {
                for (int i = 1; i <= pages; i++)
                    order.Items.Add(new ListBoxItem { Content = L.F("第 {0} 页", i), Tag = i });
                Set("pageOrder", string.Join(",", Enumerable.Range(1, pages)));
                details.Text = L.F("共 {0} 页；调整列表或输入页码顺序。", pages);
            }
            status.Text = imageFrame >= 0 ? L.T("此图片包含多个图像帧。仅导出当前帧，原始文件保留。") : Cropping && Saved == null ? "" : ReadyText;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { status.Text = L.T("预览不可用：") + ex.Message + L.T("。仍可填写参数。"); }
        if (Presets.Supported(operation.Id) && Presets.Last(operation.Id) is { } remembered)
            ApplyValues(remembered);
        loaded = true;
        UpdateRotationPreview();
        saveButton.IsEnabled = true;
        framePicker.IsEnabled = true;
        SetMetadataState();
        SyncTrimRange();
        UpdateTrimSelection();
        UpdateCropVisibility();
        if (operation.Id == "trimVideo" && transport != null && totalDuration > 0)
        {
            _ = transport.Rail.LoadThumbnails(paths[0], totalDuration, closing.Token);
        }
        if (operation.Id == "changeVideoSpeed")
        {
            saveButton.IsEnabled = Math.Abs(double.Parse(Get("speed", "1.0"), CultureInfo.InvariantCulture) - 1.0) > 0.001;
        }
        if (operation.Id == "redactPDF")
            SchedulePreview();
    }
    private int CurrentPage() => Math.Clamp(int.TryParse(Get("pageNumber", "1"), out int page) ? page : 1, 1, Math.Max(1, pages));
    private void TurnPage(int by) => Set("pageNumber", Math.Clamp(CurrentPage() + by, 1, Math.Max(1, pages)).ToString());
    private void Set(string name, string value)
    {
        if (!controls.TryGetValue(name, out var control))
            return;
        if (control is TextBox text)
        {
            if (text.IsKeyboardFocused)
                return;
            string shown = TimeText(name, value);
            text.Text = shown;
            if (IsTimeField(name))
                text.Tag = shown;
        }
        if (control is CheckBox check)
            check.IsChecked = value == "true";
        if (control is ComboBox combo)
            foreach (ComboBoxItem item in combo.Items)
                if (item.Tag?.ToString() == value)
                    combo.SelectedItem = item;
    }
    private string Get(string name, string fallback = "") => controls.TryGetValue(name, out var control) ? control switch { MetadataEditor table => table.ToJson(), TextBox text => text.Text, CheckBox check => check.IsChecked == true ? "true" : "false", ComboBox combo => ((ComboBoxItem?)combo.SelectedItem)?.Tag?.ToString() ?? fallback, PasswordBox pass => pass.Password, _ => fallback } : fallback;
    private ConversionJob Capture()
    {
        if (Cropping && cropFrame.HasSource)
        {
            // The frame is the truth: typed values that disagree with it (out of bounds, wrong ratio) are replaced by it.
            cropFrame.Ratio = CurrentRatio();
            WriteCropFields();
        }
        var values = new Dictionary<string, string>();
        foreach (var field in operation.Fields ?? [])
            values[field.Name] = Get(field.Name, field.Default);
        // The backend expects plain seconds; the boxes may hold "1:23.45".
        foreach (string name in new[] { "start", "end" })
            if (values.TryGetValue(name, out var rawTime) && !string.IsNullOrWhiteSpace(rawTime))
                values[name] = JobValidation.Time(rawTime).ToString("0.######", CultureInfo.InvariantCulture);
        if (Rotating && double.TryParse(values["angle"], NumberStyles.Float, CultureInfo.InvariantCulture, out double rotation))
        {
            if (!double.IsFinite(rotation) || rotation is < -180 or > 180)
                throw new ArgumentException(L.T("旋转角度需在 −180° 到 +180° 之间。"));
            values["angle"] = RotationDial.BackendAngle(rotation).ToString("0.#", CultureInfo.InvariantCulture);
        }
        if (values.TryGetValue("ratio", out var selectedRatio) && selectedRatio != "custom")
        { values["ratioWidth"] = "3"; values["ratioHeight"] = "2"; }
        if (values.TryGetValue("style", out var selectedStyle) && selectedStyle != "pixelate")
            values["blockSize"] = "12";
        JobValidation.ResolveRatio(values);
        foreach (var field in operation.Fields ?? [])
            if (field.Kind is "number" or "slider" && !string.IsNullOrWhiteSpace(values[field.Name]) && !values[field.Name].Contains(':') && !double.TryParse(values[field.Name], NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                throw new ArgumentException(field.Label + L.T("需要数字"));
        values["fps"] = fps.ToString(CultureInfo.InvariantCulture);
        if (!values.ContainsKey("time"))
            values["time"] = timeline.Value.ToString(CultureInfo.InvariantCulture);
        if (regions.Count > 0)
            values["regions"] = JsonSerializer.Serialize(regions);
        if (audioRanges.Count > 0)
            values["ranges"] = JsonSerializer.Serialize(audioRanges);
        if (operation.Id is "redactImage" or "redactVideo" && regions.Count == 0 && string.IsNullOrWhiteSpace(Get("regions")) && !rangeEdited)
            throw new ArgumentException(L.T("请先在预览上选择覆盖区域。"));
        if (operation.Id == "redactAudio" && audioRanges.Count == 0 && string.IsNullOrWhiteSpace(Get("ranges")) && !rangeEdited)
            throw new ArgumentException(L.T("请先在波形上选择蜂鸣时段。"));
        string[] ordered = operation.Ordered ? order.Items.Cast<ListBoxItem>().Select(x => (string)x.Tag).ToArray() : paths;
        if (ordered.Length == 0)
            throw new ArgumentException(L.T("至少需要选择一个文件"));
        if (operation.Id == "organizePDF")
        {
            string generated = string.Join(",", order.Items.Cast<ListBoxItem>().Select(x => x.Tag));
            string initial = string.Join(",", Enumerable.Range(1, pages));
            if (order.Items.Count == 0 && values["pageOrder"] == initial)
                throw new ArgumentException(L.T("至少需要保留一页。"));
            if (values["pageOrder"] == initial)
                values["pageOrder"] = generated;
            if (order.SelectedItem is ListBoxItem selected)
                values["pageNumber"] = selected.Tag.ToString()!;
        }
        if (imageFrame >= 0)
            values["imageFrame"] = imageFrame.ToString();
        var job = new ConversionJob(ordered, operation.Id, values);
        JobValidation.Check(job, originalWidth, originalHeight, totalDuration, pages);
        return job;
    }
    private void SchedulePreview() { previewDelay.Stop(); previewDelay.Start(); }
    private async Task UpdatePreview()
    {
        if (!loaded || closing.IsCancellationRequested || saving != null || selectionStart != null)
            return;
        int version = ++previewVersion;
        previewCancellation?.Cancel();
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(closing.Token);
        previewCancellation = cancellation;
        status.Text = L.T("更新效果中…");
        using var busy = busyLine.Begin();
        try
        {
            await previewGate.WaitAsync(cancellation.Token);
            try
            {
                var info = await Backend.Preview(Capture(), folder, cancellation.Token);
                if (version != previewVersion || cropFrame.IsDragging)
                    return;
                if (info.TryGetProperty("SourcePreview", out var source))
                    originalPreview = source.GetString();
                // A PDF page reports its size in points, which is the unit the drawn areas use.
                if (info.TryGetProperty("PageWidth", out var pageWidth))
                {
                    originalWidth = (int)Math.Round(pageWidth.GetDouble());
                    originalHeight = (int)Math.Round(info.GetProperty("PageHeight").GetDouble());
                    pages = info.GetProperty("Pages").GetInt32();
                    details.Text = L.F("第 {0} 页，共 {1} 页", info.GetProperty("Page").GetInt32(), pages);
                }
                var preview = info.GetProperty("Preview").GetString();
                if (preview != null)
                { player.Visibility = Visibility.Collapsed; player.Stop(); SetImage(preview); selection.Visibility = Visibility.Collapsed; showingOriginal = false; if (compareButton != null) compareButton.Content = L.T("查看原始"); status.Text = L.T("预览已更新；保存后生成新文件。"); UpdateCropVisibility(); }
            }
            finally { previewGate.Release(); }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (version == previewVersion) status.Text = L.T("预览：") + ex.Message; }
        finally { cancellation.Dispose(); if (ReferenceEquals(previewCancellation, cancellation)) previewCancellation = null; }
    }
    private void UpdateRotationPreview()
    {
        double width = previewHost.ActualWidth, height = previewHost.ActualHeight;
        if (!Rotating || originalWidth == 0 || width <= 0 || height <= 0)
            return;
        if (!double.TryParse(Get("angle", "0"), NumberStyles.Float, CultureInfo.InvariantCulture, out double angle) || !double.IsFinite(angle)) return;
        angle = Math.Clamp(angle, -180, 180);
        string flip = Get("flip", "none");
        string canvas = Get("expand", HasMedia ? "expand" : "crop");
        bool quarter = angle % 90 == 0, keep = !quarter && canvas == "keep", cropEmpty = !quarter && !HasMedia && canvas == "crop";
        // The picture is shown fitted to the preview; the rotated picture (or, when the size is kept, the original frame) is then fitted again.
        double fit = Math.Min(width / originalWidth, height / originalHeight), shownWidth = originalWidth * fit, shownHeight = originalHeight * fit;
        double radians = angle * Math.PI / 180, cos = Math.Abs(Math.Cos(radians)), sin = Math.Abs(Math.Sin(radians));
        var cropped = cropEmpty ? RotationGeometry.CropSize(originalWidth, originalHeight, angle) : (Width: originalWidth, Height: originalHeight);
        double boxWidth = cropEmpty ? cropped.Width * fit : keep ? shownWidth : shownWidth * cos + shownHeight * sin;
        double boxHeight = cropEmpty ? cropped.Height * fit : keep ? shownHeight : shownWidth * sin + shownHeight * cos;
        double zoom = keep ? 1 : Math.Min(width / boxWidth, height / boxHeight);
        var turn = new TransformGroup();
        turn.Children.Add(new RotateTransform(angle));
        turn.Children.Add(new ScaleTransform(flip == "horizontal" ? -1 : 1, flip == "vertical" ? -1 : 1));
        turn.Children.Add(new ScaleTransform(zoom, zoom));
        foreach (FrameworkElement element in new FrameworkElement[] { image, player })
        { element.RenderTransformOrigin = new Point(.5, .5); element.RenderTransform = turn; }
        Color fill = UiTheme.LineColor;   // empty corners of a picture without a fill colour: transparent in the file
        if (Path.GetExtension(paths[0]).ToLowerInvariant() is ".jpg" or ".jpeg" or ".bmp") fill = Colors.White;
        try { if (!string.IsNullOrWhiteSpace(Get("background")) && ColorConverter.ConvertFromString(Get("background").Trim()) is Color chosen) fill = chosen; }
        catch (FormatException) { }
        rotatePlate.Visibility = quarter || cropEmpty ? Visibility.Collapsed : Visibility.Visible;
        rotatePlate.Width = boxWidth * zoom;
        rotatePlate.Height = boxHeight * zoom;
        rotatePlate.Background = new SolidColorBrush(fill);
        // Keeping the original size cuts off whatever leaves the original frame.
        rotateStage.Clip = cropEmpty ? new RectangleGeometry(new Rect((width - boxWidth * zoom) / 2, (height - boxHeight * zoom) / 2, boxWidth * zoom, boxHeight * zoom))
            : keep ? new RectangleGeometry(new Rect((width - shownWidth) / 2, (height - shownHeight) / 2, shownWidth, shownHeight)) : null;
        if (!HasMedia)
        {
            int outWidth = cropEmpty ? cropped.Width : keep ? originalWidth : (int)Math.Round(originalWidth * cos + originalHeight * sin);
            int outHeight = cropEmpty ? cropped.Height : keep ? originalHeight : (int)Math.Round(originalWidth * sin + originalHeight * cos);
            details.Text = L.F("{0} × {1} 像素", originalWidth, originalHeight) + (outWidth == originalWidth && outHeight == originalHeight ? "" : L.F(" → {0} × {1} 像素", outWidth, outHeight));
        }
    }
    private void ApplyValues(Dictionary<string, string> values)
    {
        foreach (var (name, value) in values)
            if (Presets.Keeps(name))
                Set(name, value);
        foreach (var sync in syncSliderCallbacks)
            sync();
        syncFrameExtras?.Invoke();
    }
    private Dictionary<string, string> PresetValues() => (operation.Fields ?? []).Where(f => Presets.Keeps(f.Name) && f.Kind != "password").ToDictionary(f => f.Name, f => Get(f.Name, f.Default));
    private void AddPresetBar(Panel parameters)
    {
        var names = new ComboBox { MinWidth = 120, Margin = new Thickness(0, 0, 6, 0) };
        void Reload()
        {
            names.Items.Clear();
            names.Items.Add(new ComboBoxItem { Content = L.T("预设…"), Tag = "" });
            foreach (string name in Presets.Names(operation.Id))
                names.Items.Add(new ComboBoxItem { Content = name, Tag = name });
            names.SelectedIndex = 0;
        }
        names.SelectionChanged += (_, _) =>
        {
            if (names.SelectedItem is ComboBoxItem { Tag: string name } && name.Length > 0 && Presets.Named(operation.Id, name) is { } values)
            { ApplyValues(values); ParameterChanged(); status.Text = L.F("已应用预设“{0}”。", name); }
        };
        var row = new WrapPanel { Margin = new Thickness(0, 0, 0, 4) };
        row.Children.Add(names);
        row.Children.Add(Button(L.T("保存为预设"), () =>
        {
            string? name = PromptName();
            if (!string.IsNullOrWhiteSpace(name))
            { Presets.SaveNamed(operation.Id, name.Trim(), PresetValues()); Reload(); status.Text = L.F("已保存预设“{0}”。", name.Trim()); }
            return Task.CompletedTask;
        }));
        row.Children.Add(Button(L.T("删除预设"), () =>
        {
            if (names.SelectedItem is ComboBoxItem { Tag: string name } && name.Length > 0)
            { Presets.Delete(operation.Id, name); Reload(); }
            return Task.CompletedTask;
        }));
        Reload();
        parameters.Children.Insert(Math.Min(1, parameters.Children.Count), row);
    }
    private string? PromptName()
    {
        var box = new TextBox { Padding = new Thickness(8), Margin = new Thickness(0, 0, 0, 12) };
        var dialog = new Window { Title = L.T("预设名称"), Owner = host ?? this, Width = 340, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Background = Background, FontFamily = FontFamily };
        var ok = new Button { Content = L.T("保存"), IsDefault = true, Padding = new Thickness(18, 7, 18, 7), HorizontalAlignment = HorizontalAlignment.Right };
        ok.Click += (_, _) => dialog.DialogResult = true;
        var stack = new StackPanel { Margin = new Thickness(18) };
        stack.Children.Add(new TextBlock { Text = L.T("给这组设置起个名字："), Margin = new Thickness(0, 0, 0, 8) });
        stack.Children.Add(box);
        stack.Children.Add(ok);
        dialog.Content = stack;
        dialog.Loaded += (_, _) => box.Focus();
        return dialog.ShowDialog() == true ? box.Text : null;
    }
    private void CopyResult()
    {
        if (lastOutput == null || !File.Exists(lastOutput))
            return;
        try
        {
            string extension = Path.GetExtension(lastOutput).ToLowerInvariant();
            if (extension is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".webp" or ".tiff" or ".ico")
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(lastOutput);
                bitmap.EndInit();
                Clipboard.SetImage(bitmap);
            }
            else if (extension == ".txt")
                Clipboard.SetText(File.ReadAllText(lastOutput));
            else
                Clipboard.SetFileDropList([lastOutput]);
            status.Text = L.T("已复制到剪贴板。");
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.ExternalException or IOException or NotSupportedException) { status.Text = L.T("复制失败：") + ex.Message; }
    }
    private void SetImage(string path)
    {
        using var stream = File.OpenRead(path);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        image.Source = bitmap;
        image.Visibility = Visibility.Visible;
        ResetCropZoom();
        if (operation.Id == "editImage" && editSourceBgra == null)
            PrepareEditBgra(bitmap);
    }
    private void ZoomCrop(object sender, MouseWheelEventArgs e)
    {
        if (!Cropping || cropZoom == null || cropZoomTransform == null || !cropFrame.HasSource || e.Delta == 0)
            return;
        Matrix matrix = cropZoomTransform.Matrix;
        double current = matrix.M11;
        double next = Math.Clamp(current * Math.Pow(1.15, e.Delta / 120.0), 1, 8);
        if (next <= 1.0001)
        {
            ResetCropZoom();
            e.Handled = true;
            return;
        }
        if (Math.Abs(next - current) < .0001)
        {
            e.Handled = true;
            return;
        }
        Point anchor = e.GetPosition(previewHost);
        double k = next / current;
        double tx = k * matrix.OffsetX + (1 - k) * anchor.X;
        double ty = k * matrix.OffsetY + (1 - k) * anchor.Y;
        tx = Math.Clamp(tx, cropZoom.ActualWidth * (1 - next), 0);
        ty = Math.Clamp(ty, cropZoom.ActualHeight * (1 - next), 0);
        cropZoomTransform.Matrix = new Matrix(next, 0, 0, next, tx, ty);
        e.Handled = true;
    }
    private void ResetCropZoom()
    {
        if (cropZoomTransform != null)
            cropZoomTransform.Matrix = Matrix.Identity;
    }
    private void FilterMetadata(string search)
    {
        if (controls.TryGetValue("metadata", out var editor) && editor is MetadataEditor table)
            table.Filter(search);
        details.Text = L.T("可搜索字段；关闭“移除全部”后可编辑元数据。");
    }
    private void MoveItem(int direction)
    {
        int index = order.SelectedIndex, next = index + direction;
        if (index < 0 || next < 0 || next >= order.Items.Count)
            return;
        var item = order.Items[index];
        order.Items.RemoveAt(index);
        order.Items.Insert(next, item);
        order.SelectedIndex = next;
    }
    private void AddCropFrame()
    {
        Panel host = cropZoom ?? previewHost;
        host.Children.Add(cropFrame);
        cropFrame.Visibility = Visibility.Collapsed;
        System.Windows.Automation.AutomationProperties.SetName(cropFrame, L.T("裁剪选区"));
        cropFrame.DragStarted += () =>
        {
            // Grabbing the frame means the result preview is out of date, and a request still in flight is of no use.
            cropFrame.Ratio = CurrentRatio();
            previewDelay.Stop();
            previewCancellation?.Cancel();
            previewVersion++;
            transport?.Pause();
            if (status.Text == L.T("更新效果中…"))
                status.Text = Cropping && Saved == null ? "" : ReadyText;
        };
        cropFrame.Changed += () => WriteCropFields();
        cropFrame.DragEnded += () => { WriteCropFields(); transport?.InvalidateEffect(); };
    }
    // The frame is shown over the original picture; the cropped result and the video's "result" view replace it.
    private void UpdateCropVisibility() =>
        cropFrame.Visibility = Cropping && loaded && cropFrame.HasSource && showingOriginal && transport?.EffectSelected != true ? Visibility.Visible : Visibility.Collapsed;
    private CropRatio? CurrentRatio()
    {
        string ratio = Get("ratio", "free");
        return CropRatio.Parse(ratio == "custom" ? Get("ratioWidth") + ":" + Get("ratioHeight") : ratio);
    }
    private bool TryReadCropFields(out CropRect rect)
    {
        rect = default;
        var numbers = new double[4];
        string[] names = ["x", "y", "width", "height"];
        for (int i = 0; i < 4; i++)
            if (!double.TryParse(Get(names[i]), NumberStyles.Float, CultureInfo.InvariantCulture, out numbers[i]) || !double.IsFinite(numbers[i]) || Math.Abs(numbers[i]) > int.MaxValue)
                return false;
        rect = new CropRect((int)numbers[0], (int)numbers[1], (int)numbers[2], (int)numbers[3]);
        return true;
    }
    // Typed values move the frame. Numbers that are still being typed (too small, not a number yet) are left alone.
    private void FieldsToCropFrame()
    {
        if (!cropFrame.HasSource || !TryReadCropFields(out var typed))
            return;
        var ratio = CurrentRatio();
        cropFrame.Ratio = ratio;
        int min = cropFrame.MinSize;
        if (typed.Width < min || typed.Height < min)
            return;
        var keep = ratio == null ? CropKeep.Nothing : editedField == "width" ? CropKeep.Width : editedField == "height" ? CropKeep.Height : CropKeep.Nothing;
        var next = CropMath.Constrain(typed, originalWidth, originalHeight, ratio, min, keep);
        if (next != cropFrame.Crop)
            cropFrame.SetCrop(next);
        // The box being typed in is left alone so the cursor does not jump; the others follow the frame.
        WriteCropFields(editedField);
        if (!showingOriginal && originalPreview != null)
            ShowOriginalWithFrame();
    }
    private void WriteCropFields(string? except = null)
    {
        if (!cropFrame.HasSource)
            return;
        var crop = cropFrame.Crop;
        bool was = syncingRegion;
        syncingRegion = true;
        try
        {
            if (except != "x") Set("x", crop.X.ToString(CultureInfo.InvariantCulture));
            if (except != "y") Set("y", crop.Y.ToString(CultureInfo.InvariantCulture));
            if (except != "width") Set("width", crop.Width.ToString(CultureInfo.InvariantCulture));
            if (except != "height") Set("height", crop.Height.ToString(CultureInfo.InvariantCulture));
        }
        finally { syncingRegion = was; }
        UpdateCropSliders(except);
    }
    private void UpdateCropSliders(string? except = null)
    {
        if (!Cropping || !cropFrame.HasSource || originalWidth <= 0 || originalHeight <= 0)
            return;
        var crop = cropFrame.Crop;
        double widthPercent = Math.Clamp(Math.Round(crop.Width * 100.0 / originalWidth), 1, 100);
        double heightPercent = Math.Clamp(Math.Round(crop.Height * 100.0 / originalHeight), 1, 100);
        bool was = syncingCropSliders;
        syncingCropSliders = true;
        try
        {
            if (except != "width" && cropWidthSlider != null)
                cropWidthSlider.Value = widthPercent;
            if (except != "height" && cropHeightSlider != null)
                cropHeightSlider.Value = heightPercent;
        }
        finally { syncingCropSliders = was; }
        if (cropWidthValue != null) cropWidthValue.Text = widthPercent.ToString("0") + "%";
        if (cropHeightValue != null) cropHeightValue.Text = heightPercent.ToString("0") + "%";
        if (cropSizeLabel != null) cropSizeLabel.Text = crop.Width + " × " + crop.Height + " px";
    }
    private void UpdateCropPreviewSize()
    {
        if (!Cropping || HasMedia || !cropFrame.HasSource || originalWidth <= 0 || originalHeight <= 0)
            return;
        previewHost.Height = 250;
        previewHost.Width = Math.Clamp(250.0 * originalWidth / originalHeight, 210, 420);
    }
    private void ShowOriginalWithFrame()
    {
        if (originalPreview != null)
        { player.Stop(); player.Visibility = Visibility.Collapsed; SetImage(originalPreview); }
        showingOriginal = true;
        if (compareButton != null)
            compareButton.Content = L.T("查看效果");
        UpdateCropVisibility();
    }
    private void ResetCrop()
    {
        if (!cropFrame.HasSource)
            return;
        bool was = syncingRegion;
        syncingRegion = true;
        try { Set("ratio", "free"); }
        finally { syncingRegion = was; }
        cropFrame.Ratio = null;
        cropFrame.SetCrop(new CropRect(0, 0, originalWidth, originalHeight));
        WriteCropFields();
        previewDelay.Stop();
        ShowOriginalWithFrame();
        transport?.InvalidateEffect();
    }
    // For screenshots and manual checks: puts the window into a known state (see the --debug-tool-render mode).
    internal List<(string Name, Point Position)> DebugCropHandlePoints()
    {
        var c = cropFrame.CropViewRect;
        var list = new (string Name, Point Pt)[]
        {
            ("TopLeft", new Point(c.Left + 3, c.Top + 3)),
            ("Top", new Point(c.Left + c.Width / 2, c.Top + 3)),
            ("TopRight", new Point(c.Right - 3, c.Top + 3)),
            ("Right", new Point(c.Right - 3, c.Top + c.Height / 2)),
            ("BottomRight", new Point(c.Right - 3, c.Bottom - 3)),
            ("Bottom", new Point(c.Left + c.Width / 2, c.Bottom - 3)),
            ("BottomLeft", new Point(c.Left + 3, c.Bottom - 3)),
            ("Left", new Point(c.Left + 3, c.Top + c.Height / 2)),
            ("Move", new Point(c.Left + c.Width / 2, c.Top + c.Height / 2))
        };
        var result = new List<(string Name, Point Position)>();
        foreach (var (name, pt) in list)
            result.Add((name, cropFrame.TranslatePoint(pt, this)));
        return result;
    }

    internal void DebugScene(string scene)
    {
        foreach (string part in scene.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == "busy")
            { busyLine.ShowDelay = TimeSpan.Zero; busyLine.Begin(); }
            else if (part.StartsWith("angle=") && Rotating)
                Set("angle", part[6..]);
            else if (part.StartsWith("ratio=") && controls.ContainsKey("ratio"))
            { Set("ratio", part[6..]); editedField = "ratio"; ParameterChanged(); }
            else if (part.StartsWith("crop=") && cropFrame.HasSource)
            {
                int[] v = part[5..].Split(',').Select(int.Parse).ToArray();
                cropFrame.SetCrop(CropMath.Constrain(new CropRect(v[0], v[1], v[2], v[3]), originalWidth, originalHeight, CurrentRatio(), cropFrame.MinSize));
                WriteCropFields();
            }
        }
    }
    private Point SourcePoint(Point point)
    {
        double scale = Math.Min(previewHost.ActualWidth / Math.Max(1, originalWidth), previewHost.ActualHeight / Math.Max(1, originalHeight));
        double offsetX = (previewHost.ActualWidth - originalWidth * scale) / 2, offsetY = (previewHost.ActualHeight - originalHeight * scale) / 2;
        return new Point(Math.Clamp((point.X - offsetX) / scale, 0, originalWidth), Math.Clamp((point.Y - offsetY) / scale, 0, originalHeight));
    }
    private void BeginSelection(object sender, MouseButtonEventArgs e)
    {
        bool audio = operation.Id is "redactAudio" or "trimAudio";
        if (!loaded || saving != null)
            return;
        if (!audio && (operation.Id is not ("redactImage" or "redactVideo" or "redactPDF") || originalWidth == 0))
            return;
        if (HasMedia)
        { if (transport?.EffectSelected == true) { status.Text = L.T("请切换到原文件，再选择画面或音频范围。"); return; } transport?.Pause(); }
        if (!showingOriginal && !HasMedia && originalPreview != null)
        { SetImage(originalPreview); showingOriginal = true; }
        previewDelay.Stop();
        previewCancellation?.Cancel();
        selectionStart = e.GetPosition(previewHost);
        overlay.CaptureMouse();
        selection.Visibility = Visibility.Visible;
        Canvas.SetLeft(selection, selectionStart.Value.X);
        Canvas.SetTop(selection, selectionStart.Value.Y);
        selection.Width = selection.Height = 0;
    }
    private void MoveSelection(object sender, MouseEventArgs e)
    {
        if (selectionStart == null)
            return;
        var raw = e.GetPosition(previewHost);
        var point = new Point(Math.Clamp(raw.X, 0, previewHost.ActualWidth), Math.Clamp(raw.Y, 0, previewHost.ActualHeight));
        Canvas.SetLeft(selection, Math.Min(point.X, selectionStart.Value.X));
        Canvas.SetTop(selection, Math.Min(point.Y, selectionStart.Value.Y));
        selection.Width = Math.Abs(point.X - selectionStart.Value.X);
        selection.Height = Math.Abs(point.Y - selectionStart.Value.Y);
    }
    private void EndSelection(object sender, MouseButtonEventArgs e)
    {
        if (selectionStart == null)
            return;
        var first = SourcePoint(selectionStart.Value);
        var last = SourcePoint(e.GetPosition(previewHost));
        overlay.ReleaseMouseCapture();
        selectionStart = null;
        if (operation.Id is "redactAudio" or "trimAudio")
        {
            double start = Math.Clamp(Canvas.GetLeft(selection) / Math.Max(1, previewHost.ActualWidth) * totalDuration, 0, totalDuration), end = Math.Clamp((Canvas.GetLeft(selection) + selection.Width) / Math.Max(1, previewHost.ActualWidth) * totalDuration, 0, totalDuration);
            Set("start", start.ToString("0.000", CultureInfo.InvariantCulture));
            Set("end", end.ToString("0.000", CultureInfo.InvariantCulture));
            if (end - start < .001)
            { selection.Visibility = Visibility.Collapsed; return; }
            if (operation.Id == "redactAudio")
            { var r = new Dictionary<string, double> { ["start"] = start, ["end"] = end }; audioRanges.Add(r); var row = new ListBoxItem { Content = L.F("{0:0.000}–{1:0.000} 秒", start, end), Tag = r }; regionList.Items.Add(row); regionList.SelectedItem = row; status.Text = L.F("已添加 {0} 个蜂鸣时段。", audioRanges.Count); }
            return;
        }
        int x = (int)Math.Min(first.X, last.X), y = (int)Math.Min(first.Y, last.Y), w = (int)Math.Abs(last.X - first.X), h = (int)Math.Abs(last.Y - first.Y);
        if (w < 1 || h < 1)
            return;
        syncingRegion = true;
        Set("x", x.ToString());
        Set("y", y.ToString());
        Set("width", w.ToString());
        Set("height", h.ToString());
        syncingRegion = false;
        if (operation.Id == "redactPDF")
        {
            int page = CurrentPage();
            var mark = new Dictionary<string, object> { ["page"] = page, ["x"] = x, ["y"] = y, ["width"] = w, ["height"] = h };
            regions.Add(mark);
            var row = new ListBoxItem { Content = L.F("第 {0} 页 · 区域 {1}", page, regions.Count), Tag = mark };
            regionList.Items.Add(row);
            regionList.SelectedItem = row;
            status.Text = L.F("已添加 {0} 个覆盖区域。", regions.Count);
        }
        if (operation.Id is "redactImage" or "redactVideo")
        {
            double start = TimeValue(Get("start", "0")), end = TimeValue(Get("end", "0"));
            var region = new Dictionary<string, object> { ["x"] = x, ["y"] = y, ["width"] = w, ["height"] = h, ["start"] = start, ["end"] = end, ["style"] = Get("style", "pixelate"), ["blockSize"] = Get("blockSize", "12") };
            regions.Add(region);
            var row = new ListBoxItem { Content = L.F("区域 {0} · {1} × {2} · {3}", regions.Count, w, h, Choice(Get("style"))), Tag = region };
            regionList.Items.Add(row);
            regionList.SelectedItem = row;
            status.Text = L.F("已添加 {0} 个覆盖区域。", regions.Count);
        }
        if (!HasMedia)
            SchedulePreview();
    }
    private static double TimeValue(string value) => JobValidation.Time(value);
    private static bool IsTimeField(string name) => name is "start" or "end";
    // Times are stored as seconds but shown as m:ss.xx, so a trim point reads like a player's clock.
    private static string TimeText(string name, string value) =>
        IsTimeField(name) && !string.IsNullOrWhiteSpace(value) && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds) && double.IsFinite(seconds) && seconds >= 0
            ? MediaSegment.FormatPrecise(seconds)
            : value;
    private void CommitTimeField(TextBox box, Field field)
    {
        if (!loaded)
            return;
        try
        {
            string shown = MediaSegment.FormatPrecise(JobValidation.Time(box.Text));
            box.Tag = shown;
            if (box.Text != shown)
                box.Text = shown;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            box.Text = box.Tag as string ?? TimeText(field.Name, field.Default);
            status.Text = L.T("时间格式应为 m:ss.xx 或秒数。");
        }
    }
    private void SetMetadataState() { if (controls.TryGetValue("metadata", out var table) && table is MetadataEditor editor) editor.IsReadOnly = Get("remove") == "true"; }
    private static string FormatSliderValue(double val, Field f)
    {
        if (f.Unit == "EV")
            return val.ToString("0.00", CultureInfo.InvariantCulture) + " EV";
        if (f.Unit == "px")
            return Math.Round(val).ToString("0", CultureInfo.InvariantCulture) + " px";
        if (f.Step < 1 && f.Step > 0)
            return val.ToString("0.00", CultureInfo.InvariantCulture);
        if (f.Min < 0)
        {
            int iv = (int)Math.Round(val);
            return iv >= 0 ? "+" + iv : iv.ToString(CultureInfo.InvariantCulture);
        }
        return Math.Round(val).ToString("0", CultureInfo.InvariantCulture);
    }
    private void ParameterChanged()
    {
        if (syncingRegion || syncingPlayback || syncingTrim)
            return;
        SyncTrimRange();
        transport?.InvalidateEffect();
        if (Cropping)
            FieldsToCropFrame();
        if (Rotating)
            UpdateRotationPreview();
        if (operation.Id is "redactImage" or "redactVideo" && regionList.SelectedItem is ListBoxItem item && item.Tag is Dictionary<string, object> r)
        {
            try
            { foreach (string key in new[] { "x", "y", "width", "height", "start", "end", "blockSize" }) if (controls.ContainsKey(key)) r[key] = TimeValue(Get(key, "0")); r["style"] = Get("style", "pixelate"); item.Content = L.F("区域 {0} · {1} × {2} · {3}", regionList.SelectedIndex + 1, r["width"], r["height"], Choice(Get("style"))); }
            catch (ArgumentException) { }
            catch (FormatException) { }
        }
        if (operation.Id == "redactAudio" && regionList.SelectedItem is ListBoxItem audioItem && audioItem.Tag is Dictionary<string, double> range)
        {
            try
            { range["start"] = TimeValue(Get("start", "0")); range["end"] = TimeValue(Get("end", "0")); audioItem.Content = L.F("{0:0.000}–{1:0.000} 秒", range["start"], range["end"]); }
            catch (ArgumentException) { }
        }
        syncFrameExtras?.Invoke();
        if (operation.Id == "editImage" && editSourceBgra != null && editLiveBitmap != null)
            ScheduleLiveEditPreview();
        if (operation.Id == "audioEffects" && fxPlaying)
            StopAudioEffectsPreview();
        if (LivePreview)
            SchedulePreview();
    }
    private void RefreshTrimPlayButton()
    {
        if (trimPlayButton == null)
            return;
        bool isPlaying = transport?.IsPlaying == true;
        trimPlayButton.Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children =
            {
                new System.Windows.Shapes.Path
                {
                    Data = Geometry.Parse(isPlaying ? "M 0,0 H 9 V 9 H 0 Z" : "M 0,0 L 8,4.5 L 0,9 Z"),
                    Fill = UiTheme.Accent,
                    Width = 9,
                    Height = 9,
                    Stretch = Stretch.Uniform,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 6, 0)
                },
                new TextBlock
                {
                    Text = isPlaying
                        ? (L.Arabic ? "إيقاف" : "Stop")
                        : (L.Arabic ? "تشغيل المقطع المحدد" : "Play Selected Range"),
                    VerticalAlignment = VerticalAlignment.Center
                }
            }
        };
    }
    private void SyncTrimRange()
    {
        if (transport == null || operation.Id is not ("trimVideo" or "trimAudio") || totalDuration <= 0)
            return;
        try
        {
            double rawFirst = TimeValue(Get("start", "0")), rawLast = TimeValue(Get("end", "0"));
            var (first, last) = RangePlayback.Clamp(rawFirst, rawLast, totalDuration);
            transport.SetTrimRange(first, last);
            if (transport.IsRangePlaying)
                transport.UpdateRangeEnd(last);
            if (!RangePlayback.Contains(timeline.Value, first, last))
                timeline.Value = first;
        }
        catch (ArgumentException) { }
        catch (FormatException) { }
    }
    private void UpdateTrimSelection()
    {
        if (trimSelected == null || totalDuration <= 0)
            return;
        try
        {
            double first = TimeValue(Get("start", "0")), last = TimeValue(Get("end", "0"));
            if (last == 0)
                last = totalDuration;
            if (last < first)
                last = first;
            trimSelected.Text = L.F("{0} selected", MediaSegment.FormatPrecise(Math.Max(0, last - first)));
        }
        catch (ArgumentException) { }
        catch (FormatException) { }
    }
    private async Task PlayTrimRange()
    {
        if (transport == null)
            return;
        if (transport.IsPlaying)
        {
            transport.Pause();
            return;
        }
        try
        {
            double rawFirst = TimeValue(Get("start", "0")), rawLast = TimeValue(Get("end", "0"));
            var (first, last) = RangePlayback.Clamp(rawFirst, rawLast, totalDuration);
            await transport.PlayRange(first, last);
        }
        catch (ArgumentException) { }
        catch (FormatException) { }
    }
    private async Task Save()
    {
        if (!loaded || saving != null)
            return;
        transport?.Pause();
        ConversionJob job;
        try
        { job = Capture(); }
        catch (Exception ex) { status.Text = ex.Message; status.Foreground = UiTheme.Accent; return; }
        if (operation.Id == "location" && Get("mode") == "set" && string.IsNullOrWhiteSpace(Get("coordinates")) && string.IsNullOrWhiteSpace(Get("locationSource")))
        { status.Text = L.T("请填写新位置的坐标，或选一张照片借用它的位置。"); status.Foreground = UiTheme.Accent; return; }
        if (operation.Id == "redactPDF" && regions.Count == 0 && string.IsNullOrWhiteSpace(Get("findText")) && string.IsNullOrWhiteSpace(Get("regions")))
        { status.Text = L.T("请先在页面上框选要遮盖的区域，或填写要遮盖的文字。"); status.Foreground = UiTheme.Accent; return; }
        if (operation.Id is "rotateImage" or "rotateVideo" && double.TryParse(Get("angle", "0"), NumberStyles.Float, CultureInfo.InvariantCulture, out double chosenAngle) && chosenAngle % 360 == 0 && Get("flip", "none") == "none")
        { status.Text = L.T("请选择旋转角度或翻转方式。"); status.Foreground = UiTheme.Accent; return; }
        saving = CancellationTokenSource.CreateLinkedTokenSource(closing.Token);
        previewDelay.Stop();
        previewCancellation?.Cancel();
        transport?.Stop();
        player.Stop();
        player.Source = null;
        StopAudioEffectsPreview();
        a2vPreviewTimer?.Stop();
        a2vCancellation?.Cancel();
        if (stagePlayer != null) { stagePlayer.Stop(); stagePlayer.Source = null; }
        using var busy = busyLine.Begin();
        bodyPanel.IsEnabled = false;
        saveButton.IsEnabled = false;
        revealButton.Visibility = Visibility.Collapsed;
        copyButton.Visibility = Visibility.Collapsed;
        cancelButton.Visibility = Visibility.Visible;
        cancelButton.IsEnabled = true;
        status.Foreground = UiTheme.Muted;
        status.Text = L.T("处理期间可取消；原文件保留。");
        BatchResult? applied = null;
        try
        {
            var result = await submit(job, saving.Token, busy);
            var success = result.Files.Where(f => f.Output != null).ToArray();
            var failures = result.Files.Where(f => f.Error != null).ToArray();
            if (Saved != null && success.Length > 0 && failures.Length == 0)
            {
                if (Presets.Supported(operation.Id)) Presets.SaveLast(operation.Id, PresetValues());
                status.Text = L.T("已应用。");
                applied = result;
                return;
            }
            if (success.Length > 0)
            { lastOutput = success[^1].Output; revealButton.Visibility = Visibility.Visible; copyButton.Visibility = Visibility.Visible; if (Presets.Supported(operation.Id)) Presets.SaveLast(operation.Id, PresetValues()); }
            status.Text = failures.Length == 0 ? (success.Length == 1 ? L.F("已保存：{0}", Path.GetFileName(lastOutput)) : L.F("已保存 {0} 项：{1}", success.Length, Path.GetFileName(lastOutput))) : L.F("成功 {0} 项，失败 {1} 项：{2}", success.Length, failures.Length, failures[0].Error);
            Journal.Write("ToolSaved", new { job.Action, success = success.Length, failures = failures.Length });
        }
        catch (OperationCanceledException) { status.Text = L.T("处理已取消，可调整参数后重新保存。"); }
        catch (Exception ex) { status.Text = L.T("保存未完成：") + ex.Message; status.Foreground = UiTheme.Accent; }
        finally
        {
            saving.Dispose(); saving = null; bodyPanel.IsEnabled = true; saveButton.IsEnabled = true; cancelButton.Visibility = Visibility.Collapsed;
            if (applied != null) Saved?.Invoke(applied);
        }
    }
    private async Task StepFrame(int direction)
    {
        if (Catalog.Category(paths[0]) != "video")
        { timeline.Value = Math.Clamp(timeline.Value + direction * .1, 0, totalDuration); return; }
        using var busy = busyLine.Begin();
        try
        { var job = Capture(); job.Parameters!["direction"] = direction.ToString(); job.Parameters["time"] = timeline.Value.ToString(CultureInfo.InvariantCulture); var info = await Backend.Preview(job, folder, closing.Token, "--frame-step"); timeline.Value = info.GetProperty("Time").GetDouble(); }
        catch (Exception ex) { status.Text = L.T("逐帧：") + ex.Message; }
    }
    private async Task<JsonElement> PreparePlayback(bool effect, string source, double time, CancellationToken token)
    {
        using var busy = busyLine.Begin();
        var job = effect ? Capture() : new ConversionJob([source], "preview-original", new());
        job.Parameters!["time"] = time.ToString(CultureInfo.InvariantCulture);
        if (!effect)
            job.Parameters["fullPreview"] = "true";
        return await Backend.Preview(job, folder, token, "--playback");
    }

    private void PrepareEditBgra(BitmapSource source)
    {
        try
        {
            double maxDim = Math.Max(source.PixelWidth, source.PixelHeight);
            double scale = maxDim > 1000 ? 1000.0 / maxDim : 1.0;
            BitmapSource converted = source;
            if (scale < 0.999)
                converted = new TransformedBitmap(source, new ScaleTransform(scale, scale));
            if (converted.Format != PixelFormats.Bgra32)
                converted = new FormatConvertedBitmap(converted, PixelFormats.Bgra32, null, 0);
            editBgraWidth = converted.PixelWidth;
            editBgraHeight = converted.PixelHeight;
            int stride = editBgraWidth * 4;
            editSourceBgra = new byte[stride * editBgraHeight];
            converted.CopyPixels(editSourceBgra, stride, 0);
            editTargetBgra = new byte[editSourceBgra.Length];
            editBaseMean = ImageAdjust.ComputeMeanLuminance(editSourceBgra);
            editLiveBitmap = new WriteableBitmap(editBgraWidth, editBgraHeight, 96, 96, PixelFormats.Bgra32, null);
        }
        catch
        {
            editSourceBgra = null;
            editTargetBgra = null;
            editLiveBitmap = null;
        }
    }

    private void ScheduleLiveEditPreview()
    {
        if (editSourceBgra == null || editTargetBgra == null || editLiveBitmap == null)
            return;
        int ver = unchecked(++editLiveVersion);
        if (editRenderingLive) return;
        editRenderingLive = true;

        double exposure = double.TryParse(Get("exposure", "0"), NumberStyles.Float, CultureInfo.InvariantCulture, out var exp) ? exp : 0;
        double contrast = double.TryParse(Get("contrast", "1"), NumberStyles.Float, CultureInfo.InvariantCulture, out var con) ? con : 1;
        double highlights = double.TryParse(Get("highlights", "0"), NumberStyles.Float, CultureInfo.InvariantCulture, out var hl) ? hl : 0;
        double shadows = double.TryParse(Get("shadows", "0"), NumberStyles.Float, CultureInfo.InvariantCulture, out var sh) ? sh : 0;
        double saturation = double.TryParse(Get("saturation", "1"), NumberStyles.Float, CultureInfo.InvariantCulture, out var sat) ? sat : 1;
        double temperature = double.TryParse(Get("temperature", "0"), NumberStyles.Float, CultureInfo.InvariantCulture, out var temp) ? temp : 0;

        byte[] src = editSourceBgra;
        byte[] dst = editTargetBgra;
        int w = editBgraWidth;
        int h = editBgraHeight;
        double baseMean = editBaseMean;

        Task.Run(() =>
        {
            ImageAdjust.Apply(src, dst, w, h, exposure, contrast, highlights, shadows, saturation, temperature, baseMean);
        }).ContinueWith(_ =>
        {
            editRenderingLive = false;
            if (closing.IsCancellationRequested) return;

            int stride = w * 4;
            editLiveBitmap.WritePixels(new Int32Rect(0, 0, w, h), dst, stride, 0);
            if (image.Source != editLiveBitmap)
                image.Source = editLiveBitmap;

            if (ver != editLiveVersion)
                ScheduleLiveEditPreview();
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private async Task ToggleAudioEffectsPreview()
    {
        if (fxPlaying)
        {
            StopAudioEffectsPreview();
            return;
        }
        fxPreviewCancellation?.Cancel();
        var cts = CancellationTokenSource.CreateLinkedTokenSource(closing.Token);
        fxPreviewCancellation = cts;
        fxPlaying = true;
        if (fxPlayButton != null) fxPlayButton.Content = L.T("Stop preview");
        status.Text = L.T("Rendering preview…");

        try
        {
            var info = await PreparePlayback(true, paths[0], 0, cts.Token);
            if (cts.IsCancellationRequested) return;

            string? previewWav = info.TryGetProperty("Preview", out var p) ? p.GetString() : null;
            if (!string.IsNullOrEmpty(previewWav) && File.Exists(previewWav))
            {
                player.Source = new Uri(previewWav);
                player.Position = TimeSpan.Zero;
                player.Play();
                status.Text = "";
            }
            else
            {
                StopAudioEffectsPreview();
                status.Text = L.T("Preview failed: ") + "No audio generated";
            }
        }
        catch (OperationCanceledException)
        {
            StopAudioEffectsPreview();
        }
        catch (Exception ex)
        {
            StopAudioEffectsPreview();
            status.Text = L.T("Preview failed: ") + ex.Message;
        }
    }

    private void StopAudioEffectsPreview()
    {
        fxPlaying = false;
        fxPreviewCancellation?.Cancel();
        fxPreviewCancellation = null;
        player.Stop();
        player.Source = null;
        if (fxPlayButton != null) fxPlayButton.Content = L.T("Play Preview");
    }
}
