using System.IO;
using Lunaris.Infrastructure.Windows;

namespace Lunaris.Core.Utilities;

public static class PathHelper
{
    /// <summary>Default directories searched by the file indexer.</summary>
    public static IReadOnlyList<string> DefaultSearchDirectories()
    {
        var list = new List<string>();

        void Add(string? knownFolder) => list.AddIfExists(knownFolder);

        Add(KnownFolders.GetPath(KnownFolders.Desktop));
        Add(KnownFolders.GetPath(KnownFolders.Documents));
        Add(KnownFolders.GetPath(KnownFolders.Downloads));
        Add(KnownFolders.GetPath(KnownFolders.Pictures));
        Add(KnownFolders.GetPath(KnownFolders.Videos));
        Add(KnownFolders.GetPath(KnownFolders.Music));

        return list;
    }

    private static void AddIfExists(this List<string> list, string? path)
    {
        if (path is not null && Directory.Exists(path) && !list.Contains(path))
            list.Add(path);
    }

    public static bool IsExecutable(string path)
    {
        var ext = Path.GetExtension(path);
        return string.Equals(ext, ".exe", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ext, ".bat", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ext, ".cmd", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ext, ".lnk", StringComparison.OrdinalIgnoreCase);
    }
}