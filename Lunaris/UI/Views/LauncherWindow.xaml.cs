using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Lunaris.Core.Models;
using Lunaris.Infrastructure.Windows;
using Lunaris.UI.ViewModels;

namespace Lunaris.UI.Views;

public partial class LauncherWindow : Window
{
    private readonly LauncherViewModel _viewModel;
    private bool _allowClose;
    private bool _isAnimating;

    public LauncherWindow(LauncherViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.RequestClose = HideLauncher;
    }

    /// <summary>Shows, centers, focuses and plays the short opening animation.</summary>
    public void ShowAndFocus()
    {
        if (Visibility != Visibility.Visible)
        {
            Visibility = Visibility.Visible;
            _viewModel.Reset();
            AnimateOpen();
        }

        CenterOnActiveMonitor();
        WindowHelper.ShowAndActivate(this);
        SearchBox.Focus();
        Keyboard.Focus(SearchBox);
        SearchBox.SelectAll();
    }

    public void HideLauncher()
    {
        if (_isAnimating)
            return;

        _isAnimating = true;
        var animation = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(90))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn },
        };
        animation.Completed += (_, _) =>
        {
            Visibility = Visibility.Hidden;
            _isAnimating = false;
        };
        Card.BeginAnimation(OpacityProperty, animation);
    }

    private void AnimateOpen()
    {
        Card.BeginAnimation(OpacityProperty, null);
        Card.Opacity = 0;
        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(120))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };
        Card.BeginAnimation(OpacityProperty, fade);
    }

    private void CenterOnActiveMonitor()
    {
        var area = MonitorHelper.GetWorkAreaForCursor();
        Left = area.Left + (area.Width - ActualWidth) / 2;
        Top = area.Top + (area.Height - ActualHeight) / 2 - 40;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                _viewModel.MoveSelection(1);
                e.Handled = true;
                break;

            case Key.Up:
                _viewModel.MoveSelection(-1);
                e.Handled = true;
                break;

            case Key.Enter when (Keyboard.Modifiers & ModifierKeys.Control) != 0:
                _ = _viewModel.ExecuteSelectedAsync(true);
                e.Handled = true;
                break;

            case Key.Enter:
                _ = _viewModel.ExecuteSelectedAsync(false);
                e.Handled = true;
                break;

            case Key.Escape:
                HideLauncher();
                e.Handled = true;
                break;

            case Key.C when (Keyboard.Modifiers & ModifierKeys.Control) != 0:
                // Allow normal text copy while editing the query; otherwise copy the result.
                if (SearchBox.SelectionLength == 0)
                {
                    _ = _viewModel.CopySelectedAsync();
                    e.Handled = true;
                }
                break;

            case Key.P when (Keyboard.Modifiers & ModifierKeys.Control) != 0:
                _viewModel.ToggleFavoriteSelected();
                e.Handled = true;
                break;

            case Key.D when (Keyboard.Modifiers & ModifierKeys.Alt) != 0:
                // README documents ALT+D as the favorites shortcut; keep CTRL+P too.
                _viewModel.ToggleFavoriteSelected();
                e.Handled = true;
                break;

            case Key.J when (Keyboard.Modifiers & ModifierKeys.Control) != 0:
                _viewModel.MoveSelection(1);
                e.Handled = true;
                break;

            case Key.K when (Keyboard.Modifiers & ModifierKeys.Control) != 0:
                _viewModel.MoveSelection(-1);
                e.Handled = true;
                break;
        }
    }

    private void OnResultsListMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source)
            return;

        var item = ItemsControl.ContainerFromElement(ResultsList, source) as ListBoxItem;
        if (item?.DataContext is not SearchResult result)
            return;

        if (!ReferenceEquals(_viewModel.SelectedResult, result))
            _viewModel.SelectedResult = result;

        _ = _viewModel.ExecuteSelectedAsync(false);
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            HideLauncher();
            return;
        }

        base.OnClosing(e);
    }

    /// <summary>Lets the app shutdown path actually close this window.</summary>
    public void AllowClose() => _allowClose = true;
}