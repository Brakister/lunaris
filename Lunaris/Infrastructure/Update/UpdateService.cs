using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using Lunaris.Core.Interfaces;
using Lunaris.Infrastructure.Logging;

namespace Lunaris.Infrastructure.Update;

/// <summary>Checks the GitHub releases API for newer versions and installs them.</summary>
public sealed class UpdateService : IUpdateService
{
    private const string ReleasesUrl = "https://api.github.com/repos/Brakister/lunaris/releases/latest";

    private readonly HttpClient _http;

    public UpdateService()
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Lunaris", "1.0"));
        _http.Timeout = TimeSpan.FromSeconds(15);
    }

    public async Task<UpdateInfo?> CheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.GetAsync(ReleasesUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                Log.Info("Update check: GitHub returned HTTP {Status}", (int)response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;

            var tag = root.TryGetProperty("tag_name", out var tagEl) ? tagEl.GetString() : null;
            var releaseVersion = ParseVersion(tag);
            if (releaseVersion is null)
            {
                Log.Info("Update check: could not parse release tag '{Tag}'", tag);
                return null;
            }

            if (CompareVersions(releaseVersion, CurrentVersion()) <= 0)
                return null;

            string? downloadUrl = null;
            if (root.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
                    if (string.IsNullOrEmpty(name) || !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        continue;

                    downloadUrl = asset.TryGetProperty("browser_download_url", out var urlEl) ? urlEl.GetString() : null;
                    if (!string.IsNullOrEmpty(downloadUrl))
                        break;
                }
            }

            if (string.IsNullOrEmpty(downloadUrl))
            {
                Log.Info("Update check: latest release has no installer asset");
                return null;
            }

            var notes = root.TryGetProperty("body", out var bodyEl) ? bodyEl.GetString() : null;
            return new UpdateInfo { Version = releaseVersion, DownloadUrl = downloadUrl, Notes = notes };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Warn("Update check failed: {Message}", ex.Message);
            return null;
        }
    }

    public async Task<string> DownloadAsync(string url, string destinationDirectory, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destinationDirectory);

        var fileName = Path.GetFileName(new Uri(url).AbsolutePath);
        if (string.IsNullOrEmpty(fileName))
            fileName = "Lunaris-Setup.exe";

        var destinationPath = Path.Combine(destinationDirectory, fileName);

        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var target = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await source.CopyToAsync(target, cancellationToken);

        Log.Info("Update downloaded to {Path}", destinationPath);
        return destinationPath;
    }

    public bool LaunchInstaller(string installerPath)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = installerPath,
                UseShellExecute = true,
            });
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not launch installer {Path}", installerPath);
            return false;
        }
    }

    private static string CurrentVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version is null ? "1.2.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }

    private static string? ParseVersion(string? tag)
    {
        if (string.IsNullOrEmpty(tag))
            return null;

        var version = tag.StartsWith('v') || tag.StartsWith('V') ? tag[1..] : tag;
        var dash = version.IndexOf('-');
        if (dash >= 0)
            version = version[..dash];

        return string.IsNullOrEmpty(version) ? null : version;
    }

    private static int CompareVersions(string left, string right)
    {
        var l = left.Split('.');
        var r = right.Split('.');
        var length = Math.Max(l.Length, r.Length);

        for (var i = 0; i < length; i++)
        {
            var lv = i < l.Length && int.TryParse(l[i], out var a) ? a : 0;
            var rv = i < r.Length && int.TryParse(r[i], out var b) ? b : 0;
            if (lv != rv)
                return lv.CompareTo(rv);
        }

        return 0;
    }
}