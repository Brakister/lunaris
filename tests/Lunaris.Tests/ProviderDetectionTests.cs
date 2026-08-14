using Lunaris.Core.Models;
using Lunaris.Search.CalculatorProvider;
using Lunaris.Search.UrlProvider;

namespace Lunaris.Tests;

public class CalculatorProviderTests
{
    [Theory]
    [InlineData("2+2")]
    [InlineData("3 * 4")]
    [InlineData("10/2")]
    [InlineData("2^8")]
    [InlineData("sqrt(144)")]
    [InlineData("(1+2)*3")]
    [InlineData("25%")]
    public void Looks_like_math_when_digits_and_operator_present(string query)
    {
        Assert.True(CalculatorProvider.LooksLikeMath(query));
    }

    [Theory]
    [InlineData("")]
    [InlineData("hello world")]
    [InlineData("chrome")]
    [InlineData("200 km to miles")]
    [InlineData("abc def")]
    public void Does_not_look_like_math(string query)
    {
        Assert.False(CalculatorProvider.LooksLikeMath(query));
    }

    [Fact]
    public void Math_without_digits_is_not_detected()
    {
        Assert.False(CalculatorProvider.LooksLikeMath("sqrt(x)"));
    }
}

public class UrlProviderTests
{
    private static IEnumerable<SearchResult> Search(string query)
    {
        var provider = new UrlProvider(null!);
        return provider.SearchAsync(query, CancellationToken.None).Result;
    }

    [Theory]
    [InlineData("example.com")]
    [InlineData("example.com/path")]
    [InlineData("https://www.google.com")]
    [InlineData("http://example.com/path?q=1")]
    [InlineData("sub.domain.co.uk")]
    [InlineData("localhost")]
    [InlineData("localhost:3000")]
    [InlineData("127.0.0.1")]
    [InlineData("https://localhost:8080/api")]
    public void Valid_urls_are_detected(string query)
    {
        var results = Search(query);
        var single = Assert.Single(results);
        Assert.EndsWith(query, single.Subtitle, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("ftp://example.com")]
    [InlineData("not a url")]
    [InlineData("")]
    [InlineData("hello")]
    [InlineData("C:\\Windows\\notepad.exe")]
    [InlineData("1+1")]
    [InlineData("C:/Program Files/x")]
    public void Invalid_urls_return_nothing(string query)
    {
        Assert.Empty(Search(query));
    }

    [Fact]
    public void Scheme_is_added_when_missing()
    {
        var result = Assert.Single(Search("example.com"));
        Assert.Equal("https://example.com", result.Subtitle);
        Assert.Equal("example.com", result.Title);
    }

    [Fact]
    public void Path_is_stripped_from_title()
    {
        var result = Assert.Single(Search("https://www.google.com/foo/bar"));
        Assert.Equal("www.google.com", result.Title);
        Assert.Equal("https://www.google.com/foo/bar", result.Subtitle);
    }
}