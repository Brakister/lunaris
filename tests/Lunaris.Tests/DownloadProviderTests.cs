using Lunaris.Core.Models;
using Lunaris.Search.DownloadProvider;

namespace Lunaris.Tests;

public class DownloadProviderTests
{
    private static IEnumerable<SearchResult> Search(string query)
    {
        var provider = new DownloadProvider(null!, null!);
        return provider.SearchAsync(query, CancellationToken.None).Result;
    }

    [Theory]
    [InlineData("d https://example.com/file.zip")]
    [InlineData("d https://example.com/a/b/document.pdf")]
    [InlineData("dv https://www.youtube.com/watch?v=abc123")]
    [InlineData("d3 https://www.youtube.com/watch?v=abc123")]
    [InlineData("D https://example.com/file.zip")]
    [InlineData("Dv https://youtu.be/xyz")]
    public void Valid_download_queries_produce_a_result(string query)
    {
        var result = Assert.Single(Search(query));
        Assert.Equal(SearchResultKind.Command, result.Kind);
        Assert.False(string.IsNullOrEmpty(result.ExecuteHint));
        Assert.Equal("download", result.ProviderId);
    }

    [Theory]
    [InlineData("d")]
    [InlineData("d ")]
    [InlineData("dv")]
    [InlineData("dv ")]
    [InlineData("d3")]
    [InlineData("d3 ")]
    [InlineData("")]
    [InlineData("hello world")]
    [InlineData("chrome")]
    [InlineData("2+2")]
    public void Invalid_download_queries_return_nothing(string query)
    {
        Assert.Empty(Search(query));
    }

    [Theory]
    [InlineData("d https://example.com/file.zip", "Baixar arquivo")]
    [InlineData("dv https://example.com/video", "Baixar vídeo (MP4)")]
    [InlineData("d3 https://example.com/song", "Baixar áudio (MP3)")]
    public void Command_selects_the_right_action(string query, string expectedTitle)
    {
        var result = Assert.Single(Search(query));
        Assert.Equal(expectedTitle, result.Title);
    }

    [Fact]
    public void Prefix_is_case_insensitive()
    {
        var lower = Assert.Single(Search("d https://example.com/file.zip"));
        var upper = Assert.Single(Search("D https://example.com/file.zip"));
        Assert.Equal(lower.Id, upper.Id);
        Assert.Equal(lower.ExecuteHint, upper.ExecuteHint);
    }

    [Fact]
    public void Url_is_kept_in_execute_hint()
    {
        const string url = "https://example.com/file.zip";
        var result = Assert.Single(Search($"d {url}"));
        Assert.Equal(url, result.ExecuteHint);
    }
}