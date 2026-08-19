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
/// Uses atomic writes (download to temp file, then rename) to prevent file locking issues
/// with the file indexer and antivirus real-time scanning.
/// </summary>
public sealed class DownloadService : IDownloadService
{
    private const string YtDlpUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
    private const string FfmpegUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";

    // Downloads the best quality available regardless of codec, merges into MKV (any codec fits),
    // then re-encodes to MP4 with ffmpeg as H.264 (Main) + AAC 128k + faststart — a combination
    // accepted by virtually every player and site.
    private const string VideoFormat = "-f \"bv*+ba/b\" --merge-output-format mkv --recode-video mp4 --postprocessor-args \"VideoConvertor:-c:v libx264 -profile:v main -pix_fmt yuv420p -c:a aac -b:a 128k -movflags +faststart\"";
    private const string AudioFormat = "-x --audio-format mp3 --audio-quality 0";

    private const string TempExtension = ".lunaris-dl";

    private static readonly HttpClient Http;

    static DownloadService()
    {
        Http = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5,
        });
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
        Http.DefaultRequestHeaders.Accept.ParseAdd("*/*");
        Http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
        Http.Timeout = TimeSpan.FromMinutes(30);
    }

    private readonly INotificationService _notification;

    public DownloadService(INotificationService notification) => _notification = notification;

    public string DownloadsFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

    /// <summary>Bundled tools shipped with the app (next to Lunaris.exe).</summary>
    public string BundledToolsFolder { get; } = Path.Combine(
        AppContext.BaseDirectory, "tools");

    /// <summary>Fallback tools folder in AppData (used when tools are downloaded at runtime).</summary>
    public string ToolsFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lunaris", "tools");

    public async Task<DownloadResult> DownloadFileAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(DownloadsFolder);
            var target = GetUniquePath(Path.Combine(DownloadsFolder, ResolveFileName(url)));
            var tempPath = target + TempExtension;

            using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var file = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.Read, 81920, useAsync: true))
            {
                await stream.CopyToAsync(file, cancellationToken);
                await file.FlushAsync(cancellationToken);
            }

            SafeRename(tempPath, target);
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

        var toolsDir = ResolveToolsFolder();
        var ytDlp = Path.Combine(toolsDir, "yt-dlp.exe");
        var output = Path.Combine(DownloadsFolder, "%(title)s [%(id)s].%(ext)s");
        var arguments = $"--ffmpeg-location \"{toolsDir}\" -o \"{output}\" {formatArgs} \"{url}\"";

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
        var toolsDir = ResolveToolsFolder();
        var ytDlp = Path.Combine(toolsDir, "yt-dlp.exe");
        var ffmpeg = Path.Combine(toolsDir, "ffmpeg.exe");

        if (File.Exists(ytDlp) && File.Exists(ffmpeg))
            return null;

        Directory.CreateDirectory(toolsDir);
        _notification.Show("Lunaris", "Preparando ferramentas de download (yt-dlp/ffmpeg)...");

        try
        {
            if (!File.Exists(ytDlp))
                await DownloadToolAsync(YtDlpUrl, ytDlp, cancellationToken);

            if (!File.Exists(ffmpeg))
            {
                var zipPath = Path.Combine(toolsDir, "ffmpeg.zip");
                await DownloadToolAsync(FfmpegUrl, zipPath, cancellationToken);

                using var archive = ZipFile.OpenRead(zipPath);
                var entry = archive.Entries.First(e =>
                    e.FullName.EndsWith("/bin/ffmpeg.exe", StringComparison.OrdinalIgnoreCase));
                entry.ExtractToFile(ffmpeg, overwrite: true);

                TryDelete(zipPath);
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

    /// <summary>
    /// Returns the first folder that contains both yt-dlp.exe and ffmpeg.exe,
    /// preferring the bundled tools shipped with the app.
    /// </summary>
    private string ResolveToolsFolder()
    {
        if (File.Exists(Path.Combine(BundledToolsFolder, "yt-dlp.exe"))
            && File.Exists(Path.Combine(BundledToolsFolder, "ffmpeg.exe")))
        {
            return BundledToolsFolder;
        }

        return ToolsFolder;
    }

    private static async Task DownloadToolAsync(string url, string path, CancellationToken cancellationToken)
    {
        var tempPath = path + TempExtension;

        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var file = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.Read, 81920, useAsync: true))
        {
            await stream.CopyToAsync(file, cancellationToken);
            await file.FlushAsync(cancellationToken);
        }

        SafeRename(tempPath, path);
    }

    private static void SafeRename(string tempPath, string finalPath)
    {
        if (!File.Exists(tempPath))
            return;

        try
        {
            if (File.Exists(finalPath))
                File.Delete(finalPath);

            File.Move(tempPath, finalPath);
        }
        catch (IOException)
        {
            try
            {
                File.Copy(tempPath, finalPath, overwrite: true);
                File.Delete(tempPath);
            }
            catch (Exception ex)
            {
                Log.Warn("Failed to rename temp file {Temp} to {Final}: {Error}", tempPath, finalPath, ex.Message);
                throw;
            }
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* best effort cleanup */ }
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
