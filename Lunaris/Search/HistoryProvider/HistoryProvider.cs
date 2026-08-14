using Lunaris.Core.Interfaces;
using Lunaris.Core.Models;

namespace Lunaris.Search.HistoryProvider;

/// <summary>Surfaces recent and matching executed actions so history improves future rankings.</summary>
public sealed class HistoryProvider : ISearchProvider
{
    private readonly IHistoryService _history;
    private readonly ISettingsService _settings;

    public string Id => "history";

    public string Name => "Histórico";

    public HistoryProvider(IHistoryService history, ISettingsService settings)
    {
        _history = history;
        _settings = settings;
    }

    public Task<IEnumerable<SearchResult>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        if (!_settings.Current.EnableHistory)
            return Task.FromResult<IEnumerable<SearchResult>>(Array.Empty<SearchResult>());

        IEnumerable<SearchResult> results = string.IsNullOrWhiteSpace(query)
            ? _history.GetRecent(6)
            : _history.Search(query, 20);

        return Task.FromResult(results);
    }
}