using System.IO;
using Lunaris.Infrastructure.Database;
using Lunaris.Infrastructure.Logging;
using Microsoft.Win32;

namespace Lunaris.Infrastructure.Indexing;

/// <summary>Discovers installed applications from the Start Menu, registry, App Paths, PATH and Windows tools.</summary>
public static class AppIndexer
{
    private const string AppPathsKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths";
    private const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    private static readonly (string Exe, string Name, string Alias)[] SystemTools =
    {
        ("cmd.exe", "Prompt de Comando", "command prompt"),
        ("powershell.exe", "Windows PowerShell", "pwsh"),
        ("pwsh.exe", "PowerShell 7", "powershell"),
        ("mstsc.exe", "Conexão de Área de Trabalho Remota", "remote desktop"),
        ("notepad.exe", "Bloco de Notas", "notepad"),
        ("taskmgr.exe", "Gerenciador de Tarefas", "task manager"),
        ("regedit.exe", "Editor do Registro", "registry editor"),
        ("msconfig.exe", "Configuração do Sistema", "system configuration"),
        ("control.exe", "Painel de Controle", "control panel"),
        ("winver.exe", "Sobre o Windows", "windows version"),
        ("osk.exe", "Teclado Virtual", "onscreen keyboard"),
        ("snippingtool.exe", "Ferramenta de Captura", "snipping tool"),
        ("mspaint.exe", "Paint", "paint"),
        ("charmap.exe", "Mapa de Caracteres", "character map"),
        ("explorer.exe", "Explorador de Arquivos", "file explorer"),
        ("perfmon.exe", "Monitor de Desempenho", "performance monitor"),
        ("resmon.exe", "Monitor de Recursos", "resource monitor"),
    };

    public static IReadOnlyList<IndexedApplication> Discover(CancellationToken cancellationToken)
    {
        var apps = new Dictionary<string, IndexedApplication>(StringComparer.OrdinalIgnoreCase);

        try
        {
            DiscoverStartMenu(apps, cancellationToken);
            DiscoverInstalledApplications(apps, cancellationToken);
            DiscoverAppPaths(apps, cancellationToken);
            DiscoverPathExecutables(apps, cancellationToken);
            DiscoverSystemTools(apps, cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Application discovery failed");
        }

        Log.Info("Discovered {Count} applications", apps.Count);

        foreach (var app in apps.Values)
            if (string.IsNullOrEmpty(app.SearchText))
                app.SearchText = $"{app.Name} {Path.GetFileNameWithoutExtension(app.Path)}";

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

    private static void DiscoverInstalledApplications(IDictionary<string, IndexedApplication> apps, CancellationToken cancellationToken)
    {
        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        {
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                    using var key = baseKey.OpenSubKey(UninstallKey);
                    if (key is null)
                        continue;

                    foreach (var subKeyName in key.GetSubKeyNames())
                    {
                        if (cancellationToken.IsCancellationRequested)
                            return;

                        using var subKey = key.OpenSubKey(subKeyName);
                        if (subKey is null)
                            continue;

                        var displayName = GetStringValue(subKey, "DisplayName");
                        if (string.IsNullOrWhiteSpace(displayName))
                            continue;

                        var path = ResolveExecutablePath(subKey);
                        if (string.IsNullOrWhiteSpace(path))
                            continue;

                        var installLocation = GetStringValue(subKey, "InstallLocation");
                        var displayIcon = GetStringValue(subKey, "DisplayIcon");
                        var publisher = GetStringValue(subKey, "Publisher");

                        apps.TryAdd(KeyFor(path), new IndexedApplication
                        {
                            Id = KeyFor(path),
                            Name = displayName,
                            Path = path,
                            Category = "Aplicativo",
                            Icon = Lunaris.Core.Utilities.GlyphCatalog.App,
                            SearchText = string.Join(' ', new[]
                            {
                                displayName,
                                publisher,
                                Path.GetFileNameWithoutExtension(path),
                                Path.GetFileNameWithoutExtension(installLocation ?? string.Empty),
                                Path.GetFileNameWithoutExtension(displayIcon ?? string.Empty),
                            }.Where(s => !string.IsNullOrWhiteSpace(s))),
                        });
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Installed application discovery failed for {Hive} {View}", hive, view);
                }
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

    private static void DiscoverSystemTools(IDictionary<string, IndexedApplication> apps, CancellationToken cancellationToken)
    {
        var systemDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
        if (string.IsNullOrEmpty(systemDir) || !Directory.Exists(systemDir))
            return;

        foreach (var (exe, name, alias) in SystemTools)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            var path = Path.Combine(systemDir, exe);
            if (!File.Exists(path))
                continue;

            apps.TryAdd(KeyFor(path), new IndexedApplication
            {
                Id = KeyFor(path),
                Name = name,
                Path = path,
                Category = "Sistema",
                Icon = Lunaris.Core.Utilities.GlyphCatalog.App,
                SearchText = $"{name} {Path.GetFileNameWithoutExtension(exe)} {alias}",
            });
        }
    }

    private static string? ResolveExecutablePath(RegistryKey subKey)
    {
        var displayIcon = GetStringValue(subKey, "DisplayIcon");
        var path = TryParseExecutablePath(displayIcon);
        if (!string.IsNullOrWhiteSpace(path))
            return path;

        var installLocation = GetStringValue(subKey, "InstallLocation");
        if (!string.IsNullOrWhiteSpace(installLocation) && Directory.Exists(installLocation))
        {
            var locationName = Path.GetFileName(Path.TrimEndingDirectorySeparator(installLocation));
            var candidateFromFolder = FindBestExecutable(installLocation, locationName);
            if (!string.IsNullOrWhiteSpace(candidateFromFolder))
                return candidateFromFolder;
        }

        var uninstallString = GetStringValue(subKey, "QuietUninstallString");
        path = TryParseExecutablePath(uninstallString);
        if (!string.IsNullOrWhiteSpace(path))
            return path;

        uninstallString = GetStringValue(subKey, "UninstallString");
        return TryParseExecutablePath(uninstallString);
    }

    private static string? FindBestExecutable(string installLocation, string? preferredStem)
    {
        try
        {
            var executables = Directory.EnumerateFiles(installLocation, "*.exe", SearchOption.TopDirectoryOnly)
                .Where(File.Exists)
                .ToList();

            if (executables.Count == 0)
                return null;

            if (!string.IsNullOrWhiteSpace(preferredStem))
            {
                var match = executables.FirstOrDefault(file =>
                    string.Equals(Path.GetFileNameWithoutExtension(file), preferredStem, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(match))
                    return match;
            }

            return executables
                .OrderByDescending(file => Path.GetFileNameWithoutExtension(file).Length)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static string? TryParseExecutablePath(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return null;

        var value = rawValue.Trim();
        if (value.StartsWith('"'))
        {
            var endQuote = value.IndexOf('"', 1);
            if (endQuote > 1)
            {
                var candidate = value[1..endQuote];
                return File.Exists(candidate) ? candidate : null;
            }
        }

        var comma = value.IndexOf(',');
        if (comma > 0)
            value = value[..comma];

        var tokens = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
            return null;

        for (var i = tokens.Length; i > 0; i--)
        {
            var candidate = string.Join(' ', tokens.Take(i));
            if (File.Exists(candidate))
                return candidate;
        }

        return File.Exists(value) ? value : null;
    }

    private static string? GetStringValue(RegistryKey key, string valueName) =>
        key.GetValue(valueName) as string;

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
