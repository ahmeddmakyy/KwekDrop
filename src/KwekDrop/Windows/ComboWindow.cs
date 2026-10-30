using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KwekDrop;

// One window for several related tools (for example crop + rotate). Each tool keeps its own screen, shown as a tab.
// In a "chain" window every tool works on the result of the previous ones: "Apply" adds the edit to a working copy,
// "Undo" takes the last one back, and "Save new file" writes the working copy once. Other windows just switch tools.
internal sealed class ComboWindow : Window
{
    private readonly string[] paths;
    private readonly Operation combo;
    private readonly Func<ConversionJob, CancellationToken, IProgress<JobProgress>?, Task<BatchResult>> submit;
    private readonly string kind;
    private readonly ContentControl host = new();
    private readonly WrapPanel tabs = new() { Margin = new Thickness(0, 0, 0, 12) };
    private readonly Dictionary<string, Button> tabButtons = [];
    private readonly TextBlock appliedText = new() { Foreground = UiTheme.Muted, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 12, 0) };
    private readonly TextBlock savedText = new() { Foreground = UiTheme.Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
    private readonly Button undoButton, saveButton, revealButton;
    private readonly Stack<string> history = new();
    private readonly List<string> applied = [];
    private readonly string temp = Path.Combine(Journal.DirectoryPath, "combo-" + Guid.NewGuid().ToString("N"));
    private string working;
    private string current;
    private string? savedOutput;
    private ToolWindow? panel;

    public ComboWindow(string[] paths, Operation combo, Func<ConversionJob, CancellationToken, IProgress<JobProgress>?, Task<BatchResult>> submit)
    {
        this.paths = paths;
        this.combo = combo;
        this.submit = submit;
        kind = Catalog.Category(paths[0]);
        working = paths[0];
        current = combo.Parts![0];
        Title = combo.Label + " · " + Selection;
        bool cropFirst = Catalog.Definition(combo.Parts![0], kind).Id is "cropImage" or "cropVideo";
        bool trimFirst = Catalog.Definition(combo.Parts![0], kind).Id == "trimAudio";
        Width = cropFirst ? 560 : trimFirst ? 420 : 680;
        Height = cropFirst ? 780 : trimFirst ? 500 : 540;
        MinWidth = cropFirst ? 440 : trimFirst ? 380 : 540;
        MinHeight = cropFirst ? 640 : trimFirst ? 440 : 440;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = UiTheme.WindowBack;
        FontFamily = UiTheme.Font;
        FontSize = 13;
        UiTheme.Apply(this);
        undoButton = MakeButton(L.Arabic ? "تراجع عن الخطوة" : "Undo Step", Undo);
        saveButton = MakeButton(L.Arabic ? "حفظ وتطبيق" : "Apply & Save", SaveFinal);
        saveButton.Foreground = Brushes.White;
        saveButton.Background = UiTheme.Accent;
        saveButton.BorderThickness = new Thickness(0);
        revealButton = MakeButton(L.Arabic ? "فتح المجلد" : "Reveal in Explorer", () => { if (savedOutput != null) ExplorerIntegration.Reveal(savedOutput); return Task.CompletedTask; });
        revealButton.Visibility = Visibility.Collapsed;
        Content = UiTheme.Frame(this, Build(), null, toolChrome: true);
        Loaded += async (_, _) => await Show(current);
        Closing += (_, e) => { if (panel?.Busy == true) e.Cancel = true; };
        Closed += (_, _) => { panel?.Release(); CleanUp(); };
    }

    private UIElement Build()
    {
        var root = new DockPanel { Margin = new Thickness(18, 4, 18, 14) };
        
        // Sleek top control bar matching Tangerine for Mac (ref_window_2.png)
        var topBar = new DockPanel { Margin = new Thickness(0, 0, 0, 10), Background = Brushes.Transparent };
        topBar.MouseLeftButtonDown += (s, e) =>
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed && e.OriginalSource is not (Button or TextBox or ComboBox or Slider or System.Windows.Controls.Primitives.Thumb))
            {
                try { DragMove(); } catch { }
            }
        };
        
        // Center segmented capsule tabs [ قص | تدوير وعكس ]
        var segmentedCard = new Border
        {
            CornerRadius = new CornerRadius(14),
            Background = UiTheme.Dark ? new SolidColorBrush(Color.FromArgb(120, 25, 30, 36)) : Brushes.Transparent,
            Padding = new Thickness(3),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var tabStack = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (string part in combo.Parts!)
        {
            string id = part;
            var tab = new Button
            {
                Content = Catalog.Definition(id, kind).Label,
                FontFamily = UiTheme.Font,
                FontSize = 12,
                Padding = new Thickness(16, 5, 16, 5),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            tab.Click += async (_, _) => { if (id != current && panel?.Busy != true) { current = id; MarkTabs(); await Show(id); } };
            tabButtons[id] = tab;
            tabStack.Children.Add(tab);
        }
        MarkTabs();
        segmentedCard.Child = tabStack;
        topBar.Children.Add(segmentedCard);

        DockPanel.SetDock(topBar, Dock.Top);
        root.Children.Add(topBar);
        root.Children.Add(host);
        return root;
    }

    // The tool being shown is drawn like the main action button.
    private void MarkTabs()
    {
        foreach (var (id, tab) in tabButtons)
        {
            bool on = id == current;
            if (on)
            {
                tab.Background = UiTheme.Accent;
                tab.Foreground = Brushes.White;
                tab.BorderThickness = new Thickness(0);
                tab.FontWeight = FontWeights.Bold;
            }
            else
            {
                tab.Background = Brushes.Transparent;
                tab.Foreground = UiTheme.Ink;
                tab.BorderThickness = new Thickness(0);
                tab.FontWeight = FontWeights.Normal;
            }
        }
    }

    private string Selection => paths.Length == 1 ? Path.GetFileName(paths[0]) : (L.Arabic ? $"{paths.Length} ملفات" : $"{paths.Length} files");

    private static Button MakeButton(string text, Func<Task> click)
    {
        var button = new Button { Content = text, Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(6, 0, 0, 0) };
        button.Click += async (_, _) => await click();
        return button;
    }

    // Shows one tool on the current working copy (the original file until something has been applied).
    private async Task Show(string part)
    {
        if (panel?.Busy == true)
            return;
        panel?.Release();
        panel = new ToolWindow(combo.Chain ? [working] : paths, Catalog.Definition(part, kind), combo.Chain ? ApplyStep : submit, this);
        if (combo.Chain)
        {
            panel.SaveLabel = L.Arabic ? "تطبيق هذه الخطوة" : "Apply Step";
            panel.Saved = OnApplied;
        }
        host.Content = panel.Body;
        await panel.Start();
    }

    // A step writes its result into this window's own scratch folder; nothing is written beside the user's file yet.
    private Task<BatchResult> ApplyStep(ConversionJob job, CancellationToken token, IProgress<JobProgress>? progress)
    {
        string step = Path.Combine(temp, "step-" + (history.Count + 1));
        Directory.CreateDirectory(step);
        var parameters = new Dictionary<string, string>(job.Parameters ?? []) { ["_outputDir"] = step };
        return Backend.Execute(job with { Parameters = parameters }, token, progress);
    }

    private async void OnApplied(BatchResult result)
    {
        string? output = result.Files.FirstOrDefault(f => f.Output != null)?.Output;
        if (output == null || !File.Exists(output))
            return;
        history.Push(working);
        working = output;
        applied.Add(Catalog.Definition(current, kind).Label);
        savedOutput = null;
        RefreshChain();
        await Show(current);
    }

    private async Task Undo()
    {
        if (history.Count == 0 || panel?.Busy == true)
            return;
        working = history.Pop();
        applied.RemoveAt(applied.Count - 1);
        RefreshChain();
        await Show(current);
    }

    private Task SaveFinal()
    {
        if (applied.Count == 0)
            return Task.CompletedTask;
        try
        {
            string folder = OutputFolder.Current ?? Path.GetDirectoryName(Path.GetFullPath(paths[0]))!;
            string name = Path.GetFileNameWithoutExtension(working), extension = Path.GetExtension(working);
            string target = Path.Combine(folder, name + extension);
            for (int i = 1; File.Exists(target); i++)
                target = Path.Combine(folder, $"{name}-{i}{extension}");
            File.Copy(working, target);
            savedOutput = target;
            savedText.Text = (L.Arabic ? "تم الحفظ: " : "Saved: ") + Path.GetFileName(target);
            savedText.Visibility = Visibility.Visible;
            revealButton.Visibility = Visibility.Visible;
            Journal.Write("ComboSaved", new { combo.Id, steps = applied.Count });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { savedText.Text = (L.Arabic ? "فشل الحفظ: " : "Save failed: ") + ex.Message; savedText.Visibility = Visibility.Visible; }
        return Task.CompletedTask;
    }

    private void RefreshChain()
    {
        appliedText.Text = applied.Count == 0 ? (L.Arabic ? "لم يتم تطبيق خطوات بعد" : "No adjustments applied yet.") : (L.Arabic ? "تم تطبيق: " : "Applied: ") + string.Join(" → ", applied);
        undoButton.IsEnabled = saveButton.IsEnabled = applied.Count > 0;
        if (savedOutput == null)
        { savedText.Text = ""; revealButton.Visibility = Visibility.Collapsed; }
        savedText.Visibility = savedText.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // Removes only this window's own scratch folder, after checking it is the one it created.
    private void CleanUp()
    {
        try
        {
            string root = Path.GetFullPath(Journal.DirectoryPath) + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(temp);
            if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(full).StartsWith("combo-") && Directory.Exists(full))
                Directory.Delete(full, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
