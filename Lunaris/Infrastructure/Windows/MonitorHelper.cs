namespace Lunaris.Infrastructure.Windows;

/// <summary>Provides monitor-aware geometry used to center the launcher on the active display.</summary>
public static class MonitorHelper
{
    public static NativeMethods.RECT GetWorkAreaForCursor()
    {
        NativeMethods.GetCursorPos(out var point);
        var monitor = NativeMethods.MonitorFromPoint(point, 0x00000002 /*MONITOR_DEFAULTTONEAREST*/);

        var info = new NativeMethods.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        if (monitor != IntPtr.Zero && NativeMethods.GetMonitorInfo(monitor, ref info))
            return info.rcWork;

        return GetPrimaryWorkArea();
    }

    public static NativeMethods.RECT GetPrimaryWorkArea()
    {
        // Fallback: virtual screen of the primary monitor via SystemParameters is complex,
        // so use the monitor of the cursor on the primary display.
        var info = new NativeMethods.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        var primary = NativeMethods.MonitorFromPoint(new NativeMethods.POINT { X = 0, Y = 0 }, 0x00000001 /*MONITOR_DEFAULTTOPRIMARY*/);
        if (NativeMethods.GetMonitorInfo(primary, ref info))
            return info.rcWork;

        return new NativeMethods.RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1040 };
    }
}