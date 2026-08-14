using System.Globalization;

namespace Lunaris.Core.Models;

/// <summary>
/// Immutable application settings persisted in the SQLite <c>Settings</c> table as JSON.
/// </summary>
public sealed class AppSettings
{
    public bool StartWithWindows { get; set; }

    public bool ShowInTray { get; set; } = true;

    public bool OpenOnActiveMonitor { get; set; } = true;

    public bool CloseOnExecute { get; set; } = true;

    public bool AutoUpdate { get; set; } = true;

    public string HotkeyModifiers { get; set; } = "Ctrl+Alt";

    public string HotkeyKey { get; set; } = "Space";

    public string Theme { get; set; } = "System";

    public double FontSize { get; set; } = 14;

    public bool EnableApplications { get; set; } = true;

    public bool EnableFiles { get; set; } = true;

    public bool EnableSettings { get; set; } = true;

    public bool EnableHistory { get; set; } = true;

    public bool EnableClipboard { get; set; }

    public bool StoreHistory { get; set; } = true;

    public bool StoreClipboard { get; set; }

    public int MaxClipboardItems { get; set; } = 200;

    public int MaxResults { get; set; } = 8;

    public List<string> AdditionalSearchDirectories { get; set; } = new();

    public string Serialize() => System.Text.Json.JsonSerializer.Serialize(this);

    public static AppSettings Deserialize(string json)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public string DisplayHotkey =>
        $"{FormatModifiers(HotkeyModifiers)} + {HotkeyKey}";

    private static string FormatModifiers(string modifiers) =>
        string.Join(" + ", modifiers.Split('+', StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeModifier));

    private static string NormalizeModifier(string modifier)
    {
        if (string.Equals(modifier, "Ctrl", StringComparison.OrdinalIgnoreCase))
            return "CTRL";
        if (string.Equals(modifier, "Shift", StringComparison.OrdinalIgnoreCase))
            return "SHIFT";
        if (string.Equals(modifier, "Win", StringComparison.OrdinalIgnoreCase))
            return "WIN";
        return "ALT";
    }
}
