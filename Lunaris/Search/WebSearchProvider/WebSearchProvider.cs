using System.Text.RegularExpressions;
using Lunaris.Core.Interfaces;
using Lunaris.Core.Models;
using Lunaris.Core.Utilities;

namespace Lunaris.Search.WebSearchProvider;

/// <summary>
/// Bang-style web search: "g dotnet", "yt lofi", "wiki brasil", "so async" etc.
/// Opens the search in the default browser. Prefixes are configurable constants.
/// </summary>
public sealed class WebSearchProvider : ISearchProvider
{
    private static readonly Dictionary<string, (string Label, string UrlTemplate)> Engines =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["g"] = ("Google", "https://www.google.com/search?q={0}"),
            ["google"] = ("Google", "https://www.google.com/search?q={0}"),
            ["yt"] = ("YouTube", "https://www.youtube.com/results?search_query={0}"),
            ["youtube"] = ("YouTube", "https://www.youtube.com/results?search_query={0}"),
            ["wiki"] = ("Wikipédia", "https://pt.wikipedia.org/w/index.php?search={0}"),
            ["wikipedia"] = ("Wikipédia", "https://pt.wikipedia.org/w/index.php?search={0}"),
            ["so"] = ("Stack Overflow", "https://stackoverflow.com/search?q={0}"),
            ["stackoverflow"] = ("Stack Overflow", "https://stackoverflow.com/search?q={0}"),
            ["gh"] = ("GitHub", "https://github.com/search?q={0}&type=repositories"),
            ["github"] = ("GitHub", "https://github.com/search?q={0}&type=repositories"),
            ["ddg"] = ("DuckDuckGo", "https://duckduckgo.com/?q={0}"),
            ["bing"] = ("Bing", "https://www.bing.com/search?q={0}"),
            ["maps"] = ("Google Maps", "https://www.google.com/maps/search/{0}"),
            ["map"] = ("Google Maps", "https://www.google.com/maps/search/{0}"),
        };

    private static readonly Regex PrefixRegex = new(
        @"^(g|google|yt|youtube|wiki|wikipedia|so|stackoverflow|gh|github|ddg|bing|maps|map)\s+(.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IActionRunner _runner;

    public string Id => "web";

    public string Name => "Web";

    public WebSearchProvider(IActionRunner runner) => _runner = runner;

    public Task<IEnumerable<SearchResult>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var trimmed = query.Trim();
        var match = PrefixRegex.Match(trimmed);
        if (!match.Success)
            return Task.FromResult<IEnumerable<SearchResult>>(Array.Empty<SearchResult>());

        var prefix = match.Groups[1].Value.ToLowerInvariant();
        var term = match.Groups[2].Value.Trim();
        if (term.Length == 0)
            return Task.FromResult<IEnumerable<SearchResult>>(Array.Empty<SearchResult>());

        var engine = Engines[prefix];
        var url = string.Format(engine.UrlTemplate, Uri.EscapeDataString(term));

        var result = new SearchResult
        {
            Id = "web:" + prefix + ":" + term.ToLowerInvariant(),
            Title = $"Pesquisar no {engine.Label}: {term}",
            Subtitle = "Abrir no navegador",
            SearchText = $"{prefix} {term} {engine.Label} {term}",
            Icon = GlyphCatalog.Url,
            Category = "Web",
            Kind = SearchResultKind.Url,
            Score = 1.0,
            ExecuteHint = url,
            ProviderId = Id,
        };
        result.ExecuteAsync = () => _runner.ExecuteAsync(result, false);

        return Task.FromResult<IEnumerable<SearchResult>>(new[] { result });
    }
}