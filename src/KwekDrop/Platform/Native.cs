using System;
using System.Runtime.InteropServices;
using System.Text;

namespace KwekDrop;

internal static class Native
{
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT point);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, StringBuilder name, int capacity);
    public static bool ShellFileWindow()
    {
        var name = new StringBuilder(256);
        GetClassName(GetForegroundWindow(), name, name.Capacity);
        return name.ToString() is "CabinetWClass" or "ExploreWClass" or "Progman" or "WorkerW";
    }
    // Lets a window in another process come to the front; used when a second launch asks the running copy to show itself.
    [DllImport("user32.dll")] public static extern bool AllowSetForegroundWindow(int processId);
    [DllImport("user32.dll")] public static extern bool ReleaseCapture();
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr hObject);
    public static bool Down(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string? className, string? windowName);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);

    // Explorer shows the picture of what is dragged in a window of this class: if it is there, files (or other shell items) are on the move.
    public static bool ShellDragInProgress() { var window = FindWindow("SysDragImage", null); return window != IntPtr.Zero && IsWindowVisible(window); }
}
