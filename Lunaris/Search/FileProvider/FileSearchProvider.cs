using System.IO;
using Lunaris.Core.Interfaces;
using Lunaris.Core.Models;
using Lunaris.Core.Utilities;
using Lunaris.Infrastructure.Database;
using Lunaris.Infrastructure.Logging;

namespace Lunaris.Search.FileProvider;

/// <summary>
/// Searches locally indexed files and folders. Never scans the disk on each keystroke —
/// queries run against the SQLite index built by the background indexer.
/// </summary>
public sealed class FileSearchProvider : ISearchProvider
{
    private static readonly HashSet<string> KnownExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "pdf", "xlsx", "xls", "docx", "doc", "pptx", "ppt", "txt", "csv", "md",
        "jpg", "jpeg", "png", "gif", "bmp", "svg", "webp", "ico",
        "mp4", "mkv", "avi", "mov", "wmv", "mp3", "wav", "flac",
        "zip", "rar", "7z", "tar", "gz",
        "exe", "msi", "dll", "json", "xml", "html", "css", "js", "ts",
    };

    private readonly IndexedFileRepository _repository;
    private readonly IActionRunner _runner;
    private readonly ISettingsService _settings;

    public string Id => "files";

    public string Name => "Arquivos";

    public FileSearchProvider(IndexedFileRepository repository, IActionRunner runner, ISettingsService settings)
    {
        _repository = repository;
        _runner = runner;
        _settings = settings;
    }

    public Task<IEnumerable<SearchResult>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        if (!_settings.Current.EnableFiles)
            return Task.FromResult<IEnumerable<SearchResult>>(Array.Empty<SearchResult>());

        if (string.IsNullOrWhiteSpace(query))
            return Task.FromResult<IEnumerable<SearchResult>>(DefaultFolders());

        try
        {
            var (nameTokens, extension) = ParseQuery(query);
            // A single-character name token would trigger a full-table LIKE scan with
            // almost no signal; skip it (extension-only queries like ".pdf" are allowed).
            if (nameTokens.Sum(t => t.Length) < 2 && extension is null)
                return Task.FromResult<IEnumerable<SearchResult>>(Array.Empty<SearchResult>());

            var namePattern = string.Join("%", nameTokens);
            var results = _repository.Search(namePattern, extension, null, 50);
            return Task.FromResult<IEnumerable<SearchResult>>(AttachActions(results));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "File search failed for {Query}", query);
            return Task.FromResult<IEnumerable<SearchResult>>(Array.Empty<SearchResult>());
        }
    }

    private IEnumerable<SearchResult> DefaultFolders()
    {
        var folders = PathHelper.DefaultSearchDirectories()
            .Concat(_settings.Current.AdditionalSearchDirectories)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5);

        var results = folders.Select(dir =>
        {
            var name = new DirectoryInfo(dir).Name;
            var result = new SearchResult
            {
                Id = "folder:" + dir.ToLowerInvariant(),
                Title = name,
                Subtitle = dir,
                Icon = GlyphCatalog.FolderOpen,
                Category = "Pasta",
                Kind = SearchResultKind.Folder,
                Score = 5,
                ExecuteHint = dir,
                ProviderId = Id,
            };
            result.ExecuteAsync = () => _runner.ExecuteAsync(result, false);
            return result;
        }).ToList();

        return results;
    }

    private IEnumerable<SearchResult> AttachActions(IEnumerable<SearchResult> results)
    {
        foreach (var result in results)
        {
            result.ProviderId = Id;
            result.ExecuteAsync = () => _runner.ExecuteAsync(result, false);
        }

        return results;
    }

    private static (List<string> NameTokens, string? Extension) ParseQuery(string query)
    {
        var nameTokens = new List<string>();
        string? extension = null;

        var rawTokens = query.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in rawTokens)
        {
            if (token.StartsWith('.') && token.Length > 1)
            {
                extension = token[1..];
                continue;
            }

            if (KnownExtensions.Contains(token) && rawTokens.Length > 1)
            {
                extension = token;
                continue;
            }

            nameTokens.Add(token);
        }

        return (nameTokens, extension);
    }
}