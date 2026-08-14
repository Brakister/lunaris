using System.Globalization;
using Lunaris.Core.Interfaces;
using Lunaris.Core.Models;
using Lunaris.Core.Utilities;

namespace Lunaris.Search.CalculatorProvider;

/// <summary>Detects and evaluates math expressions such as "2+2" or "sqrt(144)".</summary>
public sealed class CalculatorProvider : ISearchProvider
{
    private static readonly string[] FunctionKeywords =
    {
        "sqrt", "cbrt", "sin", "cos", "tan", "asin", "acos", "atan",
        "abs", "floor", "ceil", "round", "exp", "ln", "log", "sign", "min", "max", "pow",
    };

    private readonly IActionRunner _runner;

    public string Id => "calculator";

    public string Name => "Calculadora";

    public CalculatorProvider(IActionRunner runner) => _runner = runner;

    public Task<IEnumerable<SearchResult>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        if (!LooksLikeMath(query))
            return Task.FromResult<IEnumerable<SearchResult>>(Array.Empty<SearchResult>());

        var value = FormatResult(query);
        if (value is null)
            return Task.FromResult<IEnumerable<SearchResult>>(Array.Empty<SearchResult>());

        var result = new SearchResult
        {
            Id = "calc:" + query.Trim().ToLowerInvariant(),
            Title = "= " + value,
            Subtitle = "Copiar resultado",
            Icon = GlyphCatalog.Calculator,
            Category = "Calculadora",
            Kind = SearchResultKind.Calculation,
            Score = 100,
            ExecuteHint = value,
        };
        result.ExecuteAsync = () => _runner.ExecuteAsync(result, false);

        return Task.FromResult<IEnumerable<SearchResult>>(new[] { result });
    }

    public static bool LooksLikeMath(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return false;

        var normalized = query.Trim().ToLowerInvariant();
        var hasDigit = normalized.Any(char.IsDigit);
        if (!hasDigit)
            return false;

        var hasOperator = normalized.Any(c => c is '+' or '-' or '*' or '/' or '^' or '%' or '(' or ')');
        var hasFunction = FunctionKeywords.Any(normalized.Contains);
        return hasOperator || hasFunction;
    }

    private static string? FormatResult(string query)
    {
        if (MathExpressionParser.TryEvaluate(query, out var value, out _))
        {
            if (double.IsInfinity(value) || double.IsNaN(value))
                return null;

            return value.ToString("G15", CultureInfo.InvariantCulture);
        }

        return null;
    }
}