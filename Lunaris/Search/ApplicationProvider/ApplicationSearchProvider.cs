using Lunaris.Core.Interfaces;
using Lunaris.Core.Models;
using Lunaris.Core.Services;
using Lunaris.Core.Utilities;
using Lunaris.Infrastructure.Database;
using Lunaris.Infrastructure.Indexing;
using Lunaris.Infrastructure.Logging;

namespace Lunaris.Search.ApplicationProvider;

/// <summary>Searches the locally indexed Windows applications.</summary>
public sealed class ApplicationSearchProvider : ISearchProvider
{
    private readonly IndexService _index;
    private readonly IActionRunner _runner;
    private readonly IFavoritesService _favorites;
    private readonly ISettingsService _settings;

    private IReadOnlyList<IndexedApplication> _cachedApps = Array.Empty<IndexedApplication>();
    private string[] _normalizedTitles = Array.Empty<string>();
    private string[] _normalizedPaths = Array.Empty<string>();

    public string Id => "applications";

    public string Name => "Aplicativos";

    public ApplicationSearchProvider(IndexService index, IActionRunner runner, IFavoritesService favorites, ISettingsService settings)
    {
        _index = index;
        _runner = runner;
        _favorites = favorites;
        _settings = settings;
    }

    public Task<IEnumerable<SearchResult>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        if (!_settings.Current.EnableApplications)
            return Task.FromResult<IEnumerable<SearchResult>>(Array.Empty<SearchResult>());

        if (string.IsNullOrWhiteSpace(query))
            return Task.FromResult<IEnumerable<SearchResult>>(Array.Empty<SearchResult>());

        EnsureCache();

        var preparedQuery = FuzzyMatcher.Prepare(query);
        var results = new List<SearchResult>();
        var apps = _cachedApps;

        for (var i = 0; i < apps.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromResult<IEnumerable<SearchResult>>(results);

            var app = apps[i];
            var match = FuzzyMatcher.ScoreNormalized(preparedQuery, _normalizedTitles[i]);
            if (match <= 0.12)
                match = FuzzyMatcher.ScoreNormalized(preparedQuery, _normalizedPaths[i]);

            if (match <= 0.12)
                continue;

            var result = new SearchResult
            {
                Id = app.Id,
                Title = app.Name,
                Subtitle = string.IsNullOrEmpty(app.Category) ? app.Path : app.Category,
                Icon = app.Icon,
                Category = "Aplicativo",
                Kind = SearchResultKind.App,
                Score = match,
                ExecuteHint = app.Path,
                ExecuteArguments = app.Arguments,
                CanRunAsAdministrator = true,
                IsFavorite = _favorites.IsFavorite(app.Id),
                ProviderId = Id,
            };
            result.ExecuteAsync = () => _runner.ExecuteAsync(result, false);
            results.Add(result);
        }

        if (results.Count > 60)
        {
            results = results
                .OrderByDescending(r => r.Score)
                .Take(60)
                .ToList();
        }

        return Task.FromResult<IEnumerable<SearchResult>>(results);
    }

    private void EnsureCache()
    {
        var apps = _index.Applications;
        if (ReferenceEquals(apps, _cachedApps))
            return;

        _cachedApps = apps;
        _normalizedTitles = apps.Select(a => StringNormalizer.Normalize(a.Name)).ToArray();
        _normalizedPaths = apps.Select(a => StringNormalizer.Normalize(a.Path)).ToArray();
    }
}