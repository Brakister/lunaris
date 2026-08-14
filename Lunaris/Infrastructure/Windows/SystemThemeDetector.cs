using Microsoft.Win32;

namespace Lunaris.Infrastructure.Windows;

/// <summary>Detects whether Windows is currently using the light or dark app theme.</summary>
public static class SystemThemeDetector
{
    public static bool IsDarkThemeActive()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("AppsUseLightTheme") is int value)
                return value == 0;
        }
        catch
        {
            // fall through to default
        }

        return false;
    }
}