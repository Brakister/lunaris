using System.Windows.Input;

namespace Lunaris.Core.Interfaces;

public interface IHotkeyService
{
    /// <summary>Fired on the UI thread whenever the global hotkey is pressed.</summary>
    event EventHandler? HotkeyPressed;

    bool IsRegistered { get; }

    /// <summary>Registers the hotkey (e.g. Ctrl+Alt+Space). Returns false (and logs) when the combination is unavailable.</summary>
    bool Register(Key[] modifiers, Key key);

    void Unregister();
}
