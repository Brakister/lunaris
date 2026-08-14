using System.Windows;
using System.Windows.Interop;

namespace Lunaris.Infrastructure.Windows;

public static class WindowHelper
{
    /// <summary>Shows a window and reliably steals focus from the previously active window.</summary>
    public static void ShowAndActivate(Window window)
    {
        if (!window.IsVisible)
            window.Show();

        if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;

        window.Activate();
        window.Topmost = true;

        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
            return;

        var foreground = NativeMethods.GetForegroundWindow();
        if (foreground == handle)
            return;

        var foregroundThread = NativeMethods.GetWindowThreadProcessId(foreground, out _);
        var currentThread = NativeMethods.GetCurrentThreadId();
        if (foregroundThread != currentThread)
        {
            NativeMethods.AttachThreadInput(currentThread, foregroundThread, true);
            NativeMethods.SetForegroundWindow(handle);
            NativeMethods.AttachThreadInput(currentThread, foregroundThread, false);
        }
        else
        {
            NativeMethods.SetForegroundWindow(handle);
        }
    }
}