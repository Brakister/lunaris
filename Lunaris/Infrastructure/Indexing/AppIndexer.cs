using System.IO;
using Lunaris.Infrastructure.Database;
using Lunaris.Infrastructure.Logging;
using Microsoft.Win32;

namespace Lunaris.Infrastructure.Indexing;

/// <summary>Discovers installed applications from Start Menu, App Paths and PATH.</summary>
public static class AppIndexer
{
    private const string AppPathsKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths";

    public static IReadOnlyList<IndexedApplication> Discover(CancellationToken cancellationToken)
    {
        var apps = new Dictionary<string, IndexedApplication>(StringComparer.OrdinalIgnoreCase);

        try
        {
            DiscoverStartMenu(apps, cancellationToken);
            DiscoverAppPaths(apps, cancellationToken);
            DiscoverPathExecutables(apps, cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Application discovery failed");
        }

        Log.Info("Discovered {Count} applications", apps.Count);
        return apps.Values.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void DiscoverStartMenu(IDictionary<string, IndexedApplication> apps, CancellationToken cancellationToken)
    {
        var roots = new List<string?>
        {
            KnownFolders2.StartMenuPrograms,
            KnownFolders2.RoamingStartMenuPrograms,
        };

        foreach (var root in roots)
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                continue;

            foreach (var file in EnumerateSafe(root, "*.lnk", cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                var name = Path.GetFileNameWithoutExtension(file);
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                apps.TryAdd(KeyFor(file), new IndexedApplication
                {
                    Id = KeyFor(file),
                    Name = name,
                    Path = file,
                    Category = "Aplicativo",
                    Icon = Lunaris.Core.Utilities.GlyphCatalog.App,
                });
            }
        }
    }

    private static void DiscoverAppPaths(IDictionary<string, IndexedApplication> apps, CancellationToken cancellationToken)
    {
        foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            try
            {
                using var key = hive.OpenSubKey(AppPathsKey);
                if (key is null)
                    continue;

                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    using var subKey = key.OpenSubKey(subKeyName);
                    if (subKey is null)
                        continue;

                    var path = subKey.GetValue(null) as string;
                    if (string.IsNullOrEmpty(path) || !File.Exists(path))
                        continue;

                    var name = Path.GetFileNameWithoutExtension(subKeyName);
                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    apps.TryAdd(KeyFor(path), new IndexedApplication
                    {
                        Id = KeyFor(path),
                        Name = name,
                        Path = path,
                        Category = "Aplicativo",
                        Icon = Lunaris.Core.Utilities.GlyphCatalog.App,
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "App Paths enumeration failed");
            }
        }
    }

    private static void DiscoverPathExecutables(IDictionary<string, IndexedApplication> apps, CancellationToken cancellationToken)
    {
        var pathVariable = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathVariable))
            return;

        var count = 0;
        foreach (var dir in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            if (dir.StartsWith(@"C:\Windows", StringComparison.OrdinalIgnoreCase) || !Directory.Exists(dir))
                continue;

            foreach (var file in EnumerateSafe(dir, "*.exe", cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                if (++count > 800)
                    return;

                var name = Path.GetFileNameWithoutExtension(file);
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                apps.TryAdd(KeyFor(file), new IndexedApplication
                {
                    Id = KeyFor(file),
                    Name = name,
                    Path = file,
                    Category = "Aplicativo",
                    Icon = Lunaris.Core.Utilities.GlyphCatalog.App,
                });
            }
        }
    }

    private static List<string> EnumerateSafe(string dir, string pattern, CancellationToken cancellationToken)
    {
        var list = new List<string>();
        try
        {
            foreach (var file in Directory.EnumerateFiles(dir, pattern, new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = false,
                AttributesToSkip = FileAttributes.ReparsePoint,
            }))
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
                list.Add(file);
            }
        }
        catch
        {
            // directory disappeared or is protected
        }
        return list;
    }

    private static string KeyFor(string path) => "app:" + path.ToLowerInvariant();
}

/// <summary>Thin wrapper so AppIndexer stays decoupled from KnownFolders usage in the indexer.</summary>
internal static class KnownFolders2
{
    public static string? StartMenuPrograms { get; } = Resolve(Lunaris.Infrastructure.Windows.KnownFolders.StartMenuPrograms, @"ProgramData\Microsoft\Windows\Start Menu\Programs");

    public static string? RoamingStartMenuPrograms { get; } = Resolve(Lunaris.Infrastructure.Windows.KnownFolders.RoamingStartMenuPrograms, @"Microsoft\Windows\Start Menu\Programs");

    private static string? Resolve(Guid knownFolder, string fallbackRelative)
    {
        var path = Lunaris.Infrastructure.Windows.KnownFolders.GetPath(knownFolder);
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            return path;

        try
        {
            var fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"..\Roaming", fallbackRelative);
            return Directory.Exists(fallback) ? fallback : null;
        }
        catch
        {
            return null;
        }
    }
}