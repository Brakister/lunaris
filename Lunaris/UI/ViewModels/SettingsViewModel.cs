using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using Lunaris.Core.Interfaces;
using Lunaris.Core.Models;
using Lunaris.Infrastructure.Database;
using Lunaris.Infrastructure.Windows;

namespace Lunaris.UI.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IThemeService _theme;
    private readonly IHistoryService _history;
    private readonly IClipboardHistoryService _clipboard;
    private readonly CommandRepository _commands;
    private bool _loading;

    public Action? HotkeyChanged;

    // General
    [ObservableProperty] private bool _startWithWindows;
    [ObservableProperty] private bool _showInTray;
    [ObservableProperty] private bool _openOnActiveMonitor;
    [ObservableProperty] private bool _closeOnExecute;
    [ObservableProperty] private bool _autoUpdate;

    // Hotkey
    [ObservableProperty] private string _hotkeyModifiers = "Ctrl+Alt";
    [ObservableProperty] private string _hotkeyKey = "Space";

    // Appearance
    [ObservableProperty] private string _themeName = "System";
    [ObservableProperty] private double _fontSize = 14;

    // Search providers
    [ObservableProperty] private bool _enableApplications;
    [ObservableProperty] private bool _enableFiles;
    [ObservableProperty] private bool _enableSettings;
    [ObservableProperty] private bool _enableHistory;
    [ObservableProperty] private bool _enableClipboard;

    // Privacy
    [ObservableProperty] private bool _storeHistory;
    [ObservableProperty] private bool _storeClipboard;

    [ObservableProperty] private CustomCommand? _selectedCommand;
    [ObservableProperty] private string _newDirectory = string.Empty;
    [ObservableProperty] private string _commandName = string.Empty;
    [ObservableProperty] private string _commandAlias = string.Empty;
    [ObservableProperty] private string _commandExecutable = string.Empty;
    [ObservableProperty] private string _commandArguments = string.Empty;
    [ObservableProperty] private bool _commandRunAsAdmin;

    public ObservableCollection<CustomCommand> Commands { get; } = new();

    public ObservableCollection<string> AdditionalDirectories { get; } = new();

    public string DisplayHotkey =>
        $"{string.Join(" + ", HotkeyModifiers.Split('+', StringSplitOptions.RemoveEmptyEntries).Select(m => m.ToUpperInvariant()))} + {HotkeyKey}";

    public SettingsViewModel(
        ISettingsService settings,
        IThemeService theme,
        IHistoryService history,
        IClipboardHistoryService clipboard,
        CommandRepository commands)
    {
        _settings = settings;
        _theme = theme;
        _history = history;
        _clipboard = clipboard;
        _commands = commands;
    }

    public void Load()
    {
        _loading = true;

        var s = _settings.Current;
        StartWithWindows = s.StartWithWindows;
        ShowInTray = s.ShowInTray;
        OpenOnActiveMonitor = s.OpenOnActiveMonitor;
        CloseOnExecute = s.CloseOnExecute;
        AutoUpdate = s.AutoUpdate;
        HotkeyModifiers = s.HotkeyModifiers;
        HotkeyKey = s.HotkeyKey;
        ThemeName = s.Theme;
        FontSize = s.FontSize;
        EnableApplications = s.EnableApplications;
        EnableFiles = s.EnableFiles;
        EnableSettings = s.EnableSettings;
        EnableHistory = s.EnableHistory;
        EnableClipboard = s.EnableClipboard;
        StoreHistory = s.StoreHistory;
        StoreClipboard = s.StoreClipboard;

        AdditionalDirectories.Clear();
        foreach (var dir in s.AdditionalSearchDirectories)
            AdditionalDirectories.Add(dir);

        ReloadCommands();

        _loading = false;
        OnPropertyChanged(nameof(DisplayHotkey));
    }

    public void ReloadCommands()
    {
        Commands.Clear();
        foreach (var command in _commands.GetAll())
            Commands.Add(command);
    }

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (_loading) return;
        _settings.Update(x => x.StartWithWindows = value);
        if (value) StartupManager.Enable();
        else StartupManager.Disable();
    }

    partial void OnShowInTrayChanged(bool value) => Persist(x => x.ShowInTray = value);

    partial void OnOpenOnActiveMonitorChanged(bool value) => Persist(x => x.OpenOnActiveMonitor = value);

    partial void OnCloseOnExecuteChanged(bool value) => Persist(x => x.CloseOnExecute = value);

    partial void OnAutoUpdateChanged(bool value) => Persist(x => x.AutoUpdate = value);

    partial void OnHotkeyModifiersChanged(string value)
    {
        if (_loading) return;
        _settings.Update(x => x.HotkeyModifiers = value);
        OnPropertyChanged(nameof(DisplayHotkey));
        HotkeyChanged?.Invoke();
    }

    partial void OnHotkeyKeyChanged(string value)
    {
        if (_loading) return;
        _settings.Update(x => x.HotkeyKey = value);
        OnPropertyChanged(nameof(DisplayHotkey));
        HotkeyChanged?.Invoke();
    }

    partial void OnThemeNameChanged(string value)
    {
        if (_loading) return;
        _settings.Update(x => x.Theme = value);
        _theme.Apply(value);
    }

    partial void OnFontSizeChanged(double value)
    {
        if (_loading) return;
        _settings.Update(x => x.FontSize = value);
    }

    partial void OnEnableApplicationsChanged(bool value) => Persist(x => x.EnableApplications = value);

    partial void OnEnableFilesChanged(bool value) => Persist(x => x.EnableFiles = value);

    partial void OnEnableSettingsChanged(bool value) => Persist(x => x.EnableSettings = value);

    partial void OnEnableHistoryChanged(bool value) => Persist(x => x.EnableHistory = value);

    partial void OnEnableClipboardChanged(bool value) => Persist(x => x.EnableClipboard = value);

    partial void OnStoreHistoryChanged(bool value) => Persist(x => x.StoreHistory = value);

    partial void OnStoreClipboardChanged(bool value) => Persist(x => x.StoreClipboard = value);

    private void Persist(Action<AppSettings> change)
    {
        if (_loading) return;
        _settings.Update(change);
    }

    public void AddDirectory()
    {
        var path = NewDirectory?.Trim();
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            return;

        if (AdditionalDirectories.Contains(path, StringComparer.OrdinalIgnoreCase))
            return;

        AdditionalDirectories.Add(path);
        _settings.Update(x => x.AdditionalSearchDirectories = AdditionalDirectories.ToList());
        NewDirectory = string.Empty;
    }

    public void RemoveDirectory(string path)
    {
        AdditionalDirectories.Remove(path);
        _settings.Update(x => x.AdditionalSearchDirectories = AdditionalDirectories.ToList());
    }

    public void SaveCommand()
    {
        if (string.IsNullOrWhiteSpace(CommandName) || string.IsNullOrWhiteSpace(CommandExecutable))
            return;

        var alias = string.IsNullOrWhiteSpace(CommandAlias) ? CommandName : CommandAlias;
        var command = new CustomCommand
        {
            Id = SelectedCommand?.Id ?? 0,
            Name = CommandName.Trim(),
            Alias = alias.Trim(),
            Executable = CommandExecutable.Trim(),
            Arguments = CommandArguments?.Trim() ?? string.Empty,
            RunAsAdministrator = CommandRunAsAdmin,
        };

        _commands.Save(command);
        ReloadCommands();
        ResetCommandForm();
    }

    public void DeleteSelectedCommand()
    {
        if (SelectedCommand is null)
            return;

        _commands.Delete(SelectedCommand.Id);
        ReloadCommands();
        ResetCommandForm();
    }

    public void StartEditSelectedCommand()
    {
        if (SelectedCommand is null)
            return;

        CommandName = SelectedCommand.Name;
        CommandAlias = SelectedCommand.Alias;
        CommandExecutable = SelectedCommand.Executable;
        CommandArguments = SelectedCommand.Arguments;
        CommandRunAsAdmin = SelectedCommand.RunAsAdministrator;
    }

    public void ResetCommandForm()
    {
        SelectedCommand = null;
        CommandName = string.Empty;
        CommandAlias = string.Empty;
        CommandExecutable = string.Empty;
        CommandArguments = string.Empty;
        CommandRunAsAdmin = false;
    }

    public void ClearHistory()
    {
        _history.Clear();
    }

    public void ClearClipboard()
    {
        _clipboard.Clear();
    }
}