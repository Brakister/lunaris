using Lunaris.Core.Interfaces;
using Lunaris.Core.Models;

namespace Lunaris.Search.ClipboardProvider;

/// <summary>Searches the clipboard history (only when the user enables it).</summary>
public sealed class ClipboardProvider : ISearchProvider
{
    private readonly IClipboardHistoryService _clipboard;

    public string Id => "clipboard";

    public string Name => "Clipboard";

    public ClipboardProvider(IClipboardHistoryService clipboard) => _clipboard = clipboard;

    public Task<IEnumerable<SearchResult>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        IEnumerable<SearchResult> results = string.IsNullOrWhiteSpace(query)
            ? Array.Empty<SearchResult>()
            : _clipboard.Search(query, 12);

        return Task.FromResult(results);
    }
}