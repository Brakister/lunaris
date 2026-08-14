using Lunaris.Core.Interfaces;
using Lunaris.Core.Models;
using Lunaris.Infrastructure.Database;
using Lunaris.Infrastructure.Logging;

namespace Lunaris.Core.Services;

public sealed class HistoryService : IHistoryService
{
    private static readonly HashSet<SearchResultKind> SensitiveKinds = new()
    {
        SearchResultKind.Calculation,
        SearchResultKind.TextAction,
        SearchResultKind.ClipboardItem,
    };

    private readonly HistoryRepository _repository;
    private readonly ISettingsService _settings;
    private readonly IActionRunner _runner;
    private readonly Dictionary<string, UsageStats> _statsCache = new();

    public HistoryService(HistoryRepository repository, ISettingsService settings, IActionRunner runner)
    {
        _repository = repository;
        _settings = settings;
        _runner = runner;
    }

    public void Record(string query, SearchResult result)
    {
        if (!_settings.Current.StoreHistory)
            return;

        if (SensitiveKinds.Contains(result.Kind))
            return;

        try
        {
            _repository.Record(query, result);

            if (_statsCache.TryGetValue(result.Id, out var stats))
            {
                stats.ExecutionCount++;
                stats.LastExecuted = DateTime.Now;
            }
            else
            {
                _statsCache[result.Id] = _repository.GetStats(result.Id) ?? new UsageStats { ResultId = result.Id };
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to record history for {Result}", result.Title);
        }
    }

    public UsageStats? GetStats(string resultId)
    {
        if (_statsCache.TryGetValue(resultId, out var cached))
            return cached;

        var stats = _repository.GetStats(resultId);
        if (stats is not null)
            _statsCache[resultId] = stats;
        return stats;
    }

    public IReadOnlyList<SearchResult> GetRecent(int limit)
    {
        try
        {
            var items = _repository.GetRecent(limit).Select(x => x.Result).ToList();
            foreach (var item in items)
                item.ExecuteAsync = () => _runner.ExecuteAsync(item, false);
            return items;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load recent history");
            return Array.Empty<SearchResult>();
        }
    }

    public IReadOnlyList<SearchResult> Search(string query, int limit)
    {
        try
        {
            var results = _repository.Search(query, limit);
            foreach (var item in results)
                item.ExecuteAsync = () => _runner.ExecuteAsync(item, false);
            return results;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to search history");
            return Array.Empty<SearchResult>();
        }
    }

    public void Clear()
    {
        _statsCache.Clear();
        try
        {
            _repository.Clear();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to clear history");
        }
    }
}