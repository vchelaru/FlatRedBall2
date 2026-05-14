using FlatRedBall2.Animation.Content;
using System.Collections.Generic;

namespace AnimationEditor.Core.IO;

/// <summary>
/// Assigns unique "Frame N" names to pasted frames so no two frames in a chain share a label.
/// Kept separate from clipboard serialization so the naming rule is unit-testable.
/// </summary>
public static class FramePasteLogic
{
    /// <summary>
    /// Assigns the next free "Frame N" name to each frame in <paramref name="pastedFrames"/>,
    /// using <paramref name="existingFrames"/> as the already-occupied name set.
    /// Always picks next-free "Frame N" regardless of the pasted frame's current name
    /// (even custom names fall back to "Frame N" — keeps it simple and consistent).
    /// </summary>
    public static void AssignUniqueNames(
        IList<AnimationFrameSave> existingFrames,
        IReadOnlyList<AnimationFrameSave> pastedFrames)
    {
        var taken = new HashSet<string>();
        foreach (var f in existingFrames)
            if (!string.IsNullOrEmpty(f.Name))
                taken.Add(f.Name);

        foreach (var frame in pastedFrames)
        {
            var name = NextFreeFrameName(taken);
            frame.Name = name;
            taken.Add(name);
        }
    }

    private static string NextFreeFrameName(HashSet<string> taken)
    {
        int i = 1;
        while (true)
        {
            var candidate = $"Frame {i}";
            if (!taken.Contains(candidate)) return candidate;
            i++;
        }
    }
}
