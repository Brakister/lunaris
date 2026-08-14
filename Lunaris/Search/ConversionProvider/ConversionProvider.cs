using Lunaris.Core.Interfaces;
using Lunaris.Core.Models;
using Lunaris.Core.Services;
using Lunaris.Core.Utilities;

namespace Lunaris.Search.ConversionProvider;

/// <summary>Converts units offline, e.g. "10 km", "10 km to miles", "25 c to f".</summary>
public sealed class ConversionProvider : ISearchProvider
{
    private readonly IActionRunner _runner;

    public string Id => "conversions";

    public string Name => "Conversões";

    public ConversionProvider(IActionRunner runner) => _runner = runner;

    public Task<IEnumerable<SearchResult>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        if (!ConversionService.TryParseQuery(query, out var value, out var fromUnit, out var toUnit))
            return Task.FromResult<IEnumerable<SearchResult>>(Array.Empty<SearchResult>());

        var results = new List<SearchResult>();

        if (toUnit is not null)
        {
            var conversion = ConversionService.Convert(value, fromUnit, toUnit);
            if (conversion is not null)
                results.Add(BuildResult(conversion.ToString()));
        }
        else
        {
            foreach (var conversion in ConversionService.ConvertAll(value, fromUnit))
                results.Add(BuildResult(conversion.ToString()));
        }

        return Task.FromResult<IEnumerable<SearchResult>>(results);
    }

    private SearchResult BuildResult(string text)
    {
        var result = new SearchResult
        {
            Id = "conv:" + text.ToLowerInvariant(),
            Title = text,
            Subtitle = "Copiar resultado",
            Icon = GlyphCatalog.Convert,
            Category = "Conversão",
            Kind = SearchResultKind.Calculation,
            Score = 1,
            ExecuteHint = text,
            ProviderId = Id,
        };
        result.ExecuteAsync = () => _runner.ExecuteAsync(result, false);
        return result;
    }
}