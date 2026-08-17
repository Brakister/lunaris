using Microsoft.Win32;
using Lunaris.Infrastructure.Logging;

namespace Lunaris.Infrastructure.Windows;

/// <summary>
/// Controls Windows startup via the HKCU Run registry key. Windows caches the
/// enable/disable state of Run entries in StartupApproved (the state shown in
/// Task Manager), so both values are written together to make sure the entry
/// is actually launched at logon and never gets silently skipped.
/// </summary>
public static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string ValueName = "Lunaris";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is string value
                && value.Contains("Lunaris", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to read startup entry");
            return false;
        }
    }

    public static void Enable()
    {
        var value = $"\"{Environment.ProcessPath}\"";

        using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true) ?? Registry.CurrentUser.CreateSubKey(RunKeyPath))
            key.SetValue(ValueName, value);

        // Force the "enabled" state in Task Manager (byte 0x02 = enabled by user).
        using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKeyPath, true) ?? Registry.CurrentUser.CreateSubKey(ApprovedKeyPath);
        approved.SetValue(ValueName, new byte[] { 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, RegistryValueKind.Binary);

        Log.Info("Startup entry enabled: {Value}", value);
    }

    public static void Disable()
    {
        using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
            key?.DeleteValue(ValueName, throwOnMissingValue: false);

        using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKeyPath, true);
        approved?.DeleteValue(ValueName, throwOnMissingValue: false);

        Log.Info("Startup entry disabled");
    }
}