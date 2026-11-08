using System;
using Microsoft.Extensions.Logging;

namespace AnimationEditor.App.Services;

/// <summary>
/// The process-wide diagnostic log (<c>log.txt</c>). A no-op until <see cref="CrashLogging.Install"/> runs, so code under
/// test can call it freely. Every call is safe from any thread and never throws.
/// </summary>
internal static class AppLog
{
    private static ILogger? _logger;

    /// <summary>Absolute path of the live log file, or null until the log is installed.</summary>
    public static string? FilePath { get; private set; }

    internal static void Initialize(ILogger logger, string? filePath)
    {
        _logger = logger;
        FilePath = filePath;
    }

    public static void Info(string message) => Write(LogLevel.Information, message, null);

    public static void Warn(string message, Exception? exception = null) => Write(LogLevel.Warning, message, exception);

    public static void Error(string message, Exception? exception = null) => Write(LogLevel.Error, message, exception);

    private static void Write(LogLevel level, string message, Exception? exception)
    {
        try
        {
            _logger?.Log(level, default, message, exception, static (text, _) => text);
        }
        catch
        {
            // Logging must never crash the app.
        }
    }
}
