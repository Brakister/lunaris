using System.Threading;

namespace Lunaris.Infrastructure.Windows;

/// <summary>
/// Ensures only one Lunaris instance runs. A second launch signals the first
/// instance to show the launcher and then exits.
/// </summary>
public sealed class SingleInstanceService : IDisposable
{
    private const string MutexName = @"Global\Lunaris.SingleInstance";
    private const string ShowEventName = @"Global\Lunaris.ShowLauncher";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle? _showEvent;
    private readonly CancellationTokenSource _cts = new();

    public bool IsPrimary { get; }

    public SingleInstanceService()
    {
        _mutex = new Mutex(initiallyOwned: false, MutexName);
        IsPrimary = _mutex.WaitOne(0, false);

        if (IsPrimary)
        {
            _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        }
    }

    /// <summary>
    /// Starts listening for "show launcher" requests from secondary instances.
    /// The callback runs on a background thread; marshal it to the UI thread yourself.
    /// </summary>
    public void StartListenForShow(Action onShow)
    {
        if (!IsPrimary || _showEvent is null)
            return;

        Task.Run(async () =>
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    if (_showEvent.WaitOne(500))
                        onShow();
                }
                catch (AbandonedMutexException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }
        }, _cts.Token);
    }

    /// <summary>Called from a secondary instance: signals the primary to show.</summary>
    public void SignalPrimary()
    {
        try
        {
            using var showEvent = EventWaitHandle.OpenExisting(ShowEventName);
            showEvent.Set();
        }
        catch
        {
            // Primary not running (e.g. stale handle); nothing to do.
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _showEvent?.Dispose();
        if (IsPrimary)
            _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}