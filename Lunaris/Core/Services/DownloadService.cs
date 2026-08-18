using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using Lunaris.Core.Interfaces;
using Lunaris.Core.Models;
using Lunaris.Infrastructure.Logging;

namespace Lunaris.Core.Services;

/// <summary>
/// Download engine. <c>DownloadFileAsync</c> downloads any file with HttpClient;
/// <c>DownloadVideoAsync</c>/<c>DownloadAudioAsync</c> extract videos (mp4) and audio (mp3)
/// using yt-dlp + ffmpeg, auto-downloaded into the Lunaris tools folder on first use.
/// All downloads are saved to the user's Downloads folder.
/// </summary>
public sealed class DownloadService : IDownloadService
{
    private const string YtDlpUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
    private const string FfmpegUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";

    private const string VideoFormat = "-f \"bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]/best\" --merge-output-format mp4";
    private const string AudioFormat = "-x --audio-format mp3 --audio-quality 0";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(30) };

    private readonly INotificationService _notification;

    public DownloadService(INotificationService notification) => _notification = notification;

    public string DownloadsFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

    public string ToolsFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lunaris", "tools");

    public async Task<DownloadResult> DownloadFileAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(DownloadsFolder);
            var target = GetUniquePath(Path.Combine(DownloadsFolder, ResolveFileName(url)));

            using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var file = File.Create(target);
            await stream.CopyToAsync(file, cancellationToken);

            return new DownloadResult(true, $"Baixado: {Path.GetFileName(target)}");
        }
        catch (OperationCanceledException)
        {
            return new DownloadResult(false, "Download cancelado");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Generic download failed for {Url}", url);
            return new DownloadResult(false, $"Erro ao baixar: {ex.Message}");
        }
    }

    public Task<DownloadResult> DownloadVideoAsync(string url, CancellationToken cancellationToken = default)
        => RunYtDlpAsync(url, VideoFormat, "Vídeo baixado em Downloads", cancellationToken);

    public Task<DownloadResult> DownloadAudioAsync(string url, CancellationToken cancellationToken = default)
        => RunYtDlpAsync(url, AudioFormat, "MP3 baixado em Downloads", cancellationToken);

    private async Task<DownloadResult> RunYtDlpAsync(
        string url, string formatArgs, string successMessage, CancellationToken cancellationToken)
    {
        var toolError = await EnsureToolsAsync(cancellationToken);
        if (toolError is not null)
            return new DownloadResult(false, toolError);

        var ytDlp = Path.Combine(ToolsFolder, "yt-dlp.exe");
        var output = Path.Combine(DownloadsFolder, "%(title)s [%(id)s].%(ext)s");
        var arguments = $"--ffmpeg-location \"{ToolsFolder}\" -o \"{output}\" {formatArgs} \"{url}\"";

        var startInfo = new ProcessStartInfo
        {
            FileName = ytDlp,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        try
        {
            using var process = Process.Start(startInfo)!;
            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            var stderr = await stderrTask;
            if (process.ExitCode != 0)
            {
                Log.Error("yt-dlp failed ({Code}) for {Url}: {Err}", process.ExitCode, url, stderr);
                return new DownloadResult(false, $"Erro no download: {LastLine(stderr)}");
            }

            await stdoutTask;
            return new DownloadResult(true, successMessage);
        }
        catch (OperationCanceledException)
        {
            return new DownloadResult(false, "Download cancelado");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to run yt-dlp for {Url}", url);
            return new DownloadResult(false, $"Erro: {ex.Message}");
        }
    }

    private async Task<string?> EnsureToolsAsync(CancellationToken cancellationToken)
    {
        var ytDlp = Path.Combine(ToolsFolder, "yt-dlp.exe");
        var ffmpeg = Path.Combine(ToolsFolder, "ffmpeg.exe");

        if (File.Exists(ytDlp) && File.Exists(ffmpeg))
            return null;

        Directory.CreateDirectory(ToolsFolder);
        _notification.Show("Lunaris", "Preparando ferramentas de download (yt-dlp/ffmpeg)...");

        try
        {
            if (!File.Exists(ytDlp))
                await DownloadToFileAsync(YtDlpUrl, ytDlp, cancellationToken);

            if (!File.Exists(ffmpeg))
            {
                var zipPath = Path.Combine(ToolsFolder, "ffmpeg.zip");
                await DownloadToFileAsync(FfmpegUrl, zipPath, cancellationToken);

                using var archive = ZipFile.OpenRead(zipPath);
                var entry = archive.Entries.First(e =>
                    e.FullName.EndsWith("/bin/ffmpeg.exe", StringComparison.OrdinalIgnoreCase));
                entry.ExtractToFile(ffmpeg, overwrite: true);
                File.Delete(zipPath);
            }

            return null;
        }
        catch (OperationCanceledException)
        {
            return "Instalação das ferramentas cancelada";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to prepare yt-dlp/ffmpeg tools");
            return $"Não foi possível baixar as ferramentas: {ex.Message}";
        }
    }

    private static async Task DownloadToFileAsync(string url, string path, CancellationToken cancellationToken)
    {
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var file = File.Create(path);
        await stream.CopyToAsync(file, cancellationToken);
    }

    private static string ResolveFileName(string url)
    {
        try
        {
            var segment = new Uri(url).Segments.LastOrDefault()?.Trim('/');
            if (!string.IsNullOrWhiteSpace(segment) && !string.IsNullOrEmpty(Path.GetExtension(segment)))
                return Uri.UnescapeDataString(segment);
        }
        catch
        {
            // fall through to timestamped name
        }

        return $"download_{DateTime.Now:yyyyMMdd_HHmmss}";
    }

    private static string GetUniquePath(string path)
    {
        if (!File.Exists(path))
            return path;

        var dir = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);

        for (var i = 1; ; i++)
        {
            var candidate = Path.Combine(dir, $"{name} ({i}){ext}");
            if (!File.Exists(candidate))
                return candidate;
        }
    }

    private static string LastLine(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return lines.Length == 0 ? "erro desconhecido" : lines[^1].Trim();
    }
}
