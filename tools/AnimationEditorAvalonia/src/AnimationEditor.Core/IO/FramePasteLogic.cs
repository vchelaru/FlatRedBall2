using FlatRedBall2.Animation.Content;
using System.Collections.Generic;

namespace AnimationEditor.Core.IO;

/// <summary>
/// Sanitizes pasted frames for insertion into a chain.
/// Kept separate from clipboard serialization so the naming rule is unit-testable.
/// </summary>
public static class FramePasteLogic
{
    /// <summary>
    /// Strips stale auto-generated names from pasted frames that don't have a
    /// user-defined name, so they display dynamic positional labels ("Frame N")
    /// after paste. Custom-named frames (<see cref="AnimationFrameSave.HasCustomName"/>
    /// = <c>true</c>) are left untouched — their name is sticky.
    /// </summary>
    public static void AssignUniqueNames(
        IList<AnimationFrameSave> existingFrames,
        IReadOnlyList<AnimationFrameSave> pastedFrames)
    {
        foreach (var frame in pastedFrames)
        {
            if (!frame.HasCustomName)
                frame.Name = string.Empty;
        }
    }
}
