using System;

namespace AnimationEditor.App.Services;

/// <summary>
/// Why the primary instance's IPC pipe was unreachable, as classified by an OS-specific check
/// (<c>PrimaryInstanceHangChecker</c>). Off Windows there is no window-responsiveness API, so an
/// unreachable pipe is itself treated as sufficient evidence of <see cref="Hung"/>.
/// </summary>
internal enum PrimaryInstanceHangResult
{
    /// <summary>Windows only: the pipe was unreachable, but the other process's main window is
    /// still responding — likely just busy, not frozen. Never produced off Windows.</summary>
    BusyNotHung,

    /// <summary>The primary is confirmed (Windows) or assumed (macOS/Linux) to be frozen.</summary>
    Hung,

    /// <summary>Windows only: the primary process has been running past its startup grace period
    /// and still has no visible window, so there is nothing for the user to see or lose.</summary>
    NoWindow,
}

/// <summary>What a second launch should do once it knows whether it reached the primary.</summary>
internal enum SingleInstanceRecoveryAction
{
    /// <summary>The primary is alive, reachable and its window responds — exit quietly.</summary>
    SilentExit,

    /// <summary>Windows only: tell the user the primary looks busy and to try again. No restart
    /// offered — killing a merely-busy process would lose their work.</summary>
    ShowBusyMessage,

    /// <summary>The primary is frozen — offer to kill it and take over as the new instance.</summary>
    OfferRestart,

    /// <summary>The primary process is alive but has no window at all — offer to replace it.</summary>
    OfferRestartNoWindow,
}

/// <summary>
/// Pure decision table for a second launch: given whether it reached the primary over the IPC
/// pipe and what the OS says about the primary's window, decide what to show the user. Deliberately free of
/// <c>Process</c>/P-Invoke/Avalonia so this is unit-testable without a real hung process.
/// </summary>
internal static class SingleInstanceRecoveryDecision
{
    public static SingleInstanceRecoveryAction Decide(bool reachedPrimary, PrimaryInstanceHangResult hangResult)
    {
        // A reachable pipe only proves the primary's background listener is alive (see
        // SingleInstanceServer.SendToRunningInstanceAsync), so a frozen or window-less primary
        // still gets the user's attention instead of a silent exit that looks like nothing happened.
        return hangResult switch
        {
            PrimaryInstanceHangResult.Hung => SingleInstanceRecoveryAction.OfferRestart,
            PrimaryInstanceHangResult.NoWindow => SingleInstanceRecoveryAction.OfferRestartNoWindow,
            PrimaryInstanceHangResult.BusyNotHung => reachedPrimary
                ? SingleInstanceRecoveryAction.SilentExit
                : SingleInstanceRecoveryAction.ShowBusyMessage,
            _ => throw new ArgumentOutOfRangeException(nameof(hangResult), hangResult, null),
        };
    }
}
