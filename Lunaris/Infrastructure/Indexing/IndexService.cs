using System.IO;
using System.Text.Json;
using System.Threading;
using Lunaris.Core.Interfaces;
using Lunaris.Core.Models;
using Lunaris.Core.Utilities;
using Lunaris.Infrastructure.Database;
using Lunaris.Infrastructure.Logging;

namespace Lunaris.Infrastructure.Indexing;

public sealed class IndexService : IIndexService
{
    private const string FileIndexStampKey = "fileIndex";
    private static readonly TimeSpan FreshWindow = TimeSpan.FromHours(12);

    private readonly AppIndexRepository _appIndexRepository;
    private readonly SettingsRepository _stampRepository;
    private readonly FileIndexer _fileIndexer;
    private readonly ISettingsService _settings;

    private volatile IReadOnlyList<IndexedApplication> _applications = Array.Empty<IndexedApplication>();
    private volatile bool _isReady;
    private int _paused;

    public IndexService(
        AppIndexRepository appIndexRepository,
        SettingsRepository stampRepository,
        FileIndexer fileIndexer,
        ISettingsService settings)
    {
        _appIndexRepository = appIndexRepository;
        _stampRepository = stampRepository;
        _fileIndexer = fileIndexer;
        _settings = settings;
    }

    public bool IsReady => _isReady;

    public bool IsPaused
    {
        get => Volatile.Read(ref _paused) != 0;
        set => Volatile.Write(ref _paused, value ? 1 : 0);
    }

    /// <summary>Snapshot of the current application index (thread-safe via volatile reference).</summary>
    public IReadOnlyList<IndexedApplication> Applications => _applications;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Fast path: reload persisted apps immediately so search works on first keystroke.
        try
        {
            var cached = _appIndexRepository.GetAll();
            if (cached.Count > 0)
            {
                _applications = cached;
                _isReady = true;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load cached application index");
        }

        return Task.Run(() => ReindexCoreAsync(cancellationToken, force: false), cancellationToken);
    }

    public async Task ReindexAsync(CancellationToken cancellationToken)
    {
        await Task.Run(() => ReindexCoreAsync(cancellationToken, force: true), cancellationToken);
    }

    private void ReindexCoreAsync(CancellationToken cancellationToken, bool force)
    {
        // Yield CPU to interactive apps while scanning.
        Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;

        if (IsPaused)
        {
            Log.Info("Indexing skipped: paused");
            return;
        }

        try
        {
            var discovered = AppIndexer.Discover(cancellationToken);
            _applications = discovered;
            _isReady = true;

            try
            {
                _appIndexRepository.ReplaceAll(discovered);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to persist application index");
            }
        }
        catch (OperationCanceledException)
        {
            // expected
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Application indexing failed");
        }

        // File index (SQLite) - always run on the configured roots.
        if (!_settings.Current.EnableFiles)
            return;

        var roots = ComputeRoots();

        if (!force && IsFileIndexFresh(roots))
        {
            Log.Info("File index is up to date; skipping reindex");
            return;
        }

        try
        {
            _fileIndexer.ReindexAsync(roots, cancellationToken).GetAwaiter().GetResult();
            _stampRepository.Set(FileIndexStampKey,
                JsonSerializer.Serialize(new FileIndexStamp
                {
                    Utc = DateTime.UtcNow,
                    RootsHash = RootsHash(roots),
                }));
        }
        catch (OperationCanceledException)
        {
            // expected
        }
        catch (Exception ex)
        {
            Log.Error(ex, "File indexing failed");
        }
    }

    private bool IsFileIndexFresh(IReadOnlyList<string> roots)
    {
        var stamp = _stampRepository.Get(FileIndexStampKey);
        if (string.IsNullOrEmpty(stamp))
            return false;

        try
        {
            var data = JsonSerializer.Deserialize<FileIndexStamp>(stamp);
            if (data is null)
                return false;

            if (!string.Equals(data.RootsHash, RootsHash(roots), StringComparison.Ordinal))
                return false;

            return DateTime.UtcNow - data.Utc < FreshWindow;
        }
        catch
        {
            return false;
        }
    }

    private static string RootsHash(IReadOnlyList<string> roots) =>
        string.Join("\u001F", roots.OrderBy(r => r, StringComparer.OrdinalIgnoreCase));

    private IReadOnlyList<string> ComputeRoots()
    {
        var roots = new List<string>();
        roots.AddRange(PathHelper.DefaultSearchDirectories());
        roots.AddRange(_settings.Current.AdditionalSearchDirectories);
        return roots.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private sealed class FileIndexStamp
    {
        public DateTime Utc { get; set; }

        public string RootsHash { get; set; } = string.Empty;
    }
}
