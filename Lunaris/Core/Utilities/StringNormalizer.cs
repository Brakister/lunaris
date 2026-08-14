using System.Globalization;
using System.Text;

namespace Lunaris.Core.Utilities;

/// <summary>Normalizes text for accent-insensitive, case-insensitive matching.</summary>
public static class StringNormalizer
{
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
        var builder = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}