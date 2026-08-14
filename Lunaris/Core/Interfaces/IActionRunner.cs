using Lunaris.Core.Models;

namespace Lunaris.Core.Interfaces;

/// <summary>Central executor for search results (launching apps, files, urls, copying text, ...).</summary>
public interface IActionRunner
{
    Task ExecuteAsync(SearchResult result, bool runAsAdministrator);

    Task CopyToClipboardAsync(string text);
}