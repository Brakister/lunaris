namespace Lunaris.Core.Interfaces;

/// <summary>Controls the lifecycle of the launcher window.</summary>
public interface ILauncherService
{
    bool IsVisible { get; }

    /// <summary>Shows the launcher, centers it and focuses the search box.</summary>
    void Show();

    void Hide();

    void Toggle();
}
