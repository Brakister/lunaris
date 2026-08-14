using Lunaris.Core.Interfaces;
using Lunaris.Core.Models;
using Lunaris.Infrastructure.Logging;

namespace Lunaris.Core.Services;

public sealed class SearchEngine : ISearchEngine
{
    private readonly IEnumerable<ISearchProvider> _providers;
    private readonly ISettingsService _settings;
    private readonly IHistoryService _history;
    private readonly IFavoritesService _favorites;
    private readonly List<string> _failedProviders = new();

    public SearchEngine(
        IEnumerable<ISearchProvider> providers,
        ISettingsService settings,
        IHistoryService history,
        IFavoritesService favorites)
    {
        _providers = providers;
        _settings = settings;
        _history = history;
        _favorites = favorites;
    }

    public IReadOnlyList<string> FailedProviders => _failedProviders;

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        _failedProviders.Clear();
        var results = new List<SearchResult>();
        var tasks = _providers.Select(p => RunProviderAsync(p, query, cancellationToken)).ToArray();

        var providerResults = await Task.WhenAll(tasks);
        foreach (var list in providerResults)
            results.AddRange(list);

        return RankAndMerge(query, results);
    }

    private async Task<IEnumerable<SearchResult>> RunProviderAsync(ISearchProvider provider, string query, CancellationToken cancellationToken)
    {
        try
        {
            return await provider.SearchAsync(query, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Array.Empty<SearchResult>();
        }
        catch (Exception ex)
        {
            _failedProviders.Add(provider.Name);
            Log.Error(ex, "Search provider {Provider} failed", provider.Name);
            return Array.Empty<SearchResult>();
        }
    }

    private IReadOnlyList<SearchResult> RankAndMerge(string query, List<SearchResult> results)
    {
        var dedup = new Dictionary<string, SearchResult>(StringComparer.Ordinal);
        foreach (var result in results)
        {
            if (string.IsNullOrEmpty(result.Id))
                continue;

            var normalizedId = result.Id.ToLowerInvariant();
            if (!dedup.TryGetValue(normalizedId, out var existing) || result.Score > existing.Score)
                dedup[normalizedId] = result;
        }

        var max = _settings.Current.MaxResults;

        if (string.IsNullOrWhiteSpace(query))
        {
            // Empty query: show favorites + recent history, no fuzzy matching.
            var empty = dedup.Values.ToList();
            var ranked = empty.OrderByDescending(r => r.IsFavorite ? 1 : 0)
                .ThenByDescending(r => r.Score)
                .Take(max)
                .ToList();
            return ranked;
        }

        var scored = new List<(SearchResult Result, double Match)>();
        foreach (var result in dedup.Values)
        {
            var match = FuzzyMatcher.Score(query, result.Title);
            if (match <= 0)
            {
                // Fall back to matching the subtitle (e.g. file paths, urls).
                match = FuzzyMatcher.Score(query, result.Subtitle);
            }

            if (match <= 0.12)
                continue;

            result.IsFavorite = _favorites.IsFavorite(result.Id);
            var stats = _history.GetStats(result.Id);
            SearchRanker.Rank(result, match, stats);
            scored.Add((result, match));
        }

        return scored
            .OrderByDescending(x => x.Result.Score)
            .Take(max)
            .Select(x => x.Result)
            .ToList();
    }
}
