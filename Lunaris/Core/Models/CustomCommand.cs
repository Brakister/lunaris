namespace Lunaris.Core.Models;

/// <summary>
/// A user-defined custom command (name, alias, executable, arguments) persisted in SQLite.
/// </summary>
public sealed class CustomCommand
{
    public long Id { get; set; }

    public required string Name { get; set; }

    public required string Alias { get; set; }

    public required string Executable { get; set; }

    public string Arguments { get; set; } = string.Empty;

    public bool RunAsAdministrator { get; set; }
}
