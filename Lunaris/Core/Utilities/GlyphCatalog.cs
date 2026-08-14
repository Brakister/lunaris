using Lunaris.Core.Models;

namespace Lunaris.Core.Utilities;

/// <summary>
/// Glyph catalog using the Segoe MDL2 Assets / Segoe UI Symbol fonts so the UI has no
/// external image dependencies and stays razor sharp on any DPI.
/// </summary>
public static class GlyphCatalog
{
    public const string FontFamily = "Segoe MDL2 Assets";

    public const string Search = "\uE721";
    public const string App = "\uE7F4";
    public const string File = "\uE7C3";
    public const string Folder = "\uE8B7";
    public const string Url = "\uE71B";
    public const string Setting = "\uE713";
    public const string Tool = "\uE756";
    public const string Calculator = "\uE8EF";
    public const string History = "\uE81C";
    public const string Star = "\uE734";
    public const string Command = "\uE756";
    public const string Clipboard = "\uE77F";
    public const string Hash = "\uE950";
    public const string Lock = "\uE72E";
    public const string Convert = "\uE896";
    public const string Clock = "\uE823";
    public const string FolderOpen = "\uE8B7";
    public const string Info = "\uE946";
    public const string Settings = "\uE713";

    public static string ForKind(SearchResultKind kind) => kind switch
    {
        SearchResultKind.App => App,
        SearchResultKind.File => File,
        SearchResultKind.Folder => FolderOpen,
        SearchResultKind.Url => Url,
        SearchResultKind.Setting => Setting,
        SearchResultKind.SystemTool => Tool,
        SearchResultKind.Calculation => Calculator,
        SearchResultKind.TextAction => Hash,
        SearchResultKind.ClipboardItem => Clipboard,
        SearchResultKind.Favorite => Star,
        SearchResultKind.History => History,
        SearchResultKind.Command => Command,
        _ => App,
    };
}