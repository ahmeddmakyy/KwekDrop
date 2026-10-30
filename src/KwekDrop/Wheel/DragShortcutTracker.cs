using System;

namespace KwekDrop;

internal enum DragIntent { None, Convert, Tools, Actions }

internal sealed class DragShortcutTracker(int horizontalThreshold, int verticalThreshold)
{
    private bool down, moved, suppressed;
    private int startX, startY;
    private DragIntent last;

    public static DragIntent Resolve(bool shift, bool control, bool alt)
    {
        string toolsKey = Settings.GetString("toolsKey")?.ToLowerInvariant() ?? "alt";
        string actionsKey = Settings.GetString("actionsKey")?.ToLowerInvariant() ?? "ctrl";
        string convertKey = Settings.GetString("convertKey")?.ToLowerInvariant() ?? "shift";

        static bool IsHeld(string key, bool shift, bool control, bool alt) => key switch
        {
            "shift" => shift,
            "ctrl" => control,
            "alt" => alt,
            _ => false
        };

        if (IsHeld(toolsKey, shift, control, alt)) return DragIntent.Tools;
        if (IsHeld(actionsKey, shift, control, alt)) return DragIntent.Actions;
        if (IsHeld(convertKey, shift, control, alt)) return DragIntent.Convert;
        return DragIntent.None;
    }

    public void Cancel() => suppressed = true;

    public void Reset()
    {
        down = moved = suppressed = false;
        last = DragIntent.None;
    }

    // The wheel closed because the modifier was released, but the drag is still
    // alive. Keep down/moved so pressing the same modifier again reopens it even
    // when the pointer has not moved since.
    public void ClearIntent() => last = DragIntent.None;

    public DragIntent Update(bool mouseDown, bool shift, bool control, bool alt, int x, int y)
    {
        if (!mouseDown)
        {
            Reset();
            return DragIntent.None;
        }
        if (!down)
        {
            down = true;
            startX = x;
            startY = y;
        }
        moved |= Math.Abs((long)x - startX) >= Math.Max(2, horizontalThreshold) || Math.Abs((long)y - startY) >= Math.Max(2, verticalThreshold);
        if (!moved || suppressed)
            return DragIntent.None;
        var intent = Resolve(shift, control, alt);
        if (intent == last)
            return DragIntent.None;
        last = intent;
        return intent;
    }
}

internal static class DragKeys
{
    public const string ConvertSetting = "convertKey";
    public const string ToolsSetting = "toolsKey";
    public const string ActionsSetting = "actionsKey";

    public static string For(DragIntent intent) => intent switch
    {
        DragIntent.Convert => Settings.GetString(ConvertSetting)?.ToLowerInvariant() ?? "shift",
        DragIntent.Tools => Settings.GetString(ToolsSetting)?.ToLowerInvariant() ?? "alt",
        DragIntent.Actions => Settings.GetString(ActionsSetting)?.ToLowerInvariant() ?? "ctrl",
        _ => "none"
    };

    public static string DisplayName(string key) => key.ToLowerInvariant() switch
    {
        "shift" => "Shift",
        "ctrl" => "Ctrl",
        "alt" => "Alt",
        "none" => "None",
        _ => key
    };

    public static bool IsKeyDown(string keyName) => keyName.ToLowerInvariant() switch
    {
        "shift" => Native.Down(0x10),
        "ctrl" => Native.Down(0x11),
        "alt" => Native.Down(0x12),
        _ => false
    };
}