using Lunaris.Core.Interfaces;

namespace Lunaris.Core.Services;

/// <summary>Transient notifications, routed to the tray balloon by the app shell.</summary>
public sealed class NotificationService : INotificationService
{
    public Action<string, string>? Sink { get; set; }

    public void Show(string title, string message) => Sink?.Invoke(title, message);
}