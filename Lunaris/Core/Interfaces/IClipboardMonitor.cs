namespace Lunaris.Core.Interfaces;

/// <summary>Observes the Windows clipboard for text changes.</summary>
public interface IClipboardMonitor
{
    event EventHandler<string>? TextCopied;

    /// <summary>Prevents the next identical clipboard write from being recorded.</summary>
    void SuppressNext(string text);

    void Start();

    void Stop();
}