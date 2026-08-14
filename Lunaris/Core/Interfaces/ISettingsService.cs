using Lunaris.Core.Models;

namespace Lunaris.Core.Interfaces;

public interface ISettingsService
{
    AppSettings Current { get; }

    event EventHandler? Changed;

    void Load();

    void Save();

    void Update(Action<AppSettings> change);
}