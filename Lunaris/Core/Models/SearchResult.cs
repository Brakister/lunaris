namespace Lunaris.Core.Models;

/// <summary>
/// A single searchable result produced by a search provider.
/// </summary>
public sealed class SearchResult
{
    /// <summary>Stable identifier used for history, favorites and deduplication.</summary>
    public required string Id { get; set; }

    public required string Title { get; set; }

    public string Subtitle { get; set; } = string.Empty;

    /// <summary>Extra terms (executable names, aliases) used for fuzzy matching across providers.</summary>
    public string SearchText { get; set; } = string.Empty;

    /// <summary>Icon glyph rendered with the UI glyph font.</summary>
    public string Icon { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    /// <summary>Internal relevance produced by the fuzzy matcher (0..1).</summary>
    public double Score { get; set; }

    public bool IsFavorite { get; set; }

    public bool CanRunAsAdministrator { get; set; }

    /// <summary>Destructive/system commands require an explicit confirmation step.</summary>
    public bool RequiresConfirmation { get; set; }

    public SearchResultKind Kind { get; set; } = SearchResultKind.App;

    /// <summary>Primary launch target: executable path, file path, URL, ms-settings URI, command name, text payload, ...</summary>
    public string? ExecuteHint { get; set; }

    /// <summary>Optional arguments passed to the launch target.</summary>
    public string? ExecuteArguments { get; set; }

    public string ProviderId { get; set; } = string.Empty;

    /// <summary>Async action invoked when the user executes this result.</summary>
    public Func<Task>? ExecuteAsync { get; set; }
}
