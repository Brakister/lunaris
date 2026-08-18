using System.Text.RegularExpressions;
using Lunaris.Core.Interfaces;
using Lunaris.Core.Models;
using Lunaris.Core.Utilities;

namespace Lunaris.Search.DownloadProvider;

/// <summary>
/// Download commands: "d &lt;url&gt;" downloads any file, "dv &lt;url&gt;" downloads a video as MP4,
/// "d3 &lt;url&gt;" downloads audio as MP3. Files are saved to the user's Downloads folder.
/// </summary>
public sealed class DownloadProvider : ISearchProvider
{
    private static readonly Regex CommandRegex = new(
        @"^(d3|dv|d)\s+(.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IDownloadService _downloads;
    private readonly INotificationService _notification;

    public string Id => "download";

    public string Name => "Downloads";

    public DownloadProvider(IDownloadService downloads, INotificationService notification)
    {
        _downloads = downloads;
        _notification = notification;
    }

    public Task<IEnumerable<SearchResult>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var trimmed = query.Trim();
        var match = CommandRegex.Match(trimmed);
        if (!match.Success)
            return Task.FromResult<IEnumerable<SearchResult>>(Array.Empty<SearchResult>());

        var command = match.Groups[1].Value.ToLowerInvariant();
        var target = match.Groups[2].Value.Trim();
        if (string.IsNullOrWhiteSpace(target))
            return Task.FromResult<IEnumerable<SearchResult>>(Array.Empty<SearchResult>());

        var result = command switch
        {
            "dv" => BuildResult("Baixar vídeo (MP4)", Truncate(target), GlyphCatalog.Url, target),
            "d3" => BuildResult("Baixar áudio (MP3)", Truncate(target), GlyphCatalog.Url, target),
            _ => BuildResult("Baixar arquivo", Truncate(target), GlyphCatalog.File, target),
        };

        result.ExecuteAsync = () =>
        {
            _notification.Show("Lunaris", $"Baixando... {target}");
            _ = DownloadInBackgroundAsync(command, target);
            return Task.CompletedTask;
        };

        return Task.FromResult<IEnumerable<SearchResult>>(new[] { result });
    }

    private async Task DownloadInBackgroundAsync(string command, string target)
    {
        try
        {
            var outcome = command switch
            {
                "dv" => await _downloads.DownloadVideoAsync(target, CancellationToken.None),
                "d3" => await _downloads.DownloadAudioAsync(target, CancellationToken.None),
                _ => await _downloads.DownloadFileAsync(target, CancellationToken.None),
            };

            _notification.Show("Lunaris", outcome.Success ? outcome.Message : "Erro: " + outcome.Message);
        }
        catch (Exception ex)
        {
            _notification.Show("Lunaris", "Falha no download: " + ex.Message);
        }
    }

    private static SearchResult BuildResult(string title, string subtitle, string icon, string target)
    {
        var result = new SearchResult
        {
            Id = "download:" + target.ToLowerInvariant(),
            Title = title,
            Subtitle = subtitle,
            SearchText = $"d dv d3 baixar download {target}",
            Icon = icon,
            Category = "Download",
            Kind = SearchResultKind.Command,
            Score = 1.0,
            ExecuteHint = target,
            ProviderId = "download",
        };
        return result;
    }

    private static string Truncate(string value) =>
        value.Length > 70 ? value[..70] + "…" : value;
}