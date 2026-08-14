using System.IO;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Input;
using Lunaris.Core.Interfaces;
using Lunaris.Core.Models;
using Lunaris.Core.Services;
using Lunaris.Infrastructure.Database;
using Lunaris.Infrastructure.Indexing;
using Lunaris.Infrastructure.Logging;
using Lunaris.Infrastructure.Windows;
using Lunaris.Search.ApplicationProvider;
using Lunaris.Search.CalculatorProvider;
using Lunaris.Search.ClipboardProvider;
using Lunaris.Search.CommandProvider;
using Lunaris.Search.ConversionProvider;
using Lunaris.Search.FileProvider;
using Lunaris.Search.FavoritesProvider;
using Lunaris.Search.HistoryProvider;
using Lunaris.Search.SystemProvider;
using Lunaris.Search.ToolsProvider;
using Lunaris.Search.UrlProvider;
using Lunaris.UI.ViewModels;
using Lunaris.UI.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Lunaris;

public partial class App : Application
{
    public static IHost Host { get; set; } = null!;

    public static SingleInstanceService SingleInstance { get; set; } = null!;

    public static bool IsShuttingDown { get; private set; }

    private readonly CancellationTokenSource _appCts = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Log.Info("Lunaris starting (v{Version})", typeof(App).Assembly.GetName().Version?.ToString() ?? "1.4.0");

        try
        {
            // Database schema (must run before any repository access)
            Host.Services.GetRequiredService<MigrationRunner>().Apply();

            var settings = Host.Services.GetRequiredService<ISettingsService>();
            settings.Load();

            // Load cached state
            Host.Services.GetRequiredService<IFavoritesService>().Load();
            Host.Services.GetRequiredService<CommandProvider>().Reload();

            // Theme
            var theme = Host.Services.GetRequiredService<IThemeService>();
            theme.Apply(settings.Current);

            // System tray
            var tray = Host.Services.GetRequiredService<TrayIconService>();
            tray.Initialize();
            if (Host.Services.GetRequiredService<INotificationService>() is NotificationService notifications)
                notifications.Sink = tray.Show;

            // Global hotkey
            var hotkey = Host.Services.GetRequiredService<IHotkeyService>();
            var launcher = Host.Services.GetRequiredService<ILauncherService>();
            hotkey.HotkeyPressed += (_, _) => launcher.Toggle();
            if (!RegisterConfiguredHotkey(hotkey, settings.Current))
                tray.Show("Lunaris", "A combinação de teclas já está em uso. Altere nas configurações.");

            // Re-register hotkey / reapply theme whenever settings change
            settings.Changed += (_, _) =>
            {
                theme.Apply(settings.Current);
                RegisterConfiguredHotkey(hotkey, settings.Current);
            };

            // Settings window hotkey wiring
            var settingsWindow = Host.Services.GetRequiredService<SettingsWindow>();
            var settingsVm = (SettingsViewModel)settingsWindow.DataContext;
            settingsVm.HotkeyChanged = () =>
            {
                if (!RegisterConfiguredHotkey(hotkey, settings.Current))
                    tray.Show("Lunaris", "A nova combinação de teclas já está em uso.");
            };

            // Live system theme switching (System theme follows Windows)
            Host.Services.GetRequiredService<HiddenMessageWindow>()
                .AddHandler((_, msg, _, _) =>
                {
                    if (msg == NativeMethods.WM_SETTINGCHANGE && settings.Current.Theme == "System")
                        theme.Apply("System");
                    return false;
                });

            // Clipboard history (disabled by default)
            Host.Services.GetRequiredService<IClipboardHistoryService>().Start();

            // Background indexing
            _ = Host.Services.GetRequiredService<IIndexService>()
                .StartAsync(_appCts.Token)
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        Log.Error(t.Exception, "Background indexing failed");
                }, TaskScheduler.Default);

            // Single instance: a second launch shows the launcher
            SingleInstance.StartListenForShow(() =>
                Dispatcher.Invoke(() => launcher.Show(), DispatcherPriority.Send));

            // Auto-update check (non-blocking, after the app settles)
            if (settings.Current.AutoUpdate)
                _ = CheckForUpdatesAsync(delaySeconds: 5);

            Log.Info("Lunaris started");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Startup failed");
            MessageBox.Show("Falha ao iniciar o Lunaris. Consulte o log para detalhes.", "Lunaris",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        IsShuttingDown = true;
        Log.Info("Lunaris shutting down");

        try
        {
            _appCts.Cancel();
            Host.Services.GetRequiredService<IClipboardHistoryService>().Stop();
            Host.Services.GetRequiredService<IHotkeyService>().Unregister();
            Host.Services.GetRequiredService<TrayIconService>().Dispose();
            Host.Services.GetRequiredService<HiddenMessageWindow>().Dispose();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Shutdown cleanup failed");
        }

        Log.Info("Lunaris exited");
        base.OnExit(e);
    }

    public static void ShutdownApp()
    {
        if (IsShuttingDown)
            return;

        IsShuttingDown = true;

        var launcher = Host.Services.GetService<LauncherWindow>();
        launcher?.AllowClose();

        Current?.Shutdown();
    }

    /// <summary>
    /// Checks GitHub releases and, if a newer version exists, asks the user whether to
    /// download and install it. Used both at startup and from the tray menu.
    /// </summary>
    public static async Task CheckForUpdatesAsync(bool notifyWhenUpToDate = false, int delaySeconds = 0)
    {
        try
        {
            if (delaySeconds > 0)
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));

            var update = Host.Services.GetRequiredService<IUpdateService>();
            var info = await update.CheckAsync(CancellationToken.None);

            if (info is null)
            {
                Log.Info("Update check: already up to date");
                if (notifyWhenUpToDate)
                    await NotifyAsync("Lunaris", "Você já está na versão mais recente.");
                return;
            }

            var proceed = false;
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                proceed = MessageBox.Show(
                    $"Nova versão {info.Version} disponível.\n\nDeseja baixar e instalar agora?",
                    "Lunaris — Atualização",
                    MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes;
            });

            if (!proceed)
                return;

            var updateDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Lunaris", "updates");

            var installer = await update.DownloadAsync(info.DownloadUrl, updateDir, CancellationToken.None);

            // Launch the installer, then exit so the new instance can take over.
            if (update.LaunchInstaller(installer))
                ShutdownApp();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Update flow failed");
        }
    }

    private static async Task NotifyAsync(string title, string message)
    {
        var tray = Host.Services.GetService<TrayIconService>();
        if (tray is null || Application.Current is null)
            return;

        await Application.Current.Dispatcher.InvokeAsync(() => tray.Show(title, message));
    }

    private static bool RegisterConfiguredHotkey(IHotkeyService hotkey, AppSettings settings)
    {
        var modifiers = settings.HotkeyModifiers
            .Split('+', StringSplitOptions.RemoveEmptyEntries)
            .Select(m => m.Trim().ToLowerInvariant() switch
            {
                "ctrl" => Key.LeftCtrl,
                "shift" => Key.LeftShift,
                "win" => Key.LWin,
                _ => Key.LeftAlt,
            })
            .ToArray();

        var key = Enum.TryParse<Key>(settings.HotkeyKey, true, out var parsed) ? parsed : Key.Space;
        return hotkey.Register(modifiers, key);
    }
}