using Lunaris.Core.Services;

namespace Lunaris.Tests;

public class ConversionServiceTests
{
    [Theory]
    [InlineData(1, "km", "m", 1000)]
    [InlineData(1, "m", "km", 0.001)]
    [InlineData(1, "mi", "km", 1.609344)]
    [InlineData(1, "lb", "kg", 0.45359237)]
    [InlineData(1, "g", "mg", 1000)]
    [InlineData(1, "gb", "mb", 1000)]
    [InlineData(1, "mb", "mib", 1000000.0 / 1048576)]
    [InlineData(1, "gal", "l", 3.785411784)]
    [InlineData(100, "km/h", "m/s", 100.0 / 3.6)]
    public void Converts_same_family(double value, string from, string to, double expected)
    {
        var result = ConversionService.Convert(value, from, to);
        Assert.NotNull(result);
        Assert.Equal(expected, Parse(result.ToDisplay), 4);
    }

    [Fact]
    public void Converts_between_different_units()
    {
        Assert.NotNull(ConversionService.Convert(1, "km", "mi"));
        Assert.NotNull(ConversionService.Convert(1, "kg", "lb"));
    }

    [Theory]
    [InlineData(0, "c", "f", 32)]
    [InlineData(32, "f", "c", 0)]
    [InlineData(0, "c", "k", 273.15)]
    [InlineData(100, "c", "f", 212)]
    [InlineData(212, "f", "c", 100)]
    [InlineData(0, "k", "c", -273.15)]
    public void Converts_temperature(double value, string from, string to, double expected)
    {
        var result = ConversionService.Convert(value, from, to);
        Assert.NotNull(result);
        Assert.Equal(expected, Parse(result.ToDisplay), 4);
    }

    [Theory]
    [InlineData("1 km to m")]
    [InlineData("1.5 km to miles")]
    [InlineData("1,5 kg para lb")]
    [InlineData("5 km")]
    [InlineData("10 celsius to fahrenheit")]
    public void Parses_conversion_query(string query)
    {
        Assert.True(ConversionService.TryParseQuery(query, out var value, out var from, out var to));
        Assert.True(value > 0);
        Assert.False(string.IsNullOrWhiteSpace(from));
    }

    [Theory]
    [InlineData("")]
    [InlineData("hello")]
    [InlineData("5 lightyears")]
    [InlineData("1 xyz")]
    [InlineData("km")]
    public void Rejects_invalid_conversion_query(string query)
    {
        Assert.False(ConversionService.TryParseQuery(query, out _, out _, out _));
    }

    [Fact]
    public void Convert_returns_null_for_different_families()
    {
        Assert.Null(ConversionService.Convert(1, "km", "lb"));
        Assert.Null(ConversionService.Convert(1, "c", "kg"));
        Assert.Null(ConversionService.Convert(1, "unknown", "unknown"));
    }

    [Fact]
    public void Convert_all_returns_related_units()
    {
        var results = ConversionService.ConvertAll(1, "km");
        Assert.True(results.Count >= 3);
        Assert.All(results, r => Assert.NotEqual("km", r.ToUnit));
    }

    [Fact]
    public void Convert_all_temperature()
    {
        var results = ConversionService.ConvertAll(100, "c");
        Assert.True(results.Count >= 2);
        Assert.Contains(results, r => r.ToUnit == "celsius");
        Assert.DoesNotContain(results, r => r.ToUnit == "c");
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(1000, "1000")]
    [InlineData(1.5, "1.5")]
    [InlineData(1234.567, "1234.567")]
    public void Formats_numbers(double value, string expected)
    {
        Assert.Equal(expected, ConversionService.FormatNumber(value));
    }

    private static double Parse(string display)
    {
        var parts = display.Split(' ');
        return double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
    }
}