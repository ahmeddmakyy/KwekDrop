using System.Windows.Interop;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shell;

namespace KwekDrop;

internal static class UiTheme
{
    public const double WindowCornerRadius = 20;
    private static readonly Uri FontBase = new($"pack://application:,,,/{typeof(UiTheme).Assembly.GetName().Name};component/");
    public static readonly FontFamily ArabicFont = new(FontBase, "./assets/fonts/#thmanyah sans");
    // The font follows the interface language, not the letters on screen: Arabic uses the bundled Thmanyah Sans (with the
    // Windows fonts behind it for Latin digits and names); English uses the normal Windows font.
    private static readonly FontFamily ArabicInterfaceFont = new(FontBase, "./assets/fonts/#thmanyah sans, Segoe UI Variable Display, Segoe UI, sans-serif");
    private static readonly FontFamily LatinInterfaceFont = new("Segoe UI Variable Display, Segoe UI, sans-serif");
    public static FontFamily Font => L.Arabic ? ArabicInterfaceFont : LatinInterfaceFont;

    // --- light and dark ------------------------------------------------------------------------------------------
    // "system" follows the Windows app colour (Settings → Personalisation → Colours); "light" and "dark" fix it.
    // Windows take the theme when they open; the wheel and the progress cards follow a change at once.
    public const string ThemeKey = "theme";
    public const string BlurAmountKey = "blurAmount";
    public static int BlurAmount => int.TryParse(Settings.GetString(BlurAmountKey), out var val) ? Math.Clamp(val, 0, 100) : 80;
    public const string GlassOpacityKey = "glassOpacity";
    public static int GlassOpacity => int.TryParse(Settings.GetString(GlassOpacityKey), out var val) ? Math.Clamp(val, 0, 100) : 88;

    public static double VeilAlpha(int blur, int glass)
    {
        double baseVal = Math.Clamp(0.16 + 0.40 * glass / 100.0, 0.16, 0.60);
        return Math.Clamp(baseVal - 0.22 * blur / 100.0, 0.12, 0.60);
    }

    public static double FrameVeilAlpha(int blur, int glass)
    {
        double baseVal = Math.Clamp(0.72 + 0.22 * glass / 100.0, 0.72, 0.94);
        return Math.Clamp(baseVal - 0.08 * blur / 100.0, 0.70, 0.94);
    }
    public static event Action? VisualsChanged;
    public static void NotifyVisualsChanged() => VisualsChanged?.Invoke();
    private sealed record Palette(string Window, string Surface, string Ink, string Muted, string Line, string StrongLine, string Hover, string Tint, string Rail, string Alt, string Preview);
    private static readonly Palette LightPalette = new("#4DFFF4E8", "#E6FFFFFF", "#12151A", "#363C44", "#33FFFFFF", "#66FFFFFF", "#33FFFFFF", "#14000000", "#59FFFFFF", "#26FFFFFF", "#F5F6F8");
    private static readonly Palette DarkPalette = new("#B8181C22", "#D8242830", "#E6E8EB", "#9EA4AC", "#25FFFFFF", "#5E656F", "#30343A", "#4A2D1D", "#454B53", "#2B2E33", "#202327");
    private static Palette palette = LightPalette;
    public static bool Dark { get; private set; }
    public static string Choice => Settings.GetString(ThemeKey) is "light" or "dark" or "system" ? Settings.GetString(ThemeKey)! : "light";
    public static Brush Ink { get; private set; } = Brushes.Black;
    public static Brush Muted { get; private set; } = Brushes.Gray;
    public static Brush Title { get; private set; } = Brushes.Black;
    public static Brush Status { get; private set; } = Brushes.Gray;
    public static readonly Brush Accent = Frozen("#FF5200");
    public static readonly Color AccentColor = Color.FromRgb(255, 82, 0);
    public static readonly Brush Hairline = Frozen("#1A000000");
    public static Brush WindowBack { get; private set; } = Brushes.White;
    public static Brush Surface { get; private set; } = Brushes.White;
    public static Brush Line { get; private set; } = Brushes.LightGray;
    public static Brush Tint { get; private set; } = Brushes.White;
    public static Brush PreviewBack { get; private set; } = Brushes.White;
    public static Color SurfaceColor => (Color)ColorConverter.ConvertFromString(palette.Surface);
    public static Color LineColor => (Color)ColorConverter.ConvertFromString(palette.Line);

    static UiTheme()
    {
        Refresh();
    }

    // Reads the chosen theme (and, for "system", the Windows setting) again.
    public static void Refresh()
    {
        string choice = Choice;
        Dark = choice == "dark" || choice == "system" && SystemUsesDark();
        palette = Dark ? DarkPalette : LightPalette;
        Color winColor;
        Color surfColor;
        if (Dark)
        {
            byte winAlpha = (byte)Math.Round(GlassOpacity * 2.55);
            byte surfAlpha = (byte)Math.Min(255, Math.Round(GlassOpacity * 2.55 + 20));
            winColor = Color.FromArgb(winAlpha, 0x1A, 0x1D, 0x22);
            surfColor = Color.FromArgb(surfAlpha, 0x26, 0x2A, 0x32);
        }
        else
        {
            winColor = Color.FromArgb((byte)Math.Round(GlassOpacity * 2.55 * 0.30), 0xFF, 0xF4, 0xE8);
            surfColor = (Color)ColorConverter.ConvertFromString("#E6FFFFFF");
        }

        Ink = Frozen(palette.Ink); Muted = Frozen(palette.Muted);
        Title = Frozen(palette.Ink); Status = Frozen(palette.Muted);
        WindowBack = Frozen(winColor);
        Surface = Frozen(surfColor);
        Line = Frozen(palette.Line); Tint = Frozen(palette.Tint); PreviewBack = Frozen(palette.Preview);
        NotifyVisualsChanged();
    }

    public static void Choose(string choice) { Settings.Set(ThemeKey, choice); Refresh(); }

    private static bool SystemUsesDark()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException) { return false; }
    }

    private static Brush Frozen(Color color) { var b = new SolidColorBrush(color); b.Freeze(); return b; }
    private static Brush Frozen(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }

    // Puts the palette's colours into a style text that names them as {Ink}, {Surface} and so on.
    public static string Fill(string xaml) => xaml.Replace("{Surface}", palette.Surface).Replace("{Ink}", palette.Ink).Replace("{Muted}", palette.Muted)
        .Replace("{StrongLine}", palette.StrongLine).Replace("{Line}", palette.Line).Replace("{Hover}", palette.Hover).Replace("{Tint}", palette.Tint)
        .Replace("{Rail}", palette.Rail).Replace("{Alt}", palette.Alt);

    public static void Apply(Window window)
    {
        Refresh();
        window.Icon = AppIcon.Window;
        window.Resources = (ResourceDictionary)XamlReader.Parse(Fill("""
        <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:p="clr-namespace:System.Windows.Controls.Primitives;assembly=PresentationFramework">
          <Style TargetType="Button">
            <Setter Property="Background" Value="{Tint}"/><Setter Property="Foreground" Value="{Ink}"/>
            <Setter Property="BorderThickness" Value="0"/>
            <Setter Property="Padding" Value="14,9"/><Setter Property="MinHeight" Value="36"/><Setter Property="Cursor" Value="Hand"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button">
              <Border x:Name="surface" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="8" Padding="{TemplateBinding Padding}"><ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/></Border>
              <ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="surface" Property="Opacity" Value="0.8"/></Trigger><Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="surface" Property="BorderBrush" Value="#FF5200"/><Setter TargetName="surface" Property="BorderThickness" Value="1.5"/></Trigger><Trigger Property="IsEnabled" Value="False"><Setter TargetName="surface" Property="Opacity" Value="0.45"/></Trigger></ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType="TextBox">
            <Setter Property="Background" Value="{Surface}"/><Setter Property="Foreground" Value="{Ink}"/><Setter Property="CaretBrush" Value="{Ink}"/><Setter Property="BorderThickness" Value="0"/><Setter Property="Padding" Value="10,8"/><Setter Property="MinHeight" Value="36"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="TextBox"><Border x:Name="surface" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="8"><ScrollViewer x:Name="PART_ContentHost"/></Border><ControlTemplate.Triggers><Trigger Property="IsKeyboardFocusWithin" Value="True"><Setter TargetName="surface" Property="BorderBrush" Value="#FF5200"/><Setter TargetName="surface" Property="BorderThickness" Value="1.5"/></Trigger><Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.5"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType="ComboBoxItem">
            <Setter Property="Foreground" Value="{Ink}"/><Setter Property="Padding" Value="10,9"/>
            <Setter Property="MinHeight" Value="36"/><Setter Property="HorizontalContentAlignment" Value="Stretch"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ComboBoxItem">
              <Border x:Name="row" Background="Transparent" CornerRadius="6" Padding="{TemplateBinding Padding}" Margin="0,1">
                <Grid><Grid.ColumnDefinitions><ColumnDefinition/><ColumnDefinition Width="22"/></Grid.ColumnDefinitions>
                  <ContentPresenter VerticalAlignment="Center" HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}"/>
                  <Path x:Name="check" Grid.Column="1" Data="M 1,5 L 4,8 L 10,1" Stroke="#FF5200" StrokeThickness="1.7" StrokeStartLineCap="Round" StrokeEndLineCap="Round" HorizontalAlignment="Right" VerticalAlignment="Center" Visibility="Collapsed"/>
                </Grid>
              </Border>
              <ControlTemplate.Triggers>
                <Trigger Property="IsHighlighted" Value="True"><Setter TargetName="row" Property="Background" Value="{Hover}"/></Trigger>
                <Trigger Property="IsSelected" Value="True"><Setter TargetName="row" Property="Background" Value="{Tint}"/><Setter TargetName="check" Property="Visibility" Value="Visible"/></Trigger>
                <Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.45"/></Trigger>
              </ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType="ComboBox">
            <Setter Property="Padding" Value="11,8"/><Setter Property="MinHeight" Value="38"/>
            <Setter Property="Background" Value="{Surface}"/><Setter Property="Foreground" Value="{Ink}"/>
            <Setter Property="BorderThickness" Value="0"/>
            <Setter Property="MaxDropDownHeight" Value="320"/><Setter Property="ScrollViewer.HorizontalScrollBarVisibility" Value="Disabled"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ComboBox">
              <Grid SnapsToDevicePixels="True">
                <Border x:Name="surface" CornerRadius="8" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}"/>
                <p:ToggleButton x:Name="toggle" Focusable="False" ClickMode="Press" IsChecked="{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}">
                  <p:ToggleButton.Template><ControlTemplate TargetType="p:ToggleButton"><Border Background="Transparent" CornerRadius="8"/></ControlTemplate></p:ToggleButton.Template>
                </p:ToggleButton>
                <ContentPresenter x:Name="selection" Content="{TemplateBinding SelectionBoxItem}" ContentTemplate="{TemplateBinding SelectionBoxItemTemplate}" ContentTemplateSelector="{TemplateBinding ItemTemplateSelector}" Margin="11,8,34,8" VerticalAlignment="Center" HorizontalAlignment="Left" IsHitTestVisible="False"/>
                <TextBox x:Name="PART_EditableTextBox" Visibility="Collapsed" Margin="11,4,34,4" VerticalContentAlignment="Center" IsReadOnly="{TemplateBinding IsReadOnly}" Background="Transparent" Foreground="{TemplateBinding Foreground}">
                  <TextBox.Template><ControlTemplate TargetType="TextBox"><ScrollViewer x:Name="PART_ContentHost"/></ControlTemplate></TextBox.Template>
                </TextBox>
                <Path x:Name="chevron" Data="M 0,0 L 4,4 L 8,0" Stroke="{Muted}" StrokeThickness="1.5" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round" HorizontalAlignment="Right" VerticalAlignment="Center" Margin="0,0,14,0" IsHitTestVisible="False"/>
                <p:Popup x:Name="PART_Popup" Placement="Bottom" HorizontalOffset="-8" VerticalOffset="-4" AllowsTransparency="True" Focusable="False" IsOpen="{TemplateBinding IsDropDownOpen}" PopupAnimation="Fade">
                  <Border Margin="8" Padding="4" CornerRadius="9" Background="{Surface}" BorderBrush="{Line}" BorderThickness="1" MinWidth="{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}">
                    <Border.Effect><DropShadowEffect BlurRadius="12" ShadowDepth="3" Opacity="0.12" Color="#202832"/></Border.Effect>
                    <ScrollViewer MaxHeight="{TemplateBinding MaxDropDownHeight}" CanContentScroll="True" VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Disabled">
                      <ItemsPresenter KeyboardNavigation.DirectionalNavigation="Contained"/>
                    </ScrollViewer>
                  </Border>
                </p:Popup>
              </Grid>
              <ControlTemplate.Triggers>
                <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="surface" Property="Opacity" Value="0.9"/></Trigger>
                <Trigger Property="IsKeyboardFocusWithin" Value="True"><Setter TargetName="surface" Property="BorderBrush" Value="#FF5200"/><Setter TargetName="surface" Property="BorderThickness" Value="1.5"/></Trigger>
                <Trigger Property="IsDropDownOpen" Value="True"><Setter TargetName="surface" Property="BorderBrush" Value="#FF5200"/><Setter TargetName="surface" Property="BorderThickness" Value="1.5"/><Setter TargetName="chevron" Property="Stroke" Value="#FF5200"/></Trigger>
                <Trigger Property="IsEditable" Value="True"><Setter TargetName="selection" Property="Visibility" Value="Collapsed"/><Setter TargetName="PART_EditableTextBox" Property="Visibility" Value="Visible"/></Trigger>
                <Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.45"/></Trigger>
              </ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style x:Key="SliderRail" TargetType="p:RepeatButton">
            <Setter Property="Focusable" Value="False"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="p:RepeatButton">
              <Border x:Name="rail" Height="4" VerticalAlignment="Center" Background="{TemplateBinding Background}" CornerRadius="2"/>
              <ControlTemplate.Triggers><DataTrigger Binding="{Binding Orientation, RelativeSource={RelativeSource AncestorType=Slider}}" Value="Vertical">
                <Setter TargetName="rail" Property="Height" Value="Auto"/><Setter TargetName="rail" Property="Width" Value="4"/><Setter TargetName="rail" Property="VerticalAlignment" Value="Stretch"/><Setter TargetName="rail" Property="HorizontalAlignment" Value="Center"/>
              </DataTrigger></ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style x:Key="SliderHandle" TargetType="p:Thumb">
            <Setter Property="Width" Value="24"/><Setter Property="Height" Value="24"/><Setter Property="Cursor" Value="Hand"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="p:Thumb">
              <Grid Background="Transparent">
                <Ellipse x:Name="focus" Width="24" Height="24" Stroke="#66FF5200" StrokeThickness="2" Fill="Transparent" Visibility="Collapsed"/>
                <Ellipse x:Name="knob" Width="18" Height="18" Fill="White">
                  <Ellipse.Effect><DropShadowEffect BlurRadius="4" ShadowDepth="1" Opacity="0.25" Color="#000000"/></Ellipse.Effect>
                </Ellipse>
              </Grid>
              <ControlTemplate.Triggers>
                <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="knob" Property="Opacity" Value="0.95"/></Trigger>
                <Trigger Property="IsDragging" Value="True"><Setter TargetName="focus" Property="Visibility" Value="Visible"/></Trigger>
                <DataTrigger Binding="{Binding IsKeyboardFocusWithin, RelativeSource={RelativeSource AncestorType=Slider}}" Value="True"><Setter TargetName="focus" Property="Visibility" Value="Visible"/></DataTrigger>
              </ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType="Slider">
            <Setter Property="MinHeight" Value="28"/><Setter Property="Foreground" Value="#FF5200"/>
            <Setter Property="IsMoveToPointEnabled" Value="True"/><Setter Property="Background" Value="Transparent"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Slider">
              <Grid Background="{TemplateBinding Background}" SnapsToDevicePixels="True">
                <p:Track x:Name="PART_Track" Minimum="{TemplateBinding Minimum}" Maximum="{TemplateBinding Maximum}" Value="{Binding Value, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}" Orientation="{TemplateBinding Orientation}" IsDirectionReversed="{TemplateBinding IsDirectionReversed}">
                  <p:Track.DecreaseRepeatButton><p:RepeatButton Style="{StaticResource SliderRail}" Background="{TemplateBinding Foreground}" Command="Slider.DecreaseLarge"/></p:Track.DecreaseRepeatButton>
                  <p:Track.IncreaseRepeatButton><p:RepeatButton Style="{StaticResource SliderRail}" Background="{Rail}" Command="Slider.IncreaseLarge"/></p:Track.IncreaseRepeatButton>
                  <p:Track.Thumb><p:Thumb Style="{StaticResource SliderHandle}"/></p:Track.Thumb>
                </p:Track>
              </Grid>
              <ControlTemplate.Triggers><Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.45"/></Trigger></ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType="PasswordBox"><Setter Property="Padding" Value="10,8"/><Setter Property="MinHeight" Value="36"/><Setter Property="Background" Value="{Surface}"/><Setter Property="Foreground" Value="{Ink}"/><Setter Property="CaretBrush" Value="{Ink}"/><Setter Property="BorderThickness" Value="0"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="PasswordBox"><Border x:Name="surface" Background="{TemplateBinding Background}" BorderBrush="#FF5200" BorderThickness="0" CornerRadius="8"><ScrollViewer x:Name="PART_ContentHost" Margin="{TemplateBinding Padding}"/></Border><ControlTemplate.Triggers><Trigger Property="IsKeyboardFocusWithin" Value="True"><Setter TargetName="surface" Property="BorderThickness" Value="1.5"/></Trigger><Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.5"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType="CheckBox">
            <Setter Property="Foreground" Value="{Ink}"/><Setter Property="Padding" Value="6,4"/><Setter Property="MinHeight" Value="30"/><Setter Property="VerticalContentAlignment" Value="Center"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="CheckBox">
              <StackPanel Orientation="Horizontal" Background="Transparent">
                <Border x:Name="box" Width="16" Height="16" CornerRadius="4" Background="{Surface}" BorderBrush="{Line}" BorderThickness="1" VerticalAlignment="Center">
                  <Path x:Name="check" Data="M 3,8 L 7,12 L 13,4" Stroke="White" StrokeThickness="2" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round" Visibility="Collapsed"/>
                </Border>
                <ContentPresenter Margin="8,0,0,0" VerticalAlignment="Center"/>
              </StackPanel>
              <ControlTemplate.Triggers>
                <Trigger Property="IsChecked" Value="True"><Setter TargetName="box" Property="Background" Value="#FF5200"/><Setter TargetName="box" Property="BorderBrush" Value="#FF5200"/><Setter TargetName="check" Property="Visibility" Value="Visible"/></Trigger>
                <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="box" Property="BorderBrush" Value="#FF5200"/></Trigger>
                <Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.45"/></Trigger>
              </ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType="ListBox"><Setter Property="Background" Value="{Surface}"/><Setter Property="Foreground" Value="{Ink}"/><Setter Property="BorderBrush" Value="{Line}"/><Setter Property="Padding" Value="4"/><Setter Property="ScrollViewer.HorizontalScrollBarVisibility" Value="Disabled"/></Style>
          <Style TargetType="ListBoxItem"><Setter Property="Padding" Value="9,7"/><Setter Property="HorizontalContentAlignment" Value="Stretch"/><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ListBoxItem"><Border x:Name="row" Background="Transparent" CornerRadius="6" Padding="{TemplateBinding Padding}"><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property="IsSelected" Value="True"><Setter TargetName="row" Property="Background" Value="{Tint}"/></Trigger><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="row" Property="Background" Value="{Hover}"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
          <Style TargetType="DataGrid"><Setter Property="Background" Value="{Surface}"/><Setter Property="Foreground" Value="{Ink}"/><Setter Property="BorderBrush" Value="{Line}"/><Setter Property="RowBackground" Value="{Surface}"/><Setter Property="AlternatingRowBackground" Value="{Alt}"/><Setter Property="GridLinesVisibility" Value="None"/><Setter Property="HeadersVisibility" Value="Column"/><Setter Property="RowHeight" Value="34"/><Setter Property="ColumnHeaderHeight" Value="34"/></Style>
          <Style TargetType="DataGridColumnHeader"><Setter Property="Background" Value="{Hover}"/><Setter Property="Foreground" Value="{Ink}"/><Setter Property="Padding" Value="8"/><Setter Property="BorderThickness" Value="0"/></Style>
          <Style TargetType="TextBlock"><Setter Property="Foreground" Value="{Ink}"/></Style>
          <Style TargetType="Expander"><Setter Property="Foreground" Value="{Ink}"/></Style>
          <Style TargetType="ContextMenu"><Setter Property="Background" Value="{Surface}"/><Setter Property="Foreground" Value="{Ink}"/><Setter Property="BorderBrush" Value="{Line}"/></Style>
                  <Style TargetType="ScrollBar">
            <Setter Property="SnapsToDevicePixels" Value="True"/>
            <Setter Property="OverridesDefaultStyle" Value="true"/>
            <Style.Triggers>
              <Trigger Property="Orientation" Value="Vertical">
                <Setter Property="Width" Value="8"/>
                <Setter Property="Template">
                  <Setter.Value>
                    <ControlTemplate TargetType="ScrollBar">
                      <Grid Background="Transparent">
                        <Track x:Name="PART_Track" IsDirectionReversed="true">
                          <Track.Thumb>
                            <Thumb>
                              <Thumb.Template>
                                <ControlTemplate TargetType="Thumb">
                                  <Border x:Name="thumb" Background="{StrongLine}" CornerRadius="4" Margin="1,2,1,2"/>
                                  <ControlTemplate.Triggers>
                                    <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="thumb" Property="Background" Value="{Muted}"/></Trigger>
                                    <Trigger Property="IsDragging" Value="True"><Setter TargetName="thumb" Property="Background" Value="{Ink}"/></Trigger>
                                  </ControlTemplate.Triggers>
                                </ControlTemplate>
                              </Thumb.Template>
                            </Thumb>
                          </Track.Thumb>
                        </Track>
                      </Grid>
                    </ControlTemplate>
                  </Setter.Value>
                </Setter>
              </Trigger>
              <Trigger Property="Orientation" Value="Horizontal">
                <Setter Property="Height" Value="8"/>
                <Setter Property="Template">
                  <Setter.Value>
                    <ControlTemplate TargetType="ScrollBar">
                      <Grid Background="Transparent">
                        <Track x:Name="PART_Track">
                          <Track.Thumb>
                            <Thumb>
                              <Thumb.Template>
                                <ControlTemplate TargetType="Thumb">
                                  <Border x:Name="thumb" Background="{StrongLine}" CornerRadius="4" Margin="2,1,2,1"/>
                                  <ControlTemplate.Triggers>
                                    <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="thumb" Property="Background" Value="{Muted}"/></Trigger>
                                    <Trigger Property="IsDragging" Value="True"><Setter TargetName="thumb" Property="Background" Value="{Ink}"/></Trigger>
                                  </ControlTemplate.Triggers>
                                </ControlTemplate>
                              </Thumb.Template>
                            </Thumb>
                          </Track.Thumb>
                        </Track>
                      </Grid>
                    </ControlTemplate>
                  </Setter.Value>
                </Setter>
              </Trigger>
            </Style.Triggers>
          </Style>
        </ResourceDictionary>
        """));
        window.FontFamily = Font;
        window.Foreground = Ink;
        window.FontSize = 13;
        window.WindowStyle = WindowStyle.None;
        window.AllowsTransparency = true;
        window.Background = Brushes.Transparent;
    }
    public static UIElement Frame(Window window, UIElement content, string? title = null, bool toolChrome = false)
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(42) });
        root.RowDefinitions.Add(new RowDefinition());

        // Ambient color blobs for subtle macOS color bleed
        var ambientCanvas = new Canvas { IsHitTestVisible = false, ClipToBounds = true };
        Grid.SetRowSpan(ambientCanvas, 2);

        var blob1 = new System.Windows.Shapes.Ellipse { Width = 260, Height = 260 };
        Canvas.SetLeft(blob1, -60);
        Canvas.SetTop(blob1, -60);
        ambientCanvas.Children.Add(blob1);

        var blob2 = new System.Windows.Shapes.Ellipse { Width = 260, Height = 260 };
        Canvas.SetLeft(blob2, 420);
        Canvas.SetTop(blob2, -40);
        ambientCanvas.Children.Add(blob2);

        var blob3 = new System.Windows.Shapes.Ellipse { Width = 280, Height = 280 };
        Canvas.SetLeft(blob3, 170);
        Canvas.SetTop(blob3, 220);
        ambientCanvas.Children.Add(blob3);

        var frostedLayer = new Border { IsHitTestVisible = false, CornerRadius = new CornerRadius(WindowCornerRadius) };
        Grid.SetRowSpan(frostedLayer, 2);

        void UpdateBlobs()
        {
            if (Dark)
            {
                ambientCanvas.Visibility = Visibility.Visible;
                double r = Math.Max(5, BlurAmount * 1.5);
                double opac = Math.Clamp(BlurAmount / 100.0, 0.05, 1.0);
                blob1.Fill = new SolidColorBrush(Color.FromArgb((byte)(56 * opac), 255, 90, 0));
                blob1.Effect = new BlurEffect { Radius = r };
                blob2.Fill = new SolidColorBrush(Color.FromArgb((byte)(50 * opac), 96, 165, 250));
                blob2.Effect = new BlurEffect { Radius = r };
                blob3.Fill = new SolidColorBrush(Color.FromArgb((byte)(45 * opac), 168, 85, 247));
                blob3.Effect = new BlurEffect { Radius = r };

                byte fAlpha = (byte)Math.Clamp(Math.Round(BlurAmount * 0.45), 0, 120);
                frostedLayer.Background = new SolidColorBrush(Color.FromArgb(fAlpha, 22, 26, 32));
            }
            else
            {
                ambientCanvas.Visibility = Visibility.Collapsed;
                // Real glass: no colour of our own. The window shows the blurred desktop behind it, lightened by a thin
                // white veil, so a warm wallpaper gives a warm window and a dark one a calm grey-violet window.
                double veil = BlurAmount <= 0 ? 0.94 : FrameVeilAlpha(BlurAmount, GlassOpacity);
                var glass = new SolidColorBrush(Color.FromArgb((byte)Math.Round(255 * veil), 255, 252, 248));
                glass.Freeze();
                frostedLayer.Background = glass;
            }
        }
        UpdateBlobs();
        VisualsChanged += UpdateBlobs;

        root.Children.Add(ambientCanvas);
        root.Children.Add(frostedLayer);

        FrameworkElement caption;
        if (toolChrome)
        {
            var captionGrid = new Grid { Margin = new Thickness(18, 0, 18, 0), Background = Brushes.Transparent };

            SolidColorBrush CloseFill(double alphaFraction)
            {
                if (Ink is SolidColorBrush scb)
                {
                    var c = scb.Color;
                    return new SolidColorBrush(Color.FromArgb((byte)Math.Round(255 * alphaFraction), c.R, c.G, c.B));
                }
                return new SolidColorBrush(Color.FromArgb((byte)Math.Round(255 * alphaFraction), 128, 128, 128));
            }

            var closePath = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M0,0 L8,8 M8,0 L0,8"),
                Stroke = Ink,
                Opacity = 0.75,
                StrokeThickness = 1.5,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var closeButton = new Border
            {
                Width = 22,
                Height = 22,
                CornerRadius = new CornerRadius(11),
                Background = CloseFill(0.12),
                Cursor = System.Windows.Input.Cursors.Hand,
                Child = closePath,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 0, 0)
            };
            System.Windows.Automation.AutomationProperties.SetName(closeButton, "Close");
            closeButton.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                window.Close();
            };
            closeButton.MouseEnter += (_, _) => closeButton.Background = CloseFill(0.22);
            closeButton.MouseLeave += (_, _) => closeButton.Background = CloseFill(0.12);

            var titleBlock = new TextBlock
            {
                Text = title ?? "KwekDrop",
                Foreground = Title,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                IsHitTestVisible = false
            };

            captionGrid.Children.Add(titleBlock);
            captionGrid.Children.Add(closeButton);
            caption = captionGrid;
        }
        else
        {
            var captionDock = new DockPanel { Margin = new Thickness(18, 0, 18, 0), Background = Brushes.Transparent };
            // macOS Traffic Light dots
            var trafficLights = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(trafficLights, Dock.Left);

            Border TrafficDot(Color color, Action onClick)
            {
                var dot = new Border
                {
                    Width = 12, Height = 12, CornerRadius = new CornerRadius(6),
                    Background = new SolidColorBrush(color),
                    Margin = new Thickness(0, 0, 8, 0),
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                dot.MouseLeftButtonDown += (_, _) => onClick();
                return dot;
            }

            trafficLights.Children.Add(TrafficDot(Color.FromRgb(255, 95, 86), () => window.Close()));
            trafficLights.Children.Add(TrafficDot(Color.FromRgb(255, 189, 46), () => window.WindowState = WindowState.Minimized));
            trafficLights.Children.Add(TrafficDot(Color.FromRgb(39, 201, 63), () => { }));
            captionDock.Children.Add(trafficLights);

            var titleBlock = new TextBlock
            {
                Text = title ?? "KwekDrop",
                Foreground = Title,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };
            captionDock.Children.Add(titleBlock);
            caption = captionDock;
        }

        caption.MouseLeftButtonDown += (s, e) =>
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
            {
                try { window.DragMove(); } catch { }
            }
        };

        root.Children.Add(caption);
        Grid.SetRow(content, 1);
        root.Children.Add(content);
        var surface = new ContinuousFrame { Child = root, CornerRadius = WindowCornerRadius };
        void UpdateCorners() => surface.CornerRadius = window.WindowState == WindowState.Maximized ? 0 : WindowCornerRadius;
        EventHandler changed = (_, _) => UpdateCorners();
        Action onVisuals = () =>
        {
            window.Background = WindowBack;
            UpdateBlobs();
            WindowBlur.ApplyRoundedRectBlur(window, (int)WindowCornerRadius, Dark);
        };
        surface.Loaded += (_, _) =>
        {
            window.StateChanged += changed;
            UpdateCorners();
            UpdateBlobs();
            WindowBlur.ApplyRoundedRectBlur(window, (int)WindowCornerRadius, Dark);
            VisualsChanged += onVisuals;
        };
        window.SizeChanged += (_, _) => WindowBlur.ApplyRoundedRectBlur(window, (int)WindowCornerRadius, Dark);
        surface.Unloaded += (_, _) =>
        {
            window.StateChanged -= changed;
            VisualsChanged -= onVisuals;
        };
        UpdateCorners();

        // Outer wrapper grid that holds surface and edge resizing handles
        var frameGrid = new Grid();
        frameGrid.Children.Add(surface);

        bool isResizable = window.ResizeMode != ResizeMode.NoResize;
        var helper = new WindowInteropHelper(window);
        void HookWndProc()
        {
            var hwnd = helper.Handle;
            if (hwnd == IntPtr.Zero) return;
            var source = HwndSource.FromHwnd(hwnd);
            source?.AddHook((IntPtr h, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
            {
                if (msg == 0x0084) // WM_NCHITTEST
                {
                    int screenX = unchecked((short)(long)lParam);
                    int screenY = unchecked((short)((long)lParam >> 16));
                    var pt = window.PointFromScreen(new Point(screenX, screenY));

                    if (isResizable && window.WindowState != WindowState.Maximized)
                    {
                        double border = 8.0;
                        bool l = pt.X <= border;
                        bool r = pt.X >= window.ActualWidth - border;
                        bool t = pt.Y <= border;
                        bool b = pt.Y >= window.ActualHeight - border;

                        if (t && l) { handled = true; return (IntPtr)13; } // HTTOPLEFT
                        if (t && r) { handled = true; return (IntPtr)14; } // HTTOPRIGHT
                        if (b && l) { handled = true; return (IntPtr)16; } // HTBOTTOMLEFT
                        if (b && r) { handled = true; return (IntPtr)17; } // HTBOTTOMRIGHT
                        if (l) { handled = true; return (IntPtr)10; } // HTLEFT
                        if (r) { handled = true; return (IntPtr)11; } // HTRIGHT
                        if (t) { handled = true; return (IntPtr)12; } // HTTOP
                        if (b) { handled = true; return (IntPtr)15; } // HTBOTTOM
                    }

                    // Draggable header area (row 0)
                    if (pt.Y >= 0 && pt.Y <= 44 && pt.X >= 75 && pt.X <= window.ActualWidth - 20)
                    {
                        var result = VisualTreeHelper.HitTest(window, pt);
                        var hit = result?.VisualHit;
                        bool interactive = false;
                        while (hit != null && hit != window)
                        {
                            if (hit is Button or TextBox or ComboBox or Slider or System.Windows.Controls.Primitives.Thumb or System.Windows.Controls.Primitives.RepeatButton)
                            {
                                interactive = true;
                                break;
                            }
                            hit = VisualTreeHelper.GetParent(hit);
                        }
                        if (!interactive)
                        {
                            handled = true;
                            return (IntPtr)2; // HTCAPTION
                        }
                    }
                }
                return IntPtr.Zero;
            });
        }
        if (helper.Handle != IntPtr.Zero) HookWndProc();
        else window.SourceInitialized += (_, _) => HookWndProc();

        if (isResizable)
        {
            void StartResize(int dir)
            {
                if (window.WindowState == WindowState.Maximized) return;
                Native.ReleaseCapture();
                var hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd != IntPtr.Zero)
                    Native.SendMessage(hwnd, 0x0112, (IntPtr)(0xF000 + dir), IntPtr.Zero);
            }

            // 8 Transparent overlay handles for guaranteed, instant resize cursor and dragging
            var leftH = new Border { Width = 8, HorizontalAlignment = HorizontalAlignment.Left, Background = Brushes.Transparent, Cursor = System.Windows.Input.Cursors.SizeWE };
            leftH.MouseLeftButtonDown += (_, _) => StartResize(1); // WMSZ_LEFT
            Grid.SetZIndex(leftH, 9999);
            frameGrid.Children.Add(leftH);

            var rightH = new Border { Width = 8, HorizontalAlignment = HorizontalAlignment.Right, Background = Brushes.Transparent, Cursor = System.Windows.Input.Cursors.SizeWE };
            rightH.MouseLeftButtonDown += (_, _) => StartResize(2); // WMSZ_RIGHT
            Grid.SetZIndex(rightH, 9999);
            frameGrid.Children.Add(rightH);

            var topH = new Border { Height = 8, VerticalAlignment = VerticalAlignment.Top, Background = Brushes.Transparent, Cursor = System.Windows.Input.Cursors.SizeNS, Margin = new Thickness(8, 0, 8, 0) };
            topH.MouseLeftButtonDown += (_, _) => StartResize(3); // WMSZ_TOP
            Grid.SetZIndex(topH, 9999);
            frameGrid.Children.Add(topH);

            var botH = new Border { Height = 8, VerticalAlignment = VerticalAlignment.Bottom, Background = Brushes.Transparent, Cursor = System.Windows.Input.Cursors.SizeNS, Margin = new Thickness(8, 0, 8, 0) };
            botH.MouseLeftButtonDown += (_, _) => StartResize(6); // WMSZ_BOTTOM
            Grid.SetZIndex(botH, 9999);
            frameGrid.Children.Add(botH);

            var tlH = new Border { Width = 18, Height = 18, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Background = Brushes.Transparent, Cursor = System.Windows.Input.Cursors.SizeNWSE };
            tlH.MouseLeftButtonDown += (_, _) => StartResize(4); // WMSZ_TOPLEFT
            Grid.SetZIndex(tlH, 10000);
            frameGrid.Children.Add(tlH);

            var trH = new Border { Width = 18, Height = 18, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Background = Brushes.Transparent, Cursor = System.Windows.Input.Cursors.SizeNESW };
            trH.MouseLeftButtonDown += (_, _) => StartResize(5); // WMSZ_TOPRIGHT
            Grid.SetZIndex(trH, 10000);
            frameGrid.Children.Add(trH);

            var blH = new Border { Width = 18, Height = 18, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, Background = Brushes.Transparent, Cursor = System.Windows.Input.Cursors.SizeNESW };
            blH.MouseLeftButtonDown += (_, _) => StartResize(7); // WMSZ_BOTTOMLEFT
            Grid.SetZIndex(blH, 10000);
            frameGrid.Children.Add(blH);

            var brH = new Border { Width = 18, Height = 18, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Background = Brushes.Transparent, Cursor = System.Windows.Input.Cursors.SizeNWSE };
            brH.MouseLeftButtonDown += (_, _) => StartResize(8); // WMSZ_BOTTOMRIGHT
            Grid.SetZIndex(brH, 10000);
            frameGrid.Children.Add(brH);
        }

        return frameGrid;
    }
    public static Border Card(UIElement content, Thickness padding) => new()
    {
        Child = content,
        Background = Dark ? Surface : Brushes.Transparent,
        BorderBrush = Dark ? Line : Brushes.Transparent,
        BorderThickness = Dark ? new Thickness(1) : new Thickness(0),
        CornerRadius = new CornerRadius(12),
        Padding = padding,
        Effect = Dark ? new DropShadowEffect { BlurRadius = 14, ShadowDepth = 2, Opacity = 0.04, Color = Colors.Black } : null
    };
}
