using System;

namespace AnimationEditor.App.Services;

/// <summary>Pure classification of what the other AnimationEditor process looks like to the OS.</summary>
internal static class PrimaryInstanceHangClassifier
{
    /// <summary>How long a primary may run without a visible window before that is treated as a problem.</summary>
    public static readonly TimeSpan StartupGrace = TimeSpan.FromSeconds(30);

    public static PrimaryInstanceHangResult Classify(bool windowFound, bool windowHung, TimeSpan? primaryAge)
    {
        if (windowFound)
            return windowHung ? PrimaryInstanceHangResult.Hung : PrimaryInstanceHangResult.BusyNotHung;

        // No window yet is normal while the primary boots; past the grace period it means the
        // process outlived its windows. An unknown age is treated as young: never offer to kill on a guess.
        return primaryAge >= StartupGrace ? PrimaryInstanceHangResult.NoWindow : PrimaryInstanceHangResult.BusyNotHung;
    }
}
