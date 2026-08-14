namespace Lunaris.Core.Interfaces;

/// <summary>Information about a newer release available for download.</summary>
public sealed record UpdateInfo
{
    public required string Version { get; init; }

    public required string DownloadUrl { get; init; }

    public string? Notes { get; init; }
}

/// <summary>Checks for and installs updates from the project's GitHub releases.</summary>
public interface IUpdateService
{
    /// <summary>Returns the newest release if it is newer than the running version, otherwise null.</summary>
    Task<UpdateInfo?> CheckAsync(CancellationToken cancellationToken);

    /// <summary>Downloads an installer to the destination directory and returns its full path.</summary>
    Task<string> DownloadAsync(string url, string destinationDirectory, CancellationToken cancellationToken);

    /// <summary>Starts the installer process so the user can complete the update.</summary>
    bool LaunchInstaller(string installerPath);
}
