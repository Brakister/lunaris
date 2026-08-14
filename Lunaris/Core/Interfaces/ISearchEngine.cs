using Lunaris.Core.Models;

namespace Lunaris.Core.Interfaces;

public interface ISearchEngine
{
    /// <summary>
    /// Runs the query against every provider, merges, deduplicates, ranks and returns the top results.
    /// </summary>
    Task<IReadOnlyList<SearchResult>> SearchAsync(string query, CancellationToken cancellationToken);

    /// <summary>Reports whether the given provider failed during the last search.</summary>
    IReadOnlyList<string> FailedProviders { get; }
}