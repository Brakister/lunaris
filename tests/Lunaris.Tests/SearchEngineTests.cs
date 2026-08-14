using Lunaris.Core.Interfaces;
using Lunaris.Core.Models;
using Lunaris.Core.Services;

namespace Lunaris.Tests;

public class SearchEngineTests
{
    private sealed class FakeSettings : ISettingsService
    {
        public AppSettings Current { get; } = new();
        public event EventHandler? Changed { add { } remove { } }
        public void Load() { }
        public void Save() { }
        public void Update(Action<AppSettings> change) => change(Current);
    }

    private sealed class FakeHistory : IHistoryService
    {
        public void Record(string query, SearchResult result) { }
        public UsageStats? GetStats(string resultId) => null;
        public IReadOnlyList<SearchResult> GetRecent(int limit) => Array.Empty<SearchResult>();
        public IReadOnlyList<SearchResult> Search(string query, int limit) => Array.Empty<SearchResult>();
        public void Clear() { }
    }

    private sealed class FakeFavorites : IFavoritesService
    {
        private readonly HashSet<string> _ids = new(StringComparer.OrdinalIgnoreCase);
        public void Load() { }
        public IReadOnlyList<SearchResult> GetAll() => Array.Empty<SearchResult>();
        public bool IsFavorite(string resultId) => _ids.Contains(resultId);
        public void Add(SearchResult result) => _ids.Add(result.Id);
        public void Remove(string resultId) => _ids.Remove(resultId);
        public void Toggle(SearchResult result)
        {
            if (!_ids.Add(result.Id))
                _ids.Remove(result.Id);
        }
    }

    private sealed class StubProvider : ISearchProvider
    {
        private readonly Func<string, IEnumerable<SearchResult>> _fn;
        public StubProvider(string name, Func<string, IEnumerable<SearchResult>> fn)
        {
            Name = name;
            _fn = fn;
        }
        public string Id => Name.ToLowerInvariant();
        public string Name { get; }
        public Task<IEnumerable<SearchResult>> SearchAsync(string query, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_fn(query));
        }
    }

    private sealed class ThrowingProvider : ISearchProvider
    {
        public string Id => "boom";
        public string Name => "Exploding";
        public Task<IEnumerable<SearchResult>> SearchAsync(string query, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("provider exploded");
    }

    private static SearchResult Result(string id, string title)
    {
        return new SearchResult { Id = id, Title = title };
    }

    private static SearchEngine Create(params ISearchProvider[] providers)
    {
        return new SearchEngine(providers, new FakeSettings(), new FakeHistory(), new FakeFavorites());
    }

    [Fact]
    public async Task Empty_query_returns_provider_results_without_fuzzy()
    {
        var engine = Create(new StubProvider("apps",
            q => string.IsNullOrWhiteSpace(q) ? Array.Empty<SearchResult>() : new[] { Result("a", "Alpha") }));

        var results = await engine.SearchAsync("", CancellationToken.None);
        Assert.Empty(results);
    }

    [Fact]
    public async Task Matches_by_title()
    {
        var engine = Create(new StubProvider("apps", _ => new[]
        {
            Result("chrome", "Google Chrome"),
            Result("firefox", "Mozilla Firefox"),
        }));

        var results = await engine.SearchAsync("chrome", CancellationToken.None);
        var single = Assert.Single(results);
        Assert.Equal("Google Chrome", single.Title);
    }

    [Fact]
    public async Task Very_large_query_is_tolerated()
    {
        var engine = Create(new StubProvider("apps", _ => new[] { Result("a", "Alpha") }));
        var results = await engine.SearchAsync(new string('x', 2000), CancellationToken.None);
        Assert.Empty(results);
    }

    [Fact]
    public async Task Provider_failure_is_isolated()
    {
        var engine = Create(new ThrowingProvider(), new StubProvider("apps", _ => new[] { Result("a", "Alpha") }));

        var results = await engine.SearchAsync("alpha", CancellationToken.None);
        var single = Assert.Single(results);
        Assert.Equal("Alpha", single.Title);
        Assert.Contains("Exploding", engine.FailedProviders);
    }

    [Fact]
    public async Task Duplicate_results_are_deduplicated()
    {
        var engine = Create(
            new StubProvider("one", _ => new[] { Result("dup", "Same") }),
            new StubProvider("two", _ => new[] { Result("dup", "Same") }));

        var results = await engine.SearchAsync("same", CancellationToken.None);
        Assert.Single(results);
    }

    [Fact]
    public async Task Max_results_is_respected()
    {
        var engine = Create(new StubProvider("apps", _ => Enumerable.Range(0, 30)
            .Select(i => Result($"id{i}", $"Item {i}"))));

        var results = await engine.SearchAsync("item", CancellationToken.None);
        Assert.True(results.Count <= new FakeSettings().Current.MaxResults);
    }

    [Fact]
    public async Task Favorites_are_flagged()
    {
        var favorites = new FakeFavorites();
        favorites.Add(Result("id2", "Item Beta"));

        var engine = new SearchEngine(
            new[] { new StubProvider("apps", _ => new[] { Result("id1", "Item Alpha"), Result("id2", "Item Beta") }) },
            new FakeSettings(), new FakeHistory(), favorites);

        var results = await engine.SearchAsync("beta", CancellationToken.None);
        var hit = Assert.Single(results);
        Assert.Equal("id2", hit.Id);
        Assert.True(hit.IsFavorite);
    }

    [Fact]
    public async Task Cancellation_returns_no_failure()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var engine = Create(new StubProvider("apps", _ => new[] { Result("a", "Alpha") }));

        var results = await engine.SearchAsync("alpha", cts.Token);
        Assert.Empty(results);
        Assert.Empty(engine.FailedProviders);
    }
}