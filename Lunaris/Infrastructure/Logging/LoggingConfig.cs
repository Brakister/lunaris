using System.IO;
using Serilog;

namespace Lunaris.Infrastructure.Logging;

public static class LoggingConfig
{
    public static string LogDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Lunaris",
        "logs");

    public static ILogger CreateLogger()
    {
        var dir = LogDirectory;
        try
        {
            Directory.CreateDirectory(dir);
        }
        catch
        {
            dir = Path.Combine(Path.GetTempPath(), "Lunaris", "logs");
            Directory.CreateDirectory(dir);
        }

        return new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.File(
                path: Path.Combine(dir, "lunaris-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }
}