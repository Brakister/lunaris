using Lunaris.Core.Services;
using Lunaris.Core.Utilities;

namespace Lunaris.Tests;

public class FuzzyMatcherTests
{
    [Theory]
    [InlineData("chr", "Google Chrome")]
    [InlineData("vs", "Visual Studio Code")]
    [InlineData("calc", "Calculadora")]
    [InlineData("chrome", "Google Chrome")]
    [InlineData("vscode", "Visual Studio Code")]
    public void Example_queries_match(string query, string candidate)
    {
        Assert.True(FuzzyMatcher.Score(query, candidate) > 0.2, $"{query} should match {candidate}");
    }

    [Fact]
    public void Empty_query_scores_zero()
    {
        Assert.Equal(0, FuzzyMatcher.Score("", "Google Chrome"));
        Assert.Equal(0, FuzzyMatcher.Score("   ", "Google Chrome"));
        Assert.Equal(0, FuzzyMatcher.Score("chrome", ""));
        Assert.Equal(0, FuzzyMatcher.Score(null!, "Google Chrome"));
    }

    [Fact]
    public void Very_large_query_is_handled()
    {
        var huge = new string('a', 5000);
        var score = FuzzyMatcher.Score(huge, "aaaa");
        Assert.Equal(0, score);
    }

    [Fact]
    public void Case_insensitive_matching()
    {
        Assert.Equal(FuzzyMatcher.Score("chrome", "Google Chrome"), FuzzyMatcher.Score("CHROME", "google chrome"));
        Assert.True(FuzzyMatcher.Score("ChRoMe", "Google Chrome") > 0.5);
    }

    [Fact]
    public void Accent_insensitive_matching()
    {
        Assert.True(FuzzyMatcher.Score("configuracao", "Configuração") > 0.9);
        Assert.True(FuzzyMatcher.Score("sao paulo", "São Paulo") > 0.9);
    }

    [Theory]
    [InlineData("xyzabc", "Google Chrome")]
    [InlineData("zzz", "abc")]
    public void Non_matching_queries_score_zero(string query, string candidate)
    {
        Assert.Equal(0, FuzzyMatcher.Score(query, candidate));
    }

    [Fact]
    public void Special_characters_do_not_crash()
    {
        Assert.True(FuzzyMatcher.Score("!@#$%^&*()", "!@#$%^&*()") > 0.9);
        Assert.Equal(0, FuzzyMatcher.Score("~!@#", "plain"));
    }

    [Fact]
    public void Multi_word_query_requires_all_tokens()
    {
        Assert.True(FuzzyMatcher.Score("visual code", "Visual Studio Code") > 0.2);
        Assert.Equal(0, FuzzyMatcher.Score("visual word", "Visual Studio Code"));
    }

    [Fact]
    public void Exact_match_ranks_highest()
    {
        var exact = FuzzyMatcher.Score("chrome", "Chrome");
        var prefix = FuzzyMatcher.Score("chrome", "Google Chrome");
        var fuzzy = FuzzyMatcher.Score("chrome", "Mozilla Chromium Browser");
        Assert.True(exact > prefix);
        Assert.True(prefix > fuzzy);
    }

    [Fact]
    public void StringNormalizer_strips_diacritics()
    {
        Assert.Equal("configuracoes", StringNormalizer.Normalize("Configurações"));
        Assert.Equal("cafe", StringNormalizer.Normalize("café"));
    }
}