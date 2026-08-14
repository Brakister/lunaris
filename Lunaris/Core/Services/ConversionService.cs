using System.Globalization;
using Lunaris.Core.Utilities;

namespace Lunaris.Core.Services;

public sealed class ConversionResult
{
    public required string FromUnit { get; init; }

    public required string ToUnit { get; init; }

    public required string FromDisplay { get; init; }

    public required string ToDisplay { get; init; }

    public override string ToString() => $"{FromDisplay} = {ToDisplay}";
}

/// <summary>
/// Offline unit conversion engine (length, mass, temperature, volume, data, speed).
/// Currency is intentionally left out; it will be a separate online provider later.
/// </summary>
public static class ConversionService
{
    private static readonly Dictionary<string, double> Length = new(StringComparer.OrdinalIgnoreCase)
    {
        ["km"] = 1000, ["m"] = 1, ["cm"] = 0.01, ["mm"] = 0.001,
        ["mi"] = 1609.344, ["mile"] = 1609.344, ["miles"] = 1609.344,
        ["ft"] = 0.3048, ["feet"] = 0.3048, ["in"] = 0.0254, ["inch"] = 0.0254, ["inches"] = 0.0254,
        ["yd"] = 0.9144, ["yard"] = 0.9144, ["nm"] = 1852, ["nmi"] = 1852,
    };

    private static readonly Dictionary<string, double> Mass = new(StringComparer.OrdinalIgnoreCase)
    {
        ["kg"] = 1, ["kilogram"] = 1, ["g"] = 0.001, ["gram"] = 0.001, ["mg"] = 1e-6,
        ["t"] = 1000, ["ton"] = 1000, ["lb"] = 0.45359237, ["lbs"] = 0.45359237, ["pound"] = 0.45359237,
        ["oz"] = 0.028349523125, ["ounce"] = 0.028349523125, ["st"] = 6.35029318, ["stone"] = 6.35029318,
    };

    private static readonly Dictionary<string, double> Volume = new(StringComparer.OrdinalIgnoreCase)
    {
        ["l"] = 1, ["liter"] = 1, ["litre"] = 1, ["ml"] = 0.001, ["milliliter"] = 0.001,
        ["m3"] = 1000, ["gal"] = 3.785411784, ["gallon"] = 3.785411784,
        ["qt"] = 0.946352946, ["pint"] = 0.473176473, ["pt"] = 0.473176473,
        ["cup"] = 0.24, ["tbsp"] = 0.0147867648, ["tsp"] = 0.00492892159,
        ["floz"] = 0.0295735296, ["fl oz"] = 0.0295735296,
    };

    private static readonly Dictionary<string, double> Data = new(StringComparer.OrdinalIgnoreCase)
    {
        ["b"] = 1, ["byte"] = 1, ["kb"] = 1000, ["mb"] = 1e6, ["gb"] = 1e9, ["tb"] = 1e12,
        ["kib"] = 1024, ["mib"] = 1024.0 * 1024, ["gib"] = 1024.0 * 1024 * 1024,
    };

    private static readonly Dictionary<string, double> Speed = new(StringComparer.OrdinalIgnoreCase)
    {
        ["m/s"] = 1, ["ms"] = 1, ["km/h"] = 1.0 / 3.6, ["kph"] = 1.0 / 3.6, ["kmh"] = 1.0 / 3.6,
        ["mph"] = 0.44704, ["kn"] = 0.514444, ["knot"] = 0.514444,
    };

    private static readonly (string Family, Dictionary<string, double> Units)[] Families =
    {
        ("length", Length),
        ("mass", Mass),
        ("volume", Volume),
        ("data", Data),
        ("speed", Speed),
    };

    private static readonly Dictionary<string, string> Temperature = new(StringComparer.OrdinalIgnoreCase)
    {
        ["c"] = "celsius", ["celsius"] = "celsius", ["°c"] = "celsius",
        ["f"] = "fahrenheit", ["fahrenheit"] = "fahrenheit", ["°f"] = "fahrenheit",
        ["k"] = "kelvin", ["kelvin"] = "kelvin",
    };

    public static bool TryParseQuery(string query, out double value, out string fromUnit, out string? toUnit)
    {
        value = 0;
        fromUnit = string.Empty;
        toUnit = null;

        var text = query.Trim().ToLowerInvariant();
        var match = System.Text.RegularExpressions.Regex.Match(text,
            @"^(\d+(?:[.,]\d+)?)\s*([a-z°\/]+)\s*(?:(?:to|em|para|em\s+)\s*([a-z°\/]+))?$");

        if (!match.Success)
            return false;

        var number = match.Groups[1].Value.Replace(',', '.');
        if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            return false;

        fromUnit = StringNormalizer.Normalize(match.Groups[2].Value);
        var toRaw = match.Groups[3].Value;
        if (!string.IsNullOrEmpty(toRaw))
            toUnit = StringNormalizer.Normalize(toRaw);

        return IsKnownUnit(fromUnit) && (toUnit is null || IsKnownUnit(toUnit));
    }

    public static IReadOnlyList<ConversionResult> ConvertAll(double value, string fromUnit)
    {
        var from = NormalizeUnit(fromUnit);
        var results = new List<ConversionResult>();

        if (Temperature.TryGetValue(from, out _))
        {
            foreach (var (name, target) in Temperature.Where(kv => kv.Key != from).DistinctBy(kv => kv.Value))
            {
                var converted = ConvertTemperature(value, Temperature[from], target);
                results.Add(MakeResult(value, from, name, converted));
            }
            return results;
        }

        foreach (var (family, units) in Families)
        {
            if (!units.TryGetValue(from, out var factor))
                continue;

            var baseValue = value * factor;
            foreach (var (name, targetFactor) in units.OrderBy(kv => Math.Abs(kv.Value - factor)))
            {
                if (string.Equals(name, from, StringComparison.OrdinalIgnoreCase))
                    continue;

                results.Add(MakeResult(value, from, name, baseValue / targetFactor));
                if (results.Count >= 4)
                    break;
            }
            break;
        }

        return results;
    }

    public static ConversionResult? Convert(double value, string fromUnit, string toUnit)
    {
        var from = NormalizeUnit(fromUnit);
        var to = NormalizeUnit(toUnit);

        if (Temperature.TryGetValue(from, out var fromKind) && Temperature.TryGetValue(to, out var toKind))
        {
            var converted = ConvertTemperature(value, fromKind, toKind);
            return MakeResult(value, from, to, converted);
        }

        foreach (var (family, units) in Families)
        {
            if (!units.TryGetValue(from, out var fromFactor))
                continue;
            if (!units.TryGetValue(to, out var toFactor))
                continue;

            return MakeResult(value, from, to, value * fromFactor / toFactor);
        }

        return null;
    }

    private static ConversionResult MakeResult(double value, string from, string to, double converted)
    {
        var fromDisplay = $"{FormatNumber(value)} {from.ToUpperInvariant()}";
        var toDisplay = $"{FormatNumber(converted)} {to.ToUpperInvariant()}";

        // Prettier common symbols
        var toSymbol = to == "celsius" ? "°C" : to == "fahrenheit" ? "°F" : to == "kelvin" ? "K" : to;
        toDisplay = $"{FormatNumber(converted)} {toSymbol}";

        return new ConversionResult
        {
            FromUnit = from,
            ToUnit = to,
            FromDisplay = fromDisplay,
            ToDisplay = toDisplay,
        };
    }

    public static string FormatNumber(double value)
    {
        if (Math.Abs(value) < 1e-12)
            return "0";
        if (Math.Abs(value) >= 1e9 || (Math.Abs(value) < 1e-4 && value != 0))
            return value.ToString("E2", CultureInfo.InvariantCulture);

        var rounded = Math.Round(value, 6);
        return rounded.ToString("0.######", CultureInfo.InvariantCulture);
    }

    private static double ConvertTemperature(double value, string fromKind, string toKind)
    {
        // normalize to celsius first
        var celsius = fromKind switch
        {
            "celsius" => value,
            "fahrenheit" => (value - 32) * 5 / 9,
            "kelvin" => value - 273.15,
            _ => value,
        };

        return toKind switch
        {
            "celsius" => celsius,
            "fahrenheit" => celsius * 9 / 5 + 32,
            "kelvin" => celsius + 273.15,
            _ => celsius,
        };
    }

    private static bool IsKnownUnit(string unit)
    {
        var normalized = NormalizeUnit(unit);
        return Temperature.ContainsKey(normalized)
            || Families.Any(f => f.Units.ContainsKey(normalized));
    }

    private static string NormalizeUnit(string unit) => unit.Replace("°", string.Empty).Trim();

    private static IEnumerable<T> DistinctBy<T, TKey>(this IEnumerable<T> source, Func<T, TKey> keySelector)
    {
        var seen = new HashSet<TKey>();
        foreach (var item in source)
            if (seen.Add(keySelector(item)))
                yield return item;
    }
}