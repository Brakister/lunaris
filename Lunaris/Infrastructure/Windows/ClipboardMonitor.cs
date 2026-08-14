using System.Windows;
using Lunaris.Core.Interfaces;
using Lunaris.Infrastructure.Logging;

namespace Lunaris.Infrastructure.Windows;

/// <summary>
/// Clipboard listener using AddClipboardFormatListener. Runs on the UI thread so
/// clipboard access is safe.
/// </summary>
public sealed class ClipboardMonitor : IClipboardMonitor
{
    private readonly HiddenMessageWindow _messageWindow;
    private string _lastSeen = string.Empty;
    private bool _running;

    public event EventHandler<string>? TextCopied;

    public ClipboardMonitor(HiddenMessageWindow messageWindow)
    {
        _messageWindow = messageWindow;
    }

    public void Start()
    {
        if (_running)
            return;

        if (_messageWindow.Handle == IntPtr.Zero)
            _messageWindow.Initialize();

        if (NativeMethods.AddClipboardFormatListener(_messageWindow.Handle))
        {
            _running = true;
            _messageWindow.AddHandler(OnMessage);
            Log.Info("Clipboard monitor started");
        }
        else
        {
            Log.Warn("AddClipboardFormatListener failed");
        }
    }

    public void Stop()
    {
        if (!_running)
            return;

        if (_messageWindow.Handle != IntPtr.Zero)
            NativeMethods.RemoveClipboardFormatListener(_messageWindow.Handle);
        _running = false;
    }

    /// <summary>Allows the app to avoid recording its own clipboard writes.</summary>
    public void SuppressNext(string text) => _lastSeen = text;

    private bool OnMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg != NativeMethods.WM_CLIPBOARDUPDATE)
            return false;

        try
        {
            if (!Clipboard.ContainsText())
                return true;

            var text = Clipboard.GetText(TextDataFormat.UnicodeText);
            if (string.IsNullOrEmpty(text) || text.Length > 50000)
                return true;

            if (text == _lastSeen)
                return true;

            _lastSeen = text;
            TextCopied?.Invoke(this, text);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Clipboard read failed");
        }

        return true;
    }
}