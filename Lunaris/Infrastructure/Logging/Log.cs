using Serilog;

namespace Lunaris.Infrastructure.Logging;

/// <summary>Static Serilog facade used across the app.</summary>
public static class Log
{
    public static ILogger Instance { get; private set; } = Serilog.Log.Logger;

    public static void Initialize(ILogger logger) => Instance = logger;

    public static void Debug(string message, params object?[] args) => Instance.Debug(message, args);

    public static void Info(string message, params object?[] args) => Instance.Information(message, args);

    public static void Warn(string message, params object?[] args) => Instance.Warning(message, args);

    public static void Error(Exception? exception, string message, params object?[] args) => Instance.Error(exception, message, args);

    public static void Error(string message, params object?[] args) => Instance.Error(message, args);
}
