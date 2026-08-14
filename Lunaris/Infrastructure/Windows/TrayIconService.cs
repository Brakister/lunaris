using System.IO;
using Lunaris.Core.Interfaces;
using Lunaris.Infrastructure.Logging;

namespace Lunaris.Infrastructure.Windows;

/// <summary>Delegates used by the tray context menu, provided by the DI container.</summary>
public sealed class TrayMenuActions
{
    public required Action Open { get; init; }

    public required Action Settings { get; init; }

    public required Action Reindex { get; init; }

    public required Func<string> PauseLabel { get; init; }

    public required Action TogglePause { get; init; }

    public required Action About { get; init; }

    public required Action CheckUpdates { get; init; }

    public required Action Exit { get; init; }
}

/// <summary>
/// Native Windows system tray icon with a context menu, implemented with Shell_NotifyIcon
/// (no WinForms dependency).
/// </summary>
public sealed class TrayIconService : IDisposable, INotificationService
{
    private const uint TrayId = 1;
    private const uint MenuOpen = 1;
    private const uint MenuSettings = 2;
    private const uint MenuReindex = 3;
    private const uint MenuPause = 4;
    private const uint MenuAbout = 5;
    private const uint MenuCheckUpdates = 6;
    private const uint MenuExit = 7;

    private readonly HiddenMessageWindow _messageWindow;
    private readonly TrayMenuActions _actions;

    private IntPtr _iconHandle = IntPtr.Zero;
    private bool _added;

    public TrayIconService(HiddenMessageWindow messageWindow, TrayMenuActions actions)
    {
        _messageWindow = messageWindow;
        _actions = actions;
    }

    public void Initialize()
    {
        if (_messageWindow.Handle == IntPtr.Zero)
            _messageWindow.Initialize();

        _messageWindow.AddHandler(OnMessage);

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Lunaris.ico");
        if (!File.Exists(iconPath))
            iconPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lunaris", "Lunaris.ico");

        _iconHandle = IconHelper.LoadIconFromFile(iconPath, 32);
        if (_iconHandle == IntPtr.Zero)
            Log.Warn("Tray icon could not be loaded from {Path}", iconPath);

        var data = new NativeMethods.NOTIFYICONDATA
        {
            cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.NOTIFYICONDATA>(),
            hWnd = _messageWindow.Handle,
            uID = TrayId,
            uFlags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP,
            uCallbackMessage = NativeMethods.WM_TRAY,
            hIcon = _iconHandle,
            szTip = "Lunaris Launcher",
        };

        if (!NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_ADD, ref data))
        {
            Log.Error("Shell_NotifyIcon(NIM_ADD) failed");
            return;
        }

        _added = true;
        data.uVersion = 4; // NOTIFYICON_VERSION_4
        NativeMethods.Shell_NotifyIcon(0x00000004 /*NIM_SETVERSION*/, ref data);
        Log.Info("Tray icon added");
    }

    public void Show(string title, string message)
    {
        if (!_added)
            return;

        var data = new NativeMethods.NOTIFYICONDATA
        {
            cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.NOTIFYICONDATA>(),
            hWnd = _messageWindow.Handle,
            uID = TrayId,
            uFlags = NativeMethods.NIF_INFO,
            szInfoTitle = title.Length > 63 ? title[..63] : title,
            szInfo = message.Length > 255 ? message[..255] : message,
            dwInfoFlags = NativeMethods.NIIF_INFO,
        };
        NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_MODIFY, ref data);
    }

    private bool OnMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg != NativeMethods.WM_TRAY)
            return false;

        // With NOTIFYICON_VERSION_4, LOWORD(lParam) carries the event and HIWORD(lParam)
        // carries the icon id. Comparing the full lParam can miss the event completely.
        var mouseMessage = unchecked((int)((uint)lParam.ToInt64() & 0xFFFF));
        switch (mouseMessage)
        {
            case NativeMethods.WM_LBUTTONUP:
                _actions.Open();
                return true;

            case NativeMethods.WM_LBUTTONDBLCLK:
                _actions.Open();
                return true;

            case NativeMethods.WM_RBUTTONUP:
            case NativeMethods.WM_CONTEXTMENU:
                ShowContextMenu();
                return true;
        }

        return false;
    }

    private void ShowContextMenu()
    {
        var handle = _messageWindow.Handle;
        var menu = NativeMethods.CreatePopupMenu();
        if (menu == IntPtr.Zero)
            return;

        NativeMethods.AppendMenu(menu, NativeMethods.MF_STRING, MenuOpen, "Abrir");
        NativeMethods.AppendMenu(menu, NativeMethods.MF_SEPARATOR, 0, null);
        NativeMethods.AppendMenu(menu, NativeMethods.MF_STRING, MenuSettings, "Configurações");
        NativeMethods.AppendMenu(menu, NativeMethods.MF_STRING, MenuReindex, "Reindexar");
        NativeMethods.AppendMenu(menu, NativeMethods.MF_STRING, MenuPause, _actions.PauseLabel());
        NativeMethods.AppendMenu(menu, NativeMethods.MF_STRING, MenuAbout, "Sobre");
        NativeMethods.AppendMenu(menu, NativeMethods.MF_STRING, MenuCheckUpdates, "Verificar atualizações");
        NativeMethods.AppendMenu(menu, NativeMethods.MF_SEPARATOR, 0, null);
        NativeMethods.AppendMenu(menu, NativeMethods.MF_STRING, MenuExit, "Sair");

        // For NOTIFYICON_VERSION_4, the shell provides the anchor point in wParam for
        // keyboard-triggered context menu events. Fall back to the cursor otherwise.
        var point = new NativeMethods.POINT();
        if (NativeMethods.GetCursorPos(out var cursor))
            point = cursor;

        // The menu must be dismissed by a foreground window to behave properly.
        NativeMethods.SetForegroundWindow(handle);

        var command = NativeMethods.TrackPopupMenu(menu,
            NativeMethods.TPM_RIGHTBUTTON | NativeMethods.TPM_NONOTIFY | NativeMethods.TPM_RETURNCMD,
            point.X, point.Y, 0, handle, IntPtr.Zero);

        NativeMethods.PostMessage(handle, NativeMethods.WM_NULL, IntPtr.Zero, IntPtr.Zero);
        NativeMethods.DestroyMenu(menu);

        switch (command)
        {
            case MenuOpen: _actions.Open(); break;
            case MenuSettings: _actions.Settings(); break;
            case MenuReindex: _actions.Reindex(); break;
            case MenuPause: _actions.TogglePause(); break;
            case MenuAbout: _actions.About(); break;
            case MenuCheckUpdates: _actions.CheckUpdates(); break;
            case MenuExit: _actions.Exit(); break;
        }
    }

    public void Dispose()
    {
        if (_added)
        {
            var data = new NativeMethods.NOTIFYICONDATA
            {
                cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.NOTIFYICONDATA>(),
                hWnd = _messageWindow.Handle,
                uID = TrayId,
            };
            NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_DELETE, ref data);
            _added = false;
        }

        if (_iconHandle != IntPtr.Zero)
        {
            NativeMethods.DestroyIcon(_iconHandle);
            _iconHandle = IntPtr.Zero;
        }
    }
}
