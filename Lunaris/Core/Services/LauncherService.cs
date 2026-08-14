using System.Windows;
using Lunaris.Core.Interfaces;
using Lunaris.UI.Views;

namespace Lunaris.Core.Services;

/// <summary>Controls the launcher window from services without touching WPF details.</summary>
public sealed class LauncherService : ILauncherService
{
    private readonly LauncherWindow _window;

    public LauncherService(LauncherWindow window) => _window = window;

    public bool IsVisible => _window.Visibility == Visibility.Visible;

    public void Show() => _window.ShowAndFocus();

    public void Hide() => _window.HideLauncher();

    public void Toggle()
    {
        if (IsVisible)
            Hide();
        else
            Show();
    }
}