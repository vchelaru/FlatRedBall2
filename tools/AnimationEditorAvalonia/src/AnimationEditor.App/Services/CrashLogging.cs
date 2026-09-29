using System;
using System.IO;
using System.Threading.Tasks;
using AnimationEditor.Core.Logging;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;

namespace AnimationEditor.App.Services;

/// <summary>
/// Installs process-wide unhandled-exception handlers that record crashes to <c>log.txt</c>
/// before the process terminates. Crash-only for now, but built on <see cref="ILogger"/> so
/// future warning/info/debug logging can flow through the same sink. Handlers record and
/// re-let the crash propagate — they never swallow it.
/// </summary>
internal static class CrashLogging
{
    private static FileLogger? _logger;

    /// <summary>Absolute path of the live log file, or null until <see cref="Install"/> runs.</summary>
    public static string? LogFilePath { get; private set; }

    /// <summary>
    /// Resolves the log path (next to the exe, falling back to <c>%APPDATA%</c> when that
    /// directory isn't writable), writes a session header, and subscribes the background-thread
    /// and finalizer crash handlers. Call once, early in <c>Main</c>, before Avalonia starts.
    /// </summary>
    public static void Install(string baseDirectory, string applicationDataRoot)
    {
        var path = LogFileLocation.Resolve(baseDirectory, applicationDataRoot, IsDirectoryWritable);
        LogFilePath = path.FullPath;
        _logger = new FileLogger(path.FullPath);
        _logger.LogInformation("=== AnimationEditor session started ===");

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            LogCrash("AppDomain.UnhandledException", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
            LogCrash("TaskScheduler.UnobservedTaskException", e.Exception);
    }

    /// <summary>
    /// Subscribes the UI-thread crash handler. Call after the Avalonia dispatcher exists
    /// (from <c>App.OnFrameworkInitializationCompleted</c>).
    /// </summary>
    public static void InstallDispatcherHandler() =>
        Dispatcher.UIThread.UnhandledException += (_, e) =>
            LogCrash("Dispatcher.UnhandledException", e.Exception);

    /// <summary>Records a crash through the installed logger; no-op before <see cref="Install"/>.</summary>
    public static void LogCrash(string source, Exception? exception)
    {
        if (_logger != null)
            Log(_logger, source, exception);
    }

    /// <summary>
    /// Pure wiring from an exception to a log line. Exposed so the handler behavior can be
    /// exercised in tests with an injected sink and a synthetic exception.
    /// </summary>
    internal static void Log(ILogger logger, string source, Exception? exception) =>
        logger.LogError(exception, "Unhandled exception ({Source})", source);

    private static bool IsDirectoryWritable(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var probe = Path.Combine(directory, $".write-probe-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
