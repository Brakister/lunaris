using System.Windows.Input;
using System.Windows.Interop;
using Lunaris.Core.Interfaces;
using Lunaris.Infrastructure.Logging;

namespace Lunaris.Infrastructure.Windows;

/// <summary>
/// Registers a global hotkey (default ALT + SPACE) using RegisterHotKey.
/// </summary>
public sealed class HotkeyService : IHotkeyService
{
    private const int HotkeyId = 0x4C55; // "LU"

    private readonly HiddenMessageWindow _messageWindow;
    private uint _modifiers;
    private uint _keyCode;

    public event EventHandler? HotkeyPressed;

    public bool IsRegistered { get; private set; }

    public HotkeyService(HiddenMessageWindow messageWindow)
    {
        _messageWindow = messageWindow;
    }

    public bool Register(Key[] modifiers, Key key)
    {
        Unregister();

        if (_messageWindow.Handle == IntPtr.Zero)
            _messageWindow.Initialize();

        var handle = _messageWindow.Handle;
        if (handle == IntPtr.Zero)
            return false;

        _modifiers = ToModifiers(modifiers) | NativeMethods.MOD_NOREPEAT;
        _keyCode = (uint)KeyInterop.VirtualKeyFromKey(key);

        if (NativeMethods.RegisterHotKey(handle, HotkeyId, _modifiers, _keyCode))
        {
            IsRegistered = true;
            _messageWindow.AddHandler(OnMessage);
            Log.Info("Global hotkey registered: {Combo}", ComboText(modifiers, key));
            return true;
        }

        var win32Error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
        Log.Error("RegisterHotKey failed: handle={HotkeyHandle}, mods=0x{Modifiers:X4}, vk=0x{KeyCode:X2}, win32={Win32}, combo={Combo}",
            handle, _modifiers, _keyCode, win32Error, ComboText(modifiers, key));
        return false;
    }

    public void Unregister()
    {
        if (IsRegistered && _messageWindow.Handle != IntPtr.Zero)
        {
            NativeMethods.UnregisterHotKey(_messageWindow.Handle, HotkeyId);
            IsRegistered = false;
        }
    }

    private bool OnMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            return true;
        }

        return false;
    }

    private static uint ToModifiers(Key[] modifiers)
    {
        // WPF Key values are arbitrary ints, not bit flags, so use equality.
        uint result = 0;
        foreach (var modifier in modifiers)
        {
            if (modifier is Key.LeftCtrl or Key.RightCtrl)
                result |= NativeMethods.MOD_CONTROL;
            else if (modifier is Key.LeftAlt or Key.RightAlt)
                result |= NativeMethods.MOD_ALT;
            else if (modifier is Key.LeftShift or Key.RightShift)
                result |= NativeMethods.MOD_SHIFT;
            else if (modifier is Key.LWin or Key.RWin)
                result |= NativeMethods.MOD_WIN;
        }
        return result;
    }

    private static string ComboText(Key[] modifiers, Key key) =>
        $"{string.Join("+", modifiers.Select(m => m.ToString()))}+{key}";
}