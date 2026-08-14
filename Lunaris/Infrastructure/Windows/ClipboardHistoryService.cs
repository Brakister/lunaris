using Lunaris.Core.Interfaces;
using Lunaris.Core.Models;
using Lunaris.Core.Utilities;
using Lunaris.Infrastructure.Database;
using Lunaris.Infrastructure.Logging;

namespace Lunaris.Infrastructure.Windows;

public sealed class ClipboardHistoryService : IClipboardHistoryService
{
    private readonly IClipboardMonitor _monitor;
    private readonly ClipboardRepository _repository;
    private readonly ISettingsService _settings;

    public ClipboardHistoryService(
        IClipboardMonitor monitor,
        ClipboardRepository repository,
        ISettingsService settings)
    {
        _monitor = monitor;
        _repository = repository;
        _settings = settings;
    }

    public void Start()
    {
        _monitor.TextCopied += OnTextCopied;
        _monitor.Start();
    }

    public void Stop()
    {
        _monitor.TextCopied -= OnTextCopied;
        _monitor.Stop();
    }

    private void OnTextCopied(object? sender, string text)
    {
        try
        {
            if (!_settings.Current.StoreClipboard)
                return;

            if (text.Length > 50000 || text.Length < 2)
                return;

            _repository.Insert(text);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to persist clipboard item");
        }
    }

    public IReadOnlyList<SearchResult> Search(string query, int limit)
    {
        if (!_settings.Current.EnableClipboard || !_settings.Current.StoreClipboard)
            return Array.Empty<SearchResult>();

        return _repository.Search(query, limit);
    }

    public IReadOnlyList<string> GetRecent(int limit)
    {
        if (!_settings.Current.EnableClipboard || !_settings.Current.StoreClipboard)
            return Array.Empty<string>();
        return _repository.GetRecent(limit);
    }

    public void Clear()
    {
        _repository.Clear();
        Log.Info("Clipboard history cleared");
    }
}