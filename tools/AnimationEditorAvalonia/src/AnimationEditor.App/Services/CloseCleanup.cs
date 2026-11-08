using System;

namespace AnimationEditor.App.Services;

/// <summary>
/// Runs a window's shutdown steps so one failing step cannot skip the others or escape the Closed event
/// (an exception there skips Avalonia's lifetime handler and crashes the process on Windows).
/// </summary>
internal static class CloseCleanup
{
    /// <summary>Runs every step in order; a step that throws is logged and the rest still run.</summary>
    public static void Run(params Action[] steps) => Run(steps, ex => CrashLogging.LogCrash("Window.Closed cleanup", ex));

    internal static void Run(Action[] steps, Action<Exception> onError)
    {
        foreach (var step in steps)
        {
            try
            {
                step();
            }
            catch (Exception ex)
            {
                onError(ex);
            }
        }
    }
}
