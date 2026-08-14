using System.Text;
using Lunaris.Core.Utilities;

namespace Lunaris.Core.Services;

/// <summary>
/// Accent-insensitive, case-insensitive fuzzy matching with deterministic ranking.
/// Priority: exact match &gt; prefix &gt; word start &gt; fuzzy subsequence.
/// </summary>
public static class FuzzyMatcher
{
    private static readonly char[] Separators = { ' ', '-', '_', '.', '\\', '/', '(', ')', '[', ']' };

    /// <summary>Pre-normalizes a query once so it is not re-normalized for every candidate.</summary>
    public static string Prepare(string? query) => StringNormalizer.Normalize(query);

    /// <summary>Returns a score in [0, 1]; 0 means no match.</summary>
    public static double Score(string query, string candidate) => ScorePrepared(Prepare(query), candidate);

    /// <summary>Like <see cref="Score"/>, but with the query already normalized via <see cref="Prepare"/>.</summary>
    public static double ScorePrepared(string normalizedQuery, string candidate)
    {
        if (string.IsNullOrEmpty(normalizedQuery) || string.IsNullOrEmpty(candidate))
            return 0;

        var c = StringNormalizer.Normalize(candidate);
        return c.Length == 0 ? 0 : ScoreCore(normalizedQuery, c);
    }

    /// <summary>Like <see cref="ScorePrepared"/>, but the candidate is also already normalized.</summary>
    public static double ScoreNormalized(string normalizedQuery, string normalizedCandidate)
    {
        if (string.IsNullOrEmpty(normalizedQuery) || string.IsNullOrEmpty(normalizedCandidate))
            return 0;

        return ScoreCore(normalizedQuery, normalizedCandidate);
    }

    private static double ScoreCore(string q, string c)
    {
        if (c == q)
            return 1.0;

        // Multi-token query: average of per-token best scores, weighted by coverage.
        var tokens = q.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length > 1)
        {
            var total = 0.0;
            foreach (var token in tokens)
            {
                var tokenScore = ScoreSingle(token, c);
                if (tokenScore <= 0)
                    return 0;
                total += tokenScore;
            }
            var coverage = (double)q.Length / Math.Max(q.Length, c.Length);
            return (total / tokens.Length) * (0.75 + 0.25 * coverage);
        }

        return ScoreSingle(q, c);
    }

    private static double ScoreSingle(string q, string c)
    {
        var containsIdx = c.IndexOf(q, StringComparison.Ordinal);
        if (containsIdx >= 0)
        {
            // Prefer matches at the start of the name or at word boundaries.
            var startsAtBoundary = containsIdx == 0 || IsBoundary(c[containsIdx - 1]);
            var baseScore = startsAtBoundary ? 0.97 : 0.85;
            return Math.Max(0, baseScore - containsIdx * 0.004);
        }

        // Word-start match: query prefix of any word in the candidate.
        var words = c.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        foreach (var word in words)
        {
            if (word.StartsWith(q, StringComparison.Ordinal))
                return 0.9 - Math.Max(0, word.Length - q.Length) * 0.002;
        }

        // Full-word match (acronym style, e.g. "vs" -> "Visual Studio Code").
        var initials = new System.Text.StringBuilder();
        foreach (var word in words)
            if (word.Length > 0)
                initials.Append(word[0]);
        if (initials.ToString().StartsWith(q, StringComparison.Ordinal))
            return 0.92 - Math.Max(0, initials.Length - q.Length) * 0.002;

        return FuzzySubsequence(q, c);
    }

    private static double FuzzySubsequence(string q, string c)
    {
        int qi = 0, ci = 0, matched = 0, lastIdx = -2;
        double score = 0;

        while (qi < q.Length && ci < c.Length)
        {
            if (c[ci] == q[qi])
            {
                var gap = ci - lastIdx - 1;
                var boundaryBonus = ci == 0 || IsBoundary(c[ci - 1]) ? 0.12 : 0.0;
                score += 1.0 - Math.Min(gap, 3) * 0.06 + boundaryBonus;
                lastIdx = ci;
                matched++;
                qi++;
            }
            ci++;
        }

        if (matched < q.Length)
            return 0;

        // Normalize by query length, then penalize long candidates.
        var perChar = score / q.Length;
        var density = (double)q.Length / Math.Max(1, c.Length);
        return perChar * (0.35 + 0.65 * density);
    }

    private static bool IsBoundary(char c) =>
        char.IsWhiteSpace(c) || c == '-' || c == '_' || c == '.' || c == '(' || c == '[' || c == '/' || c == '\\';
}