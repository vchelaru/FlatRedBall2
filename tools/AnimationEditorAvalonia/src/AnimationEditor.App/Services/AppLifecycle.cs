using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AnimationEditor.Core.Logging;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace AnimationEditor.App.Services;

/// <summary>
/// Records how a process starts, stops and fails to stop: the unclean-shutdown marker, the shutdown signals, and the
/// watchdog that explains a process that outlives them. The decisions live in Core (<see cref="CleanShutdownMarker"/>,
/// <see cref="LingerWatchdog"/>); this is the wiring to the log and to Avalonia.
/// </summary>
internal static class AppLifecycle
{
    /// <summary>How long after a shutdown signal the process may live before the watchdog reports on it.</summary>
    private static readonly TimeSpan LingerDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan LingerRepeat = TimeSpan.FromSeconds(60);
    private const int LingerReports = 5;

    private static readonly LingerWatchdog Watchdog =
        new(LingerDelay, LingerRepeat, LingerReports, text => AppLog.Warn(text), DescribeProcess);

    private static CleanShutdownMarker? _marker;

    /// <summary>
    /// Call once this process owns the single-instance mutex. Logs whether the previous primary exited cleanly and writes
    /// this run's marker. A secondary launch must not call it, or it would clobber the primary's marker.
    /// </summary>
    public static void OwnerStarted()
    {
        if (AppLog.FilePath is not { } logPath)
            return;

        _marker = new CleanShutdownMarker(Path.Combine(Path.GetDirectoryName(logPath) ?? "", "running.marker"));
        var previous = _marker.Begin(Environment.ProcessId, AppVersion.Current, DateTimeOffset.Now);
        if (previous == null)
            AppLog.Info("Previous run ended cleanly (no leftover marker).");
        else if (previous.ExitedForUpdate)
            AppLog.Info($"Previous run exited to install an update: {previous.Describe()}");
        else
            AppLog.Warn($"Previous run did not shut down cleanly (killed, crashed or hung): {previous.Describe()}");
    }

    /// <summary>A step on the way out (last window closed, lifetime shutdown, update restart): logs it and starts the linger watchdog.</summary>
    public static void ShutdownSignal(string reason)
    {
        AppLog.Info(reason);
        Watchdog.Arm(reason);
    }

    /// <summary>Main is about to return. A clean return removes the marker; the watchdog still covers foreground threads that keep the process alive.</summary>
    public static void MainReturning(int exitCode)
    {
        ShutdownSignal($"Main returning, Avalonia lifetime exit code {exitCode}");
        _marker?.MarkClean();
    }

    /// <summary>About to hand over to Velopack, which will exit this process to install the update; the marker records that this exit is expected.</summary>
    public static void UpdateRestartStarting()
    {
        _marker?.SetPhase(CleanShutdownMarker.UpdatingPhase);
        ShutdownSignal("Update restart requested: this process should exit now");
    }

    /// <summary>The update restart did not take: the process is staying, so its marker goes back to a plain running state.</summary>
    public static void UpdateRestartFailed() => _marker?.SetPhase(CleanShutdownMarker.RunningPhase);

    private static string DescribeProcess()
    {
        var sb = new StringBuilder(ProcessSnapshot.Capture()).AppendLine();
        sb.Append(DescribeWindows());
        return sb.ToString();
    }

    /// <summary>
    /// Lists the windows Avalonia still holds, from the UI thread. A UI thread that does not answer is itself the finding:
    /// it is hung (a blocked handler, a modal dialog loop) or already gone.
    /// </summary>
    private static string DescribeWindows()
    {
        try
        {
            var op = Dispatcher.UIThread.InvokeAsync(() =>
            {
                var windows = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Windows;
                return windows is null || windows.Count == 0
                    ? "  windows: none"
                    : "  windows: " + string.Join("; ", windows.Select(w =>
                        $"{w.GetType().Name} \"{w.Title}\" visible={w.IsVisible} state={w.WindowState} owned={w.Owner != null}"));
            });
            return op.GetTask().Wait(TimeSpan.FromSeconds(3))
                ? op.GetTask().Result
                : "  ui thread: did not answer within 3s (hung, or already shut down)";
        }
        catch (Exception ex)
        {
            return $"  ui thread: probe failed ({ex.GetType().Name}: {ex.Message})";
        }
    }
}
