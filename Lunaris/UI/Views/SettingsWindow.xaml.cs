using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Lunaris.UI.ViewModels;

namespace Lunaris.UI.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _viewModel.Load();
    }

    public void ShowWindow()
    {
        if (!IsVisible)
            Show();
        Activate();
        Focus();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // The window is a singleton: instead of destroying it (which makes Show()
        // throw on the next tray click), hide it and let the app shutdown path close it.
        if (!App.IsShuttingDown)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    private void OnKeyCaptureKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        var isModifier = key is Key.LeftCtrl or Key.RightCtrl
            or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin;

        if (isModifier)
        {
            e.Handled = true;
            return;
        }

        var keys = Keyboard.Modifiers;
        var modifierList = new List<string>();
        if ((keys & ModifierKeys.Control) != 0) modifierList.Add("Ctrl");
        if ((keys & ModifierKeys.Alt) != 0) modifierList.Add("Alt");
        if ((keys & ModifierKeys.Shift) != 0) modifierList.Add("Shift");
        if ((keys & ModifierKeys.Windows) != 0) modifierList.Add("Win");
        if (modifierList.Count == 0)
            modifierList.Add("Alt");

        _viewModel.HotkeyModifiers = string.Join("+", modifierList);
        _viewModel.HotkeyKey = key.ToString();
        e.Handled = true;
    }

    private void OnAddDirectoryClick(object sender, RoutedEventArgs e) => _viewModel.AddDirectory();

    private void OnRemoveDirectoryClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string path })
            _viewModel.RemoveDirectory(path);
    }

    private void OnCommandSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        => _viewModel.StartEditSelectedCommand();

    private void OnSaveCommandClick(object sender, RoutedEventArgs e) => _viewModel.SaveCommand();

    private void OnDeleteCommandClick(object sender, RoutedEventArgs e) => _viewModel.DeleteSelectedCommand();

    private void OnClearHistoryClick(object sender, RoutedEventArgs e) => _viewModel.ClearHistory();

    private void OnClearClipboardClick(object sender, RoutedEventArgs e) => _viewModel.ClearClipboard();
}