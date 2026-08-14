using Lunaris.Core.Interfaces;
using Lunaris.Core.Models;
using Lunaris.Core.Utilities;
using Lunaris.Infrastructure.Database;
using Lunaris.Infrastructure.Logging;

namespace Lunaris.Infrastructure.Indexing;

public sealed class IndexService : IIndexService
{
    private readonly AppIndexRepository _appIndexRepository;
    private readonly IndexedFileRepository _fileRepository;
    private readonly FileIndexer _fileIndexer;
    private readonly ISettingsService _settings;

    private volatile IReadOnlyList<IndexedApplication> _applications = Array.Empty<IndexedApplication>();
    private volatile bool _isReady;
    private int _paused;

    public IndexService(
        AppIndexRepository appIndexRepository,
        IndexedFileRepository fileRepository,
        FileIndexer fileIndexer,
        ISettingsService settings)
    {
        _appIndexRepository = appIndexRepository;
        _fileRepository = fileRepository;
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

        return Task.Run(() => ReindexCoreAsync(cancellationToken), cancellationToken);
    }

    public async Task ReindexAsync(CancellationToken cancellationToken)
    {
        await Task.Run(() => ReindexCoreAsync(cancellationToken), cancellationToken);
    }

    private void ReindexCoreAsync(CancellationToken cancellationToken)
    {
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
        try
        {
            _fileIndexer.ReindexAsync(roots, cancellationToken).GetAwaiter().GetResult();
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

    private IReadOnlyList<string> ComputeRoots()
    {
        var roots = new List<string>();
        roots.AddRange(PathHelper.DefaultSearchDirectories());
        roots.AddRange(_settings.Current.AdditionalSearchDirectories);
        return roots.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}