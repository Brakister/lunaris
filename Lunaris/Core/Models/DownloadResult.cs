namespace Lunaris.Core.Models;

/// <summary>Outcome of a download operation.</summary>
public sealed record DownloadResult(bool Success, string Message);
