using Lunaris.Core.Models;
using Lunaris.Search.WebSearchProvider;

namespace Lunaris.Tests;

public class WebSearchProviderTests
{
    private static IEnumerable<SearchResult> Search(string query)
    {
        var provider = new WebSearchProvider(null!);
        return provider.SearchAsync(query, CancellationToken.None).Result;
    }

    [Theory]
    [InlineData("g dotnet")]
    [InlineData("google aspnet")]
    [InlineData("yt lofi")]
    [InlineData("youtube remix")]
    [InlineData("wiki brasil")]
    [InlineData("wikipedia docker")]
    [InlineData("so async await")]
    [InlineData("stackoverflow linq")]
    [InlineData("gh lunaris")]
    [InlineData("github repogpt")]
    [InlineData("ddg planetas")]
    [InlineData("bing clima")]
    [InlineData("maps sao paulo")]
    [InlineData("map rio de janeiro")]
    public void Valid_bang_queries_produce_a_result(string query)
    {
        var result = Assert.Single(Search(query));
        Assert.Equal(SearchResultKind.Url, result.Kind);
        Assert.False(string.IsNullOrEmpty(result.ExecuteHint));
    }

    [Theory]
    [InlineData("g")]
    [InlineData("g ")]
    [InlineData("yt")]
    [InlineData("")]
    [InlineData("hello world")]
    [InlineData("chrome")]
    [InlineData("2+2")]
    public void Invalid_bang_queries_return_nothing(string query)
    {
        Assert.Empty(Search(query));
    }

    [Fact]
    public void Google_search_encodes_query_and_prefix()
    {
        var result = Assert.Single(Search("g hello world"));
        Assert.Equal("https://www.google.com/search?q=hello%20world", result.ExecuteHint);
        Assert.Contains("Google", result.Title);
    }

    [Fact]
    public void Prefix_is_case_insensitive()
    {
        var lower = Assert.Single(Search("g dotnet"));
        var upper = Assert.Single(Search("G dotnet"));
        Assert.Equal(lower.ExecuteHint, upper.ExecuteHint);
    }
}
