using System.IO;
using System.Threading;
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
        await Task.Run(() =>
        {
            Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;

            foreach (var entry in EnumerateEntriesSafe(directory, cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                AddEntry(batch, entry);
            }
        }, cancellationToken);
    }

    private void AddEntry(List<(string, string, string, long, DateTime, bool)> batch, FileSystemEntry entry)
    {
        try
        {
            long size = 0;
            FileSystemInfo info;
            if (entry.IsFolder)
            {
                info = new DirectoryInfo(entry.Path);
            }
            else
            {
                var fileInfo = new FileInfo(entry.Path);
                size = fileInfo.Length;
                info = fileInfo;
            }

            batch.Add((entry.Path,
                info.Name,
                Path.GetDirectoryName(entry.Path) ?? string.Empty,
                size,
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

    /// <summary>
    /// Streams directories and files in a single pass (recurse-once), so memory stays
    /// bounded regardless of tree size and the disk is not scanned twice.
    /// </summary>
    private static IEnumerable<FileSystemEntry> EnumerateEntriesSafe(string root, CancellationToken cancellationToken)
    {
        var stack = new Stack<string>();
        stack.Push(root);

        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = false,
            AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.System | FileAttributes.Hidden,
        };

        while (stack.Count > 0)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;

            var dir = stack.Pop();
            List<string> subdirs;
            List<string> files;
            try
            {
                subdirs = Directory.EnumerateDirectories(dir, "*", options).ToList();
                files = Directory.EnumerateFiles(dir, "*", options).ToList();
            }
            catch
            {
                continue; // ignore protected paths
            }

            foreach (var sub in subdirs)
            {
                yield return new FileSystemEntry(sub, true);
                stack.Push(sub);
            }

            foreach (var file in files)
                yield return new FileSystemEntry(file, false);
        }
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