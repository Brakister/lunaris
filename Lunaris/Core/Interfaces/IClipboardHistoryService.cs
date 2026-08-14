using Lunaris.Core.Models;

namespace Lunaris.Core.Interfaces;

/// <summary>Optional clipboard history. Only active when the user enables it in settings.</summary>
public interface IClipboardHistoryService
{
    void Start();

    void Stop();

    IReadOnlyList<SearchResult> Search(string query, int limit);

    IReadOnlyList<string> GetRecent(int limit);

    void Clear();
}