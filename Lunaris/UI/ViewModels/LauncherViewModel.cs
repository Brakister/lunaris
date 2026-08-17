using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Lunaris.Core.Interfaces;
using Lunaris.Core.Models;
using Lunaris.Infrastructure.Logging;

namespace Lunaris.UI.ViewModels;

public sealed partial class LauncherViewModel : ObservableObject
{
    private readonly ISearchEngine _searchEngine;
    private readonly IActionRunner _actionRunner;
    private readonly IHistoryService _history;
    private readonly IFavoritesService _favorites;
    private readonly ISettingsService _settings;
    private readonly INotificationService _notification;

    private CancellationTokenSource _cts = new();
    private bool _confirmed;

    [ObservableProperty]
    private string _query = string.Empty;

    [ObservableProperty]
    private SearchResult? _selectedResult;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private double _fontSize = 14;

    public ObservableCollection<SearchResult> Results { get; } = new();

    /// <summary>Set by the window so the VM stays UI-framework agnostic.</summary>
    public Action? RequestClose { get; set; }

    public LauncherViewModel(
        ISearchEngine searchEngine,
        IActionRunner actionRunner,
        IHistoryService history,
        IFavoritesService favorites,
        ISettingsService settings,
        INotificationService notification)
    {
        _searchEngine = searchEngine;
        _actionRunner = actionRunner;
        _history = history;
        _favorites = favorites;
        _settings = settings;
        _notification = notification;

        _fontSize = settings.Current.FontSize;
        settings.Changed += (_, _) =>
        {
            FontSize = settings.Current.FontSize;
            MaxResults = settings.Current.MaxResults;
        };
    }

    public int MaxResults { get; private set; } = 8;

    public void Reset()
    {
        _confirmed = false;
        if (!string.IsNullOrEmpty(Query))
        {
            Query = string.Empty;
        }
        else
        {
            _ = SearchAsync(string.Empty, CreateToken());
        }
    }

    private CancellationToken CreateToken()
    {
        _cts.Cancel();
        _cts = new CancellationTokenSource();
        return _cts.Token;
    }

    partial void OnQueryChanged(string value)
    {
        var token = CreateToken();
        _confirmed = false;
        _ = SearchAsync(value, token);
    }

    private async Task SearchAsync(string query, CancellationToken token)
    {
        try
        {
            await Task.Delay(40, token);

            var results = await Task.Run(() => _searchEngine.SearchAsync(query, token), token);
            token.ThrowIfCancellationRequested();

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (token.IsCancellationRequested)
                    return;

                ApplyResults(query, results);
            }, DispatcherPriority.Background);
        }
        catch (OperationCanceledException)
        {
            // newer keystroke superseded this search
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Search failed for query {Query}", query);
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                StatusText = "Erro interno ao pesquisar. Veja o log para detalhes.";
            });
        }
    }

    private void ApplyResults(string query, IReadOnlyList<SearchResult> results)
    {
        Results.Clear();
        foreach (var result in results)
            Results.Add(result);

        SelectedResult = Results.Count > 0 ? Results[0] : null;

        var failed = _searchEngine.FailedProviders;
        var warning = failed.Count > 0
            ? $" · {failed.Count} provedor(es) indisponível(is)"
            : string.Empty;

        if (string.IsNullOrWhiteSpace(query))
        {
            StatusText = Results.Count == 0
                ? "Comece a digitar para pesquisar..."
                : $"Favoritos e recentes{warning}";
        }
        else if (Results.Count == 0)
        {
            StatusText = $"Nenhum resultado para '{query}'";
        }
        else
        {
            StatusText = $"{Results.Count} resultado(s) · ↑↓ navegar · ↵ executar · ALT+D/CTRL+P favoritar{warning}";
        }
    }

    public void MoveSelection(int delta)
    {
        if (Results.Count == 0)
            return;

        var index = SelectedResult is null ? -1 : Results.IndexOf(SelectedResult);
        var next = Math.Clamp(index + delta, 0, Results.Count - 1);
        SelectedResult = Results[next];
    }

    public void MoveToFirst() => SelectedResult = Results.Count > 0 ? Results[0] : null;

    public async Task ExecuteSelectedAsync(bool runAsAdministrator)
    {
        var result = SelectedResult;
        if (result is null)
            return;

        if (result.RequiresConfirmation && !_confirmed)
        {
            _confirmed = true;
            result.Subtitle = "Pressione ENTER novamente para confirmar";
            StatusText = "Confirmação necessária — pressione ENTER para executar";
            return;
        }

        _confirmed = false;
        try
        {
            if (result.ExecuteAsync is not null)
                await result.ExecuteAsync();
            else
                await _actionRunner.ExecuteAsync(result, runAsAdministrator);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Execution failed for {Result}", result.Title);
            _notification.Show("Lunaris", $"Falha ao executar: {result.Title}");
        }

        _history.Record(Query, result);

        if (_settings.Current.CloseOnExecute)
            RequestClose?.Invoke();
    }

    public async Task CopySelectedAsync()
    {
        if (SelectedResult is null)
            return;

        await _actionRunner.CopyToClipboardAsync(SelectedResult.ExecuteHint ?? SelectedResult.Title);
    }

    public void ToggleFavoriteSelected()
    {
        if (SelectedResult is null)
            return;

        _favorites.Toggle(SelectedResult);
        _ = SearchAsync(Query, CreateToken());
    }
}