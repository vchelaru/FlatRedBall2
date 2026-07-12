using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;

namespace AnimationEditor.Core.Demo;

/// <summary>
/// Named demos for visual / history verification (DocScreenshots, Core.Tests).
/// Add new demos here — do not fork drive scripts per host, and do not call from
/// shipping Browser/Desktop <c>App</c> entry points.
/// </summary>
internal static class FeatureDemos
{
    public const string UndoLabels = "undo-labels";

    /// <summary>
    /// Runs <paramref name="demoName"/> if registered. <paramref name="textureName"/> is the
    /// relative texture used when the demo adds frames (desktop fixtures vs browser sample).
    /// </summary>
    public static bool TryRun(
        string demoName,
        AppCommands commands,
        UndoManager undoManager,
        IApplicationEvents events,
        string textureName)
    {
        if (demoName.Equals(UndoLabels, StringComparison.OrdinalIgnoreCase))
        {
            UndoLabelsDemo.Run(commands, undoManager, events, textureName);
            return true;
        }

        return false;
    }
}
