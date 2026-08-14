using Lunaris.Core.Models;

namespace Lunaris.Core.Interfaces;

public interface IFavoritesService
{
    void Load();

    IReadOnlyList<SearchResult> GetAll();

    bool IsFavorite(string resultId);

    void Add(SearchResult result);

    void Remove(string resultId);

    void Toggle(SearchResult result);
}