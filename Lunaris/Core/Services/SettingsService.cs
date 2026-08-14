using Lunaris.Core.Interfaces;
using Lunaris.Core.Models;
using Lunaris.Infrastructure.Database;
using Lunaris.Infrastructure.Logging;

namespace Lunaris.Core.Services;

public sealed class SettingsService : ISettingsService
{
    private const string StorageKey = "app";

    private readonly SettingsRepository _repository;
    private AppSettings _current = new();

    public SettingsService(SettingsRepository repository) => _repository = repository;

    public AppSettings Current => _current;

    public event EventHandler? Changed;

    public void Load()
    {
        try
        {
            var json = _repository.Get(StorageKey);
            _current = string.IsNullOrEmpty(json) ? new AppSettings() : AppSettings.Deserialize(json);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load settings; using defaults");
            _current = new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            _repository.Set(StorageKey, _current.Serialize());
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save settings");
        }
    }

    public void Update(Action<AppSettings> change)
    {
        change(_current);
        Save();
    }
}