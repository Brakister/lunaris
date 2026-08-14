using System.Windows.Interop;

namespace Lunaris.Infrastructure.Windows;

/// <summary>
/// A hidden window used as the message pump for the global hotkey, tray icon
/// and clipboard notifications. Keeps WPF out of code-behind concerns.
/// </summary>
public sealed class HiddenMessageWindow : IDisposable
{
    private readonly object _gate = new();
    private readonly List<Func<IntPtr, int, IntPtr, IntPtr, bool>> _handlers = new();
    private HwndSource? _source;

    public IntPtr Handle => _source?.Handle ?? IntPtr.Zero;

    public void Initialize()
    {
        if (_source is not null)
            return;

        var parameters = new HwndSourceParameters("LunarisMessageWindow")
        {
            ParentWindow = new IntPtr(NativeMethods.HWND_MESSAGE),
            WindowStyle = 0,
        };

        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    public void AddHandler(Func<IntPtr, int, IntPtr, IntPtr, bool> handler)
    {
        lock (_gate)
            _handlers.Add(handler);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        Func<IntPtr, int, IntPtr, IntPtr, bool>[] snapshot;
        lock (_gate)
            snapshot = _handlers.ToArray();

        foreach (var handler in snapshot)
        {
            if (handler(hwnd, msg, wParam, lParam))
            {
                handled = true;
                return IntPtr.Zero;
            }
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        _source?.RemoveHook(WndProc);
        _source?.Dispose();
        _source = null;
    }
}