using Lunaris.Core.Interfaces;
using Lunaris.Core.Services;
using Lunaris.Infrastructure.Database;
using Lunaris.Infrastructure.Indexing;
using Lunaris.Infrastructure.Logging;
using Lunaris.Infrastructure.Update;
using Lunaris.Infrastructure.Windows;
using Lunaris.Search.ApplicationProvider;
using Lunaris.Search.CalculatorProvider;
using Lunaris.Search.ClipboardProvider;
using Lunaris.Search.CommandProvider;
using Lunaris.Search.ConversionProvider;
using Lunaris.Search.DownloadProvider;
using Lunaris.Search.FileProvider;
using Lunaris.Search.FavoritesProvider;
using Lunaris.Search.HistoryProvider;
using Lunaris.Search.SystemProvider;
using Lunaris.Search.ToolsProvider;
using Lunaris.Search.UrlProvider;
using Lunaris.Search.WebSearchProvider;
using Lunaris.UI.ViewModels;
using Lunaris.UI.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Lunaris;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        Serilog.Log.Logger = LoggingConfig.CreateLogger();
        Lunaris.Infrastructure.Logging.Log.Initialize(Serilog.Log.Logger);

        var singleInstance = new SingleInstanceService();
        if (!singleInstance.IsPrimary)
        {
            singleInstance.SignalPrimary();
            return;
        }

        App.SingleInstance = singleInstance;

        try
        {
            var host = Host.CreateDefaultBuilder(args)
                .UseSerilog()
                .ConfigureServices(ConfigureServices)
                .Build();

            App.Host = host;

            var app = new App();
            app.InitializeComponent();
            app.Run();

            host.StopAsync().GetAwaiter().GetResult();
            host.Dispose();
        }
        finally
        {
            singleInstance.Dispose();
        }
    }

    private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        // ---- Infrastructure ----
        services.AddSingleton<DatabaseFactory>();
        services.AddSingleton<MigrationRunner>();
        services.AddSingleton<SettingsRepository>();
        services.AddSingleton<HistoryRepository>();
        services.AddSingleton<FavoriteRepository>();
        services.AddSingleton<CommandRepository>();
        services.AddSingleton<ClipboardRepository>();
        services.AddSingleton<IndexedFileRepository>();
        services.AddSingleton<AppIndexRepository>();
        services.AddSingleton<HiddenMessageWindow>();

        // ---- Core services ----
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IHistoryService, HistoryService>();
        services.AddSingleton<IFavoritesService, FavoritesService>();
        services.AddSingleton<IActionRunner, ActionRunner>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IClipboardMonitor, ClipboardMonitor>();
        services.AddSingleton<IClipboardHistoryService, ClipboardHistoryService>();
        services.AddSingleton<IUpdateService, UpdateService>();
        services.AddSingleton<IDownloadService, DownloadService>();

        // ---- Windows integration ----
        services.AddSingleton<HotkeyService>();
        services.AddSingleton<IHotkeyService>(sp => sp.GetRequiredService<HotkeyService>());

        services.AddSingleton(sp =>
        {
            var actions = new TrayMenuActions
            {
                Open = () => sp.GetRequiredService<ILauncherService>().Show(),
                Settings = () => sp.GetRequiredService<SettingsWindow>().ShowWindow(),
                Reindex = () => _ = sp.GetRequiredService<IIndexService>().ReindexAsync(CancellationToken.None),
                PauseLabel = () => sp.GetRequiredService<IIndexService>().IsPaused ? "Retomar" : "Pausar",
                TogglePause = () =>
                {
                    var index = sp.GetRequiredService<IIndexService>();
                    index.IsPaused = !index.IsPaused;
                },
                About = () => new AboutWindow().ShowDialog(),
                CheckUpdates = () => _ = App.CheckForUpdatesAsync(notifyWhenUpToDate: true),
                Exit = App.ShutdownApp,
            };
            return actions;
        });
        services.AddSingleton<TrayIconService>();

        // ---- Indexing ----
        services.AddSingleton<FileIndexer>();
        services.AddSingleton<IndexService>();
        services.AddSingleton<IIndexService>(sp => sp.GetRequiredService<IndexService>());

        // ---- Search providers ----
        services.AddSingleton<ApplicationSearchProvider>();
        services.AddSingleton<FileSearchProvider>();
        services.AddSingleton<CalculatorProvider>();
        services.AddSingleton<CommandProvider>();
        services.AddSingleton<UrlProvider>();
        services.AddSingleton<SystemProvider>();
        services.AddSingleton<HistoryProvider>();
        services.AddSingleton<FavoritesProvider>();
        services.AddSingleton<ToolsProvider>();
        services.AddSingleton<ConversionProvider>();
        services.AddSingleton<ClipboardProvider>();

        services.AddSingleton<WebSearchProvider>();
        services.AddSingleton<ISearchProvider>(sp => sp.GetRequiredService<WebSearchProvider>());

        services.AddSingleton<DownloadProvider>();
        services.AddSingleton<ISearchProvider>(sp => sp.GetRequiredService<DownloadProvider>());

        services.AddSingleton<ISearchProvider>(sp => sp.GetRequiredService<ApplicationSearchProvider>());
        services.AddSingleton<ISearchProvider>(sp => sp.GetRequiredService<FileSearchProvider>());
        services.AddSingleton<ISearchProvider>(sp => sp.GetRequiredService<CalculatorProvider>());
        services.AddSingleton<ISearchProvider>(sp => sp.GetRequiredService<CommandProvider>());
        services.AddSingleton<ISearchProvider>(sp => sp.GetRequiredService<UrlProvider>());
        services.AddSingleton<ISearchProvider>(sp => sp.GetRequiredService<SystemProvider>());
        services.AddSingleton<ISearchProvider>(sp => sp.GetRequiredService<HistoryProvider>());
        services.AddSingleton<ISearchProvider>(sp => sp.GetRequiredService<FavoritesProvider>());
        services.AddSingleton<ISearchProvider>(sp => sp.GetRequiredService<ToolsProvider>());
        services.AddSingleton<ISearchProvider>(sp => sp.GetRequiredService<ConversionProvider>());
        services.AddSingleton<ISearchProvider>(sp => sp.GetRequiredService<ClipboardProvider>());

        services.AddSingleton<ISearchEngine, SearchEngine>();

        // ---- UI ----
        services.AddSingleton<LauncherViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<LauncherWindow>();
        services.AddSingleton<SettingsWindow>();
        services.AddSingleton<AboutWindow>();
        services.AddSingleton<ILauncherService, LauncherService>();
    }
}