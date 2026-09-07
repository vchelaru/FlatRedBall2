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

    public static PrimaryInstanceHangResult Check()
    {
        if (!OperatingSystem.IsWindows())
        {
            // No IsHungAppWindow equivalent off Windows. A wedged pipe listener means the whole
            // process is stuck (not just its UI thread), so treat unreachable as sufficient
            // evidence on its own.
            return PrimaryInstanceHangResult.Hung;
        }

        var other = FindOtherInstanceProcess();
        var handle = other?.MainWindowHandle ?? IntPtr.Zero;
        if (handle != IntPtr.Zero && IsHungAppWindow(handle))
            return PrimaryInstanceHangResult.Hung;

        // Either no other process was found, or its window isn't reporting hung — don't offer
        // to kill a process that might just be busy.
        return PrimaryInstanceHangResult.BusyNotHung;
    }

    /// <summary>Kills the other Animation Editor process and waits (best-effort) for it to exit.
    /// Failures are swallowed — the caller re-checks mutex ownership afterward and gives up
    /// cleanly if it still isn't available.</summary>
    public static void KillOtherInstance()
    {
        using var other = FindOtherInstanceProcess();
        if (other == null) return;

        try
        {
            other.Kill();
            other.WaitForExit(5000);
        }
        catch
        {
            // Already exited, access denied, etc. — nothing more we can do here.
        }
    }

    /// <summary>Finds the other running Animation Editor process (excluding this one). Only one
    /// other should exist under the single-instance mutex.</summary>
    private static Process? FindOtherInstanceProcess()
    {
        var current = Process.GetCurrentProcess();
        return Process.GetProcessesByName(current.ProcessName)
            .FirstOrDefault(p => p.Id != current.Id);
    }
}
