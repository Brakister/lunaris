using Microsoft.Win32;

namespace Lunaris.Infrastructure.Windows;

/// <summary>Controls Windows startup via the HKCU Run registry key.</summary>
public static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Lunaris";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string value
            && value.Contains("Lunaris", StringComparison.OrdinalIgnoreCase);
    }

    public static void Enable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, true) ?? Registry.CurrentUser.CreateSubKey(RunKey);
        key.SetValue(ValueName, $"\"{Environment.ProcessPath}\"");
    }

    public static void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}