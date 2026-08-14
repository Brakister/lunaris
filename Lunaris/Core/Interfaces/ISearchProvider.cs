using Lunaris.Core.Models;

namespace Lunaris.Core.Interfaces;

public interface ISearchProvider
{
    string Id { get; }

    string Name { get; }

    Task<IEnumerable<SearchResult>> SearchAsync(string query, CancellationToken cancellationToken);
}
