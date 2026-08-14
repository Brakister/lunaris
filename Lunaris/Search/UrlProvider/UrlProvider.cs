using System.Text.RegularExpressions;
using Lunaris.Core.Interfaces;
using Lunaris.Core.Models;
using Lunaris.Core.Utilities;

namespace Lunaris.Search.UrlProvider;

/// <summary>Detects bare domains and full URLs and opens them in the default browser.</summary>
public sealed class UrlProvider : ISearchProvider
{
    private static readonly Regex DomainRegex = new(
        @"^(?:(?:https?:\/\/)?(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z]{2,})(?::\d+)?(?:[/?#][^\s]*)?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex LocalhostRegex = new(
        @"^(?:https?:\/\/)?(?:localhost|127\.0\.0\.1|\[::1\])(?::\d+)?(?:[/?#][^\s]*)?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IActionRunner _runner;

    public string Id => "urls";

    public string Name => "URLs";

    public UrlProvider(IActionRunner runner) => _runner = runner;

    public Task<IEnumerable<SearchResult>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var trimmed = query.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Contains(' '))
            return Task.FromResult<IEnumerable<SearchResult>>(Array.Empty<SearchResult>());

        if (trimmed.Contains('\\') || System.IO.Path.IsPathRooted(trimmed))
            return Task.FromResult<IEnumerable<SearchResult>>(Array.Empty<SearchResult>());

        if (!DomainRegex.IsMatch(trimmed) && !LocalhostRegex.IsMatch(trimmed))
            return Task.FromResult<IEnumerable<SearchResult>>(Array.Empty<SearchResult>());

        var url = trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : "https://" + trimmed;

        var host = GetHost(trimmed);

        var result = new SearchResult
        {
            Id = "url:" + url.ToLowerInvariant(),
            Title = host,
            Subtitle = url,
            Icon = GlyphCatalog.Url,
            Category = "URL",
            Kind = SearchResultKind.Url,
            Score = 1,
            ExecuteHint = url,
            ProviderId = Id,
        };
        result.ExecuteAsync = () => _runner.ExecuteAsync(result, false);

        return Task.FromResult<IEnumerable<SearchResult>>(new[] { result });
    }

    private static string GetHost(string value)
    {
        var withoutScheme = Regex.Replace(value, @"^https?://", string.Empty, RegexOptions.IgnoreCase);
        var slash = withoutScheme.IndexOf('/');
        return slash > 0 ? withoutScheme[..slash] : withoutScheme;
    }
}