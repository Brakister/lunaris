namespace Lunaris.Core.Interfaces;

/// <summary>Shows transient notifications (currently through the tray balloon).</summary>
public interface INotificationService
{
    void Show(string title, string message);
}