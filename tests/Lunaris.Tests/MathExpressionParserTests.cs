using Lunaris.Search.CalculatorProvider;

namespace Lunaris.Tests;

public class MathExpressionParserTests
{
    [Theory]
    [InlineData("2+2", 4)]
    [InlineData("10 - 4", 6)]
    [InlineData("3*4", 12)]
    [InlineData("8/2", 4)]
    [InlineData("2^10", 1024)]
    [InlineData("(2+3)*4", 20)]
    [InlineData("-5+10", 5)]
    [InlineData("25%", 0.25)]
    [InlineData("sqrt(144)", 12)]
    [InlineData("sqrt(81)", 9)]
    [InlineData("cbrt(27)", 3)]
    [InlineData("abs(-42)", 42)]
    [InlineData("floor(3.9)", 3)]
    [InlineData("ceil(3.1)", 4)]
    [InlineData("round(2.5)", 2)]
    [InlineData("sign(-8)", -1)]
    [InlineData("sign(5)", 1)]
    [InlineData("min(3,7)", 3)]
    [InlineData("max(3,7)", 7)]
    [InlineData("pow(2,10)", 1024)]
    [InlineData("log(100)", 2)]
    [InlineData("ln(1)", 0)]
    [InlineData("sin(0)", 0)]
    [InlineData("cos(0)", 1)]
    [InlineData("pi", System.Math.PI)]
    [InlineData("e", System.Math.E)]
    public void Evaluates_expression(string expression, double expected)
    {
        var ok = MathExpressionParser.TryEvaluate(expression, out var value, out var error);
        Assert.True(ok, $"'{expression}' failed: {error}");
        Assert.Equal(expected, value, 10);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("2+")]
    [InlineData("*3")]
    [InlineData("abc")]
    [InlineData("1/0")]
    [InlineData("5%0")]
    [InlineData("sqrt(-1)")]
    [InlineData("(2+3")]
    [InlineData("2+3)")]
    [InlineData("1 2")]
    [InlineData("unknown(3)")]
    [InlineData("foo")]
    [InlineData("2**3")]
    [InlineData("..5")]
    public void Rejects_invalid_expression(string expression)
    {
        var ok = MathExpressionParser.TryEvaluate(expression, out var value, out _);
        Assert.False(ok);
        Assert.Equal(0, value);
    }

    [Fact]
    public void Precedence_is_respected()
    {
        Assert.True(MathExpressionParser.TryEvaluate("2+3*4", out var value, out _));
        Assert.Equal(14, value);
    }

    [Fact]
    public void Percent_combines_with_operator()
    {
        Assert.True(MathExpressionParser.TryEvaluate("200+10%", out var value, out _));
        Assert.Equal(200.1, value);
    }
}