using Lunaris.Core.Interfaces;
using Lunaris.Core.Models;
using Lunaris.Infrastructure.Database;
using Lunaris.Infrastructure.Logging;

namespace Lunaris.Core.Services;

public sealed class FavoritesService : IFavoritesService
{
    private readonly FavoriteRepository _repository;
    private readonly IActionRunner _runner;
    private readonly Dictionary<string, SearchResult> _cache = new();
    private readonly object _gate = new();

    public FavoritesService(FavoriteRepository repository, IActionRunner runner)
    {
        _repository = repository;
        _runner = runner;
    }

    public void Load()
    {
        lock (_gate)
        {
            _cache.Clear();
            foreach (var favorite in _repository.GetAll())
            {
                favorite.ExecuteAsync = () => _runner.ExecuteAsync(favorite, false);
                _cache[favorite.Id] = favorite;
            }
        }
    }

    public IReadOnlyList<SearchResult> GetAll()
    {
        lock (_gate)
            return _cache.Values.ToList();
    }

    public bool IsFavorite(string resultId)
    {
        lock (_gate)
            return _cache.ContainsKey(resultId);
    }

    public void Add(SearchResult result)
    {
        lock (_gate)
        {
            if (_cache.ContainsKey(result.Id))
                return;

            try
            {
                _repository.Add(result);
                var copy = new SearchResult
                {
                    Id = result.Id,
                    Title = result.Title,
                    Subtitle = result.Subtitle,
                    Icon = result.Icon,
                    Category = result.Category,
                    Kind = result.Kind,
                    ExecuteHint = result.ExecuteHint,
                    ExecuteArguments = result.ExecuteArguments,
                    CanRunAsAdministrator = result.CanRunAsAdministrator,
                    IsFavorite = true,
                };
                copy.ExecuteAsync = () => _runner.ExecuteAsync(copy, false);
                _cache[result.Id] = copy;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to add favorite {Result}", result.Title);
            }
        }
    }

    public void Remove(string resultId)
    {
        lock (_gate)
        {
            if (_cache.Remove(resultId))
            {
                try
                {
                    _repository.Remove(resultId);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to remove favorite {ResultId}", resultId);
                }
            }
        }
    }

    public void Toggle(SearchResult result)
    {
        if (IsFavorite(result.Id))
            Remove(result.Id);
        else
            Add(result);
    }
}