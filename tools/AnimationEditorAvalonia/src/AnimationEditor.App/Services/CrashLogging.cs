using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using AnimationEditor.Core.Logging;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;

namespace AnimationEditor.App.Services;

/// <summary>
/// Opens <c>log.txt</c> and installs the process-wide unhandled-exception handlers that record crashes to it before the
/// process terminates. Handlers record and re-let the crash propagate; they never swallow it. Everything else writes
/// through <see cref="AppLog"/>.
/// </summary>
internal static class CrashLogging
{
    /// <summary>A log past this size is moved to <c>log.old.txt</c> at the next launch.</summary>
    internal const long MaxLogBytes = 2 * 1024 * 1024;

    /// <summary>Absolute path of the live log file, or null until <see cref="Install"/> runs.</summary>
    public static string? LogFilePath => AppLog.FilePath;

    /// <summary>
    /// Resolves the log path (next to the exe, or <c>%APPDATA%</c> when that directory isn't writable or Velopack
    /// replaces it on update), rotates an oversized log, writes the session header, and subscribes the background-thread,
    /// finalizer and process-exit handlers. Call once, first thing in <c>Main</c>, before Avalonia starts.
    /// </summary>
    public static void Install(string baseDirectory, string applicationDataRoot, string[] args)
    {
        var path = LogFileLocation.Resolve(baseDirectory, applicationDataRoot, IsDirectoryWritable);
        LogRotation.RotateIfLarge(path.FullPath, MaxLogBytes);

        var version = AppVersion.Current;
        AppLog.Initialize(
            new FileLogger(path.FullPath, context: SessionHeader.Context(Environment.ProcessId, version)),
            path.FullPath);
        AppLog.Info(SessionHeader.Format(version, RuntimeInformation.OSDescription, args, Environment.ProcessPath, path.FullPath));

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            LogCrash("AppDomain.UnhandledException", e.ExceptionObject as Exception, e.IsTerminating,
                e.ExceptionObject as Exception == null ? e.ExceptionObject?.ToString() : null);
        TaskScheduler.UnobservedTaskException += (_, e) =>
            LogCrash("TaskScheduler.UnobservedTaskException", e.Exception);
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            AppLog.Info($"ProcessExit (exit code {Environment.ExitCode})");
    }

    /// <summary>
    /// Subscribes the UI-thread crash handler. Call after the Avalonia dispatcher exists
    /// (from <c>App.OnFrameworkInitializationCompleted</c>).
    /// </summary>
    public static void InstallDispatcherHandler() =>
        Dispatcher.UIThread.UnhandledException += (_, e) =>
            LogCrash("Dispatcher.UnhandledException", e.Exception);

    /// <summary>Records a crash through the installed log; no-op before <see cref="Install"/>.</summary>
    public static void LogCrash(string source, Exception? exception, bool? terminating = null, string? detail = null)
    {
        var message = Describe(source, terminating, detail);
        if (exception == null)
            AppLog.Error(message);
        else
            AppLog.Error(message, exception);
    }

    /// <summary>
    /// Pure wiring from an exception to a log line. Exposed so the handler behavior can be
    /// exercised in tests with an injected sink and a synthetic exception.
    /// </summary>
    internal static void Log(ILogger logger, string source, Exception? exception, bool? terminating = null) =>
        logger.LogError(exception, "{Message}", Describe(source, terminating, null));

    private static string Describe(string source, bool? terminating, string? detail) =>
        $"Unhandled exception ({source})" +
        (terminating is { } t ? $" terminating={t}" : "") +
        (detail != null ? $" non-exception object: {detail}" : "");

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
