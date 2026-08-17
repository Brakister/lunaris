using Lunaris.Core.Models;
using Lunaris.Search.ToolsProvider;

namespace Lunaris.Tests;

public class ToolsProviderClockTests
{
    private static IEnumerable<SearchResult> Search(string query)
    {
        var provider = new ToolsProvider(null!, null!);
        return provider.SearchAsync(query, CancellationToken.None).Result;
    }

    [Theory]
    [InlineData("hora")]
    [InlineData("time")]
    [InlineData("agora")]
    [InlineData("data")]
    [InlineData("date")]
    [InlineData("hoje")]
    [InlineData("datetime")]
    [InlineData("agora completo")]
    public void Clock_keywords_produce_a_copyable_result(string query)
    {
        var result = Assert.Single(Search(query));
        Assert.Equal(SearchResultKind.TextAction, result.Kind);
        Assert.False(string.IsNullOrEmpty(result.ExecuteHint));
    }

    [Theory]
    [InlineData("horas")]
    [InlineData("datas")]
    [InlineData("hora atual de agora")]
    [InlineData("not a clock")]
    public void Non_matching_keywords_return_nothing(string query)
    {
        Assert.Empty(Search(query));
    }
}
