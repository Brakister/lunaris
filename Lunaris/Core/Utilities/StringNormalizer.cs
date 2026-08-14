using System.Globalization;
using System.Text;

namespace Lunaris.Core.Utilities;

/// <summary>Normalizes text for accent-insensitive, case-insensitive matching.</summary>
public static class StringNormalizer
{
    private static readonly StringBuilder Pool = new();

    public static string Normalize(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var stripped = RemoveDiacritics(value);
        return stripped.ToLowerInvariant();
    }

    public static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        Pool.Clear();

        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
                Pool.Append(c);
        }

        return Pool.ToString().Normalize(NormalizationForm.FormC);
    }
}