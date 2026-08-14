using Lunaris.Core.Models;

namespace Lunaris.Core.Services;

/// <summary>
/// Deterministic ranking: MatchScore + UsageScore + RecencyScore + FavoriteScore.
/// </summary>
public static class SearchRanker
{
    private const double MatchWeight = 100.0;
    private const double UsageWeight = 0.4;
    private const double RecencyHalfLifeDays = 14.0;
    private const double FavoriteBoost = 18.0;

    public static double Rank(SearchResult result, double matchScore, UsageStats? stats)
    {
        var score = matchScore * MatchWeight;

        if (stats is not null && stats.ExecutionCount > 0)
        {
            score += Math.Min(stats.ExecutionCount, 80) * UsageWeight;
            score += Math.Exp(-(DateTime.UtcNow - stats.LastExecuted).TotalDays / RecencyHalfLifeDays) * 12;
        }

        if (result.IsFavorite)
            score += FavoriteBoost;

        result.Score = score;
        return score;
    }
}