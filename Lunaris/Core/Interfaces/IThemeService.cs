using Lunaris.Core.Models;

namespace Lunaris.Core.Interfaces;

/// <summary>Applies the configured theme (System / Light / Dark) to the UI.</summary>
public interface IThemeService
{
    void Apply(string themeName);

    void Apply(AppSettings settings);

    bool IsSystemUsingDarkTheme();
}