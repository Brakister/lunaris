using Lunaris.Core.Models;

namespace Lunaris.Core.Interfaces;

/// <summary>
/// Downloads files, videos (mp4) and audio (mp3). Videos/audio are extracted with
/// yt-dlp + ffmpeg, which are auto-installed in the Lunaris tools folder on first use.
/// </summary>
public interface IDownloadService
{
    string DownloadsFolder { get; }

    Task<DownloadResult> DownloadFileAsync(string url, CancellationToken cancellationToken = default);

    Task<DownloadResult> DownloadVideoAsync(string url, CancellationToken cancellationToken = default);

    Task<DownloadResult> DownloadAudioAsync(string url, CancellationToken cancellationToken = default);
}
