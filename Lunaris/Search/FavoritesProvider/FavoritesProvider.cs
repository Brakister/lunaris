using Lunaris.Core.Interfaces;
using Lunaris.Core.Models;
using Lunaris.Core.Services;

namespace Lunaris.Search.FavoritesProvider;

/// <summary>Returns starred results, always ranked first by the engine.</summary>
public sealed class FavoritesProvider : ISearchProvider
{
    private readonly IFavoritesService _favorites;

    public string Id => "favorites";

    public string Name => "Favoritos";

    public FavoritesProvider(IFavoritesService favorites) => _favorites = favorites;

    public Task<IEnumerable<SearchResult>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var favorites = _favorites.GetAll();

        if (string.IsNullOrWhiteSpace(query))
            return Task.FromResult(favorites.Take(6));

        var results = favorites
            .Where(f => FuzzyMatcher.Score(query, f.Title) > 0.3)
            .Take(20);

        return Task.FromResult(results);
    }
}