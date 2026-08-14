using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using Lunaris.Core.Interfaces;
using Lunaris.Core.Models;
using Lunaris.Infrastructure.Logging;

namespace Lunaris.Core.Services;

/// <summary>Executes search results by launching processes, opening urls or copying text.</summary>
public sealed class ActionRunner : IActionRunner
{
    private readonly INotificationService _notification;
    private readonly IClipboardMonitor? _clipboardMonitor;

    public ActionRunner(INotificationService notification, IClipboardMonitor? clipboardMonitor = null)
    {
        _notification = notification;
        _clipboardMonitor = clipboardMonitor;
    }

    public async Task ExecuteAsync(SearchResult result, bool runAsAdministrator)
    {
        try
        {
            switch (result.Kind)
            {
                case SearchResultKind.Calculation:
                    await CopyToClipboardAsync(result.ExecuteHint ?? result.Title);
                    break;

                case SearchResultKind.TextAction:
                case SearchResultKind.ClipboardItem:
                    await CopyToClipboardAsync(result.ExecuteHint ?? result.Title);
                    break;

                case SearchResultKind.App:
                case SearchResultKind.Command:
                case SearchResultKind.SystemTool:
                case SearchResultKind.File:
                case SearchResultKind.Folder:
                case SearchResultKind.Url:
                case SearchResultKind.Setting:
                case SearchResultKind.Favorite:
                case SearchResultKind.History:
                    LaunchProcess(result, runAsAdministrator && result.CanRunAsAdministrator);
                    break;
            }
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // User cancelled the UAC prompt.
            Log.Info("UAC prompt cancelled for {Result}", result.Title);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to execute {Result}", result.Title);
            _notification.Show("Lunaris", $"Não foi possível executar: {result.Title}");
        }

        await Task.CompletedTask;
    }

    private void LaunchProcess(SearchResult result, bool runAsAdmin)
    {
        var hint = result.ExecuteHint;
        if (string.IsNullOrEmpty(hint))
        {
            Log.Warn("Result has no execute hint: {Result}", result.Title);
            return;
        }

        if (result.Kind == SearchResultKind.Folder)
        {
            // Explorer opens directories reliably; Process.Start with a directory is flaky.
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{hint.TrimEnd('\\')}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = hint,
            UseShellExecute = true,
            CreateNoWindow = true,
        };

        if (!string.IsNullOrEmpty(result.ExecuteArguments))
            startInfo.Arguments = result.ExecuteArguments;

        if (runAsAdmin)
            startInfo.Verb = "runas";

        Process.Start(startInfo);
    }

    public async Task CopyToClipboardAsync(string text)
    {
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            _clipboardMonitor?.SuppressNext(text);
            System.Windows.Clipboard.SetText(text);
        });
    }
}