using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace AnimationEditor.App.Services;

/// <summary>
/// OS-specific wiring that classifies why the primary instance's IPC pipe was unreachable, and
/// kills it when the user confirms a restart. Thin P/Invoke + <see cref="Process"/> lookup layer
/// around the pure <see cref="SingleInstanceRecoveryDecision"/> — not unit tested, since there is
/// no way to manufacture a real hung window (or a second real process under the same mutex) in a
/// test. See PR for issue #1049 for the full TDD-exception rationale.
/// </summary>
internal static class PrimaryInstanceHangChecker
{
    // Task Manager's own "Not Responding" check — a window that hasn't pumped a message in
    // ~5 seconds. This is the strong signal the pipe-connect timeout alone can't provide, since
    // the pipe listener runs on a background thread independent of the UI thread (see
    // SingleInstanceServer.SendToRunningInstanceAsync's doc comment).
    [DllImport("user32.dll")]
    private static extern bool IsHungAppWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private const int SW_RESTORE = 9;

    /// <param name="reachedPrimary">Whether the pipe hand-off succeeded. Off Windows there is no
    /// window query, so an unreachable pipe alone counts as hung while a reachable one counts as fine.</param>
    public static PrimaryInstanceHangResult Check(bool reachedPrimary = false)
    {
        if (!OperatingSystem.IsWindows())
        {
            // No IsHungAppWindow equivalent off Windows. A wedged pipe listener means the whole
            // process is stuck (not just its UI thread), so treat unreachable as sufficient
            // evidence on its own.
            return reachedPrimary ? PrimaryInstanceHangResult.BusyNotHung : PrimaryInstanceHangResult.Hung;
        }

        using var other = FindOtherInstanceProcess();
        var handle = other?.MainWindowHandle ?? IntPtr.Zero;
        bool windowFound = handle != IntPtr.Zero;

        // No process found at all (it exited between the hand-off and now) is not evidence of
        // anything: stay on the quiet, no-kill side.
        if (other == null)
        {
            AppLog.Info("Other instance: no such process found (exited meanwhile) -> BusyNotHung.");
            return PrimaryInstanceHangResult.BusyNotHung;
        }

        bool windowHung = windowFound && IsHungAppWindow(handle);
        var age = AgeOf(other);
        var result = PrimaryInstanceHangClassifier.Classify(windowFound, windowHung, age);
        AppLog.Info($"Other instance: pid={other.Id} windowFound={windowFound} windowHung={windowHung} " +
                    $"age={(age is { } a ? $"{a.TotalSeconds:0}s" : "unknown")} -> {result}.");
        return result;
    }

    /// <summary>Restores (if minimized) and raises the other instance's window. Windows only: the
    /// stale primary may be an older build whose pipe handler ignores a plain re-launch, so the
    /// launcher does it at the OS level rather than relying on the primary to.</summary>
    public static void ActivatePrimaryWindow()
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            using var other = FindOtherInstanceProcess();
            var handle = other?.MainWindowHandle ?? IntPtr.Zero;
            if (handle == IntPtr.Zero) return;
            if (IsIconic(handle)) ShowWindow(handle, SW_RESTORE);
            SetForegroundWindow(handle);
        }
        catch
        {
            // Best effort: the user can still click the taskbar button.
        }
    }

    private static TimeSpan? AgeOf(Process process)
    {
        try { return DateTime.Now - process.StartTime; }
        catch { return null; }
    }

    /// <summary>Kills the other AnimationEditor process and waits (best-effort) for it to exit.
    /// Failures are swallowed — the caller re-checks mutex ownership afterward and gives up
    /// cleanly if it still isn't available.</summary>
    public static void KillOtherInstance()
    {
        using var other = FindOtherInstanceProcess();
        if (other == null) return;

        try
        {
            AppLog.Info($"Killing other instance pid={other.Id}.");
            other.Kill();
            bool exited = other.WaitForExit(5000);
            AppLog.Info($"Other instance {(exited ? "exited" : "STILL RUNNING 5s after Kill")}.");
        }
        catch (Exception ex)
        {
            // Already exited, access denied, etc. — nothing more we can do here.
            AppLog.Warn("Killing the other instance failed.", ex);
        }
    }

    /// <summary>Finds the other running AnimationEditor process (excluding this one). Only one
    /// other should exist under the single-instance mutex.</summary>
    private static Process? FindOtherInstanceProcess()
    {
        var current = Process.GetCurrentProcess();
        return Process.GetProcessesByName(current.ProcessName)
            .FirstOrDefault(p => p.Id != current.Id);
    }
}
