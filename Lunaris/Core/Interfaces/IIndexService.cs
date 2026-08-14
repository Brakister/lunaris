namespace Lunaris.Core.Interfaces;

/// <summary>Builds the local index of applications and files in the background.</summary>
public interface IIndexService
{
    bool IsReady { get; }

    bool IsPaused { get; set; }

    Task StartAsync(CancellationToken cancellationToken);

    Task ReindexAsync(CancellationToken cancellationToken);
}