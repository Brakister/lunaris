namespace Lunaris.Core.Models;

/// <summary>Usage statistics derived from the search history for ranking purposes.</summary>
public sealed class UsageStats
{
    public string ResultId { get; set; } = string.Empty;

    public int ExecutionCount { get; set; }

    public DateTime LastExecuted { get; set; } = DateTime.MinValue;
}
