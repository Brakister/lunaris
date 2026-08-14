using Lunaris.Core.Models;

namespace Lunaris.Core.Interfaces;

public interface IHistoryService
{
    /// <summary>Records an executed action. Sensitive kinds are ignored.</summary>
    void Record(string query, SearchResult result);

    /// <summary>Usage statistics for the given result id (null when unknown).</summary>
    UsageStats? GetStats(string resultId);

    /// <summary>Most recently executed items, used as results when the query is empty.</summary>
    IReadOnlyList<SearchResult> GetRecent(int limit);

    /// <summary>Returns history results matching the query.</summary>
    IReadOnlyList<SearchResult> Search(string query, int limit);

    void Clear();
}
