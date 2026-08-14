using System.IO;
using Lunaris.Infrastructure.Database;
using Lunaris.Infrastructure.Logging;

namespace Lunaris.Infrastructure.Indexing;

/// <summary>Walks the configured user directories and persists their contents into SQLite.</summary>
public sealed class FileIndexer
{
    private readonly IndexedFileRepository _repository;

    public FileIndexer(IndexedFileRepository repository)
    {
        _repository = repository;
    }

    public async Task ReindexAsync(IReadOnlyList<string> roots, CancellationToken cancellationToken)
    {
        if (roots.Count == 0)
            return;

        var batch = new List<(string Path, string Name, string Directory, long Size, DateTime Modified, bool IsFolder)>(500);

        foreach (var root in roots)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            if (!Directory.Exists(root))
                continue;

            try
            {
                await WalkDirectoryAsync(root, batch, cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Indexing failed for {Root}", root);
            }
        }

        Flush(batch);

        try
        {
            _repository.PruneOutside(roots);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Index pruning failed");
        }

        Log.Info("File index updated ({Roots} roots)", roots.Count);
    }

    private async Task WalkDirectoryAsync(string directory, List<(string, string, string, long, DateTime, bool)> batch, CancellationToken cancellationToken)
    {
        var entries = await Task.Run(() =>
        {
            var list = new List<FileSystemEntry>();

            foreach (var dir in EnumerateDirsSafe(directory, cancellationToken))
            {
                list.Add(new FileSystemEntry(dir, true));
                foreach (var file in EnumerateFilesSafe(dir, cancellationToken))
                    list.Add(new FileSystemEntry(file, false));
            }

            return list;
        }, cancellationToken);

        foreach (var entry in entries)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            try
            {
                FileSystemInfo info = entry.IsFolder
                    ? new DirectoryInfo(entry.Path)
                    : new FileInfo(entry.Path);

                batch.Add((entry.Path,
                    info.Name,
                    Path.GetDirectoryName(entry.Path) ?? string.Empty,
                    entry.IsFolder ? 0 : new FileInfo(entry.Path).Length,
                    info.LastWriteTime,
                    entry.IsFolder));

                if (batch.Count >= 500)
                    Flush(batch);
            }
            catch
            {
                // entry vanished mid-scan
            }
        }
    }

    private static List<string> EnumerateDirsSafe(string dir, CancellationToken cancellationToken)
    {
        var list = new List<string>();
        try
        {
            foreach (var d in Directory.EnumerateDirectories(dir, "*", new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = true,
                AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.System | FileAttributes.Hidden,
            }))
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
                list.Add(d);
            }
        }
        catch
        {
            // ignore protected paths
        }
        return list;
    }

    private static List<string> EnumerateFilesSafe(string dir, CancellationToken cancellationToken)
    {
        var list = new List<string>();
        try
        {
            foreach (var f in Directory.EnumerateFiles(dir, "*", new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = false,
                AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.System | FileAttributes.Hidden,
            }))
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
                list.Add(f);
            }
        }
        catch
        {
            // ignore protected paths
        }
        return list;
    }

    private void Flush(List<(string, string, string, long, DateTime, bool)> batch)
    {
        if (batch.Count == 0)
            return;

        try
        {
            _repository.UpsertBatch(batch);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to persist file index batch");
        }

        batch.Clear();
    }

    private readonly record struct FileSystemEntry(string Path, bool IsFolder);
}